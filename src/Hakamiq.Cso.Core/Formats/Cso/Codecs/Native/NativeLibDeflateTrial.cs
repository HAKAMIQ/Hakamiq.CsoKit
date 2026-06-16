using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Core.Formats.Cso.Codecs.Native;

public sealed class NativeLibDeflateTrial : ICsoCodecTrial
{
    private readonly int level;

    public NativeLibDeflateTrial(CsoCodecKind kind, string name, int level)
    {
        if (level is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "libdeflate level must be between 1 and 12.");
        }

        Kind = kind;
        Name = name;
        this.level = level;
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        if (!capabilities.HasLibDeflate)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native libdeflate raw deflate is unavailable in this build.");
            return false;
        }

        if (NativeCsoRuntime.TryDeflateRaw(NativeCsoRawCodec.LibDeflate, level, strategy: 0, input, out byte[] compressed))
        {
            result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
            return true;
        }

        result = CsoCodecTrialResult.Fail(
            Kind,
            Name,
            "NativeCodecUnavailable",
            "Native libdeflate raw deflate failed for this block.");
        return false;
    }
}
