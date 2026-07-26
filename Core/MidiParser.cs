namespace Whisper.Core;

/// <summary>简易 MIDI 解析器：提取音符事件用于频谱可视化</summary>
public class MidiParser
{
    public record NoteEvent(long Tick, bool On, int Note, int Velocity);

    public List<NoteEvent> Notes { get; } = [];
    public int TicksPerQuarter { get; private set; } = 480;
    public long TickLength { get; private set; }

    public static MidiParser Parse(string path)
    {
        var parser = new MidiParser();
        var data = File.ReadAllBytes(path);
        int pos = 0;

        // Chunk header
        if (ReadStr(data, ref pos, 4) != "MThd") throw new Exception("Not a MIDI file");
        pos += 4; // length
        pos += 2; // format
        var ntracks = (int)ReadU16(data, ref pos);
        parser.TicksPerQuarter = (int)ReadU16(data, ref pos);

        // Read tracks
        long absoluteTick = 0;
        for (int t = 0; t < ntracks; t++)
        {
            if (ReadStr(data, ref pos, 4) != "MTrk") throw new Exception("Expected MTrk");
            var len = (int)ReadU32(data, ref pos);
            var end = pos + len;
            absoluteTick = 0;

            byte runningStatus = 0;
            while (pos < end)
            {
                var delta = ReadVarLen(data, ref pos);
                absoluteTick += delta;

                byte status = data[pos];
                if (status < 0x80) status = runningStatus;
                else { runningStatus = status; pos++; }

                int channel = status & 0x0F;
                int msgType = status & 0xF0;

                if (msgType == 0x90) // Note On
                {
                    var note = data[pos++];
                    var vel = data[pos++];
                    parser.Notes.Add(new NoteEvent(absoluteTick, vel > 0, note, vel));
                }
                else if (msgType == 0x80) // Note Off
                {
                    var note = data[pos++];
                    pos++; // velocity (ignore)
                    parser.Notes.Add(new NoteEvent(absoluteTick, false, note, 0));
                }
                else if (msgType == 0xB0) { pos += 2; } // Controller
                else if (msgType == 0xC0) { pos++; }    // Program Change
                else if (msgType == 0xE0) { pos += 2; } // Pitch Bend
                else if (status == 0xFF) // Meta
                {
                    var metaType = data[pos++];
                    var metaLen = ReadVarLen(data, ref pos);
                    if (metaType == 0x2F) break; // End of Track
                    pos += (int)metaLen;
                }
                else if (status == 0xF0 || status == 0xF7) // SysEx
                {
                    var sysexLen = ReadVarLen(data, ref pos);
                    pos += (int)sysexLen;
                }
            }
        }

        if (parser.Notes.Count > 0 && parser.TicksPerQuarter > 0)
            parser.TickLength = parser.Notes.Max(n => n.Tick);

        return parser;
    }

    /// <summary>获取指定时刻的活跃音符列表（返回 MIDI note 号）</summary>
    public List<int> GetActiveNotes(long tick)
    {
        var active = new Dictionary<int, bool>();
        foreach (var n in Notes)
        {
            if (n.Tick > tick) break;
            if (n.On) active[n.Note] = true;
            else active.Remove(n.Note);
        }
        return active.Keys.ToList();
    }

    /// <summary>MIDI tick → 秒（假设 120 BPM = 2 ticks/ms at 480 TPQ）</summary>
    public double TickToSeconds(long tick) => tick * 0.5 / (TicksPerQuarter > 0 ? TicksPerQuarter : 480) * (60.0 / 120);

    /// <summary>秒 → MIDI tick</summary>
    public long SecondsToTick(double sec) => (long)(sec * TicksPerQuarter * 120 / 60.0);

    // MIDI note → Hz: A4=69 → 440Hz
    private static readonly double[] NoteFreqs = Enumerable.Range(0, 128)
        .Select(n => 440.0 * Math.Pow(2, (n - 69) / 12.0)).ToArray();

    /// <summary>将活跃 + 释放中音符映射到频谱（拖尾衰减）</summary>
    public float[] GetSpectrum(long tick, int bands = 16)
    {
        var result = new float[bands + 1];
        var state = new Dictionary<int, (long onTick, long offTick, bool isOn)>();
        long window = TicksPerQuarter * 3;

        // 单遍扫描建立当前状态
        foreach (var n in Notes)
        {
            if (n.Tick > tick) break;
            if (n.On) state[n.Note] = (n.Tick, 0, true);
            else if (state.TryGetValue(n.Note, out var s) && s.isOn)
                state[n.Note] = (s.onTick, n.Tick, false);
        }

        foreach (var (note, s) in state)
        {
            float amp;
            if (s.isOn) amp = 0.25f;
            else
            {
                long dt = tick - s.offTick;
                if (dt > window) continue;
                amp = 0.2f * (1f - dt / (float)window);
            }
            var bin = (Math.Clamp(note, 36, 96) - 36) * (bands - 1) / 60;
            if (bin >= 0 && bin < bands) result[bin + 1] = Math.Min(1f, result[bin + 1] + amp);
        }

        result[0] = Math.Min(1f, state.Count * 0.15f);
        return result;
    }

    // === Helpers ===
    private static uint ReadU16(byte[] data, ref int pos) { var v = (uint)((data[pos] << 8) | data[pos + 1]); pos += 2; return v; }
    private static uint ReadU32(byte[] data, ref int pos) { var v = (uint)((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]); pos += 4; return v; }
    private static string ReadStr(byte[] data, ref int pos, int len) { var s = System.Text.Encoding.ASCII.GetString(data, pos, len); pos += len; return s; }
    private static long ReadVarLen(byte[] data, ref int pos) { long v = 0; byte b; do { b = data[pos++]; v = (v << 7) | (uint)(b & 0x7F); } while ((b & 0x80) != 0 && pos < data.Length); return v; }
}
