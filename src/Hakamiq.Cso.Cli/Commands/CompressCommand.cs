using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class CompressCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out CompressCommandOptions options, out string? parseError))
        {
            string errorMessage = parseError ?? "Invalid compress command arguments.";

            if (HasJsonFlag(args))
            {
                JsonConsole.Write(CsoCompressJsonContract.ArgumentError(errorMessage));
            }
            else
            {
                PrintUsage(errorMessage);
            }

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
            return options.Measure
                ? RunMeasure(options, cancellation.Token)
                : RunCompress(options, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static int RunMeasure(
        CompressCommandOptions options,
        CancellationToken cancellationToken)
    {
        CsoCompressionProfileSettings profileSettings = CsoCompressionProfilePolicy.Create(options.Profile);

        if (!options.Quiet && !options.Json)
        {
            Console.WriteLine("CSO Measure");
            Console.WriteLine("-----------");
            Console.WriteLine($"Input: {SafeFullPath(options.InputPath)}");
            Console.WriteLine("Mode:  measure only; no output file will be written.");
        }

        ConsoleCompressProgress? progress = options.Quiet || options.Json
            ? null
            : new ConsoleCompressProgress();

        CsoMeasureEstimator estimator = new();
        CsoMeasureResult result = estimator.Measure(
            new CsoMeasureOptions(
                options.InputPath,
                CsoCompressor.DefaultBlockSize,
                cancellationToken,
                progress,
                options.Profile));

        progress?.FinishLine();

        if (options.Json)
        {
            JsonConsole.Write(CsoCompressJsonContract.Measure(
                SafeFullPath(options.InputPath),
                profileSettings,
                result));

            return result.Success
                ? CliExitCodes.Success
                : ToExitCode(result.ErrorCode);
        }

        if (result.Success)
        {
            if (!options.Quiet)
            {
                Console.WriteLine("Status: OK");
                Console.WriteLine($"Original size: {result.OriginalBytes:N0}");
                Console.WriteLine($"Estimated CSO size: {result.EstimatedBytes:N0}");
                Console.WriteLine($"Estimated ratio: {result.EstimatedRatio:P2}");
                Console.WriteLine($"Estimated saved space: {result.EstimatedSavedBytes:N0}");

                if (result.EstimatedGrowthBytes > 0)
                {
                    Console.WriteLine($"Estimated growth: {result.EstimatedGrowthBytes:N0}");
                }

                Console.WriteLine($"Total blocks: {result.TotalBlocks:N0}");
                Console.WriteLine($"Compressed blocks: {result.CompressedBlocks:N0}");
                Console.WriteLine($"Stored blocks: {result.StoredBlocks:N0}");
                PrintProfileSummary(profileSettings);
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");
        Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
        PrintProfileSummary(profileSettings, Console.Error);

        return ToExitCode(result.ErrorCode);
    }

    private static int RunCompress(
        CompressCommandOptions options,
        CancellationToken cancellationToken)
    {
        CsoCompressionProfileSettings profileSettings = CsoCompressionProfilePolicy.Create(options.Profile);
        string outputPath = options.OutputPath ?? new CsoOutputPathPolicy().CreateCompressionOutputPath(options.InputPath);
        bool autoOutput = options.OutputPath is null;

        if (!options.Quiet && !options.Json)
        {
            Console.WriteLine("CSO Compression");
            Console.WriteLine("---------------");
            Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
            Console.WriteLine($"Output: {SafeFullPath(outputPath)}");

            if (autoOutput)
            {
                Console.WriteLine("Output mode: same folder; auto-named without overwriting existing files.");
            }
        }

        ConsoleCompressProgress? progress = options.Quiet || options.Json
            ? null
            : new ConsoleCompressProgress();

        CsoCompressor compressor = new();
        CsoCompressResult result = compressor.Compress(
            new CsoCompressOptions(
                options.InputPath,
                outputPath,
                options.Force && !autoOutput,
                CsoCompressor.DefaultBlockSize,
                cancellationToken,
                progress,
                options.Profile));

        progress?.FinishLine();

        if (options.Json)
        {
            JsonConsole.Write(CsoCompressJsonContract.Write(
                SafeFullPath(options.InputPath),
                SafeFullPath(outputPath),
                options.Force && !autoOutput,
                autoOutput,
                profileSettings,
                result));

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
                PrintProfileSummary(profileSettings);
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");
        Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
        PrintProfileSummary(profileSettings, Console.Error);

        return ToExitCode(result.ErrorCode);
    }

    private static bool TryParseArgs(
        string[] args,
        out CompressCommandOptions options,
        out string? errorMessage)
    {
        options = default!;
        errorMessage = null;

        if (args.Length < 1)
        {
            errorMessage = "Missing input ISO path.";
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool quiet = false;
        bool json = false;
        bool measure = false;
        bool fastAlias = false;
        bool profileExplicit = false;
        CsoCompressionProfile profile = CsoCompressionProfilePolicy.DefaultProfile;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null)
                {
                    errorMessage = "Output path was provided more than once.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing output path after -o.";
                    return false;
                }

                outputPath = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase))
            {
                if (profileExplicit)
                {
                    errorMessage = "Compression profile was provided more than once.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    errorMessage = $"Missing profile value after --profile. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                string profileValue = args[index + 1];

                if (!CsoCompressionProfilePolicy.TryParse(profileValue, out CsoCompressionProfile parsedProfile))
                {
                    errorMessage = $"Invalid compression profile '{profileValue}'. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                if (fastAlias && parsedProfile != CsoCompressionProfile.Fast)
                {
                    errorMessage = BuildFastProfileConflictMessage(parsedProfile);
                    return false;
                }

                profile = parsedProfile;
                profileExplicit = true;
                index++;
                continue;
            }

            if (string.Equals(arg, "--fast", StringComparison.OrdinalIgnoreCase))
            {
                if (profileExplicit && profile != CsoCompressionProfile.Fast)
                {
                    errorMessage = BuildFastProfileConflictMessage(profile);
                    return false;
                }

                profile = CsoCompressionProfile.Fast;
                fastAlias = true;
                continue;
            }

            if (string.Equals(arg, "--measure", StringComparison.OrdinalIgnoreCase))
            {
                measure = true;
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

            errorMessage = $"Unknown compress option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Missing input ISO path.";
            return false;
        }

        if (measure && outputPath is not null)
        {
            errorMessage = "--measure does not write output and cannot be combined with -o.";
            return false;
        }

        options = new CompressCommandOptions(
            inputPath,
            outputPath,
            force,
            quiet,
            json,
            measure,
            profile);

        return true;
    }

    private static string BuildFastProfileConflictMessage(CsoCompressionProfile profile)
    {
        string profileName = CsoCompressionProfilePolicy.GetCliName(profile);
        return $"--fast cannot be combined with --profile {profileName}. Use --profile fast or remove --fast.";
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "NotEnoughDiskSpace" => CliExitCodes.NotEnoughDiskSpace,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "SameInputOutputPath" or "OutputPathIsDirectory" or "OutputDirectoryNotFound" or "InvalidOutputPath" or "InvalidInputSize" => CliExitCodes.CannotWriteOutput,
            "OutputAccessDenied" or "CompressionIoFailed" or "OutputDriveCheckFailed" or "OutputDriveNotReady" or "OutputDriveNotFound" => CliExitCodes.CannotWriteOutput,
            "InputAccessDenied" or "MeasureIoFailed" => CliExitCodes.CannotWriteOutput,
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

    private static bool HasJsonFlag(string[] args)
    {
        return args.Any(static arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));
    }

    private static void PrintProfileSummary(CsoCompressionProfileSettings profileSettings)
    {
        PrintProfileSummary(profileSettings, Console.Out);
    }

    private static void PrintProfileSummary(
        CsoCompressionProfileSettings profileSettings,
        TextWriter writer)
    {
        writer.WriteLine($"Profile: {profileSettings.CliName}");
        writer.WriteLine($"Fast: {profileSettings.IsFast.ToString().ToLowerInvariant()}");
        writer.WriteLine($"Level: {profileSettings.Level}");
    }

    private static void PrintUsage(string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
        }

        Console.Error.WriteLine($"Usage: hakamiq-cso compress <input.iso> [-o <output.cso>] [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--force] [--quiet] [--json]");
        Console.Error.WriteLine($"       hakamiq-cso compress <input.iso> --measure [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--quiet] [--json]");
    }

    private sealed record CompressCommandOptions(
        string InputPath,
        string? OutputPath,
        bool Force,
        bool Quiet,
        bool Json,
        bool Measure,
        CsoCompressionProfile Profile);

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
