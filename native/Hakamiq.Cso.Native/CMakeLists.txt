cmake_minimum_required(VERSION 3.25)

project(Hakamiq.Cso.Native
    VERSION 0.5.0
    LANGUAGES C CXX
)

set(ZOPFLI_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../third_party/zopfli)
set(ZOPFLI_SOURCES
    ${ZOPFLI_ROOT}/src/zopfli/blocksplitter.c
    ${ZOPFLI_ROOT}/src/zopfli/cache.c
    ${ZOPFLI_ROOT}/src/zopfli/deflate.c
    ${ZOPFLI_ROOT}/src/zopfli/gzip_container.c
    ${ZOPFLI_ROOT}/src/zopfli/hash.c
    ${ZOPFLI_ROOT}/src/zopfli/katajainen.c
    ${ZOPFLI_ROOT}/src/zopfli/lz77.c
    ${ZOPFLI_ROOT}/src/zopfli/squeeze.c
    ${ZOPFLI_ROOT}/src/zopfli/tree.c
    ${ZOPFLI_ROOT}/src/zopfli/util.c
    ${ZOPFLI_ROOT}/src/zopfli/zlib_container.c
    ${ZOPFLI_ROOT}/src/zopfli/zopfli_lib.c
)

add_library(hakamiq_zopfli STATIC
    ${ZOPFLI_SOURCES}
)

target_include_directories(hakamiq_zopfli
    PUBLIC
        ${ZOPFLI_ROOT}/src
)

add_library(Hakamiq.Cso.Native SHARED
    src/hakamiq_cso_native.cpp
)

target_include_directories(Hakamiq.Cso.Native
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${ZOPFLI_ROOT}/src
)

target_link_libraries(Hakamiq.Cso.Native PRIVATE hakamiq_zopfli)

target_compile_features(Hakamiq.Cso.Native PRIVATE cxx_std_20)

if (MSVC)
    target_compile_options(Hakamiq.Cso.Native PRIVATE
        /W4
        /WX
        /permissive-
        /EHsc
        /utf-8
    )

    target_compile_definitions(Hakamiq.Cso.Native PRIVATE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
    )
endif()

set_target_properties(Hakamiq.Cso.Native PROPERTIES
    OUTPUT_NAME "Hakamiq.Cso.Native"
)
