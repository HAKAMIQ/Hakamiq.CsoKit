#pragma once

#include <cstdint>

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
        HAKAMIQ_CSO_NATIVE_INTERNAL_ERROR = 100
    };

    struct HakamiqCsoNativeVersion
    {
        uint32_t abi_version;
        uint32_t major;
        uint32_t minor;
        uint32_t patch;
    };

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_probe();

    HAKAMIQ_CSO_API int32_t hakamiq_cso_native_get_version(
        HakamiqCsoNativeVersion* version
    );
}
