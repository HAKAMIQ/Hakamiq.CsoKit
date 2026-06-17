[CmdletBinding()]
param(
    [string[]]$InputPath = @(),

    [string]$Root = "",

    [ValidateSet("game-safe", "compat", "fast", "smallest", "archive-smallest")]
    [string[]]$Profiles = @("game-safe"),

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [int]$Threads = [Math]::Max(1, [Environment]::ProcessorCount),

    [string]$OutputDirectory = "",

    [switch]$UseZopfli,

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Text
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Escape-MarkdownCell {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}

function Get-ResolvedInputFiles {
    $extensions = @(".iso", ".cso", ".zso", ".dax", ".cso2")
    $files = New-Object System.Collections.Generic.List[System.IO.FileInfo]

    foreach ($path in $InputPath) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $resolved = Resolve-Path -LiteralPath $path -ErrorAction Stop
        $item = Get-Item -LiteralPath $resolved.ProviderPath -ErrorAction Stop

        if ($item.PSIsContainer) {
            throw "InputPath must point to files. Use -Root for recursive corpus folders: $($item.FullName)"
        }

        $files.Add($item) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($Root)) {
        $resolvedRoot = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).ProviderPath

        if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
            throw "Root is not a directory: $Root"
        }

        Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
            Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
            Sort-Object FullName |
            ForEach-Object { $files.Add($_) | Out-Null }
    }

    return $files |
        Sort-Object FullName -Unique
}

function Initialize-CsoTool {
    $script:RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).ProviderPath
    $solution = Join-Path $script:RepoRoot "Hakamiq.CsoKit.slnx"
    $exe = Join-Path $script:RepoRoot "src/Hakamiq.Cso.Cli/bin/$Configuration/net10.0/hakamiq-cso.exe"
    $dll = Join-Path $script:RepoRoot "src/Hakamiq.Cso.Cli/bin/$Configuration/net10.0/Hakamiq.Cso.Cli.dll"

    if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -and
        -not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        if (-not $Quiet) {
            Write-Host "CLI binary was not found. Building $Configuration first..."
        }

        & dotnet build $solution -c $Configuration -p:NuGetAudit=false

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }

    if (Test-Path -LiteralPath $exe -PathType Leaf) {
        $script:ToolFile = $exe
        $script:ToolPrefix = @()
        return
    }

    if (Test-Path -LiteralPath $dll -PathType Leaf) {
        $script:ToolFile = "dotnet"
        $script:ToolPrefix = @($dll)
        return
    }

    throw "CLI binary was not found after build."
}

function Invoke-ToolProcess {
    param([string[]]$Arguments)

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $script:ToolFile
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    foreach ($arg in @($script:ToolPrefix + $Arguments)) {
        $psi.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process.Start() | Out-Null
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $peakWorkingSet = 0L

    while (-not $process.HasExited) {
        try {
            $process.Refresh()
            $peakWorkingSet = [Math]::Max($peakWorkingSet, [int64]$process.PeakWorkingSet64)
        }
        catch {
        }

        Start-Sleep -Milliseconds 25
    }

    $process.WaitForExit()
    $timer.Stop()

    try {
        $process.Refresh()
        $peakWorkingSet = [Math]::Max($peakWorkingSet, [int64]$process.PeakWorkingSet64)
    }
    catch {
    }

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
    $process.Dispose()

    return [pscustomobject]@{
        ExitCode = $exitCode
        Stdout = $stdout
        Stderr = $stderr
        ElapsedMilliseconds = [Math]::Round($timer.Elapsed.TotalMilliseconds, 3)
        PeakWorkingSetBytes = $peakWorkingSet
    }
}

function Invoke-JsonTool {
    param([string[]]$Arguments)

    $process = Invoke-ToolProcess -Arguments $Arguments

    if ($process.ExitCode -ne 0) {
        $message = $process.Stderr.Trim()

        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = $process.Stdout.Trim()
        }

        throw "hakamiq-cso failed with exit code $($process.ExitCode): $($Arguments -join ' ') $message"
    }

    try {
        $json = $process.Stdout.Trim() | ConvertFrom-Json
    }
    catch {
        throw "hakamiq-cso did not return valid JSON for: $($Arguments -join ' ')"
    }

    return [pscustomobject]@{
        Json = $json
        Process = $process
    }
}

