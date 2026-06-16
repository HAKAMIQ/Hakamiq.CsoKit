# Third-Party Notices

Hakamiq CsoKit includes third-party source code for optional native compression backends.

## Zopfli

- Component: Zopfli Compression Algorithm
- Upstream project: https://github.com/google/zopfli
- Local source path: `native/third_party/zopfli`
- License: Apache License, Version 2.0
- Full license text: `native/third_party/zopfli/COPYING`

Zopfli is used only for explicit `--zopfli` raw-Deflate compression trials. Normal compression profiles remain available without asking the user to enable Zopfli.
