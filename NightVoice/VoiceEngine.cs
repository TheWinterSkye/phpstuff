using System;
using NAudio.Dsp;

namespace NightVoice;

internal sealed class VoiceProcessor : IDisposable
{
    private readonly object sync = new();
    private readonly int sampleRate;
    private VoiceSettings settings = new();
    private NativePitch pitch;
    private int quality;

    private BiQuadFilter lowShelf;
    private BiQuadFilter highShelf;
    private BiQuadFilter telephoneHighPass;
    private BiQuadFilter telephoneLowPass;

    private float[] dryBlock = Array.Empty<float>();
    private float[] preBlock = Array.Empty<float>();
    private float[] shiftedBlock = Array.Empty<float>();

    private readonly float[] echoBuffer;
    private readonly float[] reverbA;
    private readonly float[] reverbB;
    private readonly float[] chorusBuffer;
    private readonly DelayLine dryDelay;
    private int echoPos;
    private int revAPos;
    private int revBPos;
    private int chorusPos;
    private double lfoPhase;
    private double robotPhase;
    private float gateEnvelope;
    private float gateGain = 1f;
    private readonly Random random = new();
    private bool disposed;

    public VoiceProcessor(int sampleRate, int initialQuality)
    {
        this.sampleRate = sampleRate;
        quality = Math.Clamp(initialQuality, 0, 2);
        pitch = new NativePitch(sampleRate, quality);
        lowShelf = BiQuadFilter.LowShelf(sampleRate, 180, 0.8f, 0);
        highShelf = BiQuadFilter.HighShelf(sampleRate, 3500, 0.8f, 0);
        telephoneHighPass = BiQuadFilter.HighPassFilter(sampleRate, 320, 0.8f);
        telephoneLowPass = BiQuadFilter.LowPassFilter(sampleRate, 3200, 0.8f);
        echoBuffer = new float[sampleRate * 2];
        reverbA = new float[(int)(sampleRate * 0.083)];
        reverbB = new float[(int)(sampleRate * 0.127)];
        chorusBuffer = new float[(int)(sampleRate * 0.08)];
        dryDelay = new DelayLine(sampleRate);
    }

    public int LatencyMilliseconds
    {
        get
        {
            lock (sync)
                return (int)Math.Round(pitch.LatencySamples * 1000.0 / sampleRate);
        }
    }

    public void Configure(VoiceSettings newSettings)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            int requestedQuality = Math.Clamp(newSettings.Quality, 0, 2);
            if (requestedQuality != quality)
            {
                var replacement = new NativePitch(sampleRate, requestedQuality);
                pitch.Dispose();
                pitch = replacement;
                quality = requestedQuality;
                dryDelay.Clear();
            }

