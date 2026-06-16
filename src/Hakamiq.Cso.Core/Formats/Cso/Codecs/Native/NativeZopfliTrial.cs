using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Core.Formats.Cso.Codecs.Native;

public sealed class NativeZopfliTrial : ICsoCodecTrial
{
    private readonly int iterations;

    public NativeZopfliTrial(int iterations)
    {
        if (iterations is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Zopfli iterations must be between 1 and 100.");
        }

        this.iterations = iterations;
        Kind = iterations switch
        {
            <= 5 => CsoCodecKind.NativeZopfli5,
            <= 15 => CsoCodecKind.NativeZopfli15,
            _ => CsoCodecKind.NativeZopfli25,
        };
        Name = $"native-zopfli-{iterations}";
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        if (NativeCsoRuntime.TryDeflateZopfli(input, iterations, out byte[] compressed))
        {
            result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
            return true;
        }

        result = CsoCodecTrialResult.Fail(
            Kind,
            Name,
            "NativeCodecUnavailable",
            "Native Zopfli raw deflate is unavailable or failed for this block.");
        return false;
    }
}
