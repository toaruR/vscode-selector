using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace VscodeSelector.Tests;

public class OptionsLoaderTests : IDisposable
{
    private sealed class ListLogger : ILogger
    {
        public List<string> Warns { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) => Warns.Add(message);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vsel-opt-" + Guid.NewGuid().ToString("N"));
    private readonly ListLogger _log = new();

    public OptionsLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Write(string json)
    {
        var p = Path.Combine(_dir, "settings.json");
        File.WriteAllText(p, json);
        return p;
    }

    private static void AssertDefaults(Options o)
    {
        Assert.Equal([" - "], o.TitleSeparators);
        Assert.Null(o.TitlePattern);
        Assert.Empty(o.ExtraProcessNames);
        Assert.Equal(SortOrder.Name, o.SortOrder);
        Assert.False(o.Topmost);
        Assert.False(o.HideOnActivate);
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var o = OptionsLoader.Load(Path.Combine(_dir, "none.json"), _log);
        AssertDefaults(o);
        Assert.Empty(_log.Warns);
    }

    [Fact]
    public void ValidFileIsApplied()
    {
        var p = Write("{\"titleSeparators\":[\" | \",\" - \"],\"titlePattern\":\"(?<project>.+) X\","
            + "\"extraProcessNames\":[\"Code-portable\"],\"sortOrder\":\"zorder\",\"topmost\":true,"
            + "\"hideOnActivate\":true,\"unknown\":123}");
        var o = OptionsLoader.Load(p, _log);
        Assert.Equal([" | ", " - "], o.TitleSeparators);
        Assert.Equal("(?<project>.+) X", o.TitlePattern);
        Assert.Equal(["Code-portable"], o.ExtraProcessNames);
        Assert.Equal(SortOrder.ZOrder, o.SortOrder);
        Assert.True(o.Topmost);
        Assert.True(o.HideOnActivate);
        Assert.Empty(_log.Warns);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[1,2]")]
    public void InvalidJsonReturnsDefaultsAndKeepsFile(string content)
    {
        var p = Write(content);
        var o = OptionsLoader.Load(p, _log);
        AssertDefaults(o);
        Assert.True(_log.Warns.Count >= 1);
        Assert.Equal(content, File.ReadAllText(p));
    }

    [Fact]
    public void TypeViolationsResetOnlyThatKey()
    {
        var p = Write("{\"titleSeparators\":\"x\",\"extraProcessNames\":[\"a\",1],\"sortOrder\":\"zorder\","
            + "\"topmost\":\"yes\",\"hideOnActivate\":true,\"bogus\":{}}");
        var o = OptionsLoader.Load(p, _log);
        Assert.Equal([" - "], o.TitleSeparators);
        Assert.Empty(o.ExtraProcessNames);
        Assert.Equal(SortOrder.ZOrder, o.SortOrder);
        Assert.False(o.Topmost);
        Assert.True(o.HideOnActivate);
        Assert.Equal(3, _log.Warns.Count);
        Assert.DoesNotContain(_log.Warns, w => w.Contains("bogus"));
    }

    [Theory]
    [InlineData("{\"titleSeparators\":[]}")]
    [InlineData("{\"titleSeparators\":[\"\"]}")]
    [InlineData("{\"sortOrder\":\"other\"}")]
    [InlineData("{\"titlePattern\":5}")]
    public void OtherViolationsWarn(string json)
    {
        var o = OptionsLoader.Load(Write(json), _log);
        AssertDefaults(o);
        Assert.Single(_log.Warns);
    }

    [Theory]
    [InlineData("(?<project>")]
    [InlineData("no group here")]
    [InlineData("(?<other>.+)")]
    public void BadTitlePatternBecomesNullWithWarning(string pattern)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["titlePattern"] = pattern, ["topmost"] = true });
        var o = OptionsLoader.Load(Write(json), _log);
        Assert.Null(o.TitlePattern);
        Assert.True(o.Topmost);
        Assert.Single(_log.Warns);
    }
}
