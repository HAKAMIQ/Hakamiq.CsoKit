namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoCompressOptions(
    string InputPath,
    string OutputPath,
    bool ForceOverwrite,
    uint BlockSize = CsoCompressor.DefaultBlockSize,
    CancellationToken CancellationToken = default,
    IProgress<CsoCompressProgress>? Progress = null,
    CsoCompressionProfile Profile = CsoCompressionProfile.GameSafe,
    int WorkerCount = 1,
    bool UseZopfli = false,
    bool DeepVerifyOutput = false,
    bool CollectCodecReport = false);
