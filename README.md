# Hakamiq CsoKit

Hakamiq CsoKit is a Windows x64 PSP ISO/CSO toolkit with both a command-line interface and a WPF desktop app.

It can detect disc image containers, analyze PSP ISO structure, inspect CSO files, deep-verify CSO blocks, safely repair readable ISO/CSO1/ZSO/DAX/CSO2 input into game-safe CSO1, decompress CSO files back to ISO, and compress ISO files to CSO.

## Current support

- WPF desktop app for common ISO/CSO workflows.
- CLI for scripting, automation, and advanced diagnostics.
- CSO decompression.
- ISO to CSO compression.
- CSO info, shallow verification, and deep block verification.
- Raw ISO deep verification.
- PSP ISO structure analysis without modifying game files.
- Safe repair/normalize for readable ISO, CSO1, ZSO, DAX, and supported CSO2 input.
- Format detection for ISO, CSO1, CSO2, ZSO, DAX, and unknown input.
- ISO compression measurement without writing an output file.
- Same-folder default output naming without creating output folders.
- Compression profiles: game-safe, compat, fast, smallest, and archive-smallest.
- Multi-candidate raw Deflate trial engine with in-memory roundtrip verification.
- Configurable compression threads and CSO block size.
- Native zlib and libdeflate raw-Deflate candidates when the native DLL is available.
- Optional native Zopfli Deflate trials with --zopfli.
- Progress output.
- Safe Ctrl+C cancellation.
- JSON output for scripts and integrations.

## Download

Download the latest Windows x64 package from the Releases page:

    hakamiq-csokit-*-win-x64.zip

Extract the ZIP file to any folder.

The extracted folder includes the command-line executable, desktop app executable, native backend DLL, and release documentation:

    hakamiq-cso.exe
    Hakamiq.Cso.App.exe
    Hakamiq.Cso.Native.dll
    README.md
    LICENSE.txt
    RELEASE_NOTES.md
    THIRD_PARTY_NOTICES.md
    SHA256SUMS.txt

Keep Hakamiq.Cso.Native.dll next to the executables.

## Quick start

For the desktop app, run:

    .\Hakamiq.Cso.App.exe

For the command-line tool, open PowerShell in the extracted folder and run:

    .\hakamiq-cso.exe --help

## Version

    .\hakamiq-cso.exe --version
## Commands

Show CSO information:

```powershell
.\hakamiq-cso.exe info ".\game.cso"
```

Verify a CSO file:

```powershell
.\hakamiq-cso.exe verify ".\game.cso"
.\hakamiq-cso.exe verify ".\game.cso" --deep --sha256
```

Detect and analyze input before compression:

```powershell
.\hakamiq-cso.exe detect ".\game.iso" --json
.\hakamiq-cso.exe analyze ".\game.iso" --psp --json
```

Safely rebuild readable ISO/CSO1/ZSO/DAX/CSO2 input as game-safe CSO1:

```powershell
.\hakamiq-cso.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify
```

ZSO, DAX, and CSO2 are supported as input decode/normalize containers only in this stage. Hakamiq CsoKit still writes CSO1 by default and does not make ZSO, DAX, or CSO2 an output format.

Compress ISO to CSO in the same folder:

```powershell
.\hakamiq-cso.exe compress ".\game.iso"
```

Choose a compression profile:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" --profile game-safe
.\hakamiq-cso.exe compress ".\game.iso" --profile compat
.\hakamiq-cso.exe compress ".\game.iso" --profile fast
.\hakamiq-cso.exe compress ".\game.iso" --profile smallest
```

`game-safe` is the default profile. It writes CSO1, keeps the default block size at 2048 bytes, uses raw Deflate candidates only, and enables deep verification after compression. With the native DLL present, `game-safe` can try managed Deflate, native zlib strategies, and native libdeflate before selecting the smallest block that roundtrips in memory. `smallest` tries more candidates but does not enable Zopfli unless `--zopfli` is explicit. `archive-smallest` is for archival experiments and may reduce compatibility if paired with larger block sizes. The short alias `--fast` is equivalent to `--profile fast`.

Do not combine `--fast` with another explicit profile. Use `--profile fast`, use `--fast`, or remove the conflicting option.

Tune compression when needed:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" --threads 8
.\hakamiq-cso.exe compress ".\game.iso" --block 16K
.\hakamiq-cso.exe compress ".\game.iso" --zopfli
.\hakamiq-cso.exe compress ".\game.iso" --codec-report
.\hakamiq-cso.exe compress ".\game.iso" --threads=8 --block=16K --zopfli
```

