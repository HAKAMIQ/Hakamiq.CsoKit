# Native Build Notes

The managed CLI remains the safe fallback path. A missing native backend must not break managed detect, analyze, verify, repair, decompress, or compress flows.

## Online build

The native project may need network access during first configure when CMake resolves zlib or libdeflate through FetchContent or equivalent dependency acquisition. Run:

```powershell
scripts/Build-Native.ps1 -Configuration Release -Platform x64
hakamiq-cso native-info
```

`native-info` must report availability honestly for zlib, libdeflate, Zopfli, and any unavailable native codec.

## Offline limitation

A fully offline native configure is not guaranteed unless dependencies are already cached or supplied by the developer environment. If native configure fails due to network/dependency resolution, use the managed-only CLI path and keep native availability reported as unavailable rather than pretending a backend exists.

## Managed-only workflow

```powershell
dotnet build Hakamiq.CsoKit.slnx --no-restore
dotnet test Hakamiq.CsoKit.slnx --no-build --no-restore
hakamiq-cso native-info
```

Managed Deflate, container decode, PSP ISO analysis, JSON diagnostics, and repair safety remain available without the native DLL.
