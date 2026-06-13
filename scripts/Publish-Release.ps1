[CmdletBinding()]
param(
    [string]$Version = "0.4.0-beta.2",
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

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$SolutionFile = Get-ChildItem -Path $RepoRoot -File |
    Where-Object { $_.Extension -in ".sln", ".slnx" } |
    Select-Object -First 1

if (-not $SolutionFile) {
    throw "No .sln or .slnx file was found in repo root: $RepoRoot"
}

$Solution = $SolutionFile.FullName
$CliProject = Join-Path $RepoRoot "src\Hakamiq.Cso.Cli\Hakamiq.Cso.Cli.csproj"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path $ArtifactsDir "publish\$Runtime"
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

Write-Host "[1/6] Restore"
Invoke-Checked "Restore" {
    dotnet restore $Solution -r $Runtime -p:NuGetAudit=false
}

Write-Host "[2/6] Build Release"
Invoke-Checked "Build Release" {
    dotnet build $Solution -c Release --no-restore -p:NuGetAudit=false -p:Version=$Version
}

Write-Host "[3/6] Test Release"
Invoke-Checked "Test Release" {
    dotnet test $Solution -c Release --no-build
}

Write-Host "[4/6] Publish single-file CLI"
Invoke-Checked "Publish CLI" {
    dotnet publish $CliProject `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -o $PublishDir `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -p:NuGetAudit=false
}

$ExePath = Join-Path $PublishDir "hakamiq-cso.exe"

if (-not (Test-Path $ExePath)) {
    throw "Published executable was not found: $ExePath"
}

Copy-Item (Join-Path $RepoRoot "README.md") (Join-Path $PublishDir "README.md") -Force
Copy-Item (Join-Path $RepoRoot "LICENSE.txt") (Join-Path $PublishDir "LICENSE.txt") -Force

Write-Host "[5/6] Smoke test"

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

foreach ($required in @("info", "verify", "decompress", "--json", "--quiet")) {
    if ($helpText -notmatch [regex]::Escape($required)) {
        throw "Help output does not contain required text: $required"
    }
}

Write-Host "[6/6] SHA256 manifest and ZIP"

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

