using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hakamiq.Cso.Core.Formats.Containers;
using Hakamiq.Cso.Core.Formats.Cso;
using Hakamiq.Cso.Core.Formats.DiscImage;
using Hakamiq.Cso.Core.Formats.Iso;

namespace Hakamiq.Cso.App.Services;

public static class CsoUiOperationService
{
    private static readonly CultureInfo ReportCulture = CultureInfo.InvariantCulture;

    public static CsoUiOperationResult Detect(string inputPath)
    {
        FormatDetectionResult result = FormatDetector.Detect(inputPath);
        StringBuilder details = new();

        details.AppendLine($"Input: {SafeFullPath(inputPath)}");

        if (result.Success)
        {
            details.AppendLine($"Format: {result.Format}");
            details.AppendLine($"Magic: {ValueOrDash(result.Magic)}");
            details.AppendLine($"Header size: {ValueOrDash(result.HeaderSize)}");
            details.AppendLine($"Uncompressed size: {ValueOrDash(result.UncompressedSize)}");
            details.AppendLine($"Block size: {ValueOrDash(result.BlockSize)}");
            details.AppendLine($"Index shift: {ValueOrDash(result.IndexShift)}");
            details.AppendLine($"Sector count: {ValueOrDash(result.SectorCount)}");
            AppendWarnings(details, result.Warnings);

            return CsoUiOperationResult.Ok("Detect completed", details.ToString());
        }

        AppendError(details, result.ErrorCode, result.ErrorMessage);
        return CsoUiOperationResult.Fail("Detect failed", details.ToString());
    }

