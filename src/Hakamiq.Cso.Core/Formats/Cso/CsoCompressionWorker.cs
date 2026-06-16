using System.IO.Compression;
using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoCompressionWorker
{
    private const int DefaultZopfliIterations = 15;

    private readonly CsoCompressionProfileSettings settings;
    private readonly CsoBestCandidateSelector candidateSelector;
    private readonly bool useZopfli;
    private readonly int zopfliIterations;

    public CsoCompressionWorker()
        : this(CsoCompressionProfilePolicy.Create(CsoCompressionProfilePolicy.DefaultProfile), new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfile profile,
        bool useZopfli = false,
        int zopfliIterations = DefaultZopfliIterations)
        : this(CsoCompressionProfilePolicy.Create(profile), new CsoBestCandidateSelector(), useZopfli, zopfliIterations)
    {
    }

    public CsoCompressionWorker(CsoCompressionProfileSettings settings)
        : this(settings, new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfileSettings settings,
        CsoBestCandidateSelector candidateSelector)
        : this(settings, candidateSelector, useZopfli: false, zopfliIterations: DefaultZopfliIterations)
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfileSettings settings,
        CsoBestCandidateSelector candidateSelector,
        bool useZopfli,
        int zopfliIterations = DefaultZopfliIterations)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
        this.useZopfli = useZopfli;
        this.zopfliIterations = zopfliIterations;
    }

    public SectorResult Compress(SectorJob job)
    {
        List<SectorResult> candidates = new(capacity: useZopfli ? 4 : 3);

        foreach ((CompressionLevel compressionLevel, int logicalLevel) in GetManagedDeflateCandidates(settings.Profile))
        {
            byte[] compressed = CompressRawDeflate(job.SourceSpan, compressionLevel);

            candidates.Add(new SectorResult(
                job.BlockIndex,
                job.SourceOffset,
                job.SourceLength,
                compressed.Length,
                IsStored: false,
                Method: CompressionMethod.RawDeflate,
                Level: logicalLevel,
                Buffer: compressed));
        }

        if (useZopfli &&
            NativeCsoRuntime.TryDeflateZopfli(job.SourceSpan, zopfliIterations, out byte[] zopfliCompressed))
        {
            candidates.Add(new SectorResult(
                job.BlockIndex,
                job.SourceOffset,
                job.SourceLength,
                zopfliCompressed.Length,
                IsStored: false,
                Method: CompressionMethod.ZopfliDeflate,
                Level: 100 + zopfliIterations,
                Buffer: zopfliCompressed));
        }

        return candidateSelector.Select(job, candidates);
    }

    private static IReadOnlyList<(CompressionLevel CompressionLevel, int LogicalLevel)> GetManagedDeflateCandidates(
        CsoCompressionProfile profile)
    {
        return profile switch
        {
            CsoCompressionProfile.Fast =>
            [
                (CompressionLevel.Fastest, 1),
            ],

            CsoCompressionProfile.Compat =>
            [
                (CompressionLevel.SmallestSize, 9),
            ],

            CsoCompressionProfile.Smallest =>
            [
                (CompressionLevel.Fastest, 1),
                (CompressionLevel.Optimal, 6),
                (CompressionLevel.SmallestSize, 9),
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile."),
        };
    }

    private static byte[] CompressRawDeflate(
        ReadOnlySpan<byte> block,
        CompressionLevel compressionLevel)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, compressionLevel, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
    }
}
