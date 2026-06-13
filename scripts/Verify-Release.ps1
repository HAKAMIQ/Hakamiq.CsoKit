[CmdletBinding()]
param(
    [string]$Version = "0.4.0-beta.2",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path $ArtifactsDir "publish\$Runtime"
$ReleaseDir = Join-Path $ArtifactsDir "release"
$ExePath = Join-Path $PublishDir "hakamiq-cso.exe"
$ManifestPath = Join-Path $PublishDir "SHA256SUMS.txt"
$ZipPath = Join-Path $ReleaseDir "hakamiq-csokit-$Version-$Runtime.zip"

Write-Host "Hakamiq CsoKit Release Verifier"
Write-Host "Version: $Version"
Write-Host "Runtime: $Runtime"
Write-Host ""

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory was not found: $PublishDir"
}

if (-not (Test-Path $ExePath)) {
    throw "Executable was not found: $ExePath"
}

if (-not (Test-Path $ManifestPath)) {
    throw "SHA256 manifest was not found: $ManifestPath"
}

if (-not (Test-Path $ZipPath)) {
    throw "Release ZIP was not found: $ZipPath"
}

$blocked = @(".git", ".github", ".vs", "bin", "obj", "TestResults")

foreach ($name in $blocked) {
    $found = Get-ChildItem $PublishDir -Force -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $name } |
        Select-Object -First 1

    if ($found) {
        throw "Blocked development artifact found in publish output: $($found.FullName)"
    }
}

$versionText = ((& $ExePath --version) | Out-String).Trim()

if ($LASTEXITCODE -ne 0) {
    throw "Version command failed."
}

if ($versionText -notmatch [regex]::Escape($Version)) {
    throw "Version mismatch. Output: $versionText"
}

$helpText = ((& $ExePath --help) | Out-String).Trim()

if ($LASTEXITCODE -ne 0) {
    throw "Help command failed."
}

foreach ($required in @("info", "verify", "decompress", "--json", "--quiet")) {
    if ($helpText -notmatch [regex]::Escape($required)) {
        throw "Help output does not contain required text: $required"
    }
}

$manifestLines = Get-Content $ManifestPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($manifestLines.Count -lt 3) {
    throw "SHA256 manifest looks incomplete."
}

Write-Host "[PASS] Release verification completed"
Write-Host "Executable: $ExePath"
Write-Host "ZIP:        $ZipPath"

