using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class VerifyCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out VerifyCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        CsoVerifier verifier = new();
        CsoVerificationResult result = verifier.Verify(options.InputPath);

        if (options.Json)
        {
            JsonConsole.Write(new
            {
                command = "verify",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                header = result.Header is null
                    ? null
                    : new
                    {
                        version = result.Header.Version,
                        headerSize = result.Header.HeaderSize,
                        effectiveHeaderSize = result.Header.EffectiveHeaderSize,
                        uncompressedSize = result.Header.UncompressedSize,
                        blockSize = result.Header.BlockSize,
                        sectorCount = result.Header.SectorCount,
                        indexShift = result.Header.IndexShift,
                        indexEntryCount = result.Header.IndexEntryCount,
                        indexTableSizeBytes = result.Header.IndexTableSizeBytes
                    },
                index = result.Header is null
                    ? null
                    : new
                    {
                        entriesRead = result.Entries.Count,
                        expectedEntries = result.Header.IndexEntryCount
                    },
                issues = result.Issues.Select(issue => new
                {
                    code = issue.Code,
                    message = issue.Message
                }).ToArray()
            });

            return result.Success
                ? CliExitCodes.Success
                : ToExitCode(result.Issues.FirstOrDefault()?.Code);
        }

        Console.WriteLine("CSO Verification");
        Console.WriteLine("----------------");
        Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");

        if (result.Success && result.Header is not null)
        {
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Version:       {result.Header.Version}");
            Console.WriteLine($"Sectors:       {result.Header.SectorCount:N0}");
            Console.WriteLine($"Index entries: {result.Entries.Count:N0}");
            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");

        foreach (CsoVerificationIssue issue in result.Issues)
        {
            Console.Error.WriteLine($"{issue.Code}: {issue.Message}");
        }

        return ToExitCode(result.Issues.FirstOrDefault()?.Code);
    }

    private static bool TryParseArgs(
        string[] args,
        out VerifyCommandOptions options)
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

        options = new VerifyCommandOptions(inputPath, json);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidIndexShift"
                => CliExitCodes.InvalidCsoHeader,
            "UnsupportedVersion" or "UnsupportedCsoVersion" => CliExitCodes.UnsupportedCsoVersion,
            "IndexTableTruncated" or "IndexEntryTruncated" or "IndexOffsetsNotMonotonic" or "IndexOffsetPastEndOfFile" or "FinalOffsetPastEndOfFile" or "FirstDataOffsetBeforeIndexEnd" or "IndexEntryCountMismatch"
                => CliExitCodes.CorruptIndexTable,
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
        Console.Error.WriteLine("Usage: hakamiq-cso verify <input.cso> [--json]");
    }

    private sealed record VerifyCommandOptions(
        string InputPath,
        bool Json);
}
