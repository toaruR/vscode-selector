using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace VscodeSelector.Tests;

/// <summary>STA スレッド上で Dispatcher を起動し、関数を実行して結果/例外を呼び出し元へ返す。</summary>
internal static class StaTestHelper
{
    public static T Run<T>(Func<T> func, int timeoutMs = 30000)
    {
        T result = default!;
        ExceptionDispatchInfo? error = null;
        Dispatcher? dispatcher = null;
        using var started = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(() =>
            {
                try { result = func(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            started.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(timeoutMs))
        {
            started.Wait();
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
            throw new TimeoutException("STA thread did not finish in " + timeoutMs + " ms.");
        }
        error?.Throw();
        return result;
    }

    public static void Run(Action action) => Run<object?>(() => { action(); return null; });
}
