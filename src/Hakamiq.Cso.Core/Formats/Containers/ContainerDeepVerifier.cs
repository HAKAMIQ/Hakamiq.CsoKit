using System.Security.Cryptography;
using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Core.Formats.Containers;

public sealed class ContainerDeepVerifier
{
    public CsoDeepVerifyResult Verify(
        IBlockContainerReader reader,
        bool computeSha256)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.BlockSize == 0 || reader.BlockSize > int.MaxValue)
        {
            return CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "InvalidBlockSize",
                    $"{reader.Format} block size is invalid for deep verification.")]);
        }

        byte[] buffer = new byte[checked((int)reader.BlockSize)];
        IncrementalHash? hash = computeSha256
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;

        int blocksChecked = 0;
        ulong bytesReconstructed = 0;

        try
        {
            for (int blockIndex = 0; blockIndex < reader.BlockCount; blockIndex++)
            {
                int bytesRead = reader.ReadBlock(blockIndex, buffer);

                if (bytesRead <= 0)
                {
                    return FailAt(
                        reader,
                        blocksChecked,
                        bytesReconstructed,
                        "CorruptCompressedBlock",
                        $"{reader.Format} block {blockIndex:N0} decoded to an empty payload. Re-dump required.",
                        blockIndex);
                }

                hash?.AppendData(buffer.AsSpan(0, bytesRead));
                bytesReconstructed += checked((ulong)bytesRead);
                blocksChecked++;
            }
        }
        catch (BlockContainerReadException ex)
        {
            return FailAt(
                reader,
                blocksChecked,
                bytesReconstructed,
                ex.Code,
                ex.Message,
                ex.BlockIndex);
        }
        catch (EndOfStreamException ex)
        {
            return FailAt(
                reader,
                blocksChecked,
                bytesReconstructed,
                "UnexpectedEndOfFile",
                ex.Message,
                blocksChecked);
        }
        catch (IOException ex)
        {
            return FailAt(
                reader,
                blocksChecked,
                bytesReconstructed,
                "DeepVerifyIoFailed",
                ex.Message,
                blocksChecked);
        }
        catch (InvalidDataException ex)
        {
            return FailAt(
                reader,
                blocksChecked,
                bytesReconstructed,
                "CorruptCompressedBlock",
                ex.Message,
                blocksChecked);
        }

        if (bytesReconstructed != reader.UncompressedSize)
        {
            return CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked,
                bytesReconstructed,
                [new CsoDeepVerifyIssue(
                    "ReconstructedSizeMismatch",
                    $"{reader.Format} deep verify reconstructed {bytesReconstructed:N0} bytes, expected {reader.UncompressedSize:N0} bytes.")]);
        }

        string? sha256 = hash is null
            ? null
            : Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        return CsoDeepVerifyResult.Ok(
            header: null,
            blocksChecked,
            bytesReconstructed,
            sha256);
    }

    private static CsoDeepVerifyResult FailAt(
        IBlockContainerReader reader,
        int blocksChecked,
        ulong bytesReconstructed,
        string code,
        string message,
        int? blockIndex)
    {
        return CsoDeepVerifyResult.Fail(
            header: null,
            blocksChecked,
            bytesReconstructed,
            [new CsoDeepVerifyIssue(
                code,
                string.IsNullOrWhiteSpace(message)
                    ? $"{reader.Format} deep verification failed."
                    : message,
                blockIndex)]);
    }
}
