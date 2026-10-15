using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VscodeSelector;

/// <summary>
/// エディタの settings.json の window.title にパス表示 ⟨${rootPath}⟩ を差し込む。
/// settings.json は JSONC (コメント・末尾カンマ可) のため、再シリアライズせず該当箇所だけ文字列で書き換える。
/// </summary>
public static class TitlePathSettings
{
    // VS Code 既定の window.title にパス表示を加えたもの。
    internal const string DefaultTitle =
        "${dirty}${activeEditorShort}${separator}${rootName} " + Constants.PathMarker + "${separator}${profileName}${separator}${appName}";

    /// <summary>%APPDATA% 配下のユーザー設定フォルダ名。</summary>
    public static string UserDirName(VsCodeEdition edition) => edition switch
    {
        VsCodeEdition.Insiders => "Code - Insiders",
        VsCodeEdition.VSCodium => "VSCodium",
        VsCodeEdition.Cursor => "Cursor",
        VsCodeEdition.Antigravity => "Antigravity IDE",
        _ => "Code",
    };

    /// <summary>既定プロファイルと各プロファイルの settings.json (既定のものは未作成でも含める)。</summary>
    public static IReadOnlyList<string> SettingsFiles(VsCodeEdition edition)
    {
        var user = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), UserDirName(edition), "User");
        var files = new List<string> { Path.Combine(user, "settings.json") };
        var profiles = Path.Combine(user, "profiles");
        if (Directory.Exists(profiles))
        {
            foreach (var dir in Directory.GetDirectories(profiles))
            {
                var f = Path.Combine(dir, "settings.json");
                if (File.Exists(f)) files.Add(f);
            }
        }
        return files;
    }

    /// <summary>各ファイルへパス表示を追加する。変更したファイルは .bak を残す。失敗したファイルのメッセージを返す。</summary>
    public static IReadOnlyList<string> Install(IEnumerable<string> files, ILogger? logger)
    {
        var errors = new List<string>();
        foreach (var f in files)
        {
            try
            {
                var text = File.Exists(f) ? File.ReadAllText(f) : "";
                var updated = AddMarker(text);
                if (updated is null)
                {
                    errors.Add($"{f}: 解析できませんでした");
                    continue;
                }
                if (updated == text) continue;
                if (File.Exists(f)) File.Copy(f, f + ".bak", overwrite: true);
                else Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                File.WriteAllText(f, updated, new UTF8Encoding(false));
                logger?.Info($"window.title path marker added: {f}");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger?.Warn($"window.title update failed ({f}): {ex.Message}");
                errors.Add($"{f}: {ex.Message}");
            }
        }
        return errors;
    }

    /// <summary>
    /// settings.json の本文にパス表示を加えた本文を返す。既に含まれていれば同じ文字列、解析できなければ null。
    /// </summary>
    public static string? AddMarker(string text)
    {
        if (text.Trim().Length == 0)
            return "{\n\t\"window.title\": \"" + DefaultTitle + "\"\n}\n";

        int open = -1, depth = 0, firstKey = -1;
        string? lastKey = null;
        bool expectValue = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = text.IndexOf('\n', i);
                if (i < 0) break;
                continue;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (i < 0) return null;
                i++;
                continue;
            }
            if (c == '"')
            {
                int end = StringEnd(text, i);
                if (end < 0) return null;
                if (depth == 1)
                {
                    if (expectValue)
                    {
                        if (lastKey == "window.title") return InsertIntoValue(text, i + 1, end);
                        expectValue = false;
                    }
                    else
                    {
                        lastKey = text[(i + 1)..end];
                        if (firstKey < 0) firstKey = i;
                    }
                }
                i = end;
                continue;
            }
            switch (c)
            {
                case '{' or '[':
                    if (depth == 0 && c == '{' && open < 0) open = i;
                    depth++;
                    expectValue = false;
                    break;
                case '}' or ']':
                    depth--;
                    if (depth == 0) return InsertKey(text, open, firstKey);
                    break;
                case ':':
                    if (depth == 1) expectValue = true;
                    break;
                default:
                    if (depth == 1) expectValue = false;
                    break;
            }
            if (open < 0) return null;
        }
        return null;
    }

    /// <summary>開き引用符 start の文字列の閉じ引用符位置。</summary>
    private static int StringEnd(string text, int start)
    {
        for (int i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == '"') return i;
            else if (text[i] == '\n') return -1;
        }
        return -1;
    }

    /// <summary>既存の window.title 値 (text[from..to]) へパス表示を差し込む。</summary>
    private static string InsertIntoValue(string text, int from, int to)
    {
        var value = text[from..to];
        if (value.Contains(Constants.PathMarker, StringComparison.Ordinal)) return text;
        const string rootName = "${rootName}";
        const string tail = "${separator}${appName}";
        int at = value.IndexOf(rootName, StringComparison.Ordinal);
        if (at >= 0) at += rootName.Length;
        else
        {
            at = value.LastIndexOf(tail, StringComparison.Ordinal);
            if (at < 0) at = value.Length;
        }
        return text[..(from + at)] + " " + Constants.PathMarker + text[(from + at)..];
    }

    /// <summary>window.title が無い場合、最初のキーの前 (空オブジェクトなら { の直後) に追加する。</summary>
    private static string InsertKey(string text, int open, int firstKey)
    {
        var entry = "\"window.title\": \"" + DefaultTitle + "\"";
        if (firstKey < 0)
            return text[..(open + 1)] + "\n\t" + entry + text[(open + 1)..];
        int lineStart = text.LastIndexOf('\n', firstKey) + 1;
        var indent = text[lineStart..firstKey];
        if (indent.Trim().Length != 0) indent = "\t";
        return text[..firstKey] + entry + ",\n" + (lineStart > open ? indent : "") + text[firstKey..];
    }
}
