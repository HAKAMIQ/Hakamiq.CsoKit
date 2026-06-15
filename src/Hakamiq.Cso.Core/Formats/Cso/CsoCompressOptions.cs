namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoCompressOptions(
    string InputPath,
    string OutputPath,
    bool ForceOverwrite,
    uint BlockSize = CsoCompressor.DefaultBlockSize,
    CancellationToken CancellationToken = default,
    IProgress<CsoCompressProgress>? Progress = null,
    CsoCompressionProfile Profile = CsoCompressionProfile.Smallest);