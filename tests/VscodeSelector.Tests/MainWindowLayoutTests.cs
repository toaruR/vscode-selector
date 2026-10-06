using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace VscodeSelector.Tests;

public class MainWindowLayoutTests
{
    private sealed record Result(int Columns, int TotalRows, int VisibleRows, Visibility Bar);

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
        return new Result(cols, rows, visible, scroll.ComputedVerticalScrollBarVisibility);
    }

    [Fact]
    public void Count19_ThreeColumns_SixRows_ScrollVisible()
    {
        var r = StaTestHelper.Run(() => Measure(19, Constants.WindowWidth, Constants.WindowHeight));
        Assert.Equal(3, r.Columns);
        Assert.Equal(6, r.VisibleRows);
        Assert.Equal(7, r.TotalRows);
        Assert.Equal(Visibility.Visible, r.Bar);
    }

    [Fact]
    public void Count18_NoScrollBar()
    {
        var r = StaTestHelper.Run(() => Measure(18, Constants.WindowWidth, Constants.WindowHeight));
        Assert.Equal(3, r.Columns);
        Assert.Equal(6, r.TotalRows);
        Assert.Equal(Visibility.Collapsed, r.Bar);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Dpi_SameLayout(double s)
    {
        var r = StaTestHelper.Run(() => Measure(19, Constants.WindowWidth, Constants.WindowHeight, s));
        Assert.Equal(3, r.Columns);
        Assert.Equal(Visibility.Visible, r.Bar);
    }

    [Fact]
    public void MinSize_OneColumn_OneRow()
    {
        var r = StaTestHelper.Run(() => Measure(1, Constants.MinWidth, Constants.MinHeight));
        Assert.Equal(1, r.Columns);
        Assert.Equal(1, r.TotalRows);
        Assert.Equal(1, r.VisibleRows);
    }
}
