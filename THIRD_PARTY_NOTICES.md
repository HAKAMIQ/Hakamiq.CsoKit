# Third-Party Notices

Hakamiq CsoKit includes third-party source code and statically linked native compression components.

## zlib

- Component: zlib compression library
- Upstream project: https://github.com/madler/zlib
- Build intake: CMake FetchContent, tag `v1.3.2`
- License: zlib License

zlib is used for raw-Deflate candidate trials with `Z_DEFAULT_STRATEGY`, `Z_FILTERED`, `Z_HUFFMAN_ONLY`, and `Z_RLE`.

## libdeflate

- Component: libdeflate
- Upstream project: https://github.com/ebiggers/libdeflate
- Build intake: CMake FetchContent, tag `v1.25`
- License: MIT License

libdeflate is used for raw-Deflate candidate trials at levels 1, 6, 9, and 12.

## Zopfli

- Component: Zopfli Compression Algorithm
- Upstream project: https://github.com/google/zopfli
- Local source path: `native/third_party/zopfli`
- License: Apache License, Version 2.0
- Full license text: `native/third_party/zopfli/COPYING`

Zopfli is used only for explicit `--zopfli` raw-Deflate compression trials. Normal compression profiles remain available without asking the user to enable Zopfli.