            settings = newSettings;
            lowShelf = BiQuadFilter.LowShelf(sampleRate, 180, 0.8f, settings.BassDb);
            highShelf = BiQuadFilter.HighShelf(sampleRate, 3500, 0.8f, settings.TrebleDb);
            pitch.Configure(
                settings.PitchSemitones,
                settings.FormantSemitones,
                settings.PreserveFormants,
                settings.FormantBaseHz);
        }
    }

    public float Process(short[] samples, int count)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            EnsureCapacity(count);

            float gateThreshold = MathF.Pow(10f, settings.GateDb / 20f);
            for (int i = 0; i < count; i++)
            {
                float dry = samples[i] / 32768f;
                dryBlock[i] = dry;

                float magnitude = Math.Abs(dry);
                gateEnvelope = Math.Max(magnitude, gateEnvelope * 0.9955f);
                float targetGate = gateEnvelope >= gateThreshold
                    ? 1f
                    : MathF.Pow(Math.Clamp(gateEnvelope / Math.Max(gateThreshold, 0.000001f), 0f, 1f), 2f);
                float gateSpeed = targetGate > gateGain ? 0.10f : 0.0025f;
                gateGain += (targetGate - gateGain) * gateSpeed;

                float x = dry * gateGain;
                x = lowShelf.Transform(x);
                x = highShelf.Transform(x);
                preBlock[i] = x;
            }

            bool usePitch = Math.Abs(settings.PitchSemitones) > 0.001f || Math.Abs(settings.FormantSemitones) > 0.001f;
            if (usePitch)
                pitch.Process(preBlock, shiftedBlock, count);
            else
                Array.Copy(preBlock, shiftedBlock, count);

            int dryLatency = usePitch ? pitch.LatencySamples : 0;
            float peak = 0f;
            string mode = settings.Mode;

            for (int i = 0; i < count; i++)
            {
                float dry = dryDelay.Process(dryBlock[i], dryLatency);
                float x = shiftedBlock[i];

                if (mode is "Telephone" or "Space Radio")
                {
                    x = telephoneHighPass.Transform(x);
                    x = telephoneLowPass.Transform(x);
                }

                if (mode == "Robot")
                {
                    robotPhase += 2 * Math.PI * 72 / sampleRate;
                    if (robotPhase > 2 * Math.PI) robotPhase -= 2 * Math.PI;
                    x *= (float)Math.Sin(robotPhase);
                }
                else if (mode == "Android")
                {
                    robotPhase += 2 * Math.PI * 34 / sampleRate;
                    if (robotPhase > 2 * Math.PI) robotPhase -= 2 * Math.PI;
                    x = x * 0.80f + x * (float)Math.Sin(robotPhase) * 0.28f;
                }
                else if (mode == "Dark Whisper")
                {
                    float noise = ((float)random.NextDouble() * 2f - 1f) * Math.Min(0.10f, Math.Abs(x) * 1.5f);
                    x = x * 0.58f + noise;
                }

                if (settings.Drive > 0)
                {
                    float gain = 1f + settings.Drive * 9f;
                    x = MathF.Tanh(x * gain) / Math.Max(0.1f, MathF.Tanh(gain * 0.75f));
                }

                if (settings.Chorus > 0) x = ApplyChorus(x, settings.Chorus);
                if (settings.Echo > 0) x = ApplyEcho(x, settings.Echo);
                if (settings.Reverb > 0) x = ApplyReverb(x, settings.Reverb);

                x = dry * (1f - settings.Mix) + x * settings.Mix;
                x = MathF.Tanh(x * 1.05f) * 0.94f;
                peak = Math.Max(peak, Math.Abs(x));
                samples[i] = (short)Math.Clamp((int)(x * 32767f), short.MinValue, short.MaxValue);
            }

            return peak;
        }
    }

    private void EnsureCapacity(int count)
    {
        if (dryBlock.Length >= count) return;
        dryBlock = new float[count];
        preBlock = new float[count];
        shiftedBlock = new float[count];
    }

    private float ApplyEcho(float x, float amount)
    {
        int delay = (int)(sampleRate * (0.13 + amount * 0.27));
        int read = (echoPos - delay + echoBuffer.Length) % echoBuffer.Length;
        float delayed = echoBuffer[read];
        echoBuffer[echoPos] = x + delayed * (0.16f + amount * 0.42f);
        echoPos = (echoPos + 1) % echoBuffer.Length;
        return x + delayed * amount * 0.50f;
    }

    private float ApplyReverb(float x, float amount)
    {
        float a = reverbA[revAPos];
        float b = reverbB[revBPos];
        reverbA[revAPos] = x + a * 0.73f;
        reverbB[revBPos] = x + b * 0.69f + a * 0.12f;
        revAPos = (revAPos + 1) % reverbA.Length;
        revBPos = (revBPos + 1) % reverbB.Length;
        return x + (a + b) * amount * 0.22f;
    }

    private float ApplyChorus(float x, float amount)
    {
        chorusBuffer[chorusPos] = x;
        lfoPhase += 2 * Math.PI * 0.34 / sampleRate;
        if (lfoPhase > 2 * Math.PI) lfoPhase -= 2 * Math.PI;
        double delay = sampleRate * (0.012 + 0.006 * Math.Sin(lfoPhase));
        double readPosition = chorusPos - delay;
        while (readPosition < 0) readPosition += chorusBuffer.Length;
        int i0 = (int)readPosition;
        int i1 = (i0 + 1) % chorusBuffer.Length;
        float fraction = (float)(readPosition - i0);
        float delayed = chorusBuffer[i0] * (1 - fraction) + chorusBuffer[i1] * fraction;
        chorusPos = (chorusPos + 1) % chorusBuffer.Length;
        return x + delayed * amount * 0.42f;
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(VoiceProcessor));
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            pitch.Dispose();
        }
    }

    private sealed class DelayLine
    {
        private readonly float[] buffer;
        private int position;

        public DelayLine(int maximumDelay) => buffer = new float[Math.Max(2, maximumDelay + 1)];

        public float Process(float input, int delaySamples)
        {
            delaySamples = Math.Clamp(delaySamples, 0, buffer.Length - 1);
            buffer[position] = input;
            int read = position - delaySamples;
            if (read < 0) read += buffer.Length;
            float output = buffer[read];
            position++;
            if (position >= buffer.Length) position = 0;
            return output;
        }

        public void Clear()
        {
            Array.Clear(buffer);
            position = 0;
        }
    }
}
