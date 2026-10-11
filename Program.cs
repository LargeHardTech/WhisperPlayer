using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading;
using Whisper.Core;
using Whisper.UI;
using System.Runtime.InteropServices;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Whisper;

internal static class Program
{
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll")] private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
    [DllImport("user32.dll")] private static extern bool ActivateKeyboardLayout(IntPtr hkl, uint Flags);
    public static Player Player { get; } = new();
    public static string[] Args { get; private set; } = [];
    public static ConcurrentQueue<string> ExternalArgsQueue { get; } = new();

    private static Mutex? _mutex;
    private const string MutexName = "Whisper_SingleInstance";
    private const string PipeName = "Whisper_Pipe";

    static void Main(string[] args)
    {
        Console.Title = "Whisper Player";   
        BassLoader.Init();
        
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // 已有实例运行：把 args 通过命名管道传过去，然后静默退出
            SendArgsToRunningInstance(args);
            return;
        }

        // 老实例：启动管道服务器监听新实例的请求
        StartPipeServer();

        // 启用控制台窗口拖拽支持
        ConsoleDragDrop.Init(ExternalArgsQueue);

        Args = args;
        // v2.5.0 按钮默认 Opaque 阴影会吃 1px 空间，Height=1 的按钮会被裁成 0 → 关掉
        Terminal.Gui.Configuration.ButtonSettings.Current =
            Terminal.Gui.Configuration.ButtonSettings.Current with { DefaultShadow = ShadowStyles.None };
        try { Application.Init(); }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".whisper", "bass_log.txt"), $"Init failed: {ex}\n");
            throw;
        }
        try
        {
            // 切换到英文输入法（避免快捷键冲突）
            var hkl = GetKeyboardLayout(0);
            var enHkl = LoadKeyboardLayout("00000409", 1); // US English
            ActivateKeyboardLayout(enHkl, 0);

            Application.Run(new MainView(), null);

            // 恢复
            ActivateKeyboardLayout(hkl, 0);
        }
        finally
        {
            ConsoleDragDrop.Shutdown();
            Player.Dispose();
            Application.Shutdown();
        }
    }

    private static void SendArgsToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            foreach (var arg in args)
                writer.WriteLine(arg);
        }
        catch { }
    }

    private static void StartPipeServer()
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var p = line.Trim();
                        if (!string.IsNullOrEmpty(p))
                            ExternalArgsQueue.Enqueue(p);
                    }
                }
                catch
                {
                    break;  // 主程序退出时跳出
                }
            }
        }) { IsBackground = true };
        thread.Start();
    }
}
