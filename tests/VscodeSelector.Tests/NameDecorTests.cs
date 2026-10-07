using System.Linq;
using Xunit;

namespace VscodeSelector.Tests;

public class NameDecorTests
{
    private static string Heads(string name)
        => string.Concat(NameDecor.Segment(name).Where(s => s.IsHead).Select(s => s.Text));

    [Theory]
    [InlineData("vscode_selector", "vs")]
    [InlineData("vscode-selector", "vs")]
    [InlineData("vscodeSelector", "vS")]
    [InlineData("VscodeSelector", "VS")]
    [InlineData("HTTPServer", "HS")]
    [InlineData("my app.v2", "mav2")]
    [InlineData("__init__", "i")]
    [InlineData("", "")]
    public void Segment_HeadsAreWordInitials(string name, string expected)
        => Assert.Equal(expected, Heads(name));

    [Theory]
    [InlineData("vscode_selector")]
    [InlineData("HTTPServer")]
    [InlineData("a__b")]
    public void Segment_ConcatenationRestoresName(string name)
        => Assert.Equal(name, string.Concat(NameDecor.Segment(name).Select(s => s.Text)));

    [Fact]
    public void Hash_IsDeterministicAndDistinguishesNames()
    {
        Assert.Equal(NameDecor.Hash("proj"), NameDecor.Hash("proj"));
        Assert.NotEqual(NameDecor.Hash("proj"), NameDecor.Hash("proj2"));
    }

    [Fact]
    public void Pattern_IsHorizontallySymmetric()
    {
        var p = NameDecor.Pattern("vscode-selector");
        int n = NameDecor.IdenticonCells;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                Assert.Equal(p[y, x], p[y, n - 1 - x]);
    }

    [Fact]
    public void Identicon_IsFrozenAndCached()
    {
        var a = NameDecor.Identicon("proj");
        Assert.True(a.IsFrozen);
        Assert.Same(a, NameDecor.Identicon("proj"));
    }
}
