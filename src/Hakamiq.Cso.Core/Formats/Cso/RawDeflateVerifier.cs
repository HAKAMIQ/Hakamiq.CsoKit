using System.IO.Compression;

namespace Hakamiq.Cso.Core.Formats.Cso;

public static class RawDeflateVerifier
{
    public static bool TryInflate(
        ReadOnlySpan<byte> compressed,
        Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;

        try
        {
            using MemoryStream input = new(compressed.ToArray());
            using DeflateStream deflate = new(input, CompressionMode.Decompress);

            while (bytesWritten < output.Length)
            {
                int read = deflate.Read(output[bytesWritten..]);

                if (read == 0)
                {
                    break;
                }

                bytesWritten += read;
            }

            return bytesWritten == output.Length;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static bool RoundtripEquals(
        ReadOnlySpan<byte> compressed,
        ReadOnlySpan<byte> original,
        int expectedBytes)
    {
        byte[] restored = new byte[expectedBytes];

        return TryInflate(compressed, restored, out int bytesWritten) &&
            bytesWritten == expectedBytes &&
            restored.AsSpan().SequenceEqual(original);
    }
}
