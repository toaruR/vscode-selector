using System.Text.RegularExpressions;

namespace VscodeSelector;

/// <summary>EnumWindows 1 ウィンドウ分の Win32 観測値 (設計 §5.1)。</summary>
/// <param name="ProcessName">exe ベース名 (拡張子付き)。OpenProcess 失敗時は null。</param>
public readonly record struct WindowCandidate(
    nint Hwnd, uint ProcessId, string ClassName, string Title,
    bool IsVisible, bool HasOwner, bool IsToolWindow, bool IsCloaked,
    bool IsMinimized, string? ProcessName);

/// <summary>設計 §5.1 手順 1〜5 の純関数実装 (Win32 非依存)。</summary>
public static class WindowFilter
{
    public const string RequiredClassName = "Chrome_WidgetWin_1";

    private static readonly string[] StandardExeNames = ["Code.exe", "Code - Insiders.exe", "VSCodium.exe"];
    private static readonly Regex AppRe = new(Constants.AppRe, RegexOptions.CultureInvariant);

    /// <summary>採用なら RawWindow、除外なら null。OpenProcess 失敗フォールバック時のみ log を 1 行呼ぶ。</summary>
    public static RawWindow? Evaluate(
        WindowCandidate c, IReadOnlyCollection<string> extraProcessNames, Action<string>? log = null)
    {
        if (!c.IsVisible || c.HasOwner || c.IsToolWindow || c.Title.Length == 0) return null;
        if (c.IsCloaked) return null;
        if (c.ClassName != RequiredClassName) return null;

        string processName;
        if (c.ProcessName is null)
        {
            if (!AppRe.IsMatch(c.Title)) return null;
            log?.Invoke($"OpenProcess failed (elevated?) hwnd=0x{c.Hwnd:X} pid={c.ProcessId}; accepted by title fallback");
            processName = "";
        }
        else
        {
            if (!IsAllowedExe(c.ProcessName, extraProcessNames)) return null;
            processName = c.ProcessName;
        }

        return new RawWindow(c.Hwnd, c.ProcessId, processName, c.ClassName, c.Title, c.IsMinimized);
    }

    private static bool IsAllowedExe(string name, IReadOnlyCollection<string> extra)
    {
        foreach (var s in StandardExeNames)
            if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var e in extra)
        {
            if (string.IsNullOrEmpty(e)) continue;
            // extraProcessNames は拡張子なしの exe 名 (設計 §4)。拡張子付き指定も許容。
            if (string.Equals(name, e + ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, e, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
