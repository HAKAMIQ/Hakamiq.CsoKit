namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoIndexBuilder
{
    private readonly List<uint> entries;

    public CsoIndexBuilder(int sectorCount)
    {
        if (sectorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount), "Sector count cannot be negative.");
        }

        entries = new List<uint>(checked(sectorCount + 1));
    }

    public IReadOnlyList<uint> Entries => entries;

    public void AddSectorOffset(ulong offset, bool isStored)
    {
        entries.Add(EncodeIndexOffset(offset, isStored));
    }

    public void AddFinalOffset(ulong offset)
    {
        entries.Add(EncodeIndexOffset(offset, hasFlag: false));
    }

    public static uint EncodeIndexOffset(ulong offset, bool hasFlag)
    {
        if (offset > CsoIndexEntry.OffsetMask)
        {
            throw new InvalidDataException("CSO output is too large for the current index table.");
        }

        uint rawValue = checked((uint)offset);

        if (hasFlag)
        {
            rawValue |= CsoIndexEntry.FlagMask;
        }

        return rawValue;
    }
}
