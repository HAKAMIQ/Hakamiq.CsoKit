using System.Buffers.Binary;
using System.IO.Compression;
using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Tests.Formats.Cso;

internal static class CsoTestFileFactory
{
    public static string CreateTempCsoV1(
        byte[] originalBytes,
        uint blockSize = 2048,
        byte version = 1,
        byte indexShift = 0,
        Func<int, bool>? storeBlockUncompressed = null,
        uint? headerSizeOverride = null)
    {
        if (originalBytes.Length == 0)
        {
            throw new ArgumentException("Original bytes must not be empty.", nameof(originalBytes));
        }

        int blockSizeInt = checked((int)blockSize);
        int sectorCount = checked((originalBytes.Length + blockSizeInt - 1) / blockSizeInt);
        int indexEntryCount = checked(sectorCount + 1);
        int dataStart = checked(CsoConstants.MinimumHeaderSize + (indexEntryCount * sizeof(uint)));

        List<uint> rawEntries = [];
        List<byte> payload = [];

        ulong currentOffset = checked((ulong)dataStart);

        for (int blockIndex = 0; blockIndex < sectorCount; blockIndex++)
        {
            int sourceOffset = checked(blockIndex * blockSizeInt);
            int sourceLength = Math.Min(blockSizeInt, originalBytes.Length - sourceOffset);
            byte[] block = originalBytes.AsSpan(sourceOffset, sourceLength).ToArray();
            bool storeUncompressed = storeBlockUncompressed?.Invoke(blockIndex) ?? false;

            rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift, storeUncompressed));

            byte[] storedBlock = storeUncompressed
                ? block
                : CompressRawDeflate(block);

            payload.AddRange(storedBlock);
            currentOffset += checked((ulong)storedBlock.Length);
        }

        rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift, hasFlag: false));

        byte[] bytes = new byte[checked(dataStart + payload.Count)];
        WriteHeader(
            bytes,
            headerSizeOverride ?? CsoConstants.MinimumHeaderSize,
            checked((ulong)originalBytes.Length),
            blockSize,
            version,
            indexShift);

        int indexOffset = CsoConstants.MinimumHeaderSize;

        foreach (uint rawEntry in rawEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(indexOffset, sizeof(uint)), rawEntry);
            indexOffset += sizeof(uint);
        }

        payload.CopyTo(bytes, dataStart);

        string path = Path.Combine(Path.GetTempPath(), $"HakamiqCsoKit_{Guid.NewGuid():N}.cso");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static uint EncodeIndexOffset(
        ulong offset,
        byte indexShift,
        bool hasFlag)
    {
        ulong shiftedOffset = offset >> indexShift;

        if ((shiftedOffset << indexShift) != offset)
        {
            throw new ArgumentException("CSO test offset is not aligned with index_shift.", nameof(indexShift));
        }

        if (shiftedOffset > CsoIndexEntry.OffsetMask)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "CSO test offset does not fit in a v1 index entry.");
        }

        uint rawValue = checked((uint)shiftedOffset);

        if (hasFlag)
        {
            rawValue |= CsoIndexEntry.FlagMask;
        }

        return rawValue;
    }

    private static byte[] CompressRawDeflate(byte[] block)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
    }

    private static void WriteHeader(
        byte[] bytes,
        uint headerSize,
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift)
    {
        bytes[0] = (byte)'C';
        bytes[1] = (byte)'I';
        bytes[2] = (byte)'S';
        bytes[3] = (byte)'O';

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), headerSize);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), blockSize);

        bytes[20] = version;
        bytes[21] = indexShift;
        bytes[22] = 0;
        bytes[23] = 0;
    }
}
