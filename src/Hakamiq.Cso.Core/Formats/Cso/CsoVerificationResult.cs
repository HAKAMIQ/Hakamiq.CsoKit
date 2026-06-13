namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoVerificationResult(
    bool Success,
    CsoHeader? Header,
    IReadOnlyList<CsoIndexEntry> Entries,
    IReadOnlyList<CsoVerificationIssue> Issues)
{
    public static CsoVerificationResult Ok(
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries)
    {
        return new CsoVerificationResult(true, header, entries, Array.Empty<CsoVerificationIssue>());
    }

    public static CsoVerificationResult Fail(
        CsoHeader? header,
        IReadOnlyList<CsoIndexEntry> entries,
        IReadOnlyList<CsoVerificationIssue> issues)
    {
        return new CsoVerificationResult(false, header, entries, issues);
    }
}