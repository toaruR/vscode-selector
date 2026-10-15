namespace VscodeSelector;

internal static class Constants
{
    public const int PollIntervalMs = 2000;
    public const int PollIntervalHiddenMs = 5000;
    public const int DebounceMs = 250;
    public const int EnumBudgetMs = 50;
    public const int EnumBudgetWindows = 200;
    public const int ActivateMaxAttempts = 4;
    public const int ActivateRetryDelayMs = 50;
    public const int ToastMs = 3000;
    public const int ButtonWidth = 150;
    public const int ButtonHeight = 48;
    public const int ButtonMargin = 6;
    public const int ButtonCellWidth = ButtonWidth + 2 * ButtonMargin;
    public const int ScrollBarWidth = 17;
    public const int MaxLabelChars = 40;
    public const int WindowWidth = 520;
    public const int WindowHeight = 384;
    public const int StatusBarHeight = 24;
    public const int ListHeight = WindowHeight - StatusBarHeight;
    public const int WindowBorder = 1;
    // 1 列/1 行にぴったり縮められる最小。横長 (左バー) 時はバー幅を足した MinWidthWithSideBar を使う。
    public const int MinWidth = ButtonCellWidth + 2 * WindowBorder;
    public const int MinWidthWithSideBar = MinWidth + StatusBarHeight;
    public const int MinHeight = ButtonHeight + 2 * ButtonMargin + 2 * WindowBorder;

    public const int DetectLatencyMax = PollIntervalMs + DebounceMs + EnumBudgetMs;
    public const int DetectLatencyMaxHidden = PollIntervalHiddenMs + DebounceMs + EnumBudgetMs;

    // エディタの window.title に差し込むパス表示 (TitlePathSettings) とその括弧。
    public const string PathMarkerOpen = "⟨";
    public const string PathMarkerClose = "⟩";
    public const string PathMarker = PathMarkerOpen + "${rootPath}" + PathMarkerClose;

    public const string AppRe = @"\s-\s(Visual Studio Code(?: - Insiders)?|VSCodium|Code - OSS|Cursor|Antigravity IDE)$";
}
