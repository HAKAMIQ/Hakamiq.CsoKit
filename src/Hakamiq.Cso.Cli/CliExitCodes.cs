namespace Hakamiq.Cso.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int InvalidArguments = 2;
    public const int InputNotFound = 10;
    public const int InvalidCsoHeader = 11;
    public const int UnsupportedCsoVersion = 12;
    public const int CorruptIndexTable = 13;
}