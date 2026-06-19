[CmdletBinding()]
param(
[string]$Version = "0.6.0",
[string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
param(
[string]$StepName,
[scriptblock]$Command
)

& $Command

if ($LASTEXITCODE -ne 0) {
    throw "$StepName failed with exit code $LASTEXITCODE."
}

}

function Get-RelativePathCompat {
param(
[string]$BasePath,
[string]$FullPath
)

$baseFullPath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$itemFullPath = [System.IO.Path]::GetFullPath($FullPath)

$baseUri = New-Object System.Uri($baseFullPath)
$itemUri = New-Object System.Uri($itemFullPath)

$relativeUri = $baseUri.MakeRelativeUri($itemUri)
$relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())

return $relativePath.Replace('\', '/')

}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$SolutionFile = Get-ChildItem -Path $RepoRoot -File |
Where-Object { $_.Extension -in ".sln", ".slnx" } |
Select-Object -First 1

if (-not $SolutionFile) {
throw "No .sln or .slnx file was found in repo root: $RepoRoot"
}

if ($Runtime -ne "win-x64") {
throw "Native backend packaging currently supports win-x64 only. Runtime requested: $Runtime"
}

$Solution = $SolutionFile.FullName
$CliProject = Join-Path $RepoRoot "src\Hakamiq.Cso.Cli\Hakamiq.Cso.Cli.csproj"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path (Join-Path $ArtifactsDir "publish") $Runtime
$ReleaseDir = Join-Path $ArtifactsDir "release"
$ZipPath = Join-Path $ReleaseDir "hakamiq-csokit-$Version-$Runtime.zip"

Write-Host "Hakamiq CsoKit Release Publisher"
Write-Host "Version:  $Version"
Write-Host "Runtime:  $Runtime"
Write-Host "Solution: $Solution"
Write-Host ""

Remove-Item $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $PublishDir | Out-Null
New-Item -ItemType Directory -Force $ReleaseDir | Out-Null

Write-Host "[1/8] Restore"
Invoke-Checked "Restore" {
$args = @(
"restore",
$Solution,
"-r",
$Runtime,
"-p:NuGetAudit=false"
)

dotnet @args

}

Write-Host "[2/8] Build Release"
Invoke-Checked "Build Release" {
$args = @(
"build",
$Solution,
"-c",
"Release",
"--no-restore",
"-p:NuGetAudit=false",
"-p:Version=$Version"
)

dotnet @args

}

Write-Host "[3/8] Test Release"
Invoke-Checked "Test Release" {
$args = @(
"test",
$Solution,
"-c",
"Release",
"--no-build"
)

dotnet @args

}

Write-Host "[4/8] Publish single-file CLI"
Invoke-Checked "Publish CLI" {
$args = @(
"publish",
$CliProject,
"-c",
"Release",
"-r",
$Runtime,
"--self-contained",
"true",
"-o",
$PublishDir,
"-p:PublishSingleFile=true",
"-p:EnableCompressionInSingleFile=true",
"-p:PublishTrimmed=false",
"-p:DebugType=None",
"-p:DebugSymbols=false",
"-p:Version=$Version",
"-p:NuGetAudit=false"
)

dotnet @args

}

$ExePath = Join-Path $PublishDir "hakamiq-cso.exe"

if (-not (Test-Path $ExePath)) {
throw "Published executable was not found: $ExePath"
}

Write-Host "[5/8] Build and copy native backend"

& "$PSScriptRoot\Build-Native.ps1" -Configuration Release -Platform x64

$NativeDllPath = Join-Path $ArtifactsDir "native-build\win-x64\Release\Hakamiq.Cso.Native.dll"
$PublishNativeDllPath = Join-Path $PublishDir "Hakamiq.Cso.Native.dll"

if (-not (Test-Path $NativeDllPath)) {
throw "Native DLL was not produced: $NativeDllPath"
}

Copy-Item $NativeDllPath $PublishNativeDllPath -Force

if (-not (Test-Path $PublishNativeDllPath)) {
throw "Native DLL was not copied to publish directory: $PublishNativeDllPath"
}

Copy-Item (Join-Path $RepoRoot "README.md") (Join-Path $PublishDir "README.md") -Force
Copy-Item (Join-Path $RepoRoot "LICENSE.txt") (Join-Path $PublishDir "LICENSE.txt") -Force
Copy-Item (Join-Path $RepoRoot "THIRD_PARTY_NOTICES.md") (Join-Path $PublishDir "THIRD_PARTY_NOTICES.md") -Force

$ReleaseNotesPath = Join-Path $RepoRoot "RELEASE_NOTES.md"
if (Test-Path $ReleaseNotesPath) {
    Copy-Item $ReleaseNotesPath (Join-Path $PublishDir "RELEASE_NOTES.md") -Force
}

Write-Host "[6/8] Smoke test"

$versionText = ((& $ExePath --version) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
throw "Version smoke test failed."
}

if ($versionText -notmatch [regex]::Escape($Version)) {
throw "Version output does not contain expected version. Output: $versionText"
}

$helpText = ((& $ExePath --help) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
throw "Help smoke test failed."
}

foreach ($required in @("info", "verify", "repair", "analyze", "detect", "decompress", "compress", "codecs", "native-info", "--json", "--quiet", "--profile", "--threads", "--block", "--zopfli", "--codec-report", "game-safe|compat|fast|smallest|archive-smallest")) {
if ($helpText -notmatch [regex]::Escape($required)) {
throw "Help output does not contain required text: $required"
}
}

Write-Host "[7/8] Native smoke test"

$nativeInfoOutput = ((& $ExePath native-info) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
throw "native-info smoke test failed."
}

if ($nativeInfoOutput -notmatch "Backend:\s+native") {
Write-Host $nativeInfoOutput
throw "native-info did not report native backend."
}

if ($nativeInfoOutput -notmatch "Native available:\s+True") {
Write-Host $nativeInfoOutput
throw "native-info did not report native availability."
}

foreach ($requiredCapability in @(
"Native zlib:\s+available",
"Native libdeflate:\s+available",
"Native Zopfli:\s+available",
"LZ4 decode:\s+available"
)) {
if ($nativeInfoOutput -notmatch $requiredCapability) {
Write-Host $nativeInfoOutput
throw "native-info did not report required codec capability: $requiredCapability"
}
}

Write-Host "[8/8] SHA256 manifest and ZIP"

$ManifestPath = Join-Path $PublishDir "SHA256SUMS.txt"

Get-ChildItem $PublishDir -File -Recurse |
Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
Sort-Object FullName |
ForEach-Object {
$hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$relative = Get-RelativePathCompat -BasePath $PublishDir -FullPath $_.FullName
"$hash  $relative"
} |
Set-Content $ManifestPath -Encoding UTF8

if (Test-Path $ZipPath) {
Remove-Item $ZipPath -Force
}

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "[PASS] Release package created"
Write-Host "PublishDir: $PublishDir"
Write-Host "ZipPath:    $ZipPath"


