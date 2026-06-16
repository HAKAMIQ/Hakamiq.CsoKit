namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoDeepVerifyIssue(
    string Code,
    string Message,
    int? BlockIndex = null);
