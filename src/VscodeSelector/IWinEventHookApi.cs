namespace VscodeSelector;

/// <summary>SetWinEventHook のコールバック (WINEVENTPROC 相当)。</summary>
public delegate void WinEventCallback(uint eventType, nint hwnd, int idObject, int idChild);

/// <summary>WinEventChangeNotifier が使う Win32 フック API の抽象 (設計 §6)。テストでは fake に差し替える。</summary>
public interface IWinEventHookApi
{
    /// <summary>WINEVENT_OUTOFCONTEXT でフック登録。失敗時 0。呼び出しスレッドがメッセージループを持つこと。</summary>
    nint SetWinEventHook(uint eventMin, uint eventMax, WinEventCallback callback);
    /// <summary>登録したスレッドから呼ぶこと。</summary>
    bool UnhookWinEvent(nint hook);
    /// <summary>呼び出しスレッドでメッセージループを回す。QuitMessageLoop が呼ばれるまでブロックする。</summary>
    void RunMessageLoop();
    /// <summary>RunMessageLoop 中のスレッド (nativeThreadId) のループを終了させる。</summary>
    void QuitMessageLoop(uint nativeThreadId);
    uint GetCurrentNativeThreadId();
}
