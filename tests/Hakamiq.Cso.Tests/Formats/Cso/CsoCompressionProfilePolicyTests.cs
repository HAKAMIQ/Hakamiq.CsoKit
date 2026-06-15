using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoCompressionProfilePolicyTests
{
    [Theory]
    [InlineData("compat", CsoCompressionProfile.Compat)]
    [InlineData("FAST", CsoCompressionProfile.Fast)]
    [InlineData("smallest", CsoCompressionProfile.Smallest)]
    public void TryParse_WithSupportedProfile_ReturnsProfile(
        string value,
        CsoCompressionProfile expected)
    {
        bool success = CsoCompressionProfilePolicy.TryParse(value, out CsoCompressionProfile actual);

        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("extreme")]
    public void TryParse_WithUnsupportedProfile_ReturnsFalse(string value)
    {
        bool success = CsoCompressionProfilePolicy.TryParse(value, out _);

        Assert.False(success);
    }

    [Fact]
    public void Create_WithFastProfile_UsesFastSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Fast);

        Assert.Equal("fast", settings.CliName);
        Assert.True(settings.IsFast);
        Assert.Equal(1, settings.Level);
    }

    [Fact]
    public void Create_WithSmallestProfile_UsesDefaultSafeSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Smallest);

        Assert.Equal("smallest", settings.CliName);
        Assert.False(settings.IsFast);
        Assert.Equal(9, settings.Level);
    }

    [Fact]
    public void Create_WithCompatProfile_UsesSafeSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Compat);

        Assert.Equal("compat", settings.CliName);
        Assert.False(settings.IsFast);
        Assert.Equal(9, settings.Level);
    }
}
