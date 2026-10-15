using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace VscodeSelector;

/// <summary>列挙→解析→区別→並べ替え→差分適用を行う ViewModel (設計 §5.3, §6, §7-6)。</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public const string EnumerationWarning = "ウィンドウ一覧の取得に失敗しました。前回の一覧を表示しています。";
    public const string ForegroundBlockedMessage = "前面に出せませんでした。タスクバーから切り替えてください。";
    public const string FolderUnavailableMessage = "このウィンドウはローカルのフォルダを開いていません。";
    public const string FolderSetupDoneMessage = "設定しました。タイトルが更新されたらもう一度 Shift+クリックしてください。";
    public const string FolderSetupFailedMessage = "settings.json を更新できませんでした。ログを確認してください。";

    private readonly IWindowEnumerator _enumerator;
    private readonly ITitleParser _parser;
    private readonly IWindowActivator _activator;
    private readonly IClock _clock;
    private readonly Options _options;
    private readonly Action? _requestMinimize;
    private readonly Action<Action> _ui;
    private readonly ILogger? _logger;
    private readonly Func<nint>? _previousForeground;
    private readonly object _gate = new();
    private readonly Dictionary<nint, int> _sequenceMap = new();
    private IDisposable? _toastTimer;
    private string? _toast;
    private string? _warning;
    private int _toastVersion;

    public MainViewModel(IWindowEnumerator enumerator, ITitleParser parser, IWindowActivator activator,
        IClock clock, Options options, Action? requestMinimize = null,
        Action<Action>? uiDispatch = null, ILogger? logger = null, Func<nint>? previousForeground = null)
    {
        _enumerator = enumerator;
        _parser = parser;
        _activator = activator;
        _clock = clock;
        _options = options;
        _requestMinimize = requestMinimize;
        _ui = uiDispatch ?? (a => a());
        _logger = logger;
        _previousForeground = previousForeground;
    }

    public ObservableCollection<ProjectEntry> Entries { get; } = new();

    /// <summary>ステータスバー表示。トースト優先、次に警告、無ければ空。</summary>
    public string StatusText => _toast ?? _warning ?? "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>背景スレッドから呼ばれる。例外を投げない。</summary>
    public void OnChanged()
    {
        lock (_gate)
        {
            IReadOnlyList<RawWindow> raw;
            try
            {
                raw = _enumerator.Enumerate();
            }
            catch (EnumerationException ex)
            {
                _logger?.Warn($"EnumWindows failed: {ex.Win32Error}");
                _ui(() => SetWarning(EnumerationWarning));
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger?.Warn($"enumeration failed: {ex.Message}");
                _ui(() => SetWarning(EnumerationWarning));
                return;
            }

            var entries = new List<ProjectEntry>(raw.Count);
            foreach (var w in raw)
            {
                var parsed = _parser.Parse(w.Title, _options);
                entries.Add(new ProjectEntry(w.Hwnd, w.ProcessId, EditionOf(w.ProcessName),
                    parsed.ProjectName, w.Title, parsed, w.IsMinimized));
            }

            var live = new HashSet<nint>(entries.Select(e => e.Hwnd));
            foreach (var k in _sequenceMap.Keys.Where(k => !live.Contains(k)).ToList())
                _sequenceMap.Remove(k);

            IReadOnlyList<ProjectEntry> desired = Disambiguator.Apply(entries, _sequenceMap);
            if (_options.SortOrder == SortOrder.Name)
            {
                // OrderBy は安定ソート
                desired = desired.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
            }

            _ui(() =>
            {
                ProjectEntryDiff.Apply(Entries, desired);
                SetWarning(null);
            });
        }
    }

    public void Activate(ProjectEntry e)
    {
        var className = WindowFilter.RequiredClassName;
        // 押す前から前面だった VS Code はトグルとして最小化する。
        if (_previousForeground?.Invoke() == e.Hwnd && _activator.TryMinimize(e.Hwnd, e.ProcessId, className))
            return;
        var result = _activator.Activate(e.Hwnd, e.ProcessId, className);
        switch (result)
        {
            case ActivationResult.Success:
                if (_options.HideOnActivate) _requestMinimize?.Invoke();
                break;
            case ActivationResult.WindowGone:
                _ui(() =>
                {
                    var cur = Entries.FirstOrDefault(x => x.Hwnd == e.Hwnd);
                    if (cur is not null) Entries.Remove(cur);
                });
                OnChanged();
                break;
            case ActivationResult.ForegroundBlocked:
                _ui(() => ShowToast(ForegroundBlockedMessage));
                break;
        }
    }

    /// <summary>
    /// Shift+クリック: プロジェクトのフォルダをエクスプローラーで開く (ワークスペースはファイルを選択表示)。
    /// タイトルにパス表示が無ければ confirmSetup で確認し、エディタの settings.json に追加する。UI スレッドから呼ぶ。
    /// </summary>
    public void OpenFolder(ProjectEntry e, Func<bool> confirmSetup)
    {
        var path = e.Parsed.FolderPath;
        if (path is null)
        {
            if (!confirmSetup()) return;
            var errors = TitlePathSettings.Install(TitlePathSettings.SettingsFiles(e.Edition), _logger);
            ShowToast(errors.Count == 0 ? FolderSetupDoneMessage : FolderSetupFailedMessage);
            return;
        }
        // リモートの rootPath はリモート側のパス。WSL は \\wsl.localhost 経由で開け、それ以外はローカルの同名パスと取り違えるため開かない。
        path = e.Parsed.Remote switch
        {
            _ when path.Length == 0 => null,
            RemoteKind.None => path,
            RemoteKind.Wsl when WslPath.Distro(e.Parsed.RemoteLabel) is { } distro
                => WslPath.ToWindows(distro, path, d => WslPath.Home(d, _logger)),
            _ => null,
        };
        if (path is null)
        {
            ShowToast(FolderUnavailableMessage);
            return;
        }
        string? args = Directory.Exists(path) ? $"\"{path}\""
            : File.Exists(path) ? $"/select,\"{path}\""
            : null;
        if (args is null)
        {
            ShowToast(FolderUnavailableMessage);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Warn($"explorer start failed ({path}): {ex.Message}");
        }
    }

    private void ShowToast(string message)
    {
        _toastTimer?.Dispose();
        int version = ++_toastVersion;
        _toast = message;
        RaiseStatus();
        _toastTimer = _clock.Schedule(Constants.ToastMs, () => _ui(() =>
        {
            if (version != _toastVersion) return;
            _toast = null;
            RaiseStatus();
        }));
    }

    private void SetWarning(string? message)
    {
        if (_warning == message) return;
        _warning = message;
        RaiseStatus();
    }

    private void RaiseStatus() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));

    private static VsCodeEdition EditionOf(string processName)
    {
        var n = processName ?? "";
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        if (n.Equals("Code - Insiders", StringComparison.OrdinalIgnoreCase)) return VsCodeEdition.Insiders;
        if (n.Equals("VSCodium", StringComparison.OrdinalIgnoreCase)) return VsCodeEdition.VSCodium;
        if (n.Equals("Cursor", StringComparison.OrdinalIgnoreCase)) return VsCodeEdition.Cursor;
        if (n.Equals("Antigravity IDE", StringComparison.OrdinalIgnoreCase)) return VsCodeEdition.Antigravity;
        return VsCodeEdition.Stable;
    }
}
