namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoBestCandidateSelector
{
    public SectorResult Select(SectorJob job, SectorResult candidate)
    {
        ValidateCandidate(job, candidate);

        if (candidate.OutputLength >= job.SourceLength)
        {
            return CreateStoredResult(job);
        }

        return candidate;
    }

    public SectorResult Select(SectorJob job, IReadOnlyList<SectorResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        SectorResult? bestCandidate = null;

        for (int index = 0; index < candidates.Count; index++)
        {
            SectorResult candidate = candidates[index];
            ValidateCandidate(job, candidate);

            if (bestCandidate is null || IsBetterCandidate(candidate, bestCandidate))
            {
                bestCandidate = candidate;
            }
        }

        if (bestCandidate is null ||
            (!bestCandidate.IsStored && bestCandidate.OutputLength >= job.SourceLength))
        {
            return CreateStoredResult(job);
        }

        return bestCandidate;
    }

    public static SectorResult CreateStoredResult(SectorJob job)
    {
        byte[] storedBuffer = job.SourceSpan.ToArray();

        return new SectorResult(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            storedBuffer.Length,
            IsStored: true,
            Method: CompressionMethod.Store,
            Level: 0,
            Buffer: storedBuffer,
            CodecName: "store");
    }

    private static bool IsBetterCandidate(SectorResult candidate, SectorResult currentBest)
    {
        if (candidate.OutputLength != currentBest.OutputLength)
        {
            return candidate.OutputLength < currentBest.OutputLength;
        }

        if (candidate.IsStored != currentBest.IsStored)
        {
            return candidate.IsStored;
        }

        return candidate.Level > currentBest.Level;
    }

    private static void ValidateCandidate(SectorJob job, SectorResult candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.BlockIndex != job.BlockIndex)
        {
            throw new InvalidDataException("Compression candidate block index does not match the sector job.");
        }

        if (candidate.SourceOffset != job.SourceOffset)
        {
            throw new InvalidDataException("Compression candidate source offset does not match the sector job.");
        }

        if (candidate.SourceLength != job.SourceLength)
        {
            throw new InvalidDataException("Compression candidate source length does not match the sector job.");
        }

        if (candidate.OutputLength < 0 || candidate.OutputLength > candidate.Buffer.Length)
        {
            throw new InvalidDataException("Compression candidate output length is invalid.");
        }
    }
}
