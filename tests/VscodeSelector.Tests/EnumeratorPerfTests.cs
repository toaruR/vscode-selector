using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace VscodeSelector.Tests;

[Trait("Category", "Interactive")]
public class EnumeratorPerfTests(ITestOutputHelper output)
{
    private const int Warmup = 10;
    private const int Samples = 100;
    private const int MaxAttempts = 3;

    [Fact]
    public void Enumerate_P95_WithinBudget_For200Windows()
    {
        var specs = Enumerable.Range(0, Constants.EnumBudgetWindows)
            .Select(i => ("T005_OtherClass", "T005-" + i, false))
            .ToList();
        using var host = new TestWindowHost(specs);
        var exe = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule!.FileName);
        var e = new Win32WindowEnumerator(new[] { exe });

        for (int i = 0; i < Warmup; i++) e.Enumerate();

        bool passed = false;
        for (int attempt = 1; attempt <= MaxAttempts && !passed; attempt++)
        {
            var times = new double[Samples];
            for (int i = 0; i < Samples; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                e.Enumerate();
                times[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            }
            Array.Sort(times);
            double p95 = times[94];
            output.WriteLine($"attempt {attempt}: p95 = {p95:F3} ms (budget {Constants.EnumBudgetMs} ms)");
            Assert.True(p95 <= 2 * Constants.EnumBudgetMs, $"p95 {p95:F3} ms exceeds 2x budget");
            if (p95 <= Constants.EnumBudgetMs) passed = true;
        }
        Assert.True(passed, "p95 exceeded EnumBudgetMs in all attempts");
    }
}
