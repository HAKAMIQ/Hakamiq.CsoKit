# Hakamiq CsoKit CLI Reference

This file documents the command-line tool:

    hakamiq-cso.exe

The desktop app is intended for common interactive workflows. The CLI is intended for scripts, automation, diagnostics, and advanced options.

## Basic commands

Show help:

    .\hakamiq-cso.exe --help

Show version:

    .\hakamiq-cso.exe --version

Show native backend status:

    .\hakamiq-cso.exe native-info

List codec availability:

    .\hakamiq-cso.exe codecs

## Info

Show CSO metadata:

    .\hakamiq-cso.exe info ".\game.cso"

## Verify

Verify a CSO file:

    .\hakamiq-cso.exe verify ".\game.cso"

Run deep block verification:

    .\hakamiq-cso.exe verify ".\game.cso" --deep

Run deep verification and calculate SHA256:

    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256

Return structured output:

    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256 --json

## Detect

Detect supported container or image format:

    .\hakamiq-cso.exe detect ".\game.iso"
    .\hakamiq-cso.exe detect ".\game.cso" --json

Supported detection includes ISO, CSO1, CSO2, ZSO, DAX, and unknown input.

## Analyze

Analyze PSP ISO structure without modifying files:

    .\hakamiq-cso.exe analyze ".\game.iso" --psp
    .\hakamiq-cso.exe analyze ".\game.iso" --psp --json

## Compress

Compress ISO to CSO:

    .\hakamiq-cso.exe compress ".\game.iso"

Use an explicit output file:

    .\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso"

Overwrite an existing output file:

    .\hakamiq-cso.exe compress ".\game.iso" -o ".\game.cso" --force

Measure estimated CSO size without writing output:

    .\hakamiq-cso.exe compress ".\game.iso" --measure

Use JSON output:

    .\hakamiq-cso.exe compress ".\game.iso" --profile fast --json

## Compression profiles

Available profiles:

    game-safe
    compat
    fast
    smallest
    archive-smallest

The default profile is game-safe.

game-safe writes CSO1, keeps the default block size at 2048 bytes, uses raw Deflate candidates, and enables deep verification after compression.

fast is for speed.

smallest tries stronger candidates but does not enable Zopfli unless --zopfli is explicit.

archive-smallest is for archival experiments and may reduce compatibility when paired with larger block sizes.

Use a profile:

    .\hakamiq-cso.exe compress ".\game.iso" --profile fast

The short alias is equivalent to --profile fast:

    .\hakamiq-cso.exe compress ".\game.iso" --fast

Do not combine --fast with another explicit profile.

## Compression options

Set compression worker count:

    .\hakamiq-cso.exe compress ".\game.iso" --threads 8

Set block size:

    .\hakamiq-cso.exe compress ".\game.iso" --block 16K

Enable optional native Zopfli trials:

    .\hakamiq-cso.exe compress ".\game.iso" --zopfli

Print codec winner metrics:

    .\hakamiq-cso.exe compress ".\game.iso" --codec-report

Block size accepts byte values and K or M suffixes. It must be at least 2048 and must be a power of two. Larger blocks can improve compression but may reduce compatibility or increase random-read latency.

## Decompress

Decompress CSO to ISO:

    .\hakamiq-cso.exe decompress ".\game.cso"

Use an explicit output file:

    .\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso"

Overwrite output:

    .\hakamiq-cso.exe decompress ".\game.cso" -o ".\game.iso" --force

## Repair

Repair or normalize readable input into game-safe CSO1:

    .\hakamiq-cso.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Readable input can include ISO, CSO1, ZSO, DAX, and supported CSO2. Hakamiq CsoKit writes CSO1 by default.

Safe repair does not invent missing data. If a compressed block is corrupt or the source is incomplete, the command fails with a diagnosis such as ReDumpRequired and does not write a partial output file.

Padding a non-2048-aligned ISO is only done when explicit repair behavior is requested.

## Output naming

If the default output already exists, Hakamiq CsoKit writes a safe converted name instead, then keeps counting upward.

Example:

    game.cso
    game - Hakamiq Converted.cso
    game - Hakamiq Converted 2.cso

Hakamiq CsoKit does not create output folders automatically. If -o is used, the destination folder must already exist.

## Native backend

The release package includes:

    Hakamiq.Cso.Native.dll

Keep it next to the executables.

Native zlib and libdeflate are used only as safe raw-Deflate candidates. The managed Deflate fallback remains available. The --zopfli option requires the native backend and remains opt-in.

Check native status:

    .\hakamiq-cso.exe native-info

## JSON output

Add --json when another program or script needs structured output.

Examples:

    .\hakamiq-cso.exe verify ".\game.cso" --json
    .\hakamiq-cso.exe verify ".\game.cso" --deep --sha256 --json
    .\hakamiq-cso.exe compress ".\game.iso" --measure --profile smallest --json

Compression and measure JSON output includes schemaVersion, command, mode, success, options, metrics, and error when a command fails.

Example profile object:

    {
      "profile": {
        "name": "smallest",
        "fast": false,
        "level": 9
      }
    }

Invalid profile values return a clear argument error. Conflicting profile options return the same argument error contract in JSON mode and a concise message in text mode.

## Checksums

SHA256SUMS.txt is included in release packages.

Use it to check that downloaded release files were not changed or corrupted after release.

## Third-party notices

THIRD_PARTY_NOTICES.md documents third-party native compression components and licenses, including Zopfli, zlib, and libdeflate.