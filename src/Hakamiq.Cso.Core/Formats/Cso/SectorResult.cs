namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record SectorResult(
    int BlockIndex,
    ulong SourceOffset,
    int SourceLength,
    int OutputLength,
    bool IsStored,
    CompressionMethod Method,
    int Level,
    byte[] Buffer)
{
    public ReadOnlySpan<byte> OutputSpan => Buffer.AsSpan(0, OutputLength);
}
