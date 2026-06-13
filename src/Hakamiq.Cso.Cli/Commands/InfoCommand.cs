using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class InfoCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out InfoCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        CsoHeaderReader reader = new();
        CsoHeaderReadResult result = reader.Read(options.InputPath);

        if (!result.Success || result.Header is null)
        {
            if (options.Json)
            {
                JsonConsole.Write(new
                {
                    command = "info",
                    success = false,
                    input = SafeFullPath(options.InputPath),
                    error = new
                    {
                        code = result.ErrorCode,
                        message = result.ErrorMessage
                    }
                });
            }
            else
            {
                Console.Error.WriteLine("CSO Info");
                Console.Error.WriteLine("--------");
                Console.Error.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
                Console.Error.WriteLine("Status: FAILED");
                Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
            }

            return ToExitCode(result.ErrorCode);
        }

        CsoHeader header = result.Header;

        if (options.Json)
        {
            JsonConsole.Write(new
            {
                command = "info",
                success = true,
                input = SafeFullPath(options.InputPath),
                header = new
                {
                    version = header.Version,
                    headerSize = header.HeaderSize,
                    uncompressedSize = header.UncompressedSize,
                    blockSize = header.BlockSize,
                    sectorCount = header.SectorCount,
                    indexShift = header.IndexShift,
                    indexEntryCount = header.IndexEntryCount,
                    indexTableSizeBytes = header.IndexTableSizeBytes
                }
            });

            return CliExitCodes.Success;
        }

        Console.WriteLine("CSO Info");
        Console.WriteLine("--------");
        Console.WriteLine($"Input:              {SafeFullPath(options.InputPath)}");
        Console.WriteLine($"Version:            {header.Version}");
        Console.WriteLine($"Header size:        {header.HeaderSize:N0}");
        Console.WriteLine($"Uncompressed size:  {header.UncompressedSize:N0}");
        Console.WriteLine($"Block size:         {header.BlockSize:N0}");
        Console.WriteLine($"Sector count:       {header.SectorCount:N0}");
        Console.WriteLine($"Index shift:        {header.IndexShift}");
        Console.WriteLine($"Index entries:      {header.IndexEntryCount:N0}");
        Console.WriteLine($"Index table bytes:  {header.IndexTableSizeBytes:N0}");

        return CliExitCodes.Success;
    }

    private static bool TryParseArgs(
        string[] args,
        out InfoCommandOptions options)
    {
        options = default!;

        if (args.Length is not (1 or 2))
        {
            return false;
        }

        string inputPath = args[0];
        bool json = false;

        for (int index = 1; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        options = new InfoCommandOptions(inputPath, json);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize"
                => CliExitCodes.InvalidCsoHeader,
            _ => CliExitCodes.GeneralFailure
        };
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: hakamiq-cso info <input.cso> [--json]");
    }

    private sealed record InfoCommandOptions(
        string InputPath,
        bool Json);
}
