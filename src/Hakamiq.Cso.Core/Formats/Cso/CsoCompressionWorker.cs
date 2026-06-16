using Hakamiq.Cso.Core.Formats.Cso.Codecs;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoCompressionWorker
{
    private const int DefaultZopfliIterations = 15;

    private readonly CsoCompressionProfileSettings settings;
    private readonly CsoBestCandidateSelector candidateSelector;
    private readonly CsoTrialEngine trialEngine;

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
        bool useExperimental = zopfliIterations > DefaultZopfliIterations;

        CsoTrialPlan plan = CsoTrialPlanner.CreatePlan(this.settings.Profile, useZopfli, useExperimental);
        IReadOnlyList<ICsoCodecTrial> trials = CsoTrialPlanner.CreateTrials(plan);
        trialEngine = new CsoTrialEngine(trials, this.candidateSelector);
    }

    public SectorResult Compress(SectorJob job)
    {
        return trialEngine.Compress(job);
    }
}
