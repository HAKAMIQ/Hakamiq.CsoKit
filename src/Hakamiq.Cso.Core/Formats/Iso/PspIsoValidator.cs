namespace Hakamiq.Cso.Core.Formats.Iso;

public sealed class PspIsoValidator
{
    private static readonly string[] RequiredPaths =
    [
        "UMD_DATA.BIN",
        "PSP_GAME/PARAM.SFO",
        "PSP_GAME/SYSDIR/EBOOT.BIN",
    ];

    public PspIsoValidationResult Validate(string inputPath, bool allowPadding = false)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InvalidInputPath", "Input path is empty.")]);
        }

        if (!File.Exists(inputPath))
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InputNotFound", "Input file was not found.")]);
        }

        try
        {
            FileInfo inputInfo = new(inputPath);
            IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(inputInfo.Length, allowPadding);
            List<PspIsoValidationIssue> issues = [];
            List<string> warnings = [];

            if (!alignment.Success)
            {
                issues.Add(new PspIsoValidationIssue(
                    alignment.ErrorCode ?? "IsoAlignmentFailed",
                    alignment.ErrorMessage ?? "ISO alignment validation failed."));
            }
            else if (alignment.PaddingBytes > 0)
            {
                warnings.Add($"ISO requires {alignment.PaddingBytes:N0} bytes of explicit zero padding to align to 2048 bytes.");
            }

            using FileStream input = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);

            Iso9660Reader iso = new(input);
            bool hasPvd = iso.HasPrimaryVolumeDescriptor();

            if (!hasPvd)
            {
                issues.Add(new PspIsoValidationIssue(
                    "MissingIso9660PrimaryVolumeDescriptor",
                    "ISO9660 primary volume descriptor was not found."));
            }

            bool hasUmdData = false;
            bool hasParamSfo = false;
            bool hasEboot = false;

            if (hasPvd)
            {
                hasUmdData = iso.TryFindPath("UMD_DATA.BIN", out _);
                hasParamSfo = iso.TryFindPath("PSP_GAME/PARAM.SFO", out _);
                hasEboot = iso.TryFindPath("PSP_GAME/SYSDIR/EBOOT.BIN", out _);

                AddMissingPathIssues(issues, hasUmdData, hasParamSfo, hasEboot);
            }
            else
            {
                foreach (string path in RequiredPaths)
                {
                    issues.Add(new PspIsoValidationIssue(
                        ToMissingCode(path),
                        $"Required PSP path was not validated because ISO9660 could not be read.",
                        path));
                }
            }

            return new PspIsoValidationResult(
                Success: issues.Count == 0,
                inputPath,
                inputInfo.Length,
                alignment.PaddingBytes,
                hasPvd,
                hasUmdData,
                hasParamSfo,
                hasEboot,
                issues,
                warnings);
        }
        catch (UnauthorizedAccessException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("IsoReadFailed", ex.Message)]);
        }
        catch (InvalidDataException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("IsoStructureInvalid", ex.Message)]);
        }
    }

    private static void AddMissingPathIssues(
        List<PspIsoValidationIssue> issues,
        bool hasUmdData,
        bool hasParamSfo,
        bool hasEboot)
    {
        if (!hasUmdData)
        {
            issues.Add(new PspIsoValidationIssue(
                "MissingUmdDataBin",
                "Required PSP path UMD_DATA.BIN was not found.",
                "UMD_DATA.BIN"));
        }

        if (!hasParamSfo)
        {
            issues.Add(new PspIsoValidationIssue(
                "MissingParamSfo",
                "Required PSP path PSP_GAME/PARAM.SFO was not found.",
                "PSP_GAME/PARAM.SFO"));
        }

        if (!hasEboot)
        {
            issues.Add(new PspIsoValidationIssue(
                "MissingEbootBin",
                "Required PSP path PSP_GAME/SYSDIR/EBOOT.BIN was not found.",
                "PSP_GAME/SYSDIR/EBOOT.BIN"));
        }
    }

    private static string ToMissingCode(string path)
    {
        return path.ToUpperInvariant() switch
        {
            "UMD_DATA.BIN" => "MissingUmdDataBin",
            "PSP_GAME/PARAM.SFO" => "MissingParamSfo",
            "PSP_GAME/SYSDIR/EBOOT.BIN" => "MissingEbootBin",
            _ => "MissingPspIsoPath",
        };
    }
}
