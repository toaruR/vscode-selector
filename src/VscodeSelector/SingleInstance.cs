namespace VscodeSelector;

/// <summary>
/// 名前付き Mutex による多重起動防止。2 つ目のインスタンスは名前付きイベントで既存側へ表示を依頼して終了する。
/// 名前はセッションローカル (Local\) なので、別ユーザー・別セッションとは干渉しない。
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string DefaultName = "VscodeSelector.SingleInstance";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _signal;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle? signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>最初のインスタンスなら所有権付きで返す。既に起動済みなら既存側へ通知して null。</summary>
    public static SingleInstance? TryAcquire(string name = DefaultName, Action? beforeSignal = null)
    {
        var mutex = new Mutex(true, $@"Local\{name}.Mutex", out bool created);
        if (created)
            return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show"));
        mutex.Dispose();
        try
        {
            using var signal = EventWaitHandle.OpenExisting($@"Local\{name}.Show");
            beforeSignal?.Invoke();
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        return null;
    }

    /// <summary>後続インスタンスから表示依頼が来たらスレッドプール上で onShow を呼ぶ。</summary>
    public void ListenShowRequests(Action onShow)
    {
        if (_signal is null || _wait is not null) return;
        _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onShow(), null, Timeout.Infinite, false);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _signal?.Dispose();
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex.Dispose();
    }
}
