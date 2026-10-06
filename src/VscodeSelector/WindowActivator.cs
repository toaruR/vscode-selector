namespace VscodeSelector;

/// <summary>設計 §7 のアクティブ化手順。例外は投げない。</summary>
public sealed class WindowActivator : IWindowActivator
{
    internal const byte VkMenu = 0x12;

    private readonly IForegroundApi _api;

    private readonly ILogger? _logger;

    public WindowActivator() : this(new Win32ForegroundApi()) { }
    internal WindowActivator(IForegroundApi api, ILogger? logger = null)
    {
        _api = api;
        _logger = logger;
    }

    public ActivationResult Activate(nint hwnd, uint expectedPid, string expectedClassName)
    {
        try
        {
            if (!_api.IsWindow(hwnd)
                || _api.GetWindowProcessId(hwnd) != expectedPid
                || _api.GetClassName(hwnd) != expectedClassName)
                return ActivationResult.WindowGone;

            if (_api.IsIconic(hwnd)) _api.ShowWindowRestore(hwnd);

            for (int attempt = 1; attempt <= Constants.ActivateMaxAttempts; attempt++)
            {
                if (attempt > 1) _api.Sleep(Constants.ActivateRetryDelayMs);
                bool ok = false;
                try { ok = TryAttempt(hwnd, attempt); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // 1 試行の失敗は次の試行へ進むため継続する (記録のみ)。
                    _logger?.Warn($"Activate attempt {attempt} failed: {ex.GetType().Name}: {ex.Message}");
                }
                if (ok) return ActivationResult.Success;
            }
            _api.FlashWindow(hwnd);
            return ActivationResult.ForegroundBlocked;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Error($"Activate failed: {ex.GetType().Name}: {ex.Message}");
            return ActivationResult.ForegroundBlocked;
        }
    }

    public bool TryMinimize(nint hwnd, uint expectedPid, string expectedClassName)
    {
        try
        {
            if (!_api.IsWindow(hwnd)
                || _api.GetWindowProcessId(hwnd) != expectedPid
                || _api.GetClassName(hwnd) != expectedClassName
                || _api.IsIconic(hwnd))
                return false;
            _api.ShowWindowMinimize(hwnd);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Error($"Minimize failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private bool TryAttempt(nint hwnd, int attempt)
    {
        switch (attempt)
        {
            case 1:
                return _api.SetForegroundWindow(hwnd);
            case 2:
                _api.AllowSetForegroundWindowAny();
                return _api.SetForegroundWindow(hwnd);
            case 3:
                _api.KeybdEvent(VkMenu, false);
                _api.KeybdEvent(VkMenu, true);
                return _api.SetForegroundWindow(hwnd);
            default:
                uint cur = _api.GetCurrentThreadId();
                uint fg = _api.GetForegroundThreadId();
                _api.AttachThreadInput(cur, fg, true);
                try
                {
                    _api.BringWindowToTop(hwnd);
                    return _api.SetForegroundWindow(hwnd);
                }
                finally { _api.AttachThreadInput(cur, fg, false); }
        }
    }
}
