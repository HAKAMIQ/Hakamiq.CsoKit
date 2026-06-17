using System.Reflection;
using Hakamiq.Cso.Core.Formats.Cso;

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
            "detect" => DetectCommand.Run(args[1..]),
            "analyze" => AnalyzeCommand.Run(args[1..]),
            "info" => InfoCommand.Run(args[1..]),
            "verify" => VerifyCommand.Run(args[1..]),
            "repair" => RepairCommand.Run(args[1..]),
            "decompress" => DecompressCommand.Run(args[1..]),
            "compress" => CompressCommand.Run(args[1..]),
            "codecs" => CodecsCommand.Run(),
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
        Console.WriteLine("  hakamiq-cso detect <input> [--json]");
        Console.WriteLine("  hakamiq-cso analyze <input.iso> [--psp] [--json]");
        Console.WriteLine("  hakamiq-cso info <input.cso> [--json]");
        Console.WriteLine("  hakamiq-cso verify <input.cso> [--deep] [--sha256] [--json]");
        Console.WriteLine("  hakamiq-cso repair <input.iso|input.cso> -o <output.cso> [--profile game-safe] [--repair pad-last-sector] [--deep-verify] [--force] [--json]");
        Console.WriteLine("  hakamiq-cso decompress <input.cso> [-o <output.iso>] [--force] [--quiet] [--json]");
        Console.WriteLine($"  hakamiq-cso compress <input.iso> [-o <output.cso>] [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--threads <n>] [--block <bytes>] [--zopfli] [--deep-verify] [--codec-report] [--force] [--quiet] [--json]");
        Console.WriteLine($"  hakamiq-cso compress <input.iso> --measure [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--block <bytes>] [--zopfli] [--quiet] [--json]");
        Console.WriteLine("  hakamiq-cso codecs");
        Console.WriteLine("  hakamiq-cso native-info");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  detect       Detect ISO/CSO/ZSO/DAX intake format.");
        Console.WriteLine("  analyze      Validate PSP ISO structure without modifying it.");
        Console.WriteLine("  info         Read and print CSO header information.");
        Console.WriteLine("  verify       Validate CSO header/index, or every block with --deep.");
        Console.WriteLine("  repair       Rebuild readable ISO/CSO/ZSO/DAX input into game-safe CSO1.");
        Console.WriteLine("  decompress   Decompress CSO to ISO.");
        Console.WriteLine("  compress     Compress ISO to CSO.");
        Console.WriteLine("  codecs       Show codec matrix and native availability.");
        Console.WriteLine("  native-info  Show native backend availability.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  hakamiq-cso detect game.iso");
        Console.WriteLine("  hakamiq-cso analyze game.iso --psp");
        Console.WriteLine("  hakamiq-cso compress game.iso --profile game-safe --deep-verify");
        Console.WriteLine("  hakamiq-cso verify game.cso --deep --sha256");
        Console.WriteLine("  hakamiq-cso repair old.zso -o fixed.cso --deep-verify");
        Console.WriteLine("  hakamiq-cso codecs");
        Console.WriteLine("  hakamiq-cso native-info");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help       Show help.");
        Console.WriteLine("  --version    Show version.");
    }
}
