using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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
    }

    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProjectEntry pe }) _vm.Activate(pe);
    }
}
