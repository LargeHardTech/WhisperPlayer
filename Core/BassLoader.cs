using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Whisper.Core;

public static class BassLoader
{
    public static readonly string LibDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".whisper");

    public static void Init()
    {
        // 处理 .whisper 路径异常（同名文件存在 / 权限不足）
        try { Directory.CreateDirectory(LibDir); }
        catch (IOException)
        {
            if (File.Exists(LibDir)) File.Delete(LibDir);
            Directory.CreateDirectory(LibDir);
        }

        var bassPath = Path.Combine(LibDir, "bass.dll");
        ExtractDll("bass.dll", bassPath);

        var bassFxPath = Path.Combine(LibDir, "bass_fx.dll");
        ExtractDll("bass_fx.dll", bassFxPath);

        var bassAacPath = Path.Combine(LibDir, "bass_aac.dll");
        ExtractDll("bass_aac.dll", bassAacPath);

        // 关键：先加载 bass.dll，再加载 bass_fx.dll、bass_aac.dll，确保共用同一个 bass 实例
        if (File.Exists(bassPath))
            NativeLibrary.Load(bassPath);

        if (File.Exists(bassFxPath))
            NativeLibrary.Load(bassFxPath);

        if (File.Exists(bassAacPath))
            NativeLibrary.Load(bassAacPath);

        // Brotli 压缩的 ffmpeg.exe → 启动时解压一次
        ExtractFfmpeg();

        if (!ManagedBass.Bass.Init(-1, 44100, ManagedBass.DeviceInitFlags.Default))
        {
            var err = ManagedBass.Bass.LastError;
            throw new Exception($"BASS init failed: {err}");
        }
    }

    private static bool ExtractDll(string name, string target)
    {
        if (File.Exists(target)) return false;

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Whisper.{name}");
        if (stream == null) return false;
        using var fs = File.Create(target);
        stream.CopyTo(fs);
        return true;
    }

    private static void ExtractFfmpeg()
    {
        var target = Path.Combine(LibDir, "ffmpeg.exe");
        if (File.Exists(target)) return;

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Whisper.ffmpeg.exe.br");
        if (stream == null) return;

        using var decompressed = new BrotliStream(stream, CompressionMode.Decompress);
        using var fs = File.Create(target);
        decompressed.CopyTo(fs);
    }

    public static void Free()
    {
        ManagedBass.Bass.Free();
    }
}
