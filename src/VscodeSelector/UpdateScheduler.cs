namespace VscodeSelector;

/// <summary>
/// 更新戦略 (設計 §6)。Changed と定期ポーリングの両方を DebounceMs で 1 回にまとめて列挙を起動する。
/// HookActive の値に関わらずポーリングは常に動く。最小化中は PollIntervalHiddenMs を使う。
/// </summary>
public sealed class UpdateScheduler : IDisposable
{
    private readonly IChangeNotifier _notifier;
    private readonly IClock _clock;
    private readonly Action _enumerate;
    private readonly Func<bool> _isHidden;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private IDisposable? _pollTimer;
    private IDisposable? _debounceTimer;
    private bool _started;
    private bool _disposed;

    public UpdateScheduler(IChangeNotifier notifier, IClock clock, Action enumerate,
        Func<bool> isHidden, ILogger? logger = null)
    {
        _notifier = notifier;
        _clock = clock;
        _enumerate = enumerate;
        _isHidden = isHidden;
        _logger = logger;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed) return;
            _started = true;
            _notifier.Changed += OnChanged;
            SchedulePollLocked();
        }
        try { _notifier.Start(); } catch (Exception ex) { _logger?.Warn($"change notifier start failed: {ex.Message}"); }
        if (!_notifier.HookActive) _logger?.Warn("HookActive=false: polling only");
    }

    private int CurrentPollInterval() =>
        _isHidden() ? Constants.PollIntervalHiddenMs : Constants.PollIntervalMs;

    private void SchedulePollLocked()
    {
        _pollTimer = _clock.Schedule(CurrentPollInterval(), OnPoll);
    }

    private void OnPoll()
    {
        lock (_gate)
        {
            if (_disposed) return;
            SchedulePollLocked();
            RequestEnumerationLocked();
        }
    }

    private void OnChanged()
    {
        lock (_gate)
        {
            if (_disposed) return;
            RequestEnumerationLocked();
        }
    }

    // 最初のイベントから DebounceMs の窓を開き、窓内の追加要求は吸収する。
    private void RequestEnumerationLocked()
    {
        if (_debounceTimer is not null) return;
        _debounceTimer = _clock.Schedule(Constants.DebounceMs, OnDebounceElapsed);
    }

    private void OnDebounceElapsed()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _debounceTimer = null;
        }
        var start = _clock.NowMs;
        try { _enumerate(); }
        catch (Exception ex) { _logger?.Warn($"enumeration callback failed: {ex.Message}"); }
        var elapsed = _clock.NowMs - start;
        if (elapsed > Constants.EnumBudgetMs)
            _logger?.Warn($"enumeration took {elapsed} ms (budget {Constants.EnumBudgetMs} ms)");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _notifier.Changed -= OnChanged;
            _pollTimer?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
