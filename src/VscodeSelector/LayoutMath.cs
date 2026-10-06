namespace VscodeSelector;

internal static class LayoutMath
{
    public static int Columns(double windowWidth)
        => (int)Math.Floor((windowWidth - Constants.ScrollBarWidth) / Constants.ButtonCellWidth);

    public static int Rows(double windowHeight)
        => (int)Math.Floor((windowHeight - Constants.StatusBarHeight)
                           / (Constants.ButtonHeight + 2 * Constants.ButtonMargin));

    public static string Truncate(string s)
        => s.Length <= Constants.MaxLabelChars
            ? s
            : s.Substring(0, Constants.MaxLabelChars - 1) + "…";
}
