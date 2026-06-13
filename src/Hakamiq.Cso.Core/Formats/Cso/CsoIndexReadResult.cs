namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoIndexReadResult(
    bool Success,
    IReadOnlyList<CsoIndexEntry> Entries,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CsoIndexReadResult Ok(IReadOnlyList<CsoIndexEntry> entries)
    {
        return new CsoIndexReadResult(true, entries, null, null);
    }

    public static CsoIndexReadResult Fail(string errorCode, string errorMessage)
    {
        return new CsoIndexReadResult(false, Array.Empty<CsoIndexEntry>(), errorCode, errorMessage);
    }
}