namespace VscodeSelector;

/// <summary>
/// SetWinEventHook による変更通知 (設計 §6)。専用スレッドでフック登録とメッセージループを回し、
/// Changed はスレッドプールへ投げて発火する。登録失敗時は例外を投げず HookActive=false (ポーリングのみ)。
/// </summary>
public sealed class WinEventChangeNotifier : IChangeNotifier
{
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectNameChange = 0x800C;
    internal const int ObjIdWindow = 0;

    private readonly IWinEventHookApi _api;
    private readonly object _gate = new();
    private Thread? _thread;
    private uint _nativeTid;
    private volatile bool _hookActive;
    private bool _disposed;

    private readonly ILogger? _logger;

    public WinEventChangeNotifier() : this(new Win32WinEventHookApi()) { }
    internal WinEventChangeNotifier(IWinEventHookApi api, ILogger? logger = null)
    {
        _api = api;
        _logger = logger;
    }

    public event Action? Changed;

    public bool HookActive => _hookActive;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _thread is not null) return;
            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() => HookThread(ready)) { IsBackground = true, Name = "WinEventHook" };
            _thread.Start();
            ready.Wait();
        }
    }

    private void HookThread(ManualResetEventSlim ready)
    {
        nint hook = 0;
        try
        {
            _nativeTid = _api.GetCurrentNativeThreadId();
            hook = _api.SetWinEventHook(EventObjectDestroy, EventObjectNameChange, OnEvent);
            _hookActive = hook != 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 登録失敗はポーリングのみで継続する (設計 §6)。
            hook = 0;
            _hookActive = false;
            _logger?.Warn($"WinEvent hook registration failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally { ready.Set(); }
        if (hook == 0) return;
        try { _api.RunMessageLoop(); }
        finally
        {
            _hookActive = false;
            _api.UnhookWinEvent(hook);
        }
    }

    private void OnEvent(uint evt, nint hwnd, int idObject, int idChild)
    {
        if (idObject != ObjIdWindow) return;
        if (evt != EventObjectShow && evt != EventObjectDestroy && evt != EventObjectNameChange) return;
        var handler = Changed;
        if (handler is null) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { handler(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // 購読側の例外でフック通知経路を止めない。
                _logger?.Warn($"Changed handler threw: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        Thread? t;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            t = _thread;
        }
        if (t is null) return;
        if (_hookActive) _api.QuitMessageLoop(_nativeTid);
        t.Join(5000);
    }
}
