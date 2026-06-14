namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoCompressResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong BytesRead,
    ulong BytesWritten,
    int CompressedBlocks,
    int StoredBlocks)
{
    public static CsoCompressResult Ok(
        ulong bytesRead,
        ulong bytesWritten,
        int compressedBlocks,
        int storedBlocks)
    {
        return new CsoCompressResult(
            true,
            null,
            null,
            bytesRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks);
    }

    public static CsoCompressResult Fail(
        string errorCode,
        string errorMessage,
        ulong bytesRead = 0,
        ulong bytesWritten = 0,
        int compressedBlocks = 0,
        int storedBlocks = 0)
    {
        return new CsoCompressResult(
            false,
            errorCode,
            errorMessage,
            bytesRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks);
    }
}