function ConvertTo-StringIntMap {
    param([object]$Value)

    $map = [ordered]@{}

    if ($null -eq $Value) {
        return $map
    }

    foreach ($property in $Value.PSObject.Properties) {
        $map[$property.Name] = [int]$property.Value
    }

    return $map
}

function Get-TopCodec {
    param([System.Collections.IDictionary]$CodecWins)

    if ($CodecWins.Count -eq 0) {
        return $null
    }

    if ($CodecWins.Count -eq 1) {
        return @($CodecWins.Keys)[0]
    }

    return "mixed"
}

function Get-Sha256Hex {
    param([string]$Path)

    return ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash).ToLowerInvariant()
}

function Get-ReportMarkdown {
    param([object]$Report)

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine("# Hakamiq.CsoKit Benchmark Truth Report")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- Generated UTC: $($Report.generatedAtUtc)")
    [void]$builder.AppendLine("- Repo: $($Report.repo)")
    [void]$builder.AppendLine("- Profiles: $($Report.profiles -join ', ')")
    [void]$builder.AppendLine("- External comparison dependency: none")
    [void]$builder.AppendLine("- Corpus stored in Git: false")
    [void]$builder.AppendLine("- Output target: CSO1 game-safe normalization/compression")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("| Input | Profile | Format | Input bytes | Logical bytes | Output bytes | Saved % | Encode ms | Decode ms | Verify ms | Peak bytes | SHA256 match | Deep verify | Selected codec | Status |")
    [void]$builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|")

    foreach ($case in $Report.cases) {
        $status = if ($case.success) { "PASS" } else { "FAIL" }
        [void]$builder.AppendLine(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14} |" -f
            (Escape-MarkdownCell $case.input),
            (Escape-MarkdownCell $case.profile),
            (Escape-MarkdownCell $case.format),
            $case.inputSizeBytes,
            $case.logicalSizeBytes,
            $case.outputSizeBytes,
            $case.savedPercent,
            $case.encodeMilliseconds,
            $case.decodeMilliseconds,
            $case.verifyMilliseconds,
            $case.peakWorkingSetBytes,
            $case.sha256Match,
            $case.deepVerifySuccess,
            (Escape-MarkdownCell $case.selectedCodec),
            $status))
    }

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Limits")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- This report proves only the supplied local files and profiles.")
    [void]$builder.AppendLine("- CSO1 remains the safe output target.")
    [void]$builder.AppendLine("- Experimental or unavailable codecs are reported as candidates only when the CLI exposes them for the selected profile and flags.")
    [void]$builder.AppendLine("- A failed case is evidence to investigate; the script does not produce a trusted output for failed cases.")

    return $builder.ToString()
}

