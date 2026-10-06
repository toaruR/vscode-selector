namespace VscodeSelector;

/// <summary>WindowActivator が使う Win32 前景 API の抽象 (設計 §7)。テストでは fake に差し替える。</summary>
public interface IForegroundApi
{
    bool IsWindow(nint hwnd);
    uint GetWindowProcessId(nint hwnd);
    string GetClassName(nint hwnd);
    bool IsIconic(nint hwnd);
    void ShowWindowRestore(nint hwnd);
    void ShowWindowMinimize(nint hwnd);
    bool SetForegroundWindow(nint hwnd);
    /// <summary>AllowSetForegroundWindow(ASFW_ANY)。</summary>
    void AllowSetForegroundWindowAny();
    void KeybdEvent(byte vk, bool keyUp);
    uint GetForegroundThreadId();
    uint GetCurrentThreadId();
    bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    bool BringWindowToTop(nint hwnd);
    void FlashWindow(nint hwnd);
    void Sleep(int ms);
}
