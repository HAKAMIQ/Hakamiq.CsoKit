namespace Hakamiq.Cso.Core.Formats.Containers;

public sealed class BlockContainerReadException : Exception
{
    public BlockContainerReadException(
        string code,
        string message,
        int? blockIndex = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        BlockIndex = blockIndex;
    }

    public string Code { get; }

    public int? BlockIndex { get; }
}
