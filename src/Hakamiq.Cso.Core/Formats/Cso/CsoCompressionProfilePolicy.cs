using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public static class CsoCompressionProfilePolicy
{
    public const string CompatName = "compat";
    public const string FastName = "fast";
    public const string SmallestName = "smallest";

    public static readonly string[] SupportedNames =
    [
        CompatName,
        FastName,
        SmallestName,
    ];

    public static string SupportedNamesText => string.Join("|", SupportedNames);

    public static CsoCompressionProfile DefaultProfile => CsoCompressionProfile.Smallest;

    public static bool TryParse(string? value, out CsoCompressionProfile profile)
    {
        profile = DefaultProfile;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case CompatName:
                profile = CsoCompressionProfile.Compat;
                return true;

            case FastName:
                profile = CsoCompressionProfile.Fast;
                return true;

            case SmallestName:
                profile = CsoCompressionProfile.Smallest;
                return true;

            default:
                return false;
        }
    }

    public static string GetCliName(CsoCompressionProfile profile)
    {
        return profile switch
        {
            CsoCompressionProfile.Compat => CompatName,
            CsoCompressionProfile.Fast => FastName,
            CsoCompressionProfile.Smallest => SmallestName,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile."),
        };
    }

    public static CsoCompressionProfileSettings Create(CsoCompressionProfile profile)
    {
        return profile switch
        {
            CsoCompressionProfile.Compat => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Compat,
                CompatName,
                "compat",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            CsoCompressionProfile.Fast => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Fast,
                FastName,
                "fast",
                IsFast: true,
                Level: 1,
                CompressionLevel: CompressionLevel.Fastest),

            CsoCompressionProfile.Smallest => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Smallest,
                SmallestName,
                "smallest",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile."),
        };
    }
}
