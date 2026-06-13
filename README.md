# Hakamiq CsoKit

Hakamiq CsoKit is a modern command-line CSO inspection and decompression toolkit.

Current scope:

- Read CSO header information.
- Verify CSO header and index table.
- Decompress CSO v1 to ISO.
- Support structured JSON output for integration.
- Support quiet mode, progress output, Ctrl+C cancellation, overwrite protection, and disk-space preflight.

## Commands

```text
hakamiq-cso info <input.cso> [--json]
hakamiq-cso verify <input.cso> [--json]
hakamiq-cso decompress <input.cso> -o <output.iso> [--force] [--quiet] [--json]
```

## Examples

Read CSO information:

```powershell
.\hakamiq-cso.exe info ".\game.cso"
```

Verify a CSO:

```powershell
.\hakamiq-cso.exe verify ".\game.cso"
```

Decompress CSO to ISO:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso"
```

Overwrite an existing ISO:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --force
```

Machine-readable output:

```powershell
.\hakamiq-cso.exe verify ".\game.cso" --json
```

Quiet decompression:

```powershell
.\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --quiet
```

## Exit codes

```text
0    Success
1    General failure
2    Invalid arguments
10   Input not found
11   Invalid CSO header
12   Unsupported CSO version
13   Corrupt CSO index table
14   Output already exists
15   Cannot write output
16   Not enough disk space
20   Decompression failed
130  Operation canceled
```

## Current limitations

- Decompression currently supports CSO v1 only.
- The verifier checks CSO structure; decompression compatibility is currently separate.
- ISO to CSO compression is not implemented yet.
- CHD integration is not part of this repository yet.