Initialize-CsoTool

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("HakamiqCsoKit_BenchmarkTruth_" + [Guid]::NewGuid().ToString("N"))
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$artifactRoot = if ($KeepArtifacts) {
    Join-Path $OutputDirectory "artifacts"
}
else {
    Join-Path ([System.IO.Path]::GetTempPath()) ("HakamiqCsoKit_BenchmarkArtifacts_" + [Guid]::NewGuid().ToString("N"))
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$files = @(Get-ResolvedInputFiles)
$results = New-Object System.Collections.Generic.List[object]
$overallSuccess = $true

foreach ($file in $files) {
    foreach ($profile in $Profiles) {
        $caseId = [Guid]::NewGuid().ToString("N")
        $caseDir = Join-Path $artifactRoot $caseId
        New-Item -ItemType Directory -Path $caseDir -Force | Out-Null

        $warnings = New-Object System.Collections.Generic.List[string]
        $failures = New-Object System.Collections.Generic.List[string]
        $caseSuccess = $false
        $format = $null
        $logicalSizeBytes = 0L
        $outputSizeBytes = 0L
        $savedPercent = 0.0
        $encodeMilliseconds = 0.0
        $decodeMilliseconds = 0.0
        $verifyMilliseconds = 0.0
        $inputVerifyMilliseconds = 0.0
        $peakWorkingSetBytes = 0L
        $sha256Match = $false
        $deepVerifySuccess = $false
        $sourceSha256 = $null
        $outputSha256 = $null
        $codecCandidates = @()
        $selectedCodecWins = [ordered]@{}
        $rejectedReasons = [ordered]@{}
        $selectedCodec = $null

        try {
            $detect = Invoke-JsonTool -Arguments @("detect", $file.FullName, "--json")
            $format = [string]$detect.Json.format

            foreach ($warning in @($detect.Json.warnings)) {
                if ($null -ne $warning) {
                    $warnings.Add([string]$warning) | Out-Null
                }
            }

            if ($null -ne $detect.Json.uncompressedSize) {
                $logicalSizeBytes = [int64]$detect.Json.uncompressedSize
            }
            else {
                $logicalSizeBytes = [int64]$file.Length
            }

            if ($format -eq "RawIso") {
                $sourceSha256 = Get-Sha256Hex -Path $file.FullName
            }
            elseif ($format -in @("Cso1", "Cso2", "Zso", "Dax")) {
                $inputVerify = Invoke-JsonTool -Arguments @("verify", $file.FullName, "--deep", "--sha256", "--json")
                $inputVerifyMilliseconds = $inputVerify.Process.ElapsedMilliseconds
                $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$inputVerify.Process.PeakWorkingSetBytes)

                if (-not [bool]$inputVerify.Json.success) {
                    throw "Input deep verification failed."
                }

                $sourceSha256 = [string]$inputVerify.Json.deep.sha256

                if ($null -ne $inputVerify.Json.deep.bytesReconstructed) {
                    $logicalSizeBytes = [int64]$inputVerify.Json.deep.bytesReconstructed
                }
            }
            else {
                throw "Unsupported or unknown input format: $format"
            }

            $outputCso = Join-Path $caseDir "output.cso"
            $restoredIso = Join-Path $caseDir "restored.iso"
            $encodeArgs = @()

            if ($format -eq "RawIso") {
                $encodeArgs = @(
                    "compress",
                    $file.FullName,
                    "-o",
                    $outputCso,
                    "--profile",
                    $profile,
                    "--threads",
                    ([string]$Threads),
                    "--deep-verify",
                    "--codec-report",
                    "--codec-report-block-limit",
                    "0",
                    "--json"
                )
            }
            else {
                $encodeArgs = @(
                    "repair",
                    $file.FullName,
                    "-o",
                    $outputCso,
                    "--profile",
                    $profile,
                    "--deep-verify",
                    "--codec-report",
                    "--codec-report-block-limit",
                    "0",
                    "--json"
                )
            }

            if ($UseZopfli -and $format -eq "RawIso") {
                $encodeArgs += "--zopfli"
            }

            $encode = Invoke-JsonTool -Arguments $encodeArgs
            $encodeMilliseconds = $encode.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$encode.Process.PeakWorkingSetBytes)

            if (-not [bool]$encode.Json.success) {
                throw "Encode or normalization command reported success=false."
            }

            if (-not (Test-Path -LiteralPath $outputCso -PathType Leaf)) {
                throw "Encode command succeeded but output CSO was not found."
            }

            $outputSizeBytes = [int64](Get-Item -LiteralPath $outputCso).Length

            if ($logicalSizeBytes -gt 0) {
                $savedPercent = [Math]::Round((1.0 - ([double]$outputSizeBytes / [double]$logicalSizeBytes)) * 100.0, 4)
            }

            if ($null -ne $encode.Json.codecReport) {
                $selectedCodecWins = ConvertTo-StringIntMap -Value $encode.Json.codecReport.selectedCodecWins
                $rejectedReasons = ConvertTo-StringIntMap -Value $encode.Json.codecReport.rejectedReasons
                $candidateAttempts = ConvertTo-StringIntMap -Value $encode.Json.codecReport.candidateAttempts
                $codecCandidates = @($candidateAttempts.Keys)
                $selectedCodec = Get-TopCodec -CodecWins $selectedCodecWins
            }

            $verify = Invoke-JsonTool -Arguments @("verify", $outputCso, "--deep", "--sha256", "--json")
            $verifyMilliseconds = $verify.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$verify.Process.PeakWorkingSetBytes)
            $deepVerifySuccess = [bool]$verify.Json.success
            $outputSha256 = [string]$verify.Json.deep.sha256

            if (-not $deepVerifySuccess) {
                throw "Output deep verification failed."
            }

            $decode = Invoke-JsonTool -Arguments @("decompress", $outputCso, "-o", $restoredIso, "--force", "--json")
            $decodeMilliseconds = $decode.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$decode.Process.PeakWorkingSetBytes)

            if (-not (Test-Path -LiteralPath $restoredIso -PathType Leaf)) {
                throw "Decode command succeeded but restored ISO was not found."
            }

            $restoredSha256 = Get-Sha256Hex -Path $restoredIso
            $sha256Match = [string]::Equals($sourceSha256, $restoredSha256, [System.StringComparison]::OrdinalIgnoreCase) -and
                [string]::Equals($sourceSha256, $outputSha256, [System.StringComparison]::OrdinalIgnoreCase)

            if (-not $sha256Match) {
                throw "Logical SHA256 mismatch after verify/decode."
            }

            $caseSuccess = $true
        }
        catch {
            $overallSuccess = $false
            $failures.Add($_.Exception.Message) | Out-Null
        }

        $results.Add([ordered]@{
            input = $file.FullName
            profile = $profile
            format = $format
            inputSizeBytes = [int64]$file.Length
            logicalSizeBytes = [int64]$logicalSizeBytes
            outputSizeBytes = [int64]$outputSizeBytes
            savedPercent = $savedPercent
            encodeMilliseconds = $encodeMilliseconds
            decodeMilliseconds = $decodeMilliseconds
            verifyMilliseconds = $verifyMilliseconds
            inputVerifyMilliseconds = $inputVerifyMilliseconds
            peakWorkingSetBytes = [int64]$peakWorkingSetBytes
            codecCandidates = $codecCandidates
            selectedCodec = $selectedCodec
            selectedCodecWins = $selectedCodecWins
            rejectedReasons = $rejectedReasons
            sha256Match = $sha256Match
            deepVerifySuccess = $deepVerifySuccess
            warnings = @($warnings)
            failures = @($failures)
            success = $caseSuccess
        }) | Out-Null
    }
}

