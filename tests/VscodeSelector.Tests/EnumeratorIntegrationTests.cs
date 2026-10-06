using System.IO;
using System.Diagnostics;
using Xunit;

namespace VscodeSelector.Tests;

[Trait("Category", "Interactive")]
public class EnumeratorIntegrationTests
{
    private static string HostExeBaseName()
        => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule!.FileName);

    [Fact]
    public void Enumerate_ReturnsOnlyChromeWidgetWindows_OfExtraProcess()
    {
        string t1 = "T004-a-" + Guid.NewGuid().ToString("N");
        string t2 = "T004-b-" + Guid.NewGuid().ToString("N");
        string t3 = "T004-other-" + Guid.NewGuid().ToString("N");
        using var host = new TestWindowHost(new[]
        {
            ("Chrome_WidgetWin_1", t1, false),
            ("Chrome_WidgetWin_1", t2, false),
            ("T004_OtherClass", t3, false),
        });
        var e = new Win32WindowEnumerator(new[] { HostExeBaseName() });
        var mine = e.Enumerate().Where(w => w.Title.StartsWith("T004-")).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, w => w.Title == t1 && w.Hwnd == host.Handles[0]);
        Assert.Contains(mine, w => w.Title == t2 && w.Hwnd == host.Handles[1]);
        Assert.All(mine, w => Assert.Equal("Chrome_WidgetWin_1", w.ClassName));
        Assert.All(mine, w => Assert.Equal((uint)Environment.ProcessId, w.ProcessId));
    }

    [Fact]
    public void Enumerate_ReportsMinimizedWindow()
    {
        string t = "T004-min-" + Guid.NewGuid().ToString("N");
        using var host = new TestWindowHost(new[] { ("Chrome_WidgetWin_1", t, true) });
        var e = new Win32WindowEnumerator(new[] { HostExeBaseName() });
        var w = Assert.Single(e.Enumerate(), x => x.Title == t);
        Assert.True(w.IsMinimized);
    }
}
