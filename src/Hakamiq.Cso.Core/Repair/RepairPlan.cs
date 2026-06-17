using Hakamiq.Cso.Core.Formats.DiscImage;

namespace Hakamiq.Cso.Core.Repair;

public sealed record RepairPlan(
    DetectedDiscFormat InputFormat,
    RepairMode Mode,
    bool WritesTempIso,
    string? FallbackReason = null);
