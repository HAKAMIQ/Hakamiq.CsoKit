namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoMeasureResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong OriginalBytes,
    ulong EstimatedBytes,
    ulong EstimatedSavedBytes,
    ulong EstimatedGrowthBytes,
    double EstimatedRatio,
    int TotalBlocks,
    int CompressedBlocks,
    int StoredBlocks)
{
    public static CsoMeasureResult Ok(
        ulong originalBytes,
        ulong estimatedBytes,
        int totalBlocks,
        int compressedBlocks,
        int storedBlocks)
    {
        ulong savedBytes = originalBytes > estimatedBytes
            ? originalBytes - estimatedBytes
            : 0;

        ulong growthBytes = estimatedBytes > originalBytes
            ? estimatedBytes - originalBytes
            : 0;

        double ratio = originalBytes == 0
            ? 0
            : (double)estimatedBytes / originalBytes;

        return new CsoMeasureResult(
            true,
            null,
            null,
            originalBytes,
            estimatedBytes,
            savedBytes,
            growthBytes,
            ratio,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }

    public static CsoMeasureResult Fail(
        string errorCode,
        string errorMessage,
        ulong originalBytes = 0,
        ulong estimatedBytes = 0,
        int totalBlocks = 0,
        int compressedBlocks = 0,
        int storedBlocks = 0)
    {
        return new CsoMeasureResult(
            false,
            errorCode,
            errorMessage,
            originalBytes,
            estimatedBytes,
            0,
            0,
            0,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }
}
