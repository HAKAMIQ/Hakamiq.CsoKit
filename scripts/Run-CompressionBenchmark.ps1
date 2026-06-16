[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$InputIso,

    [string]$Runtime = "win-x64",
    [string]$OutputDir,
    [string]$MaxCsoExe,
    [string]$MaxCsoArguments = "--format=cso1 --quiet `"{input}`" -o `"{output}`"",
    [string]$CisoExe,
    [string]$CisoArguments = "9 `"{input}`" `"{output}`"",
    [switch]$IncludeManagedComparison,
    [switch]$SkipZopfli,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"

function Convert-ToSafeFileName {
    param([string]$Value)

    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $chars = $Value.ToCharArray() | ForEach-Object {
        if ($invalid -contains $_) { '_' } else { $_ }
    }

    return (-join $chars)
}

function Invoke-External {
    param(
        [string]$StepName,
        [scriptblock]$Command
    )

    $elapsed = Measure-Command { & $Command }
    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }

    return [math]::Round($elapsed.TotalSeconds, 3)
}

function Get-HashText {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Invoke-HakamiqBenchmarkProfile {
    param(
        [string]$ToolName,
        [string]$RuntimeBackend,
        [string]$Profile,
        [string]$ResultProfile,
        [bool]$DisableNative,
        [string[]]$ExtraCompressArgs = @(),
        [string]$ExePath,
        [System.IO.FileInfo]$IsoItem,
        [string]$IsoHash,
        [string]$WorkDir,
        [string]$SafeBaseName,
        [bool]$KeepArtifacts,
        [System.Collections.Generic.List[object]]$Rows
    )

    $rowProfile = if ([string]::IsNullOrWhiteSpace($ResultProfile)) { $Profile } else { $ResultProfile }
    $suffix = if ($DisableNative) { "managed-runtime" } else { "native-runtime" }
    $csoPath = Join-Path $WorkDir "$SafeBaseName-$ToolName-$rowProfile-$suffix.cso"
    $restoredPath = Join-Path $WorkDir "$SafeBaseName-$ToolName-$rowProfile-$suffix-restored.iso"
    $oldDisableNative = [Environment]::GetEnvironmentVariable("HAKAMIQ_CSO_DISABLE_NATIVE")

    try {
        if ($DisableNative) {
            $env:HAKAMIQ_CSO_DISABLE_NATIVE = "1"
        }
        else {
            Remove-Item Env:\HAKAMIQ_CSO_DISABLE_NATIVE -ErrorAction SilentlyContinue
        }

        $compressSeconds = Invoke-External "$ToolName compress $rowProfile" {
            & $ExePath compress $IsoItem.FullName -o $csoPath --profile $Profile --force --quiet @ExtraCompressArgs
        }

        $verifySeconds = Invoke-External "$ToolName deep verify $rowProfile" {
            & $ExePath verify $csoPath --deep --sha256
        }

        $decompressSeconds = Invoke-External "$ToolName decompress $rowProfile" {
            & $ExePath decompress $csoPath -o $restoredPath --force --quiet
        }
    }
    finally {
        if ($null -eq $oldDisableNative) {
            Remove-Item Env:\HAKAMIQ_CSO_DISABLE_NATIVE -ErrorAction SilentlyContinue
        }
        else {
            $env:HAKAMIQ_CSO_DISABLE_NATIVE = $oldDisableNative
        }
    }

    $restoredHash = Get-HashText -Path $restoredPath
    $status = if ($restoredHash -eq $IsoHash) { "PASS" } else { "FAIL" }
    $csoBytes = (Get-Item -LiteralPath $csoPath).Length

    $Rows.Add([pscustomobject]@{
        Tool = $ToolName
        Backend = $RuntimeBackend
        Profile = $rowProfile
        Input = $IsoItem.FullName
        InputBytes = $IsoItem.Length
        CsoBytes = $csoBytes
        Ratio = [math]::Round(($csoBytes / [double]$IsoItem.Length), 6)
        CompressSeconds = $compressSeconds
        VerifySeconds = $verifySeconds
        DecompressSeconds = $decompressSeconds
        Roundtrip = $status
    })

    if (-not $KeepArtifacts) {
        Remove-Item -LiteralPath $csoPath, $restoredPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-NativeInfo {
    param(
        [string]$Text,
        [string]$ExpectedBackend,
        [string]$ExpectedAvailability,
        [string]$FailureMessage
    )

    if ($Text -notmatch "Backend:\s+$ExpectedBackend" -or $Text -notmatch "Native available:\s+$ExpectedAvailability") {
        Write-Host $Text
        throw $FailureMessage
    }
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$PublishDir = Join-Path (Join-Path $RepoRoot "artifacts\publish") $Runtime
$ExePath = Join-Path $PublishDir "hakamiq-cso.exe"

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Published executable was not found. Run scripts\Run-FinalReleaseGate.ps1 first. Missing: $ExePath"
}

$nativeInfo = ((& $ExePath native-info) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "native-info failed."
}
Assert-NativeInfo -Text $nativeInfo -ExpectedBackend "native" -ExpectedAvailability "True" -FailureMessage "Benchmark requires the final published native runtime backend."

$managedInfo = ""
if ($IncludeManagedComparison) {
    $oldDisableNative = [Environment]::GetEnvironmentVariable("HAKAMIQ_CSO_DISABLE_NATIVE")

    try {
        $env:HAKAMIQ_CSO_DISABLE_NATIVE = "1"
        $managedInfo = ((& $ExePath native-info) | Out-String).Trim()
    }
    finally {
        if ($null -eq $oldDisableNative) {
            Remove-Item Env:\HAKAMIQ_CSO_DISABLE_NATIVE -ErrorAction SilentlyContinue
        }
        else {
            $env:HAKAMIQ_CSO_DISABLE_NATIVE = $oldDisableNative
        }
    }

    Assert-NativeInfo -Text $managedInfo -ExpectedBackend "managed" -ExpectedAvailability "False" -FailureMessage "Managed comparison did not disable the native runtime backend."
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $FinalOutputDir = Join-Path (Join-Path $RepoRoot "artifacts\benchmarks") $stamp
}
else {
    $FinalOutputDir = $OutputDir
}

$FinalOutputDir = [System.IO.Path]::GetFullPath($FinalOutputDir)
$ParentOutputDir = Split-Path -Parent $FinalOutputDir
$LeafOutputDir = Split-Path -Leaf $FinalOutputDir
$TempOutputDir = Join-Path $ParentOutputDir (".$LeafOutputDir.incomplete-$PID")
$WorkDir = Join-Path $TempOutputDir "work"
$completed = $false

if (Test-Path -LiteralPath $FinalOutputDir) {
    throw "Benchmark output directory already exists: $FinalOutputDir"
}

Remove-Item -LiteralPath $TempOutputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

try {
    $rows = New-Object System.Collections.Generic.List[object]
    $profiles = @("game-safe", "fast", "smallest")

    foreach ($iso in $InputIso) {
        if (-not (Test-Path -LiteralPath $iso)) {
            throw "Input ISO was not found: $iso"
        }

        $isoItem = Get-Item -LiteralPath $iso
        if ($isoItem.PSIsContainer) {
            throw "Input ISO path is a directory: $($isoItem.FullName)"
        }

        $isoHash = Get-HashText -Path $isoItem.FullName
        $safeBaseName = Convert-ToSafeFileName -Value $isoItem.BaseName

        foreach ($profile in $profiles) {
            Invoke-HakamiqBenchmarkProfile `
                -ToolName "hakamiq" `
                -RuntimeBackend "native-runtime" `
                -Profile $profile `
                -ResultProfile $profile `
                -DisableNative $false `
                -ExtraCompressArgs @() `
                -ExePath $ExePath `
                -IsoItem $isoItem `
                -IsoHash $isoHash `
                -WorkDir $WorkDir `
                -SafeBaseName $safeBaseName `
                -KeepArtifacts $KeepArtifacts.IsPresent `
                -Rows $rows
        }

        if (-not $SkipZopfli) {
            Invoke-HakamiqBenchmarkProfile `
                -ToolName "hakamiq" `
                -RuntimeBackend "native-runtime" `
                -Profile "smallest" `
                -ResultProfile "smallest-zopfli" `
                -DisableNative $false `
                -ExtraCompressArgs @("--zopfli") `
                -ExePath $ExePath `
                -IsoItem $isoItem `
                -IsoHash $isoHash `
                -WorkDir $WorkDir `
                -SafeBaseName $safeBaseName `
                -KeepArtifacts $KeepArtifacts.IsPresent `
                -Rows $rows
        }

        if ($IncludeManagedComparison) {
            Invoke-HakamiqBenchmarkProfile `
                -ToolName "hakamiq-managed" `
                -RuntimeBackend "managed-runtime" `
                -Profile "fast" `
                -ResultProfile "fast" `
                -DisableNative $true `
                -ExtraCompressArgs @() `
                -ExePath $ExePath `
                -IsoItem $isoItem `
                -IsoHash $isoHash `
                -WorkDir $WorkDir `
                -SafeBaseName $safeBaseName `
                -KeepArtifacts $KeepArtifacts.IsPresent `
                -Rows $rows
        }

        if (-not [string]::IsNullOrWhiteSpace($MaxCsoExe)) {
            if (-not (Test-Path -LiteralPath $MaxCsoExe)) {
                throw "maxcso executable was not found: $MaxCsoExe"
            }

            $maxOutput = Join-Path $WorkDir "$safeBaseName-maxcso.cso"
            $expandedArgs = $MaxCsoArguments.Replace("{input}", $isoItem.FullName).Replace("{output}", $maxOutput)

            $maxSeconds = Invoke-External "maxcso compress" {
                $command = "& `"$MaxCsoExe`" $expandedArgs"
                Invoke-Expression $command
            }

            if (-not (Test-Path -LiteralPath $maxOutput)) {
                throw "maxcso did not produce the expected output: $maxOutput"
            }

            $maxVerifySeconds = Invoke-External "maxcso output deep verify" {
                & $ExePath verify $maxOutput --deep --sha256
            }

            $maxRestored = Join-Path $WorkDir "$safeBaseName-maxcso-restored.iso"
            $maxDecompressSeconds = Invoke-External "maxcso output decompress" {
                & $ExePath decompress $maxOutput -o $maxRestored --force --quiet
            }

            $maxRestoredHash = Get-HashText -Path $maxRestored
            $maxStatus = if ($maxRestoredHash -eq $isoHash) { "PASS" } else { "FAIL" }
            $maxBytes = (Get-Item -LiteralPath $maxOutput).Length

            $rows.Add([pscustomobject]@{
                Tool = "maxcso"
                Backend = "external"
                Profile = "default"
                Input = $isoItem.FullName
                InputBytes = $isoItem.Length
                CsoBytes = $maxBytes
                Ratio = [math]::Round(($maxBytes / [double]$isoItem.Length), 6)
                CompressSeconds = $maxSeconds
                VerifySeconds = $maxVerifySeconds
                DecompressSeconds = $maxDecompressSeconds
                Roundtrip = $maxStatus
            })

            if (-not $KeepArtifacts) {
                Remove-Item -LiteralPath $maxOutput, $maxRestored -Force -ErrorAction SilentlyContinue
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($CisoExe)) {
            if (-not (Test-Path -LiteralPath $CisoExe)) {
                throw "ciso executable was not found: $CisoExe"
            }

            $cisoOutput = Join-Path $WorkDir "$safeBaseName-ciso-level9.cso"
            $expandedArgs = $CisoArguments.Replace("{input}", $isoItem.FullName).Replace("{output}", $cisoOutput)

            $cisoSeconds = Invoke-External "ciso level 9 compress" {
                $command = "& `"$CisoExe`" $expandedArgs"
                Invoke-Expression $command
            }

            if (-not (Test-Path -LiteralPath $cisoOutput)) {
                throw "ciso did not produce the expected output: $cisoOutput"
            }

            $cisoVerifySeconds = Invoke-External "ciso output deep verify" {
                & $ExePath verify $cisoOutput --deep --sha256
            }

            $cisoRestored = Join-Path $WorkDir "$safeBaseName-ciso-level9-restored.iso"
            $cisoDecompressSeconds = Invoke-External "ciso output decompress" {
                & $ExePath decompress $cisoOutput -o $cisoRestored --force --quiet
            }

            $cisoRestoredHash = Get-HashText -Path $cisoRestored
            $cisoStatus = if ($cisoRestoredHash -eq $isoHash) { "PASS" } else { "FAIL" }
            $cisoBytes = (Get-Item -LiteralPath $cisoOutput).Length

            $rows.Add([pscustomobject]@{
                Tool = "ciso"
                Backend = "external"
                Profile = "level-9"
                Input = $isoItem.FullName
                InputBytes = $isoItem.Length
                CsoBytes = $cisoBytes
                Ratio = [math]::Round(($cisoBytes / [double]$isoItem.Length), 6)
                CompressSeconds = $cisoSeconds
                VerifySeconds = $cisoVerifySeconds
                DecompressSeconds = $cisoDecompressSeconds
                Roundtrip = $cisoStatus
            })

            if (-not $KeepArtifacts) {
                Remove-Item -LiteralPath $cisoOutput, $cisoRestored -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($rows.Count -eq 0) {
        throw "Benchmark produced no rows."
    }

    $failedRows = @($rows | Where-Object { $_.Roundtrip -ne "PASS" })
    if ($failedRows.Count -gt 0) {
        $failedRows | Format-Table -AutoSize
        throw "Benchmark contains failed roundtrip rows."
    }

    if ($IncludeManagedComparison) {
        $managedRows = @($rows | Where-Object { $_.Tool -eq "hakamiq-managed" })
        if ($managedRows.Count -eq 0) {
            throw "Benchmark requested managed comparison but produced no managed rows."
        }
    }

    $CsvPath = Join-Path $TempOutputDir "compression-benchmark.csv"
    $MarkdownPath = Join-Path $TempOutputDir "compression-benchmark.md"

    $rows | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding UTF8

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Hakamiq CsoKit Compression Benchmark")
    $lines.Add("")
    $lines.Add("Native runtime backend:")
    $lines.Add('```text')
    $lines.Add($nativeInfo)
    $lines.Add('```')
    if ($IncludeManagedComparison) {
        $lines.Add("")
        $lines.Add("Managed fallback backend:")
        $lines.Add('```text')
        $lines.Add($managedInfo)
        $lines.Add('```')
    }
    $lines.Add("")
    $lines.Add("Note: native-runtime means the final executable can load the native backend. zlib and libdeflate can participate as safe raw-Deflate candidates; Zopfli remains explicit via --zopfli.")
    $lines.Add("")
    $lines.Add("| Tool | Backend | Profile | InputBytes | CsoBytes | Ratio | CompressSeconds | VerifySeconds | DecompressSeconds | Roundtrip |")
    $lines.Add("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|")
    foreach ($row in $rows) {
        $lines.Add("| $($row.Tool) | $($row.Backend) | $($row.Profile) | $($row.InputBytes) | $($row.CsoBytes) | $($row.Ratio) | $($row.CompressSeconds) | $($row.VerifySeconds) | $($row.DecompressSeconds) | $($row.Roundtrip) |")
    }

    Set-Content -LiteralPath $MarkdownPath -Value $lines -Encoding UTF8

    if (-not (Test-Path -LiteralPath $CsvPath)) {
        throw "Benchmark CSV was not created: $CsvPath"
    }
    if (-not (Test-Path -LiteralPath $MarkdownPath)) {
        throw "Benchmark markdown was not created: $MarkdownPath"
    }

    if (-not $KeepArtifacts) {
        Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Move-Item -LiteralPath $TempOutputDir -Destination $FinalOutputDir
    $completed = $true

    $FinalCsvPath = Join-Path $FinalOutputDir "compression-benchmark.csv"
    $FinalMarkdownPath = Join-Path $FinalOutputDir "compression-benchmark.md"

    Write-Host "Compression benchmark result"
    Write-Host "----------------------------"
    Write-Host "CSV:      $FinalCsvPath"
    Write-Host "Markdown: $FinalMarkdownPath"
    $rows | Format-Table -AutoSize
}
finally {
    if (-not $completed) {
        Remove-Item -LiteralPath $TempOutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
