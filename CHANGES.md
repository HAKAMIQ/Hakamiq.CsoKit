# Official Release Gate and Raw ISO Verify Hardening

## Fixed
- Enabled RawIso deep verification through IsoContainerReader instead of returning UnsupportedContainer.
- Added strict raw ISO sector-alignment preflight before full deep read.
- Kept ISO verify diagnostics separate from CSO-specific header/index/sentinel concepts.
- Added RawIso deep verify tests for Core and CLI JSON contracts.
- Kept Arabic/English plain text report separation and UI status localization.
- Bumped release metadata to 0.6.0 across Core, CLI, App, CMake, and native version output.

## Added
- `scripts/Run-OfficialReleaseGate.ps1` for official local release validation.
- `docs/RELEASE_0_6_0_GATE.md` with the release process.

## Gate expectation
- `dotnet build -c Release` must pass.
- `dotnet test -c Release --no-build` must pass.
- Published CLI and WPF App ZIPs must be clean and include SHA256 manifests.
- Optional real ISO gate should pass before public release.

## Release notes
- Added `RELEASE_NOTES.md` for the 0.6.0 release.
