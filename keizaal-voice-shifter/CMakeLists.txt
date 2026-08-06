cmake_minimum_required(VERSION 3.24)
project(KeizaalVoiceShifter VERSION 1.0.0 LANGUAGES CXX)

if(NOT WIN32)
    message(FATAL_ERROR "Keizaal Voice Shifter is a Windows-only application.")
endif()

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

if(NOT DEFINED MINIAUDIO_DIR)
    set(MINIAUDIO_DIR "${CMAKE_SOURCE_DIR}/third_party/miniaudio")
endif()
if(NOT DEFINED SIGNALSMITH_DIR)
    set(SIGNALSMITH_DIR "${CMAKE_SOURCE_DIR}/third_party/signalsmith-stretch")
endif()

if(NOT EXISTS "${MINIAUDIO_DIR}/miniaudio.h")
    message(FATAL_ERROR "miniaudio.h was not found at ${MINIAUDIO_DIR}")
endif()
if(NOT EXISTS "${SIGNALSMITH_DIR}/signalsmith-stretch.h")
    message(FATAL_ERROR "signalsmith-stretch.h was not found at ${SIGNALSMITH_DIR}")
endif()

add_executable(KeizaalVoiceShifter WIN32
    src/main.cpp
    src/app.rc
)

target_include_directories(KeizaalVoiceShifter PRIVATE
    "${MINIAUDIO_DIR}"
    "${SIGNALSMITH_DIR}"
)

target_compile_definitions(KeizaalVoiceShifter PRIVATE
    UNICODE
    _UNICODE
    NOMINMAX
    WIN32_LEAN_AND_MEAN
)

if(MSVC)
    target_compile_options(KeizaalVoiceShifter PRIVATE /O2 /GL /EHsc /permissive- /W4 /wd4100 /wd4244 /wd4267)
    target_link_options(KeizaalVoiceShifter PRIVATE /LTCG /OPT:REF /OPT:ICF)
endif()

target_link_libraries(KeizaalVoiceShifter PRIVATE
    comctl32
    ole32
    uuid
    avrt
    winmm
    uxtheme
    dwmapi
    shlwapi
    shell32
)

set_target_properties(KeizaalVoiceShifter PROPERTIES
    OUTPUT_NAME "KeizaalVoiceShifter"
)