$artifactReportPath = $null

if ($KeepArtifacts) {
    $artifactReportPath = (Resolve-Path -LiteralPath $artifactRoot).ProviderPath
}

$report = [ordered]@{
    schemaVersion = 1
    command = "benchmark-truth-layer"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    repo = $script:RepoRoot
    configuration = $Configuration
    profiles = [string[]]$Profiles
    threads = $Threads
    useZopfli = [bool]$UseZopfli
    success = [bool]$overallSuccess
    skipped = [bool]($files.Count -eq 0)
    filesChecked = [int]$files.Count
    reportDirectory = (Resolve-Path -LiteralPath $OutputDirectory).ProviderPath
    artifacts = $artifactReportPath
    evidence = [ordered]@{
        outputTarget = "CSO1"
        externalComparisonDependency = "none"
        corpusStoredInGit = $false
        failedCasesProduceTrustedOutput = $false
    }
    cases = $results.ToArray()
}

$jsonPath = Join-Path $OutputDirectory "benchmark-truth-report.json"
$markdownPath = Join-Path $OutputDirectory "benchmark-truth-report.md"

Write-Utf8NoBom -Path $jsonPath -Text ($report | ConvertTo-Json -Depth 18)
Write-Utf8NoBom -Path $markdownPath -Text (Get-ReportMarkdown -Report $report)

if (-not $KeepArtifacts) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $Quiet) {
    Write-Host "Benchmark truth JSON: $jsonPath"
    Write-Host "Benchmark truth Markdown: $markdownPath"
    Write-Host "Files checked: $($files.Count)"
    Write-Host "Status: $(if ($overallSuccess) { 'PASS' } else { 'FAILED' })"
}

if ($overallSuccess) {
    exit 0
}

exit 1
