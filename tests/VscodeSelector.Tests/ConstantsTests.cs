using System.Text.RegularExpressions;
using Xunit;

namespace VscodeSelector.Tests;

public class ConstantsTests
{
    [Fact]
    public void TableValues_MatchDesign()
    {
        Assert.Equal(2000, Constants.PollIntervalMs);
        Assert.Equal(5000, Constants.PollIntervalHiddenMs);
        Assert.Equal(250, Constants.DebounceMs);
        Assert.Equal(50, Constants.EnumBudgetMs);
        Assert.Equal(200, Constants.EnumBudgetWindows);
        Assert.Equal(4, Constants.ActivateMaxAttempts);
        Assert.Equal(50, Constants.ActivateRetryDelayMs);
        Assert.Equal(3000, Constants.ToastMs);
        Assert.Equal(150, Constants.ButtonWidth);
        Assert.Equal(48, Constants.ButtonHeight);
        Assert.Equal(6, Constants.ButtonMargin);
        Assert.Equal(162, Constants.ButtonCellWidth);
        Assert.Equal(17, Constants.ScrollBarWidth);
        Assert.Equal(40, Constants.MaxLabelChars);
        Assert.Equal(520, Constants.WindowWidth);
        Assert.Equal(384, Constants.WindowHeight);
        Assert.Equal(24, Constants.StatusBarHeight);
        Assert.Equal(360, Constants.ListHeight);
        Assert.Equal(240, Constants.MinWidth);
        Assert.Equal(120, Constants.MinHeight);
    }

    [Fact]
    public void AppRe_IsSameStringAsDesignP1()
    {
        Assert.Equal(@"\s-\s(Visual Studio Code(?: - Insiders)?|VSCodium|Code - OSS)$", Constants.AppRe);
        var re = new Regex(Constants.AppRe);
        Assert.Matches(re, "a - Visual Studio Code - Insiders");
        Assert.Matches(re, "a - VSCodium");
        Assert.DoesNotMatch(re, "a - Notepad");
    }

    [Fact]
    public void DetectLatency_Values()
    {
        Assert.Equal(2300, Constants.DetectLatencyMax);
        Assert.Equal(5300, Constants.DetectLatencyMaxHidden);
    }

    [Fact]
    public void Columns_Values()
    {
        Assert.Equal(3, LayoutMath.Columns(520));
        Assert.Equal(1, LayoutMath.Columns(240));
    }

    [Fact]
    public void Rows_Values()
    {
        Assert.Equal(6, LayoutMath.Rows(384));
        Assert.Equal(5, LayoutMath.Rows(383));
    }

    [Fact]
    public void RetryMaxWait_Is150()
        => Assert.Equal(150, (Constants.ActivateMaxAttempts - 1) * Constants.ActivateRetryDelayMs);

    [Fact]
    public void Truncate_Boundaries()
    {
        var s40 = new string('a', 40);
        Assert.Equal(s40, LayoutMath.Truncate(s40));
        var r = LayoutMath.Truncate(new string('a', 41));
        Assert.Equal(40, r.Length);
        Assert.Equal(new string('a', 39) + "…", r);
    }
}
