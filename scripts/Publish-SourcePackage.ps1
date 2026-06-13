[CmdletBinding()]
param(
    [string]$Version = "0.4.0-beta.1"
)

$ErrorActionPreference = "Stop"

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
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$SourceDir = Join-Path $ArtifactsDir "source"
$ZipPath = Join-Path $SourceDir "hakamiq-csokit-$Version-source.zip"

$blockedTopLevel = @(".git", ".vs", "bin", "obj", "artifacts", "TestResults")
$blockedNested = @("bin", "obj", "TestResults")

Write-Host "Hakamiq CsoKit Source Package Publisher"
Write-Host "Version: $Version"
Write-Host "Repo:    $RepoRoot"
Write-Host ""

Remove-Item $SourceDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $SourceDir | Out-Null

$items = Get-ChildItem $RepoRoot -Force -Recurse | Where-Object {
    $relative = Get-RelativePathCompat -BasePath $RepoRoot -FullPath $_.FullName
    $parts = $relative -split '/'
    $include = -not $_.PSIsContainer

    if ($parts.Count -eq 0) {
        $include = $false
    }
    elseif ($blockedTopLevel -contains $parts[0]) {
        $include = $false
    }
    else {
        foreach ($part in $parts) {
            if ($blockedNested -contains $part) {
                $include = $false
                break
            }
        }
    }

    $include
}

$manifestPath = Join-Path $SourceDir "SOURCE_FILES.txt"
$items |
    Sort-Object FullName |
    ForEach-Object { Get-RelativePathCompat -BasePath $RepoRoot -FullPath $_.FullName } |
    Set-Content $manifestPath -Encoding UTF8

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

$stagingDir = Join-Path $SourceDir "staging"
New-Item -ItemType Directory -Force $stagingDir | Out-Null

foreach ($item in $items) {
    $relative = Get-RelativePathCompat -BasePath $RepoRoot -FullPath $item.FullName
    $destination = Join-Path $stagingDir $relative
    $destinationDir = Split-Path $destination -Parent
    New-Item -ItemType Directory -Force $destinationDir | Out-Null
    Copy-Item $item.FullName $destination -Force
}

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $ZipPath -Force
Remove-Item $stagingDir -Recurse -Force

Write-Host "[PASS] Source package created"
Write-Host "ZipPath: $ZipPath"
