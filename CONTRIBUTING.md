# Contributing

These scripts are for contributors and maintainers only.

## General rules

Do not commit release artifacts, game images, local corpus files, bin folders, obj folders, or temporary output.

Run the relevant gate before changing compression, repair, verification, packaging, or release behavior.

Do not recreate an existing release tag unless the release is intentionally being replaced.

## Developer roundtrip gate

Use the roundtrip gate before changing compression behavior. It compresses a real ISO to CSO, decompresses the generated CSO back to ISO, then compares SHA256 hashes.

    .\scripts\Run-RoundtripGate.ps1 -InputIso "D:\Games\PSP\game.iso"

Keep generated artifacts for inspection:

    .\scripts\Run-RoundtripGate.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts

The script does not overwrite existing files and does not create output folders automatically.

## Developer profile roundtrip matrix

Use the profile matrix before changing profile behavior. It runs ISO to CSO to ISO roundtrip checks for selected compression profiles.

    .\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso"

Check specific profiles:

    .\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso" -Profiles game-safe,smallest,fast

Keep generated artifacts:

    .\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts

## Developer release gate

Use the consolidated release gate before release-oriented commits.

    .\scripts\Run-ReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"

Skip real ISO conversion checks:

    .\scripts\Run-ReleaseGate.ps1 -SkipRealIsoGates

Keep generated real-gate artifacts:

    .\scripts\Run-ReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso" -KeepArtifacts

## Developer published EXE smoke

Use the published EXE smoke after the consolidated release gate. It publishes hakamiq-cso.exe and tests the executable directly.

    .\scripts\Run-PublishedExeSmoke.ps1 -InputIso "D:\Games\PSP\game.iso"

Skip real ISO conversion checks:

    .\scripts\Run-PublishedExeSmoke.ps1 -SkipRealIsoGates

## Developer final release gate

Use the final release gate after the consolidated release gate and published EXE smoke are stable.

    .\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"

Allow a dirty working tree and skip package creation for local validation:

    .\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso" -AllowDirty -SkipReleasePackage

Quick non-ISO check:

    .\scripts\Run-FinalReleaseGate.ps1 -SkipRealIsoGates -SkipReleasePackage

The full final gate requires a clean Git working tree unless -AllowDirty is supplied.

## Official release gate

Use the official portable gate before publishing:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64

Optional local real-corpus smoke:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "D:\Games\PSP\game.iso"

If NuGet vulnerability metadata times out with NU1900, retry later. If the release must be validated while the NuGet audit feed is unavailable, use -SkipNuGetAudit and document that the NuGet audit was skipped for that run.