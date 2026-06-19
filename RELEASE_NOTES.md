# Hakamiq CsoKit 0.6.0

Windows x64 release focused on PSP ISO/CSO workflows, WPF usability, and release-grade verification.

## What's new

- Added WPF desktop app release path for Windows.
- Added localized plain-text reports for Arabic and English.
- Added deep forensic CSO verification details.
- Added Raw ISO deep verification through the block-container verifier.
- Added repair diagnostics that distinguish rebuild-only from corruption repair and redump-required cases.
- Improved progress feedback, report opening, output naming, and language switching.
- Added official release gate script.

## Supported workflows

- Detect supported container/image format.
- Analyze PSP ISO structure.
- Verify CSO and Raw ISO.
- Compress ISO to CSO.
- Decompress CSO to ISO.
- Rebuild/repair readable ISO/CSO into normalized CSO.

## Release gate

Before publishing, run:

```powershell
.\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "G:\path\game.iso"
```

If NuGet vulnerability metadata times out with `NU1900`, retry later. If the release must be validated while the NuGet audit feed is unavailable, use `-SkipNuGetAudit` and document that the NuGet audit was skipped for that run.
