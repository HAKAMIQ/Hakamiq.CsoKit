using System.Reflection;

namespace Hakamiq.Cso.Cli.Commands;

public static class CsoCommandDispatcher
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return CliExitCodes.InvalidArguments;
        }

        string command = args[0].Trim().ToLowerInvariant();

        return command switch
        {
            "info" => InfoCommand.Run(args[1..]),
            "verify" => VerifyCommand.Run(args[1..]),
            "decompress" => DecompressCommand.Run(args[1..]),
            "compress" => CompressCommand.Run(args[1..]),
            "native-info" => NativeInfoCommand.Run(),
            "--help" or "-h" or "help" => PrintHelpAndReturnSuccess(),
            "--version" or "-v" => PrintVersionAndReturnSuccess(),
            _ => UnknownCommand(command)
        };
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return CliExitCodes.InvalidArguments;
    }

    private static int PrintHelpAndReturnSuccess()
    {
        PrintHelp();
        return CliExitCodes.Success;
    }

    private static int PrintVersionAndReturnSuccess()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        Console.WriteLine($"Hakamiq.CsoKit {version}");
        return CliExitCodes.Success;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Hakamiq.CsoKit");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  hakamiq-cso info <input.cso> [--json]");
        Console.WriteLine("  hakamiq-cso verify <input.cso> [--json]");
        Console.WriteLine("  hakamiq-cso decompress <input.cso> [-o <output.iso>] [--force] [--quiet] [--json]");
        Console.WriteLine("  hakamiq-cso compress <input.iso> [-o <output.cso>] [--force] [--quiet] [--json]");
        Console.WriteLine("  hakamiq-cso compress <input.iso> --measure [--quiet] [--json]");
        Console.WriteLine("  hakamiq-cso native-info");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info         Read and print CSO header information.");
        Console.WriteLine("  verify       Validate CSO header and index table.");
        Console.WriteLine("  decompress   Decompress CSO to ISO.");
        Console.WriteLine("  compress     Compress ISO to CSO.");
        Console.WriteLine("  native-info  Show native backend availability.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help       Show help.");
        Console.WriteLine("  --version    Show version.");
    }
}