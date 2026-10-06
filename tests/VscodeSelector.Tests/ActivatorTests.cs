using Xunit;

namespace VscodeSelector.Tests;

public class ActivatorTests
{
    private sealed class FakeApi : IForegroundApi
    {
        public List<string> Calls = [];
        public List<int> Sleeps = [];
        public bool Exists = true, Iconic;
        public uint Pid = 10;
        public string Cls = "Chrome_WidgetWin_1";
        public int SucceedOnSfwCall = int.MaxValue; // 1 始まりの SetForegroundWindow 呼出番号
        public bool ThrowInBring;
        private int _sfw;

        public bool IsWindow(nint h) { Calls.Add("IsWindow"); return Exists; }
        public uint GetWindowProcessId(nint h) => Pid;
        public string GetClassName(nint h) => Cls;
        public bool IsIconic(nint h) => Iconic;
        public void ShowWindowRestore(nint h) => Calls.Add("ShowWindow(SW_RESTORE)");
        public void ShowWindowMinimize(nint h) => Calls.Add("ShowWindow(SW_MINIMIZE)");
        public bool SetForegroundWindow(nint h) { Calls.Add("SetForegroundWindow"); return ++_sfw >= SucceedOnSfwCall; }
        public void AllowSetForegroundWindowAny() => Calls.Add("AllowSetForegroundWindow(ASFW_ANY)");
        public void KeybdEvent(byte vk, bool up) => Calls.Add("keybd_event(" + vk.ToString("X2") + "," + (up ? "up" : "down") + ")");
        public uint GetForegroundThreadId() => 7;
        public uint GetCurrentThreadId() => 3;
        public bool AttachThreadInput(uint a, uint b, bool attach) { Calls.Add(attach ? "Attach" : "Detach"); return true; }
        public bool BringWindowToTop(nint h)
        {
            Calls.Add("BringWindowToTop");
            if (ThrowInBring) throw new InvalidOperationException("boom");
            return true;
        }
        public void FlashWindow(nint h) => Calls.Add("FlashWindowEx");
        public void Sleep(int ms) => Sleeps.Add(ms);
    }

    private static (ActivationResult, FakeApi) Run(FakeApi f)
        => (new WindowActivator(f).Activate(1, 10, "Chrome_WidgetWin_1"), f);

    private static List<string> Seq(FakeApi f) => f.Calls.Where(c => c != "IsWindow").ToList();

    [Fact]
    public void FirstSuccess_OnlySetForegroundWindow()
    {
        var (r, f) = Run(new FakeApi { SucceedOnSfwCall = 1 });
        Assert.Equal(ActivationResult.Success, r);
        Assert.Equal(new[] { "SetForegroundWindow" }, Seq(f));
    }

    [Fact]
    public void WorkaroundA_Sequence()
    {
        var (r, f) = Run(new FakeApi { SucceedOnSfwCall = 2 });
        Assert.Equal(ActivationResult.Success, r);
        Assert.Equal(new[] { "SetForegroundWindow", "AllowSetForegroundWindow(ASFW_ANY)", "SetForegroundWindow" }, Seq(f));
    }

    [Fact]
    public void WorkaroundB_ContainsAltDownUp()
    {
        var (r, f) = Run(new FakeApi { SucceedOnSfwCall = 3 });
        Assert.Equal(ActivationResult.Success, r);
        int d = f.Calls.IndexOf("keybd_event(12,down)");
        Assert.True(d >= 0);
        Assert.Equal("keybd_event(12,up)", f.Calls[d + 1]);
    }

    [Fact]
    public void WorkaroundC_Order()
    {
        var (r, f) = Run(new FakeApi { SucceedOnSfwCall = 4 });
        Assert.Equal(ActivationResult.Success, r);
        int a = f.Calls.IndexOf("Attach");
        Assert.True(a >= 0);
        Assert.Equal(new[] { "Attach", "BringWindowToTop", "SetForegroundWindow", "Detach" }, f.Calls.Skip(a).Take(4));
    }

    [Fact]
    public void WorkaroundC_DetachesOnException()
    {
        var (r, f) = Run(new FakeApi { ThrowInBring = true });
        Assert.Equal(ActivationResult.ForegroundBlocked, r);
        Assert.Contains("Detach", f.Calls);
        Assert.True(f.Calls.IndexOf("Detach") > f.Calls.IndexOf("BringWindowToTop"));
    }

    [Fact]
    public void AlwaysFail_FourAttempts_Delays_Blocked_FlashOnce()
    {
        var (r, f) = Run(new FakeApi());
        Assert.Equal(ActivationResult.ForegroundBlocked, r);
        Assert.Equal(Constants.ActivateMaxAttempts, f.Calls.Count(c => c == "SetForegroundWindow"));
        Assert.Equal(Constants.ActivateMaxAttempts - 1, f.Sleeps.Count);
        Assert.All(f.Sleeps, s => Assert.Equal(50, s));
        Assert.Equal(1, f.Calls.Count(c => c == "FlashWindowEx"));
    }

    [Fact]
    public void WindowGone_WhenIsWindowFalse()
    {
        var (r, f) = Run(new FakeApi { Exists = false });
        Assert.Equal(ActivationResult.WindowGone, r);
        Assert.DoesNotContain("SetForegroundWindow", f.Calls);
    }

    [Theory]
    [InlineData(99u, "Chrome_WidgetWin_1")]
    [InlineData(10u, "Other")]
    public void WindowGone_WhenPidOrClassMismatch(uint pid, string cls)
    {
        var (r, f) = Run(new FakeApi { Pid = pid, Cls = cls });
        Assert.Equal(ActivationResult.WindowGone, r);
        Assert.DoesNotContain("SetForegroundWindow", f.Calls);
    }

    [Fact]
    public void Iconic_RestoresBeforeSetForeground()
    {
        var (_, f) = Run(new FakeApi { Iconic = true, SucceedOnSfwCall = 1 });
        Assert.Contains("ShowWindow(SW_RESTORE)", f.Calls);
        Assert.True(f.Calls.IndexOf("ShowWindow(SW_RESTORE)") < f.Calls.IndexOf("SetForegroundWindow"));
    }

    [Fact]
    public void NotIconic_NoRestore()
    {
        var (_, f) = Run(new FakeApi { Iconic = false, SucceedOnSfwCall = 1 });
        Assert.DoesNotContain("ShowWindow(SW_RESTORE)", f.Calls);
    }

    [Fact]
    public void TryMinimize_Normal_Minimizes()
    {
        var f = new FakeApi();
        Assert.True(new WindowActivator(f).TryMinimize(1, 10, "Chrome_WidgetWin_1"));
        Assert.Equal(new[] { "ShowWindow(SW_MINIMIZE)" }, Seq(f));
    }

    [Theory]
    [InlineData(true, true, 10u)]
    [InlineData(false, false, 10u)]
    [InlineData(false, true, 99u)]
    public void TryMinimize_IconicOrGone_DoesNothing(bool iconic, bool exists, uint pid)
    {
        var f = new FakeApi { Iconic = iconic, Exists = exists, Pid = pid };
        Assert.False(new WindowActivator(f).TryMinimize(1, 10, "Chrome_WidgetWin_1"));
        Assert.Empty(Seq(f));
    }
}
