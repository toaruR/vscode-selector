using Xunit;

namespace VscodeSelector.Tests;

public class WslPathTests
{
    [Theory]
    [InlineData("WSL: Ubuntu", "Ubuntu")]
    [InlineData("WSL: Ubuntu-22.04", "Ubuntu-22.04")]
    [InlineData("WSL", null)]
    [InlineData("SSH: host", null)]
    [InlineData(null, null)]
    public void Distro(string? label, string? distro)
    {
        Assert.Equal(distro, WslPath.Distro(label));
    }

    [Theory]
    [InlineData("~/for_win/goodspress", @"\\wsl.localhost\Ubuntu\home\u\for_win\goodspress")]
    [InlineData("~", @"\\wsl.localhost\Ubuntu\home\u")]
    [InlineData("/var/www/", @"\\wsl.localhost\Ubuntu\var\www")]
    [InlineData("relative/path", null)]
    [InlineData("~user/x", null)]
    public void ToWindows(string linux, string? expected)
    {
        Assert.Equal(expected, WslPath.ToWindows("Ubuntu", linux, _ => "/home/u/"));
    }

    [Fact]
    public void ToWindows_HomeUnknown_ReturnsNull()
    {
        Assert.Null(WslPath.ToWindows("Ubuntu", "~/x", _ => null));
        Assert.Equal(@"\\wsl.localhost\Ubuntu\x", WslPath.ToWindows("Ubuntu", "/x", _ => null));
    }
}
