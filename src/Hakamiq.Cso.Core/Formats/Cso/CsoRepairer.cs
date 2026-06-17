using Hakamiq.Cso.Core.Formats.Containers;
using Hakamiq.Cso.Core.Formats.DiscImage;
using Hakamiq.Cso.Core.Formats.Iso;
using Hakamiq.Cso.Core.Repair;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoRepairer
{
    private const ulong RepairScratchSafetyMarginBytes = 64UL * 1024UL * 1024UL;

    public CsoRepairResult Repair(CsoRepairOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.InputPath))
        {
            return CsoRepairResult.Fail("InputNotFound", "Input file was not found.");
        }

        if (options.CollectCodecReport && options.CodecReportBlockLimit < 0)
        {
            return CsoRepairResult.Fail("InvalidCodecReportBlockLimit", "Codec report block limit cannot be negative.");
        }

        FormatDetectionResult format = new FormatDetector().Detect(options.InputPath);

        if (!format.Success)
        {
            return CsoRepairResult.Fail(
                format.ErrorCode ?? "FormatDetectionFailed",
                format.ErrorMessage ?? "Format detection failed.");
        }

        return format.Format switch
        {
            DetectedDiscFormat.Cso1 or
                DetectedDiscFormat.Cso2 or
                DetectedDiscFormat.Zso or
                DetectedDiscFormat.Dax => RepairContainer(options, format.Format),
            DetectedDiscFormat.RawIso => RepairIso(options),
            _ => CsoRepairResult.Fail(
                "RepairNotPossible",
                "Input format is not supported for safe repair.",
                format.Format.ToString()),
        };
    }

    private static CsoRepairResult RepairContainer(
        CsoRepairOptions options,
        DetectedDiscFormat format)
    {
        CsoRepairResult streamingResult = new StreamingRepairService().RepairContainer(options, format);

        if (streamingResult.Success || streamingResult.ErrorCode != "StreamingRepairUnsupported")
        {
            return streamingResult;
        }

        return RepairContainerViaTempIso(options, format, streamingResult.ErrorMessage ?? "Streaming repair is not supported for this input.");
    }

    private static CsoRepairResult RepairContainerViaTempIso(
        CsoRepairOptions options,
        DetectedDiscFormat format,
        string fallbackReason)
    {
        string tempIso = CreateTempPath(options.OutputPath, ".repair.iso");
        string? paddedIso = null;

        try
        {
            using IBlockContainerReader reader = CreateContainerReader(options.InputPath, format);
            CsoRepairResult? diskSpaceFailure = CheckRepairScratchSpace(
                reader,
                options.OutputPath,
                format);

            if (diskSpaceFailure is not null)
            {
                return diskSpaceFailure;
            }

            DecodeContainerToIso(reader, tempIso, options.CancellationToken);

            FileInfo decodedInfo = new(tempIso);
            IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(decodedInfo.Length, options.PadLastSector);

            if (!alignment.Success)
            {
                return CsoRepairResult.Fail(
                    alignment.ErrorCode ?? "IsoAlignmentFailed",
                    alignment.ErrorMessage ?? "Decoded ISO alignment validation failed.",
                    format.ToString());
            }

            string isoPath = tempIso;

            if (alignment.PaddingBytes > 0)
            {
                paddedIso = CreateTempPath(options.OutputPath, ".padded.iso");
                CopyWithPadding(tempIso, paddedIso, alignment.PaddingBytes);
                isoPath = paddedIso;
            }

            return CompressIsoToGameSafe(
                isoPath,
                options.OutputPath,
                options.ForceOverwrite,
                options.Profile,
                options.DeepVerify,
                options.CollectCodecReport,
                options.CodecReportBlockLimit,
                options.CancellationToken,
                inputFormat: format.ToString(),
                alignment.PaddingBytes);
        }
        catch (BlockContainerReadException ex)
        {
            return CsoRepairResult.Fail(
                MapContainerReadError(ex),
                $"{ex.Code}: {ex.Message}",
                format.ToString());
        }
        catch (InvalidDataException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString());
        }
        catch (IOException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString());
        }
        finally
        {
            SafeDelete(tempIso);

            if (paddedIso is not null)
            {
                SafeDelete(paddedIso);
            }
        }
    }

    private static CsoRepairResult RepairIso(CsoRepairOptions options)
    {
        FileInfo inputInfo = new(options.InputPath);
        IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(inputInfo.Length, options.PadLastSector);

        if (!alignment.Success)
        {
            return CsoRepairResult.Fail(
                alignment.ErrorCode ?? "IsoAlignmentFailed",
                alignment.ErrorMessage ?? "ISO alignment validation failed.",
                "RawIso");
        }

        string isoPath = options.InputPath;
        string? paddedIso = null;

        try
        {
            if (alignment.PaddingBytes > 0)
            {
                paddedIso = CreateTempPath(options.OutputPath, ".padded.iso");
                CopyWithPadding(options.InputPath, paddedIso, alignment.PaddingBytes);
                isoPath = paddedIso;
            }

            return CompressIsoToGameSafe(
                isoPath,
                options.OutputPath,
                options.ForceOverwrite,
                options.Profile,
                options.DeepVerify,
                options.CollectCodecReport,
                options.CodecReportBlockLimit,
                options.CancellationToken,
                inputFormat: "RawIso",
                alignment.PaddingBytes);
        }
        finally
        {
            if (paddedIso is not null)
            {
                SafeDelete(paddedIso);
            }
        }
    }

    private static CsoRepairResult CompressIsoToGameSafe(
        string isoPath,
        string outputPath,
        bool forceOverwrite,
        CsoCompressionProfile profile,
        bool deepVerify,
        bool collectCodecReport,
        int codecReportBlockLimit,
        CancellationToken cancellationToken,
        string inputFormat,
        long paddingBytes)
    {
        CsoCompressResult compress = new CsoCompressor().Compress(
            new CsoCompressOptions(
                isoPath,
                outputPath,
                forceOverwrite,
                CsoCompressor.DefaultBlockSize,
                cancellationToken,
                Progress: null,
                Profile: profile,
                WorkerCount: Math.Max(1, Environment.ProcessorCount),
                UseZopfli: false,
                DeepVerifyOutput: deepVerify,
                CollectCodecReport: collectCodecReport,
                CodecReportBlockLimit: codecReportBlockLimit));

        if (!compress.Success)
        {
            return CsoRepairResult.Fail(
                compress.ErrorCode ?? "RepairNotPossible",
                compress.ErrorMessage ?? "Repair compression failed.",
                inputFormat);
        }

        return CsoRepairResult.Ok(
            inputFormat,
            compress.BytesRead,
            compress.BytesWritten,
            paddingBytes,
            mode: "temp-iso-fallback",
            usedTempIso: true,
            codecTrialSummary: compress.CodecTrialSummary);
    }

    private static CsoRepairResult? CheckRepairScratchSpace(
        IBlockContainerReader reader,
        string outputPath,
        DetectedDiscFormat format)
    {
        ulong requiredBytes;

        try
        {
            requiredBytes = checked(reader.UncompressedSize + reader.UncompressedSize + RepairScratchSafetyMarginBytes);
        }
        catch (OverflowException)
        {
            return CsoRepairResult.Fail(
                "DiskSpacePreflightFailed",
                $"{format} repair scratch-space estimate overflowed. Input is too large for safe normalization.",
                format.ToString());
        }

        CsoDiskSpacePreflightResult diskSpace = new CsoDiskSpacePreflight().CheckOutputSpace(
            outputPath,
            requiredBytes);

        if (diskSpace.Success)
        {
            return null;
        }

        return CsoRepairResult.Fail(
            "DiskSpacePreflightFailed",
            diskSpace.ErrorMessage ?? "Repair scratch-space preflight failed.",
            format.ToString());
    }

    private static void CopyWithPadding(string inputPath, string outputPath, long paddingBytes)
    {
        using FileStream input = new(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        input.CopyTo(output);

        if (paddingBytes > 0)
        {
            output.Write(new byte[checked((int)paddingBytes)]);
        }

        output.Flush(true);
    }

    private static IBlockContainerReader CreateContainerReader(
        string inputPath,
        DetectedDiscFormat format)
    {
        return format switch
        {
            DetectedDiscFormat.Cso1 => new Cso1ContainerReader(inputPath),
            DetectedDiscFormat.Cso2 => new Cso2ContainerReader(inputPath),
            DetectedDiscFormat.Zso => new ZsoContainerReader(inputPath),
            DetectedDiscFormat.Dax => new DaxContainerReader(inputPath),
            _ => throw new BlockContainerReadException(
                "UnsupportedContainer",
                $"{format} is not supported for safe normalization.")
        };
    }

    private static void DecodeContainerToIso(
        IBlockContainerReader reader,
        string outputIsoPath,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[checked((int)reader.BlockSize)];
        ulong totalWritten = 0;

        using FileStream output = new(
            outputIsoPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 128,
            FileOptions.SequentialScan);

        for (int blockIndex = 0; blockIndex < reader.BlockCount; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesRead = reader.ReadBlock(blockIndex, buffer);

            if (bytesRead <= 0)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"{reader.Format} block {blockIndex} decoded to an empty payload. Re-dump required.",
                    blockIndex);
            }

            output.Write(buffer, 0, bytesRead);
            totalWritten += (ulong)bytesRead;
        }

        if (totalWritten != reader.UncompressedSize)
        {
            throw new BlockContainerReadException(
                "RepairNotPossible",
                $"{reader.Format} decoded size mismatch. Written {totalWritten:N0} bytes, expected {reader.UncompressedSize:N0} bytes.");
        }

        output.Flush(true);
    }

    private static string MapContainerReadError(BlockContainerReadException exception)
    {
        return exception.Code switch
        {
            "UnsupportedContainer" => "UnsupportedContainer",
            "CorruptCompressedBlock" or
                "UnexpectedEndOfFile" or
                "IndexOffsetPastEndOfFile" or
                "FinalOffsetPastEndOfFile" or
                "StoredBlockTooSmall" or
                "InvalidCompressedBlockSize" or
                "IndexOffsetsNotMonotonic" => "ReDumpRequired",
            _ => exception.Code,
        };
    }

    private static string CreateTempPath(string outputPath, string suffix)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
        string fileName = Path.GetFileName(outputPath);

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}{suffix}");
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
