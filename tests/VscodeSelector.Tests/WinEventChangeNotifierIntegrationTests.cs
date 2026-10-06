using Xunit;

namespace VscodeSelector.Tests;

[Trait("Category", "Interactive")]
public class WinEventChangeNotifierIntegrationTests
{
    [Fact]
    public void RealHook_FiresChangedWithinOneSecondWhenWindowClosed()
    {
        using var n = new WinEventChangeNotifier();
        using var fired = new ManualResetEventSlim();
        n.Changed += () => fired.Set();
        n.Start();
        Assert.True(n.HookActive);

        var host = new TestWindowHost(new[] { ("Chrome_WidgetWin_1", "T008-" + Guid.NewGuid().ToString("N"), false) });
        // 作成時の SHOW 通知を排出してから、破棄 (DESTROY) だけを計測する。
        Thread.Sleep(500);
        fired.Reset();
        host.Dispose();
        Assert.True(fired.Wait(1000), "Changed not fired within 1s");
        Assert.True(n.HookActive);
    }
}
