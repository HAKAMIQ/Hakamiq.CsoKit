Hakamiq CsoKit is a Windows x64 command-line tool for PSP CSO files.

It can inspect CSO files, verify their structure, and decompress supported CSO files back to ISO.

Current beta support:

* CSO v1 decompression
* Legacy CSO v1 headers using version `0` or `1`
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

## Native backend

The release package includes a native backend:

```text
Hakamiq.Cso.Native.dll
```

Check it with:

```powershell
.\hakamiq-cso.exe native-info
```

If the native backend is unavailable, make sure the DLL is still in the same folder as the EXE.

## Commands

Show CSO information:

```powershell
.\hakamiq-cso.exe info ".\game.cso"
```

Verify a CSO file:

```powershell
.\hakamiq-cso.exe verify ".\game.cso"
```

Decompress CSO to ISO:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso"
```

Overwrite the output file:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --force
```

Run with less console output:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --quiet
```

Use full paths:

```powershell
.\hakamiq-cso.exe decompress "D:\Games\PSP\game.cso" -o "D:\Games\PSP\game.iso"
```

## JSON output

Add `--json` when another program or script needs structured output:

```powershell
.\hakamiq-cso.exe verify ".\game.cso" --json
```

Manual PowerShell use usually works best with the default text output.

## Checksums

`SHA256SUMS.txt` is included for release file verification.

You can use it to check that the downloaded files were not changed or corrupted.

## Exit codes

Exit codes are useful for scripts, batch files, CI jobs, and integrations.

```text
0    Success
1    General failure
2    Invalid command or missing argument
10   Input file not found
11   Invalid CSO file header
12   Unsupported CSO version
13   Corrupt CSO index table
14   Output file already exists
15   Cannot write output file
16   Not enough disk space
20   Decompression failed
130  Operation canceled by user
```

## Limitations

Not implemented yet:

* CSO v2
* ZSO
* DAX
* ISO to CSO compression

Notes:

* Verification checks CSO structure, not game compatibility.
* CHD integration is not included in this tool.
