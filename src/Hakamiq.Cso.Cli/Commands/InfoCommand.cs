using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.Cli.Commands;

public static class InfoCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: hakamiq-cso info <input.cso>");
            return CliExitCodes.InvalidArguments;
        }

        string inputPath = args[0];
        CsoHeaderReader reader = new();
        CsoHeaderReadResult result = reader.Read(inputPath);

        if (!result.Success || result.Header is null)
        {
            Console.Error.WriteLine(result.ErrorMessage);

            return result.ErrorCode switch
            {
                "InputNotFound" => CliExitCodes.InputNotFound,
                "UnsupportedVersion" => CliExitCodes.UnsupportedCsoVersion,
                _ => CliExitCodes.InvalidCsoHeader
            };
        }

        CsoHeader header = result.Header;

        Console.WriteLine("CSO Information");
        Console.WriteLine("----------------");
        Console.WriteLine($"Path:              {Path.GetFullPath(inputPath)}");
        Console.WriteLine($"Version:           {header.Version}");
        Console.WriteLine($"Header size:       {header.HeaderSize:N0} bytes");
        Console.WriteLine($"Uncompressed size: {header.UncompressedSize:N0} bytes");
        Console.WriteLine($"Block size:        {header.BlockSize:N0} bytes");
        Console.WriteLine($"Sector count:      {header.SectorCount:N0}");
        Console.WriteLine($"Index shift:       {header.IndexShift}");

        return CliExitCodes.Success;
    }
}