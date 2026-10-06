using System;
using System.IO;
using System.Globalization;
using System.Text;

namespace VscodeSelector;

/// <summary>日次ファイル app-YYYYMMDD.log に追記するロガー。古いファイルは削除しない。例外は投げない。</summary>
public sealed class FileLogger : ILogger
{
    private readonly string _dir;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();

    public FileLogger(string logDirectory, Func<DateTime>? now = null)
    {
        _dir = logDirectory;
        _now = now ?? (() => DateTime.Now);
    }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VsCodeSelector", "logs");

    public void Debug(string message) => Write("DEBUG", message);
    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            var t = _now();
            var file = Path.Combine(_dir, "app-" + t.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            var line = t.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                       + " [" + level + "] " + message + Environment.NewLine;
            lock (_gate)
            {
                Directory.CreateDirectory(_dir);
                File.AppendAllText(file, line, new UTF8Encoding(false));
            }
        }
        catch
        {
            // ログ失敗でアプリを止めない
        }
    }
}
