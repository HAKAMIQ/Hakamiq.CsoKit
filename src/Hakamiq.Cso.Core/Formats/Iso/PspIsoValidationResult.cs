namespace Hakamiq.Cso.Core.Formats.Iso;

public sealed record PspIsoValidationResult(
    bool Success,
    string InputPath,
    long InputBytes,
    long PaddingBytes,
    bool HasIso9660PrimaryVolumeDescriptor,
    bool HasUmdDataBin,
    bool HasParamSfo,
    bool HasEbootBin,
    IReadOnlyList<PspIsoValidationIssue> Issues,
    IReadOnlyList<string> Warnings)
{
    public static PspIsoValidationResult Fail(
        string inputPath,
        long inputBytes,
        IReadOnlyList<PspIsoValidationIssue> issues,
        IReadOnlyList<string>? warnings = null,
        long paddingBytes = 0,
        bool hasIso9660PrimaryVolumeDescriptor = false,
        bool hasUmdDataBin = false,
        bool hasParamSfo = false,
        bool hasEbootBin = false)
    {
        return new PspIsoValidationResult(
            Success: false,
            inputPath,
            inputBytes,
            paddingBytes,
            hasIso9660PrimaryVolumeDescriptor,
            hasUmdDataBin,
            hasParamSfo,
            hasEbootBin,
            issues,
            warnings ?? Array.Empty<string>());
    }
}
