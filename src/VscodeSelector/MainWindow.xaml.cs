using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 最大化不要: キャプションのダブルクリックや Win+↑ で最大化されないようにする。
        nint hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GwlStyle, GetWindowLong(hwnd, GwlStyle) & ~WsMaximizeBox);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProjectEntry pe }) _vm.Activate(pe);
    }
}
