using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.DirectX8;

namespace Whisper.Core;

public enum PlayMode { Sequential, SingleLoop, Random }

public class Player : IDisposable
{
    [DllImport("bass_fx", CallingConvention = CallingConvention.StdCall)]
    private static extern int BASS_FX_TempoCreate(int chan, uint flags);

    private int _handle;
    private int _sourceHandle;
    private float _volume = 0.8f;
    private float _speed = 1.0f;
    private bool _is8Bit;
    private WaveOutSynth? _synth;
    private float _saveVolume;  // 切回 BASS 时恢复音量
    private bool _suppressEnded;
    private SyncProcedure? _onEnded;  // 必须存字段防 GC

    public bool IsPlaying => _handle != 0 && Bass.ChannelIsActive(_handle) == PlaybackState.Playing;
    public bool IsPaused => _handle != 0 && Bass.ChannelIsActive(_handle) == PlaybackState.Paused;

    public TimeSpan Position
    {
        get => _handle == 0 ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_handle, Bass.ChannelGetPosition(_handle)));
        set
        {
            if (_handle != 0)
                Bass.ChannelSetPosition(_handle, Bass.ChannelSeconds2Bytes(_handle, value.TotalSeconds));
        }
    }

    public TimeSpan Duration => _handle == 0 ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_handle, Bass.ChannelGetLength(_handle)));

    public int SampleRate => _handle != 0 ? Bass.ChannelGetInfo(_handle).Frequency : 0;
    public int Channels => _handle != 0 ? Bass.ChannelGetInfo(_handle).Channels : 0;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            if (_handle != 0 && !_is8Bit) Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _volume);
        }
    }

    public float Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25f, 4f);
            if (_handle != 0)
                Bass.ChannelSetAttribute(_handle, ChannelAttribute.Tempo, (float)((_speed - 1.0) * 100));
        }
    }

    public string? CurrentFile { get; private set; }
    public PlayMode Mode { get; set; } = PlayMode.Sequential;

    public TimeSpan? LoopA { get; set; }
    public TimeSpan? LoopB { get; set; }

    public event Action? PlaybackEnded;

    public void Open(string path)
    {
        _suppressEnded = true;
        Close();
        _suppressEnded = false;

        _sourceHandle = Bass.CreateStream(path, 0, 0, BassFlags.Decode);
        if (_sourceHandle == 0) throw new Exception($"Cannot open file: {Bass.LastError}");

        // 创建 Tempo 流（变速不变调）
        _handle = BASS_FX_TempoCreate(_sourceHandle, 0u);
        if (_handle == 0) throw new Exception($"Cannot create tempo stream: {Bass.LastError}");

        Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _is8Bit ? 0 : _volume);
        if (Math.Abs(_speed - 1f) > 0.01f)
            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Tempo, (float)((_speed - 1.0) * 100));

        _onEnded = OnEnded;
        Bass.ChannelSetSync(_handle, SyncFlags.End, 0, _onEnded, IntPtr.Zero);

        CurrentFile = path;
        if (EqEnabled) ApplyEq();
        Bass.ChannelPlay(_handle);
    }

    public void Play()
    {
        if (_handle == 0) return;
        if (Bass.ChannelIsActive(_handle) != PlaybackState.Playing)
            Bass.ChannelPlay(_handle);
    }

    public void Pause()
    {
        if (_handle != 0) Bass.ChannelPause(_handle);
    }

    public void Stop()
    {
        _suppressEnded = true;
        _synth?.Stop();
        if (_handle != 0)
        {
            Bass.ChannelStop(_handle);
            Bass.ChannelSetPosition(_handle, 0);
        }
        _suppressEnded = false;
    }

    public void Close()
    {
        _suppressEnded = true;
        RemoveEq();
        _synth?.Stop();
        if (_handle != 0)
        {
            Bass.StreamFree(_handle);
            _handle = 0;
        }
        if (_sourceHandle != 0)
        {
            Bass.StreamFree(_sourceHandle);
            _sourceHandle = 0;
        }
        CurrentFile = null;
    }

    public void Tick()
    {
        if (_handle == 0) return;
        if (LoopA != null && LoopB != null && Position >= LoopB.Value)
            Position = LoopA.Value;

        if (_is8Bit && _synth != null && IsPlaying)
            UpdateAndPlaySynth();
    }

    private void UpdateAndPlaySynth()
    {
        if (_sourceHandle == 0 || _synth == null) return;
        Bass.ChannelGetData(_sourceHandle, _fftBuffer, (int)DataFlags.FFT2048);

        var binHz = 44100f / 2048f;
        Span<float> freqs = stackalloc float[16], amps = stackalloc float[16];

        // 4 柱三角波 (bass: bin 4-18, ~86-388Hz) + 12 柱方波 (mid/high: bin 19+, ~409Hz+)
        int ch = 0;
        for (int j = 4; j <= 18 && ch < 4; j++)
        {
            if (_fftBuffer[j] < 0.02f) continue;
            freqs[ch] = j * binHz;
            amps[ch] = Math.Clamp(_fftBuffer[j] * 2f, 0, 0.5f);
            ch++;
        }
        int sqStart = ch; ch = 4;
        for (int j = 19; j < 1024 && ch < 16; j++)
        {
            if (_fftBuffer[j] < 0.02f) continue;
            freqs[ch] = j * binHz;
            amps[ch] = Math.Clamp(_fftBuffer[j] * 2f, 0, 0.5f);
            ch++;
        }

        // 全静音时不播放
        bool any = false;
        for (int i = 0; i < 16; i++) if (amps[i] > 0.001f) { any = true; break; }
        if (!any) return;
        _synth.Play(freqs, amps);
    }

    private readonly float[] _fftBuffer = new float[2048];
    private int _fftBands = 16;

    /// <summary>获取频谱数据（0=总音量, 1..bands=各频段）</summary>
    public float[] GetSpectrum(int bands = 16)
    {
        var result = new float[bands + 1];
        if (_handle == 0 || !IsPlaying) return result;

        // 总音量
        var level = Bass.ChannelGetLevel(_handle);
        var left = level & 0xFFFF;
        var right = (level >> 16) & 0xFFFF;
        result[0] = Math.Clamp(Math.Max(left, right) / 32768f, 0, 1);

        // FFT
        if (_fftBands != bands) _fftBands = bands;
        Bass.ChannelGetData(_handle, _fftBuffer, (int)DataFlags.FFT2048);

        // 对数分组到 N 个频段
        var fftLen = 1024;
        for (int b = 0; b < bands; b++)
        {
            var start = (int)(fftLen * Math.Pow(b / (double)bands, 2));
            var end = Math.Max(start + 1, (int)(fftLen * Math.Pow((b + 1) / (double)bands, 2)));
            end = Math.Min(end, fftLen);
            float max = 0;
            for (int i = start; i < end; i++)
                max = Math.Max(max, _fftBuffer[i]);
            result[b + 1] = Math.Clamp(max * 3f, 0, 1);
        }
        return result;
    }

    public void ApplySpeed()
    {
        if (_handle != 0)
            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Tempo, (float)((_speed - 1.0) * 100));
    }

    public bool Is8Bit => _is8Bit;

    public void Toggle8Bit()
    {
        _is8Bit = !_is8Bit;
        if (_is8Bit)
        {
            _synth ??= new WaveOutSynth();
            // 静音 BASS 输出，由 waveOut 接管
            _saveVolume = _volume;
            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, 0);
        }
        else
        {
            _synth?.Stop();
            // 恢复 BASS 音量
            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _saveVolume);
        }
    }

    public void ApplyDsp8Bit()
    {
        _synth ??= new WaveOutSynth();
    }

    // === EQ ===
    public static readonly float[] EqFreqs = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
    public float[] EqGains = new float[10];
    public bool EqEnabled { get; private set; }
    private DXParamEQ? _eq;

    public static readonly (string Name, float[] Gains)[] EqPresets =
    [
        ("Flat",       [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        ("Pop",        [-1, 1, 2, 3, 2, 0, -1, -1, 0, 0]),
        ("Rock",       [4, 3, 1, -1, -2, -1, 2, 3, 4, 3]),
        ("Classical",  [3, 2, 1, 2, -1, -1, 0, 2, 3, 4]),
        ("Jazz",       [3, 2, 1, 2, -2, -2, 0, 1, 2, 3]),
        ("Bass Boost", [6, 4, 2, 1, 0, 0, 0, 0, 0, 0]),
        ("Treble",     [-3, -2, -1, 0, 1, 3, 5, 6, 6, 5]),
        ("Vocal",      [-4, -3, 0, 2, 4, 3, 2, 0, -1, -2]),
    ];

    public void ToggleEq(bool enable)
    {
        EqEnabled = enable;
        if (_handle == 0) return;
        if (enable) ApplyEq(); else RemoveEq();
    }

    public void ApplyEqPreset(int idx)
    {
        if (idx < 0 || idx >= EqPresets.Length) return;
        Array.Copy(EqPresets[idx].Gains, EqGains, 10);
        if (EqEnabled && _handle != 0) ApplyEq();
    }

    public void SetEqBand(int band, float gain)
    {
        if (band < 0 || band >= 10) return;
        EqGains[band] = Math.Clamp(gain, -15, 15);
        if (EqEnabled && _handle != 0 && _eq != null)
            _eq.UpdateBand(band, gain);
    }

    private void ApplyEq()
    {
        RemoveEq();
        _eq = new DXParamEQ(_handle, 0);
        for (int i = 0; i < 10; i++)
            _eq.AddBand(EqFreqs[i]);
        for (int i = 0; i < 10; i++)
            _eq.UpdateBand(i, EqGains[i]);
    }

    private void RemoveEq()
    {
        _eq?.Dispose();
        _eq = null;
    }

    /// <summary>切换到指定输出设备</summary>
    public void SwitchDevice(int deviceIndex)
    {
        if (_handle == 0) return;
        // 初始化目标设备（已初始化则忽略错误）
        ManagedBass.Bass.Init(deviceIndex, 44100, ManagedBass.DeviceInitFlags.Default);
        // 移动流到目标设备
        ManagedBass.Bass.ChannelSetDevice(_handle, deviceIndex);
        // 重启播放让切换生效
        if (IsPlaying) { ManagedBass.Bass.ChannelPause(_handle); ManagedBass.Bass.ChannelPlay(_handle); }
    }

    public void Dispose()
    {
        _suppressEnded = true;
        Close();
        BassLoader.Free();
    }

    private void OnEnded(int syncHandle, int channel, int data, IntPtr user)
    {
        if (_suppressEnded) return;
        if (LoopA != null && LoopB != null) return;
        PlaybackEnded?.Invoke();
    }
}
