# Keizaal Voice Shifter

A native Windows real-time voice pitch and formant shifter for Skyrim roleplay.

## What it does

- Captures a selected microphone or virtual-cable recording endpoint.
- Applies high-quality Signalsmith Stretch pitch shifting with independent formant control.
- Protects consonants and sibilants by blending a latency-aligned dry signal when speech is unvoiced.
- Includes gate, high-pass filter, warmth, presence, air, de-esser, compressor, output gain, and limiter controls.
- Sends the processed voice to a selected playback endpoint such as `CABLE Input` or `CABLE In 16ch`.
- Offers a separate toggleable headphone monitor output.
- Saves one custom preset in `%APPDATA%\KeizaalVoiceShifter\custom.ini`.
- Builds as one self-contained native `KeizaalVoiceShifter.exe` with no .NET or Python installation required.
- Targets 48 kHz shared-mode WASAPI for compatibility with the user's existing VB-Audio cable setup.

## Correct VB-CABLE routing

The names are backwards because one side is playback and the other is recording:

1. In this app, select the VB-CABLE **playback** endpoint: `CABLE Input` or `CABLE In 16ch`.
2. In Skyrim/Keizaal or Windows microphone settings, select the matching **recording** endpoint: `CABLE Output` or `CABLE Out 16ch`.
3. Select normal headphones for the monitor output. Do not select the same device for monitor and virtual output.

Use headphones before enabling voice monitoring to prevent feedback.

## Quality notes

The default Balanced engine is intended for live roleplay. Best Voice uses a longer analysis window and adds more delay. Extreme shifts can never sound fully natural with a pitch/formant shifter; the cleanest range is usually within about four semitones, with moderate formant adjustment and consonant protection enabled.

## Open-source components

- [Signalsmith Stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch), MIT License.
- [miniaudio](https://github.com/mackron/miniaudio), public domain or MIT No Attribution.

The application source is released under the MIT License.
