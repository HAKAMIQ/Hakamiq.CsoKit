# Hakamiq CsoKit 0.5.0

Windows x64 stable release for PSP ISO/CSO workflows.

## Supported workflows

- Inspect CSO files.
- Verify CSO header and index structure.
- Decompress CSO to ISO.
- Compress ISO to CSO.
- Estimate CSO size without writing output.
- Use compression profiles: `smallest`, `compat`, and `fast`.
- Use configurable `--threads`, `--block`, and optional native `--zopfli`.
- Use text output for manual PowerShell work or JSON output for automation.

## Compression profiles

- `smallest`: default safe profile. Tries multiple managed Deflate candidates per block and chooses the smallest valid sector.
- `compat`: compatibility-oriented profile. Uses a conservative safe Deflate path.
- `fast`: faster compression path. Produces larger CSO files in exchange for speed.

## Advanced compression

- `--threads <n>` enables a bounded parallel compression pipeline with ordered CSO output.
- `--block <bytes>` supports larger power-of-two CSO block sizes for users who want better ratios and can accept reader compatibility tradeoffs.
- `--zopfli` enables slower native Zopfli raw-Deflate trials. It requires `Hakamiq.Cso.Native.dll` to be available beside the executable.

## Validation status

This release includes local gates for:

- Restore, build, and tests.
- Forbidden public wording scan.
- CLI help smoke.
- JSON argument smoke.
- Real ISO -> CSO -> ISO SHA256 roundtrip.
- Real profile matrix for `smallest`, `compat`, and `fast`.
- Published `hakamiq-cso.exe` smoke tests.
- Release ZIP and source package verification.

## Package contents

The win-x64 release package contains:

- `hakamiq-cso.exe`
- `Hakamiq.Cso.Native.dll`
- `README.md`
- `LICENSE.txt`
- `RELEASE_NOTES.md`
- `THIRD_PARTY_NOTICES.md`
- `SHA256SUMS.txt`

Keep the files together in the same folder.

## Native backend note

The release package includes the native DLL used for runtime probing and optional Zopfli compression trials. Normal compression remains fully functional without `--zopfli`; explicit `--zopfli` fails clearly if the native backend is unavailable.

## Scope

This release intentionally focuses on PSP ISO/CSO workflows only. It does not add other PSP formats, CHD workflows, game-specific patches, lossy compression, or emulator configuration.
