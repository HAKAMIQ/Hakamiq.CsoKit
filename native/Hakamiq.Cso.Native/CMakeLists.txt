cmake_minimum_required(VERSION 3.25)

project(Hakamiq.Cso.Native
    VERSION 0.5.0
    LANGUAGES CXX
)

add_library(Hakamiq.Cso.Native SHARED
    src/hakamiq_cso_native.cpp
)

target_include_directories(Hakamiq.Cso.Native
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

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
