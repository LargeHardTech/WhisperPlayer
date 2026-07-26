using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

namespace Whisper.Core;

/// <summary>支持把文件拖到控制台窗口自动添加到播放列表</summary>
internal static class ConsoleDragDrop
{
    private const int GWLP_WNDPROC = -4;
    private const uint WM_DROPFILES = 0x0233;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    private static IntPtr _hwnd;
    private static IntPtr _originalProc;
    private static WndProcDelegate? _newProc;  // 必须保活防 GC

    public static void Init(ConcurrentQueue<string> filesQueue)
    {
        _hwnd = GetConsoleWindow();
        if (_hwnd == IntPtr.Zero) return;

        _newProc = (hWnd, msg, wParam, lParam) =>
        {
            if (msg == WM_DROPFILES)
            {
                var hDrop = wParam;
                var count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    var len = DragQueryFileW(hDrop, i, null, 0);
                    var sb = new StringBuilder((int)len + 1);
                    DragQueryFileW(hDrop, i, sb, (uint)sb.Capacity);
                    filesQueue.Enqueue(sb.ToString());
                }
                DragFinish(hDrop);
                return IntPtr.Zero;
            }
            return CallWindowProc(_originalProc, hWnd, msg, wParam, lParam);
        };

        _originalProc = SetWindowLongPtrCompat(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newProc));
        DragAcceptFiles(_hwnd, true);
    }

    public static void Shutdown()
    {
        if (_hwnd != IntPtr.Zero && _originalProc != IntPtr.Zero)
        {
            SetWindowLongPtrCompat(_hwnd, GWLP_WNDPROC, _originalProc);
            DragAcceptFiles(_hwnd, false);
        }
    }

    private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return (IntPtr)SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
    }
}
