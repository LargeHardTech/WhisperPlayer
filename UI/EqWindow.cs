using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
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

    public EqWindow(Whisper.Core.Player player)
    {
        Title = "均衡器";
        _player = player;
        X = Pos.Center(); Y = Pos.Center();
        Width = 58; Height = 11;

        // Enable
        _cbEnable = new CheckBox { Text = "启用", X = 1, Y = 0, Value = player.EqEnabled ? CheckState.Checked : CheckState.UnChecked };
        _cbEnable.ValueChanged += (_, _) => {
            var on = _cbEnable.Value == CheckState.Checked;
            _player.ToggleEq(on);
            SetControlsEnabled(!on);
        };
        Add(_cbEnable);

        // Preset as prev/name/next buttons
        var lblPr = new Label { Text = "预设:", X = 8, Y = 0 };
        Add(lblPr);

        _btnPrev = new Button { Text = "<", X = 18, Y = 0, Width = 3, Height = 1, NoPadding = true };
        _btnPrev.Accepting += (_, _) => CyclePreset(-1);
        Add(_btnPrev);

        _lblPresetName = new Label { Text = "", X = 23, Y = 0, Width = 14 };
        Add(_lblPresetName);

        _btnNext = new Button { Text = ">", X = 38, Y = 0, Width = 3, Height = 1, NoPadding = true };
        _btnNext.Accepting += (_, _) => CyclePreset(1);
        Add(_btnNext);

        // Find current preset
        _presetIdx = FindPreset(player.EqGains);
        _lblPresetName.Text = _presetIdx >= 0 ? Player.EqPresets[_presetIdx].Name : "自定义";

        // 10 bands
        for (int i = 0; i < 10; i++)
        {
            var x = 1 + i * 5;
            var freq = Player.EqFreqs[i];
            var hdr = new Label { Text = freq >= 1000 ? $"{freq / 1000}k" : $"{freq}", X = x, Y = 2 };
            Add(hdr);

            var lbl = new Label { Text = "", X = x, Y = 3, Width = 4 };
            _lblGains[i] = lbl;
            Add(lbl);

            var band = i;
            var up = new Button { Text = "+", X = x, Y = 4, Width = 3, Height = 1, NoPadding = true };
            up.Accepting += (_, _) => { player.SetEqBand(band, player.EqGains[band] + 1); RefreshGains(); _presetIdx = -1; _lblPresetName.Text = "自定义"; };
            _btns[i, 0] = up;
            Add(up);

            var dn = new Button { Text = "-", X = x, Y = 5, Width = 3, Height = 1, NoPadding = true };
            dn.Accepting += (_, _) => { player.SetEqBand(band, player.EqGains[band] - 1); RefreshGains(); _presetIdx = -1; _lblPresetName.Text = "自定义"; };
            _btns[i, 1] = dn;
            Add(dn);
        }

        RefreshGains();  // 修复重新进入时颜色不正确的 bug

        // OK + Reset
        var btnOk = new Button { NoPadding = true, Text = "确定", X = 18, Y = 7 };
        btnOk.Accepting += (_, _) => Application.RequestStop(this);
        Add(btnOk);

        var btnReset = new Button { NoPadding = true, Text = "重置", X = 32, Y = 7 };
        btnReset.Accepting += (_, _) => {
            for (int i = 0; i < 10; i++) _player.SetEqBand(i, 0);
            _presetIdx = 0;
            _lblPresetName.Text = "平直";
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
                > 0 => new Attribute(Color.BrightGreen, Color.Black),
                < 0 => new Attribute(Color.BrightRed, Color.Black),
                _ => new Attribute(Color.Gray, Color.Black)
            };
            _lblGains[i].SetScheme(new Scheme { Normal = c });
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
        Application.Run(w, null);
        _isOpen = false;
    }
}
