#define HAKAMIQ_CSO_NATIVE_EXPORTS

#include "hakamiq_cso_native.h"

#include <cstdlib>
#include <cstring>

#include "zopfli/zopfli.h"

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

    int32_t hakamiq_cso_native_deflate_zopfli(
        const uint8_t* input,
        size_t input_size,
        int32_t iterations,
        uint8_t* output,
        size_t output_capacity,
        size_t* output_size
    )
    {
        if ((input == nullptr && input_size != 0) ||
            output == nullptr ||
            output_size == nullptr ||
            iterations < 1 ||
            iterations > 100)
        {
            return HAKAMIQ_CSO_NATIVE_INVALID_ARGUMENT;
        }

        ZopfliOptions options;
        ZopfliInitOptions(&options);
        options.numiterations = iterations;
        options.blocksplitting = 1;
        options.blocksplittinglast = 1;
        options.blocksplittingmax = 15;

        unsigned char* zopfli_output = nullptr;
        size_t zopfli_output_size = 0;

        ZopfliCompress(
            &options,
            ZOPFLI_FORMAT_DEFLATE,
            input,
            input_size,
            &zopfli_output,
            &zopfli_output_size);

        *output_size = zopfli_output_size;

        if (zopfli_output == nullptr && zopfli_output_size != 0)
        {
            return HAKAMIQ_CSO_NATIVE_INTERNAL_ERROR;
        }

        if (zopfli_output_size > output_capacity)
        {
            std::free(zopfli_output);
            return HAKAMIQ_CSO_NATIVE_OUTPUT_TOO_SMALL;
        }

        if (zopfli_output_size > 0)
        {
            std::memcpy(output, zopfli_output, zopfli_output_size);
        }

        std::free(zopfli_output);

        return HAKAMIQ_CSO_NATIVE_OK;
    }
}
