# Hakamiq.CsoKit Release Scorecard — R3-C

## Current status

- Independence: PASS. The repository is self-contained and does not depend on external comparison tools for tests, scripts, or release gates.
- CLI coverage: PASS. Public commands remain `detect`, `analyze`, `verify`, `repair`, `decompress`, `compress`, `codecs`, and `native-info`.
- CSO1 compression safety: PASS with game-safe defaults, deep verification option, atomic output, and stored-block fallback.
- Container intake: PASS for readable CSO1, CSO2, ZSO, and DAX fixtures.
- Repair/normalize: PASS for streaming container repair to CSO1 output without a temporary ISO on the primary container path.
- Corrupt refusal: PASS by tests for truncated or corrupt payloads that must not leave final output.
- PSP ISO analysis: PASS for bounded PARAM.SFO and UMD_DATA.BIN metadata reads with warnings for missing/corrupt metadata.
- JSON diagnostics: PASS for schema versioning and stable command/success/error fields across primary commands.
- Repository hygiene: PASS when `scripts/Test-RepoTextEncoding.ps1` and the forbidden-term scan are clean.
- Native backend clarity: PASS by documentation and honest `native-info` availability reporting.

## Proven evidence in-tree

- Synthetic and golden fixtures are deterministic and below 1 MB.
- No copyrighted game data is stored in tests.
- CSO1, CSO2, ZSO, and DAX container readers are exercised with safe byte patterns.
- Repair tests assert streaming mode and no temporary ISO artifact.
- PARAM.SFO tests cover valid, missing, corrupt, and DISC_ID mismatch cases.

## Not proven yet

- Broad real-world corpus quality requires the optional local corpus gate on privately owned files.
- Raw compression ranking is not claimed without a controlled internal benchmark.
- Native dependency availability depends on the local build/runtime environment.
- Very large CSO1 outputs that require shifted indexes remain a deliberate safety refusal.

## Final gate checklist

```powershell
git status --short
# Run the local forbidden external-comparison hook scan before release.
scripts/Test-RepoTextEncoding.ps1
scripts/Build-Native.ps1 -Configuration Release -Platform x64
dotnet build Hakamiq.CsoKit.slnx --no-restore
dotnet test Hakamiq.CsoKit.slnx --no-build --no-restore
scripts/Run-ReleaseGate.ps1 -SkipRealIsoGates
```

No push is part of this scorecard.
