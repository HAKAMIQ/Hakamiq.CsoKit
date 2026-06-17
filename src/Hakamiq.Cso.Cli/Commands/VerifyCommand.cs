using Hakamiq.Cso.Core.Formats.Containers;
using Hakamiq.Cso.Core.Formats.Cso;
using Hakamiq.Cso.Core.Formats.DiscImage;

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

        if (options.Deep)
        {
            return RunDeepVerify(options);
        }

        CsoVerifier verifier = new();
        CsoVerificationResult result = verifier.Verify(options.InputPath);

        if (options.Json)
        {
            object? header = result.Header is null
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
                };

            object? index = result.Header is null
                ? null
                : new
                {
                    entriesRead = result.Entries.Count,
                    expectedEntries = result.Header.IndexEntryCount
                };

            string? firstCode = result.Issues.FirstOrDefault()?.Code;
            string? firstMessage = result.Issues.FirstOrDefault()?.Message;

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "verify",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = "Cso1",
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    header,
                    index,
                    issues = result.Issues.Select(issue => new
                    {
                        code = issue.Code,
                        message = issue.Message
                    }).ToArray()
                },
                header,
                index,
                issues = result.Issues.Select(issue => new
                {
                    code = issue.Code,
                    message = issue.Message
                }).ToArray(),
                error = result.Success
                    ? null
                    : new CsoCommandError(firstCode ?? "VerificationFailed", firstMessage ?? "CSO verification failed.")
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

    private static int RunDeepVerify(VerifyCommandOptions options)
    {
        FormatDetectionResult detected = new FormatDetector().Detect(options.InputPath);
        CsoDeepVerifyResult result;

        if (!detected.Success)
        {
            result = CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    detected.ErrorCode ?? "FormatDetectionFailed",
                    detected.ErrorMessage ?? "Format detection failed.")]);
        }
        else
        {
            result = detected.Format switch
            {
                DetectedDiscFormat.Cso1 => new CsoDeepVerifier().Verify(
                    options.InputPath,
                    options.Sha256),
                DetectedDiscFormat.Cso2 or
                    DetectedDiscFormat.Zso or
                    DetectedDiscFormat.Dax => RunContainerDeepVerify(
                        options.InputPath,
                        detected.Format,
                        options.Sha256),
                _ => CsoDeepVerifyResult.Fail(
                    header: null,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "UnsupportedContainer",
                        $"{detected.Format} is not supported by deep verification. Use CSO1, CSO2, ZSO, or DAX input.")]),
            };
        }

        if (options.Json)
        {
            object? header = result.Header is null
                ? null
                : new
                {
                    version = result.Header.Version,
                    uncompressedSize = result.Header.UncompressedSize,
                    blockSize = result.Header.BlockSize,
                    sectorCount = result.Header.SectorCount,
                    indexShift = result.Header.IndexShift
                };

            object deep = new
            {
                blocksChecked = result.BlocksChecked,
                bytesReconstructed = result.BytesReconstructed,
                sha256 = result.Sha256
            };

            var issues = result.Issues.Select(issue => new
            {
                code = issue.Code,
                message = issue.Message,
                blockIndex = issue.BlockIndex
            }).ToArray();

            string? firstCode = result.Issues.FirstOrDefault()?.Code;
            string? firstMessage = result.Issues.FirstOrDefault()?.Message;

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "verify",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = detected.Success ? detected.Format.ToString() : null,
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    mode = "deep",
                    header,
                    deep,
                    issues
                },
                mode = "deep",
                header,
                deep,
                issues,
                error = result.Success
                    ? null
                    : new CsoCommandError(firstCode ?? "VerificationFailed", firstMessage ?? "Deep verification failed.")
            });

            return result.Success
                ? CliExitCodes.Success
                : ToExitCode(result.Issues.FirstOrDefault()?.Code);
        }

        Console.WriteLine("Deep Verification");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");

        if (detected.Success)
        {
            Console.WriteLine($"Format: {detected.Format}");
        }

        if (result.Success)
        {
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Blocks checked:      {result.BlocksChecked:N0}");
            Console.WriteLine($"Bytes reconstructed: {result.BytesReconstructed:N0}");

            if (!string.IsNullOrWhiteSpace(result.Sha256))
            {
                Console.WriteLine($"SHA256:              {result.Sha256}");
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");

        foreach (CsoDeepVerifyIssue issue in result.Issues)
        {
            Console.Error.WriteLine($"{issue.Code}: {issue.Message}");
        }

        return ToExitCode(result.Issues.FirstOrDefault()?.Code);
    }

    private static CsoDeepVerifyResult RunContainerDeepVerify(
        string inputPath,
        DetectedDiscFormat format,
        bool computeSha256)
    {
        try
        {
            using IBlockContainerReader reader = CreateContainerReader(inputPath, format);
            return new ContainerDeepVerifier().Verify(reader, computeSha256);
        }
        catch (BlockContainerReadException ex)
        {
            return CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(ex.Code, ex.Message, ex.BlockIndex)]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return CsoDeepVerifyResult.Fail(
                header: null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("DeepVerifyIoFailed", ex.Message)]);
        }
    }

    private static IBlockContainerReader CreateContainerReader(
        string inputPath,
        DetectedDiscFormat format)
    {
        return format switch
        {
            DetectedDiscFormat.Cso2 => new Cso2ContainerReader(inputPath),
            DetectedDiscFormat.Zso => new ZsoContainerReader(inputPath),
            DetectedDiscFormat.Dax => new DaxContainerReader(inputPath),
            _ => throw new BlockContainerReadException(
                "UnsupportedContainer",
                $"{format} is not supported by container deep verification."),
        };
    }

    private static bool TryParseArgs(
        string[] args,
        out VerifyCommandOptions options)
    {
        options = default!;

        if (args.Length < 1)
        {
            return false;
        }

        string inputPath = args[0];
        bool json = false;
        bool deep = false;
        bool sha256 = false;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (string.Equals(arg, "--deep", StringComparison.OrdinalIgnoreCase))
            {
                deep = true;
                continue;
            }

            if (string.Equals(arg, "--sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256 = true;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        if (sha256 && !deep)
        {
            return false;
        }

        options = new VerifyCommandOptions(inputPath, json, deep, sha256);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidIndexShift"
                => CliExitCodes.InvalidCsoHeader,
            "UnsupportedVersion" or "UnsupportedCsoVersion" or "UnsupportedContainer"
                => CliExitCodes.UnsupportedCsoVersion,
            "IndexTableTruncated" or "IndexEntryTruncated" or "IndexOffsetsNotMonotonic" or "IndexOffsetPastEndOfFile" or "FinalOffsetPastEndOfFile" or "FirstDataOffsetBeforeIndexEnd" or "IndexEntryCountMismatch" or "FinalIndexEntryHasFlag" or "FinalOffsetMismatch"
                => CliExitCodes.CorruptIndexTable,
            "CorruptCompressedBlock" or "CsoDeepVerifyFailed" or "InvalidCompressedBlockSize" or "StoredBlockTooSmall" or "UnexpectedEndOfFile" or "ReconstructedSizeMismatch" or "ReDumpRequired"
                => CliExitCodes.DecompressionFailed,
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
        Console.Error.WriteLine("Usage: hakamiq-cso verify <input.cso|input.zso|input.dax> [--deep] [--sha256] [--json]");
    }

    private sealed record VerifyCommandOptions(
        string InputPath,
        bool Json,
        bool Deep,
        bool Sha256);
}
