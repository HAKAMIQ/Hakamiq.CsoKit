using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso.Codecs.Managed;

public sealed class ManagedDeflateTrial : ICsoCodecTrial
{
    public static readonly ManagedDeflateTrial Fastest = new(
        CsoCodecKind.ManagedDeflateFastest,
        "managed-deflate-fastest",
        CompressionLevel.Fastest);

    public static readonly ManagedDeflateTrial Optimal = new(
        CsoCodecKind.ManagedDeflateOptimal,
        "managed-deflate-optimal",
        CompressionLevel.Optimal);

    public static readonly ManagedDeflateTrial Smallest = new(
        CsoCodecKind.ManagedDeflateSmallest,
        "managed-deflate-smallest",
        CompressionLevel.SmallestSize);

    private readonly CompressionLevel compressionLevel;

    private ManagedDeflateTrial(
        CsoCodecKind kind,
        string name,
        CompressionLevel compressionLevel)
    {
        Kind = kind;
        Name = name;
        this.compressionLevel = compressionLevel;
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        try
        {
            using MemoryStream compressed = new();

            using (DeflateStream deflate = new(compressed, compressionLevel, leaveOpen: true))
            {
                deflate.Write(input);
            }

            byte[] output = compressed.ToArray();
            result = new CsoCodecTrialResult(Kind, Name, output, output.Length, Success: true);
            return true;
        }
        catch (InvalidDataException ex)
        {
            result = CsoCodecTrialResult.Fail(Kind, Name, "ManagedDeflateFailed", ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            result = CsoCodecTrialResult.Fail(Kind, Name, "ManagedDeflateFailed", ex.Message);
            return false;
        }
    }
}
