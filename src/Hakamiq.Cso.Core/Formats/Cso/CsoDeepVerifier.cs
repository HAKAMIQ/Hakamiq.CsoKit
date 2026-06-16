using System.Security.Cryptography;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoDeepVerifier
{
    private readonly CsoVerifier verifier = new();

    public CsoDeepVerifyResult Verify(string inputPath, bool computeSha256)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InvalidInputPath", "Input path is empty.")]);
        }

        if (!File.Exists(inputPath))
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputNotFound", "Input file was not found.")]);
        }

        CsoVerificationResult shallow = verifier.Verify(inputPath);

        if (!shallow.Success || shallow.Header is null || shallow.Entries.Count == 0)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                shallow.Issues
                    .Select(issue => new CsoDeepVerifyIssue(issue.Code, issue.Message, issue.BlockIndex))
                    .ToArray());
        }

        if (shallow.Header.Version is not (0 or 1))
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "UnsupportedContainer",
                    "CsoDeepVerifier is limited to CSO1 semantics. Use ContainerDeepVerifier for CSO2/ZSO/DAX input.")]);
        }

        try
        {
            using FileStream input = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 128,
                FileOptions.SequentialScan);

            return Verify(input, shallow.Header, shallow.Entries, computeSha256);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("DeepVerifyIoFailed", ex.Message)]);
        }
    }

    public CsoDeepVerifyResult Verify(
        Stream input,
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries,
        bool computeSha256)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(entries);

        if (!input.CanRead || !input.CanSeek)
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("StreamNotSeekable", "Deep verification requires a readable seekable stream.")]);
        }

        if (header.Version is not (0 or 1))
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "UnsupportedContainer",
                    "CsoDeepVerifier is limited to CSO1 semantics. Use ContainerDeepVerifier for CSO2/ZSO/DAX input.")]);
        }

        if (entries.Count != header.IndexEntryCount)
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "IndexEntryCountMismatch",
                    $"Expected {header.IndexEntryCount:N0} index entries, got {entries.Count:N0}.")]);
        }

        if (entries[^1].HasFlag)
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("FinalIndexEntryHasFlag", "Final CSO index entry must not carry the stored-block flag.")]);
        }

        if (entries[^1].Offset != (ulong)input.Length)
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "FinalOffsetMismatch",
                    $"Final CSO offset is {entries[^1].Offset:N0}, but file length is {input.Length:N0}.")]);
        }

        int blockSize = checked((int)header.BlockSize);
        byte[] outputBuffer = new byte[blockSize];
        IncrementalHash? hash = computeSha256
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;

        int blocksChecked = 0;
        ulong bytesReconstructed = 0;

        try
        {
            for (int blockIndex = 0; blockIndex < header.SectorCount; blockIndex++)
            {
                CsoIndexEntry current = entries[blockIndex];
                CsoIndexEntry next = entries[blockIndex + 1];

                if (next.Offset < current.Offset)
                {
                    return FailAt(header, blocksChecked, bytesReconstructed, "IndexOffsetsNotMonotonic", blockIndex);
                }

                ulong remaining = header.UncompressedSize - ((ulong)blockIndex * header.BlockSize);
                int expectedBytes = checked((int)Math.Min(header.BlockSize, remaining));
                ulong physicalSize = next.Offset - current.Offset;

                if (expectedBytes <= 0)
                {
                    return FailAt(header, blocksChecked, bytesReconstructed, "InvalidExpectedBlockSize", blockIndex);
                }

                if (current.Offset > (ulong)input.Length)
                {
                    return FailAt(header, blocksChecked, bytesReconstructed, "IndexOffsetPastEndOfFile", blockIndex);
                }

                input.Position = checked((long)current.Offset);

                if (current.HasFlag)
                {
                    if (physicalSize < (ulong)expectedBytes)
                    {
                        return FailAt(header, blocksChecked, bytesReconstructed, "StoredBlockTooSmall", blockIndex);
                    }

                    ReadExactly(input, outputBuffer.AsSpan(0, expectedBytes));
                }
                else
                {
                    if (physicalSize == 0 || physicalSize > int.MaxValue)
                    {
                        return FailAt(header, blocksChecked, bytesReconstructed, "InvalidCompressedBlockSize", blockIndex);
                    }

                    byte[] compressed = new byte[(int)physicalSize];
                    ReadExactly(input, compressed);

                    if (!RawDeflateVerifier.TryInflate(compressed, outputBuffer.AsSpan(0, expectedBytes), out int bytesWritten) ||
                        bytesWritten != expectedBytes)
                    {
                        return FailAt(header, blocksChecked, bytesReconstructed, "CorruptCompressedBlock", blockIndex);
                    }
                }

                hash?.AppendData(outputBuffer.AsSpan(0, expectedBytes));
                bytesReconstructed += (ulong)expectedBytes;
                blocksChecked++;
            }
        }
        catch (EndOfStreamException)
        {
            return FailAt(header, blocksChecked, bytesReconstructed, "CsoDeepVerifyFailed", blocksChecked);
        }

        if (bytesReconstructed != header.UncompressedSize)
        {
            return CsoDeepVerifyResult.Fail(
                header,
                blocksChecked,
                bytesReconstructed,
                [new CsoDeepVerifyIssue(
                    "ReconstructedSizeMismatch",
                    $"Deep verify reconstructed {bytesReconstructed:N0} bytes, expected {header.UncompressedSize:N0}.")]);
        }

        string? sha256 = hash is null
            ? null
            : Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        return CsoDeepVerifyResult.Ok(header, blocksChecked, bytesReconstructed, sha256);
    }

    private static CsoDeepVerifyResult FailAt(
        CsoHeader header,
        int blocksChecked,
        ulong bytesReconstructed,
        string code,
        int blockIndex)
    {
        string message = code switch
        {
            "IndexOffsetsNotMonotonic" => $"CSO index offsets are not monotonic at block {blockIndex:N0}.",
            "StoredBlockTooSmall" => $"Stored CSO block {blockIndex:N0} is smaller than the expected sector payload.",
            "InvalidCompressedBlockSize" => $"Compressed CSO block {blockIndex:N0} has an invalid physical size.",
            "CorruptCompressedBlock" => $"Compressed CSO block {blockIndex:N0} could not be inflated as raw deflate. Diagnosis: Re-dump required.",
            "IndexOffsetPastEndOfFile" => $"CSO block {blockIndex:N0} points past the end of file.",
            _ => $"CSO deep verification failed at block {blockIndex:N0}.",
        };

        return CsoDeepVerifyResult.Fail(
            header,
            blocksChecked,
            bytesReconstructed,
            [new CsoDeepVerifyIssue(code, message, blockIndex)]);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            totalRead += read;
        }
    }
}
