using Xunit;

namespace VscodeSelector.Tests;

public class MainViewModelTests
{
    private static readonly Options Defaults = new([" - "], null, [], SortOrder.Name, false, false);

    private static RawWindow W(nint h, string title, string proc = "Code.exe", bool min = false) =>
        new(h, 100 + (uint)h, proc, "Chrome_WidgetWin_1", title, min);

    private sealed class Fixture
    {
        public FakeEnumerator Enum = new();
        public FakeActivator Act = new();
        public VirtualClock Clock = new();
        public int MinimizeCount;
        public MainViewModel Vm;
        public Fixture(Options? o = null) =>
            Vm = new MainViewModel(Enum, new FakeParser(), Act, Clock, o ?? Defaults, () => MinimizeCount++);
    }

    private static nint[] Hwnds(MainViewModel vm) => vm.Entries.Select(e => e.Hwnd).ToArray();

    [Fact]
    public void EnumerationFailure_KeepsList_SetsWarning_RecoversOnNextSuccess()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a"), W(2, "b")];
        f.Vm.OnChanged();
        var before = f.Vm.Entries.ToList();

        f.Enum.Throw = true;
        f.Vm.OnChanged();
        Assert.Equal(before.Count, f.Vm.Entries.Count);
        Assert.True(before.Zip(f.Vm.Entries).All(p => ReferenceEquals(p.First, p.Second)));
        Assert.Equal(MainViewModel.EnumerationWarning, f.Vm.StatusText);

