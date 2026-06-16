using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Core.Formats.Cso.Codecs.Native;

public sealed class NativeZlibTrial : ICsoCodecTrial
{
    private readonly NativeCsoRawCodec codec;

    public NativeZlibTrial(CsoCodecKind kind, string name, NativeCsoRawCodec codec)
    {
        Kind = kind;
        Name = name;
        this.codec = codec;
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        if (!capabilities.HasZlib)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native zlib raw deflate is unavailable in this build.");
            return false;
        }

        if (NativeCsoRuntime.TryDeflateRaw(codec, level: 9, strategy: 0, input, out byte[] compressed))
        {
            result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
            return true;
        }

        result = CsoCodecTrialResult.Fail(
            Kind,
            Name,
            "NativeCodecUnavailable",
            "Native zlib raw deflate failed for this block.");
        return false;
    }
}
