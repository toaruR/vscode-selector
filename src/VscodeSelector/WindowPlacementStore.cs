using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace VscodeSelector;

/// <summary>ウィンドウの位置とサイズ (DIP) を window.json に保存・復元する。例外は投げない。</summary>
public static class WindowPlacementStore
{
    // 画面外に消えないよう、仮想スクリーンとこれ以上重なっていれば復元する。
    private const double MinVisible = 40;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VsCodeSelector", "window.json");

    public static Rect? Load(string path, ILogger logger)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var r = new Rect(
                root.GetProperty("left").GetDouble(), root.GetProperty("top").GetDouble(),
                root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble());
            if (!IsFinite(r) || r.Width <= 0 || r.Height <= 0)
            {
                logger.Warn($"window placement invalid ({path}); using default");
                return null;
            }
            return r;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.Warn($"window placement read failed ({path}): {ex.Message}; using default");
            return null;
        }
    }

    public static void Save(string path, Rect r, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                left = r.Left, top = r.Top, width = r.Width, height = r.Height
            }));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.Warn($"window placement write failed ({path}): {ex.Message}");
        }
    }

    /// <summary>保存位置が現在の仮想スクリーンと十分重なるか (モニタ構成変更で画面外に出るのを防ぐ)。</summary>
    public static bool IsVisible(Rect r, Rect screen)
    {
        var i = Rect.Intersect(r, screen);
        return !i.IsEmpty && i.Width >= MinVisible && i.Height >= MinVisible;
    }

    private static bool IsFinite(Rect r) =>
        double.IsFinite(r.Left) && double.IsFinite(r.Top) && double.IsFinite(r.Width) && double.IsFinite(r.Height);
}
