#pragma once

#include <cstdint>
#include <cstddef>

#if defined(_WIN32)
    #if defined(HAKAMIQ_CSO_NATIVE_EXPORTS)
        #define HAKAMIQ_CSO_API __declspec(dllexport)
    #else
        #define HAKAMIQ_CSO_API __declspec(dllimport)
    #endif
#else
    #define HAKAMIQ_CSO_API
#endif

extern "C"
{
    enum HakamiqCsoNativeStatus : int32_t
    {
        HAKAMIQ_CSO_NATIVE_OK = 0,
        HAKAMIQ_CSO_NATIVE_UNSUPPORTED_PLATFORM = 1,
        HAKAMIQ_CSO_NATIVE_INVALID_ARGUMENT = 2,
        HAKAMIQ_CSO_NATIVE_OUTPUT_TOO_SMALL = 3,
        HAKAMIQ_CSO_NATIVE_CODEC_UNAVAILABLE = 4,
        HAKAMIQ_CSO_NATIVE_INTERNAL_ERROR = 100
    };

    enum HakamiqCsoCodec : int32_t
    {
        HAKAMIQ_CSO_CODEC_ZLIB_DEFAULT = 1,
        HAKAMIQ_CSO_CODEC_ZLIB_FILTERED = 2,
        HAKAMIQ_CSO_CODEC_ZLIB_HUFFMAN_ONLY = 3,
        HAKAMIQ_CSO_CODEC_ZLIB_RLE = 4,

        HAKAMIQ_CSO_CODEC_LIBDEFLATE = 10,
        HAKAMIQ_CSO_CODEC_ZOPFLI = 20,
        HAKAMIQ_CSO_CODEC_7Z_DEFLATE = 30
    };

    struct HakamiqCsoNativeVersion
    {
        uint32_t abi_version;
        uint32_t major;
        uint32_t minor;
        uint32_t patch;
    };

    struct HakamiqCsoNativeCapabilities
    {
        uint32_t abi_version;
        uint32_t has_zlib;
        uint32_t has_libdeflate;
        uint32_t has_zopfli;
        uint32_t has_7z_deflate;
        uint32_t has_lz4;
    };

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_probe();

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_get_version(
        HakamiqCsoNativeVersion* version
    );

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_get_capabilities(
        HakamiqCsoNativeCapabilities* capabilities
    );

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_deflate_raw(
        int32_t codec,
        int32_t level,
        int32_t strategy,
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_inflate_raw(
        const uint8_t* input,
        size_t input_size,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_deflate_zopfli(
        const uint8_t* input,
        size_t input_size,
        int32_t iterations,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    );
}
