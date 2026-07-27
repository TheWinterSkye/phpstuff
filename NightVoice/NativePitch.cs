using System;
using System.Runtime.InteropServices;

namespace NightVoice;

internal sealed class NativePitch : IDisposable
{
    private IntPtr handle;

    public NativePitch(int sampleRate, int quality)
    {
        handle = NvPitchCreate(sampleRate, quality);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("The high-quality pitch engine could not start.");
    }

    public int LatencySamples => handle == IntPtr.Zero ? 0 : Math.Max(0, NvPitchLatency(handle));

    public void Configure(float pitchSemitones, float formantSemitones, bool preserveFormants, float formantBaseHz)
    {
        ThrowIfDisposed();
        if (NvPitchConfigure(handle, pitchSemitones, formantSemitones, preserveFormants ? 1 : 0, formantBaseHz) == 0)
            throw new InvalidOperationException("The pitch engine rejected its settings.");
    }

    public void Process(float[] input, float[] output, int sampleCount)
    {
        ThrowIfDisposed();
        if (sampleCount <= 0) return;
        if (NvPitchProcess(handle, input, output, sampleCount) == 0)
            throw new InvalidOperationException("The pitch engine could not process audio.");
    }

    public void Reset()
    {
        if (handle != IntPtr.Zero) NvPitchReset(handle);
    }

    private void ThrowIfDisposed()
    {
        if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(NativePitch));
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero) return;
        NvPitchDestroy(handle);
        handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    ~NativePitch()
    {
        if (handle != IntPtr.Zero) NvPitchDestroy(handle);
    }

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvPitchCreate(int sampleRate, int quality);

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NvPitchDestroy(IntPtr handle);

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NvPitchReset(IntPtr handle);

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvPitchConfigure(IntPtr handle, float pitchSemitones, float formantSemitones, int preserveFormants, float formantBaseHz);

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvPitchProcess(IntPtr handle, [In] float[] input, [Out] float[] output, int sampleCount);

    [DllImport("NightPitch.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvPitchLatency(IntPtr handle);
}
