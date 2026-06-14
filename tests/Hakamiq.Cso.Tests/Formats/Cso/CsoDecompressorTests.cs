using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoDecompressorTests
{
    [Fact]
    public void Decompress_WithValidRawDeflateCsoV1_WritesExpectedIsoBytes()
    {
        byte[] original = Enumerable.Range(0, 8192 + 123)
            .Select(index => (byte)(index % 4))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal((ulong)original.Length, result.BytesWritten);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithLegacyVersionZeroCsoV1_WritesExpectedIsoBytes()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 2))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original, version: 0);
        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithUncompressedHighBitBlocks_WritesExpectedIsoBytes()
    {
        byte[] original = Enumerable.Range(0, 5000)
            .Select(index => (byte)(index % 251))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(
            original,
            storeBlockUncompressed: _ => true);

        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithForceOverwrite_ReplacesExistingOutput()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 7))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();
        File.WriteAllBytes(outputPath, [0xFF]);

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: true));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WhenSiblingTmpFileExists_DoesNotDeleteIt()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 3))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();
        string siblingTempPath = outputPath + ".tmp";
        byte[] siblingTempBytes = [1, 2, 3, 4];
        File.WriteAllBytes(siblingTempPath, siblingTempBytes);

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
            Assert.True(File.Exists(siblingTempPath));
            Assert.Equal(siblingTempBytes, File.ReadAllBytes(siblingTempPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
            File.Delete(siblingTempPath);
        }
    }


    [Fact]
    public void Decompress_WithMissingOutputDirectory_DoesNotCreateDirectory()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 5))
            .ToArray();

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_Missing_{Guid.NewGuid():N}");
        string outputPath = Path.Combine(outputDirectory, "Game.iso");

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("OutputDirectoryNotFound", result.ErrorCode);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            File.Delete(csoPath);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static string CreateTempIsoPath()
    {
        return Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}.iso");
    }
}
