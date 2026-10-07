using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Xunit;

namespace VscodeSelector.Tests;

public class WindowPlacementStoreTests : IDisposable
{
    private sealed class ListLogger : ILogger
    {
        public List<string> Warns { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) => Warns.Add(message);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vsel-wp-" + Guid.NewGuid().ToString("N"));
    private readonly ListLogger _log = new();

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string PathOf => Path.Combine(_dir, "sub", "window.json");

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var r = new Rect(100.5, -20, 640, 300);
        WindowPlacementStore.Save(PathOf, r, _log);
        Assert.Equal(r, WindowPlacementStore.Load(PathOf, _log));
        Assert.Empty(_log.Warns);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNullWithoutWarning()
    {
        Assert.Null(WindowPlacementStore.Load(PathOf, _log));
        Assert.Empty(_log.Warns);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"left\":1,\"top\":2}")]
    [InlineData("{\"left\":1,\"top\":2,\"width\":0,\"height\":10}")]
    [InlineData("{\"left\":\"a\",\"top\":2,\"width\":10,\"height\":10}")]
    public void Load_Invalid_ReturnsNullAndWarns(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        File.WriteAllText(PathOf, json);
        Assert.Null(WindowPlacementStore.Load(PathOf, _log));
        Assert.Single(_log.Warns);
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(1880, 100, true)]    // 右端に 40 残る
    [InlineData(1890, 100, false)]   // 右端に 30 しか残らない
    [InlineData(-5000, 100, false)]  // 外したモニタ上
    public void IsVisible_RequiresOverlap(double left, double top, bool expected)
    {
        var screen = new Rect(0, 0, 1920, 1080);
        Assert.Equal(expected, WindowPlacementStore.IsVisible(new Rect(left, top, 520, 384), screen));
    }
}
