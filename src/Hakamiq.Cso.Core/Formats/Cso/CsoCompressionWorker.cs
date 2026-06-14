using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoCompressionWorker
{
    private readonly CsoBestCandidateSelector candidateSelector;

    public CsoCompressionWorker()
        : this(new CsoBestCandidateSelector())
    {
    }

    public CsoCompressionWorker(CsoBestCandidateSelector candidateSelector)
    {
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
    }

    public SectorResult Compress(SectorJob job)
    {
        byte[] compressed = CompressRawDeflate(job.SourceSpan);

        SectorResult rawDeflateCandidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            compressed.Length,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: compressed);

        return candidateSelector.Select(job, rawDeflateCandidate);
    }

    private static byte[] CompressRawDeflate(ReadOnlySpan<byte> block)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
    }
}
