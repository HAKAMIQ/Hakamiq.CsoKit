namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoMeasureOptions(
    string InputPath,
    uint BlockSize = CsoCompressor.DefaultBlockSize,
    CancellationToken CancellationToken = default,
    IProgress<CsoCompressProgress>? Progress = null);
