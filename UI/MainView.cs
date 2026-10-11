using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Whisper.Core;

namespace Whisper.UI;

public class MainView : Window
{
    private readonly Player _player = Program.Player;
    private readonly MidiPlayer _midi = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _playlist = [];
    private Label _lblHelp = null!;
    private Label _lblCmdPrompt = null!, _lblCmdInput = null!;
    private Scheme _schemeCmdPrompt = null!, _schemeCmdInput = null!, _schemeCmdError = null!;
    private int _currentIndex = -1;
    private LrcParser? _lrc;
    private readonly Random _rng = new();
    private readonly CharAnim _charAnim;
    private double _animTime;
    private Scheme _schemeAnim = null!, _schemeCurr = null!, _schemeDim = null!;
    private Button _btnDevice = null!;
    private List<int> _deviceIds = [];
    private List<string> _deviceNames = [];
    private int _currentDevice = -1;  // BASS device index
    private readonly CommandEngine _cmdEngine = new();
    private bool _inCmdMode;
    private bool _cmdErrorShown;
    private bool _scriptRunning;
    private string[] _scriptLines = [];
    private int _scriptIdx;
    private string _scriptName = "";
    private string _scriptCurrentLine = "";
    private DateTime _scriptWaitUntil;  // 等待计时
    private bool _scriptWaitDone;      // 等待歌曲播完
    private readonly StringBuilder _cmdBuffer = new();
    private Scheme _schemeCmd = null!;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".whisper");
    private static readonly string AnimFile = Path.Combine(ConfigDir, "charanim.json");
    private static readonly string LastDirFile = Path.Combine(ConfigDir, "lastdir.txt");
    private static readonly string PlaylistFile = Path.Combine(ConfigDir, "playlist.txt");
    private static readonly string StateFile = Path.Combine(ConfigDir, "state.txt");

    private readonly Label _lblTitle;
    private readonly Label _lblTime;
    private readonly ProgressBar _progress;
    private readonly Label[] _lrcLabels = new Label[11];
    private readonly ListView _lstFiles;
    private readonly ProgressBar _volBar;
    private readonly Label _lblLoop;
    private readonly Button _btnPlayPause;
    private readonly Button _btnSpeed;
    private readonly Button _btnMode;
    private readonly Button _btn8Bit;
    private Scheme _btn8BitDefault = null!;
    private readonly ProgressBar[] _spectrumBars = new ProgressBar[17];
    private readonly FrameView _spectrumView;
    private readonly FrameView _metaView;
    private readonly Label[] _metaLabels = new Label[9];

    private const int MetaWidth = 28;
    private const int MetaHeight = 11;
    private const int MetaY = 2;
    private const int PlaylistY = 13;

    private int _speedIdx = 2;
    private int _modeIdx = 0;
    private readonly StringBuilder _pathBuffer = new();
    private bool _lastWasDriveLetter;
    private bool _dialogOpen;
    private readonly VideoMvPlayer _videoMv = new();
    private bool _isVideo;  // 当前播放的是视频文件

    private static readonly string[] _audioExts = [".mp3", ".flac", ".wav", ".ogg", ".aac", ".wma", ".m4a", ".ape", ".mid", ".midi", ".mp4", ".mkv", ".avi", ".webm"];

    private static readonly float[] Speeds = [0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f];
    private static readonly string[] Modes = ["Sequential", "Single Loop", "Random"];

    public MainView()
    {
        Title = "Whisper Player";
        BorderStyle = LineStyle.Double;

        _charAnim = CharAnim.LoadOrCreate();
        var cNormal = new Scheme {
            Normal = new Attribute(Color.Green, Color.Black),
            Focus = new Attribute(Color.Black, Color.DarkGray),
            HotNormal = new Attribute(Color.BrightBlue, Color.Black),
            HotFocus = new Attribute(Color.BrightCyan, Color.DarkGray),
            Disabled = new Attribute(Color.DarkGray, Color.Black)
        };
        _schemeAnim = new Scheme {
            Normal = new Attribute(Color.DarkGray, Color.Black),
            Focus = new Attribute(Color.DarkGray, Color.Black),
            HotNormal = new Attribute(Color.DarkGray, Color.Black),
            HotFocus = new Attribute(Color.DarkGray, Color.Black),
            Disabled = new Attribute(Color.DarkGray, Color.Black)
        };
        _schemeCmdPrompt = new Scheme {
            Normal = new Attribute(Color.Gray, Color.Black),
            Focus = new Attribute(Color.Gray, Color.Black),
            HotNormal = new Attribute(Color.Gray, Color.Black),
            HotFocus = new Attribute(Color.Gray, Color.Black),
            Disabled = new Attribute(Color.Gray, Color.Black)
        };
        _schemeCmdInput = new Scheme {
            Normal = new Attribute(Color.BrightYellow, Color.Black),
            Focus = new Attribute(Color.BrightYellow, Color.Black),
            HotNormal = new Attribute(Color.BrightYellow, Color.Black),
            HotFocus = new Attribute(Color.BrightYellow, Color.Black),
            Disabled = new Attribute(Color.BrightYellow, Color.Black)
        };
        _schemeCmdError = new Scheme {
            Normal = new Attribute(Color.Red, Color.Black),
            Focus = new Attribute(Color.Red, Color.Black),
            HotNormal = new Attribute(Color.Red, Color.Black),
            HotFocus = new Attribute(Color.Red, Color.Black),
            Disabled = new Attribute(Color.Red, Color.Black)
        };
        var cWindow = new Scheme {
            Normal = new Attribute(Color.Green, Color.Black),
            Focus = new Attribute(Color.Green, Color.Black),
            HotNormal = new Attribute(Color.BrightBlue, Color.Black),
            HotFocus = new Attribute(Color.BrightCyan, Color.DarkGray),
            Disabled = new Attribute(Color.DarkGray, Color.Black)
        };
        SetScheme(cWindow);

        var cDim = new Scheme {
            Normal = new Attribute(Color.DarkGray, Color.Black),
            Focus = new Attribute(Color.DarkGray, Color.Black),
            HotNormal = new Attribute(Color.DarkGray, Color.Black),
            HotFocus = new Attribute(Color.DarkGray, Color.Black),
            Disabled = new Attribute(Color.DarkGray, Color.Black)
        };
        _schemeDim = cDim;
        var cCurr = new Scheme {
            Normal = new Attribute(Color.BrightYellow, Color.Black),
            Focus = new Attribute(Color.BrightYellow, Color.Black),
            HotNormal = new Attribute(Color.BrightYellow, Color.Black),
            HotFocus = new Attribute(Color.BrightYellow, Color.Black),
            Disabled = new Attribute(Color.BrightYellow, Color.Black)
        };
        _schemeCurr = cCurr;
        var cList = new Scheme {
            Normal = new Attribute(Color.Green, Color.Black),
            Focus = new Attribute(Color.Black, Color.DarkGray),
            HotNormal = new Attribute(Color.BrightBlue, Color.Black),
            HotFocus = new Attribute(Color.Black, Color.DarkGray),
            Disabled = new Attribute(Color.DarkGray, Color.Black)
        };

        // === Top info ===
        _lblTitle = new Label {
            Text = "Whisper - Press Ctrl+O to open files",
            X = 0, Y = 0, Width = Dim.Fill(26), Height = 1
        };
        _lblTitle.SetScheme(cNormal);
        var btnDevReload = new Button { NoPadding = true,
            Text = "Refresh", X = Pos.AnchorEnd(30), Y = 0, Width = 8, Height = 1
        };
        _btnDevice = new Button { NoPadding = true,
            Text = "Dev: -", X = Pos.Right(btnDevReload) + 1, Y = 0, Width = 28, Height = 1
        };
        _btnDevice.Accepting += (_, _) => CycleDevice();
        PopulateDevices();
        btnDevReload.Accepting += (_, _) => {
            PopulateDevices();
            _player.SwitchDevice(_currentDevice);
        };
        _lblTime = new Label { Text = "00:00 / 00:00", X = 0, Y = 1, Width = 16, Height = 1 };
        var progBar = new Scheme { Normal = new Attribute(Color.Green, Color.DarkGray), HotNormal = new Attribute(Color.BrightGreen, Color.DarkGray) };
        _progress = new ProgressBar { X = 18, Y = 1, Width = Dim.Fill(), Height = 1, Fraction = 0 };
        _progress.SetScheme(progBar);

        // === Lyrics 11 lines Y=2..12 (right of metadata) ===
        for (int i = 0; i < 11; i++)
        {
            _lrcLabels[i] = new Label { X = MetaWidth + 1, Y = 2 + i, Width = Dim.Fill(), Height = 1,
                TextAlignment = Alignment.Center };
            _lrcLabels[i].SetScheme(i == 5 ? cCurr : cDim);
        }

        // === Metadata GroupBox (left, fixed size) ===
        _metaView = new FrameView {
            Title = "Metadata", X = 0, Y = MetaY, Width = MetaWidth, Height = MetaHeight,
            BorderStyle = LineStyle.Single
        };
        _metaView.SetScheme(cWindow);
        for (int i = 0; i < 9; i++)
        {
            _metaLabels[i] = new Label { X = 0, Y = i, Width = Dim.Fill(), Height = 1 };
            _metaLabels[i].SetScheme(cNormal);
            _metaView.Add(_metaLabels[i]);
        }

        // === Spectrum (35%, left) + Playlist (65%, right) ===
        // 如果元数据 GroupBox 高度延伸到播放列表区域，播放列表左移避开
        var metaOverflows = MetaY + MetaHeight > PlaylistY;
        var playlistW = metaOverflows ? Dim.Fill(MetaWidth) : Dim.Percent(65);

        _spectrumView = new FrameView { Title = "Spectrum", X = 0, Y = PlaylistY, Width = Dim.Percent(35), Height = Dim.Fill(3), BorderStyle = LineStyle.Single };
        _spectrumView.SetScheme(cWindow);
        var specLbls = new[] { "Ovl", "Sub", "L1 ", "L2 ", "L3 ", "L4 ", "M1 ", "M2 ", "M3 ", "M4 ", "H1 ", "H2 ", "H3 ", "H4 ", "Hi1", "Hi2", "Hi3" };
        var specCyan = new Scheme { Normal = new Attribute(Color.BrightCyan, Color.Black) };
        var specBar = new Scheme { Normal = new Attribute(Color.Green, Color.Black) };
        for (int i = 0; i < 17; i++)
        {
            // 频段标签
            _spectrumView.Add(new Label { Text = specLbls[i], X = 0, Y = i, Width = 4, Height = 1 });
            _spectrumBars[i] = new ProgressBar { X = 4, Y = i, Width = Dim.Fill(), Height = 1, Fraction = 0 };
            _spectrumBars[i].SetScheme(i == 0 ? specCyan : specBar);
            _spectrumView.Add(_spectrumBars[i]);
        }

        // Playlist (65%, right)
        var lstLabel = new Label { Text = "Playlist:", X = Pos.Right(_spectrumView), Y = PlaylistY, Width = playlistW, Height = 1 };
        lstLabel.SetScheme(cNormal);
        _lstFiles = new ListView { X = Pos.Right(_spectrumView), Y = PlaylistY + 1, Width = playlistW, Height = Dim.Fill(3) };
        _lstFiles.SetScheme(cList);
        _lstFiles.SetSource(_playlist);
        _lstFiles.Accepting += (_, _) => { if (_lstFiles.SelectedItem is int sel && sel >= 0) { _currentIndex = sel; PlayIndex(sel); } };

        // === Bottom controls ===
        var btnY = Pos.AnchorEnd(3);
        var volY = Pos.AnchorEnd(2);
        var helpY = Pos.AnchorEnd(1);

        var btnPrev = new Button { NoPadding = true, Text = "_<", X = 0, Y = btnY, Width = 7, Height = 1 }; btnPrev.Accepting += (_, _) => Prev();
        var btnNext = new Button { NoPadding = true, Text = "_>", X = Pos.Right(btnPrev) + 1, Y = btnY, Width = 7, Height = 1 }; btnNext.Accepting += (_, _) => Next();

        _btnPlayPause = new Button { NoPadding = true, Text = "_Play", X = Pos.Right(btnNext) + 1, Y = btnY, Width = 10, Height = 1 }; _btnPlayPause.Accepting += (_, _) => TogglePlayPause();
        var btnStop = new Button { NoPadding = true, Text = "_Stop", X = Pos.Right(_btnPlayPause) + 1, Y = btnY, Width = 9, Height = 1 }; btnStop.Accepting += (_, _) => { if (_midiPath != null) _midi.Stop(); else _player.Stop(); if (_isVideo) { _videoMv.Close(); for (int i = 0; i < 11; i++) _lrcLabels[i].Text = ""; } };
        var btnOpen = new Button { NoPadding = true, Text = "_Open", X = Pos.Right(btnStop) + 1, Y = btnY, Width = 9, Height = 1 }; btnOpen.Accepting += (_, _) => OpenFiles();
        var btnDir = new Button { NoPadding = true, Text = "_Load", X = Pos.Right(btnOpen) + 1, Y = btnY, Width = 9, Height = 1 }; btnDir.Accepting += (_, _) => LoadFolder();
        var btnDel = new Button { NoPadding = true, Text = "_Del", X = Pos.Right(btnDir) + 1, Y = btnY, Width = 8, Height = 1 }; btnDel.Accepting += (_, _) => DeleteSelected();
        var btnAbort = new Button { NoPadding = true, Text = "Abo_rt", X = Pos.Right(btnDel) + 1, Y = btnY, Width = 9, Height = 1 };
        btnAbort.Accepting += (_, _) => AbortScript();
        var btnSort = new Button { NoPadding = true, Text = "_Sort", X = Pos.Right(btnAbort) + 1, Y = btnY, Width = 9, Height = 1 };
        btnSort.Accepting += (_, _) => SortPlaylist();
        var btnClrAll = new Button { NoPadding = true, Text = "Cl_rAll", X = Pos.Right(btnSort) + 1, Y = btnY, Width = 9, Height = 1 }; btnClrAll.Accepting += (_, _) => ClearAll();

        // Volume row
        var lblVol = new Label { Text = "Vol:", X = 0, Y = volY, Width = 5, Height = 1 };
        _volBar = new ProgressBar { X = Pos.Right(lblVol) + 1, Y = volY, Width = 20, Height = 1, Fraction = 0.8f };
        _player.Volume = 0.8f;

        _btnSpeed = new Button { NoPadding = true, Text = "1.00x", X = Pos.Right(_volBar) + 2, Y = volY, Width = 8, Height = 1 };
        _btnSpeed.Accepting += (_, _) => { _speedIdx = (_speedIdx + 1) % Speeds.Length; _btnSpeed.Text = $"{Speeds[_speedIdx]:F2}x"; _player.Speed = Speeds[_speedIdx]; _player.ApplySpeed(); };
        _btnMode = new Button { NoPadding = true, Text = Modes[0], X = Pos.Right(_btnSpeed) + 1, Y = volY, Width = 12, Height = 1 };
        _btnMode.Accepting += (_, _) => { _modeIdx = (_modeIdx + 1) % Modes.Length; _btnMode.Text = Modes[_modeIdx]; _player.Mode = (PlayMode)_modeIdx; };
        var _btnEQ = new Button { NoPadding = true, Text = "_EQ", X = Pos.Right(_btnMode) + 1, Y = volY, Width = 6, Height = 1 };
        _btnEQ.Accepting += (_, _) => EqWindow.Show(_player);
        _btn8Bit = new Button { NoPadding = true, Text = "8_Bit", X = Pos.Right(_btnEQ) + 1, Y = volY, Width = 8, Height = 1 };
        _btn8BitDefault = _btn8Bit.GetScheme();
        _btn8Bit.Accepting += (_, _) => Toggle8Bit();
        var btnLoopA = new Button { NoPadding = true, Text = "_A", X = Pos.Right(_btn8Bit) + 1, Y = volY, Width = 6, Height = 1 }; btnLoopA.Accepting += (_, _) => { _player.LoopA = _player.Position; UpdateLoopLabel(); };
        var btnLoopB = new Button { NoPadding = true, Text = "_B", X = Pos.Right(btnLoopA) + 1, Y = volY, Width = 6, Height = 1 }; btnLoopB.Accepting += (_, _) => { _player.LoopB = _player.Position; UpdateLoopLabel(); };
        var btnClear = new Button { NoPadding = true, Text = "_Clear", X = Pos.Right(btnLoopB) + 1, Y = volY, Width = 9, Height = 1 }; btnClear.Accepting += (_, _) => { _player.LoopA = null; _player.LoopB = null; UpdateLoopLabel(); };

        _lblLoop = new Label { X = Pos.Right(btnClear) + 2, Y = volY, Width = Dim.Fill(), Height = 1 };

        _lblHelp = new Label { Text = HelpText, X = 0, Y = helpY, Width = Dim.Fill(), Height = 1 };
        _lblHelp.SetScheme(cDim);
        _lblCmdPrompt = new Label { Text = ">>>", X = 0, Y = helpY, Width = 4, Height = 1, Visible = false };
        _lblCmdPrompt.SetScheme(_schemeCmdPrompt);
        _lblCmdInput = new Label { X = 4, Y = helpY, Width = Dim.Fill(), Height = 1, Visible = false };
        _lblCmdInput.SetScheme(_schemeCmdInput);

        Add(_lblTitle, _lblTime, _progress, _btnDevice, btnDevReload, _metaView);
        foreach (var l in _lrcLabels) Add(l);
        Add(lstLabel, _lstFiles, _spectrumView,
            btnPrev, btnNext, _btnPlayPause, btnStop, btnOpen, btnDir, btnDel, btnLoopA, btnLoopB, btnClear, btnAbort, btnSort, btnClrAll,
            lblVol, _volBar, _btnSpeed, _btnMode, _btnEQ, _btn8Bit, _lblLoop, _lblHelp, _lblCmdPrompt, _lblCmdInput);

        var timer = new System.Timers.Timer(100);
        timer.Elapsed += (_, _) => Application.Invoke(Refresh);
        timer.Start();

        _player.PlaybackEnded += () => Application.Invoke(() => {
            _scriptWaitDone = false;
            Advance();
        });
        Application.KeyDown += OnRootKey;

        LoadPlaylist();
        PrependArgs();

        RegisterCommands();
    }

    /// <summary>退出前保存配置（v2 Run 返回后由 Program 显式调用，Dispose 不保证触发）</summary>
    public void SaveAll()
    {
        SavePlaylist();
        SaveState();
    }

    protected override void Dispose(bool disposing)
    {
        SaveAll();
        base.Dispose(disposing);
    }

    private void RegisterCommands()
    {
        _cmdEngine.Register("play",  _ => { if (!_player.IsPlaying) _player.Play(); }, "播放");
        _cmdEngine.Register("pause", _ => _player.Pause(), "暂停");
        _cmdEngine.Register("stop",  _ => { _player.Stop(); if (_isVideo) { _videoMv.Close(); for (int i = 0; i < 11; i++) _lrcLabels[i].Text = ""; } }, "停止");
        _cmdEngine.Register("next",  _ => Next(), "下一首");
        _cmdEngine.Register("prev",  _ => Prev(), "上一首");
        _cmdEngine.Register("q",     _ => Application.RequestStop(), "退出");
        _cmdEngine.Register("quit",  _ => Application.RequestStop(), "退出");
        _cmdEngine.Register("thanks", _ => {
            MessageBox.Query(Application.Instance, 52, 10, "Thanks",
                "Whisper uses the following open source projects:\n\n" +
                "BASS Audio Library\n" +
                "https://www.un4seen.com/\n\n" +
                "FFmpeg (LGPL)\n" +
                "https://ffmpeg.org/\n\n" +
                "Thank you!", "OK");
        }, "鸣谢开源项目");
        _cmdEngine.Register("reload",_ => { SavePlaylist(); LoadPlaylist(); }, "重载播放列表");
        _cmdEngine.Register("clear", _ => ClearAll(), "清空播放列表");
        _cmdEngine.Register("open",  _ => OpenFiles(), "打开文件");
        _cmdEngine.Register("load",  _ => LoadFolder(), "加载文件夹");
        _cmdEngine.Register("loop", arg => {
            if (arg == "a") { _player.LoopA = _player.Position; UpdateLoopLabel(); }
            else if (arg == "b") { _player.LoopB = _player.Position; UpdateLoopLabel(); }
            else { _player.LoopA = null; _player.LoopB = null; UpdateLoopLabel(); }
        }, "AB循环 (a/b/clear)");
        _cmdEngine.Register("speed", arg => {
            if (float.TryParse(arg, out var s))
            {
                _player.Speed = Math.Clamp(s, 0.5f, 2.0f);
                _player.ApplySpeed();
                _btnSpeed.Text = $"{_player.Speed:F2}x";
            }
        }, "变速 (0.5-2.0)");
        _cmdEngine.Register("vol", arg => {
            if (float.TryParse(arg, out var v))
            {
                _player.Volume = Math.Clamp(v, 0f, 1f);
                _volBar.Fraction = _player.Volume;
            }
        }, "音量 (0-1)");
        _cmdEngine.Register("seek", arg => {
            if (int.TryParse(arg, out var sec))
                _player.Position += TimeSpan.FromSeconds(sec);
        }, "跳转 (±秒)");
        _cmdEngine.Register("mode", arg => {
            if (int.TryParse(arg, out var m) && m >= 0 && m < Modes.Length)
            {
                _modeIdx = m; _btnMode.Text = Modes[m];
                _player.Mode = (PlayMode)m;
            }
            else { _modeIdx = (_modeIdx + 1) % Modes.Length; _btnMode.Text = Modes[_modeIdx]; _player.Mode = (PlayMode)_modeIdx; }
        }, "播放模式 (0=顺序 1=单曲 2=随机)");
        _cmdEngine.Register("sort", _ => SortPlaylist(), "按文件名排序播放列表");
        _cmdEngine.Register("help", _ => {
            MessageBox.Query(Application.Instance, 55, 16, "Commands",
                _cmdEngine.GetHelpText(), "OK");
        }, "列出所有命令");
        _cmdEngine.Register("exec", arg => {
            if (string.IsNullOrWhiteSpace(arg) || arg.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Query(Application.Instance, 55, 18, "exec - Script Runner",
                    "Usage: exec <path.whisp>\n\n" +
                    "Execute a Whisper script (.whisp).\n" +
                    "Each line is one command (same as :cmd mode).\n\n" +
                    "Available commands:\n" +
                    _cmdEngine.GetHelpText() + "\n\n" +
                    "Lines starting with # are comments.", "OK");
                return;
            }
            var path = arg.Trim().Trim('"').Trim('\'');
            while (path.Contains(@"\\")) path = path.Replace(@"\\", @"\");
            if (!File.Exists(path))
            {
                MessageBox.Query(Application.Instance, 50, 6, "exec", $"File not found:\n{path}", "OK");
                return;
            }
            try
            {
                var allLines = File.ReadAllLines(path);
                _scriptLines = allLines;
                _scriptIdx = 0;
                _scriptName = Path.GetFileName(path);
                _scriptRunning = true;
                _scriptWaitDone = false;
                ExecuteNextScriptLine();
            }
            catch (Exception ex)
            {
                MessageBox.Query(Application.Instance, 60, 8, "exec", $"Error reading script:\n{ex.Message}", "OK");
            }
        }, "执行脚本 (.whisp)");
        _cmdEngine.Register("info", _ => {
            var info =
                "Whisper - Terminal Audio Player\n" +
                "────────────────────────────────\n" +
                "BASS + WaveOutSynth 双引擎\n" +
                "10-band Parametric EQ\n" +
                "Spectrum Analyzer, LRC Lyrics\n" +
                "MIDI, 8-Bit, DSP Speed Control\n" +
                "Script engine (.whisp)\n" +
                "Terminal.Gui TUI framework\n" +
                "────────────────────────────────\n" +
                "by LargeHardTech\n" +
                "github.com/LargeHardTech/Whisper";
            MessageBox.Query(Application.Instance, 42, info.Split('\n').Length + 2, "About Whisper", info, "OK");
        }, "关于Whisper");
        _cmdEngine.Register("wait", arg => {
            // 脚本专用命令，控制台输入报错
            if (!_scriptRunning) { MessageBox.Query(Application.Instance, 40, 4, "wait", "wait is script-only. Use inside a .whisp file.", "OK"); return; }
            if (string.IsNullOrWhiteSpace(arg)) return;
            var parts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            var mode = parts[0].ToLower();
            var val = parts.Length > 1 ? parts[1] : "";
            if (mode == "done") _scriptWaitDone = true;
            else if (mode == "s" && double.TryParse(val, out var sec)) _scriptWaitUntil = DateTime.Now.AddSeconds(sec);
            else if (mode == "m" && double.TryParse(val, out var min)) _scriptWaitUntil = DateTime.Now.AddMinutes(min);
            else if (mode == "h" && double.TryParse(val, out var hr)) _scriptWaitUntil = DateTime.Now.AddHours(hr);
            else if (mode == "time" && TimeSpan.TryParse(val, out var ts))
            {
                var now = DateTime.Now;
                var target = now.Date + ts;
                if (target <= now) target = target.AddDays(1);
                _scriptWaitUntil = target;
            }
        }, "等待 (done/s/m/h/time)");
        ResumePlayback();
    }

    private void TogglePlayPause()
    {
        if (_midiPath != null)
        {
            if (_midi.IsPlaying) _midi.Pause(); else _midi.Play();
        }
        else
        {
            if (_player.IsPlaying) _player.Pause(); else { _player.Play(); if (_isVideo && !_videoMv.IsOpen && _playlist.Count > 0) { _videoMv.Open(_playlist[_currentIndex]); _videoMv.Seek(_player.Position); } }
        }
    }


    private void Toggle8Bit()
    {
        _player.Toggle8Bit();
        _btn8Bit.SetScheme(_player.Is8Bit ? new Scheme { Normal = new Attribute(Color.Black, Color.BrightGreen) } : _btn8BitDefault);
    }

    private void SortPlaylist()
    {
        // 自然排序：1, 2, 10 而不是 1, 10, 2（ObservableCollection 无 Sort，重建）
        var sorted = _playlist.OrderBy(p => p, Comparer<string>.Create((a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)))).ToList();
        _playlist.Clear();
        foreach (var p in sorted) _playlist.Add(p);
        _lstFiles.SetSource(_playlist);
        // 保持当前播放
        if (_currentIndex >= 0)
        {
            _currentIndex = sorted.IndexOf(_player.CurrentFile ?? "");
            _lstFiles.SelectedItem = _currentIndex >= 0 ? _currentIndex : 0;
        }
    }

    private static int NaturalCompare(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int numA = 0, numB = 0;
                while (i < a.Length && char.IsDigit(a[i])) { numA = numA * 10 + (a[i] - '0'); i++; }
                while (j < b.Length && char.IsDigit(b[j])) { numB = numB * 10 + (b[j] - '0'); j++; }
                if (numA != numB) return numA.CompareTo(numB);
            }
            else
            {
                var ca = char.ToLowerInvariant(a[i]);
                var cb = char.ToLowerInvariant(b[j]);
                if (ca != cb) return ca.CompareTo(cb);
                i++; j++;
            }
        }
        return a.Length.CompareTo(b.Length);
    }

    private void AbortScript()
    {
        if (!_scriptRunning) return;
        _scriptRunning = false;
        _scriptWaitUntil = DateTime.MinValue;
        _scriptWaitDone = false;
        UpdateCmdPrompt();
        MessageBox.Query(Application.Instance, 40, 5, "Script", "Script aborted.", "OK");
    }

    private const string HelpText = "Space:Play  ,/.:Prev/Next  Up/Dn:Vol  <-/->:Seek  Del:Remove  Ctrl+O:Open  A/B:Loop  C:Clr  Q:Quit  :Cmd";

    private void UpdateCmdPrompt()
    {
        if (_scriptRunning)
        {
            _lblHelp.Visible = _inCmdMode;
            _lblCmdPrompt.Visible = true;
            _lblCmdPrompt.Text = _inCmdMode ? ">>>" : "Exec";
            _lblCmdInput.Visible = true;
            _lblCmdInput.Text = _inCmdMode ? _cmdBuffer.ToString() : $"{_scriptName} : {_scriptCurrentLine}";
            return;
        }
        var show = _inCmdMode;
        _lblHelp.Visible = !show;
        _lblCmdPrompt.Visible = show;
        _lblCmdPrompt.Text = ">>>";
        _lblCmdInput.Visible = show;
        _lblCmdInput.Text = show ? _cmdBuffer.ToString() : "";
    }

    private void ExecuteCommand()
    {
        var input = _cmdBuffer.ToString().Trim();
        _cmdBuffer.Clear();
        if (input.Length == 0) { _inCmdMode = false; UpdateCmdPrompt(); return; }
        if (!_cmdEngine.Execute(input))
        {
            _lblCmdPrompt.SetScheme(_schemeCmdError);
            _lblCmdInput.SetScheme(_schemeCmdError);
            _lblCmdInput.Text = $"Unknown command: {input}";
            _inCmdMode = false;
            _lblHelp.Visible = false;
            _lblCmdPrompt.Visible = true;
            _lblCmdInput.Visible = true;
            _cmdErrorShown = true;
            return;
        }
        _inCmdMode = false;
        UpdateCmdPrompt();
    }

    private void OnRootKey(object? sender, Key ke)
    {
        // 消费命令错误提示后的任意按键
        if (_cmdErrorShown) { _cmdErrorShown = false; _lblCmdPrompt.SetScheme(_schemeCmdPrompt); _lblCmdInput.SetScheme(_schemeCmdInput); UpdateCmdPrompt(); ke.Handled = true; return; }

        // Escape：优先中止脚本（命令模式中不拦截）
        if (ke.KeyCode == KeyCode.Esc && _scriptRunning && !_inCmdMode) { AbortScript(); ke.Handled = true; return; }

        // === 命令模式 ===
        if (_inCmdMode)
        {
            ke.Handled = true;
            if (ke.KeyCode == KeyCode.Esc) { _inCmdMode = false; _cmdBuffer.Clear(); UpdateCmdPrompt(); return; }
            if (ke.KeyCode == KeyCode.Backspace) { if (_cmdBuffer.Length > 0) _cmdBuffer.Length--; UpdateCmdPrompt(); return; }
            if (ke.KeyCode == KeyCode.Enter) { ExecuteCommand(); return; }
            var cc = ke.TryGetPrintableRune(out var cr) ? (char)cr.Value : '\0';
            if (cc >= 32 && cc < 127) { _cmdBuffer.Append(cc); UpdateCmdPrompt(); return; }
            return;
        }

        // 对话框打开时不拦截任何按键
        if (_dialogOpen) return;

        var c = ke.TryGetPrintableRune(out var pr) ? (char)pr.Value : '\0';
        if (ke.IsCtrl) { ke.Handled = true; _pathBuffer.Clear(); if (c == 'o' || c == 'O') { OpenFiles(); } else if (c == 'q' || c == 'Q') { Application.RequestStop(); } return; }
        if (ke.KeyCode == KeyCode.CursorLeft) { ke.Handled = true; _pathBuffer.Clear(); Seek(-5); return; }
        if (ke.KeyCode == KeyCode.CursorRight) { ke.Handled = true; _pathBuffer.Clear(); Seek(5); return; }
        if (ke.KeyCode == KeyCode.CursorUp) { ke.Handled = true; _pathBuffer.Clear(); if (_midiPath == null) _player.Volume = Math.Clamp(_player.Volume + 0.05f, 0, 1); return; }
        if (ke.KeyCode == KeyCode.CursorDown) { ke.Handled = true; _pathBuffer.Clear(); if (_midiPath == null) _player.Volume = Math.Clamp(_player.Volume - 0.05f, 0, 1); return; }
        if (ke.KeyCode == KeyCode.Delete) { ke.Handled = true; _pathBuffer.Clear(); DeleteSelected(); return; }

        // 回车：如果在路径收集模式，结束路径
        if (ke.KeyCode == KeyCode.Enter)
        {
            if (_pathBuffer.Length > 0 && IsPathPrefix(_pathBuffer.ToString()))
            {
                var path = _pathBuffer.ToString().Trim('"').Trim();
                _pathBuffer.Clear();
                ProcessDroppedFile(path);
                ke.Handled = true;
                return;
            }
            _pathBuffer.Clear();
            return;
        }

        // 可打印字符：检测文件路径（拖拽到控制台时路径会作为文本输入）
        if (c >= 32 && c < 127)
        {
            ke.Handled = true;
            _pathBuffer.Append(c);
            var input = _pathBuffer.ToString();

            if (IsPathPrefix(input))
            {
                if (IsPathComplete(input))
                {
                    _pathBuffer.Clear();
                    ProcessDroppedFile(input.Trim('"'));
                }
                return;  // 路径收集模式：消费按键，不触发快捷键
            }

            // 不像路径前缀，保留最后一个字符（可能是新路径的盘符开头）
            _pathBuffer.Clear();
            if (char.IsLetter(c)) { _pathBuffer.Append(c); _lastWasDriveLetter = true; }
        }
        else
        {
            _pathBuffer.Clear();
            return;
        }

        switch (c) {
            case 'q': case 'Q': Application.RequestStop(); return;
            case ':': if (_lastWasDriveLetter) { _lastWasDriveLetter = false; _pathBuffer.Append(':'); return; } _inCmdMode = true; _cmdBuffer.Clear(); UpdateCmdPrompt(); return;
            case ' ': TogglePlayPause(); return;
            case ',': Prev(); return; case '.': Next(); return;
            case 'c': _player.LoopA = null; _player.LoopB = null; UpdateLoopLabel(); return;
            case 's': SortPlaylist(); return;
            case '8': Toggle8Bit(); return;
            case 'e': case 'E': EqWindow.Show(_player); return;
            case 'v': case 'V': if (_isVideo) { _videoMv.Close(); _isVideo = false; } else if (_playlist.Count > 0 && IsVideoFile(_playlist[_currentIndex])) { _videoMv.Open(_playlist[_currentIndex]); _isVideo = true; } return;
        }
    }

    /// <summary>检测字符串是否像 Windows 文件路径前缀 (X: 或 "X:)</summary>
    private static bool IsPathPrefix(string s)
    {
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (char.IsLetter(s[i]) && s[i + 1] == ':')
            {
                if (i == 0 || (i == 1 && s[0] == '"'))
                    return true;
            }
        }
        return false;
    }

    /// <summary>检测路径是否以音频扩展名结尾</summary>
    private static bool IsPathComplete(string s)
    {
        s = s.Trim('"');
        foreach (var ext in _audioExts)
            if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void ProcessDroppedFile(string path)
    {
        path = path.Trim('"').Trim();
        if (File.Exists(path))
            Program.ExternalArgsQueue.Enqueue(path);
        else if (Directory.Exists(path))
        {
            foreach (var f in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
                if (IsMusicFile(f))
                    Program.ExternalArgsQueue.Enqueue(f);
        }
    }

    private void Advance()
    {
        switch (_player.Mode) {
            case PlayMode.SingleLoop: if (_currentIndex >= 0) PlayIndex(_currentIndex); break;
            case PlayMode.Random:
                if (_playlist.Count > 1) { int r; do r = _rng.Next(_playlist.Count); while (r == _currentIndex); _currentIndex = r; _lstFiles.SelectedItem = r; PlayIndex(r); }
                else Next();
                break;
            default: Next(); break;
        }
    }

    private void Next() { if (_playlist.Count == 0) return; _currentIndex = (_currentIndex + 1) % _playlist.Count; _lstFiles.SelectedItem = _currentIndex; PlayIndex(_currentIndex); }
    private void Prev() { if (_playlist.Count == 0) return; _currentIndex = _currentIndex <= 0 ? _playlist.Count - 1 : _currentIndex - 1; _lstFiles.SelectedItem = _currentIndex; PlayIndex(_currentIndex); }
    private void Seek(int sec)
    {
        if (_midiPath != null)
        {
            var t = _midi.Position + TimeSpan.FromSeconds(sec);
            _midi.Position = t < TimeSpan.Zero ? TimeSpan.Zero : t > _midi.Length ? _midi.Length : t;
            return;
        }
        if (_player.Duration <= TimeSpan.Zero) return;
        var bt = _player.Position + TimeSpan.FromSeconds(sec);
        _player.Position = bt < TimeSpan.Zero ? TimeSpan.Zero : bt > _player.Duration ? _player.Duration : bt;
        if (_isVideo) _videoMv.Seek(_player.Position);
    }

    private void DeleteSelected()
    {
        var sel = _lstFiles.SelectedItem ?? -1; if (sel < 0 || sel >= _playlist.Count) return;
        var wasCurrent = sel == _currentIndex; _playlist.RemoveAt(sel);
        if (sel < _currentIndex) _currentIndex--;
        else if (wasCurrent) { _player.Close(); _lrc = null; _lblTitle.Text = "Whisper"; _currentIndex = -1; }
        _lstFiles.SetSource(_playlist);
        if (sel >= _playlist.Count) sel = _playlist.Count - 1;
        if (sel >= 0) _lstFiles.SelectedItem = sel;
        if (wasCurrent && sel < _playlist.Count && sel >= 0) { _currentIndex = sel; PlayIndex(sel); }
    }

    private void OpenFiles()
    {
        var lastDir = LoadLastDir();
        if (!Directory.Exists(lastDir))
            lastDir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (Directory.Exists(lastDir)) Environment.CurrentDirectory = lastDir;
        var dlg = new OpenDialog { Title = "Open Music", AllowsMultipleSelection = true, MustExist = true };
        _dialogOpen = true;
        Application.Run(dlg); _dialogOpen = false; if (dlg.Canceled) return;
        var firstPath = dlg.FilePaths.FirstOrDefault(); if (firstPath != null) SaveLastDir(Path.GetDirectoryName(firstPath) ?? lastDir);
        foreach (var f in dlg.FilePaths) if (File.Exists(f) && !_playlist.Contains(f)) _playlist.Add(f);
        _lstFiles.SetSource(_playlist);
        if (_currentIndex < 0 && _playlist.Count > 0) { _currentIndex = 0; _lstFiles.SelectedItem = 0; PlayIndex(0); }
    }

    private void LoadFolder()
    {
        if (_currentIndex < 0 || _currentIndex >= _playlist.Count) { MessageBox.Query(Application.Instance, 50, 5, "No song", "No song is currently playing", "OK"); return; }
        var dir = Path.GetDirectoryName(_playlist[_currentIndex]); if (dir == null || !Directory.Exists(dir)) return;
        var added = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)) if (IsMusicFile(f) && !_playlist.Contains(f)) { _playlist.Add(f); added++; }
        _lstFiles.SetSource(_playlist);
        MessageBox.Query(Application.Instance, 50, 5, "Done", $"Loaded {added} songs from folder", "OK");
    }

    private void ClearAll() { _midi.Stop(); _midiPath = null; _player.Close(); _playlist.Clear(); _currentIndex = -1; _lrc = null; _lblTitle.Text = "Whisper"; _lstFiles.SetSource(_playlist); }

    private void LoadPlaylist() { try { if (!File.Exists(PlaylistFile)) return; foreach (var l in File.ReadAllLines(PlaylistFile)) { var p = l.Trim(); if (File.Exists(p) && IsMusicFile(p) && !_playlist.Contains(p)) _playlist.Add(p); } _lstFiles.SetSource(_playlist); } catch { } }

    private bool PrependArgs() { var added = 0; for (int i = Program.Args.Length - 1; i >= 0; i--) { var p = Program.Args[i].Trim().Trim('"'); if (File.Exists(p) && IsMusicFile(p) && !_playlist.Contains(p)) { _playlist.Insert(0, p); added++; } } if (added > 0) _lstFiles.SetSource(_playlist); return added > 0; }

    /// <summary>新实例传来的歌曲：插到列表最前面并播放第一首</summary>
    private void PrependAndPlay(string[] paths)
    {
        var added = 0;
        for (int i = paths.Length - 1; i >= 0; i--)
        {
            var p = paths[i].Trim().Trim('"');
            if (File.Exists(p) && IsMusicFile(p) && !_playlist.Contains(p))
            {
                _playlist.Insert(0, p);
                added++;
            }
        }
        if (added > 0)
        {
            _lstFiles.SetSource(_playlist);
            _currentIndex = 0;
            _lstFiles.SelectedItem = 0;
            PlayIndex(0);
        }
    }

    private void SavePlaylist() { try { Directory.CreateDirectory(ConfigDir); File.WriteAllLines(PlaylistFile, _playlist); } catch { } }

    private void SaveState()
    {
        try
        {
            if (_currentIndex < 0 || _currentIndex >= _playlist.Count) return;
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(StateFile, $"index={_currentIndex}\nposition={_player.Position.TotalSeconds:F3}\nvol={_player.Volume:F2}\neq={(_player.EqEnabled ? 1 : 0)}\neq_gains={string.Join(",", _player.EqGains)}");
        }
        catch { }
    }

    private void ResumePlayback()
    {
        var savedIdx = -1;
        double savedPos = 0, savedVol = -1;
        if (File.Exists(StateFile))
        {
            try
            {
                foreach (var line in File.ReadAllLines(StateFile))
                {
                    if (line.StartsWith("index=")) int.TryParse(line[6..], out savedIdx);
                    else if (line.StartsWith("position=")) double.TryParse(line[9..], out savedPos);
                    else if (line.StartsWith("vol=")) double.TryParse(line[4..], out savedVol);
                    else if (line.StartsWith("eq=")) { if (int.TryParse(line[3..], out var eq) && eq == 1) _player.ToggleEq(true); }
                    else if (line.StartsWith("eq_gains="))
                    {
                        var gs = line[9..].Split(',');
                        for (int i = 0; i < 10 && i < gs.Length; i++)
                            if (float.TryParse(gs[i], out var g)) _player.EqGains[i] = g;
                    }
                }
            }
            catch { }
        }

        if (savedVol >= 0) { _player.Volume = (float)savedVol; _volBar.Fraction = _player.Volume; }
        if (savedIdx >= 0 && savedIdx < _playlist.Count && Program.Args.Length == 0)
        {
            _currentIndex = savedIdx;
            _lstFiles.SelectedItem = savedIdx;
            var saveVol = _player.Volume;
            _player.Volume = 0;
            PlayIndex(savedIdx);
            _player.Stop(); if (savedPos > 0) _player.Position = TimeSpan.FromSeconds(savedPos);
            if (_isVideo && savedPos > 0) _videoMv.Seek(_player.Position);
            _player.Volume = saveVol;
        }
        else if (savedIdx >= 0 && Program.Args.Length > 0)
        {
            // 有命令行参数：播放 args 插入的第一首，保存旧位置供下次无参启动
            _currentIndex = 0;
            _lstFiles.SelectedItem = 0;
            var saveVol = _player.Volume;
            _player.Volume = 0;
            PlayIndex(0);
            _player.Stop();
            _player.Volume = saveVol;
        }
        else if (_playlist.Count > 0)
        {
            _currentIndex = 0;
            _lstFiles.SelectedItem = 0;
            PlayIndex(0);
        }
    }

    private void PlayIndex(int idx)
    {
        if (idx < 0 || idx >= _playlist.Count) return; var path = _playlist[idx];
        if (!File.Exists(path)) { _lblTitle.Text = $"File not found: {Path.GetFileName(path)}"; _playlist.RemoveAt(idx); _lstFiles.SetSource(_playlist); if (idx < _playlist.Count) { _currentIndex = idx; _lstFiles.SelectedItem = idx; PlayIndex(idx); } else if (_playlist.Count > 0) { _currentIndex = _playlist.Count - 1; _lstFiles.SelectedItem = _currentIndex; PlayIndex(_currentIndex); } else _currentIndex = -1; return; }

        var ext = Path.GetExtension(path).ToLower();
        if (ext is ".mid" or ".midi")
        {
            _player.Close();
            _midi.Open(path);
            _midi.Play();
            _midiPath = path;
            _midiParser = MidiParser.Parse(path);
            _lblTitle.Text = Path.GetFileNameWithoutExtension(path);
            _lrc = null;
            UpdateMidiMetadata(path);
        }
        else
        {
            _midi.Stop();
            _midiPath = null;
            _midiParser = null;
            _player.Open(path); if (_currentDevice >= 0) _player.SwitchDevice(_currentDevice); if (_player.Is8Bit) _player.ApplyDsp8Bit(); var name = Path.GetFileNameWithoutExtension(path); _lblTitle.Text = name;
            var lrcPath = Path.ChangeExtension(path, ".lrc"); if (File.Exists(lrcPath)) { _lrc = LrcParser.Parse(lrcPath); if (_lrc.Title != null) _lblTitle.Text = _lrc.Artist != null ? $"{_lrc.Artist} - {_lrc.Title}" : _lrc.Title; } else _lrc = null;
            UpdateMetadata(path);
            // 视频：打开 FFmpeg 解码
            if (IsVideoFile(path)) { _videoMv.Open(path); _isVideo = true; }
            else { _videoMv.Close(); _isVideo = false; }
        }
    }

    private string? _midiPath;
    private MidiParser? _midiParser;

    private void UpdateMetadata(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            var sizeMB = fi.Length / 1024.0 / 1024.0;
            var dur = _player.Duration;
            var rate = _player.SampleRate;
            var chan = _player.Channels;
            var ext = Path.GetExtension(path).TrimStart('.').ToUpper();
            var br = dur > TimeSpan.Zero ? (int)(fi.Length * 8 / dur.TotalSeconds / 1000) : 0;

            var fname = Path.GetFileName(path);
            if (fname.Length > 20) fname = fname[..17] + "...";

            // ID3v1 标签（从文件末尾 128 字节读取）
            var (id3Artist, id3Album) = ReadId3v1(path);
            var artist = _lrc?.Artist ?? id3Artist;
            var album = id3Album;
            if (string.IsNullOrEmpty(artist)) artist = "Unknown";
            if (string.IsNullOrEmpty(album)) album = "Unknown";
            if (artist.Length > 20) artist = artist[..17] + "...";
            if (album.Length > 20) album = album[..17] + "...";

            _metaLabels[0].Text = $"File: {fname}";
            _metaLabels[1].Text = $"Size: {sizeMB:F1} MB";
            _metaLabels[2].Text = $"Rate: {rate} Hz";
            _metaLabels[3].Text = $"Chan: {(chan == 1 ? "Mono" : chan == 2 ? "Stereo" : $"{chan}ch")}";
            _metaLabels[4].Text = $"Type: {ext}";
            _metaLabels[5].Text = $"Br:  {br} kbps";
            _metaLabels[6].Text = $"Art: {artist}";
            _metaLabels[7].Text = $"Alb: {album}";
            _metaLabels[8].Text = $"Date: {fi.CreationTime:yyyy-MM-dd}";
        }
        catch { }
    }

    private void UpdateMidiMetadata(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            var sizeKB = fi.Length / 1024.0;
            var fname = Path.GetFileName(path);
            if (fname.Length > 20) fname = fname[..17] + "...";
            var dur = _midi.Length;
            var notes = _midiParser?.Notes.Count ?? 0;

            _metaLabels[0].Text = $"File: {fname}";
            _metaLabels[1].Text = $"Size: {sizeKB:F0} KB";
            _metaLabels[2].Text = $"Type: MIDI";
            _metaLabels[3].Text = $"Notes: {notes}";
            _metaLabels[4].Text = $"Dur:  {dur:m\\:ss}";
            _metaLabels[5].Text = $"Synth: MS GS";
            _metaLabels[6].Text = "";
            _metaLabels[7].Text = "";
        }
        catch { }
    }

    /// <summary>从文件末尾读取 ID3v1 标签（MP3/WAV）</summary>
    private static (string artist, string album) ReadId3v1(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 128) return ("", "");
            fs.Seek(-128, SeekOrigin.End);
            var buf = new byte[128];
            if (fs.Read(buf, 0, 128) < 128) return ("", "");
            if (buf[0] != (byte)'T' || buf[1] != (byte)'A' || buf[2] != (byte)'G') return ("", "");
            var artist = System.Text.Encoding.Default.GetString(buf, 33, 30).TrimEnd('\0', ' ');
            var album = System.Text.Encoding.Default.GetString(buf, 63, 30).TrimEnd('\0', ' ');
            return (artist, album);
        }
        catch { return ("", ""); }
    }

    private void UpdateLoopLabel() { _lblLoop.Text = _player.LoopA != null && _player.LoopB != null ? $"AB: {_player.LoopA:mm\\:ss} - {_player.LoopB:mm\\:ss}" : _player.LoopA != null ? $"A: {_player.LoopA:mm\\:ss}" : ""; }

    private void ExecuteNextScriptLine()
    {
        while (_scriptRunning && _scriptIdx < _scriptLines.Length)
        {
            var line = _scriptLines[_scriptIdx].Trim();
            _scriptIdx++;
            if (line.Length == 0 || line.StartsWith('#')) continue;

            _scriptCurrentLine = line;
            UpdateCmdPrompt();

            if (line.StartsWith("wait ", StringComparison.OrdinalIgnoreCase))
            {
                _cmdEngine.Execute(line);
                return; // 等待条件满足后由 TickScript 继续
            }
            _cmdEngine.Execute(line);
        }
        // 脚本结束
        if (_scriptRunning)
        {
            _scriptRunning = false;
            UpdateCmdPrompt();
        }
    }

    private void TickScript()
    {
        if (!_scriptRunning) return;
        if (_scriptWaitDone) return;  // 还在等：由 PlaybackEnded 事件处理
        if (_scriptWaitUntil > DateTime.MinValue && DateTime.Now < _scriptWaitUntil) return;
        _scriptWaitUntil = DateTime.MinValue;
        ExecuteNextScriptLine();
    }

    private void PopulateDevices()
    {
        var names = new List<string>();
        _deviceIds = new List<int>();
        var selected = 0;
        for (int i = 0; ManagedBass.Bass.GetDeviceInfo(i, out var info); i++)
        {
            if (!info.IsEnabled || info.IsLoopback || info.Name.Contains("No Sound")) continue;
            if (info.IsDefault) selected = names.Count;
            names.Add(info.Name);
            _deviceIds.Add(i);
        }
        _deviceNames = names;
        if (names.Count > 0)
        {
            _currentDevice = _deviceIds[selected];
            _btnDevice.Text = $"Dev: {names[selected]}";
        }
        else
        {
            _currentDevice = -1;
            _btnDevice.Text = "Dev: -";
        }
    }

    private void CycleDevice()
    {
        if (_deviceNames.Count == 0) return;
        var idx = _deviceIds.IndexOf(_currentDevice);
        idx = (idx + 1) % _deviceNames.Count;
        _currentDevice = _deviceIds[idx];
        _btnDevice.Text = $"Dev: {_deviceNames[idx]}";
        _player.SwitchDevice(_currentDevice);
    }

    private void Refresh()
    {
        _player.Tick();
        if (_midiPath != null) _midi.Tick();
        TickScript();

        // 处理来自新实例或拖拽的外部文件
        if (!Program.ExternalArgsQueue.IsEmpty)
        {
            var paths = new List<string>();
            while (Program.ExternalArgsQueue.TryDequeue(out var p))
                paths.Add(p);

            var music = new List<string>();
            var nonMusic = new List<string>();
            foreach (var raw in paths)
            {
                var p = raw.Trim().Trim('"');
                if (!File.Exists(p)) continue;
                if (IsMusicFile(p)) music.Add(p);
                else nonMusic.Add(p);
            }

            if (music.Count > 0)
                PrependAndPlay(music.ToArray());

            if (nonMusic.Count > 0)
            {
                var names = string.Join("\n", nonMusic.Take(5).Select(Path.GetFileName));
                if (nonMusic.Count > 5) names += $"\n... +{nonMusic.Count - 5} more";
                MessageBox.Query(Application.Instance, 50, 8, "Not audio", $"Skipped non-audio file(s):\n{names}", "OK");
            }
        }

        var pos = _midiPath != null ? _midi.Position : _player.Position;
        var dur = _midiPath != null ? _midi.Length : _player.Duration;
        _lblTime.Text = $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        if (dur > TimeSpan.Zero) _progress.Fraction = (float)(pos.TotalSeconds / dur.TotalSeconds);
        if (_midiPath != null && _midi.Length > TimeSpan.Zero && _midi.Position >= _midi.Length - TimeSpan.FromMilliseconds(200))
        {
            _midi.Stop();
            Advance();
        }
        _volBar.Enabled = _midiPath == null;
        _btnSpeed.Enabled = _midiPath == null;
        _volBar.Fraction = _player.Volume;
        _btnPlayPause.Text = (_midiPath != null ? _midi.IsPlaying : _player.IsPlaying) ? "_Pause" : "_Play";

        // Spectrum
        // Spectrum（MIDI 从解析器获取，暂停时归零；BASS 从 FFT）
        var spec = _midiPath != null && _midiParser != null
            ? (_midi.IsPlaying ? _midiParser.GetSpectrum(_midiParser.SecondsToTick(_midi.Position.TotalSeconds), 16) : new float[17])
            : _player.GetSpectrum(16);
        for (int i = 0; i < 17; i++) _spectrumBars[i].Fraction = spec[i];

        // MV / Lyrics / CharAnim
        if (_isVideo && _videoMv.IsOpen)
        {
            // 重置颜色方案，清除歌词模式下的黄色高亮
            for (int i = 0; i < 11; i++) _lrcLabels[i].SetScheme(_schemeAnim);
            bool playing = _midiPath != null ? _midi.IsPlaying : _player.IsPlaying;
            if (playing)
            {
                var frame = _videoMv.ReadFrame();
                if (frame != null)
                    for (int i = 0; i < 11 && i < frame.Length; i++)
                        _lrcLabels[i].Text = frame[i];
                else
                    _isVideo = false;
            }
        }
        else if (_lrc != null) { for (int i = 0; i < 11; i++) _lrcLabels[i].SetScheme(i == 5 ? _schemeCurr : _schemeDim); var idx = _lrc.GetIndex(_player.Position); for (int i = 0; i < 11; i++) { var li = idx - 5 + i; _lrcLabels[i].Text = li >= 0 && li < _lrc.Lines.Count ? _lrc.Lines[li].Text : ""; } }
        else
        {
            for (int i = 0; i < 11; i++) _lrcLabels[i].SetScheme(_schemeAnim);
            if (_midiPath != null ? _midi.IsPlaying : _player.IsPlaying)
                _animTime = (_midiPath != null ? _midi.Position : _player.Position).TotalSeconds;
            var fidx = _charAnim.FrameCount > 0 ? (int)(_animTime * _charAnim.Fps) % _charAnim.FrameCount : 0;
            var frame = _charAnim.GetFrame(fidx);
            for (int i = 0; i < 11; i++) _lrcLabels[i].Text = frame[i];
        }
    }

    private static string LoadLastDir() { try { return File.Exists(LastDirFile) ? File.ReadAllText(LastDirFile).Trim() : ""; } catch { return ""; } }
    private static void SaveLastDir(string dir) { try { Directory.CreateDirectory(ConfigDir); File.WriteAllText(LastDirFile, dir); } catch { } }
    private static bool IsMusicFile(string path) => Path.GetExtension(path).ToLower() is ".mp3" or ".flac" or ".wav" or ".ogg" or ".aac" or ".wma" or ".m4a" or ".ape" or ".mid" or ".midi" or ".mp4" or ".mkv" or ".avi" or ".webm";
    private static bool IsVideoFile(string path) => Path.GetExtension(path).ToLower() is ".mp4" or ".mkv" or ".avi" or ".webm";
}
