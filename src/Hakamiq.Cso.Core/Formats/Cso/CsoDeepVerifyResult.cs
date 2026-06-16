namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoDeepVerifyResult(
    bool Success,
    CsoHeader? Header,
    int BlocksChecked,
    ulong BytesReconstructed,
    string? Sha256,
    IReadOnlyList<CsoDeepVerifyIssue> Issues)
{
    public static CsoDeepVerifyResult Fail(
        CsoHeader? header,
        int blocksChecked,
        ulong bytesReconstructed,
        IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        return new CsoDeepVerifyResult(
            Success: false,
            header,
            blocksChecked,
            bytesReconstructed,
            Sha256: null,
            issues);
    }

    public static CsoDeepVerifyResult Ok(
        CsoHeader? header,
        int blocksChecked,
        ulong bytesReconstructed,
        string? sha256)
    {
        return new CsoDeepVerifyResult(
            Success: true,
            header,
            blocksChecked,
            bytesReconstructed,
            sha256,
            Array.Empty<CsoDeepVerifyIssue>());
    }
}
