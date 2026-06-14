param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$nativeRoot = Join-Path $repoRoot "native\Hakamiq.Cso.Native"
$buildDir = Join-Path $repoRoot "artifacts\native-build\win-x64"

Write-Host "Hakamiq CsoKit Native Builder"
Write-Host "Configuration: $Configuration"
Write-Host "Platform:      $Platform"
Write-Host ""

$env:Path = "C:\Program Files\CMake\bin;$env:Path"

cmd /c "`"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat`" -arch=amd64 -host_arch=amd64 && cmake -S `"$nativeRoot`" -B `"$buildDir`" -A x64 && cmake --build `"$buildDir`" --config $Configuration"

$dllPath = Join-Path $buildDir "$Configuration\Hakamiq.Cso.Native.dll"

if (-not (Test-Path $dllPath)) {
    throw "Native DLL was not produced: $dllPath"
}

Write-Host ""
Write-Host "[PASS] Native DLL built"
Write-Host "DLL: $dllPath"
