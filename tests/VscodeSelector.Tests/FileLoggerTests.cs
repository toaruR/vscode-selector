using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace VscodeSelector.Tests;

public class FileLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vsel-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void WritesDailyFileAndKeepsOldFiles()
    {
        Directory.CreateDirectory(_dir);
        var old = Path.Combine(_dir, "app-20000101.log");
        File.WriteAllText(old, "old");

        var log = new FileLogger(_dir, () => new DateTime(2025, 3, 7, 12, 0, 0));
        log.Warn("hello");
        log.Info("world");

        var f = Path.Combine(_dir, "app-20250307.log");
        Assert.True(File.Exists(f));
        var lines = File.ReadAllLines(f);
        Assert.Equal(2, lines.Length);
        Assert.Contains("[WARN] hello", lines[0]);
        Assert.True(File.Exists(old));
        Assert.Equal("old", File.ReadAllText(old));
    }

    [Fact]
    public void NewDayCreatesNewFile()
    {
        var day = new DateTime(2025, 1, 31, 23, 59, 0);
        var log = new FileLogger(_dir, () => day);
        log.Error("a");
        day = day.AddMinutes(2);
        log.Debug("b");
        Assert.True(File.Exists(Path.Combine(_dir, "app-20250131.log")));
        Assert.True(File.Exists(Path.Combine(_dir, "app-20250201.log")));
    }

    [Fact]
    public void DoesNotThrowWhenDirectoryIsUnusable()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "file");
        File.WriteAllText(blocker, "x");
        var log = new FileLogger(Path.Combine(blocker, "sub"));
        log.Warn("ignored");
    }
}
