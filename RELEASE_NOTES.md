# Hakamiq CsoKit 0.5.0-beta.1

Windows x64 beta release for PSP ISO/CSO workflows.

## Supported workflows

- Inspect CSO files.
- Verify CSO header and index structure.
- Decompress CSO to ISO.
- Compress ISO to CSO.
- Estimate CSO size without writing output.
- Use compression profiles: `smallest`, `compat`, and `fast`.
- Use text output for manual PowerShell work or JSON output for automation.

## Compression profiles

- `smallest`: default safe profile. Best managed compression size currently used by the tool.
- `compat`: compatibility-oriented profile. Uses the same safe CSO behavior as `smallest` in this beta.
- `fast`: faster compression path. Produces larger CSO files in exchange for speed.

## Validation status

This beta line includes local gates for:

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
- `SHA256SUMS.txt`

Keep the files together in the same folder.

## Native backend note

The release package includes the native probe DLL. Compression remains on the managed safe path in this beta unless a future version explicitly enables a native compression backend.

## Scope

This release intentionally focuses on PSP ISO/CSO workflows only. It does not add other PSP formats, CHD workflows, game-specific patches, lossy compression, or emulator configuration.
