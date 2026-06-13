namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoHeader(
    uint HeaderSize,
    ulong UncompressedSize,
    uint BlockSize,
    byte Version,
    byte IndexShift)
{
    public bool IsVersionSupported => Version is 1 or 2;

    public long SectorCount
    {
        get
        {
            if (BlockSize == 0)
            {
                return 0;
            }

            return checked((long)((UncompressedSize + BlockSize - 1) / BlockSize));
        }
    }
}