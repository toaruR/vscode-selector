using System;
using System.Threading;
using Xunit;

namespace VscodeSelector.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void SecondAcquire_ReturnsNull_AndSignalsFirst()
    {
        var name = "VscodeSelector.Test." + Guid.NewGuid().ToString("N");
        // Mutex はスレッド所有なので、別スレッドから 2 つ目を取得して別プロセスを模す。
        using var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        using var shown = new ManualResetEventSlim();
        first!.ListenShowRequests(shown.Set);

        SingleInstance? second = null;
        var called = false;
        var t = new Thread(() => second = SingleInstance.TryAcquire(name, () => called = true));
        t.Start();
        t.Join();

        Assert.Null(second);
        Assert.True(called);
        Assert.True(shown.Wait(2000));
    }

    [Fact]
    public void AfterDispose_CanAcquireAgain()
    {
        var name = "VscodeSelector.Test." + Guid.NewGuid().ToString("N");
        SingleInstance.TryAcquire(name)!.Dispose();
        using var again = SingleInstance.TryAcquire(name);
        Assert.NotNull(again);
    }
}
