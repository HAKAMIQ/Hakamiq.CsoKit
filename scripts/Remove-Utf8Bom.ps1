[CmdletBinding()]
param([switch]$Apply)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$extensions = @('.cs', '.cpp', '.h', '.hpp', '.cmake', '.ps1', '.md')
$files = Get-ChildItem -LiteralPath $repoRoot -File -Recurse |
    Where-Object {
        $_.FullName -notmatch '\\(\.git|bin|obj|artifacts|TestResults)\\' -and
        (($extensions -contains $_.Extension.ToLowerInvariant()) -or $_.Name -eq 'CMakeLists.txt')
    }

$changed = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $changed.Add($file.FullName)
        if ($Apply) {
            $withoutBom = New-Object byte[] ($bytes.Length - 3)
            [Array]::Copy($bytes, 3, $withoutBom, 0, $withoutBom.Length)
            [IO.File]::WriteAllBytes($file.FullName, $withoutBom)
        }
    }
}

if ($changed.Count -eq 0) {
    Write-Host 'UTF-8 BOM scan PASS: no BOM found in source text files.'
    exit 0
}

if ($Apply) {
    Write-Host "Removed UTF-8 BOM from $($changed.Count) file(s)."
}
else {
    Write-Host "UTF-8 BOM found in $($changed.Count) file(s). Re-run with -Apply to remove."
}

$changed | ForEach-Object { Write-Host "  $_" }
exit 0
