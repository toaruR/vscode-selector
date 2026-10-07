using Windows.Win32;
using Windows.Win32.Foundation;
using Xunit;

namespace VscodeSelector.Tests;

public class SmokeTests
{
    [Fact]
    public void EnumerationException_HoldsWin32Error()
        => Assert.Equal(5, new EnumerationException(5).Win32Error);

    [Fact]
    public void RawWindow_ValueEquality()
    {
        var a = new RawWindow(1, 2, "Code", "Chrome_WidgetWin_1", "t", false);
        var b = new RawWindow(1, 2, "Code", "Chrome_WidgetWin_1", "t", false);
        Assert.Equal(a, b);
        Assert.NotEqual(a, b with { IsMinimized = true });
    }

    [Fact]
    public void VsCodeEdition_HasFiveValues()
    {
        Assert.Equal(5, Enum.GetValues<VsCodeEdition>().Length);
        Assert.True(Enum.IsDefined(VsCodeEdition.Stable));
        Assert.True(Enum.IsDefined(VsCodeEdition.Insiders));
        Assert.True(Enum.IsDefined(VsCodeEdition.VSCodium));
        Assert.True(Enum.IsDefined(VsCodeEdition.Cursor));
        Assert.True(Enum.IsDefined(VsCodeEdition.Antigravity));
    }

    [Fact]
    public void ActivationResult_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<ActivationResult>().Length);
        Assert.True(Enum.IsDefined(ActivationResult.Success));
        Assert.True(Enum.IsDefined(ActivationResult.WindowGone));
        Assert.True(Enum.IsDefined(ActivationResult.ForegroundBlocked));
    }

    [Fact]
    public void CsWin32_IsWindow_NullHwnd_ReturnsFalse()
        => Assert.False((bool)PInvoke.IsWindow(HWND.Null));

    [Fact]
    public void Contracts_Interfaces_ArePublicInterfaces()
    {
        foreach (var t in new[] { typeof(IWindowEnumerator), typeof(ITitleParser), typeof(IWindowActivator), typeof(IChangeNotifier) })
        {
            Assert.True(t.IsInterface);
            Assert.True(t.IsPublic);
        }
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IChangeNotifier)));
    }

    [Fact]
    public void Contracts_IWindowActivator_Activate_Signature()
    {
        var m = typeof(IWindowActivator).GetMethod("Activate");
        Assert.NotNull(m);
        Assert.Equal(typeof(ActivationResult), m!.ReturnType);
        Assert.Equal(new[] { typeof(nint), typeof(uint), typeof(string) }, m.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Fact]
    public void Contracts_IChangeNotifier_Members()
    {
        var t = typeof(IChangeNotifier);
        Assert.NotNull(t.GetEvent("Changed"));
        Assert.NotNull(t.GetProperty("HookActive"));
        Assert.NotNull(t.GetMethod("Start"));
    }

    [Fact]
    public void Contracts_Records_AreRecordsWithExpectedProperties()
    {
        Assert.NotNull(typeof(ProjectEntry).GetProperty("DisplayName"));
        Assert.NotNull(typeof(ProjectEntry).GetProperty("FullTitle"));
        Assert.NotNull(typeof(ParsedTitle).GetProperty("IsNoFolder"));
        Assert.NotNull(typeof(Options).GetProperty("TitleSeparators"));
        Assert.NotNull(typeof(RawWindow).GetProperty("IsMinimized"));
        Assert.Equal(2, Enum.GetValues<SortOrder>().Length);
    }

    [Fact]
    public void CsWin32_PInvoke_IsWindowVisible_NullHwnd_ReturnsFalse()
    {
        Assert.False((bool)PInvoke.IsWindowVisible(HWND.Null));
    }
}