`--threads` controls compression workers. `--block` accepts byte values and `K` or `M` suffixes, must be at least `2048`, and must be a power of two. Larger blocks can improve compression but may reduce compatibility or increase random-read latency. The default remains 2048 for PSP/PPSSPP safety.

`--zopfli` enables slower native Zopfli raw-Deflate trials for maximum size reduction. It requires `Hakamiq.Cso.Native.dll` beside the executable and is never enabled by default.

`--codec-report` prints how many blocks were won by each codec candidate. JSON compression output always includes the same `metrics.codecWins` object.

Safe repair does not invent missing data. If a compressed block is corrupt or the source is incomplete, the command fails with a diagnosis such as `ReDumpRequired` and does not write a partial output file. Padding a non-2048-aligned ISO is only done when `--repair pad-last-sector` is explicit.

If `.\game.cso` already exists, Hakamiq CsoKit writes `.\game - Hakamiq Converted.cso` instead. If that also exists, it writes `.\game - Hakamiq Converted 2.cso`, then keeps counting upward.

Use an explicit output file when needed:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso"
.\hakamiq-cso.exe compress ".\game.iso" --output-path ".\game.cso"
```

Estimate CSO size without writing an output file:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" --measure
.\hakamiq-cso.exe compress ".\game.iso" --measure --profile fast
```

Decompress CSO to ISO in the same folder:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso"
```

If `.\game.iso` already exists, Hakamiq CsoKit writes `.\game - Hakamiq Converted.iso` instead.

Use an explicit output file when needed:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso"
```

Overwrite the output file:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso" --force
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --force
```

Run with less console output:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" --quiet
.\hakamiq-cso.exe decompress ".\game.cso" --quiet
```

Use full paths:

```powershell
.\hakamiq-cso.exe compress "D:\Games\PSP\game.iso"
.\hakamiq-cso.exe decompress "D:\Games\PSP\game.cso"
```

Hakamiq CsoKit does not create output folders automatically. If you pass `-o`, the destination folder must already exist.


## Developer roundtrip gate

Use the roundtrip gate before changing compression behavior. It compresses a real ISO to CSO, decompresses the generated CSO back to ISO, then compares SHA256 hashes.

```powershell
.\scripts\Run-RoundtripGate.ps1 -InputIso "D:\Games\PSP\game.iso"
```

By default, test artifacts are written beside the input ISO with unique safe names and removed after a successful match. To keep the generated CSO and restored ISO for inspection:

```powershell
.\scripts\Run-RoundtripGate.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts
```

The script does not overwrite existing files and does not create output folders automatically.

## Developer profile roundtrip matrix

Use the profile matrix before changing profile behavior. It runs a real ISO -> CSO -> ISO roundtrip for each selected compression profile, then compares SHA256 hashes and restored ISO sizes.

```powershell
.\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso"
```

By default, the matrix checks `game-safe`, `compat`, `fast`, and `smallest`, with deep verification before decompression. To check specific profiles:

```powershell
.\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso" -Profiles game-safe,smallest,fast
```

Successful artifacts are removed by default. To keep the generated CSO and restored ISO files for inspection:

```powershell
.\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts
```

The script writes uniquely named artifacts beside the input ISO, does not overwrite existing files, and does not create output folders automatically.


## Developer release gate

Use the consolidated release gate before release-oriented commits. It runs restore, build, tests, forbidden-term scan, help smoke, JSON argument smoke, the real roundtrip gate, and the profile roundtrip matrix.

```powershell
.\scripts\Run-ReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"
```

For a quick gate that skips the real ISO conversion checks:

```powershell
.\scripts\Run-ReleaseGate.ps1 -SkipRealIsoGates
```

To keep generated real-gate artifacts for manual inspection:

```powershell
.\scripts\Run-ReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts
```


