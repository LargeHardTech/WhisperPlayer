using System.Text.RegularExpressions;

namespace Whisper.Core;

/// <summary>
/// LRC 歌词解析
/// </summary>
public class LrcParser
{
    public record LyricLine(TimeSpan Time, string Text);

    public string? Title { get; private set; }
    public string? Artist { get; private set; }
    public List<LyricLine> Lines { get; } = [];

    public static LrcParser Parse(string lrcPath)
    {
        var parser = new LrcParser();
        var lines = File.ReadAllLines(lrcPath);

        foreach (var raw in lines)
        {
            // 元数据
            var meta = Regex.Match(raw, @"\[ti:(.+)\]", RegexOptions.IgnoreCase);
            if (meta.Success) { parser.Title = meta.Groups[1].Value; continue; }

            meta = Regex.Match(raw, @"\[ar:(.+)\]", RegexOptions.IgnoreCase);
            if (meta.Success) { parser.Artist = meta.Groups[1].Value; continue; }

            // 时间标签 [mm:ss.xx]
            var matches = Regex.Matches(raw, @"\[(\d+):(\d+(?:\.\d+)?)\]");
            var text = Regex.Replace(raw, @"\[.*?\]", "").Trim();

            foreach (Match m in matches)
            {
                var min = int.Parse(m.Groups[1].Value);
                var sec = double.Parse(m.Groups[2].Value);
                var time = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                parser.Lines.Add(new LyricLine(time, text));
            }
        }

        // 用稳定的 LINQ OrderBy 排序（同时间戳保留文件顺序，避免原文/翻译错位）
        var sorted = parser.Lines.OrderBy(l => l.Time).ToList();
        parser.Lines.Clear();
        foreach (var l in sorted) parser.Lines.Add(l);
        return parser;
    }

    /// <summary>
    /// 根据当前播放时间获取应显示的歌词索引。
    /// 如果当前行是空行，自动跳到同时间戳的下一个有内容的行。
    /// </summary>
    public int GetIndex(TimeSpan position)
    {
        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            if (Lines[i].Time <= position)
            {
                // 空行：找同时间戳非空行
                if (string.IsNullOrWhiteSpace(Lines[i].Text))
                {
                    var t = Lines[i].Time;
                    for (int j = i + 1; j < Lines.Count && Lines[j].Time == t; j++)
                        if (!string.IsNullOrWhiteSpace(Lines[j].Text)) return j;
                    // 后面没有就往前找
                    for (int j = i - 1; j >= 0 && Lines[j].Time == t; j--)
                        if (!string.IsNullOrWhiteSpace(Lines[j].Text)) return j;
                }
                return i;
            }
        }
        return -1;
    }
}
