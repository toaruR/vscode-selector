using System.Runtime.InteropServices;

namespace VscodeSelector;

/// <summary>
/// 自プロセス以外で最後に前面になったウィンドウを記録する。
/// ボタン押下時は自ウィンドウが前面になるため、押す前の前面ウィンドウ判定に使う。
/// UI スレッドで生成すること (WINEVENT_OUTOFCONTEXT のコールバックは生成スレッドのメッセージループで届く)。
/// </summary>
internal sealed class ForegroundTracker : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutOfContext = 0;

    private delegate void WinEventProc(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint h, out uint pid);

    private static readonly uint SelfPid = (uint)Environment.ProcessId;

    // GC 回収防止のためフック解除まで保持する。
    private readonly WinEventProc _proc;
    private nint _hook;

    public ForegroundTracker()
    {
        _proc = (_, _, hwnd, _, _, _, _) => Record(hwnd);
        Record(GetForegroundWindow());
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, _proc, 0, 0, WineventOutOfContext);
    }

    /// <summary>自プロセス以外で最後に前面になったウィンドウ。未記録なら 0。</summary>
    public nint LastExternal { get; private set; }

    private void Record(nint hwnd)
    {
        if (hwnd == 0) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != SelfPid) LastExternal = hwnd;
    }

    public void Dispose()
    {
        if (_hook == 0) return;
        UnhookWinEvent(_hook);
        _hook = 0;
    }
}
