# Hakamiq CsoKit 0.5.0 Release Scorecard

This scorecard is a release blocker list. It is not marketing copy. Do not push a tag or publish a release until the local tree is clean, the commit is signed, the tag is signed, and GitHub release verification is complete.

| Area | Required evidence | Status |
|---|---|---|
| Correctness / roundtrip | Real PSP input passes compress, verify, decompress, and SHA256 restore for smallest, compat, and fast. | PASS locally |
| Release engineering | Run-FinalReleaseGate.ps1 produces release ZIP, source ZIP, verifies packages, and checks final native backend. | PASS locally with AllowDirty; repeat after commit on clean tree |
| Native loading | Final published hakamiq-cso.exe native-info reports Backend native, Native available True, and Native version 0.5.0 ABI 1. | PASS locally |
| Native runtime policy | Native backend is ABI and fallback infrastructure only in 0.5.0. | PASS |
| Native compression performance | Native compression acceleration claim is rejected because managed fast is not slower in current benchmark evidence. | PASS as rejection of claim |
| Compression competitiveness | Hakamiq is faster than maxcso on this sample, but maxcso produces a smaller CSO. | NOT 10/10 |
| Workflow safety | Release workflow runs only from pushed v* tags and does not expose workflow_dispatch. | PASS locally |
| Signing readiness | Final commit and v0.5.0 tag still need signing and GitHub verification. | PENDING |
| Official release | No official release until clean-tree final gate, signed commit, signed tag, GitHub release, and attestations are verified. | BLOCKED |

## Latest local validation

- Final Release Gate: PASS
- Final native-info: Backend native / Native available True / Native version 0.5.0 ABI 1
- Benchmark CSV: artifact-only local evidence, not committed
- Benchmark roundtrip: PASS for all rows

## Latest benchmark evidence

| Tool | Backend | Profile | CsoBytes | CompressSeconds | Roundtrip |
|---|---|---|---:|---:|---|
| hakamiq | native-runtime | smallest | 269082233 | 12.731 | PASS |
| hakamiq | native-runtime | compat | 269082233 | 11.656 | PASS |
| hakamiq | native-runtime | fast | 312978389 | 3.094 | PASS |
| hakamiq-managed | managed-runtime | fast | 312978389 | 3.068 | PASS |
| maxcso | external | default | 265247498 | 19.327 | PASS |

## R1 architecture decision

- Do not claim native compression performance improvement for 0.5.0.
- Do not keep native block store hints or any native compression hot-path change in production.
- Keep native backend loading, ABI verification, native-info, final native release gate, and managed fallback testing.
- Treat future compression parallelism, pipelining, or native acceleration as a separate measured phase, not part of R1.
