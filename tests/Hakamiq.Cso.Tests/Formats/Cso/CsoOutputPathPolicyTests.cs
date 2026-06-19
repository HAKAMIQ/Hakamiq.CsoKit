using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoOutputPathPolicyTests
{
    [Fact]
    public void CreateCompressionOutputPath_WhenTargetIsAvailable_UsesSameFolderWithCsoExtension()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game.cso"), outputPath);
            Assert.False(Directory.Exists(Path.Combine(directory, "_cso-output")));
            Assert.False(Directory.Exists(Path.Combine(directory, "output")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateCompressionOutputPath_WhenTargetExists_AppendsHakamiqConvertedSuffix()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");
        string existingCsoPath = Path.Combine(directory, "Game.cso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);
            File.WriteAllBytes(existingCsoPath, [2]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game - Hakamiq Converted.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateCompressionOutputPath_WhenConvertedTargetExists_AppendsNumber()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);
            File.WriteAllBytes(Path.Combine(directory, "Game.cso"), [2]);
            File.WriteAllBytes(Path.Combine(directory, "Game - Hakamiq Converted.cso"), [3]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game - Hakamiq Converted 2.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateDecompressionOutputPath_WhenTargetExists_AppendsHakamiqConvertedSuffix()
    {
        string directory = CreateTempDirectory();
        string csoPath = Path.Combine(directory, "Game.cso");

        try
        {
            File.WriteAllBytes(csoPath, [1]);
            File.WriteAllBytes(Path.Combine(directory, "Game.iso"), [2]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateDecompressionOutputPath(csoPath);

            Assert.Equal(Path.Combine(directory, "Game - Hakamiq Converted.iso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