## Developer published EXE smoke

Use the published EXE smoke after the consolidated release gate. It publishes `hakamiq-cso.exe` to `artifacts\published-exe-smoke\win-x64` and tests the executable directly instead of `dotnet run`.

```powershell
.\scripts\Run-PublishedExeSmoke.ps1 -InputIso "D:\Games\PSP\game.iso"
```

For a quick publish-only smoke that skips real ISO conversion checks:

```powershell
.\scripts\Run-PublishedExeSmoke.ps1 -SkipRealIsoGates
```

The full smoke runs help, version, native-info, JSON argument checks, measure checks, deep verify checks, and real ISO -> CSO -> ISO SHA256 checks for `game-safe`, `compat`, `fast`, and `smallest` using the published executable. Generated smoke artifacts are removed after success unless `-KeepArtifacts` is supplied.

## Developer final release gate

Use the final release gate after the consolidated release gate and published EXE smoke are stable. It checks project version fields, runs the release gate, runs the published EXE smoke, publishes the win-x64 release ZIP, verifies the release package, and creates the source package.

```powershell
.\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"
```

For local validation before committing the final-gate script itself, allow a dirty working tree and skip release package creation:

```powershell
.\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso" -AllowDirty -SkipReleasePackage
```

For a quick non-ISO check:

```powershell
.\scripts\Run-FinalReleaseGate.ps1 -SkipRealIsoGates -SkipReleasePackage
```

The full final gate requires a clean Git working tree unless `-AllowDirty` is supplied.

## Native backend

The release package includes:

```text
Hakamiq.Cso.Native.dll
```

Check it with:

```powershell
.\hakamiq-cso.exe native-info
```

If the native backend is unavailable, make sure the DLL is still in the same folder as the EXE. Native zlib and libdeflate are used only as safe raw-Deflate candidates, and the managed Deflate fallback remains available. The `--zopfli` option requires this native backend and remains opt-in.

List codec availability with:

```powershell
.\hakamiq-cso.exe codecs
```

## JSON output

Add `--json` when another program or script needs structured output:

```powershell
.\hakamiq-cso.exe verify ".\game.cso" --json
.\hakamiq-cso.exe verify ".\game.cso" --deep --sha256 --json
.\hakamiq-cso.exe compress ".\game.iso" --measure --profile smallest --json
.\hakamiq-cso.exe compress ".\game.iso" --profile fast --json
```

Compress and measure JSON output includes `schemaVersion`, `command`, `mode`, `success`, `options.profile`, `options.blockSize`, `options.threads`, `options.zopfli`, `metrics`, and `error` when a command fails. The profile object reports the resolved profile name, whether fast mode is enabled, and the logical compression level.

Example measure profile block:

```json
{
  "schemaVersion": 1,
  "command": "compress",
  "mode": "measure",
  "success": true,
  "options": {
    "profile": {
      "name": "smallest",
      "fast": false,
      "level": 9
    },
    "force": false,
    "autoOutput": false,
    "blockSize": 2048,
    "threads": 8,
    "zopfli": false,
    "deepVerify": false
  }
}
```

Invalid profile values return a clear argument error. Supported profiles are `game-safe`, `compat`, `fast`, `smallest`, and `archive-smallest`. Conflicting profile options return the same argument error contract in JSON mode and a concise message in text mode.

Manual PowerShell use usually works best with the default text output.

## Checksums

`SHA256SUMS.txt` is included for release file verification.

Use it to check that the downloaded files were not changed or corrupted after release.

`THIRD_PARTY_NOTICES.md` documents third-party native compression components and licenses, including Zopfli, zlib, and libdeflate.

## Exit codes

```text
0    Success
1    General failure
2    Invalid command or missing argument
10   Input file not found
11   Invalid CSO file header
12   Unsupported CSO file
13   Corrupt CSO index table
14   Output file already exists
15   Cannot write output file
16   Not enough disk space
20   Decompression failed
21   Compression failed
130  Operation canceled by user
```

## Limitations

Hakamiq CsoKit intentionally focuses on ISO and CSO workflows. Other PSP image/container families are outside the current v1 scope.

Verification checks CSO structure. It does not confirm whether a game works in a specific emulator or device.
