using Xunit;

namespace VscodeSelector.Tests;

public class EnumeratorErrorTests
{
    [Fact]
    public void Enumerate_WhenEnumWindowsFails_ThrowsWithWin32Error()
    {
        var e = new Win32WindowEnumerator(
            () => Array.Empty<string>(),
            (Func<nint, bool> cb, out int err) => { err = 5; return false; });
        var ex = Assert.Throws<EnumerationException>(() => e.Enumerate());
        Assert.Equal(5, ex.Win32Error);
    }

    [Fact]
    public void Enumerate_WhenEnumWindowsSucceedsWithNoWindows_ReturnsEmpty()
    {
        var e = new Win32WindowEnumerator(
            () => Array.Empty<string>(),
            (Func<nint, bool> cb, out int err) => { err = 0; return true; });
        Assert.Empty(e.Enumerate());
    }
}
