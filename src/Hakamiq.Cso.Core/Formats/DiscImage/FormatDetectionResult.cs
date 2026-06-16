namespace Hakamiq.Cso.Core.Formats.DiscImage;

public sealed record FormatDetectionResult(
    bool Success,
    DetectedDiscFormat Format,
    string Magic,
    uint? HeaderSize,
    ulong? UncompressedSize,
    uint? BlockSize,
    byte? IndexShift,
    long? SectorCount,
    IReadOnlyList<string> Warnings,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static FormatDetectionResult Fail(string code, string message)
    {
        return new FormatDetectionResult(
            Success: false,
            DetectedDiscFormat.Unknown,
            Magic: string.Empty,
            HeaderSize: null,
            UncompressedSize: null,
            BlockSize: null,
            IndexShift: null,
            SectorCount: null,
            Warnings: Array.Empty<string>(),
            ErrorCode: code,
            ErrorMessage: message);
    }
}
