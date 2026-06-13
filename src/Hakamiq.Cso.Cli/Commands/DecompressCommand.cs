using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class DecompressCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out DecompressCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        using CancellationTokenSource cancellation = new();

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;

            if (!cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();

                if (!options.Quiet && !options.Json)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Cancellation requested. Cleaning up...");
                }
            }
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            if (!options.Quiet && !options.Json)
            {
                Console.WriteLine("CSO Decompression");
                Console.WriteLine("-----------------");
                Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
                Console.WriteLine($"Output: {SafeFullPath(options.OutputPath)}");
            }

            ConsoleDecompressProgress? progress = options.Quiet || options.Json
                ? null
                : new ConsoleDecompressProgress();

            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(
                    options.InputPath,
                    options.OutputPath,
                    options.Force,
                    cancellation.Token,
                    progress));

            progress?.FinishLine();

            if (options.Json)
            {
                JsonConsole.Write(new
                {
                    command = "decompress",
                    success = result.Success,
                    input = SafeFullPath(options.InputPath),
                    output = SafeFullPath(options.OutputPath),
                    force = options.Force,
                    bytesWritten = result.BytesWritten,
                    error = result.Success
                        ? null
                        : new
                        {
                            code = result.ErrorCode,
                            message = result.ErrorMessage
                        }
                });

                return result.Success
                    ? CliExitCodes.Success
                    : ToExitCode(result.ErrorCode);
            }

            if (result.Success)
            {
                if (!options.Quiet)
                {
                    Console.WriteLine("Status: OK");
                    Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");
                }

                return CliExitCodes.Success;
            }

            Console.Error.WriteLine("Status: FAILED");
            Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");

            return ToExitCode(result.ErrorCode);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static bool TryParseArgs(
        string[] args,
        out DecompressCommandOptions options)
    {
        options = default!;

        if (args.Length < 3)
        {
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool quiet = false;
        bool json = false;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null || index + 1 >= args.Length)
                {
                    return false;
                }

                outputPath = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
            {
                quiet = true;
                continue;
            }

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath) ||
            string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        options = new DecompressCommandOptions(
            inputPath,
            outputPath,
            force,
            quiet,
            json);

        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "UnsupportedVersion" or "UnsupportedCsoVersion" or "UnsupportedDecompressionVersion" => CliExitCodes.UnsupportedCsoVersion,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "NotEnoughDiskSpace" => CliExitCodes.NotEnoughDiskSpace,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "SameInputOutputPath" or "OutputPathIsDirectory" or "InvalidOutputPath" => CliExitCodes.CannotWriteOutput,
            "OutputAccessDenied" or "DecompressionIoFailed" or "OutputDriveCheckFailed" or "OutputDriveNotReady" or "OutputDriveNotFound" => CliExitCodes.CannotWriteOutput,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidIndexShift"
                => CliExitCodes.InvalidCsoHeader,
            "IndexTableTruncated" or "IndexEntryTruncated" or "IndexOffsetsNotMonotonic" or "IndexOffsetPastEndOfFile" or "FinalOffsetPastEndOfFile" or "FirstDataOffsetBeforeIndexEnd" or "IndexEntryCountMismatch"
                => CliExitCodes.CorruptIndexTable,
            _ => CliExitCodes.DecompressionFailed
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
        Console.Error.WriteLine("Usage: hakamiq-cso decompress <input.cso> -o <output.iso> [--force] [--quiet] [--json]");
    }

    private sealed record DecompressCommandOptions(
        string InputPath,
        string OutputPath,
        bool Force,
        bool Quiet,
        bool Json);

    private sealed class ConsoleDecompressProgress : IProgress<CsoDecompressProgress>
    {
        private bool hasWritten;

        public void Report(CsoDecompressProgress value)
        {
            hasWritten = true;

            Console.Write(
                $"\rProgress: {value.Percent,6:0.0}%  Blocks: {value.CompletedBlocks:N0}/{value.TotalBlocks:N0}  Bytes: {value.BytesWritten:N0}/{value.TotalBytes:N0}");
        }

        public void FinishLine()
        {
            if (hasWritten)
            {
                Console.WriteLine();
            }
        }
    }
}
