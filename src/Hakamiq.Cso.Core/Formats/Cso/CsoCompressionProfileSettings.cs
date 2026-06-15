using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed record CsoCompressionProfileSettings(
    CsoCompressionProfile Profile,
    string CliName,
    string DisplayName,
    bool IsFast,
    int Level,
    CompressionLevel CompressionLevel);
