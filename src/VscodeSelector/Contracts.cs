namespace VscodeSelector;

public enum VsCodeEdition { Stable, Insiders, VSCodium }
public enum RemoteKind { None, Wsl, Ssh, DevContainer, Codespaces, Other }

public readonly record struct RawWindow(
    nint Hwnd, uint ProcessId, string ProcessName,
    string ClassName, string Title, bool IsMinimized);

public sealed record ParsedTitle(
    string ProjectName,
    RemoteKind Remote, string? RemoteLabel,
    bool IsNoFolder, bool IsWorkspaceFile);

public sealed record ProjectEntry(
    nint Hwnd, uint ProcessId, VsCodeEdition Edition,
    string DisplayName,
    string FullTitle,
    ParsedTitle Parsed, bool IsMinimized);

/// <summary>EnumWindows が 0 を返した場合に投げる。Win32Error (GetLastError の値) を保持する。</summary>
public sealed class EnumerationException(int win32Error) : Exception
{
    public int Win32Error { get; } = win32Error;
}

public enum ActivationResult { Success, WindowGone, ForegroundBlocked }

public enum SortOrder { Name, ZOrder }

public sealed record Options(
    string[] TitleSeparators,
    string? TitlePattern,
    string[] ExtraProcessNames,
    SortOrder SortOrder,
    bool Topmost,
    bool HideOnActivate);

public interface IWindowEnumerator
{
    /// <summary>フィルタ済み RawWindow (Z オーダー順)。EnumWindows 失敗時 EnumerationException。</summary>
    IReadOnlyList<RawWindow> Enumerate();
}

public interface ITitleParser
{
    /// <summary>例外を投げない (解析不能は全文フォールバック)。</summary>
    ParsedTitle Parse(string title, Options options);
}

public interface IWindowActivator
{
    /// <summary>WindowGone: IsWindow が false、または PID/ClassName 不一致。ForegroundBlocked: 全試行失敗。例外は投げない。</summary>
    ActivationResult Activate(nint hwnd, uint expectedPid, string expectedClassName);
}

public interface IChangeNotifier : IDisposable
{
    /// <summary>スレッドプール上で発火する。</summary>
    event Action Changed;
    bool HookActive { get; }
    void Start();
}
