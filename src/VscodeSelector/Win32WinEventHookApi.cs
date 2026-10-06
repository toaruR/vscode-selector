using System.Runtime.InteropServices;

namespace VscodeSelector;

/// <summary>IWinEventHookApi の実 Win32 実装 (設計 §6)。</summary>
internal sealed class Win32WinEventHookApi : IWinEventHookApi
{
    private const uint WineventOutOfContext = 0;
    private const uint WmQuit = 0x0012;

    private delegate void WinEventProc(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll", EntryPoint = "SetWinEventHook")] private static extern nint SetWinEventHookNative(uint min, uint max, nint mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll", EntryPoint = "UnhookWinEvent")] private static extern bool UnhookWinEventNative(nint hook);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG m, nint h, uint a, uint b);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref MSG m);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint t, uint m, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    // GC 回収防止のためフック解除まで保持する。
    private readonly Dictionary<nint, WinEventProc> _procs = new();

    public nint SetWinEventHook(uint eventMin, uint eventMax, WinEventCallback callback)
    {
        WinEventProc proc = (_, evt, hwnd, idObject, idChild, _, _) => callback(evt, hwnd, idObject, idChild);
        nint h = SetWinEventHookNative(eventMin, eventMax, 0, proc, 0, 0, WineventOutOfContext);
        if (h != 0) lock (_procs) _procs[h] = proc;
        return h;
    }

    public bool UnhookWinEvent(nint hook)
    {
        bool ok = UnhookWinEventNative(hook);
        lock (_procs) _procs.Remove(hook);
        return ok;
    }

    public void RunMessageLoop()
    {
        while (GetMessageW(out var m, 0, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessageW(ref m); }
    }

    public void QuitMessageLoop(uint nativeThreadId) => PostThreadMessageW(nativeThreadId, WmQuit, 0, 0);

    public uint GetCurrentNativeThreadId() => GetCurrentThreadId();
}
