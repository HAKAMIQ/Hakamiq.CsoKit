namespace Hakamiq.Cso.Core.Formats.Cso;

public static class CsoBlockReader
{
    public static int ReadExactlyOrLess(Stream stream, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(stream);

        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
