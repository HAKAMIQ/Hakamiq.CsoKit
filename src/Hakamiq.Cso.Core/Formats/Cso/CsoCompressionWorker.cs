using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoCompressionWorker
{
    private readonly CsoCompressionProfileSettings settings;
    private readonly CsoBestCandidateSelector candidateSelector;

    public CsoCompressionWorker()
        : this(CsoCompressionProfilePolicy.Create(CsoCompressionProfilePolicy.DefaultProfile), new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(CsoCompressionProfile profile)
        : this(CsoCompressionProfilePolicy.Create(profile), new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(CsoCompressionProfileSettings settings)
        : this(settings, new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfileSettings settings,
        CsoBestCandidateSelector candidateSelector)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
    }

    public SectorResult Compress(SectorJob job)
    {
        byte[] compressed = CompressRawDeflate(job.SourceSpan, settings.CompressionLevel);

        SectorResult rawDeflateCandidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            compressed.Length,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: settings.Level,
            Buffer: compressed);

        return candidateSelector.Select(job, rawDeflateCandidate);
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
