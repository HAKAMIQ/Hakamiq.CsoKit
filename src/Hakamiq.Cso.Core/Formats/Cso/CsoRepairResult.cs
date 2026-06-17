using Hakamiq.Cso.Core.Compression.Trials;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoRepairResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string InputFormat,
    ulong BytesRead,
    ulong BytesWritten,
    long PaddingBytes,
    string Mode = "temp-iso-fallback",
    bool UsedTempIso = true,
    string? FallbackReason = null,
    CodecTrialSummary? CodecTrialSummary = null,
    int CompressedBlocks = 0,
    int StoredBlocks = 0,
    int ZeroBlocks = 0)
{
    public static CsoRepairResult Ok(
        string inputFormat,
        ulong bytesRead,
        ulong bytesWritten,
        long paddingBytes,
        string mode = "temp-iso-fallback",
        bool usedTempIso = true,
        string? fallbackReason = null,
        CodecTrialSummary? codecTrialSummary = null,
        int compressedBlocks = 0,
        int storedBlocks = 0,
        int zeroBlocks = 0)
    {
        return new CsoRepairResult(
            true,
            null,
            null,
            inputFormat,
            bytesRead,
            bytesWritten,
            paddingBytes,
            mode,
            usedTempIso,
            fallbackReason,
            codecTrialSummary,
            compressedBlocks,
            storedBlocks,
            zeroBlocks);
    }

    public static CsoRepairResult Fail(
        string code,
        string message,
        string inputFormat = "Unknown",
        string mode = "temp-iso-fallback",
        bool usedTempIso = true,
        string? fallbackReason = null,
        CodecTrialSummary? codecTrialSummary = null,
        int compressedBlocks = 0,
        int storedBlocks = 0,
        int zeroBlocks = 0)
    {
        return new CsoRepairResult(
            false,
            code,
            message,
            inputFormat,
            0,
            0,
            0,
            mode,
            usedTempIso,
            fallbackReason,
            codecTrialSummary,
            compressedBlocks,
            storedBlocks,
            zeroBlocks);
    }
}
