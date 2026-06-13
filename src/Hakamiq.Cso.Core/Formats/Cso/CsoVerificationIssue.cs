namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoVerificationIssue(
    string Code,
    string Message,
    int? BlockIndex = null);