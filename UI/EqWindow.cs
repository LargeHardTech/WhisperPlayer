using Terminal.Gui;
using Whisper.Core;

namespace Whisper.UI;

public class EqWindow : Window
{
    private static bool _isOpen;
    private readonly Whisper.Core.Player _player;
    private readonly CheckBox _cbEnable;
    private readonly Label _lblPresetName;
    private readonly Label[] _lblGains = new Label[10];
    private readonly Button[,] _btns = new Button[10, 2];
    private Button? _btnPrev, _btnNext;
    private int _presetIdx;
    private bool _suppress;

    public EqWindow(Whisper.Core.Player player) : base("Equalizer")
    {
        _player = player;
        X = Pos.Center(); Y = Pos.Center();
        Width = 58; Height = 11;

        // Enable
        _cbEnable = new CheckBox(1, 0, "Enable") { Checked = player.EqEnabled };
        _cbEnable.Toggled += _ => {
            _player.ToggleEq(_cbEnable.Checked);
            SetControlsEnabled(!_cbEnable.Checked);
        };
        Add(_cbEnable);

        // Preset as prev/name/next buttons
        var lblPr = new Label(10, 0, "Preset:");
        Add(lblPr);

        _btnPrev = new Button(18, 0, "<") { Width = 3 };
        _btnPrev.Clicked += () => CyclePreset(-1);
        Add(_btnPrev);

        _lblPresetName = new Label(23, 0, "") { Width = 14 };
        Add(_lblPresetName);

        _btnNext = new Button(38, 0, ">") { Width = 3 };
        _btnNext.Clicked += () => CyclePreset(1);
        Add(_btnNext);

        // Find current preset
        _presetIdx = FindPreset(player.EqGains);
        _lblPresetName.Text = _presetIdx >= 0 ? Player.EqPresets[_presetIdx].Name : "Custom";

        // 10 bands
        for (int i = 0; i < 10; i++)
        {
            var x = 1 + i * 5;
            var freq = Player.EqFreqs[i];
            var hdr = new Label(x, 2, freq >= 1000 ? $"{freq / 1000}k" : $"{freq}");
            Add(hdr);

            var lbl = new Label(x, 3, "") { Width = 4 };
            _lblGains[i] = lbl;
            Add(lbl);

            var band = i;
            var up = new Button(x, 4, "+") { Width = 3 };
            up.Clicked += () => { player.SetEqBand(band, player.EqGains[band] + 1); RefreshGains(); _presetIdx = -1; _lblPresetName.Text = "Custom"; };
            _btns[i, 0] = up;
            Add(up);

            var dn = new Button(x, 5, "-") { Width = 3 };
            dn.Clicked += () => { player.SetEqBand(band, player.EqGains[band] - 1); RefreshGains(); _presetIdx = -1; _lblPresetName.Text = "Custom"; };
            _btns[i, 1] = dn;
            Add(dn);
        }

        RefreshGains();  // 修复重新进入时颜色不正确的 bug

        // OK + Reset
        var btnOk = new Button(20, 7, "OK");
        btnOk.Clicked += () => Application.RequestStop(this);
        Add(btnOk);

        var btnReset = new Button(32, 7, "Reset");
        btnReset.Clicked += () => {
            for (int i = 0; i < 10; i++) _player.SetEqBand(i, 0);
            _presetIdx = 0;
            _lblPresetName.Text = "Flat";
            RefreshGains();
        };
        Add(btnReset);

        SetControlsEnabled(!player.EqEnabled);
    }

    private void CyclePreset(int dir)
    {
        if (_presetIdx < 0) _presetIdx = 0;
        _presetIdx = (_presetIdx + dir + Player.EqPresets.Length) % Player.EqPresets.Length;
        _player.ApplyEqPreset(_presetIdx);
        _lblPresetName.Text = Player.EqPresets[_presetIdx].Name;
        RefreshGains();
    }

    private static int FindPreset(float[] gains)
    {
        for (int p = 0; p < Player.EqPresets.Length; p++)
        {
            bool match = true;
            for (int i = 0; i < 10; i++)
                if (Math.Abs(gains[i] - Player.EqPresets[p].Gains[i]) > 0.01f)
                    { match = false; break; }
            if (match) return p;
        }
        return -1;
    }

    private static string GainText(float g) => g >= 0 ? $"+{g:F0}" : $"{g:F0}";

    private void RefreshGains()
    {
        for (int i = 0; i < 10; i++)
        {
            _lblGains[i].Text = GainText(_player.EqGains[i]);
            var c = _player.EqGains[i] switch
            {
                > 0 => Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black),
                < 0 => Application.Driver.MakeAttribute(Color.BrightRed, Color.Black),
                _ => Application.Driver.MakeAttribute(Color.Gray, Color.Black)
            };
            _lblGains[i].ColorScheme = new ColorScheme { Normal = c };
        }
    }

    private void SetControlsEnabled(bool en)
    {
        _btnPrev!.Enabled = en;
        _btnNext!.Enabled = en;
        for (int i = 0; i < 10; i++)
        {
            _btns[i, 0].Enabled = en;
            _btns[i, 1].Enabled = en;
        }
    }

    public static void Show(Whisper.Core.Player player)
    {
        if (_isOpen) return;
        _isOpen = true;
        var w = new EqWindow(player);
        Application.Run(w);
        _isOpen = false;
    }
}
