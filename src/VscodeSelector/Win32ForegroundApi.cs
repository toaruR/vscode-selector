using System.Runtime.InteropServices;
using System.Text;

namespace VscodeSelector;

/// <summary>IForegroundApi の実 Win32 実装 (設計 §7)。</summary>
internal sealed class Win32ForegroundApi : IForegroundApi
{
    private const uint AsfwAny = 0xFFFFFFFF;
    private const int SwRestore = 9;
    private const uint FlashwAll = 3;
    private const uint FlashwTimerNoFg = 12;
    private const uint KeyeventfKeyUp = 2;

    public bool IsWindow(nint hwnd) => N.IsWindow(hwnd);

    public uint GetWindowProcessId(nint hwnd)
    {
        N.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    public string GetClassName(nint hwnd)
    {
        var buf = new StringBuilder(256);
        int n = N.GetClassNameW(hwnd, buf, buf.Capacity);
        return n > 0 ? buf.ToString(0, n) : "";
    }

    public bool IsIconic(nint hwnd) => N.IsIconic(hwnd);
    public void ShowWindowRestore(nint hwnd) => N.ShowWindow(hwnd, SwRestore);
    public bool SetForegroundWindow(nint hwnd) => N.SetForegroundWindow(hwnd);
    public void AllowSetForegroundWindowAny() => N.AllowSetForegroundWindow(AsfwAny);
    public void KeybdEvent(byte vk, bool keyUp) => N.keybd_event(vk, 0, keyUp ? KeyeventfKeyUp : 0, 0);

    public uint GetForegroundThreadId()
    {
        nint fg = N.GetForegroundWindow();
        return fg == 0 ? 0 : N.GetWindowThreadProcessId(fg, out _);
    }

    public uint GetCurrentThreadId() => N.GetCurrentThreadId();
    public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach) => N.AttachThreadInput(idAttach, idAttachTo, attach);
    public bool BringWindowToTop(nint hwnd) => N.BringWindowToTop(hwnd);

    public void FlashWindow(nint hwnd)
    {
        var fi = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = hwnd,
            dwFlags = FlashwAll | FlashwTimerNoFg, uCount = 3, dwTimeout = 0,
        };
        N.FlashWindowEx(ref fi);
    }

    public void Sleep(int ms) => Thread.Sleep(ms);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize; public nint hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout;
    }

    private static class N
    {
        [DllImport("user32.dll")] public static extern bool IsWindow(nint h);
        [DllImport("user32.dll")] public static extern bool IsIconic(nint h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(nint h, int cmd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint h);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint pid);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);
        [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint h, out uint pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(nint h);
        [DllImport("user32.dll")] public static extern bool FlashWindowEx(ref FLASHWINFO fi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(nint h, StringBuilder sb, int max);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    }
}
