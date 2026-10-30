using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace VscodeSelector;

/// <summary>ProjectEntry の Edition/Remote を小文字ラベルへ変換する (設計 §8)。</summary>
public sealed class EditionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ProjectEntry e) return "";
        var label = e.Edition.ToString().ToLowerInvariant();
        if (e.Parsed.Remote != RemoteKind.None)
            label += " / " + (e.Parsed.RemoteLabel ?? e.Parsed.Remote.ToString()).ToLowerInvariant();
        return label;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>ProjectEntry の ProjectName からアイデンティコンを生成する。</summary>
public sealed class IdenticonConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProjectEntry e ? NameDecor.Identicon(e.Parsed.ProjectName) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>メインウィンドウ (設計 §8)。</summary>
public partial class MainWindow : Window
{
    public static readonly RoutedUICommand ActivateIndexCommand = new("ActivateIndex", nameof(ActivateIndexCommand), typeof(MainWindow));
    public static readonly RoutedUICommand ActivateFocusedCommand = new("ActivateFocused", nameof(ActivateFocusedCommand), typeof(MainWindow));
    public static readonly RoutedUICommand MinimizeCommand = new("Minimize", nameof(MinimizeCommand), typeof(MainWindow));

    private const int GwlStyle = -16;
    private const int WsMaximizeBox = 0x00010000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint h, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);

    // SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED
    private const uint SwpFrameChanged = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;

    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ActivateIndexCommand, (_, e) =>
        {
            if (e.Parameter is string s && int.TryParse(s, out var i) && i >= 0 && i < _vm.Entries.Count)
                _vm.Activate(_vm.Entries[i]);
        }));
        CommandBindings.Add(new CommandBinding(ActivateFocusedCommand, (_, _) =>
        {
            if (Keyboard.FocusedElement is Button { DataContext: ProjectEntry pe }) _vm.Activate(pe);
        }));
        CommandBindings.Add(new CommandBinding(MinimizeCommand, (_, _) => WindowState = WindowState.Minimized));
        Root.SizeChanged += (_, e) => PlaceGripBar(e.NewSize);
        // 右端の余白をなくすため、ボタン幅を一覧の幅いっぱいに広げる。
        ListScroll.SizeChanged += (_, e) => EntriesList.Tag = LayoutMath.CellWidth(e.NewSize.Width);
        // エディタが増えて収まらなくなった時もリサイズ後と同様に広げる。最小化中は復元時に行う。
        _vm.Entries.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add) ScheduleGrowToFit();
        };
        StateChanged += (_, _) => ScheduleGrowToFit();
    }

    /// <summary>擬似バーを短辺へ置く: 横長なら左端 (縦バー)、縦長なら上端 (横バー)。</summary>
    private void PlaceGripBar(Size size)
    {
        bool vertical = size.Width >= size.Height;
        DockPanel.SetDock(GripBar, vertical ? Dock.Left : Dock.Top);
        GripBar.Width = vertical ? Constants.StatusBarHeight : double.NaN;
        GripBar.Height = vertical ? double.NaN : Constants.StatusBarHeight;
        CloseButton.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        CloseButton.VerticalAlignment = vertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        MinWidth = vertical ? Constants.MinWidthWithSideBar : Constants.MinWidth;
    }

    private void OnGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    /// <summary>一覧の余白でのドラッグ移動。ScrollViewer がクリックを処理済みにするため Preview で拾い、ボタン上は除く。</summary>
    private void OnContentMouseDown(object sender, MouseButtonEventArgs e)
    {
        for (var d = e.OriginalSource as DependencyObject; d != null && d != sender; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is ButtonBase) return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 最大化不要: キャプションのダブルクリックや Win+↑ で最大化されないようにする。
        nint hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GwlStyle, GetWindowLong(hwnd, GwlStyle) & ~WsMaximizeBox);
        // スタイル変更後にフレームを再計算しないと、初回アクティブ化で古いキャプションボタンが描かれ残る。
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, SwpFrameChanged);
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
    }

    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private bool _resizing;

    /// <summary>ドラッグ移動・リサイズの完了後 (自動拡大の後) に発火する。</summary>
    public event Action? MoveSizeEnded;

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmSizing) _resizing = true;
        else if (msg == WmExitSizeMove)
        {
            if (_resizing)
            {
                _resizing = false;
                ScheduleGrowToFit();
            }
            // 自動拡大 (Loaded 優先度) の結果を含めるため、それより低い優先度で通知する。
            Dispatcher.BeginInvoke(() => MoveSizeEnded?.Invoke(), DispatcherPriority.Background);
        }
        return 0;
    }

    /// <summary>確定サイズ・追加ボタンのレイアウトが済んでから測るため Loaded 優先度で後回しにする。</summary>
    private void ScheduleGrowToFit()
        => Dispatcher.BeginInvoke(() =>
        {
            if (WindowState == WindowState.Normal && IsVisible) GrowToFitEntries();
        }, DispatcherPriority.Loaded);

    /// <summary>ボタンが一覧に収まらなければ、長辺方向へウィンドウを広げる (画面端まで)。</summary>
    private void GrowToFitEntries()
    {
        var list = new Size(ListScroll.ActualWidth, ListScroll.ActualHeight);
        var fit = LayoutMath.FitListSize(list, _vm.Entries.Count);
        double right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        double bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (fit.Width > list.Width) Width = Math.Min(ActualWidth + fit.Width - list.Width, right - Left);
        if (fit.Height > list.Height) Height = Math.Min(ActualHeight + fit.Height - list.Height, bottom - Top);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProjectEntry pe }) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            _vm.OpenFolder(pe, () => MessageBox.Show(this,
                $"{pe.Edition} のウィンドウタイトルにフォルダのパスがありません。\n" +
                "settings.json の window.title にパス表示 " + Constants.PathMarker + " を追加しますか？\n" +
                "(変更前のファイルは .bak として保存します)",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        else
            _vm.Activate(pe);
    }
}
