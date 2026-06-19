namespace Hakamiq.Cso.App.Services;

public sealed record CsoUiOperationResult(
    bool Success,
    string Status,
    string Details)
{
    public static CsoUiOperationResult Ok(string status, string details)
    {
        return new CsoUiOperationResult(true, status, details);
    }

    public static CsoUiOperationResult Fail(string status, string details)
    {
        return new CsoUiOperationResult(false, status, details);
    }
}
