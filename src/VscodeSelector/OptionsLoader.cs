using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VscodeSelector;

/// <summary>settings.json を読み込み検証する。例外は投げず、ファイルは書き換えない。</summary>
public static class OptionsLoader
{
    public static Options Defaults() =>
        new([" - "], null, [], SortOrder.Name, false, false);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VsCodeSelector", "settings.json");

    public static Options Load(string path, ILogger logger)
    {
        var d = Defaults();
        string text;
        try
        {
            if (!File.Exists(path)) return d;
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            logger.Warn($"settings read failed ({path}): {ex.Message}; using defaults");
            return d;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            logger.Warn($"settings.json is not valid JSON ({path}): {ex.Message}; using defaults");
            return d;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warn("settings.json root is not an object; using defaults");
                return d;
            }

            var separators = d.TitleSeparators;
            var pattern = d.TitlePattern;
            var extra = d.ExtraProcessNames;
            var sort = d.SortOrder;
            var topmost = d.Topmost;
            var hide = d.HideOnActivate;

            foreach (var p in root.EnumerateObject())
            {
                switch (p.Name)
                {
                    case "titleSeparators":
                        var s = ReadStrings(p.Value, nonEmptyItems: true);
                        if (s is { Length: > 0 }) separators = s;
                        else Bad(logger, p.Name, "non-empty array of non-empty strings");
                        break;
                    case "titlePattern":
                        if (p.Value.ValueKind == JsonValueKind.Null) pattern = null;
                        else if (p.Value.ValueKind == JsonValueKind.String) pattern = ValidatePattern(p.Value.GetString()!, logger);
                        else Bad(logger, p.Name, "string or null");
                        break;
                    case "extraProcessNames":
                        var e = ReadStrings(p.Value, nonEmptyItems: false);
                        if (e != null) extra = e;
                        else Bad(logger, p.Name, "array of strings");
                        break;
                    case "sortOrder":
                        var so = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                        if (so == "name") sort = SortOrder.Name;
                        else if (so == "zorder") sort = SortOrder.ZOrder;
                        else Bad(logger, p.Name, "\"name\" or \"zorder\"");
                        break;
                    case "topmost":
                        if (IsBool(p.Value)) topmost = p.Value.GetBoolean();
                        else Bad(logger, p.Name, "boolean");
                        break;
                    case "hideOnActivate":
                        if (IsBool(p.Value)) hide = p.Value.GetBoolean();
                        else Bad(logger, p.Name, "boolean");
                        break;
                }
            }

            return new Options(separators, pattern, extra, sort, topmost, hide);
        }
    }

    private static bool IsBool(JsonElement v) => v.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static void Bad(ILogger logger, string key, string expected) =>
        logger.Warn($"settings key '{key}' has invalid type/value (expected {expected}); using default");

    private static string[]? ReadStrings(JsonElement v, bool nonEmptyItems)
    {
        if (v.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return null;
            var str = item.GetString()!;
            if (nonEmptyItems && str.Length == 0) return null;
            list.Add(str);
        }
        return list.ToArray();
    }

    private static string? ValidatePattern(string pattern, ILogger logger)
    {
        try
        {
            var re = new Regex(pattern);
            if (!re.GetGroupNames().Contains("project"))
            {
                logger.Warn("titlePattern has no named group 'project'; treated as null");
                return null;
            }
            return pattern;
        }
        catch (ArgumentException ex)
        {
            logger.Warn($"titlePattern is not a valid regex ({ex.Message}); treated as null");
            return null;
        }
    }
}
