# Hakamiq CsoKit 0.5.0 Release Scorecard

This scorecard is a release blocker list. It is not marketing copy. Do not push a tag or publish a release until the local tree is clean, the commit is signed, the tag is signed, and GitHub release verification is complete.

| Area | Required evidence | Status |
|---|---|---|
| Correctness / roundtrip | Real PSP input passes compress, deep verify, decompress, and SHA256 restore for game-safe, compat, fast, and smallest. | PASS locally before R3; rerun gate after R3 changes |
| Release engineering | Run-FinalReleaseGate.ps1 produces release ZIP, source ZIP, verifies packages, and checks final native backend. | PASS locally with AllowDirty; repeat after commit on clean tree |
| Native loading | Final published hakamiq-cso.exe native-info reports Backend native, Native available True, and Native version 0.5.0 ABI 2. | PASS locally |
| Third-party notices | `THIRD_PARTY_NOTICES.md` documents Zopfli, zlib, and libdeflate license intake and release verification requires the notice in ZIP/manifest. | PASS locally |
| Native runtime policy | Native backend remains optional and fallback-safe; explicit `--zopfli` requires native availability. | PASS locally |
| Native compression performance | Native zlib, libdeflate, and explicit Zopfli raw-Deflate smoke tests roundtrip through the codec matrix. | PASS locally |
| Compression competitiveness | Multi-candidate managed Deflate, native zlib/libdeflate, optional native Zopfli, configurable block size, and threaded pipeline are implemented. Real maxcso comparison must be rerun. | PENDING BENCHMARK |
| Workflow safety | Release workflow runs only from pushed v* tags and does not expose workflow_dispatch. | PASS locally |
| Signing readiness | Final commit and v0.5.0 tag still need signing and GitHub verification. | PENDING |
| Official release | No official release until clean-tree final gate, signed commit, signed tag, GitHub release, and attestations are verified. | BLOCKED |

## Latest local validation

- Release Gate without real ISO: PASS
- Final native-info: Backend native / Native available True / Native version 0.5.0 ABI 2
- Zopfli CLI smoke: PASS
- Third-party notice package check: PASS
- Benchmark CSV: artifact-only local evidence, not committed
- Benchmark roundtrip: previous sample PASS for all rows before the threaded/Zopfli compression phase

## Previous benchmark evidence

| Tool | Backend | Profile | CsoBytes | CompressSeconds | Roundtrip |
|---|---|---|---:|---:|---|
| hakamiq | native-runtime | smallest | 269082233 | 12.731 | PASS |
| hakamiq | native-runtime | compat | 269082233 | 11.656 | PASS |
| hakamiq | native-runtime | fast | 312978389 | 3.094 | PASS |
| hakamiq-managed | managed-runtime | fast | 312978389 | 3.068 | PASS |
| maxcso | external | default | 265247498 | 19.327 | PASS |

## Current compression decision

- Keep native backend loading, ABI verification, native-info, final native release gate, and managed fallback testing.
- Keep `--zopfli` explicit because Zopfli is intentionally much slower than the default profiles.
- Keep threaded compression bounded and ordered so CSO index output remains deterministic.
- Rerun the real ISO benchmark against maxcso after committing this phase.
