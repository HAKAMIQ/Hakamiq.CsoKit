# Hakamiq CsoKit

Hakamiq CsoKit is a Windows x64 command-line tool for PSP CSO files.

It can inspect CSO files, verify their structure, decompress CSO files back to ISO, and compress ISO files to CSO.

## Current beta support

* CSO decompression
* ISO to CSO compression
* CSO info and verification
* ISO compression measurement without writing an output file
* Same-folder default output naming without creating output folders
* Progress output
* Safe Ctrl+C cancellation
* JSON output for scripts and integrations

## Download

Download the latest beta package from the Releases page:

```text
hakamiq-csokit-*-win-x64.zip
```

Extract the ZIP file to any folder.

The extracted folder should include:

```text
hakamiq-cso.exe
Hakamiq.Cso.Native.dll
README.md
LICENSE.txt
SHA256SUMS.txt
```

Keep `Hakamiq.Cso.Native.dll` next to `hakamiq-cso.exe`.

## Quick start

Open PowerShell in the extracted folder and run:

```powershell
.\hakamiq-cso.exe --help
```

Hakamiq CsoKit is a command-line tool, not a double-click desktop app.

## Version

```powershell
.\hakamiq-cso.exe --version
```

## Commands

Show CSO information:

```powershell
.\hakamiq-cso.exe info ".\game.cso"
```

Verify a CSO file:

```powershell
.\hakamiq-cso.exe verify ".\game.cso"
```

Compress ISO to CSO in the same folder:

```powershell
.\hakamiq-cso.exe compress ".\game.iso"
```

If `.\game.cso` already exists, Hakamiq CsoKit writes `.\game - Hakamiq Converted.cso` instead. If that also exists, it writes `.\game - Hakamiq Converted 2.cso`, then keeps counting upward.

Use an explicit output file when needed:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso"
```

Estimate CSO size without writing an output file:

```powershell
.\hakamiq-cso.exe compress ".\game.iso" --measure
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

## Native backend

The release package includes:

```text
Hakamiq.Cso.Native.dll
```

Check it with:

```powershell
.\hakamiq-cso.exe native-info
```

If the native backend is unavailable, make sure the DLL is still in the same folder as the EXE.

## JSON output

Add `--json` when another program or script needs structured output:

```powershell
.\hakamiq-cso.exe verify ".\game.cso" --json
.\hakamiq-cso.exe compress ".\game.iso" --measure --json
```

Manual PowerShell use usually works best with the default text output.

## Checksums

`SHA256SUMS.txt` is included for release file verification.

Use it to check that the downloaded files were not changed or corrupted after release.

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
