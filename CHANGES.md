# Changes

## 0.6.0

Hakamiq CsoKit 0.6.0 adds the Windows desktop app, improves the CLI release path, and hardens ISO/CSO verification. This release is mainly about making the tool easier to use without weakening the repair and verification rules.

### Added

- WPF desktop app for common ISO/CSO workflows.
- Official release gate script for local release validation.
- Raw ISO deep verification through the block-container verifier.
- CLI and Core tests for Raw ISO verification behavior.
- Arabic and English plain-text report separation.
- Better repair diagnostics for rebuild-only, corruption repair, and redump-required cases.
- 0.6.0 release gate notes under docs/release.

### Fixed

- Raw ISO deep verify no longer reports UnsupportedContainer when the input can be read as an ISO.
- Raw ISO verification now checks sector alignment before doing a full deep read.
- ISO diagnostics stay separate from CSO-only concepts such as headers, index tables, and sentinels.
- WPF operation names are clearer: Compress to CSO, Decompress to ISO, Verify, and Repair.
- Arabic UI status and operation labels are separated from English text.
- Output-change UI sizing was adjusted to avoid clipped labels.

### Changed

- Release metadata was bumped to 0.6.0 across Core, CLI, App, CMake, and native version output.
- The official release gate is portable by default.
- Real ISO smoke testing is optional instead of requiring a developer-specific game path.
- Public release validation no longer depends on a local corpus path.

### Release gate

Before publishing 0.6.0, the release gate must pass:

- Release build.
- Release test run.
- Published CLI package.
- Published WPF App package.
- SHA256 manifest generation.
- Optional real ISO smoke when a local game image is available.

That is enough for the public release. Deeper benchmark and forensic notes live under docs/archive.