using System.Runtime.InteropServices;

namespace Whisper.Core;

/// <summary>Win32 waveOut 双缓冲正弦波合成器，AOT 兼容</summary>
public unsafe class WaveOutSynth : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded, dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext; public uint reserved; }

    private const int WAVE_MAPPER = -1;
    private const uint WHDR_DONE = 1;
    private const int CALLBACK_NULL = 0;

    [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hwo, int uDeviceID, WAVEFORMATEX* pwfx, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);
    [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr hwo, WAVEHDR* pwh, uint cbwh);
    [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr hwo, WAVEHDR* pwh, uint cbwh);
    [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr hwo, WAVEHDR* pwh, uint cbwh);
    [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr hwo);
    [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr hwo);

    private IntPtr _hwo;
    private readonly int _sampleRate, _bufferSamples;
    private readonly short[] _bufA, _bufB;
    private GCHandle _ghA, _ghB;
    private readonly IntPtr _hdrA, _hdrB;
    private WAVEHDR* _headerA, _headerB;
    private int _toggle;  // 0 → 写 A, 1 → 写 B

    public WaveOutSynth(int sampleRate = 44100, int bufferMs = 200)
    {
        _sampleRate = sampleRate;
        _bufferSamples = sampleRate * bufferMs / 1000;
        _bufA = new short[_bufferSamples]; _bufB = new short[_bufferSamples];
        _ghA = GCHandle.Alloc(_bufA, GCHandleType.Pinned);
        _ghB = GCHandle.Alloc(_bufB, GCHandleType.Pinned);

        WAVEFORMATEX fmt = new() { wFormatTag = 1, nChannels = 1, nSamplesPerSec = (uint)sampleRate,
            wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = (uint)(sampleRate * 2) };

        _hdrA = Marshal.AllocHGlobal(sizeof(WAVEHDR));
        _hdrB = Marshal.AllocHGlobal(sizeof(WAVEHDR));
        _headerA = (WAVEHDR*)_hdrA;
        _headerB = (WAVEHDR*)_hdrB;
        *_headerA = new WAVEHDR { lpData = _ghA.AddrOfPinnedObject(), dwBufferLength = (uint)(_bufferSamples * 2), dwFlags = WHDR_DONE };
        *_headerB = new WAVEHDR { lpData = _ghB.AddrOfPinnedObject(), dwBufferLength = (uint)(_bufferSamples * 2), dwFlags = WHDR_DONE };

        if (waveOutOpen(out _hwo, WAVE_MAPPER, &fmt, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL) != 0)
            throw new Exception("waveOutOpen failed");

        waveOutPrepareHeader(_hwo, _headerA, (uint)sizeof(WAVEHDR));
        waveOutPrepareHeader(_hwo, _headerB, (uint)sizeof(WAVEHDR));
    }

    public void Play(Span<float> frequencies, Span<float> amplitudes)
    {
        if (_hwo == IntPtr.Zero) return;

        var hdr = _toggle == 0 ? _headerA : _headerB;
        var buf = _toggle == 0 ? _bufA : _bufB;

        // 双缓冲：上一个写完立即写下一个，waveOut 自动连续播放
        int waited = 0;
        while ((hdr->dwFlags & WHDR_DONE) == 0 && waited < 10)
            { Thread.Sleep(1); waited++; }
        if ((hdr->dwFlags & WHDR_DONE) == 0) return; // 跳过，下轮再写

        int n = Math.Min(frequencies.Length, amplitudes.Length);

        for (int i = 0; i < _bufferSamples; i++)
        {
            float s = 0;
            float t = i / (float)_sampleRate;
            for (int f = 0; f < n; f++)
            {
                if (amplitudes[f] < 0.001f) continue;
                float osc = f < 4
                    ? (2f * MathF.Abs(2f * (t * frequencies[f] % 1f) - 1f) - 1f)   // 三角波 (ch 0-3)
                    : (MathF.Sin(2f * MathF.PI * frequencies[f] * t) > 0 ? 1f : -1f); // 方波 (ch 4-15)
                s += amplitudes[f] * osc;
            }
            buf[i] = (short)Math.Clamp(s * 12000f, short.MinValue, short.MaxValue);
        }

        waveOutWrite(_hwo, hdr, (uint)sizeof(WAVEHDR));
        _toggle ^= 1;  // 下一次写另一个缓冲
    }

    public void Stop()
    {
        if (_hwo != IntPtr.Zero) waveOutReset(_hwo);
    }

    public void Dispose()
    {
        if (_hwo != IntPtr.Zero)
        {
            waveOutReset(_hwo);
            waveOutUnprepareHeader(_hwo, _headerA, (uint)sizeof(WAVEHDR));
            waveOutUnprepareHeader(_hwo, _headerB, (uint)sizeof(WAVEHDR));
            waveOutClose(_hwo);
            _hwo = IntPtr.Zero;
        }
        if (_ghA.IsAllocated) _ghA.Free();
        if (_ghB.IsAllocated) _ghB.Free();
        if (_hdrA != IntPtr.Zero) Marshal.FreeHGlobal(_hdrA);
        if (_hdrB != IntPtr.Zero) Marshal.FreeHGlobal(_hdrB);
    }
}