        f.Enum.Throw = false;
        f.Enum.Windows = [W(1, "a")];
        f.Vm.OnChanged();
        Assert.Single(f.Vm.Entries);
        Assert.Equal("", f.Vm.StatusText);
    }

    [Fact]
    public void Activate_WindowGone_RemovesEntryAndReenumerates()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a"), W(2, "b")];
        f.Vm.OnChanged();
        var target = f.Vm.Entries.First(e => e.Hwnd == 1);
        f.Act.Result = ActivationResult.WindowGone;
        f.Act.OnActivate = () => f.Enum.Windows = [W(2, "b")];
        int callsBefore = f.Enum.Calls;

        f.Vm.Activate(target);

        Assert.Equal(callsBefore + 1, f.Enum.Calls);
        Assert.DoesNotContain(f.Vm.Entries, e => e.Hwnd == 1);
        Assert.Single(f.Vm.Entries);
        Assert.Equal(0, f.MinimizeCount);
    }

    [Fact]
    public void Activate_WindowGone_RemovesEvenIfEnumerationFails()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a"), W(2, "b")];
        f.Vm.OnChanged();
        f.Act.Result = ActivationResult.WindowGone;
        f.Enum.Throw = true;
        f.Vm.Activate(f.Vm.Entries.First(e => e.Hwnd == 1));
        Assert.DoesNotContain(f.Vm.Entries, e => e.Hwnd == 1);
    }

    [Fact]
    public void Activate_ForegroundBlocked_SetsToastThatClearsAfterToastMs()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a")];
        f.Vm.OnChanged();
        f.Act.Result = ActivationResult.ForegroundBlocked;
        f.Vm.Activate(f.Vm.Entries[0]);
        Assert.Equal(MainViewModel.ForegroundBlockedMessage, f.Vm.StatusText);

        f.Clock.Advance(Constants.ToastMs - 1);
        Assert.Equal(MainViewModel.ForegroundBlockedMessage, f.Vm.StatusText);
        f.Clock.Advance(1);
        Assert.Equal("", f.Vm.StatusText);
        Assert.Equal(0, f.MinimizeCount);
    }

    [Fact]
    public void Diff_AddRemoveUpdate_KeepsReferencesOfUnchangedHwnd()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a"), W(2, "b"), W(3, "c")];
        f.Vm.OnChanged();
        var e1 = f.Vm.Entries.First(e => e.Hwnd == 1);
        var e2 = f.Vm.Entries.First(e => e.Hwnd == 2);
        var e3 = f.Vm.Entries.First(e => e.Hwnd == 3);

        f.Enum.Windows = [W(1, "a"), W(3, "c2"), W(4, "d")];
        f.Vm.OnChanged();

        Assert.Equal(new nint[] { 1, 3, 4 }, Hwnds(f.Vm));
        Assert.Same(e1, f.Vm.Entries.First(e => e.Hwnd == 1));
        Assert.NotSame(e3, f.Vm.Entries.First(e => e.Hwnd == 3));
        Assert.Equal("c2", f.Vm.Entries.First(e => e.Hwnd == 3).DisplayName);
        Assert.DoesNotContain(f.Vm.Entries, e => ReferenceEquals(e, e2));

        var snapshot = f.Vm.Entries.ToList();
        f.Vm.OnChanged();
        Assert.True(snapshot.Zip(f.Vm.Entries).All(p => ReferenceEquals(p.First, p.Second)));
    }

    [Fact]
    public void Diff_ReorderMovesWithoutRecreating()
    {
        var f = new Fixture(Defaults with { SortOrder = SortOrder.ZOrder });
        f.Enum.Windows = [W(1, "a"), W(2, "b"), W(3, "c")];
        f.Vm.OnChanged();
        var e1 = f.Vm.Entries[0];
        f.Enum.Windows = [W(3, "c"), W(1, "a"), W(2, "b")];
        f.Vm.OnChanged();
        Assert.Equal(new nint[] { 3, 1, 2 }, Hwnds(f.Vm));
        Assert.Same(e1, f.Vm.Entries[1]);
    }

    [Fact]
    public void SortOrder_Name_IsCaseInsensitiveAscendingStable()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "banana"), W(2, "Apple"), W(3, "cherry"), W(4, "apple")];
        f.Vm.OnChanged();
        Assert.Equal(new nint[] { 2, 4, 1, 3 }, Hwnds(f.Vm));
    }

    [Fact]
    public void SortOrder_ZOrder_KeepsEnumerationOrder()
    {
        var f = new Fixture(Defaults with { SortOrder = SortOrder.ZOrder });
        f.Enum.Windows = [W(1, "zeta"), W(2, "alpha"), W(3, "mid")];
        f.Vm.OnChanged();
        Assert.Equal(new nint[] { 1, 2, 3 }, Hwnds(f.Vm));
    }

    [Fact]
    public void Activate_Success_HideOnActivateTrue_RequestsMinimizeOnce()
    {
        var f = new Fixture(Defaults with { HideOnActivate = true });
        f.Enum.Windows = [W(1, "a")];
        f.Vm.OnChanged();
        f.Vm.Activate(f.Vm.Entries[0]);
        Assert.Equal(1, f.MinimizeCount);
        Assert.Single(f.Act.Calls);
        Assert.Equal((nint)1, f.Act.Calls[0].Hwnd);
        Assert.Equal(101u, f.Act.Calls[0].Pid);
        Assert.Equal("Chrome_WidgetWin_1", f.Act.Calls[0].Class);
    }

    [Fact]
    public void Activate_Success_HideOnActivateFalse_NoMinimize()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a")];
        f.Vm.OnChanged();
        f.Vm.Activate(f.Vm.Entries[0]);
        Assert.Equal(0, f.MinimizeCount);
    }

    [Fact]
    public void Disambiguation_AndEdition_AreApplied()
    {
        var f = new Fixture(Defaults with { SortOrder = SortOrder.ZOrder });
        f.Enum.Windows = [W(1, "proj"), W(2, "proj", "Code - Insiders.exe"), W(3, "solo")];
        f.Vm.OnChanged();
        Assert.Equal(VsCodeEdition.Insiders, f.Vm.Entries[1].Edition);
        Assert.Equal("proj (Insiders)", f.Vm.Entries[1].DisplayName);
        Assert.Equal("proj", f.Vm.Entries[0].DisplayName);
        Assert.Equal("solo", f.Vm.Entries[2].DisplayName);
    }

    [Fact]
    public void Activate_AlreadyForeground_MinimizesInsteadOfActivating()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a"), W(2, "b")];
        f.Vm = new MainViewModel(f.Enum, new FakeParser(), f.Act, f.Clock, Defaults, () => f.MinimizeCount++,
            previousForeground: () => 1);
        f.Vm.OnChanged();

        f.Vm.Activate(f.Vm.Entries.First(e => e.Hwnd == 1));
        Assert.Equal(new nint[] { 1 }, f.Act.MinimizeCalls);
        Assert.Empty(f.Act.Calls);

        f.Vm.Activate(f.Vm.Entries.First(e => e.Hwnd == 2));
        Assert.Single(f.Act.MinimizeCalls);
        Assert.Single(f.Act.Calls);
    }

    [Fact]
    public void Activate_PreviousForegroundAlreadyMinimized_FallsBackToActivate()
    {
        var f = new Fixture();
        f.Enum.Windows = [W(1, "a")];
        f.Vm = new MainViewModel(f.Enum, new FakeParser(), f.Act, f.Clock, Defaults, () => f.MinimizeCount++,
            previousForeground: () => 1);
        f.Act.MinimizeResult = false;
        f.Vm.OnChanged();

        f.Vm.Activate(f.Vm.Entries[0]);
        Assert.Single(f.Act.MinimizeCalls);
        Assert.Single(f.Act.Calls);
    }
}
