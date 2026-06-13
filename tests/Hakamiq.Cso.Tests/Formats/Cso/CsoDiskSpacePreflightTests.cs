using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoDiskSpacePreflightTests
{
    [Fact]
    public void CheckOutputSpace_WithSmallRequirement_ReturnsSuccess()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}.iso");

        CsoDiskSpacePreflight preflight = new();
        CsoDiskSpacePreflightResult result = preflight.CheckOutputSpace(outputPath, requiredBytes: 1);

        Assert.True(result.Success);
        Assert.True(result.AvailableBytes >= 1);
    }

    [Fact]
    public void CheckOutputSpace_WithImpossibleRequirement_ReturnsNotEnoughDiskSpace()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}.iso");

        CsoDiskSpacePreflight preflight = new();
        CsoDiskSpacePreflightResult result = preflight.CheckOutputSpace(outputPath, requiredBytes: ulong.MaxValue);

        Assert.False(result.Success);
        Assert.Equal("NotEnoughDiskSpace", result.ErrorCode);
    }
}
