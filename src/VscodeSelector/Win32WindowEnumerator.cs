using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VscodeSelector;

/// <summary>EnumWindows 呼び出しの抽象。失敗時 false を返し lastError に GetLastError 値を設定する。</summary>
internal delegate bool EnumWindowsFunc(Func<nint, bool> callback, out int lastError);

/// <summary>EnumWindows / DWM / QueryFullProcessImageName による IWindowEnumerator (設計 §5.1)。</summary>
public sealed class Win32WindowEnumerator : IWindowEnumerator
{
    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80;
    private const int DwmwaCloaked = 14;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly Func<IReadOnlyCollection<string>> _extraNames;
    private readonly EnumWindowsFunc _enumWindows;
    private readonly Action<string>? _log;

    public Win32WindowEnumerator(Func<IReadOnlyCollection<string>> extraProcessNames, Action<string>? log = null)
        : this(extraProcessNames, NativeEnumWindows, log) { }

    public Win32WindowEnumerator(IReadOnlyCollection<string> extraProcessNames, Action<string>? log = null)
        : this(() => extraProcessNames, NativeEnumWindows, log) { }

    internal Win32WindowEnumerator(
        Func<IReadOnlyCollection<string>> extraProcessNames, EnumWindowsFunc enumWindows, Action<string>? log = null)
    {
        _extraNames = extraProcessNames;
        _enumWindows = enumWindows;
        _log = log;
    }

    public IReadOnlyList<RawWindow> Enumerate()
    {
        var extra = _extraNames();
        var result = new List<RawWindow>();
        var exeCache = new Dictionary<uint, string?>();
        bool ok = _enumWindows(hwnd =>
        {
            try
            {
                var c = Observe(hwnd, exeCache);
                if (c is not null && WindowFilter.Evaluate(c.Value, extra, _log) is { } w) result.Add(w);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log?.Invoke($"window 0x{hwnd:X} skipped: {ex.Message}");
            }
            return true;
        }, out int err);
        if (!ok) throw new EnumerationException(err);
        return result;
    }

    [ThreadStatic] private static char[]? _classBuf;

    private WindowCandidate? Observe(nint hwnd, Dictionary<uint, string?> exeCache)
    {
        // 安価な判定を先に行い、Filter が必ず除外するウィンドウは高コスト API を呼ばず打ち切る (WindowFilter.Evaluate と同値)。
        if (!Native.IsWindowVisible(hwnd)) return null;
        var ClassBuf = _classBuf ??= new char[256];
        int n = Native.GetClassNameW(hwnd, ClassBuf, ClassBuf.Length);
        if (n != WindowFilter.RequiredClassName.Length
            || !new ReadOnlySpan<char>(ClassBuf, 0, n).SequenceEqual(WindowFilter.RequiredClassName)) return null;
        if (!Native.IsWindow(hwnd)) return null;
        if (Native.GetWindow(hwnd, GwOwner) != 0) return null;
        if ((Native.GetWindowLongPtr(hwnd, GwlExStyle) & WsExToolWindow) != 0) return null;
        if (Native.DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) != 0) cloaked = 0;
        if (cloaked != 0) return null;
        int len = Native.GetWindowTextLength(hwnd);
        if (len <= 0) return null;
        var sb = new StringBuilder(len + 1);
        Native.GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString();
        if (title.Length == 0) return null;
        bool iconic = Native.IsIconic(hwnd);
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (!exeCache.TryGetValue(pid, out var exe)) exeCache[pid] = exe = QueryExeName(pid);
        return new WindowCandidate(hwnd, pid, WindowFilter.RequiredClassName, title, true, false, false,
            false, iconic, exe);
    }

    private static string? QueryExeName(uint pid)
    {
        if (pid == 0) return null;
        nint h = Native.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == 0) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (!Native.QueryFullProcessImageName(h, 0, sb, ref size)) return null;
            return Path.GetFileName(sb.ToString(0, size));
        }
        finally { Native.CloseHandle(h); }
    }

    private static bool NativeEnumWindows(Func<nint, bool> callback, out int lastError)
    {
        bool ok = Native.EnumWindows((h, _) => callback(h), 0);
        lastError = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }

    private delegate bool EnumProc(nint hwnd, nint lParam);

    private static class Native
    {
        [DllImport("user32.dll", SetLastError = true)] public static extern bool EnumWindows(EnumProc cb, nint lParam);
        [DllImport("user32.dll")] public static extern bool IsWindow(nint h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint h);
        [DllImport("user32.dll")] public static extern bool IsIconic(nint h);
        [DllImport("user32.dll")] public static extern nint GetWindow(nint h, uint cmd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint h, int idx);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(nint h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint h, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(nint h, [Out] char[] buf, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint h, out uint pid);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint h, int attr, out int value, int size);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern nint OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(nint h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool QueryFullProcessImageName(nint h, uint flags, StringBuilder sb, ref int size);
    }
}
