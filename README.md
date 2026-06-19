# Hakamiq CsoKit

Hakamiq CsoKit is a Windows x64 PSP ISO/CSO toolkit with a command-line interface and a WPF desktop app.

## Download

Download the latest Windows x64 package from the [Releases page](https://github.com/HAKAMIQ/Hakamiq.CsoKit/releases/latest):

    hakamiq-csokit-*-win-x64.zip

Extract the ZIP file to any folder. Keep Hakamiq.Cso.Native.dll next to the executables.

## Quick start

Run the desktop app:

    .\Hakamiq.Cso.App.exe

Run the command-line tool:

    .\hakamiq-cso.exe --help

Show the version:

    .\hakamiq-cso.exe --version

## Common commands

Show CSO information:

    .\hakamiq-cso.exe info ".\game.cso"

Verify a CSO file:

    .\hakamiq-cso.exe verify ".\game.cso"

Deep-verify a CSO file and calculate SHA256:

    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256

Detect an input format:

    .\hakamiq-cso.exe detect ".\game.iso"

Analyze a PSP ISO:

    .\hakamiq-cso.exe analyze ".\game.iso" --psp

Compress ISO to CSO:

    .\hakamiq-cso.exe compress ".\game.iso"

Compress using the fast profile:

    .\hakamiq-cso.exe compress ".\game.iso" --profile fast

Decompress CSO to ISO:

    .\hakamiq-cso.exe decompress ".\game.cso"

Repair or normalize readable input into CSO1:

    .\hakamiq-cso.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Profiles: game-safe (default), compat, fast, smallest, archive-smallest.

For all CLI options, see [docs/CLI.md](docs/CLI.md).

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | General failure |
| 2 | Invalid command or missing argument |
| 10 | Input file not found |
| 11 | Invalid CSO file header |
| 12 | Unsupported CSO file |
| 13 | Corrupt CSO index table |
| 14 | Output file already exists |
| 15 | Cannot write output file |
| 16 | Not enough disk space |
| 20 | Decompression failed |
| 21 | Compression failed |
| 130 | Operation canceled by user |

## Limitations

Hakamiq CsoKit focuses on PSP ISO/CSO workflows.
ZSO, DAX, and CSO2 are supported as readable input containers, but CSO1 is the default output format.
Verification checks structure; it does not guarantee emulator or device compatibility.

## More documentation

- CLI reference: [docs/CLI.md](docs/CLI.md)
- Contributor scripts and release gates: [CONTRIBUTING.md](CONTRIBUTING.md)