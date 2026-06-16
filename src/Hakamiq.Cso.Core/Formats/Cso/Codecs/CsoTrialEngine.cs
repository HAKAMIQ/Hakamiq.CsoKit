namespace Hakamiq.Cso.Core.Formats.Cso.Codecs;

public sealed class CsoTrialEngine
{
    private readonly IReadOnlyList<ICsoCodecTrial> trials;
    private readonly CsoBestCandidateSelector selector;

    public CsoTrialEngine(
        IReadOnlyList<ICsoCodecTrial> trials,
        CsoBestCandidateSelector selector)
    {
        this.trials = trials ?? throw new ArgumentNullException(nameof(trials));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public SectorResult Compress(SectorJob job)
    {
        List<SectorResult> candidates = new(trials.Count);

        foreach (ICsoCodecTrial trial in trials)
        {
            if (!trial.TryCompressRawDeflate(job.SourceSpan, out CsoCodecTrialResult result) ||
                !result.Success)
            {
                continue;
            }

            if (result.Length <= 0 || result.Length >= job.SourceLength)
            {
                continue;
            }

            if (!RawDeflateVerifier.RoundtripEquals(
                result.OutputSpan,
                job.SourceSpan,
                expectedBytes: job.SourceLength))
            {
                continue;
            }

            candidates.Add(new SectorResult(
                job.BlockIndex,
                job.SourceOffset,
                job.SourceLength,
                result.Length,
                IsStored: false,
                Method: result.Kind is CsoCodecKind.NativeZopfli5 or CsoCodecKind.NativeZopfli15 or CsoCodecKind.NativeZopfli25
                    ? CompressionMethod.ZopfliDeflate
                    : CompressionMethod.RawDeflate,
                Level: GetLogicalLevel(result.Kind),
                Buffer: result.Buffer,
                CodecName: result.Name));
        }

        return selector.Select(job, candidates);
    }

    private static int GetLogicalLevel(CsoCodecKind kind)
    {
        return kind switch
        {
            CsoCodecKind.ManagedDeflateFastest => 1,
            CsoCodecKind.ManagedDeflateOptimal => 6,
            CsoCodecKind.ManagedDeflateSmallest => 9,
            CsoCodecKind.NativeZopfli5 => 105,
            CsoCodecKind.NativeZopfli15 => 115,
            CsoCodecKind.NativeZopfli25 => 125,
            _ => (int)kind,
        };
    }
}
