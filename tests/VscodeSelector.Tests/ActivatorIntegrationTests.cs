using System.Runtime.InteropServices;
using Xunit;

namespace VscodeSelector.Tests;

[Trait("Category", "Interactive")]
public class ActivatorIntegrationTests
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(nint h);

    private sealed class RecordingApi(IForegroundApi inner) : IForegroundApi
    {
        public List<string> Calls { get; } = new();
        public bool IsWindow(nint h) => inner.IsWindow(h);
        public uint GetWindowProcessId(nint h) => inner.GetWindowProcessId(h);
        public string GetClassName(nint h) => inner.GetClassName(h);
        public bool IsIconic(nint h) => inner.IsIconic(h);
        public void ShowWindowRestore(nint h) { Calls.Add("ShowWindow(SW_RESTORE)"); inner.ShowWindowRestore(h); }
        public bool SetForegroundWindow(nint h) { Calls.Add("SetForegroundWindow"); return inner.SetForegroundWindow(h); }
        public void AllowSetForegroundWindowAny() { Calls.Add("AllowSetForegroundWindow"); inner.AllowSetForegroundWindowAny(); }
        public void KeybdEvent(byte vk, bool up) { Calls.Add("keybd_event"); inner.KeybdEvent(vk, up); }
        public uint GetForegroundThreadId() => inner.GetForegroundThreadId();
        public uint GetCurrentThreadId() => inner.GetCurrentThreadId();
        public bool AttachThreadInput(uint a, uint b, bool at) { Calls.Add("AttachThreadInput"); return inner.AttachThreadInput(a, b, at); }
        public bool BringWindowToTop(nint h) { Calls.Add("BringWindowToTop"); return inner.BringWindowToTop(h); }
        public void FlashWindow(nint h) { Calls.Add("FlashWindowEx"); inner.FlashWindow(h); }
        public void Sleep(int ms) => inner.Sleep(ms);
    }

    private const string Cls = "Chrome_WidgetWin_1";

    [Fact]
    public void Activate_MinimizedWindow_RestoresThenSetsForeground()
    {
        using var host = new TestWindowHost(new[] { (Cls, "T007-min-" + Guid.NewGuid().ToString("N"), true) });
        nint hwnd = host.Handles[0];
        Assert.True(IsIconic(hwnd));
        var api = new RecordingApi(new Win32ForegroundApi());
        var result = new WindowActivator(api).Activate(hwnd, (uint)Environment.ProcessId, Cls);

        Assert.True(result is ActivationResult.Success or ActivationResult.ForegroundBlocked, $"result={result}");
        Assert.False(IsIconic(hwnd));
        Assert.True(api.Calls.Count >= 2);
        Assert.Equal("ShowWindow(SW_RESTORE)", api.Calls[0]);
        Assert.Equal("SetForegroundWindow", api.Calls[1]);
        if (result == ActivationResult.Success) Assert.Equal(hwnd, GetForegroundWindow());
        else Assert.Contains("FlashWindowEx", api.Calls);
    }

    [Fact]
    public void Activate_NonMinimizedWindow_DoesNotCallShowWindow()
    {
        using var host = new TestWindowHost(new[] { (Cls, "T007-nor-" + Guid.NewGuid().ToString("N"), false) });
        nint hwnd = host.Handles[0];
        var api = new RecordingApi(new Win32ForegroundApi());
        var result = new WindowActivator(api).Activate(hwnd, (uint)Environment.ProcessId, Cls);

        Assert.True(result is ActivationResult.Success or ActivationResult.ForegroundBlocked, $"result={result}");
        Assert.DoesNotContain("ShowWindow(SW_RESTORE)", api.Calls);
        Assert.Equal("SetForegroundWindow", api.Calls[0]);
        if (result == ActivationResult.Success) Assert.Equal(hwnd, GetForegroundWindow());
        else Assert.Contains("FlashWindowEx", api.Calls);
    }

    [Fact]
    public void Activate_MinimizedWindow_WithRealActivator_RestoresWindow()
    {
        using var host = new TestWindowHost(new[] { (Cls, "T007-real-" + Guid.NewGuid().ToString("N"), true) });
        nint hwnd = host.Handles[0];
        var result = new WindowActivator().Activate(hwnd, (uint)Environment.ProcessId, Cls);
        Assert.True(result is ActivationResult.Success or ActivationResult.ForegroundBlocked);
        Assert.False(IsIconic(hwnd));
    }
}
