using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VscodeSelector;

/// <summary>列挙→解析→区別→並べ替え→差分適用を行う ViewModel (設計 §5.3, §6, §7-6)。</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public const string EnumerationWarning = "ウィンドウ一覧の取得に失敗しました。前回の一覧を表示しています。";
    public const string ForegroundBlockedMessage = "前面に出せませんでした。タスクバーから切り替えてください。";

    private readonly IWindowEnumerator _enumerator;
    private readonly ITitleParser _parser;
    private readonly IWindowActivator _activator;
    private readonly IClock _clock;
    private readonly Options _options;
    private readonly Action? _requestMinimize;
    private readonly Action<Action> _ui;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<nint, int> _sequenceMap = new();
    private IDisposable? _toastTimer;
    private string? _toast;
    private string? _warning;
    private int _toastVersion;

    public MainViewModel(IWindowEnumerator enumerator, ITitleParser parser, IWindowActivator activator,
        IClock clock, Options options, Action? requestMinimize = null,
        Action<Action>? uiDispatch = null, ILogger? logger = null)
    {
        _enumerator = enumerator;
        _parser = parser;
        _activator = activator;
        _clock = clock;
        _options = options;
        _requestMinimize = requestMinimize;
        _ui = uiDispatch ?? (a => a());
        _logger = logger;
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
        return VsCodeEdition.Stable;
    }
}
