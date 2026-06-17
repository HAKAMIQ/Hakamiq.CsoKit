using Hakamiq.Cso.Core.Formats.Containers;
using Hakamiq.Cso.Core.Formats.Cso;
using Hakamiq.Cso.Core.Formats.DiscImage;

namespace Hakamiq.Cso.Core.Repair;

public sealed class StreamingRepairService
{
    public CsoRepairResult RepairContainer(
        CsoRepairOptions options,
        DetectedDiscFormat format)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            using IBlockContainerReader reader = CreateContainerReader(options.InputPath, format);
            CsoCompressResult write = new Cso1Writer().WriteFromContainer(
                options.InputPath,
                reader,
                options.OutputPath,
                options.ForceOverwrite,
                options.Profile,
                options.DeepVerify || options.Profile == CsoCompressionProfile.GameSafe,
                options.CancellationToken);

            if (!write.Success)
            {
                return CsoRepairResult.Fail(
                    NormalizeWriteError(write.ErrorCode),
                    write.ErrorMessage ?? "Streaming repair failed.",
                    format.ToString(),
                    mode: RepairMode.Streaming.ToString(),
                    usedTempIso: false);
            }

            return CsoRepairResult.Ok(
                format.ToString(),
                write.BytesRead,
                write.BytesWritten,
                paddingBytes: 0,
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
        catch (BlockContainerReadException ex)
        {
            return CsoRepairResult.Fail(
                MapContainerReadError(ex),
                $"{ex.Code}: {ex.Message}",
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
        catch (InvalidDataException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
        catch (IOException ex)
        {
            return CsoRepairResult.Fail(
                "IoError",
                ex.Message,
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
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
                $"{format} is not supported for streaming repair.")
        };
    }

    private static string NormalizeWriteError(string? errorCode)
    {
        return errorCode switch
        {
            "CsoDeepVerifyFailed" or "VerificationFailed" => "VerificationFailed",
            "NativeZopfliUnavailable" => "NativeCodecUnavailable",
            "CompressionIoFailed" => "IoError",
            null or "" => "RepairNotPossible",
            _ => errorCode,
        };
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
}
