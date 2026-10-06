using System.Collections.ObjectModel;

namespace VscodeSelector;

/// <summary>Hwnd をキーとした差分適用 (設計 §6)。変化の無い Hwnd の ProjectEntry 参照は保持する。</summary>
public static class ProjectEntryDiff
{
    public static void Apply(ObservableCollection<ProjectEntry> target, IReadOnlyList<ProjectEntry> desired)
    {
        var desiredByHwnd = new Dictionary<nint, ProjectEntry>();
        foreach (var d in desired) desiredByHwnd[d.Hwnd] = d;

        // Remove
        for (int i = target.Count - 1; i >= 0; i--)
            if (!desiredByHwnd.ContainsKey(target[i].Hwnd)) target.RemoveAt(i);

        // Add / Move / Update (目標の並びに合わせる)
        for (int i = 0; i < desired.Count; i++)
        {
            var want = desired[i];
            if (i < target.Count && target[i].Hwnd == want.Hwnd)
            {
                if (!target[i].Equals(want)) target[i] = want;
                continue;
            }
            int found = -1;
            for (int j = i + 1; j < target.Count; j++)
                if (target[j].Hwnd == want.Hwnd) { found = j; break; }
            if (found >= 0)
            {
                target.Move(found, i);
                if (!target[i].Equals(want)) target[i] = want;
            }
            else
            {
                target.Insert(i, want);
            }
        }
    }
}
