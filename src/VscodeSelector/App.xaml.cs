using System.Windows;

namespace VscodeSelector;

/// <summary>コンポーネントを組み立てて起動する (設計 §3, §8)。</summary>
public partial class App : Application
{
    private UpdateScheduler? _scheduler;
    private WinEventChangeNotifier? _notifier;
    private ForegroundTracker? _foreground;
    // タイマースレッドから参照するため UI スレッドで StateChanged 時に更新する。
    private volatile bool _minimized;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var logger = new FileLogger(FileLogger.DefaultDirectory);
        var options = OptionsLoader.Load(OptionsLoader.DefaultPath, logger);
        var enumerator = new Win32WindowEnumerator(options.ExtraProcessNames, s => logger.Debug(s));
        var parser = new TitleParser();
        var activator = new WindowActivator();
        var clock = new SystemClock();
        _notifier = new WinEventChangeNotifier();
        var foreground = _foreground = new ForegroundTracker();

        MainWindow? window = null;
        var vm = new MainViewModel(enumerator, parser, activator, clock, options,
            requestMinimize: options.HideOnActivate
                ? () => window!.WindowState = WindowState.Minimized
                : null,
            uiDispatch: a => Dispatcher.BeginInvoke(a),
            logger: logger,
            previousForeground: () => foreground.LastExternal);
        window = new MainWindow(vm) { Topmost = options.Topmost };
        window.StateChanged += (_, _) => _minimized = window.WindowState == WindowState.Minimized;
        RestorePlacement(window, logger);
        window.Closing += (_, _) => WindowPlacementStore.Save(WindowPlacementStore.DefaultPath,
            window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds,
            logger);
        _scheduler = new UpdateScheduler(_notifier, clock, vm.OnChanged, () => _minimized, logger);
        MainWindow = window;
        window.Show();
        vm.OnChanged();
        _scheduler.Start();
    }

    /// <summary>前回終了時の位置とサイズを復元する。画面外になる場合は既定位置のまま。</summary>
    private static void RestorePlacement(Window window, ILogger logger)
    {
        if (WindowPlacementStore.Load(WindowPlacementStore.DefaultPath, logger) is not { } r) return;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!WindowPlacementStore.IsVisible(r, screen))
        {
            logger.Info("saved window placement is off-screen; using default");
            return;
        }
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = r.Left;
        window.Top = r.Top;
        window.Width = r.Width;
        window.Height = r.Height;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scheduler?.Dispose();
        _notifier?.Dispose();
        _foreground?.Dispose();
        base.OnExit(e);
    }
}
