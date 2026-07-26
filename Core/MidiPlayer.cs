using System.Runtime.InteropServices;
using System.Text;

namespace Whisper.Core;

/// <summary>Windows MCI MIDI 播放器（异步 + 时间估算，无 UI 阻塞）</summary>
public class MidiPlayer : IDisposable
{
    [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
    private static extern int mciSendStringA(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

    private string _alias = "";
    private TimeSpan _length;
    private bool _playing;
    private TimeSpan _syncPos;      // 上次 MCI 查询的位置
    private DateTime _syncTime;     // 上次查询的时刻

    public TimeSpan Position
    {
        get
        {
            if (!_playing) return _syncPos;
            var dt = DateTime.UtcNow - _syncTime;
            return _syncPos + TimeSpan.FromMilliseconds(dt.TotalMilliseconds);
        }
        set { _syncPos = value; Send($"seek {_alias} to {value.TotalMilliseconds:F0}"); }
    }

    public TimeSpan Length => _length;
    public bool IsPlaying => _playing;

    private void Send(string cmd) { if (_alias.Length > 0) mciSendStringA(cmd, null, 0, IntPtr.Zero); }
    private string? Query(string cmd) { if (_alias.Length == 0) return null; var sb = new StringBuilder(32); mciSendStringA(cmd, sb, 32, IntPtr.Zero); return sb.ToString(); }

    private static int _id;
    private static string NextAlias() => $"wmidi{Interlocked.Increment(ref _id)}";

    public void Open(string path)
    {
        Close();
        _alias = NextAlias();
        Send($"open \"{path}\" type sequencer alias {_alias}");
        Send($"set {_alias} time format milliseconds");
        var len = Query($"status {_alias} length");
        _length = TimeSpan.FromMilliseconds(int.TryParse(len, out var ms) ? ms : 0);
        _syncPos = TimeSpan.Zero;
        _playing = false;
    }

    public void Play()
    {
        Send($"play {_alias}");
        _playing = true;
        _syncTime = DateTime.UtcNow;
        PollAsync();
    }

    public void Pause()
    {
        Send($"pause {_alias}");
        _playing = false;
        _syncPos = Position; // 冻结估算值
        PollAsync();
    }

    public void Stop()
    {
        Send($"stop {_alias}");
        _playing = false;
        _syncPos = TimeSpan.Zero;
    }

    public void Seek(TimeSpan pos)
    {
        _syncPos = pos;
        _syncTime = DateTime.UtcNow;
        Send($"seek {_alias} to {pos.TotalMilliseconds:F0}");
    }

    public void Tick()
    {
        // 每 200ms 异步校准一次位置
        if (_alias.Length > 0 && _playing && (DateTime.UtcNow - _syncTime).TotalMilliseconds > 200)
            PollAsync();
    }

    private void PollAsync()
    {
        _ = Task.Run(() =>
        {
            var pos = Query($"status {_alias} position");
            var mode = Query($"status {_alias} mode");
            if (mode == "playing") _playing = true;
            if (pos != null && int.TryParse(pos, out var ms))
            {
                _syncPos = TimeSpan.FromMilliseconds(ms);
                _syncTime = DateTime.UtcNow;
            }
        });
    }

    public void Close()
    {
        if (_alias.Length > 0) { Send($"close {_alias}"); _alias = ""; }
    }

    public void Dispose() => Close();
}
