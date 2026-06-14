#define HAKAMIQ_CSO_NATIVE_EXPORTS

#include "hakamiq_cso_native.h"

extern "C"
{
    int32_t hakamiq_cso_native_probe()
    {
        return HAKAMIQ_CSO_NATIVE_OK;
    }

    int32_t hakamiq_cso_native_get_version(
        HakamiqCsoNativeVersion* version
    )
    {
        if (version == nullptr)
        {
            return HAKAMIQ_CSO_NATIVE_INVALID_ARGUMENT;
        }

        version->abi_version = 1;
        version->major = 0;
        version->minor = 5;
        version->patch = 0;

        return HAKAMIQ_CSO_NATIVE_OK;
    }
}
