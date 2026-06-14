# Hakamiq CsoKit stability fix notes

This source package applies the stability fixes requested after the CSO readiness review.

## P1-A sector engine foundation

- Add `CompressionMethod`, `SectorJob`, and `SectorResult` as the first block-pipeline data contracts.
- Add `CsoBlockReader`, `CsoIndexBuilder`, and `CsoOrderedOutputWriter` to split block reading, index construction, and ordered output writing from the compressor loop.
- Keep the current compression behavior unchanged: zlib/raw-deflate path, same store-raw decision, same ordered output flow.
- Clean remaining user-facing unsupported CSO wording to `Unsupported CSO file.`
- Add focused tests for the new sector/index foundation contracts.

## Fixed

- Treat legacy CSO header `version = 0` as supported alongside `version = 1`.
- Add `EffectiveHeaderSize` and use it for legacy index reading/verification so unreliable CSO `header_size` does not break valid legacy files.
- Add upper-bound validation for dangerous `block_size` and `index_shift` values.
- Make decompression output temp files unique instead of using `<output>.tmp`, preventing accidental deletion of a user-owned sibling temp file.
- Replace output via `File.Move(..., overwrite: true)` after successful temp write instead of deleting the destination first.
- Make CLI `--version` read `AssemblyInformationalVersion` instead of hardcoding `0.4.0-beta.1`.
- Fix CLI exit-code mapping for `UnsupportedVersion`, `BlockSizeTooLarge`, `InvalidIndexShift`, and additional corrupt-index verification codes.
- Change GitHub Actions artifact upload from `actions/upload-artifact@v7` to `actions/upload-artifact@v6`.
- Add source packaging script that excludes `.git`, `.vs`, `bin`, `obj`, `artifacts`, and `TestResults`.

## Added tests

- CSO raw-deflate decompression roundtrip.
- Legacy CSO `version = 0` decompression roundtrip.
- High-bit uncompressed block decompression roundtrip.
- Forced overwrite decompression behavior.
- Existing `<output>.tmp` sibling preservation.
- Legacy/unreliable CSO `header_size` behavior in header and index readers.

## Validation note

The editing environment used to prepare this package does not include the .NET SDK, so `dotnet build` / `dotnet test` could not be executed here. Run the normal local gates on Windows before merging:

```powershell
dotnet restore .\Hakamiq.CsoKit.slnx -r win-x64 -p:NuGetAudit=false
dotnet build .\Hakamiq.CsoKit.slnx -c Debug --no-restore -p:NuGetAudit=false
dotnet test .\Hakamiq.CsoKit.slnx -c Debug --no-build
.\scripts\Publish-Release.ps1
.\scripts\Verify-Release.ps1
```
