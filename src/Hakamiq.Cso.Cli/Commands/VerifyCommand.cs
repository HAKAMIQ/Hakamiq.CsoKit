using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class VerifyCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: hakamiq-cso verify <input.cso>");
            return CliExitCodes.InvalidArguments;
        }

        string inputPath = args[0];
        CsoVerifier verifier = new();
        CsoVerificationResult result = verifier.Verify(inputPath);

        if (result.Success && result.Header is not null)
        {
            Console.WriteLine("CSO Verification");
            Console.WriteLine("----------------");
            Console.WriteLine($"Path:         {Path.GetFullPath(inputPath)}");
            Console.WriteLine($"Status:       OK");
            Console.WriteLine($"Version:      {result.Header.Version}");
            Console.WriteLine($"Sector count: {result.Header.SectorCount:N0}");
            Console.WriteLine($"Index entries: {result.Entries.Count:N0}");

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("CSO Verification");
        Console.Error.WriteLine("----------------");
        Console.Error.WriteLine($"Path:   {Path.GetFullPath(inputPath)}");
        Console.Error.WriteLine("Status: FAILED");

        foreach (CsoVerificationIssue issue in result.Issues)
        {
            if (issue.BlockIndex is null)
            {
                Console.Error.WriteLine($"- {issue.Code}: {issue.Message}");
            }
            else
            {
                Console.Error.WriteLine($"- {issue.Code} [block {issue.BlockIndex:N0}]: {issue.Message}");
            }
        }

        string? firstCode = result.Issues.FirstOrDefault()?.Code;

        return firstCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "UnsupportedVersion" => CliExitCodes.UnsupportedCsoVersion,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize"
                => CliExitCodes.InvalidCsoHeader,
            _ => CliExitCodes.CorruptIndexTable
        };
    }
}
