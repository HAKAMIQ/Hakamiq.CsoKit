using Hakamiq.Cso.Core.Native;

namespace Hakamiq.Cso.Tests.Native;

public sealed class NativeCsoRuntimeTests
{
    [Fact]
    public void NativeZlibRawDeflate_RoundtripsEachStrategy()
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();
        if (!capabilities.HasZlib)
        {
            return;
        }

        byte[] original = CreateSampleBlock();

        NativeCsoRawCodec[] codecs =
        [
            NativeCsoRawCodec.ZlibDefault,
            NativeCsoRawCodec.ZlibFiltered,
            NativeCsoRawCodec.ZlibHuffmanOnly,
            NativeCsoRawCodec.ZlibRle,
        ];

        foreach (NativeCsoRawCodec codec in codecs)
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
        if (!capabilities.HasLibDeflate)
        {
            return;
        }

        byte[] original = CreateSampleBlock();

        int[] levels = [1, 6, 9, 12];

        foreach (int level in levels)
        {
            Assert.True(NativeCsoRuntime.TryDeflateRaw(NativeCsoRawCodec.LibDeflate, level, strategy: 0, original, out byte[] compressed));
            Assert.True(NativeCsoRuntime.TryInflateRaw(compressed, original.Length, out byte[] restored));
            Assert.Equal(original, restored);
        }
    }

    private static byte[] CreateSampleBlock()
    {
        return new byte[4096];
    }
}