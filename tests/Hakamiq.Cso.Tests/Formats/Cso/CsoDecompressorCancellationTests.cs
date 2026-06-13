using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

public sealed class CsoDecompressorCancellationTests
{
    [Fact]
    public void Decompress_WithAlreadyCanceledToken_ReturnsOperationCanceled()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        CsoDecompressor decompressor = new();

        CsoDecompressResult result = decompressor.Decompress(
            new CsoDecompressOptions(
                "input.cso",
                "output.iso",
                ForceOverwrite: false,
                cancellation.Token));

        Assert.False(result.Success);
        Assert.Equal("OperationCanceled", result.ErrorCode);
    }
}
