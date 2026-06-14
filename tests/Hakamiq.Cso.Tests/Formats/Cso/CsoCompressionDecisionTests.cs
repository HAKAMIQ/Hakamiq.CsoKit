using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoCompressionDecisionTests
{
    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsSmaller_ReturnsCompressedCandidate()
    {
        byte[] source = new byte[16];
        SectorJob job = new(
            BlockIndex: 3,
            SourceOffset: 4096,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 4,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: new byte[] { 1, 2, 3, 4 });

        CsoBestCandidateSelector selector = new();
        SectorResult result = selector.Select(job, candidate);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Equal(9, result.Level);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsEqualSize_ReturnsStoredSector()
    {
        byte[] source = new byte[] { 10, 20, 30, 40 };
        SectorJob job = new(
            BlockIndex: 1,
            SourceOffset: 2048,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: source.Length,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: new byte[] { 1, 2, 3, 4 });

        CsoBestCandidateSelector selector = new();
        SectorResult result = selector.Select(job, candidate);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(0, result.Level);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsLarger_ReturnsStoredSector()
    {
        byte[] source = new byte[] { 1, 2, 3 };
        SectorJob job = new(
            BlockIndex: 2,
            SourceOffset: 4096,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 5,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: new byte[] { 1, 2, 3, 4, 5 });

        CsoBestCandidateSelector selector = new();
        SectorResult result = selector.Select(job, candidate);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenMultipleCandidatesExist_ReturnsSmallestCandidate()
    {
        byte[] source = new byte[32];
        SectorJob job = new(
            BlockIndex: 5,
            SourceOffset: 10240,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult larger = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 12,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 6,
            Buffer: new byte[12]);

        SectorResult smaller = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 8,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: new byte[8]);

        CsoBestCandidateSelector selector = new();
        SectorResult result = selector.Select(job, new[] { larger, smaller });

        Assert.False(result.IsStored);
        Assert.Equal(8, result.OutputLength);
        Assert.Equal(9, result.Level);
    }

    [Fact]
    public void CompressionWorker_Compress_WhenBlockIsSmallAndUnhelpful_StoresOriginalBytes()
    {
        byte[] source = new byte[] { 1, 2, 3, 4 };
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new();
        SectorResult result = worker.Compress(job);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void CompressionWorker_Compress_WhenBlockIsCompressible_ReturnsCompressedCandidate()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new();
        SectorResult result = worker.Compress(job);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Equal(9, result.Level);
        Assert.True(result.OutputLength < source.Length);
    }
}
