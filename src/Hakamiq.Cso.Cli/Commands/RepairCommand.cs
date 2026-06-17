using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class RepairCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out RepairCommandOptions options, out string? errorMessage))
        {
            if (HasJsonFlag(args))
            {
                JsonConsole.Write(new
                {
                    schemaVersion = 1,
                    command = "repair",
                    success = false,
                    input = args.Length > 0 ? SafeFullPath(args[0]) : null,
                    output = (string?)null,
                    format = (string?)null,
                    warnings = Array.Empty<string>(),
                    diagnostics = new { },
                    error = new CsoCommandError("InvalidArguments", errorMessage ?? "Invalid repair command arguments.")
                });
            }
            else
            {
                PrintUsage(errorMessage);
            }

            return CliExitCodes.InvalidArguments;
        }

        CsoRepairResult result = new CsoRepairer().Repair(
            new CsoRepairOptions(
                options.InputPath,
                options.OutputPath,
                options.Force,
                options.Profile,
                options.PadLastSector,
                options.DeepVerify));

        if (options.Json)
        {
            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "repair",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                output = SafeFullPath(options.OutputPath),
                format = result.InputFormat,
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    mode = result.Mode,
                    usedTempIso = result.UsedTempIso,
                    fallbackReason = result.FallbackReason
                },
                inputFormat = result.InputFormat,
                profile = CsoCompressionProfilePolicy.GetCliName(options.Profile),
                repair = new
                {
                    padLastSector = options.PadLastSector,
                    paddingBytes = result.PaddingBytes,
                    deepVerify = options.DeepVerify,
                    mode = result.Mode,
                    usedTempIso = result.UsedTempIso,
                    fallbackReason = result.FallbackReason
                },
                metrics = new
                {
                    bytesRead = result.BytesRead,
                    bytesWritten = result.BytesWritten
                },
                error = result.Success
                    ? null
                    : new CsoCommandError(result.ErrorCode ?? "RepairNotPossible", result.ErrorMessage ?? "Repair was not possible.")
            });

            return result.Success ? CliExitCodes.Success : ToExitCode(result.ErrorCode);
        }

        if (result.Success)
        {
            Console.WriteLine("CSO Repair");
            Console.WriteLine("----------");
            Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
            Console.WriteLine($"Output: {SafeFullPath(options.OutputPath)}");
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Input format: {result.InputFormat}");
            Console.WriteLine($"Repair mode: {result.Mode}");
            Console.WriteLine($"Used temp ISO: {result.UsedTempIso.ToString().ToLowerInvariant()}");
            Console.WriteLine($"Profile: {CsoCompressionProfilePolicy.GetCliName(options.Profile)}");
            Console.WriteLine($"Bytes read: {result.BytesRead:N0}");
            Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");

            if (result.PaddingBytes > 0)
            {
                Console.WriteLine($"Padding added: {result.PaddingBytes:N0}");
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");
        Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
        return ToExitCode(result.ErrorCode);
    }

    private static bool TryParseArgs(
        string[] args,
        out RepairCommandOptions options,
        out string? errorMessage)
    {
        options = default!;
        errorMessage = null;

        if (args.Length < 1)
        {
            errorMessage = "Missing input path.";
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool json = false;
        bool padLastSector = false;
        bool deepVerify = false;
        CsoCompressionProfile profile = CsoCompressionProfile.GameSafe;

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

                outputPath = args[++index];
                continue;
            }

            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = $"Missing profile value after --profile. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                string profileValue = args[++index];

                if (!CsoCompressionProfilePolicy.TryParse(profileValue, out profile))
                {
                    errorMessage = $"Invalid repair profile '{profileValue}'. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                if (profile != CsoCompressionProfile.GameSafe)
                {
                    errorMessage = "repair currently supports --profile game-safe only.";
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--deep-verify", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--deep", StringComparison.OrdinalIgnoreCase))
            {
                deepVerify = true;
                continue;
            }

            if (string.Equals(arg, "--repair", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing repair action after --repair.";
                    return false;
                }

                string action = args[++index];

                if (!string.Equals(action, "pad-last-sector", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Unsupported repair action: {action}";
                    return false;
                }

                padLastSector = true;
                continue;
            }

            if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            errorMessage = $"Unknown repair option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Missing input path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errorMessage = "Missing output path. Use -o <output.cso>.";
            return false;
        }

        options = new RepairCommandOptions(
            inputPath,
            outputPath,
            force,
            json,
            profile,
            padLastSector,
            DeepVerify: deepVerify || profile == CsoCompressionProfile.GameSafe);

        return true;
    }

    private static bool HasJsonFlag(string[] args)
    {
        return args.Any(static arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "IsoNotSectorAligned" => CliExitCodes.InvalidCsoHeader,
            "ReDumpRequired" or "CorruptCompressedBlock" => CliExitCodes.DecompressionFailed,
            "UnsupportedContainer" => CliExitCodes.UnsupportedCsoVersion,
            "DiskSpacePreflightFailed" => CliExitCodes.NotEnoughDiskSpace,
            _ => CliExitCodes.GeneralFailure,
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

    private static void PrintUsage(string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
        }

        Console.Error.WriteLine("Usage: hakamiq-cso repair <input.iso|input.cso> -o <output.cso> [--profile game-safe] [--repair pad-last-sector] [--deep-verify] [--force] [--json]");
    }

    private sealed record RepairCommandOptions(
        string InputPath,
        string OutputPath,
        bool Force,
        bool Json,
        CsoCompressionProfile Profile,
        bool PadLastSector,
        bool DeepVerify);
}
