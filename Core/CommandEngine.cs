namespace Whisper.Core;

/// <summary>可扩展命令引擎：注册/执行命令，UI 无关</summary>
public class CommandEngine
{
    private readonly Dictionary<string, (Action<string?> Handler, string Desc)> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册命令（大小写不敏感）</summary>
    public void Register(string name, Action<string?> handler, string description = "")
    {
        _commands[name] = (handler, description);
    }

    /// <summary>解析 "cmd arg" 并执行，返回是否找到命令</summary>
    public bool Execute(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var cmd = parts[0];
        var arg = parts.Length > 1 ? parts[1] : null;

        if (_commands.TryGetValue(cmd, out var entry))
        {
            entry.Handler(arg);
            return true;
        }
        return false;
    }

    /// <summary>获取所有已注册命令名</summary>
    public IEnumerable<string> Commands => _commands.Keys;

    /// <summary>获取格式化的帮助文本（命令 + 中文说明）</summary>
    public string GetHelpText(int padding = 8)
    {
        var maxLen = _commands.Keys.Max(k => k.Length);
        return string.Join("\n", _commands
            .OrderBy(x => x.Key)
            .Select(x => $"  {x.Key.PadRight(maxLen + 2)}{x.Value.Desc}"));
    }
}
