cmake_minimum_required(VERSION 3.24)
project(NightPitch LANGUAGES CXX)

include(FetchContent)
FetchContent_Declare(
    signalsmith_stretch
    GIT_REPOSITORY https://github.com/Signalsmith-Audio/signalsmith-stretch.git
    GIT_TAG 57b93f4e9206a089a45387eaa39bdc9f310d3308
    GIT_SHALLOW FALSE
)
FetchContent_MakeAvailable(signalsmith_stretch)

add_library(NightPitch SHARED NightPitch.cpp)
target_compile_features(NightPitch PRIVATE cxx_std_17)
target_link_libraries(NightPitch PRIVATE signalsmith-stretch)

if(MSVC)
    target_compile_options(NightPitch PRIVATE /O2 /EHsc)
    set_property(TARGET NightPitch PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

set_target_properties(NightPitch PROPERTIES
    OUTPUT_NAME "NightPitch"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_SOURCE_DIR}/bin"
)
