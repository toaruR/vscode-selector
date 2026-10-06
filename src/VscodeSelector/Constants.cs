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
    public const int MinWidth = 240;
    public const int MinHeight = 120;

    public const int DetectLatencyMax = PollIntervalMs + DebounceMs + EnumBudgetMs;
    public const int DetectLatencyMaxHidden = PollIntervalHiddenMs + DebounceMs + EnumBudgetMs;

    public const string AppRe = @"\s-\s(Visual Studio Code(?: - Insiders)?|VSCodium|Code - OSS)$";
}
