using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class DecompressCommand
{
    public static int Run(string[] args)
    {
        if (args.Length is not (3 or 4))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        string inputPath = args[0];

        int outputOptionIndex = Array.FindIndex(
            args,
            item => string.Equals(item, "-o", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item, "--output", StringComparison.OrdinalIgnoreCase));

        if (outputOptionIndex < 0 || outputOptionIndex + 1 >= args.Length)
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        string outputPath = args[outputOptionIndex + 1];
        bool force = args.Any(item => string.Equals(item, "--force", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine("CSO Decompression");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Input:  {Path.GetFullPath(inputPath)}");
        Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");

        CsoDecompressor decompressor = new();
        CsoDecompressResult result = decompressor.Decompress(
            new CsoDecompressOptions(inputPath, outputPath, force));

        if (result.Success)
        {
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");
            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");
        Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");

        return result.ErrorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "UnsupportedDecompressionVersion" => CliExitCodes.UnsupportedCsoVersion,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "OutputAccessDenied" or "DecompressionIoFailed" => CliExitCodes.CannotWriteOutput,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize"
                => CliExitCodes.InvalidCsoHeader,
            "IndexTableTruncated" or "IndexEntryTruncated" or "IndexOffsetsNotMonotonic" or "IndexOffsetPastEndOfFile"
                => CliExitCodes.CorruptIndexTable,
            _ => CliExitCodes.DecompressionFailed
        };
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: hakamiq-cso decompress <input.cso> -o <output.iso> [--force]");
    }
}