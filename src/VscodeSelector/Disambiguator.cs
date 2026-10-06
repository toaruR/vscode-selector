namespace VscodeSelector;

/// <summary>同名プロジェクトの区別 (設計 5.3)。純関数 (sequenceMap のみ更新)。</summary>
public static class Disambiguator
{
    public static IReadOnlyList<ProjectEntry> Apply(IReadOnlyList<ProjectEntry> entries, IDictionary<nint, int> sequenceMap)
    {
        if (entries is null || entries.Count == 0) return entries ?? [];
        var names = entries.Select(e => e.Parsed.ProjectName).ToArray();

        // 同名グループ (ProjectName 単位)
        var dupCount = names.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());

        // Step 1,2
        var labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var s = names[i];
            if (dupCount[s] > 1)
            {
                if (!string.IsNullOrEmpty(e.Parsed.RemoteLabel))
                    s += $" [{e.Parsed.RemoteLabel}]";
            }
            labels[i] = s;
        }
        var cnt2 = labels.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        for (int i = 0; i < entries.Count; i++)
        {
            if (cnt2[labels[i]] > 1 && dupCount[names[i]] > 1)
            {
                var suffix = entries[i].Edition switch
                {
                    VsCodeEdition.Insiders => " (Insiders)",
                    VsCodeEdition.VSCodium => " (VSCodium)",
                    _ => "",
                };
                labels[i] += suffix;
            }
        }

        // Step 3: Hwnd 昇順、#N は sequenceMap で保持
        var cnt3 = labels.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        foreach (var grp in Enumerable.Range(0, entries.Count)
                     .Where(i => cnt3[labels[i]] > 1)
                     .GroupBy(i => labels[i]))
        {
            var ordered = grp.OrderBy(i => entries[i].Hwnd).ToList();
            var used = new HashSet<int>();
            var unassigned = new List<int>();
            foreach (var i in ordered)
            {
                if (sequenceMap.TryGetValue(entries[i].Hwnd, out var n) && n >= 1 && used.Add(n)) continue;
                unassigned.Add(i);
            }
            int next = 1;
            foreach (var i in unassigned)
            {
                while (used.Contains(next)) next++;
                sequenceMap[entries[i].Hwnd] = next;
                used.Add(next);
            }
        }

        var result = new ProjectEntry[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var label = labels[i];
            if (cnt3[label] > 1 && sequenceMap.TryGetValue(entries[i].Hwnd, out var n) && n >= 2)
                label += $" #{n}";
            result[i] = entries[i] with { DisplayName = label };
        }
        return result;
    }
}
