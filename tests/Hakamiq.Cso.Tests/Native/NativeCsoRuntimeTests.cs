using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Tests.Native;

public sealed class NativeCsoRuntimeTests
{
    [Fact]
    public void NativeZlibRawDeflate_RoundtripsEachStrategy()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasZlib, "Native zlib is unavailable; build the native DLL before running codec coverage tests.");

        byte[] original = CreateSampleBlock();

        foreach (NativeCsoRawCodec codec in new[]
        {
            NativeCsoRawCodec.ZlibDefault,
            NativeCsoRawCodec.ZlibFiltered,
            NativeCsoRawCodec.ZlibHuffmanOnly,
            NativeCsoRawCodec.ZlibRle,
        })
        {
            Assert.True(NativeCsoRuntime.TryDeflateRaw(codec, level: 9, strategy: 0, original, out byte[] compressed));
            Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void NativeLibDeflateRawDeflate_RoundtripsRequestedLevels()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        Assert.True(capabilities.HasLibDeflate, "Native libdeflate is unavailable; build the native DLL before running codec coverage tests.");

        byte[] original = CreateSampleBlock();

        foreach (int level in new[] { 1, 6, 9, 12 })
        {
            Assert.True(NativeCsoRuntime.TryDeflateRaw(NativeCsoRawCodec.LibDeflate, level, strategy: 0, original, out byte[] compressed));
            Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
            Assert.Equal(original, restored);
        }
    }

    private static byte[] CreateSampleBlock()
    {
        byte[] block = new byte[4096];
        return block;
    }
}
