using System;
using System.IO;
using Xunit;

namespace VscodeSelector.Tests;

public class TitlePathSettingsTests : IDisposable
{
    private const string Marker = "⟨${rootPath}⟩";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vsel-tps-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void ExistingTitle_InsertsAfterRootName_KeepsComments()
    {
        var text = "{\n\t// c \"window.title\": \"x\"\n\t\"window.title\": \"${dirty}${rootName}${separator}${appName}\", /* k */\n\t\"a\": 1,\n}";
        var r = TitlePathSettings.AddMarker(text);
        Assert.Equal(text.Replace("${rootName}${separator}", "${rootName} " + Marker + "${separator}"), r);
    }

    [Theory]
    [InlineData("${activeEditorShort}${separator}${appName}", "${activeEditorShort} " + Marker + "${separator}${appName}")]
    [InlineData("${activeEditorShort}", "${activeEditorShort} " + Marker)]
    public void ExistingTitle_WithoutRootName(string before, string after)
    {
        Assert.Equal("{\"window.title\": \"" + after + "\"}", TitlePathSettings.AddMarker("{\"window.title\": \"" + before + "\"}"));
    }

    [Fact]
    public void AlreadyInstalled_Unchanged()
    {
        var text = "{ \"window.title\": \"${rootName} " + Marker + "\" }";
        Assert.Same(text, TitlePathSettings.AddMarker(text));
    }

    [Fact]
    public void NoTitle_InsertsBeforeFirstKey_WithIndent()
    {
        var text = "{\n    \"a\": { \"window.title\": \"nested\" },\n    \"b\": [1, \"s\"]\n}";
        var r = TitlePathSettings.AddMarker(text);
        Assert.Equal("{\n    \"window.title\": \"" + TitlePathSettings.DefaultTitle + "\",\n    \"a\"" + text[(text.IndexOf("\"a\"") + 3)..], r);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("// only comment\n{\n}")]
    public void EmptySettings_AddsTitle(string text)
    {
        var r = TitlePathSettings.AddMarker(text);
        Assert.NotNull(r);
        Assert.Contains("\"window.title\": \"" + TitlePathSettings.DefaultTitle + "\"", r);
        Assert.Contains(Marker, TitlePathSettings.DefaultTitle);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("{ \"a\": \"unterminated }")]
    [InlineData("{ /* open ")]
    public void Unparseable_ReturnsNull(string text)
    {
        Assert.Null(TitlePathSettings.AddMarker(text));
    }

    [Fact]
    public void Install_WritesBackupAndCreatesMissing()
    {
        var existing = Path.Combine(_dir, "a", "settings.json");
        var missing = Path.Combine(_dir, "b", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "{\"x\": 1}");

        Assert.Empty(TitlePathSettings.Install([existing, missing], null));

        Assert.Equal("{\"x\": 1}", File.ReadAllText(existing + ".bak"));
        Assert.Contains(Marker, File.ReadAllText(existing));
        Assert.Contains(Marker, File.ReadAllText(missing));
        Assert.False(File.Exists(missing + ".bak"));
    }
}