    public static CsoUiOperationResult Analyze(string inputPath)
    {
        PspIsoValidationResult result = PspIsoValidator.Validate(inputPath, allowPadding: false);
        StringBuilder details = new();

        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine($"Bytes: {result.InputBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"ISO9660 PVD: {result.HasIso9660PrimaryVolumeDescriptor}");
        details.AppendLine($"PSP_GAME: {result.HasPspGame}");
        details.AppendLine($"UMD_DATA.BIN: {result.HasUmdDataBin}");
        details.AppendLine($"PARAM.SFO: {result.HasParamSfo}");
        details.AppendLine($"EBOOT.BIN: {result.HasEbootBin}");

        if (!string.IsNullOrWhiteSpace(result.Title))
        {
            details.AppendLine($"Title: {result.Title}");
        }

        if (!string.IsNullOrWhiteSpace(result.DiscIdFromUmdData))
        {
            details.AppendLine($"DISC_ID UMD_DATA: {result.DiscIdFromUmdData}");
        }

        if (!string.IsNullOrWhiteSpace(result.DiscIdFromParamSfo))
        {
            details.AppendLine($"DISC_ID PARAM.SFO: {result.DiscIdFromParamSfo}");
        }

        AppendWarnings(details, result.Warnings);

        if (result.Issues.Count > 0)
        {
            details.AppendLine();
            details.AppendLine("Issues:");

            foreach (PspIsoValidationIssue issue in result.Issues)
            {
                details.AppendLine($"- {issue.Code}: {issue.Message}");
            }
        }

        return result.Success
            ? CsoUiOperationResult.Ok("Analyze completed", details.ToString())
            : CsoUiOperationResult.Fail("Analyze failed", details.ToString());
    }

    public static CsoUiOperationResult Measure(
        string inputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        IProgress<double>? progress)
    {
        CsoMeasureResult result = new CsoMeasureEstimator().Measure(
            new CsoMeasureOptions(
                InputPath: inputPath,
                BlockSize: blockSize,
                Progress: CreateCompressProgress(progress),
                Profile: profile,
                UseZopfli: false));

        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine($"Profile: {CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.AppendLine($"Block size: {blockSize.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Original bytes: {result.OriginalBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Estimated bytes: {result.EstimatedBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Estimated ratio: {result.EstimatedRatio:P2}");
        details.AppendLine($"Compressed blocks: {result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Stored blocks: {result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoUiOperationResult.Ok("Measure completed", details.ToString())
            : CsoUiOperationResult.Fail("Measure failed", details.ToString());
    }

    public static CsoUiOperationResult Compress(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        int workerCount,
        bool forceOverwrite,
        bool deepVerifyOutput,
        bool collectCodecReport,
        IProgress<double>? progress)
    {
        CsoCompressResult result = new CsoCompressor().Compress(
            new CsoCompressOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                BlockSize: blockSize,
                Progress: CreateCompressProgress(progress),
                Profile: profile,
                WorkerCount: workerCount,
                UseZopfli: false,
                DeepVerifyOutput: deepVerifyOutput,
                CollectCodecReport: collectCodecReport));

        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine($"Output: {SafeFullPath(outputPath)}");
        details.AppendLine($"Profile: {CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.AppendLine($"Block size: {blockSize.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Threads: {workerCount.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Bytes read: {result.BytesRead.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Bytes written: {result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Compressed blocks: {result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Stored blocks: {result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Zero-content blocks: {result.ZeroBlocks.ToString("N0", ReportCulture)}");
        AppendCodecWins(details, result.EffectiveCodecWins);
        AppendCodecReport(details, result.CodecTrialSummary is null ? 0 : result.CodecTrialSummary.BlocksReported);

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoUiOperationResult.Ok("Compress completed", details.ToString())
            : CsoUiOperationResult.Fail("Compress failed", details.ToString());
    }

    public static CsoUiOperationResult Decompress(
        string inputPath,
        string outputPath,
        bool forceOverwrite,
        IProgress<double>? progress)
    {
        CsoDecompressResult result = new CsoDecompressor().Decompress(
            new CsoDecompressOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                Progress: CreateDecompressProgress(progress)));

        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine($"Output: {SafeFullPath(outputPath)}");
        details.AppendLine($"Bytes written: {result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoUiOperationResult.Ok("Decompress completed", details.ToString())
            : CsoUiOperationResult.Fail("Decompress failed", details.ToString());
    }

    public static CsoUiOperationResult Verify(
        string inputPath,
        bool deepVerify,
        bool computeSha256,
        IProgress<double>? progress)
    {
        CsoUiOperationResult result = deepVerify
            ? VerifyDeep(inputPath, computeSha256)
            : VerifyShallow(inputPath);

        progress?.Report(100);
        return result;
    }

    public static CsoUiOperationResult Repair(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        bool forceOverwrite,
        bool deepVerify,
        bool collectCodecReport,
        IProgress<double>? progress)
    {
        CsoRepairResult result = CsoRepairer.Repair(
            new CsoRepairOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                Profile: profile,
                PadLastSector: false,
                DeepVerify: deepVerify,
                CollectCodecReport: collectCodecReport,
                Progress: CreateCompressProgress(progress)));

        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine($"Output: {SafeFullPath(outputPath)}");
        details.AppendLine($"Input format: {result.InputFormat}");
        details.AppendLine($"Profile: {CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.AppendLine($"Repair mode: {result.RepairMode}");
        details.AppendLine($"Corruption detected: {result.CorruptionDetected}");
        details.AppendLine($"Input verification: {result.InputVerificationStatus}");
        details.AppendLine($"Output verification: {result.OutputVerificationStatus}");
        details.AppendLine($"Action taken: {result.ActionTaken}");
        details.AppendLine($"Conclusion: {result.Conclusion}");
        details.AppendLine($"Bytes read: {result.BytesRead.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Bytes written: {result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Padding bytes: {result.PaddingBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Compressed blocks: {result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Stored blocks: {result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.AppendLine($"Zero-content blocks: {result.ZeroBlocks.ToString("N0", ReportCulture)}");
        AppendRepairIssues(details, "Input verification issues", result.EffectiveInputIssues);
        AppendRepairIssues(details, "Output verification issues", result.EffectiveOutputIssues);
        AppendCodecReport(details, result.CodecTrialSummary is null ? 0 : result.CodecTrialSummary.BlocksReported);

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoUiOperationResult.Ok(CreateRepairSuccessStatus(result), details.ToString())
            : CsoUiOperationResult.Fail(CreateRepairFailureStatus(result), details.ToString());
    }

    public static string CreateSuggestedCompressOutputPath(string inputPath)
    {
        return new CsoOutputPathPolicy().CreateCompressionOutputPath(inputPath);
    }

    public static string CreateSuggestedDecompressOutputPath(string inputPath)
    {
        return new CsoOutputPathPolicy().CreateDecompressionOutputPath(inputPath);
    }

    public static string CreateSuggestedRepairOutputPath(string inputPath)
    {
        string fullInputPath = Path.GetFullPath(inputPath);
        string directory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string baseName = NormalizeUserVisibleBaseName(Path.GetFileNameWithoutExtension(fullInputPath));

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "repaired";
        }

        string candidate = Path.Combine(directory, $"{baseName}.repaired.cso");

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        for (int number = 2; number < int.MaxValue; number++)
        {
            candidate = Path.Combine(directory, $"{baseName}.repaired-{number}.cso");

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create a unique repair output file name.");
    }

    private static string NormalizeUserVisibleBaseName(string baseName)
    {
        string normalized = baseName.Trim();

        while (normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(" - Hakamiq Repaired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^".repaired".Length].Trim()
                : normalized[..^" - Hakamiq Repaired".Length].Trim();
        }

        return normalized;
    }

    private static CsoUiOperationResult VerifyShallow(string inputPath)
    {
        FormatDetectionResult detected = FormatDetector.Detect(inputPath);
        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine("Verification type: Shallow");
        details.AppendLine("Output written: False");
        details.AppendLine("Action taken: Header and index metadata were inspected only; compressed block payloads were not decompressed.");

        if (!detected.Success)
        {
            details.AppendLine("Result: Failed");
            details.AppendLine("Corruption detected: Unknown");
            details.AppendLine("Conclusion: The input could not be identified as a supported CSO-like file. No corruption verdict was produced.");
            AppendError(details, detected.ErrorCode, detected.ErrorMessage);

            return CsoUiOperationResult.Fail("Verify failed; input format was not recognized", details.ToString());
        }

        details.AppendLine($"Format: {detected.Format}");

        if (detected.Format is not (DetectedDiscFormat.Cso1 or DetectedDiscFormat.Cso2))
        {
            details.AppendLine("Result: Failed");
            details.AppendLine("Corruption detected: Unknown");
            details.AppendLine($"Conclusion: Shallow verify supports CSO1/CSO2 only. Detected format: {detected.Format}. Use Deep verify for ZSO/DAX.");

            return CsoUiOperationResult.Fail("Verify failed; unsupported shallow format", details.ToString());
        }

        CsoVerificationResult result = new CsoVerifier().Verify(inputPath);
        details.AppendLine($"Result: {(result.Success ? "Passed" : "Failed")}");
        details.AppendLine($"Corruption detected: {FormatBoolean(!result.Success)}");
        details.AppendLine("Repair needed: Not determined by shallow verify");

        if (result.Header is not null)
        {
            details.AppendLine($"Version: {result.Header.Version}");
            details.AppendLine($"Uncompressed size: {result.Header.UncompressedSize.ToString("N0", CultureInfo.CurrentCulture)}");
            details.AppendLine($"Block size: {result.Header.BlockSize.ToString("N0", CultureInfo.CurrentCulture)}");
            details.AppendLine($"Sectors: {result.Header.SectorCount.ToString("N0", CultureInfo.CurrentCulture)}");
        }

        details.AppendLine($"Index entries: {result.Entries.Count.ToString("N0", CultureInfo.CurrentCulture)}");
        AppendVerificationIssues(details, result.Issues);
        details.AppendLine($"Conclusion: {CreateShallowVerifyConclusion(result)}");

        return result.Success
            ? CsoUiOperationResult.Ok("Shallow verify passed; no header/index corruption detected", details.ToString())
            : CsoUiOperationResult.Fail("Shallow verify failed; structural issues detected", details.ToString());
    }

    private static CsoUiOperationResult VerifyDeep(string inputPath, bool computeSha256)
    {
        FormatDetectionResult detected = FormatDetector.Detect(inputPath);
        StringBuilder details = new();
        details.AppendLine($"Input: {SafeFullPath(inputPath)}");
        details.AppendLine("Verification type: Deep");
        details.AppendLine("Output written: False");
        details.AppendLine("Action taken: The file was read block-by-block and payload data was reconstructed in memory. No repair output was produced.");

        if (!detected.Success)
        {
            details.AppendLine("Result: Failed");
            details.AppendLine("Corruption detected: Unknown");
            details.AppendLine("Conclusion: The input could not be identified as a supported CSO-like file. No corruption verdict was produced.");
            AppendError(details, detected.ErrorCode, detected.ErrorMessage);

            return CsoUiOperationResult.Fail("Deep verify failed; input format was not recognized", details.ToString());
        }

        details.AppendLine($"Format: {detected.Format}");

        Stopwatch stopwatch = Stopwatch.StartNew();
        CsoDeepVerifyResult result = detected.Format switch
        {
            DetectedDiscFormat.Cso1 => new CsoDeepVerifier().Verify(inputPath, computeSha256),
            DetectedDiscFormat.RawIso or DetectedDiscFormat.Cso2 or DetectedDiscFormat.Zso or DetectedDiscFormat.Dax => RunContainerDeepVerify(inputPath, detected.Format, computeSha256),
            _ => CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("UnsupportedContainer", $"{detected.Format} is not supported by deep verification.")]),
        };
        stopwatch.Stop();

        AppendDeepVerifyDiagnostics(details, result, stopwatch.Elapsed, detected.Format);
        AppendDeepIssues(details, result.Issues);
        details.AppendLine($"Conclusion: {CreateDeepVerifyConclusion(result, detected.Format)}");
        details.AppendLine($"Limitations: {CreateDeepVerifyLimitations(detected.Format)}");

        return result.Success
            ? CsoUiOperationResult.Ok("Deep verify passed; no corruption detected", details.ToString())
            : CsoUiOperationResult.Fail(CreateDeepVerifyFailureStatus(result), details.ToString());
    }

    private static CsoDeepVerifyResult RunContainerDeepVerify(
        string inputPath,
        DetectedDiscFormat format,
        bool computeSha256)
    {
        try
        {
            CsoDeepVerifyResult? earlyFailure = ValidateRawIsoBeforeDeepRead(inputPath, format);

            if (earlyFailure is not null)
            {
                return earlyFailure;
            }

            using IBlockContainerReader reader = CreateContainerReader(inputPath, format);
            CsoDeepVerifyResult result = ContainerDeepVerifier.Verify(reader, computeSha256);
            long? fileLength = GetFileLengthOrNull(inputPath);

            return fileLength is null || result.FileLength is not null
                ? result
                : result with { FileLength = fileLength.Value };
        }
        catch (BlockContainerReadException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(ex.Code, ex.Message, ex.BlockIndex)]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("DeepVerifyIoFailed", ex.Message)]);
        }
    }

    private static CsoDeepVerifyResult? ValidateRawIsoBeforeDeepRead(
        string inputPath,
        DetectedDiscFormat format)
    {
        if (format is not DetectedDiscFormat.RawIso)
        {
            return null;
        }

        long? fileLength = GetFileLengthOrNull(inputPath);

        if (fileLength is null)
        {
            return null;
        }

        IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(fileLength.Value, allowPadding: false);

        if (alignment.Success)
        {
            return null;
        }

        long totalBlocks = fileLength.Value <= 0
            ? 0
            : checked((fileLength.Value + IsoAlignmentPolicy.SectorSize - 1) / IsoAlignmentPolicy.SectorSize);

        return CsoDeepVerifyResult.Fail(
            header: null,
            blocksChecked: 0,
            bytesReconstructed: 0,
            [new CsoDeepVerifyIssue(
                alignment.ErrorCode ?? "IsoAlignmentFailed",
                alignment.ErrorMessage ?? "Raw ISO alignment validation failed.")]) with
        {
            AlgorithmName = "Hybrid raw ISO verification",
            VerificationScope = "ISO9660 probe + raw sector read + full payload reconstruction",
            LegacyLayer = "ISO9660 primary-volume probe and strict 2048-byte sector-alignment validation",
            ModernLayer = "Not reached because raw ISO alignment validation failed.",
            ForensicLayer = "Coverage, zero-content, bounds, and reconstruction diagnostics",
            FileLength = fileLength.Value,
            TotalBlocks = totalBlocks,
            ExpectedReconstructedBytes = fileLength.Value > 0 ? (ulong)fileLength.Value : 0,
        };
    }

    private static IBlockContainerReader CreateContainerReader(
        string inputPath,
        DetectedDiscFormat format)
    {
        return format switch
        {
            DetectedDiscFormat.RawIso => new IsoContainerReader(inputPath),
            DetectedDiscFormat.Cso2 => new Cso2ContainerReader(inputPath),
            DetectedDiscFormat.Zso => new ZsoContainerReader(inputPath),
            DetectedDiscFormat.Dax => new DaxContainerReader(inputPath),
            _ => throw new BlockContainerReadException(
                "UnsupportedContainer",
                $"{format} is not supported by container deep verification."),
        };
    }

    private static Progress<CsoCompressProgress>? CreateCompressProgress(IProgress<double>? progress)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<CsoCompressProgress>(value => progress.Report(value.Percent));
    }

    private static Progress<CsoDecompressProgress>? CreateDecompressProgress(IProgress<double>? progress)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<CsoDecompressProgress>(value => progress.Report(value.Percent));
    }

    private static void AppendWarnings(StringBuilder details, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        details.AppendLine();
        details.AppendLine("Warnings:");

        foreach (string warning in warnings)
        {
            details.AppendLine($"- {warning}");
        }
    }

    private static void AppendDeepVerifyDiagnostics(
        StringBuilder details,
        CsoDeepVerifyResult result,
        TimeSpan elapsed,
        DetectedDiscFormat format)
    {
        details.AppendLine();
        details.AppendLine("Verification layers:");
        details.AppendLine($"Algorithm: {result.AlgorithmName}");
        details.AppendLine($"Scope: {result.VerificationScope}");
        details.AppendLine($"Legacy layer: {result.LegacyLayer}");
        details.AppendLine($"Modern layer: {result.ModernLayer}");
        details.AppendLine($"Forensic layer: {result.ForensicLayer}");

        details.AppendLine();
        details.AppendLine("Integrity checks:");
        details.AppendLine($"Header check: {CreateHeaderCheckStatus(result, format)}");
        details.AppendLine($"Index check: {CreateIndexCheckStatus(result, format)}");
        details.AppendLine($"Final sentinel: {CreateFinalSentinelStatus(result, format)}");
        details.AppendLine($"Block offset order: {CreateOffsetOrderStatus(result)}");
        details.AppendLine($"Bounds check: {CreateBoundsStatus(result)}");
        details.AppendLine($"Payload read/decode: {CreatePayloadStatus(result)}");
        details.AppendLine($"Reconstructed size: {CreateReconstructedSizeStatus(result)}");

        details.AppendLine();
        if (format is DetectedDiscFormat.RawIso)
        {
            details.AppendLine("Raw image metadata:");
            details.AppendLine($"Image format: {format}");
            details.AppendLine($"Sector size: {FormatByteCount((ulong)IsoAlignmentPolicy.SectorSize)}");
            details.AppendLine($"Logical image size: {FormatByteCount(result.ExpectedReconstructedBytes)}");
            details.AppendLine($"Physical file size: {FormatNullableByteCount(result.FileLength)}");
            details.AppendLine($"Container ratio: {FormatContainerRatio(result)}");
            details.AppendLine($"Space saved: {FormatSpaceSaved(result)}");
        }
        else
        {
            details.AppendLine("CSO metadata:");
            details.AppendLine($"CSO version: {FormatCsoVersion(result)}");
            details.AppendLine($"Block size: {FormatNullableByteCount(result.Header?.BlockSize)}");
            details.AppendLine($"Index shift: {FormatNullableNumber(result.Header?.IndexShift)}");
            details.AppendLine($"Uncompressed size: {FormatByteCount(result.ExpectedReconstructedBytes)}");
            details.AppendLine($"Compressed file size: {FormatNullableByteCount(result.FileLength)}");
            details.AppendLine($"Container ratio: {FormatContainerRatio(result)}");
            details.AppendLine($"Space saved: {FormatSpaceSaved(result)}");
        }

        details.AppendLine();
        details.AppendLine("Forensic statistics:");
        details.AppendLine($"Result: {(result.Success ? "Passed" : "Failed")}");
        details.AppendLine($"Corruption detected: {CreateCorruptionVerdict(result)}");
        details.AppendLine($"Coverage: {FormatCoverage(result)}");
        details.AppendLine($"Blocks checked: {FormatBlocksChecked(result)}");
        details.AppendLine($"Bytes reconstructed: {FormatByteCount(result.BytesReconstructed)}");
        details.AppendLine($"Expected reconstructed bytes: {FormatByteCount(result.ExpectedReconstructedBytes)}");
        details.AppendLine($"File length: {FormatNullableByteCount(result.FileLength)}");
        details.AppendLine($"Header size: {FormatNullableNumber(result.HeaderSize)}");
        details.AppendLine($"Index entries: {FormatNullableNumber(result.IndexEntryCount)}");
        details.AppendLine($"Index table bytes: {FormatNullableNumber(result.IndexTableBytes)}");
        details.AppendLine($"Index end offset: {FormatNullableNumber(result.IndexEndOffset)}");
        details.AppendLine($"First data offset: {FormatNullableNumber(result.FirstDataOffset)}");
        details.AppendLine($"Final data offset: {FormatNullableNumber(result.FinalDataOffset)}");
        details.AppendLine($"Physical payload bytes: {FormatByteCount(result.PhysicalPayloadBytes)}");
        details.AppendLine($"Payload blocks decoded: {FormatNumber(result.PayloadBlocksDecoded)}");
        details.AppendLine($"Compressed blocks: {FormatNumber(result.CompressedBlocks)}");
        details.AppendLine($"Stored blocks: {FormatNumber(result.StoredBlocks)}");
        details.AppendLine($"Decoded zero-content blocks: {FormatNumber(result.ZeroBlocks)}");
        details.AppendLine("Zero-content note: Counted after payload decode; may overlap compressed/stored block counts.");
        details.AppendLine($"Reconstructed SHA256: {CreateSha256Status(result)}");
        details.AppendLine($"Elapsed: {FormatElapsed(elapsed)}");
        details.AppendLine($"Throughput: {FormatThroughput(result.BytesReconstructed, elapsed)}");
        details.AppendLine($"Repair needed: {CreateDeepRepairNeed(result)}");
    }

    private static void AppendVerificationIssues(StringBuilder details, IReadOnlyList<CsoVerificationIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.AppendLine("Issues: none");
            return;
        }

        details.AppendLine();
        details.AppendLine("Issues:");

        foreach (CsoVerificationIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.AppendLine($"- {issue.Code}{block}: {issue.Message}");
        }
    }

    private static void AppendDeepIssues(StringBuilder details, IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.AppendLine("Issues: none");
            return;
        }

        details.AppendLine();
        details.AppendLine("Issues:");

        foreach (CsoDeepVerifyIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.AppendLine($"- {issue.Code}{block}: {issue.Message}");
        }
    }

    private static void AppendRepairIssues(
        StringBuilder details,
        string heading,
        IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.AppendLine($"{heading}: none");
            return;
        }

        details.AppendLine();
        details.AppendLine($"{heading}:");

        foreach (CsoDeepVerifyIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.AppendLine($"- {issue.Code}{block}: {issue.Message}");
        }
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "True" : "False";
    }

    private static string CreateHeaderCheckStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "InvalidMagic", "HeaderTooSmall", "UnsupportedVersion", "InvalidHeaderSize", "InvalidUncompressedSize", "InvalidBlockSize", "BlockSizeTooLarge", "InvalidIndexShift", "HeaderReadFailed"))
        {
            return "Failed";
        }

        return result.Header is null && result.TotalBlocks > 0
            ? "Passed via container reader"
            : result.Header is null ? "Not completed" : "Passed";
    }

    private static string CreateIndexCheckStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "IndexReadFailed", "IndexEntryCountMismatch", "EmptyIndexTable", "IndexTableTooLarge", "IndexTableTruncated", "IndexEntryTruncated", "FirstDataOffsetBeforeIndexEnd"))
        {
            return "Failed";
        }

        return result.TotalBlocks > 0 || result.Success ? "Passed" : "Not completed";
    }

    private static string CreateFinalSentinelStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "FinalIndexEntryHasFlag", "CsoV2FinalSentinelHighBit", "FinalOffsetMismatch", "FinalOffsetPastEndOfFile"))
        {
            return "Failed";
        }

        return result.Header is null ? "N/A for this container reader" : "Passed";
    }

    private static string CreateOffsetOrderStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "IndexOffsetsNotMonotonic") ? "Failed" : result.TotalBlocks > 0 ? "Passed" : "Not completed";
    }

    private static string CreateBoundsStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "IndexOffsetPastEndOfFile", "FinalOffsetPastEndOfFile", "StoredBlockTooSmall", "InvalidCompressedBlockSize", "IsoNotSectorAligned", "InvalidIsoSize", "IsoAlignmentFailed")
            ? "Failed"
            : result.TotalBlocks > 0 ? "Passed" : "Not completed";
    }

    private static string CreatePayloadStatus(CsoDeepVerifyResult result)
    {
        if (HasAnyIssue(result, "CorruptCompressedBlock", "UnexpectedEndOfFile", "CsoDeepVerifyFailed", "DeepVerifyIoFailed", "InputReadFailed"))
        {
            return "Failed";
        }

        if (result.PayloadBlocksDecoded > 0 || result.Success)
        {
            return "Passed";
        }

        return "Not completed";
    }

    private static string CreateReconstructedSizeStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "ReconstructedSizeMismatch") ? "Failed" : result.Success ? "Passed" : "Not completed";
    }

    private static bool HasAnyIssue(CsoDeepVerifyResult result, params string[] issueCodes)
    {
        foreach (CsoDeepVerifyIssue issue in result.Issues)
        {
            foreach (string code in issueCodes)
            {
                if (string.Equals(issue.Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string FormatCoverage(CsoDeepVerifyResult result)
    {
        return result.TotalBlocks <= 0
            ? "-"
            : $"{result.CoveragePercent.ToString("N2", ReportCulture)}% of indexed blocks";
    }

    private static string FormatBlocksChecked(CsoDeepVerifyResult result)
    {
        string checkedBlocks = FormatNumber(result.BlocksChecked);

        return result.TotalBlocks <= 0
            ? checkedBlocks
            : $"{checkedBlocks} / {FormatNumber(result.TotalBlocks)}";
    }

    private static string FormatByteCount(ulong bytes)
    {
        return bytes < 1024UL * 1024UL
            ? $"{FormatNumber(bytes)} bytes"
            : $"{FormatNumber(bytes)} bytes ({FormatMib(bytes)} MiB)";
    }

    private static string FormatNullableByteCount(long? bytes)
    {
        return bytes is null || bytes.Value < 0
            ? "-"
            : FormatByteCount((ulong)bytes.Value);
    }

    private static string FormatNullableByteCount(uint? bytes)
    {
        return bytes is null ? "-" : FormatByteCount(bytes.Value);
    }

    private static string FormatMib(ulong bytes)
    {
        return ((double)bytes / (1024d * 1024d)).ToString("N2", ReportCulture);
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", ReportCulture);
    }

    private static string FormatNumber(ulong value)
    {
        return value.ToString("N0", ReportCulture);
    }

    private static string FormatNullableNumber(long? value)
    {
        return value is null ? "-" : FormatNumber(value.Value);
    }

    private static string FormatNullableNumber(byte? value)
    {
        return value is null ? "-" : value.Value.ToString("N0", ReportCulture);
    }

    private static string FormatCsoVersion(CsoDeepVerifyResult result)
    {
        return result.Header is null ? "-" : result.Header.Version.ToString("N0", ReportCulture);
    }

    private static string FormatContainerRatio(CsoDeepVerifyResult result)
    {
        if (result.FileLength is null || result.FileLength <= 0 || result.ExpectedReconstructedBytes == 0)
        {
            return "-";
        }

        double ratio = (double)result.FileLength.Value / result.ExpectedReconstructedBytes;
        return ratio.ToString("P2", ReportCulture);
    }

    private static string FormatSpaceSaved(CsoDeepVerifyResult result)
    {
        if (result.FileLength is null || result.FileLength <= 0 || result.ExpectedReconstructedBytes == 0)
        {
            return "-";
        }

        double saved = 1.0 - ((double)result.FileLength.Value / result.ExpectedReconstructedBytes);
        return saved.ToString("P2", ReportCulture);
    }

    private static string CreateSha256Status(CsoDeepVerifyResult result)
    {
        return result.Sha256Computed ? result.Sha256! : "Disabled";
    }

    private static string CreateCorruptionVerdict(CsoDeepVerifyResult result)
    {
        if (result.Success)
        {
            return "False";
        }

        return HasAnyIssue(
            result,
            "InvalidInputPath",
            "InputNotFound",
            "InputAccessDenied",
            "UnsupportedContainer",
            "StreamNotSeekable",
            "DeepVerifyIoFailed")
            ? "Unknown"
            : "True";
    }

    private static string CreateDeepVerifyFailureStatus(CsoDeepVerifyResult result)
    {
        return string.Equals(CreateCorruptionVerdict(result), "Unknown", StringComparison.Ordinal)
            ? "Deep verify failed; no corruption verdict"
            : "Deep verify failed; corruption detected";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string FormatThroughput(ulong bytes, TimeSpan elapsed)
    {
        if (bytes == 0 || elapsed.TotalSeconds <= 0)
        {
            return "-";
        }

        double mibPerSecond = bytes / (1024d * 1024d) / elapsed.TotalSeconds;
        return $"{mibPerSecond.ToString("N2", ReportCulture)} MiB/s";
    }

    private static string CreateShallowVerifyConclusion(CsoVerificationResult result)
    {
        return result.Success
            ? "No header/index corruption was detected. This is a metadata-only pass and does not prove that every compressed block can be decompressed."
            : "Structural CSO metadata issues were detected. Run Deep verify or Repair to classify the damage.";
    }

    private static string CreateDeepRepairNeed(CsoDeepVerifyResult result)
    {
        return result.Success
            ? "No"
            : "Yes or re-dump required; see Issues for the exact failing block/condition.";
    }

    private static string CreateDeepVerifyConclusion(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return result.Success
                ? "No raw-image read, alignment, or reconstruction problems were detected. The input was readable and every checked sector reconstructed successfully."
                : "Raw-image read, alignment, or unsupported container structure failed. The file did not fully reconstruct under deep verification.";
        }

        return result.Success
            ? "No corruption was detected by deep verification. The input was readable and all checked payload blocks reconstructed successfully."
            : "Corruption or unsupported container structure was detected. The file did not fully reconstruct under deep verification.";
    }

    private static string CreateDeepVerifyLimitations(DetectedDiscFormat format)
    {
        return format is DetectedDiscFormat.RawIso
            ? "This verification validates raw image readability, 2048-byte sector alignment, full block coverage, and payload reconstruction. It does not prove Redump hash match, game database identity, or gameplay correctness."
            : "This verification validates container structure, index/bounds semantics, and payload decompression. It does not prove Redump hash match, game database identity, or gameplay correctness.";
    }

    private static string CreateRepairSuccessStatus(CsoRepairResult result)
    {
        return result.RepairMode switch
        {
            CsoRepairMode.RebuildOnly => "Rebuild completed; no input corruption was proven",
            CsoRepairMode.DamageRepair => "Repair completed; recoverable input issues were detected",
            _ => "Repair completed",
        };
    }

    private static string CreateRepairFailureStatus(CsoRepairResult result)
    {
        return result.RepairMode switch
        {
            CsoRepairMode.ReDumpRequired => "Repair failed; re-dump required",
            CsoRepairMode.DamageRepair => "Repair failed after detecting input issues",
            _ => "Repair failed",
        };
    }

    private static void AppendCodecWins(StringBuilder details, IReadOnlyDictionary<string, int> codecWins)
    {
        if (codecWins.Count == 0)
        {
            return;
        }

        details.AppendLine();
        details.AppendLine("Codec wins:");

        foreach (KeyValuePair<string, int> item in codecWins)
        {
            details.AppendLine($"- {item.Key}: {item.Value.ToString("N0", CultureInfo.CurrentCulture)}");
        }
    }

    private static void AppendCodecReport(StringBuilder details, int blocksReported)
    {
        if (blocksReported <= 0)
        {
            return;
        }

        details.AppendLine($"Codec report blocks: {blocksReported.ToString("N0", CultureInfo.CurrentCulture)}");
    }

    private static void AppendError(StringBuilder details, string? code, string? message)
    {
        details.AppendLine();
        details.AppendLine($"Error: {code ?? "UnknownError"}");
        details.AppendLine(message ?? "Operation failed.");
    }

    private static long? GetFileLengthOrNull(string inputPath)
    {
        try
        {
            return new FileInfo(inputPath).Length;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }

    private static string ValueOrDash<T>(T? value)
        where T : struct
    {
        return value is null
            ? "-"
            : string.Format(CultureInfo.CurrentCulture, "{0:N0}", value.Value);
    }

    private static string ValueOrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}