using Xunit;

namespace VscodeSelector.Tests;

public class WinEventChangeNotifierTests
{
    private sealed class FakeHookApi : IWinEventHookApi
    {
        public nint HookResult = 42;
        public int UnhookCalls;
        public WinEventCallback? Callback;
        private readonly ManualResetEventSlim _quit = new();
        public nint SetWinEventHook(uint min, uint max, WinEventCallback cb) { Callback = cb; return HookResult; }
        public bool UnhookWinEvent(nint hook) { Interlocked.Increment(ref UnhookCalls); return true; }
        public void RunMessageLoop() => _quit.Wait();
        public void QuitMessageLoop(uint tid) => _quit.Set();
        public uint GetCurrentNativeThreadId() => 1;
    }

    [Fact]
    public void Start_HookFails_NoThrowAndHookInactive()
    {
        var api = new FakeHookApi { HookResult = 0 };
        using var n = new WinEventChangeNotifier(api);
        n.Start();
        Assert.False(n.HookActive);
        Assert.Equal(0, api.UnhookCalls);
    }

    [Fact]
    public void Start_HookSucceeds_HookActive()
    {
        var api = new FakeHookApi();
        using var n = new WinEventChangeNotifier(api);
        n.Start();
        Assert.True(n.HookActive);
    }

    [Theory]
    [InlineData(0x8002u, 0, true)]
    [InlineData(0x8001u, 0, true)]
    [InlineData(0x800Cu, 0, true)]
    [InlineData(0x8002u, -4, false)]
    [InlineData(0x8001u, -1, false)]
    [InlineData(0x800Cu, -2, false)]
    public void Event_FiresOnlyForObjIdWindow(uint evt, int idObject, bool expected)
    {
        var api = new FakeHookApi();
        using var n = new WinEventChangeNotifier(api);
        using var fired = new ManualResetEventSlim();
        n.Changed += () => fired.Set();
        n.Start();
        api.Callback!(evt, 1, idObject, 0);
        Assert.Equal(expected, fired.Wait(expected ? 2000 : 300));
    }

    [Fact]
    public void Changed_FiresOnThreadPoolThread()
    {
        var api = new FakeHookApi();
        using var n = new WinEventChangeNotifier(api);
        using var fired = new ManualResetEventSlim();
        bool? onPool = null;
        int firedTid = -1;
        n.Changed += () => { onPool = Thread.CurrentThread.IsThreadPoolThread; firedTid = Environment.CurrentManagedThreadId; fired.Set(); };
        n.Start();
        var t = new Thread(() => api.Callback!(0x8002, 1, 0, 0));
        t.Start(); t.Join();
        Assert.True(fired.Wait(2000));
        Assert.True(onPool);
        Assert.NotEqual(t.ManagedThreadId, firedTid);
    }

    [Fact]
    public void Dispose_UnhooksExactlyOnce()
    {
        var api = new FakeHookApi();
        var n = new WinEventChangeNotifier(api);
        n.Start();
        n.Dispose();
        n.Dispose();
        Assert.Equal(1, api.UnhookCalls);
        Assert.False(n.HookActive);
    }
}
