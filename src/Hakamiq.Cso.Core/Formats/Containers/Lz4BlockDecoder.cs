namespace Hakamiq.Cso.Core.Formats.Containers;

internal static class Lz4BlockDecoder
{
    public static bool TryDecode(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;
        int inputOffset = 0;

        try
        {
            while (inputOffset < input.Length && bytesWritten < output.Length)
            {
                byte token = input[inputOffset++];
                int literalLength = token >> 4;

                if (!TryReadExtendedLength(input, ref inputOffset, ref literalLength))
                {
                    return false;
                }

                if (literalLength > input.Length - inputOffset ||
                    literalLength > output.Length - bytesWritten)
                {
                    return false;
                }

                input.Slice(inputOffset, literalLength).CopyTo(output[bytesWritten..]);
                inputOffset += literalLength;
                bytesWritten += literalLength;

                if (inputOffset == input.Length || bytesWritten == output.Length)
                {
                    return bytesWritten == output.Length && HasOnlyZeroPadding(input[inputOffset..]);
                }

                if (input.Length - inputOffset < 2)
                {
                    return false;
                }

                int matchOffset = input[inputOffset] | (input[inputOffset + 1] << 8);
                inputOffset += 2;

                if (matchOffset == 0 || matchOffset > bytesWritten)
                {
                    return false;
                }

                int matchLength = token & 0x0F;

                if (!TryReadExtendedLength(input, ref inputOffset, ref matchLength))
                {
                    return false;
                }

                matchLength += 4;

                if (matchLength > output.Length - bytesWritten)
                {
                    return false;
                }

                int copyFrom = bytesWritten - matchOffset;

                for (int i = 0; i < matchLength; i++)
                {
                    output[bytesWritten++] = output[copyFrom + i];
                }
            }

            return bytesWritten == output.Length && HasOnlyZeroPadding(input[inputOffset..]);
        }
        catch (IndexOutOfRangeException)
        {
            bytesWritten = 0;
            return false;
        }
    }

    private static bool TryReadExtendedLength(
        ReadOnlySpan<byte> input,
        ref int inputOffset,
        ref int length)
    {
        if (length != 15)
        {
            return true;
        }

        while (inputOffset < input.Length)
        {
            byte value = input[inputOffset++];
            length = checked(length + value);

            if (value != 255)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOnlyZeroPadding(ReadOnlySpan<byte> input)
    {
        foreach (byte value in input)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }
}
