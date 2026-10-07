using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace VscodeSelector;

/// <summary>プロジェクト名の識別補助: 単語先頭文字の強調とアイデンティコン生成。</summary>
public static class NameDecor
{
    public const int IdenticonCells = 5;

    /// <summary>snake / kebab / camel / Pascal / 空白 / 数字境界で区切り、各単語の先頭 1 文字を IsHead=true で返す。</summary>
    public static IReadOnlyList<(string Text, bool IsHead)> Segment(string name)
    {
        var result = new List<(string, bool)>();
        int runStart = 0;
        for (int i = 0; i < name.Length; i++)
        {
            if (!IsHead(name, i)) continue;
            if (i > runStart) result.Add((name[runStart..i], false));
            result.Add((name[i].ToString(), true));
            runStart = i + 1;
        }
        if (runStart < name.Length) result.Add((name[runStart..], false));
        return result;
    }

    private static bool IsHead(string s, int i)
    {
        char c = s[i];
        if (!char.IsLetterOrDigit(c)) return false;
        if (i == 0) return true;
        char p = s[i - 1];
        if (!char.IsLetterOrDigit(p)) return true;
        if (char.IsDigit(c) != char.IsDigit(p)) return true;
        if (char.IsUpper(c) && char.IsLower(p)) return true;
        // 頭字語の終わり: "HTTPServer" の S
        return char.IsUpper(c) && char.IsUpper(p) && i + 1 < s.Length && char.IsLower(s[i + 1]);
    }

    /// <summary>FNV-1a 64bit。string.GetHashCode はプロセスごとにランダム化されるため使わない。</summary>
    public static ulong Hash(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        // 下位ビットの偏りを散らす (SplitMix64 の最終段)
        h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
        h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
        return h ^ (h >> 33);
    }

    /// <summary>色相はパターンと独立した上位ビットから取る。</summary>
    public static double Hue(string name) => (Hash(name) >> 48) % 360;

    /// <summary>左右対称の 5x5 パターン。左 3 列分 (15 ビット) をハッシュから取り、右 2 列はミラー。</summary>
    public static bool[,] Pattern(string name)
    {
        ulong h = Hash(name);
        var cells = new bool[IdenticonCells, IdenticonCells];
        int bit = 0;
        for (int x = 0; x < (IdenticonCells + 1) / 2; x++)
            for (int y = 0; y < IdenticonCells; y++)
            {
                bool on = (h >> bit++ & 1) == 1;
                cells[y, x] = on;
                cells[y, IdenticonCells - 1 - x] = on;
            }
        return cells;
    }

    public static Color AccentColor(string name, double lightness = 0.42) => FromHsl(Hue(name), 0.65, lightness);

    private static readonly Dictionary<string, ImageSource> IconCache = new();
    private static readonly Dictionary<string, Brush> BrushCache = new();

    public static ImageSource Identicon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached)) return cached;
        var fg = new SolidColorBrush(AccentColor(name, 0.5));
        fg.Freeze();
        var bg = new SolidColorBrush(AccentColor(name, 0.93));
        bg.Freeze();
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(bg, null, new RectangleGeometry(new Rect(0, 0, IdenticonCells + 1, IdenticonCells + 1))));
        var cells = new GeometryGroup();
        var p = Pattern(name);
        for (int y = 0; y < IdenticonCells; y++)
            for (int x = 0; x < IdenticonCells; x++)
                if (p[y, x]) cells.Children.Add(new RectangleGeometry(new Rect(x + 0.5, y + 0.5, 1, 1)));
        group.Children.Add(new GeometryDrawing(fg, null, cells));
        var img = new DrawingImage(group);
        img.Freeze();
        return IconCache[name] = img;
    }

    public static Brush AccentBrush(string name)
    {
        if (BrushCache.TryGetValue(name, out var cached)) return cached;
        var b = new SolidColorBrush(AccentColor(name));
        b.Freeze();
        return BrushCache[name] = b;
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        (double r, double g, double b) = hp switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        double m = l - c / 2;
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    // --- XAML 用添付プロパティ ---

    /// <summary>TextBlock に ProjectEntry を渡すと DisplayName を描画し、ProjectName 部分の単語先頭を強調する。</summary>
    public static readonly DependencyProperty EntryProperty = DependencyProperty.RegisterAttached(
        "Entry", typeof(ProjectEntry), typeof(NameDecor), new PropertyMetadata(null, OnEntryChanged));

    public static ProjectEntry? GetEntry(DependencyObject d) => (ProjectEntry?)d.GetValue(EntryProperty);
    public static void SetEntry(DependencyObject d, ProjectEntry? v) => d.SetValue(EntryProperty, v);

    private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.Inlines.Clear();
        if (e.NewValue is not ProjectEntry pe) return;
        string project = pe.Parsed.ProjectName;
        string display = pe.DisplayName;
        // Disambiguator の接尾辞 (" (Insiders)" / " #2" 等) は強調しない
        bool prefixed = project.Length > 0 && display.StartsWith(project, StringComparison.Ordinal);
        string head = prefixed ? project : display;
        var accent = AccentBrush(project);
        foreach (var (text, isHead) in Segment(head))
        {
            var run = new Run(text);
            if (isHead)
            {
                run.Foreground = accent;
                run.FontSize = tb.FontSize * 1.3;
                run.FontWeight = FontWeights.ExtraBold;
            }
            tb.Inlines.Add(run);
        }
        if (prefixed && display.Length > project.Length) tb.Inlines.Add(new Run(display[project.Length..]));
    }
}
