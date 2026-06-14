[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

function Find-VsDevCmd {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $vswherePath = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"

    if (Test-Path $vswherePath) {
        $installationPath = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1

        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installationPath)) {
            $candidate = Join-Path $installationPath "Common7\Tools\VsDevCmd.bat"

            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\2026\Enterprise\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2026\Professional\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2026\Community\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files (x86)\Microsoft Visual Studio\2026\BuildTools\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Visual Studio Developer Command Prompt was not found. Install Visual Studio with C++ build tools."
}

if ($Configuration -ne "Debug" -and $Configuration -ne "Release") {
    throw "Unsupported configuration: $Configuration"
}

if ($Platform -ne "x64") {
    throw "Unsupported platform: $Platform"
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$NativeRoot = Join-Path $RepoRoot "native\Hakamiq.Cso.Native"
$BuildDir = Join-Path $RepoRoot "artifacts\native-build\win-x64"

Write-Host "Hakamiq CsoKit Native Builder"
Write-Host "Configuration: $Configuration"
Write-Host "Platform:      $Platform"
Write-Host ""

if (-not (Test-Path $NativeRoot)) {
    throw "Native project directory was not found: $NativeRoot"
}

$cmakeBin = "C:\Program Files\CMake\bin"
if (Test-Path $cmakeBin) {
    $env:Path = "$cmakeBin;$env:Path"
}

$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
if (-not $cmakeCommand) {
    throw "cmake.exe was not found on PATH."
}

$VsDevCmd = Find-VsDevCmd

Write-Host "CMake:    $($cmakeCommand.Source)"
Write-Host "VsDevCmd: $VsDevCmd"
Write-Host ""

$cmdLine = '"' + $VsDevCmd + '" -arch=amd64 -host_arch=amd64' +
    ' && cmake -S "' + $NativeRoot + '" -B "' + $BuildDir + '" -A x64' +
    ' && cmake --build "' + $BuildDir + '" --config ' + $Configuration

cmd.exe /d /s /c $cmdLine

if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}

$DllPath = Join-Path $BuildDir "$Configuration\Hakamiq.Cso.Native.dll"

if (-not (Test-Path $DllPath)) {
    throw "Native DLL was not produced: $DllPath"
}

Write-Host ""
Write-Host "[PASS] Native DLL built"
Write-Host "DLL: $DllPath"
