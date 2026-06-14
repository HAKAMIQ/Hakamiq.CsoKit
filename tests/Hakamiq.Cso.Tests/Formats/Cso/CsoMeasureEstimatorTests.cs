using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoMeasureEstimatorTests
{
    [Fact]
    public void Measure_WithValidIso_ReturnsEstimateWithoutCreatingOutputFile()
    {
        byte[] original = Enumerable.Range(0, 8192 + 123)
            .Select(index => (byte)(index % 251))
            .ToArray();

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal((ulong)original.Length, result.OriginalBytes);
            Assert.True(result.EstimatedBytes > 0);
            Assert.Equal(result.TotalBlocks, result.CompressedBlocks + result.StoredBlocks);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Measure_WithValidIso_MatchesCurrentCompressorOutputSizeAndBlockDecisions()
    {
        byte[] original = Enumerable.Range(0, 16384 + 333)
            .Select(index => (byte)(index % 17))
            .ToArray();

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult measureResult = estimator.Measure(new CsoMeasureOptions(isoPath));

            CsoCompressor compressor = new();
            CsoCompressResult compressResult = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(measureResult.Success, measureResult.ErrorMessage);
            Assert.True(compressResult.Success, compressResult.ErrorMessage);
            Assert.Equal(compressResult.BytesRead, measureResult.OriginalBytes);
            Assert.Equal(compressResult.BytesWritten, measureResult.EstimatedBytes);
            Assert.Equal(compressResult.CompressedBlocks, measureResult.CompressedBlocks);
            Assert.Equal(compressResult.StoredBlocks, measureResult.StoredBlocks);
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Measure_WithMissingInput_ReturnsInputNotFound()
    {
        string isoPath = CreateTempPath(".iso");

        CsoMeasureEstimator estimator = new();
        CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

        Assert.False(result.Success);
        Assert.Equal("InputNotFound", result.ErrorCode);
        Assert.Equal((ulong)0, result.OriginalBytes);
        Assert.Equal((ulong)0, result.EstimatedBytes);
    }

    [Fact]
    public void Measure_WithEmptyInput_ReturnsInvalidInputSize()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            File.WriteAllBytes(isoPath, Array.Empty<byte>());

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

            Assert.False(result.Success);
            Assert.Equal("InvalidInputSize", result.ErrorCode);
        }
        finally
        {
            File.Delete(isoPath);
        }
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}{extension}");
    }
}
