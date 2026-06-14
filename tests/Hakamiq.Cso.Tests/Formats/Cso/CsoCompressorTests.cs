using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoCompressorTests
{
    [Fact]
    public void Compress_WithValidIso_CreatesCsoThatDecompressesToOriginalBytes()
    {
        byte[] original = Enumerable.Range(0, 8192 + 123)
            .Select(index => (byte)(index % 251))
            .ToArray();

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult compressResult = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(compressResult.Success, compressResult.ErrorMessage);
            Assert.Equal((ulong)original.Length, compressResult.BytesRead);
            Assert.True(File.Exists(csoPath));

            CsoDecompressor decompressor = new();
            CsoDecompressResult decompressResult = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompressResult.Success, decompressResult.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Fact]
    public void Compress_WithCompressibleIso_UsesCompressedBlocks()
    {
        byte[] original = new byte[8192];

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.CompressedBlocks > 0);
            Assert.True(result.BytesWritten < result.BytesRead);
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WhenOutputExistsWithoutForce_Fails()
    {
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(csoPath, new byte[] { 9, 9, 9 });

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("OutputAlreadyExists", result.ErrorCode);
            Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WithForceOverwrite_ReplacesExistingOutput()
    {
        byte[] original = Enumerable.Range(0, 4096)
            .Select(index => (byte)(index % 13))
            .ToArray();

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);
            File.WriteAllBytes(csoPath, new byte[] { 9, 9, 9 });

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: true));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotEqual(new byte[] { 9, 9, 9 }, File.ReadAllBytes(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}{extension}");
    }
}