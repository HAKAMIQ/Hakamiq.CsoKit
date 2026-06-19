# Hakamiq CsoKit CLI Reference

This is the command-line reference for `hakamiq-cso.exe`.

The desktop app is the easier path for normal use. The CLI is for scripting, automation, diagnostics, and cases where you want exact control over compression, repair, or verification.

## Start here

Print help:

    .\hakamiq-cso.exe --help

Check the installed version:

    .\hakamiq-cso.exe --version

Check whether the native backend is available:

    .\hakamiq-cso.exe native-info

List available codecs:

    .\hakamiq-cso.exe codecs

## Inspect a file

Show CSO metadata:

    .\hakamiq-cso.exe info ".\game.cso"

Detect the input format before doing anything else:

    .\hakamiq-cso.exe detect ".\game.iso"

JSON is useful when another script needs to read the result:

    .\hakamiq-cso.exe detect ".\game.cso" --json

Detected formats include ISO, CSO1, CSO2, ZSO, DAX, and unknown input.

## Analyze a PSP ISO

Analyze checks PSP ISO structure without changing the file.

    .\hakamiq-cso.exe analyze ".\game.iso" --psp

For scripts:

    .\hakamiq-cso.exe analyze ".\game.iso" --psp --json

Run this before compression if you want a quick sanity check.

## Verify CSO files

Basic verification checks the container structure:

    .\hakamiq-cso.exe verify ".\game.cso"

Deep verification reads compressed blocks and validates more of the file:

    .\hakamiq-cso.exe verify ".\game.cso" --deep

Add SHA256 when you need a stable hash for records or comparison:

    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256

Machine-readable result:

    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256 --json

## Compress ISO to CSO

Default compression writes a CSO next to the ISO:

    .\hakamiq-cso.exe compress ".\game.iso"

Set the output path when you need a specific name:

    .\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso"

Overwrite only when you mean it:

    .\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso" --force

Estimate output size without writing a CSO:

    .\hakamiq-cso.exe compress ".\game.iso" --measure

For automation:

    .\hakamiq-cso.exe compress ".\game.iso" --profile fast --json

## Profiles

Available profiles:

    game-safe
    compat
    fast
    smallest
    archive-smallest

`game-safe` is the default. It writes CSO1, keeps the default 2048-byte block size, uses raw Deflate candidates, and deep-verifies the output.

`fast` is the quick path.

`smallest` tries harder candidates. It still does not enable Zopfli unless you ask for it.

`archive-smallest` is for experiments where size matters more than broad compatibility.

Pick a profile:

    .\hakamiq-cso.exe compress ".\game.iso" --profile fast

Shortcut:

    .\hakamiq-cso.exe compress ".\game.iso" --fast

Do not combine `--fast` with another explicit profile. Pick one.

## Compression tuning

Threads:

    .\hakamiq-cso.exe compress ".\game.iso" --threads 8

Block size:

    .\hakamiq-cso.exe compress ".\game.iso" --block 16K

Optional Zopfli trials:

    .\hakamiq-cso.exe compress ".\game.iso" --zopfli

Codec winner report:

    .\hakamiq-cso.exe compress ".\game.iso" --codec-report

Block size accepts raw bytes, `K`, or `M`. It must be at least 2048 and a power of two. Larger blocks can improve compression, but they may hurt compatibility or random-read behavior. For PSP safety, 2048 is still the sensible default.

## Decompress CSO to ISO

Default output goes next to the CSO:

    .\hakamiq-cso.exe decompress ".\game.cso"

Choose an output path:

    .\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso"

Overwrite intentionally:

    .\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --force

## Repair and normalize

Repair is conservative. It rebuilds readable input into game-safe CSO1, but it does not invent missing data.

    .\hakamiq-cso.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Readable input can include ISO, CSO1, ZSO, DAX, and supported CSO2. Output is CSO1 by default.

If a compressed block is corrupt or the source is incomplete, the command fails with a diagnosis such as `ReDumpRequired`. Good. A broken game should not become a fake "fixed" file.

Padding a non-2048-aligned ISO only happens when explicit repair behavior is requested.

## Output naming

If the default output already exists, Hakamiq CsoKit chooses a safe converted name:

    game.cso
    game - Hakamiq Converted.cso
    game - Hakamiq Converted 2.cso

With `-o`, the destination folder must already exist.

## Native backend

Release packages include:

    Hakamiq.Cso.Native.dll

Keep it next to the executables.

The native backend adds zlib and libdeflate raw-Deflate candidates. Managed Deflate remains the fallback. Zopfli is native-only and opt-in through `--zopfli`.

Quick check:

    .\hakamiq-cso.exe native-info

## JSON output

Add `--json` when another program needs structured output.

Examples:

    .\hakamiq-cso.exe verify ".\game.cso" --json
    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256 --json
    .\hakamiq-cso.exe compress ".\game.iso" --measure --profile smallest --json

Compression and measure JSON include `schemaVersion`, `command`, `mode`, `success`, `options`, `metrics`, and `error` when the command fails.

Profile object example:

    {
      "profile": {
        "name": "smallest",
        "fast": false,
        "level": 9
      }
    }

Invalid profile values return a clear argument error. Conflicting profile options use the same contract in JSON mode and a short message in text mode.

## Checksums

Release packages include `SHA256SUMS.txt`.

Use it when you want to confirm the downloaded files are unchanged.

## Third-party notices

`THIRD_PARTY_NOTICES.md` lists native compression components and licenses, including Zopfli, zlib, and libdeflate.