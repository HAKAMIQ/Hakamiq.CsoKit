using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoOutputSafetyPolicyTests
{
    [Fact]
    public void Validate_WithSameInputAndOutput_ReturnsFailure()
    {
        string path = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}.cso");

        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(path, path, forceOverwrite: true);

            Assert.False(result.Success);
            Assert.Equal("SameInputOutputPath", result.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_WithExistingOutputAndNoForce_ReturnsOutputAlreadyExists()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Output_{Guid.NewGuid():N}.iso");

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            File.WriteAllBytes(outputPath, [2]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputPath, forceOverwrite: false);

            Assert.False(result.Success);
            Assert.Equal("OutputAlreadyExists", result.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Validate_WithExistingOutputAndForce_ReturnsSuccess()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Output_{Guid.NewGuid():N}.iso");

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            File.WriteAllBytes(outputPath, [2]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputPath, forceOverwrite: true);

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Validate_WithDirectoryAsOutput_ReturnsOutputPathIsDirectory()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_OutputDir_{Guid.NewGuid():N}");

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            Directory.CreateDirectory(outputDirectory);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputDirectory, forceOverwrite: true);

            Assert.False(result.Success);
            Assert.Equal("OutputPathIsDirectory", result.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
