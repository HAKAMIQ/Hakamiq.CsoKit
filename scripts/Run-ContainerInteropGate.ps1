[CmdletBinding()]
param(
    [string]$MaxCsoPath = "maxcso",

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

function Resolve-CommandPath {
    param(
        [string]$Command
    )

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $null
    }

    if (Test-Path -LiteralPath $Command -PathType Leaf) {
        return (Resolve-Path -LiteralPath $Command).Path
    }

    $resolved = Get-Command $Command -ErrorAction SilentlyContinue

    if ($resolved) {
        return $resolved.Source
    }

    return $null
}

function Invoke-External {
    param(
        [string]$StepName,
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host "[$StepName]"
    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
}

function Invoke-HakamiqCso {
    param(
        [string]$StepName,
        [string[]]$CommandArguments
    )

    Write-Host ""
    Write-Host "[$StepName]"

    $dotnetArguments = @(
        "run",
        "--project",
        $CliProject,
        "--no-restore",
        "--"
    ) + $CommandArguments

    & dotnet @dotnetArguments

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
}

function Get-Sha256Hex {
    param(
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function New-SmallAlignedIso {
    param(
        [string]$Path
    )

    $bytes = New-Object byte[] 8192

    for ($index = 0; $index -lt $bytes.Length; $index++) {
        $bytes[$index] = [byte](($index * 17 + 31) % 251)
    }

    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$CliProject = Join-Path $RepoRoot "src\Hakamiq.Cso.Cli\Hakamiq.Cso.Cli.csproj"
$MaxCsoExe = Resolve-CommandPath -Command $MaxCsoPath

Write-Host "Hakamiq CsoKit Container Interop Gate"
Write-Host "Repo:   $RepoRoot"
Write-Host "maxcso: $(if ($MaxCsoExe) { $MaxCsoExe } else { 'not found' })"

if (-not $MaxCsoExe) {
    Write-Host "Status: SKIPPED"
    Write-Host "Reason: maxcso was not found. Pass -MaxCsoPath <path-to-maxcso.exe> to enable this gate."
    exit 0
}

if (-not (Test-Path -LiteralPath $CliProject)) {
    throw "CLI project was not found: $CliProject"
}

$WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("HakamiqCsoKitContainerInterop_{0:N}" -f [guid]::NewGuid())
New-Item -ItemType Directory -Path $WorkRoot | Out-Null

$success = $false

try {
    $InputIso = Join-Path $WorkRoot "interop.iso"
    New-SmallAlignedIso -Path $InputIso
    $OriginalHash = Get-Sha256Hex -Path $InputIso

    foreach ($format in @("cso1", "zso", "cso2", "dax")) {
        $extension = switch ($format) {
            "zso" { ".zso" }
            "dax" { ".dax" }
            default { ".cso" }
        }

        $containerPath = Join-Path $WorkRoot ("interop-{0}{1}" -f $format, $extension)
        $normalizedPath = Join-Path $WorkRoot ("interop-{0}.normalized.cso" -f $format)
        $restoredPath = Join-Path $WorkRoot ("interop-{0}.restored.iso" -f $format)

        $maxcsoArgs = @("--format=$format", $InputIso, "-o", $containerPath)

        if ($Quiet) {
            $maxcsoArgs = @("--quiet") + $maxcsoArgs
        }

        Invoke-External -StepName "maxcso $format" -FilePath $MaxCsoExe -Arguments $maxcsoArgs

        if (-not (Test-Path -LiteralPath $containerPath)) {
            throw "maxcso did not create expected artifact: $containerPath"
        }

        Invoke-HakamiqCso -StepName "repair $format to CSO1 game-safe" -CommandArguments @(
            "repair",
            $containerPath,
            "-o",
            $normalizedPath,
            "--force",
            "--deep-verify"
        )

        Invoke-HakamiqCso -StepName "verify normalized $format" -CommandArguments @(
            "verify",
            $normalizedPath,
            "--deep",
            "--sha256"
        )

        Invoke-HakamiqCso -StepName "decompress normalized $format" -CommandArguments @(
            "decompress",
            $normalizedPath,
            "-o",
            $restoredPath,
            "--force"
        )

        $RestoredHash = Get-Sha256Hex -Path $restoredPath

        if (-not [string]::Equals($OriginalHash, $RestoredHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "SHA256 mismatch for $format. Original=$OriginalHash Restored=$RestoredHash"
        }

        Write-Host "[$format] SHA256 OK: $RestoredHash"
    }

    $success = $true
    Write-Host ""
    Write-Host "Status: PASS"
}
finally {
    if ($success -and -not $KeepArtifacts) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Artifacts removed."
    }
    else {
        Write-Host "Artifacts: $WorkRoot"
    }
}
