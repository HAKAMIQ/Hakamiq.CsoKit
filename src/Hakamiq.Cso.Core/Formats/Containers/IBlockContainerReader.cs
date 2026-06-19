using Hakamiq.Cso.Core.Formats.DiscImage;

namespace Hakamiq.Cso.Core.Formats.Containers;

public interface IBlockContainerReader : IDisposable
{
    DetectedDiscFormat Format { get; }

    ulong UncompressedSize { get; }

    uint BlockSize { get; }

    int BlockCount { get; }

    int ReadBlock(int blockIndex, Span<byte> output);
}
