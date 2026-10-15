using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace VscodeSelector.Tests;

public class MainWindowLayoutTests
{
    private sealed record Result(int Columns, int TotalRows, int VisibleRows, bool Scrollable);

    private static ProjectEntry Mock(int i) => new(
        (nint)(1000 + i), (uint)(2000 + i), VsCodeEdition.Stable, "proj" + i,
        $"a.ts - proj{i} - Visual Studio Code",
        new ParsedTitle("proj" + i, RemoteKind.None, null, false, false), false);

    private static Result Measure(int count, double w, double h, double? dpi = null)
    {
        var vm = new MainViewModel(new FakeEnumerator(), new FakeParser(), new FakeActivator(), new VirtualClock(),
            new Options(new[] { " - " }, null, Array.Empty<string>(), SortOrder.Name, false, false));
        for (var i = 0; i < count; i++) vm.Entries.Add(Mock(i));
        var window = new MainWindow(vm);
        if (dpi is double s) VisualTreeHelper.SetRootDpi(window, new DpiScale(s, s));
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));
        root.UpdateLayout();
        var list = (ItemsControl)root.FindName("EntriesList");
        var scroll = (ScrollViewer)root.FindName("ListScroll");
        var buttons = Enumerable.Range(0, count)
            .Select(i => (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(i))
            .Where(b => b != null).ToList();
        var pts = buttons.Select(b => b.TranslatePoint(new Point(0, 0), list)).ToList();
        var cols = pts.Count == 0 ? 0 : pts.Count(p => Math.Abs(p.Y - pts[0].Y) < 0.5);
        var rows = pts.Select(p => Math.Round(p.Y)).Distinct().Count();
        var visible = (int)Math.Floor(scroll.ViewportHeight / (Constants.ButtonHeight + 2 * Constants.ButtonMargin));
        return new Result(cols, rows, visible, scroll.ScrollableHeight > 0);
    }

    [Fact]
    public void Count19_ThreeColumns_SixRows_Scrollable()
    {
        var r = StaTestHelper.Run(() => Measure(19, Constants.WindowWidth, Constants.WindowHeight));
        Assert.Equal(3, r.Columns);
        Assert.Equal(6, r.VisibleRows);
        Assert.Equal(7, r.TotalRows);
        Assert.True(r.Scrollable);
    }

    [Fact]
    public void Count18_NotScrollable()
    {
        var r = StaTestHelper.Run(() => Measure(18, Constants.WindowWidth, Constants.WindowHeight));
        Assert.Equal(3, r.Columns);
        Assert.Equal(6, r.TotalRows);
        Assert.False(r.Scrollable);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Dpi_SameLayout(double s)
    {
        var r = StaTestHelper.Run(() => Measure(19, Constants.WindowWidth, Constants.WindowHeight, s));
        Assert.Equal(3, r.Columns);
        Assert.True(r.Scrollable);
    }

    [Fact]
    public void MinSize_Wide_ExactlyOneRow()
    {
        // Measure はクライアント (枠の内側) を測るため枠分を引く。横長なのでバーは左端。
        int b = 2 * Constants.WindowBorder;
        var r = StaTestHelper.Run(() => Measure(3, Constants.MinWidthWithSideBar - b, Constants.MinHeight - b));
        Assert.Equal(1, r.Columns);
        Assert.Equal(1, r.VisibleRows);
    }

    [Fact]
    public void MinWidth_Tall_ExactlyOneColumn()
    {
        int b = 2 * Constants.WindowBorder;
        var r = StaTestHelper.Run(() => Measure(3, Constants.MinWidth - b, 400));
        Assert.Equal(1, r.Columns);
        Assert.Equal(3, r.TotalRows);
    }

    [Fact]
    public void TallWindow_ButtonFillsWidth_NoRightGap()
    {
        int b = 2 * Constants.WindowBorder;
        double w = Constants.MinWidthWithSideBar - b;
        var gap = StaTestHelper.Run(() =>
        {
            var vm = new MainViewModel(new FakeEnumerator(), new FakeParser(), new FakeActivator(), new VirtualClock(),
                new Options(new[] { " - " }, null, Array.Empty<string>(), SortOrder.Name, false, false));
            vm.Entries.Add(Mock(0));
            var window = new MainWindow(vm);
            var root = (FrameworkElement)window.Content;
            for (var i = 0; i < 2; i++)
            {
                root.Measure(new Size(w, 500));
                root.Arrange(new Rect(0, 0, w, 500));
                root.UpdateLayout();
            }
            var list = (ItemsControl)root.FindName("EntriesList");
            var btn = (FrameworkElement)VisualTreeHelper.GetChild(list.ItemContainerGenerator.ContainerFromIndex(0), 0);
            var x = btn.TranslatePoint(new Point(0, 0), root).X;
            return w - (x + btn.ActualWidth + Constants.ButtonMargin);
        });
        Assert.Equal(0, gap, 0.5);
    }

    [Fact]
    public void TallWindow_GripBarOnTop()
    {
        var dock = StaTestHelper.Run(() =>
        {
            var vm = new MainViewModel(new FakeEnumerator(), new FakeParser(), new FakeActivator(), new VirtualClock(),
                new Options(new[] { " - " }, null, Array.Empty<string>(), SortOrder.Name, false, false));
            var window = new MainWindow(vm);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(200, 500));
            root.Arrange(new Rect(0, 0, 200, 500));
            root.UpdateLayout();
            return DockPanel.GetDock((UIElement)root.FindName("GripBar"));
        });
        Assert.Equal(Dock.Top, dock);
    }

    [Fact]
    public void EntryAdded_Overflow_GrowsWindow()
    {
        var (before, after) = StaTestHelper.Run(() =>
        {
            var vm = new MainViewModel(new FakeEnumerator(), new FakeParser(), new FakeActivator(), new VirtualClock(),
                new Options(new[] { " - " }, null, Array.Empty<string>(), SortOrder.Name, false, false));
            // 横長 1 行に 2 列分。
            var window = new MainWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0,
                Width = Constants.MinWidthWithSideBar + Constants.ButtonCellWidth, Height = Constants.MinHeight,
                ShowActivated = false,
            };
            window.Show();
            for (var i = 0; i < 2; i++) vm.Entries.Add(Mock(i));
            Pump();
            var w0 = window.ActualWidth;
            vm.Entries.Add(Mock(2));
            Pump();
            var w1 = window.ActualWidth;
            window.Close();
            return (w0, w1);
        });
        Assert.Equal(before + Constants.ButtonCellWidth, after, 0.5);
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
