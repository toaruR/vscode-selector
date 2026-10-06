using Xunit;

namespace VscodeSelector.Tests;

public class WindowFilterTests
{
    private static readonly string[] None = [];

    private static WindowCandidate Ok(
        string? exe = "Code.exe", string title = "a.cs - proj - Visual Studio Code",
        string cls = "Chrome_WidgetWin_1", bool visible = true, bool owner = false,
        bool tool = false, bool cloaked = false, bool min = false)
        => new(1, 10, cls, title, visible, owner, tool, cloaked, min, exe);

    [Fact]
    public void Baseline_Accepted()
    {
        var r = WindowFilter.Evaluate(Ok(), None);
        Assert.NotNull(r);
        Assert.Equal("Code.exe", r!.Value.ProcessName);
        Assert.False(r.Value.IsMinimized);
    }

    [Fact]
    public void Cloaked_Excluded() => Assert.Null(WindowFilter.Evaluate(Ok(cloaked: true), None));

    [Fact]
    public void Owner_Excluded() => Assert.Null(WindowFilter.Evaluate(Ok(owner: true), None));

    [Fact]
    public void EmptyTitle_Excluded() => Assert.Null(WindowFilter.Evaluate(Ok(title: ""), None));

    [Fact]
    public void ToolWindow_Excluded() => Assert.Null(WindowFilter.Evaluate(Ok(tool: true), None));

    [Fact]
    public void Invisible_Excluded() => Assert.Null(WindowFilter.Evaluate(Ok(visible: false), None));

    [Theory]
    [InlineData("Chrome_WidgetWin_0")]
    [InlineData("chrome_widgetwin_1")]
    [InlineData("Other")]
    public void WrongClass_Excluded(string cls) => Assert.Null(WindowFilter.Evaluate(Ok(cls: cls), None));

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("chrome.exe")]
    [InlineData("slack.exe")]
    public void NonVsCodeExe_Rejected(string exe) => Assert.Null(WindowFilter.Evaluate(Ok(exe: exe), None));

    [Theory]
    [InlineData("Code.exe")]
    [InlineData("Code - Insiders.exe")]
    [InlineData("VSCodium.exe")]
    [InlineData("code.EXE")]
    [InlineData("VSCODIUM.exe")]
    public void StandardExe_AcceptedCaseInsensitive(string exe)
        => Assert.NotNull(WindowFilter.Evaluate(Ok(exe: exe), None));

    [Theory]
    [InlineData("slack.exe", "slack")]
    [InlineData("Slack.exe", "SLACK")]
    [InlineData("SLACK.EXE", "slack")]
    public void ExtraProcessNames_Accepted(string exe, string extra)
        => Assert.NotNull(WindowFilter.Evaluate(Ok(exe: exe), [extra]));

    [Fact]
    public void ExtraProcessNames_DoNotAffectOthers()
        => Assert.Null(WindowFilter.Evaluate(Ok(exe: "chrome.exe"), ["slack"]));

    [Fact]
    public void OpenProcessFailed_AppReMatch_AcceptedWithEmptyNameAndOneLog()
    {
        var logs = new List<string>();
        var r = WindowFilter.Evaluate(Ok(exe: null), None, logs.Add);
        Assert.NotNull(r);
        Assert.Equal("", r!.Value.ProcessName);
        Assert.Single(logs);
    }

    [Fact]
    public void OpenProcessFailed_AppReMismatch_ExcludedNoLog()
    {
        var logs = new List<string>();
        Assert.Null(WindowFilter.Evaluate(Ok(exe: null, title: "foo - Notepad"), None, logs.Add));
        Assert.Empty(logs);
    }

    [Fact]
    public void ExeKnown_NoFallbackLog()
    {
        var logs = new List<string>();
        Assert.NotNull(WindowFilter.Evaluate(Ok(), None, logs.Add));
        Assert.Empty(logs);
    }

    [Fact]
    public void Minimized_AcceptedAndPreserved()
    {
        var r = WindowFilter.Evaluate(Ok(min: true), None);
        Assert.NotNull(r);
        Assert.True(r!.Value.IsMinimized);
    }
}
