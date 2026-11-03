using System.Collections.Generic;

/// <summary>
/// 치료 임무 기록. 장면이 바뀌어도 남아 있어 마지막 진료실(치료 보고서)에서 보여 준다.
/// 병원에서 새로 시작하면 지운다.
/// </summary>
public static class JourneyScore
{
    public class Entry { public string key, label; public int done, total; public bool Complete => total > 0 && done >= total; }

    static readonly List<Entry> entries = new List<Entry>();
    public static IReadOnlyList<Entry> Entries => entries;

    public static void Record(string key, string label, int done, int total)
    {
        if (string.IsNullOrEmpty(key)) return;
        var e = entries.Find(x => x.key == key);
        if (e == null) { e = new Entry { key = key }; entries.Add(e); }
        e.label = label; e.done = done; e.total = total;
    }

    public static void Reset() => entries.Clear();
}
