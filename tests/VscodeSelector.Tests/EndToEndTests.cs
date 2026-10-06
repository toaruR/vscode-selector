using System.IO;
using System.Diagnostics;
using Xunit;

namespace VscodeSelector.Tests;

/// <summary>実コンポーネント + TestWindowHost による結合受け入れ試験 (設計 T3/T6/T8/T10)。</summary>
[Trait("Category", "Interactive")]
public class EndToEndTests
{
    private const string Cls = "Chrome_WidgetWin_1";
    private const int DisappearBudgetMs = 2300;
    // 実時間計測の揺らぎ許容: Timer 2 段 (poll+debounce) の ~15.6ms 粒度 x2、test 側 Sleep(10) の粒度、
    // dispatcher/スレッドプールの遅延。厳密な 2300 判定は仮想時計の UpdateSchedulerTests で行う。
    private const int OsJitterToleranceMs = 50;
    private static readonly Options Defaults = new([" - "], null, [], SortOrder.Name, false, false);

    private static string HostExeBaseName()
        => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule!.FileName);

    private sealed class HookFailedNotifier : IChangeNotifier
    {
        public event Action? Changed { add { } remove { } }
        public bool HookActive => false;
        public void Start() { }
        public void Dispose() { }
    }

    /// <summary>指定 hwnd の ProcessName を差し替える (Insiders 模擬)。</summary>
    private sealed class RenamingEnumerator(IWindowEnumerator inner, nint hwnd, string processName) : IWindowEnumerator
    {
        public IReadOnlyList<RawWindow> Enumerate() =>
            inner.Enumerate().Select(w => w.Hwnd == hwnd ? w with { ProcessName = processName } : w).ToList();
    }

    private static MainViewModel NewVm(IWindowEnumerator? e = null) =>
        new(e ?? new Win32WindowEnumerator(new[] { HostExeBaseName() }), new TitleParser(),
            new WindowActivator(), new SystemClock(), Defaults);

    private static string[] Names(MainViewModel vm, string token)
    {
        for (int i = 0; i < 5; i++)
        {
            try { return vm.Entries.Where(e => e.FullTitle.Contains(token)).Select(e => e.DisplayName).ToArray(); }
            catch (InvalidOperationException) { Thread.Sleep(5); }
        }
        return [];
    }

    private static long WaitUntilGone(MainViewModel vm, string token, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Names(vm, token).Length == 0) return sw.ElapsedMilliseconds;
            Thread.Sleep(10);
        }
        return -1;
    }

    private static void RunDisappearScenario(IChangeNotifier notifier)
    {
        string token = "T017-" + Guid.NewGuid().ToString("N");
        var host = new TestWindowHost(new[] { (Cls, token + " - Visual Studio Code", false) });
        var vm = NewVm();
        using var scheduler = new UpdateScheduler(notifier, new SystemClock(), vm.OnChanged, () => false);
        using (notifier)
        {
            vm.OnChanged();
            scheduler.Start();
            Assert.Single(Names(vm, token));

            var sw = Stopwatch.StartNew();
            host.Dispose();
            long gone = WaitUntilGone(vm, token, DisappearBudgetMs + OsJitterToleranceMs);
            Assert.True(gone >= 0, $"entry still present after {sw.ElapsedMilliseconds} ms");
            Assert.True(sw.ElapsedMilliseconds <= DisappearBudgetMs + OsJitterToleranceMs, $"took {sw.ElapsedMilliseconds} ms");
        }
    }

    [Fact]
    public void RealComponents_EntryDisappearsWithin2300msAfterWindowClosed()
        => RunDisappearScenario(new WinEventChangeNotifier());

    [Fact]
    public void HookFailureDouble_EntryDisappearsWithin2300msByPollingOnly()
    {
        var n = new HookFailedNotifier();
        Assert.False(n.HookActive);
        RunDisappearScenario(n);
    }

    [Fact]
    public void SameNameThreeWindows_DisplayNamesUniqueAndStableAcrossReenumeration()
    {
        string token = "T017-same-" + Guid.NewGuid().ToString("N");
        string title = token + " - Visual Studio Code";
        using var host = new TestWindowHost(new[] { (Cls, title, false), (Cls, title, false), (Cls, title, false) });
        var inner = new Win32WindowEnumerator(new[] { HostExeBaseName() });
        var vm = NewVm(new RenamingEnumerator(inner, host.Handles[2], "Code - Insiders.exe"));

        vm.OnChanged();
        var first = vm.Entries.Where(e => e.FullTitle.Contains(token)).ToDictionary(e => e.Hwnd, e => e.DisplayName);
        Assert.Equal(3, first.Count);
        Assert.Equal(3, first.Values.Distinct().Count());
        Assert.Contains("Insiders", first[host.Handles[2]]);

        vm.OnChanged();
        vm.OnChanged();
        var again = vm.Entries.Where(e => e.FullTitle.Contains(token)).ToDictionary(e => e.Hwnd, e => e.DisplayName);
        Assert.Equal(first.OrderBy(k => k.Key), again.OrderBy(k => k.Key));
    }

    [Fact]
    public void ActivateClosedWindow_ReturnsWindowGoneAndRemovesEntry()
    {
        string token = "T017-gone-" + Guid.NewGuid().ToString("N");
        var host = new TestWindowHost(new[] { (Cls, token + " - Visual Studio Code", false) });
        var vm = NewVm();
        vm.OnChanged();
        var entry = Assert.Single(vm.Entries, e => e.FullTitle.Contains(token));

        host.Dispose();
        Assert.Equal(ActivationResult.WindowGone,
            new WindowActivator().Activate(entry.Hwnd, entry.ProcessId, Cls));

        vm.Activate(entry);
        Assert.DoesNotContain(vm.Entries, e => e.FullTitle.Contains(token));
    }
}
