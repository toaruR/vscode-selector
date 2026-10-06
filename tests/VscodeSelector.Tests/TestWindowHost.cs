using System.Runtime.InteropServices;

namespace VscodeSelector.Tests;

/// <summary>専用 STA スレッド上にダミー最上位ウィンドウを作成する。破棄でスレッド終了。</summary>
internal sealed class TestWindowHost : IDisposable
{
    private delegate nint WndProc(nint h, uint m, nint w, nint l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName; public string lpszClassName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassW(ref WNDCLASS c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClassW(string n, nint inst);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint p);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint h, int cmd);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG m, nint h, uint a, uint b);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref MSG m);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint t, uint m, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? n);

    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WM_QUIT = 0x0012;
    private const int ERROR_CLASS_ALREADY_EXISTS = 1410;
    private const int SW_SHOWNOACTIVATE = 4, SW_SHOWMINNOACTIVE = 7;

    private readonly Thread _thread;
    private readonly uint _threadId;
    private readonly List<nint> _hwnds = new();
    private readonly WndProc _proc = DefWindowProcW;

    public IReadOnlyList<nint> Handles => _hwnds;

    /// <param name="windows">(クラス名, タイトル, 最小化)</param>
    public TestWindowHost(IEnumerable<(string ClassName, string Title, bool Minimized)> windows)
    {
        var specs = windows.ToList();
        Exception? error = null;
        uint tid = 0;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            var inst = GetModuleHandleW(null);
            var classes = new List<string>();
            try
            {
                tid = GetCurrentThreadId();
                foreach (var name in specs.Select(s => s.ClassName).Distinct())
                {
                    var wc = new WNDCLASS
                    {
                        lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
                        hInstance = inst,
                        lpszClassName = name,
                    };
                    if (RegisterClassW(ref wc) == 0 && Marshal.GetLastWin32Error() != ERROR_CLASS_ALREADY_EXISTS)
                        throw new InvalidOperationException($"RegisterClass failed: {Marshal.GetLastWin32Error()}");
                    classes.Add(name);
                }
                foreach (var s in specs)
                {
                    var h = CreateWindowExW(0, s.ClassName, s.Title, WS_OVERLAPPEDWINDOW, 100, 100, 300, 200, 0, 0, inst, 0);
                    if (h == 0) throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
                    _hwnds.Add(h);
                    ShowWindow(h, s.Minimized ? SW_SHOWMINNOACTIVE : SW_SHOWNOACTIVATE);
                }
            }
            catch (Exception ex) { error = ex; }
            ready.Set();
            if (error is null)
                while (GetMessageW(out var m, 0, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessageW(ref m); }
            foreach (var h in _hwnds) DestroyWindow(h);
            foreach (var c in classes) UnregisterClassW(c, inst);
        });
        _thread.IsBackground = true;
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
        _threadId = tid;
        if (error is not null) { _thread.Join(); throw error; }
    }

    public void Dispose()
    {
        if (_thread.IsAlive)
        {
            PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
            _thread.Join(5000);
        }
    }
}
