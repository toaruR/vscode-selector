using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace VscodeSelector;

/// <summary>WSL 接続ウィンドウの rootPath (例 ~/proj, /home/u/proj) を \\wsl.localhost\ディストリ\... へ変換する。</summary>
public static class WslPath
{
    private const int HomeTimeoutMs = 3000;
    private static readonly ConcurrentDictionary<string, string> HomeCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>リモートラベル "WSL: Ubuntu" からディストリ名を取り出す。取れなければ null。</summary>
    public static string? Distro(string? remoteLabel)
    {
        const string prefix = "WSL: ";
        if (remoteLabel is null || !remoteLabel.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var d = remoteLabel[prefix.Length..].Trim();
        return d.Length == 0 ? null : d;
    }

    /// <summary>Windows から見たパス。~ 始まりは home で展開する (home が null なら変換不可で null)。</summary>
    public static string? ToWindows(string distro, string linuxPath, Func<string, string?> home)
    {
        var p = linuxPath;
        if (p == "~" || p.StartsWith("~/", StringComparison.Ordinal))
        {
            var h = home(distro);
            if (string.IsNullOrEmpty(h)) return null;
            p = h.TrimEnd('/') + p[1..];
        }
        if (!p.StartsWith('/')) return null;
        return @"\\wsl.localhost\" + distro + p.TrimEnd('/').Replace('/', '\\');
    }

    /// <summary>ディストリ既定ユーザーの $HOME を wsl.exe で取得する (VS Code の WSL 接続も既定ユーザー)。失敗時 null (キャッシュしない)。</summary>
    public static string? Home(string distro, ILogger? logger)
    {
        if (HomeCache.TryGetValue(distro, out var cached)) return cached;
        var home = QueryHome(distro, logger);
        if (home is not null) HomeCache[distro] = home;
        return home;
    }

    private static string? QueryHome(string d, ILogger? logger)
    {
        try
        {
            var psi = new ProcessStartInfo("wsl.exe") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-d", d, "-e", "sh", "-c", "printf %s \"$HOME\"" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(HomeTimeoutMs) || !output.Wait(HomeTimeoutMs))
            {
                try { p.Kill(); } catch (InvalidOperationException) { }
                logger?.Warn($"wsl home lookup timed out ({d})");
                return null;
            }
            var home = output.Result.Trim();
            return home.StartsWith('/') ? home : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger?.Warn($"wsl home lookup failed ({d}): {ex.Message}");
            return null;
        }
    }
}
