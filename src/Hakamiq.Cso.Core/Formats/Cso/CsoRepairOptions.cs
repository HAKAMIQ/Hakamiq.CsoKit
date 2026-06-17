namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoRepairOptions(
    string InputPath,
    string OutputPath,
    bool ForceOverwrite,
    CsoCompressionProfile Profile = CsoCompressionProfile.GameSafe,
    bool PadLastSector = false,
    bool DeepVerify = true,
    CancellationToken CancellationToken = default,
    bool CollectCodecReport = false,
    int CodecReportBlockLimit = 64);
