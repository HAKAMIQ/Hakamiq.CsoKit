using System.Buffers.Binary;
using Hakamiq.Cso.Core.Formats.Containers;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class Cso1Writer
{
    private const ulong OutputSafetyBufferBytes = 64UL * 1024UL * 1024UL;

    private readonly CsoOutputSafetyPolicy outputSafetyPolicy = new();
    private readonly CsoDiskSpacePreflight diskSpacePreflight = new();

    public CsoCompressResult WriteFromContainer(
        string inputPath,
        IBlockContainerReader reader,
        string outputPath,
        bool forceOverwrite,
        CsoCompressionProfile profile = CsoCompressionProfile.GameSafe,
        bool deepVerify = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        cancellationToken.ThrowIfCancellationRequested();

        CsoOutputSafetyResult outputSafety = outputSafetyPolicy.Validate(
            inputPath,
            outputPath,
            forceOverwrite);

        if (!outputSafety.Success)
        {
            return CsoCompressResult.Fail(
                outputSafety.ErrorCode ?? "OutputSafetyFailed",
                outputSafety.ErrorMessage ?? "Output safety validation failed.");
        }

        if (reader.UncompressedSize == 0)
        {
            return CsoCompressResult.Fail("InvalidInputSize", "Readable container has an empty logical payload.");
        }

        uint targetBlockSize = CsoCompressor.DefaultBlockSize;
        int targetBlockSizeInt = checked((int)targetBlockSize);
        int totalOutputBlocks = checked((int)((reader.UncompressedSize + targetBlockSize - 1) / targetBlockSize));
        ulong indexBytes = checked((ulong)(totalOutputBlocks + 1) * sizeof(uint));
        ulong headerAndIndexBytes = checked((ulong)CsoConstants.MinimumHeaderSize + indexBytes);
        byte indexShift = CsoIndexShiftPolicy.ComputeShift(checked(reader.UncompressedSize + headerAndIndexBytes));

        if (indexShift > 0)
        {
            return CsoCompressResult.Fail(
                "IndexShiftRequired",
                "Streaming CSO1 repair keeps index_shift at 0 for game-safe output; this logical image is too large for the current CSO1 writer.");
        }

        ulong requiredBytes = AddSafetyBuffer(checked(reader.UncompressedSize + headerAndIndexBytes));
        CsoDiskSpacePreflightResult diskSpace = diskSpacePreflight.CheckOutputSpace(outputPath, requiredBytes);

        if (!diskSpace.Success)
        {
            return CsoCompressResult.Fail(
                diskSpace.ErrorCode ?? "DiskSpacePreflightFailed",
                diskSpace.ErrorMessage ?? "Disk space preflight failed.");
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string tempOutputPath = CreateUniqueTempOutputPath(fullOutputPath);

        try
        {
            CsoCompressResult result;

            using (FileStream output = new(
                tempOutputPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1024 * 128,
                FileOptions.SequentialScan))
            {
                result = WriteBlocks(
                    reader,
                    output,
                    targetBlockSizeInt,
                    totalOutputBlocks,
                    profile,
                    cancellationToken);

                output.Flush(true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (deepVerify || profile == CsoCompressionProfile.GameSafe)
            {
                CsoDeepVerifyResult deepVerifyResult = new CsoDeepVerifier().Verify(
                    tempOutputPath,
                    computeSha256: false);

                if (!deepVerifyResult.Success)
                {
                    CsoDeepVerifyIssue? issue = deepVerifyResult.Issues.FirstOrDefault();
                    SafeDelete(tempOutputPath);

                    return CsoCompressResult.Fail(
                        issue?.Code ?? "VerificationFailed",
                        issue?.Message ?? "Streaming repair output failed CSO deep verification.");
                }
            }

            File.Move(tempOutputPath, fullOutputPath, overwrite: forceOverwrite);
            return result;
        }
        catch (OperationCanceledException)
        {
            SafeDelete(tempOutputPath);
            return CsoCompressResult.Fail("OperationCanceled", "Operation was canceled.");
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeDelete(tempOutputPath);
            return CsoCompressResult.Fail("OutputAccessDenied", ex.Message);
        }
        catch (InvalidDataException ex)
        {
            SafeDelete(tempOutputPath);
            return CsoCompressResult.Fail("CompressionFailed", ex.Message);
        }
        catch (IOException ex)
        {
            SafeDelete(tempOutputPath);
            return CsoCompressResult.Fail("IoError", ex.Message);
        }
        catch
        {
            SafeDelete(tempOutputPath);
            throw;
        }
    }

    private static CsoCompressResult WriteBlocks(
        IBlockContainerReader reader,
        FileStream output,
        int targetBlockSize,
        int totalOutputBlocks,
        CsoCompressionProfile profile,
        CancellationToken cancellationToken)
    {
        ulong dataStart = checked((ulong)CsoConstants.MinimumHeaderSize + ((ulong)(totalOutputBlocks + 1) * sizeof(uint)));
        CsoIndexBuilder indexBuilder = new(totalOutputBlocks, indexShift: 0);
        CsoOrderedOutputWriter outputWriter = new(output);
        CsoCompressionWorker compressionWorker = new(profile, useZopfli: false);

        outputWriter.ReserveDataStart(dataStart);

        byte[] readerBuffer = new byte[checked((int)reader.BlockSize)];
        byte[] outputBlock = new byte[targetBlockSize];
        int outputBlockFill = 0;
        int outputBlockIndex = 0;
        ulong logicalBytesRead = 0;
        ulong outputBlockSourceOffset = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;
        Dictionary<string, int> codecWins = new(StringComparer.OrdinalIgnoreCase);

        for (int readerBlockIndex = 0; readerBlockIndex < reader.BlockCount; readerBlockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int decodedBytes = reader.ReadBlock(readerBlockIndex, readerBuffer);

            if (decodedBytes <= 0)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"{reader.Format} block {readerBlockIndex} decoded to an empty payload. Re-dump required.",
                    readerBlockIndex);
            }

            if (logicalBytesRead + (ulong)decodedBytes > reader.UncompressedSize)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"{reader.Format} block {readerBlockIndex} decoded past the declared logical size. Re-dump required.",
                    readerBlockIndex);
            }

            ReadOnlySpan<byte> decoded = readerBuffer.AsSpan(0, decodedBytes);

            while (!decoded.IsEmpty)
            {
                int copy = Math.Min(targetBlockSize - outputBlockFill, decoded.Length);
                decoded[..copy].CopyTo(outputBlock.AsSpan(outputBlockFill));
                outputBlockFill += copy;
                logicalBytesRead += (ulong)copy;
                decoded = decoded[copy..];

                if (outputBlockFill == targetBlockSize)
                {
                    WriteOneOutputBlock(
                        outputBlock,
                        outputBlockFill,
                        outputBlockIndex,
                        outputBlockSourceOffset,
                        compressionWorker,
                        indexBuilder,
                        outputWriter,
                        codecWins,
                        ref compressedBlocks,
                        ref storedBlocks);

                    outputBlockIndex++;
                    outputBlockSourceOffset = logicalBytesRead;
                    outputBlockFill = 0;
                }
            }
        }

        if (logicalBytesRead != reader.UncompressedSize)
        {
            throw new BlockContainerReadException(
                "CorruptCompressedBlock",
                $"{reader.Format} decoded size mismatch. Read {logicalBytesRead:N0} bytes, expected {reader.UncompressedSize:N0} bytes.");
        }

        if (outputBlockFill > 0)
        {
            WriteOneOutputBlock(
                outputBlock,
                outputBlockFill,
                outputBlockIndex,
                outputBlockSourceOffset,
                compressionWorker,
                indexBuilder,
                outputWriter,
                codecWins,
                ref compressedBlocks,
                ref storedBlocks);

            outputBlockIndex++;
        }

        if (outputBlockIndex != totalOutputBlocks)
        {
            throw new InvalidDataException($"Streaming writer produced {outputBlockIndex:N0} blocks, expected {totalOutputBlocks:N0}.");
        }

        indexBuilder.AddFinalOffset(outputWriter.Position);
        ulong bytesWritten = outputWriter.Position;

        WriteHeaderAndIndex(
            output,
            reader.UncompressedSize,
            checked((uint)targetBlockSize),
            indexBuilder.Entries);

        return CsoCompressResult.Ok(
            logicalBytesRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks,
            codecWins);
    }

    private static void WriteOneOutputBlock(
        byte[] outputBlock,
        int sourceLength,
        int outputBlockIndex,
        ulong sourceOffset,
        CsoCompressionWorker compressionWorker,
        CsoIndexBuilder indexBuilder,
        CsoOrderedOutputWriter outputWriter,
        Dictionary<string, int> codecWins,
        ref int compressedBlocks,
        ref int storedBlocks)
    {
        SectorJob job = new(
            outputBlockIndex,
            sourceOffset,
            sourceLength,
            outputBlock.AsMemory(0, sourceLength));

        SectorResult sectorResult = compressionWorker.Compress(job);
        indexBuilder.AddSectorOffset(outputWriter.Position, sectorResult.IsStored);
        outputWriter.Write(sectorResult);

        if (sectorResult.IsStored)
        {
            storedBlocks++;
        }
        else
        {
            compressedBlocks++;
        }

        string codecName = sectorResult.EffectiveCodecName;
        codecWins[codecName] = codecWins.TryGetValue(codecName, out int current)
            ? checked(current + 1)
            : 1;
    }

    private static void WriteHeaderAndIndex(
        FileStream output,
        ulong uncompressedSize,
        uint blockSize,
        IReadOnlyList<uint> indexEntries)
    {
        Span<byte> header = stackalloc byte[CsoConstants.MinimumHeaderSize];

        header[0] = (byte)'C';
        header[1] = (byte)'I';
        header[2] = (byte)'S';
        header[3] = (byte)'O';

        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), CsoConstants.MinimumHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), blockSize);
        header[20] = 1;
        header[21] = 0;
        header[22] = 0;
        header[23] = 0;

        output.Position = 0;
        output.Write(header);

        Span<byte> rawEntry = stackalloc byte[sizeof(uint)];

        foreach (uint entry in indexEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rawEntry, entry);
            output.Write(rawEntry);
        }
    }

    private static ulong AddSafetyBuffer(ulong requiredBytes)
    {
        if (requiredBytes > ulong.MaxValue - OutputSafetyBufferBytes)
        {
            return ulong.MaxValue;
        }

        return requiredBytes + OutputSafetyBufferBytes;
    }

    private static string CreateUniqueTempOutputPath(string fullOutputPath)
    {
        string directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        string fileName = Path.GetFileName(fullOutputPath);

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
