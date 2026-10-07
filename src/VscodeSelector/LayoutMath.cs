namespace VscodeSelector;

internal static class LayoutMath
{
    public static int Columns(double windowWidth)
        => (int)Math.Floor((windowWidth - Constants.ScrollBarWidth) / Constants.ButtonCellWidth);

    /// <summary>列数を保ったまま余り幅を各列へ配分したセル幅。端数は切り捨てて折り返しを防ぐ。</summary>
    public static double CellWidth(double listWidth)
    {
        int cols = Math.Max(1, (int)Math.Floor(listWidth / Constants.ButtonCellWidth));
        return Math.Max(Constants.ButtonCellWidth, Math.Floor(listWidth / cols));
    }

    public static int Rows(double windowHeight)
        => (int)Math.Floor((windowHeight - Constants.StatusBarHeight)
                           / (Constants.ButtonHeight + 2 * Constants.ButtonMargin));

    public static string Truncate(string s)
        => s.Length <= Constants.MaxLabelChars
            ? s
            : s.Substring(0, Constants.MaxLabelChars - 1) + "…";
}
