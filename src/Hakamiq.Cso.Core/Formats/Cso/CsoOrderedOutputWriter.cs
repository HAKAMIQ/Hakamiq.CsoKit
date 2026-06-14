namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoOrderedOutputWriter
{
    private readonly Stream output;

    public CsoOrderedOutputWriter(Stream output)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public ulong Position => checked((ulong)output.Position);

    public void ReserveDataStart(ulong dataStart)
    {
        if (dataStart > long.MaxValue)
        {
            throw new InvalidDataException("CSO index table is too large.");
        }

        output.SetLength(checked((long)dataStart));
        output.Position = checked((long)dataStart);
    }

    public void Write(SectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        output.Write(result.OutputSpan);
    }
}
