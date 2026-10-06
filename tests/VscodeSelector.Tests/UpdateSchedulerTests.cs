using Xunit;

namespace VscodeSelector.Tests;

public class UpdateSchedulerTests
{
    private sealed class FakeClock : IClock
    {
        private sealed class Item(long due, Action cb) : IDisposable
        {
            public long Due = due; public Action Cb = cb; public bool Cancelled;
            public void Dispose() => Cancelled = true;
        }
        private readonly List<Item> _items = new();
        public long NowMs { get; private set; }
        public IDisposable Schedule(int delayMs, Action callback)
        {
            var i = new Item(NowMs + delayMs, callback);
            _items.Add(i);
            return i;
        }
        /// <summary>時計だけ進める (列挙所要時間の模擬)。</summary>
        public void Skew(long ms) => NowMs += ms;
        public void AdvanceTo(long target)
        {
            while (true)
            {
                var next = _items.Where(i => !i.Cancelled && i.Due <= target).OrderBy(i => i.Due).FirstOrDefault();
                if (next is null) break;
                _items.Remove(next);
                if (next.Due > NowMs) NowMs = next.Due;
                next.Cb();
            }
            if (target > NowMs) NowMs = target;
        }
    }

    private sealed class FakeNotifier : IChangeNotifier
    {
        public bool HookActive { get; set; }
        public event Action? Changed;
        public void Start() { }
        public void Fire() => Changed?.Invoke();
        public void Dispose() { }
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Warns = new();
        public void Debug(string m) { }
        public void Info(string m) { }
        public void Warn(string m) => Warns.Add(m);
        public void Error(string m) { }
    }

    [Fact]
    public void BurstWithinDebounce_CollapsesToOneEnumeration()
    {
        var clock = new FakeClock(); var n = new FakeNotifier { HookActive = true };
        var count = 0;
        using var s = new UpdateScheduler(n, clock, () => count++, () => false);
        s.Start();
        clock.AdvanceTo(100);
        for (int i = 0; i < 5; i++) { n.Fire(); clock.AdvanceTo(clock.NowMs + 40); }
        Assert.Equal(0, count);
        clock.AdvanceTo(100 + Constants.DebounceMs);
        Assert.Equal(1, count);
        clock.AdvanceTo(1000);
        Assert.Equal(1, count);
    }

    [Fact]
    public void HookInactive_PollingAloneTriggersEnumeration()
    {
        var clock = new FakeClock(); var n = new FakeNotifier { HookActive = false };
        var count = 0;
        using var s = new UpdateScheduler(n, clock, () => count++, () => false);
        s.Start();
        clock.AdvanceTo(Constants.PollIntervalMs + Constants.DebounceMs - 1);
        Assert.Equal(0, count);
        clock.AdvanceTo(Constants.PollIntervalMs + Constants.DebounceMs);
        Assert.Equal(1, count);
    }

    // 窓を closeAt で閉じ、反映 (列挙完了) 時刻の絶対値を返す。
    private static long ReflectedAt(long closeAt, bool hidden)
    {
        var clock = new FakeClock(); var n = new FakeNotifier { HookActive = false };
        var windowOpen = true; long reflected = -1;
        using var s = new UpdateScheduler(n, clock, () =>
        {
            clock.Skew(Constants.EnumBudgetMs); // 列挙 50ms
            if (!windowOpen && reflected < 0) reflected = clock.NowMs;
        }, () => hidden);
        s.Start();
        clock.AdvanceTo(closeAt);
        windowOpen = false;
        clock.AdvanceTo(10_000);
        return reflected;
    }

    [Fact]
    public void WorstCase_VisibleReflectedWithin2300()
    {
        var t = ReflectedAt(1, false);
        Assert.Equal(Constants.DetectLatencyMax, t);
        Assert.True(t <= 2300 && t < 2301);
    }

    [Fact]
    public void WorstCase_HiddenUsesHiddenIntervalAndReflectsWithin5300()
    {
        var t = ReflectedAt(1, true);
        Assert.Equal(Constants.DetectLatencyMaxHidden, t);
        Assert.True(t <= 5300);
    }

    [Fact]
    public void EnumerationOverBudget_LogsWarningAndContinues()
    {
        var clock = new FakeClock(); var n = new FakeNotifier { HookActive = true };
        var log = new ListLogger(); var count = 0;
        using var s = new UpdateScheduler(n, clock, () =>
        {
            count++;
            clock.Skew(Constants.EnumBudgetMs + 1);
            if (count == 2) throw new InvalidOperationException("boom");
        }, () => false, log);
        s.Start();
        clock.AdvanceTo(Constants.PollIntervalMs + Constants.DebounceMs);
        Assert.Equal(1, count);
        Assert.Single(log.Warns, w => w.Contains("budget"));
        clock.AdvanceTo(2 * Constants.PollIntervalMs + Constants.DebounceMs + Constants.EnumBudgetMs + 1);
        Assert.Equal(2, count); // 例外後も継続
        clock.AdvanceTo(3 * Constants.PollIntervalMs + 2 * Constants.EnumBudgetMs + Constants.DebounceMs + 10);
        Assert.Equal(3, count);
    }
}
