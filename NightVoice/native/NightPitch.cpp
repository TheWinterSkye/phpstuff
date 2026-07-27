#include <algorithm>
#include <cmath>
#include <cstdint>
#include <memory>
#include "signalsmith-stretch.h"

#if defined(_WIN32)
#define NV_EXPORT extern "C" __declspec(dllexport)
#define NV_CALL __cdecl
#else
#define NV_EXPORT extern "C"
#define NV_CALL
#endif

namespace {
struct PitchState {
    int sampleRate;
    signalsmith::stretch::SignalsmithStretch<float> stretch;

    explicit PitchState(int rate, int quality) : sampleRate(rate) {
        // Voice-oriented configurations. Larger windows are smoother but add latency.
        switch (quality) {
            case 0: stretch.configure(1, 2048, 512, false); break;
            case 2: stretch.configure(1, 4096, 1024, true); break;
            default: stretch.configure(1, 3072, 768, true); break;
        }
        stretch.setTransposeSemitones(0.0f, 8000.0f / static_cast<float>(sampleRate));
        stretch.setFormantBase(180.0f / static_cast<float>(sampleRate));
        stretch.setFormantSemitones(0.0f, true);
    }
};
}

NV_EXPORT void* NV_CALL nv_pitch_create(int sampleRate, int quality) {
    try {
        if (sampleRate < 8000) return nullptr;
        quality = std::clamp(quality, 0, 2);
        return new PitchState(sampleRate, quality);
    } catch (...) {
        return nullptr;
    }
}

NV_EXPORT void NV_CALL nv_pitch_destroy(void* handle) {
    delete static_cast<PitchState*>(handle);
}

NV_EXPORT void NV_CALL nv_pitch_reset(void* handle) {
    if (!handle) return;
    try {
        static_cast<PitchState*>(handle)->stretch.reset();
    } catch (...) {
    }
}

NV_EXPORT int NV_CALL nv_pitch_configure(
    void* handle,
    float pitchSemitones,
    float formantSemitones,
    int preserveFormants,
    float formantBaseHz) {
    if (!handle) return 0;
    try {
        auto* state = static_cast<PitchState*>(handle);
        pitchSemitones = std::clamp(pitchSemitones, -24.0f, 24.0f);
        formantSemitones = std::clamp(formantSemitones, -12.0f, 12.0f);
        formantBaseHz = std::clamp(formantBaseHz, 70.0f, 400.0f);
        state->stretch.setTransposeSemitones(
            pitchSemitones,
            8000.0f / static_cast<float>(state->sampleRate));
        state->stretch.setFormantBase(formantBaseHz / static_cast<float>(state->sampleRate));
        state->stretch.setFormantSemitones(formantSemitones, preserveFormants != 0);
        return 1;
    } catch (...) {
        return 0;
    }
}

NV_EXPORT int NV_CALL nv_pitch_process(
    void* handle,
    const float* input,
    float* output,
    int sampleCount) {
    if (!handle || !input || !output || sampleCount <= 0) return 0;
    try {
        auto* state = static_cast<PitchState*>(handle);
        float* inputChannel = const_cast<float*>(input);
        float* outputChannel = output;
        float* inputChannels[1] = { inputChannel };
        float* outputChannels[1] = { outputChannel };
        state->stretch.process(inputChannels, sampleCount, outputChannels, sampleCount);
        return 1;
    } catch (...) {
        std::fill(output, output + sampleCount, 0.0f);
        return 0;
    }
}

NV_EXPORT int NV_CALL nv_pitch_latency(void* handle) {
    if (!handle) return 0;
    try {
        auto* state = static_cast<PitchState*>(handle);
        return state->stretch.inputLatency() + state->stretch.outputLatency();
    } catch (...) {
        return 0;
    }
}
