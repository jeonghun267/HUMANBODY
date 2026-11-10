using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>마지막 진료실의 치료 보고서: 임무마다 해낸 수와 별점(★), 받은 배지를 보여 준다.</summary>
[AddComponentMenu("인체탐험/치료 보고서")]
public class TreatmentReport : MonoBehaviour
{
    public TextMeshProUGUI body;
    public TextMeshProUGUI summary;
    public TextMeshProUGUI badges;

    void Start() => Refresh();

    static int Stars(JourneyScore.Entry e)
    {
        if (e.total <= 0) return 0;
        float r = (float)e.done / e.total;
        return r >= 1f ? 3 : r >= .6f ? 2 : r > 0 ? 1 : 0;
    }

    public void Refresh()
    {
        var text = new StringBuilder();
        int complete = 0, stars = 0;
        foreach (var e in JourneyScore.Entries)
        {
            int s = Stars(e); stars += s;
            if (e.Complete) complete++;
            string count = e.total > 1 ? $" {e.done}/{e.total}" : "";
            text.AppendLine($"{e.label}{count}  <color=#FFD66B>{new string('★', s)}</color><color=#5A6B73>{new string('★', 3 - s)}</color>");
        }
        if (JourneyScore.Entries.Count == 0) text.AppendLine("기록된 치료가 없어요.");
        if (body != null) body.text = text.ToString();
        int total = JourneyScore.Entries.Count;
        if (summary != null) summary.text = total > 0 && complete == total ? $"모든 치료 완료! 별 {stars}개" : $"치료 {complete} / {total} 완료 · 별 {stars}개";
        if (badges != null) badges.text = string.Join("   ", Badges());
    }

    /// <summary>받은 배지 이름들.</summary>
    public static List<string> Badges()
    {
        var map = JourneyScore.Entries.ToDictionary(e => e.key, e => e);
        bool Done(string k) => map.TryGetValue(k, out var e) && e.Complete;
        float Ratio(string k) => map.TryGetValue(k, out var e) && e.total > 0 ? (float)e.done / e.total : 0;
        var list = new List<string>();
        if (Done("cavity") && Done("plaque")) list.Add("충치 박사");
        if (Done("acid")) list.Add("위산 해결사");
        if (Done("hpylori") && Done("germs")) list.Add("세균 사냥꾼");
        if (Ratio("nutrients") >= .8f) list.Add("영양 만점");
        if (Done("enzymes")) list.Add("효소 박사");
        if (Done("fiber")) list.Add("유익균 친구");
        if (Ratio("quiz") >= .8f) list.Add("퀴즈 왕");
        if (list.Count == 0) list.Add("다음엔 배지를 모아 보자!");
        return list.Select(b => "<color=#7FE3FF>[" + b + "]</color>").ToList();
    }
}
