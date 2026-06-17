namespace Hakamiq.Cso.Core.Formats.Cso;

public sealed class CsoOutputPathPolicy
{
    private const string ConvertedSuffix = " - Hakamiq Converted";

    public string CreateCompressionOutputPath(string inputPath)
    {
        return CreateSiblingOutputPath(inputPath, ".cso");
    }

    public string CreateDecompressionOutputPath(string inputPath)
    {
        return CreateSiblingOutputPath(inputPath, ".iso");
    }

    private static string CreateSiblingOutputPath(
        string inputPath,
        string outputExtension)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is empty.", nameof(inputPath));
        }

        string fullInputPath = Path.GetFullPath(inputPath);
        string directory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string baseName = Path.GetFileNameWithoutExtension(fullInputPath);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Hakamiq Converted";
        }

        string preferredPath = Path.Combine(directory, baseName + outputExtension);

        if (!File.Exists(preferredPath) && !Directory.Exists(preferredPath))
        {
            return preferredPath;
        }

        string convertedPath = Path.Combine(directory, baseName + ConvertedSuffix + outputExtension);

        if (!File.Exists(convertedPath) && !Directory.Exists(convertedPath))
        {
            return convertedPath;
        }

        for (int number = 2; number < int.MaxValue; number++)
        {
            string candidate = Path.Combine(directory, $"{baseName}{ConvertedSuffix} {number}{outputExtension}");

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create a unique output file name.");
    }
}
