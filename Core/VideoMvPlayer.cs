using System.Diagnostics;

namespace Whisper.Core;

public class VideoMvPlayer : IDisposable
{
    private Process? _ffmpeg;
    private BinaryReader? _reader;
    private string? _path;
    private int _width, _height;
    private readonly byte[] _frame;
    private int _fps = 10;
    private TimeSpan? _seekTarget;
    private readonly string _ffmpegPath;

    public bool IsOpen { get; private set; }
    public int Width => _width;
    public int Height => _height;

    public VideoMvPlayer(string? ffmpegPath = null)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        var home = Path.Combine(BassLoader.LibDir, "ffmpeg.exe");
        _ffmpegPath = ffmpegPath
            ?? (File.Exists(local) ? local
                : File.Exists(home) ? home
                : "ffmpeg");
        _frame = new byte[0];
    }

    /// <summary>Seek 到指定时间：关闭旧管道，重新从 -ss 启动</summary>
    public void Seek(TimeSpan pos)
    {
        if (!IsOpen || _path == null) return;
        Close();
        _seekTarget = pos;
        OpenAtPosition(_path, pos, _fps, _width, _height);
    }

    /// <summary>打开视频文件，启动 FFmpeg 解码管道</summary>
    public void Open(string path, int fps = 10, int maxWidth = 80, int maxHeight = 40)
    {
        Close();
        _path = path;
        _fps = fps;

        var probe = ProbeVideo(path);
        // 终端字符 ~2:1 (高:宽)，用比例缩放保持原视频纵横比
        float aspect = probe.h > 0 ? (float)probe.w / probe.h * 2f : 1f;  // *2 补偿字符高宽比
        if (aspect >= 1f) { _width = Math.Min(probe.w, maxWidth); _height = Math.Max(1, (int)(_width / aspect)); }
        else { _height = Math.Min(probe.h, maxHeight); _width = Math.Max(1, (int)(_height * aspect)); }
        _width = Math.Clamp(_width, 10, maxWidth);
        _height = Math.Clamp(_height, 1, 11);  // 歌词区只有11行

        OpenAtPosition(path, TimeSpan.Zero, fps, _width, _height);
    }

    private void OpenAtPosition(string path, TimeSpan start, int fps, int w, int h)
    {
        var ssArg = start > TimeSpan.Zero ? $"-ss {start.TotalSeconds:F3} " : "";
        _ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-loglevel quiet {ssArg}-i \"{path}\" -an -f rawvideo -pix_fmt gray -s {w}x{h} -r {fps} -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _ffmpeg.Start();
        _reader = new BinaryReader(_ffmpeg.StandardOutput.BaseStream);
        IsOpen = true;
    }

    /// <summary>读取一帧灰度像素，转换成 ASCII 字符</summary>
    public string[]? ReadFrame()
    {
        if (!IsOpen || _reader == null) return null;
        if (_ffmpeg == null || _ffmpeg.HasExited) { IsOpen = false; return null; }

        int size = _width * _height;
        try
        {
            var data = _reader.ReadBytes(size);
            if (data.Length < size) { IsOpen = false; return null; }
            return RenderAscii(data);
        }
        catch (EndOfStreamException) { IsOpen = false; return null; }
        catch { return null; }
    }

    private string[] RenderAscii(byte[] gray)
    {
        const string chars = " .:-=+*#%@";
        var lines = new string[_height];
        for (int y = 0; y < _height; y++)
        {
            var row = new char[_width];
            int offset = y * _width;
            for (int x = 0; x < _width; x++)
            {
                int g = gray[offset + x];
                row[x] = chars[g * (chars.Length - 1) / 255];
            }
            lines[y] = new string(row);
        }
        return lines;
    }

    private static (int w, int h) ProbeVideo(string path)
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{path}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            p.Start();
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            var parts = output.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                return (w, h);
        }
        catch { }
        return (80, 40); // fallback
    }

    public void Dispose()
    {
        Close();
    }

    public void Close()
    {
        _reader?.Dispose();
        _reader = null;
        if (_ffmpeg != null && !_ffmpeg.HasExited)
        {
            try { _ffmpeg.Kill(); } catch { }
            _ffmpeg.Dispose();
        }
        _ffmpeg = null;
        IsOpen = false;
    }
}
