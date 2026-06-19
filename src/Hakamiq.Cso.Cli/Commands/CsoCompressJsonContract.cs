using Hakamiq.Cso.Core.Compression.Trials;
using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class CsoCompressJsonContract
{
    public const int CurrentSchemaVersion = 1;

    public static CsoMeasureJsonOutput Measure(
        string input,
        CsoCompressionProfileSettings profileSettings,
        CsoMeasureResult result,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(profileSettings);
        ArgumentNullException.ThrowIfNull(result);

        return new CsoMeasureJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "measure",
            result.Success,
            input,
            (string?)null,
            "RawIso",
            [],
            new
            {
                mode = "measure",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                false,
                false,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            new CsoMeasureJsonMetrics(
                result.OriginalBytes,
                result.EstimatedBytes,
                result.EstimatedRatio,
                result.EstimatedSavedBytes,
                result.EstimatedGrowthBytes,
                result.TotalBlocks,
                result.CompressedBlocks,
                result.StoredBlocks),
            result.Success ? null : Error(result.ErrorCode, result.ErrorMessage));
    }

    public static CsoWriteJsonOutput Write(
        string input,
        string output,
        bool force,
        bool autoOutput,
        CsoCompressionProfileSettings profileSettings,
        CsoCompressResult result,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(profileSettings);
        ArgumentNullException.ThrowIfNull(result);

        return new CsoWriteJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "write",
            result.Success,
            input,
            output,
            "Cso1",
            [],
            new
            {
                mode = "write",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                force,
                autoOutput,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            new CsoWriteJsonMetrics(
                result.BytesRead,
                result.BytesWritten,
                result.CompressedBlocks,
                result.StoredBlocks,
                result.ZeroBlocks,
                result.EffectiveCodecWins),
            codecReport ? result.CodecTrialSummary : null,
            result.Success ? null : Error(result.ErrorCode, result.ErrorMessage));
    }

    public static CsoArgumentErrorJsonOutput ArgumentError(string message)
    {
        return new CsoArgumentErrorJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "arguments",
            Success: false,
            Input: (string?)null,
            Output: (string?)null,
            Format: (string?)null,
            Warnings: [],
            Diagnostics: new { },
            Error("InvalidArguments", message));
    }

    private static CsoCommandError Error(string? code, string? message)
    {
        return new CsoCommandError(
            string.IsNullOrWhiteSpace(code) ? "Unknown" : code,
            string.IsNullOrWhiteSpace(message) ? "Command failed." : message);
    }
}

public sealed record CsoCompressJsonOptions(
    CsoProfileOutput Profile,
    bool Force,
    bool AutoOutput,
    uint BlockSize,
    int Threads,
    bool Zopfli,
    bool DeepVerify,
    bool CodecReport,
    int CodecReportBlockLimit = 64);

public sealed record CsoMeasureJsonMetrics(
    ulong OriginalBytes,
    ulong EstimatedBytes,
    double EstimatedRatio,
    ulong EstimatedSavedBytes,
    ulong EstimatedGrowthBytes,
    int TotalBlocks,
    int CompressedBlocks,
    int StoredBlocks);

public sealed record CsoWriteJsonMetrics(
    ulong BytesRead,
    ulong BytesWritten,
    int CompressedBlocks,
    int StoredBlocks,
    int ZeroBlocks,
    IReadOnlyDictionary<string, int> CodecWins);

public sealed record CsoMeasureJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string Input,
    string? Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCompressJsonOptions Options,
    CsoMeasureJsonMetrics Metrics,
    CsoCommandError? Error);

public sealed record CsoWriteJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string Input,
    string Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCompressJsonOptions Options,
    CsoWriteJsonMetrics Metrics,
    CodecTrialSummary? CodecReport,
    CsoCommandError? Error);

public sealed record CsoArgumentErrorJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string? Input,
    string? Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCommandError Error);