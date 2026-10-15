using System.Windows;

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

    /// <summary>
    /// count 個のボタンが全部収まる一覧サイズ。短辺側の行数/列数は保ち、長辺側だけを広げる
    /// (横長なら幅、縦長なら高さ)。既に収まっていれば元のサイズを返す。
    /// </summary>
    public static Size FitListSize(Size list, int count)
    {
        if (count <= 0) return list;
        const int rowHeight = Constants.ButtonHeight + 2 * Constants.ButtonMargin;
        if (list.Width >= list.Height)
        {
            int rows = Math.Max(1, (int)Math.Floor(list.Height / rowHeight));
            int cols = (count + rows - 1) / rows;
            return new Size(Math.Max(list.Width, cols * Constants.ButtonCellWidth), list.Height);
        }
        else
        {
            int cols = Math.Max(1, (int)Math.Floor(list.Width / Constants.ButtonCellWidth));
            int rows = (count + cols - 1) / cols;
            return new Size(list.Width, Math.Max(list.Height, rows * rowHeight));
        }
    }

    public static string Truncate(string s)
        => s.Length <= Constants.MaxLabelChars
            ? s
            : s.Substring(0, Constants.MaxLabelChars - 1) + "…";
}
