namespace Hakamiq.Cso.Core.Compression.Trials;

public sealed class CodecTrialSummaryBuilder
{
    private readonly int retainedBlockLimit;
    private readonly List<CodecTrialReport> retainedBlocks = [];
    private readonly Dictionary<string, int> rejectedReasons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> candidateAttempts = new(StringComparer.OrdinalIgnoreCase);
    private int blocksReported;

    public CodecTrialSummaryBuilder(int retainedBlockLimit)
    {
        if (retainedBlockLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedBlockLimit), "Codec report block limit cannot be negative.");
        }

        this.retainedBlockLimit = retainedBlockLimit;
    }

    public void Add(CodecTrialReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        blocksReported++;

        if (retainedBlocks.Count < retainedBlockLimit)
        {
            retainedBlocks.Add(report);
        }

        foreach (CodecTrialCandidateResult candidate in report.Candidates)
        {
            Increment(candidateAttempts, candidate.CodecName);

            if (!string.IsNullOrWhiteSpace(candidate.RejectedReason))
            {
                Increment(rejectedReasons, candidate.RejectedReason);
            }
        }
    }

    public CodecTrialSummary? Build(IReadOnlyDictionary<string, int> selectedCodecWins)
    {
        ArgumentNullException.ThrowIfNull(selectedCodecWins);

        if (blocksReported == 0)
        {
            return null;
        }

        return new CodecTrialSummary(
            blocksReported,
            retainedBlocks.OrderBy(static report => report.BlockIndex).ToArray(),
            new Dictionary<string, int>(selectedCodecWins, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(rejectedReasons, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(candidateAttempts, StringComparer.OrdinalIgnoreCase));
    }

    private static void Increment(Dictionary<string, int> values, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        values[key] = values.TryGetValue(key, out int current)
            ? checked(current + 1)
            : 1;
    }
}
