namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoRepairResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string InputFormat,
    ulong BytesRead,
    ulong BytesWritten,
    long PaddingBytes)
{
    public static CsoRepairResult Ok(
        string inputFormat,
        ulong bytesRead,
        ulong bytesWritten,
        long paddingBytes)
    {
        return new CsoRepairResult(true, null, null, inputFormat, bytesRead, bytesWritten, paddingBytes);
    }

    public static CsoRepairResult Fail(
        string code,
        string message,
        string inputFormat = "Unknown")
    {
        return new CsoRepairResult(false, code, message, inputFormat, 0, 0, 0);
    }
}
