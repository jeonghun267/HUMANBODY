using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진료실 큰 화면의 진찰 결과 모니터 (홀로그램을 묻히지 않게 어두운 바탕, 가운데는 비워 둔다).
/// 왼쪽: 간호사에게 말한 증상과 심전도. 오른쪽: 몸 스캔으로 찾은 곳(이·위·대장)이 하나씩 채워진다.
/// 아래: 진찰이 끝나면 오늘의 치료 경로(입 → 식도 → 위 → 소장 → 대장). 마지막 진료실에서는 모두 치료 완료로 보인다.
/// </summary>
[AddComponentMenu("인체탐험/진찰 결과 모니터")]
public class ClinicMonitor : MonoBehaviour
{
    [Serializable]
    public class Finding
    {
        public Image dot;
        public TextMeshProUGUI result;
        [TextArea] public string foundText;
    }

    public TextMeshProUGUI title, symptom, status;
    public Finding[] findings = new Finding[0];
    public CanvasGroup plan;
    public Image[] planSteps = new Image[0];
    [Tooltip("마지막 진료실: 처음부터 모두 치료 완료로 보인다")] public bool completeOnStart;
    public Color waitColor = new Color(.32f, .42f, .48f, 1f), foundColor = new Color(1f, .45f, .4f, 1f), doneColor = new Color(.5f, .9f, 1f, 1f);
    [Tooltip("결과 칸이 채워질 때 '삐빅', 치료 계획이 뜰 때 '삐삐삐' (화면에서 난다)")] public string foundSound = "monitor_found", planSound = "monitor_plan";
    [Tooltip("치료 계획 소리를 몸 스캔 완료음 뒤로 미루는 시간 (초)")] public float planSoundDelay = .6f;

    /// <summary>치료 경로 칸: 할 일은 파랑, 끝낸 곳은 초록빛 청록 (흰 글자가 읽히는 깊이).</summary>
    public static readonly Color StepTodo = new Color(.16f, .55f, .78f, 1f), StepDone = new Color(.18f, .55f, .42f, 1f);

    bool[] got = new bool[0];
    bool planShown, complete;

    void Start()
    {
        got = new bool[findings.Length];
        if (completeOnStart) { Complete(); return; }
        if (plan != null) plan.alpha = 0f;
        Refresh();
    }

    public void SetSymptom(string text) { if (symptom != null) symptom.text = text; }

    /// <summary>몸 스캔에서 index 번째 자리(0 이, 1 위, 2 대장)를 진찰했다.</summary>
    public void Found(int index)
    {
        if (complete || index < 0 || index >= findings.Length) return;
        if (got.Length != findings.Length) got = new bool[findings.Length];
        if (!got[index] && !string.IsNullOrEmpty(foundSound)) Sfx.Play(foundSound, transform.position, 1f);
        got[index] = true;
        Refresh();
    }

    public void ShowPlan()
    {
        if (!planShown && !string.IsNullOrEmpty(planSound) && isActiveAndEnabled) StartCoroutine(PlanSound());
        planShown = true;
        for (int i = 0; i < got.Length; i++) got[i] = true;
        Refresh();
    }

    IEnumerator PlanSound()
    {
        yield return new WaitForSeconds(planSoundDelay);
        Sfx.Play(planSound, transform.position, 1f);
    }

    public void Complete()
    {
        complete = planShown = true;
        if (got.Length != findings.Length) got = new bool[findings.Length];
        for (int i = 0; i < got.Length; i++) got[i] = true;
        if (title != null) title.text = "치료 결과";
        Refresh();
    }

    void Refresh()
    {
        int count = 0;
        for (int i = 0; i < findings.Length; i++)
        {
            var f = findings[i]; bool g = i < got.Length && got[i]; if (g) count++;
            if (f.dot != null) f.dot.color = complete ? doneColor : g ? foundColor : waitColor;
            if (f.result != null) f.result.text = complete ? "치료 완료" : g ? f.foundText : "?";
        }
        foreach (var step in planSteps) if (step != null) step.color = complete ? StepDone : StepTodo;
        if (status != null)
            status.text = complete ? "모두 건강해졌어요!" : planShown ? "진찰 끝 · 치료를 시작해요" : count == 0 ? "몸 스캔에서 아픈 곳을 찾아요" : $"진찰 중 · {count} / {findings.Length}";
    }

    void Update()
    {
        if (plan != null) plan.alpha = Mathf.MoveTowards(plan.alpha, planShown ? 1f : 0f, Time.deltaTime * 1.5f);
        if (complete) return;
        // 아직 찾지 못한 칸의 점은 천천히 깜박여 '찾는 중' 을 알린다
        float blink = .55f + .45f * Mathf.Sin(Time.time * 3f);
        for (int i = 0; i < findings.Length; i++)
            if (findings[i].dot != null && !(i < got.Length && got[i])) findings[i].dot.color = Color.Lerp(waitColor * .7f, waitColor, blink);
    }
}
