namespace Hakamiq.Cso.Core.Formats.Cso.Codecs.Native;

public sealed class NativeUnavailableTrial : ICsoCodecTrial
{
    private readonly string errorCode;
    private readonly string errorMessage;

    public NativeUnavailableTrial(
        CsoCodecKind kind,
        string name,
        string errorCode,
        string errorMessage)
    {
        Kind = kind;
        Name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name;
        this.errorCode = string.IsNullOrWhiteSpace(errorCode) ? "NativeCodecUnavailable" : errorCode;
        this.errorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Native codec is unavailable in this build."
            : errorMessage;
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        result = CsoCodecTrialResult.Fail(Kind, Name, errorCode, errorMessage);
        return false;
    }
}
