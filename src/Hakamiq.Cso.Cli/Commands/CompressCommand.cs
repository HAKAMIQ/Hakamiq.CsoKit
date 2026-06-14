using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class CompressCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out CompressCommandOptions options))
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
                Console.WriteLine("CSO Compression");
                Console.WriteLine("---------------");
                Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
                Console.WriteLine($"Output: {SafeFullPath(options.OutputPath)}");
            }

            ConsoleCompressProgress? progress = options.Quiet || options.Json
                ? null
                : new ConsoleCompressProgress();

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(
                    options.InputPath,
                    options.OutputPath,
                    options.Force,
                    CsoCompressor.DefaultBlockSize,
                    cancellation.Token,
                    progress));

            progress?.FinishLine();

            if (options.Json)
            {
                JsonConsole.Write(new
                {
                    command = "compress",
                    success = result.Success,
                    input = SafeFullPath(options.InputPath),
                    output = SafeFullPath(options.OutputPath),
                    force = options.Force,
                    bytesRead = result.BytesRead,
                    bytesWritten = result.BytesWritten,
                    compressedBlocks = result.CompressedBlocks,
                    storedBlocks = result.StoredBlocks,
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
                    Console.WriteLine($"Bytes read: {result.BytesRead:N0}");
                    Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");
                    Console.WriteLine($"Compressed blocks: {result.CompressedBlocks:N0}");
                    Console.WriteLine($"Stored blocks: {result.StoredBlocks:N0}");
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
        out CompressCommandOptions options)
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

        options = new CompressCommandOptions(
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
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "NotEnoughDiskSpace" => CliExitCodes.NotEnoughDiskSpace,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "SameInputOutputPath" or "OutputPathIsDirectory" or "InvalidOutputPath" or "InvalidInputSize" => CliExitCodes.CannotWriteOutput,
            "OutputAccessDenied" or "CompressionIoFailed" or "OutputDriveCheckFailed" or "OutputDriveNotReady" or "OutputDriveNotFound" => CliExitCodes.CannotWriteOutput,
            "InvalidBlockSize" or "BlockSizeTooLarge" => CliExitCodes.InvalidCsoHeader,
            _ => CliExitCodes.CompressionFailed
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
        Console.Error.WriteLine("Usage: hakamiq-cso compress <input.iso> -o <output.cso> [--force] [--quiet] [--json]");
    }

    private sealed record CompressCommandOptions(
        string InputPath,
        string OutputPath,
        bool Force,
        bool Quiet,
        bool Json);

    private sealed class ConsoleCompressProgress : IProgress<CsoCompressProgress>
    {
        private bool hasWritten;

        public void Report(CsoCompressProgress value)
        {
            hasWritten = true;

            Console.Write(
                $"\rProgress: {value.Percent,6:0.0}%  Blocks: {value.CompletedBlocks:N0}/{value.TotalBlocks:N0}  Bytes: {value.BytesRead:N0}/{value.TotalBytes:N0}");
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