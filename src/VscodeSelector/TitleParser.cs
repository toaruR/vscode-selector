using System.Text.RegularExpressions;

namespace VscodeSelector;

/// <summary>§5.2 タイトル解析 (P0〜P5)。例外を投げない。</summary>
public sealed class TitleParser : ITitleParser
{
    private const string NoFolderName = "(No Folder)";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex AppRe = new(Constants.AppRe, RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex RemoteRe = new(@"\s\[([^\]]+)\]$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex WorkspaceRe = new(@"\s\(Workspace\)$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex NoFolderRe = new(@"^(Welcome|Get Started|Settings|Extension: .*)$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex PathMarkerRe = new(@"\s?" + Constants.PathMarkerOpen + "([^" + Constants.PathMarkerOpen + Constants.PathMarkerClose + "]*)" + Constants.PathMarkerClose, RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex AppNameOnlyRe = new(@"^(Visual Studio Code(?: - Insiders)?|VSCodium|Code - OSS)$", RegexOptions.CultureInvariant, RegexTimeout);

    public ParsedTitle Parse(string title, Options options)
    {
        title ??= "";
        string? path = null;
        try
        {
            // パス表示 ⟨${rootPath}⟩ は区切り文字を含みうるため、解析前に取り除く。
            var pm = PathMarkerRe.Match(title);
            if (pm.Success)
            {
                path = pm.Groups[1].Value.Trim();
                title = title.Remove(pm.Index, pm.Length);
            }
            return (ParseCore(title, options) ?? Fallback(title)) with { FolderPath = path };
        }
        catch (Exception)
        {
            return Fallback(title) with { FolderPath = path };
        }
    }

    private static ParsedTitle? ParseCore(string title, Options? options)
    {
        // P0: titlePattern
        var pattern = CompilePattern(options?.TitlePattern);
        if (pattern is not null)
        {
            var m = pattern.Match(title);
            if (m.Success)
            {
                var g = m.Groups["project"];
                if (g.Success && g.Value.Length > 0)
                    return new ParsedTitle(Truncate(g.Value), RemoteKind.None, null, false, false);
            }
        }

        // P1: APP_RE
        string rest;
        var app = AppRe.Match(title);
        if (app.Success)
            rest = title[..app.Index];
        else if (AppNameOnlyRe.IsMatch(title))
            return NoFolder();
        else
            return null; // P5

        // P2: prefixes
        const string admin = "[Administrator] ";
        if (rest.StartsWith(admin, StringComparison.Ordinal)) rest = rest[admin.Length..];
        if (rest.StartsWith("● ", StringComparison.Ordinal) || rest.StartsWith("* ", StringComparison.Ordinal))
            rest = rest[2..];

        // P3: separators
        var root = rest;
        var multi = false;
        foreach (var sep in options?.TitleSeparators ?? [" - "])
        {
            if (string.IsNullOrEmpty(sep)) continue;
            var parts = rest.Split(sep);
            if (parts.Length > 1)
            {
                root = parts[^1];
                multi = true;
                break;
            }
        }

        // P4: remote / workspace / no folder
        var remote = RemoteKind.None;
        string? label = null;
        var rm = RemoteRe.Match(root);
        if (rm.Success)
        {
            label = rm.Groups[1].Value;
            remote = ClassifyRemote(label);
            root = root[..rm.Index];
        }
        var isWorkspace = false;
        var wm = WorkspaceRe.Match(root);
        if (wm.Success)
        {
            isWorkspace = true;
            root = root[..wm.Index];
        }
        if (!multi && !isWorkspace && remote == RemoteKind.None && NoFolderRe.IsMatch(root))
            return NoFolder();

        if (root.Trim().Length == 0) return null;
        return new ParsedTitle(Truncate(root), remote, label, false, isWorkspace);
    }

    private static Regex? CompilePattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return null;
        try
        {
            var re = new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout);
            return re.GetGroupNames().Contains("project") ? re : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static RemoteKind ClassifyRemote(string label)
    {
        if (label.StartsWith("WSL", StringComparison.Ordinal)) return RemoteKind.Wsl;
        if (label.StartsWith("SSH", StringComparison.Ordinal)) return RemoteKind.Ssh;
        if (label.StartsWith("Dev Container", StringComparison.Ordinal)) return RemoteKind.DevContainer;
        if (label.StartsWith("Codespaces", StringComparison.Ordinal)) return RemoteKind.Codespaces;
        return RemoteKind.Other;
    }

    private static ParsedTitle NoFolder() => new(NoFolderName, RemoteKind.None, null, true, false);

    private static ParsedTitle Fallback(string title) =>
        new(Truncate(title), RemoteKind.None, null, false, false);

    internal static string Truncate(string s) =>
        s.Length > Constants.MaxLabelChars ? s[..(Constants.MaxLabelChars - 1)] + "…" : s;
}
