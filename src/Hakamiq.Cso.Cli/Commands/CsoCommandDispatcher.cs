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
        Console.WriteLine("Hakamiq.CsoKit 0.3.0-dev");
        return CliExitCodes.Success;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Hakamiq.CsoKit");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  hakamiq-cso info <input.cso>");
        Console.WriteLine("  hakamiq-cso verify <input.cso>");
        Console.WriteLine("  hakamiq-cso decompress <input.cso> -o <output.iso> [--force]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info        Read and print CSO header information.");
        Console.WriteLine("  verify      Validate CSO header and index table.");
        Console.WriteLine("  decompress  Decompress CSO v1 to ISO.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help      Show help.");
        Console.WriteLine("  --version   Show version.");
    }
}