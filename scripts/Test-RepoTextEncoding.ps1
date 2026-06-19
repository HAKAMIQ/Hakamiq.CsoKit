[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$extensions = @('.cs', '.cpp', '.h', '.hpp', '.cmake', '.ps1', '.md')
$failed = New-Object System.Collections.Generic.List[string]

Get-ChildItem -LiteralPath $repoRoot -File -Recurse |
    Where-Object {
        $_.FullName -notmatch '\\(\.git|bin|obj|artifacts|TestResults)\\' -and
        (($extensions -contains $_.Extension.ToLowerInvariant()) -or $_.Name -eq 'CMakeLists.txt')
    } |
    ForEach-Object {
        $bytes = [IO.File]::ReadAllBytes($_.FullName)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            $failed.Add($_.FullName)
        }
    }

if ($failed.Count -gt 0) {
    Write-Error "UTF-8 BOM found in $($failed.Count) source text file(s):`n$($failed -join [Environment]::NewLine)"
    exit 1
}

Write-Host 'Repo text encoding gate PASS.'
exit 0
