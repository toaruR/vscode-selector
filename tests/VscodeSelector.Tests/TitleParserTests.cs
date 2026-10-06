using Xunit;

namespace VscodeSelector.Tests;

public class TitleParserTests
{
    private const string Dot = "●";
    private const string Ellipsis = "…";
    private static readonly TitleParser P = new();

    private static Options Opt(string? pattern = null) =>
        new([" - "], pattern, [], SortOrder.Name, false, false);

    private static ParsedTitle Parse(string t, string? pattern = null) => P.Parse(t, Opt(pattern));

    [Theory]
    [InlineData("main.ts - my-project - Visual Studio Code", "my-project")]
    [InlineData(Dot + " main.ts - my-project - Visual Studio Code - Insiders", "my-project")]
    [InlineData("my-project - Visual Studio Code", "my-project")]
    [InlineData("日本語フォルダ - Visual Studio Code", "日本語フォルダ")]
    public void Simple(string title, string name)
    {
        var r = Parse(title);
        Assert.Equal(name, r.ProjectName);
        Assert.Equal(RemoteKind.None, r.Remote);
        Assert.Null(r.RemoteLabel);
        Assert.False(r.IsWorkspaceFile);
        Assert.False(r.IsNoFolder);
    }

    [Fact]
    public void T2d_Wsl()
    {
        var r = Parse("a.ts - app [WSL: Ubuntu] - Visual Studio Code");
        Assert.Equal("app", r.ProjectName);
        Assert.Equal(RemoteKind.Wsl, r.Remote);
        Assert.Equal("WSL: Ubuntu", r.RemoteLabel);
    }

    [Fact]
    public void T2e_Workspace()
    {
        var r = Parse("x - team (Workspace) - Visual Studio Code");
        Assert.Equal("team", r.ProjectName);
        Assert.True(r.IsWorkspaceFile);
        Assert.False(r.IsNoFolder);
    }

    [Theory]
    [InlineData("Welcome - Visual Studio Code")]
    [InlineData("Visual Studio Code")]
    public void T2f_T2g_NoFolder(string title)
    {
        var r = Parse(title);
        Assert.Equal("(No Folder)", r.ProjectName);
        Assert.True(r.IsNoFolder);
        Assert.Equal(RemoteKind.None, r.Remote);
        Assert.False(r.IsWorkspaceFile);
    }

    [Theory]
    [InlineData("a.ts - app [SSH: host] - Visual Studio Code", RemoteKind.Ssh)]
    [InlineData("a.ts - app [Dev Container: x] - Visual Studio Code", RemoteKind.DevContainer)]
    [InlineData("a.ts - app [Codespaces: y] - Visual Studio Code", RemoteKind.Codespaces)]
    [InlineData("a.ts - app [Foo] - Visual Studio Code", RemoteKind.Other)]
    public void RemoteKinds(string title, RemoteKind kind)
    {
        var r = Parse(title);
        Assert.Equal("app", r.ProjectName);
        Assert.Equal(kind, r.Remote);
    }

    [Fact]
    public void AdministratorPrefixAndStarDirty()
    {
        Assert.Equal("p", Parse("[Administrator] * a.ts - p - Visual Studio Code").ProjectName);
    }

    [Fact]
    public void T4_KnownLimitAndPattern()
    {
        const string t = "a.ts - my - app - Visual Studio Code";
        Assert.Equal("app", Parse(t).ProjectName);
        Assert.Equal("my - app",
            Parse(t, @"^\S+ - (?<project>.+) - Visual Studio Code$").ProjectName);
    }

    [Fact]
    public void T5_Truncate()
    {
        var r41 = Parse(new string('A', 41));
        Assert.Equal(new string('A', 39) + Ellipsis, r41.ProjectName);
        Assert.Equal(40, r41.ProjectName.Length);
        var s40 = new string('A', 40);
        Assert.Equal(s40, Parse(s40).ProjectName);
    }

    [Theory]
    [InlineData("(")]
    [InlineData(@"^(?<x>.+)$")]
    [InlineData("")]
    public void InvalidPattern_TreatedAsNull(string pattern)
    {
        var r = Parse("main.ts - my-project - Visual Studio Code", pattern);
        Assert.Equal("my-project", r.ProjectName);
    }

    [Fact]
    public void PatternMismatch_FallsThroughToP1()
    {
        Assert.Equal("my-project",
            Parse("main.ts - my-project - Visual Studio Code", @"^zzz(?<project>.+)$").ProjectName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("random title")]
    [InlineData(" - Visual Studio Code")]
    public void NoThrow_FullTextFallback(string title)
    {
        var r = Parse(title);
        Assert.Equal(title, r.ProjectName);
        Assert.False(r.IsNoFolder);
    }

    [Fact]
    public void NullTitle_NoThrow()
    {
        Assert.Equal("", P.Parse(null!, Opt()).ProjectName);
    }

    [Fact]
    public void RoundTrip_T2b_T2d_T2e()
    {
        var tb = Dot + " main.ts - my-project - Visual Studio Code - Insiders";
        var b = Parse(tb);
        Assert.Equal(tb, $"{Dot} main.ts - {b.ProjectName} - Visual Studio Code - Insiders");

        var td = "a.ts - app [WSL: Ubuntu] - Visual Studio Code";
        var d = Parse(td);
        Assert.Equal(td, $"a.ts - {d.ProjectName} [{d.RemoteLabel}] - Visual Studio Code");

        var te = "x - team (Workspace) - Visual Studio Code";
        var e = Parse(te);
        Assert.True(e.IsWorkspaceFile);
        Assert.Equal(te, $"x - {e.ProjectName} (Workspace) - Visual Studio Code");
    }
}
