using System.Diagnostics;
using Hakamiq.Cso.Core.Compression.Trials;

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

            candidates.Add(ToSectorResult(job, result));
        }

        return selector.Select(job, candidates);
    }

    public SectorResult CompressWithReport(SectorJob job)
    {
        List<SectorResult> candidates = new(trials.Count);
        List<CodecTrialCandidateResult> candidateReports = new(trials.Count + 1);

        foreach (ICsoCodecTrial trial in trials)
        {
            Stopwatch encodeTimer = Stopwatch.StartNew();
            bool produced = trial.TryCompressRawDeflate(job.SourceSpan, out CsoCodecTrialResult result);
            encodeTimer.Stop();

            if (!produced || !result.Success)
            {
                candidateReports.Add(CreateCandidateReport(
                    trial.Kind,
                    trial.Name,
                    compressedBytes: 0,
                    sourceBytes: job.SourceLength,
                    encodeMilliseconds: encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: result.ErrorCode ?? "CodecUnavailable",
                    selectedWinner: false,
                    fallbackReason: result.ErrorMessage));
                continue;
            }

            if (result.Length <= 0)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: "InvalidCompressedBytes",
                    selectedWinner: false,
                    fallbackReason: null));
                continue;
            }

            if (result.Length >= job.SourceLength)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: "NotSmallerThanStored",
                    selectedWinner: false,
                    fallbackReason: "Stored block is smaller or equal."));
                continue;
            }

            Stopwatch decodeTimer = Stopwatch.StartNew();
            bool passedRoundtrip = RawDeflateVerifier.RoundtripEquals(
                result.OutputSpan,
                job.SourceSpan,
                expectedBytes: job.SourceLength);
            decodeTimer.Stop();

            if (!passedRoundtrip)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeTimer.Elapsed.TotalMilliseconds,
                    passedRoundtrip: false,
                    rejectedReason: "RoundtripMismatch",
                    selectedWinner: false,
                    fallbackReason: "Codec output did not decode back to the source block."));
                continue;
            }

            candidates.Add(ToSectorResult(job, result));
            candidateReports.Add(CreateCandidateReport(
                result.Kind,
                result.Name,
                result.Length,
                job.SourceLength,
                encodeTimer.Elapsed.TotalMilliseconds,
                decodeTimer.Elapsed.TotalMilliseconds,
                passedRoundtrip: true,
                rejectedReason: null,
                selectedWinner: false,
                fallbackReason: null));
        }

        SectorResult selected = selector.Select(job, candidates);
        bool storedFallback = selected.IsStored;
        string selectedCodec = selected.EffectiveCodecName;

        if (storedFallback)
        {
            candidateReports.Add(new CodecTrialCandidateResult(
                "store",
                "store",
                0,
                job.SourceLength,
                1.0,
                0,
                0,
                PassedRoundtrip: true,
                RejectedReason: null,
                SelectedWinner: true,
                FallbackReason: candidates.Count == 0 ? "No candidate passed and beat stored bytes." : "Stored bytes won size policy."));
        }

        IReadOnlyList<CodecTrialCandidateResult> finalized = candidateReports
            .Select(candidate => candidate.CodecName.Equals(selectedCodec, StringComparison.OrdinalIgnoreCase)
                ? candidate with { SelectedWinner = true }
                : candidate)
            .ToArray();

        CodecTrialReport report = new(
            job.BlockIndex,
            job.SourceLength,
            finalized,
            selectedCodec,
            storedFallback);

        return selected with { TrialReport = report };
    }

    private static SectorResult ToSectorResult(SectorJob job, CsoCodecTrialResult result)
    {
        return new SectorResult(
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
            CodecName: result.Name);
    }

    private static CodecTrialCandidateResult CreateCandidateReport(
        CsoCodecKind kind,
        string name,
        int compressedBytes,
        int sourceBytes,
        double encodeMilliseconds,
        double decodeMilliseconds,
        bool passedRoundtrip,
        string? rejectedReason,
        bool selectedWinner,
        string? fallbackReason)
    {
        return new CodecTrialCandidateResult(
            name,
            GetCodecFamily(kind),
            GetLogicalLevel(kind),
            compressedBytes,
            sourceBytes <= 0 ? 0 : (double)compressedBytes / sourceBytes,
            encodeMilliseconds,
            decodeMilliseconds,
            passedRoundtrip,
            rejectedReason,
            selectedWinner,
            fallbackReason);
    }

    private static string GetCodecFamily(CsoCodecKind kind)
    {
        return kind switch
        {
            CsoCodecKind.Store => "store",
            CsoCodecKind.ManagedDeflateFastest or
                CsoCodecKind.ManagedDeflateOptimal or
                CsoCodecKind.ManagedDeflateSmallest => "managed-deflate",
            CsoCodecKind.NativeZlibDefault or
                CsoCodecKind.NativeZlibFiltered or
                CsoCodecKind.NativeZlibHuffmanOnly or
                CsoCodecKind.NativeZlibRle => "native-zlib",
            CsoCodecKind.NativeLibDeflate1 or
                CsoCodecKind.NativeLibDeflate6 or
                CsoCodecKind.NativeLibDeflate9 or
                CsoCodecKind.NativeLibDeflate12 => "native-libdeflate",
            CsoCodecKind.NativeZopfli5 or
                CsoCodecKind.NativeZopfli15 or
                CsoCodecKind.NativeZopfli25 => "native-zopfli",
            CsoCodecKind.NativeSevenZipDeflate => "native-7z-deflate-unavailable",
            _ => "unknown",
        };
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
