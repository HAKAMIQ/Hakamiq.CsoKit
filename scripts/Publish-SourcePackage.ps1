[CmdletBinding()]
param(
    [string]$Version = "0.6.0"
)

$ErrorActionPreference = "Stop"

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $itemFullPath = [System.IO.Path]::GetFullPath($FullPath)

    $baseUri = [System.Uri]::new($baseFullPath)
    $itemUri = [System.Uri]::new($itemFullPath)
    $relativeUri = $baseUri.MakeRelativeUri($itemUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())

    return $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Assert-RequiredSourceFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $fullPath = Join-Path $RepoRoot $RelativePath

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required source file is missing: $RelativePath"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactsDir = Join-Path $repoRoot "artifacts"
$sourceDir = Join-Path $artifactsDir "source"
$zipPath = Join-Path $sourceDir "hakamiq-csokit-$Version-source.zip"
$stagingDir = Join-Path $sourceDir "staging"

$blockedTopLevel = @(".git", ".vs", "bin", "obj", "artifacts", "TestResults")
$blockedNested = @("bin", "obj", "TestResults")

$requiredSourceFiles = @(
    "Hakamiq.CsoKit.slnx",
    "src\Hakamiq.Cso.Core\Hakamiq.Cso.Core.csproj",
    "src\Hakamiq.Cso.Cli\Hakamiq.Cso.Cli.csproj",
    "src\Hakamiq.Cso.App\Hakamiq.Cso.App.csproj",
    "tests\Hakamiq.Cso.Tests\Hakamiq.Cso.Tests.csproj",
    "native\Hakamiq.Cso.Native\CMakeLists.txt",
    "native\Hakamiq.Cso.Native\src\hakamiq_cso_native.cpp",
    "native\Hakamiq.Cso.Native\include\hakamiq_cso_native.h",
    "native\third_party\zopfli\src\zopfli\zopfli_lib.c"
)

Write-Host "Hakamiq CsoKit Source Package Publisher"
Write-Host "Version: $Version"
Write-Host "Repo:    $repoRoot"
Write-Host ""

foreach ($relativePath in $requiredSourceFiles) {
    Assert-RequiredSourceFile -RepoRoot $repoRoot -RelativePath $relativePath
}

Remove-Item $sourceDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stagingDir | Out-Null

$items = Get-ChildItem $repoRoot -Force -Recurse -File | Where-Object {
    $relative = Get-RelativePathCompat -BasePath $repoRoot -FullPath $_.FullName
    $parts = $relative -split '[\\/]'

    if ($parts.Count -eq 0) {
        return $false
    }

    if ($blockedTopLevel -contains $parts[0]) {
        return $false
    }

    foreach ($part in $parts) {
        if ($blockedNested -contains $part) {
            return $false
        }
    }

    return $true
}

$relativeItems = @(
    $items |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePathCompat -BasePath $repoRoot -FullPath $_.FullName }
)

foreach ($relative in $relativeItems) {
    $sourcePath = Join-Path $repoRoot $relative
    $destinationPath = Join-Path $stagingDir $relative
    $destinationDir = Split-Path $destinationPath -Parent

    New-Item -ItemType Directory -Force $destinationDir | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
}

$manifestPath = Join-Path $stagingDir "SOURCE_FILES.txt"
$relativeItems |
    ForEach-Object { $_.Replace([System.IO.Path]::DirectorySeparatorChar, '/') } |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $stagingDir -Recurse -Force

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Source package was not produced: $zipPath"
}

Write-Host "[PASS] Source package created"
Write-Host "ZipPath: $zipPath"
