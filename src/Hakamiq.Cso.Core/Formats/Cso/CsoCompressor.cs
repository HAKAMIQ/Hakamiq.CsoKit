using System.Buffers.Binary;
using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoCompressor
{
    public const uint DefaultBlockSize = 2048;

    private const ulong OutputSafetyBufferBytes = 64UL * 1024UL * 1024UL;
    private const int ProgressReportBlockInterval = 256;

    private readonly CsoOutputSafetyPolicy outputSafetyPolicy = new();
    private readonly CsoDiskSpacePreflight diskSpacePreflight = new();

    public CsoCompressResult Compress(CsoCompressOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CancellationToken cancellationToken = options.CancellationToken;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            CsoOutputSafetyResult outputSafety = outputSafetyPolicy.Validate(
                options.InputPath,
                options.OutputPath,
                options.ForceOverwrite);

            if (!outputSafety.Success)
            {
                return CsoCompressResult.Fail(
                    outputSafety.ErrorCode ?? "OutputSafetyFailed",
                    outputSafety.ErrorMessage ?? "Output safety validation failed.");
            }

            if (!File.Exists(options.InputPath))
            {
                return CsoCompressResult.Fail("InputNotFound", "Input file was not found.");
            }

            if (options.BlockSize == 0)
            {
                return CsoCompressResult.Fail("InvalidBlockSize", "CSO block size is zero.");
            }

            if (options.BlockSize > CsoConstants.MaxSupportedBlockSize)
            {
                return CsoCompressResult.Fail(
                    "BlockSizeTooLarge",
                    $"CSO block size is too large. Maximum supported block size is {CsoConstants.MaxSupportedBlockSize:N0} bytes.");
            }

            FileInfo inputInfo = new(options.InputPath);

            if (inputInfo.Length <= 0)
            {
                return CsoCompressResult.Fail("InvalidInputSize", "Input ISO file is empty.");
            }

            ulong inputBytes = checked((ulong)inputInfo.Length);
            int blockSize = checked((int)options.BlockSize);
            int totalBlocks = checked((int)((inputBytes + options.BlockSize - 1) / options.BlockSize));
            ulong indexBytes = checked((ulong)(totalBlocks + 1) * sizeof(uint));
            ulong headerAndIndexBytes = checked((ulong)CsoConstants.MinimumHeaderSize + indexBytes);
            ulong requiredBytes = AddSafetyBuffer(checked(inputBytes + headerAndIndexBytes));

            CsoDiskSpacePreflightResult diskSpace = diskSpacePreflight.CheckOutputSpace(options.OutputPath, requiredBytes);

            if (!diskSpace.Success)
            {
                return CsoCompressResult.Fail(
                    diskSpace.ErrorCode ?? "DiskSpacePreflightFailed",
                    diskSpace.ErrorMessage ?? "Disk space preflight failed.");
            }

            string fullOutputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");

            string tempOutputPath = CreateUniqueTempOutputPath(fullOutputPath);

            try
            {
                CsoCompressResult result;

                using (FileStream input = new(
                    options.InputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 128,
                    FileOptions.SequentialScan))
                {
                    using FileStream output = new(
                        tempOutputPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1024 * 128,
                        FileOptions.SequentialScan);

                    result = CompressBlocks(
                        input,
                        output,
                        inputBytes,
                        blockSize,
                        totalBlocks,
                        cancellationToken,
                        options.Progress);

                    output.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();

                File.Move(tempOutputPath, fullOutputPath, overwrite: options.ForceOverwrite);

                return result;
            }
            catch (OperationCanceledException)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("OperationCanceled", "Operation was canceled.");
            }
            catch (InvalidDataException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("CompressionFailed", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("OutputAccessDenied", ex.Message);
            }
            catch (IOException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("CompressionIoFailed", ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            return CsoCompressResult.Fail("OperationCanceled", "Operation was canceled.");
        }
    }

    private static CsoCompressResult CompressBlocks(
        FileStream input,
        FileStream output,
        ulong inputBytes,
        int blockSize,
        int totalBlocks,
        CancellationToken cancellationToken,
        IProgress<CsoCompressProgress>? progress)
    {
        ulong dataStart = checked((ulong)CsoConstants.MinimumHeaderSize + ((ulong)(totalBlocks + 1) * sizeof(uint)));

        CsoIndexBuilder indexBuilder = new(totalBlocks);
        CsoOrderedOutputWriter outputWriter = new(output);
        byte[] inputBuffer = new byte[blockSize];

        outputWriter.ReserveDataStart(dataStart);

        ulong totalRead = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;

        ReportProgress(progress, completedBlocks: 0, totalBlocks, totalRead, inputBytes);

        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int expectedBytes = checked((int)Math.Min((ulong)blockSize, inputBytes - totalRead));
            int read = CsoBlockReader.ReadExactlyOrLess(input, inputBuffer.AsSpan(0, expectedBytes));

            if (read != expectedBytes)
            {
                throw new EndOfStreamException($"Unexpected end of ISO. Expected {expectedBytes:N0} bytes, got {read:N0}.");
            }

            SectorJob job = new(
                blockIndex,
                totalRead,
                read,
                inputBuffer.AsMemory(0, read));

            SectorResult sectorResult = CompressSector(job);
            ulong blockOffset = outputWriter.Position;

            indexBuilder.AddSectorOffset(blockOffset, sectorResult.IsStored);
            outputWriter.Write(sectorResult);

            if (sectorResult.IsStored)
            {
                storedBlocks++;
            }
            else
            {
                compressedBlocks++;
            }

            totalRead += (ulong)read;

            int completedBlocks = blockIndex + 1;

            if (completedBlocks == totalBlocks ||
                completedBlocks % ProgressReportBlockInterval == 0)
            {
                ReportProgress(progress, completedBlocks, totalBlocks, totalRead, inputBytes);
            }
        }

        indexBuilder.AddFinalOffset(outputWriter.Position);

        ulong bytesWritten = outputWriter.Position;

        WriteHeaderAndIndex(
            output,
            inputBytes,
            (uint)blockSize,
            indexBuilder.Entries);

        ReportProgress(progress, totalBlocks, totalBlocks, totalRead, inputBytes);

        return CsoCompressResult.Ok(
            totalRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks);
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

    private static SectorResult CompressSector(SectorJob job)
    {
        byte[] compressed = CompressRawDeflate(job.SourceSpan);
        bool storeUncompressed = compressed.Length >= job.SourceLength;

        if (storeUncompressed)
        {
            byte[] storedBuffer = job.SourceSpan.ToArray();

            return new SectorResult(
                job.BlockIndex,
                job.SourceOffset,
                job.SourceLength,
                storedBuffer.Length,
                IsStored: true,
                Method: CompressionMethod.Store,
                Level: 0,
                Buffer: storedBuffer);
        }

        return new SectorResult(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            compressed.Length,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: compressed);
    }

    private static byte[] CompressRawDeflate(ReadOnlySpan<byte> block)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
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

    private static void ReportProgress(
        IProgress<CsoCompressProgress>? progress,
        int completedBlocks,
        int totalBlocks,
        ulong bytesRead,
        ulong totalBytes)
    {
        progress?.Report(new CsoCompressProgress(
            completedBlocks,
            totalBlocks,
            bytesRead,
            totalBytes));
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