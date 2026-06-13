namespace Hakamiq.Cso.Core.Formats.Cso;

public static class CsoConstants
{
    public const int MinimumHeaderSize = 24;
    public const string MagicText = "CISO";

    public static readonly byte[] MagicBytes =
    [
        (byte)'C',
        (byte)'I',
        (byte)'S',
        (byte)'O'
    ];
}