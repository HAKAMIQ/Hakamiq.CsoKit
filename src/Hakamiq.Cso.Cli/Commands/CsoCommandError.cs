namespace Hakamiq.Cso.Cli.Commands;

public sealed record CsoCommandError(
    string Code,
    string Message);
