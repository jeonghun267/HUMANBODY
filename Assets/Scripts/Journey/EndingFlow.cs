using System.Collections;
using UnityEngine;

/// <summary>
/// 마지막 진료실: 의사 선생님이 치료 결과를 알려 주고 → OX 퀴즈 → 보고서(별점·배지) → 작별 인사 → 잠시 뒤 처음(병원)으로.
/// </summary>
[AddComponentMenu("인체탐험/마무리 진행")]
public class EndingFlow : MonoBehaviour
{
    public HospitalNpc doctor;
    public OxQuiz quiz;
    public TreatmentReport report;
    [Tooltip("의사 선생님의 작별 인사 대사 번호")] public int goodbyeNode = 1;
    [Tooltip("작별 인사 뒤 처음으로 돌아가기까지(초)")] public float restartAfter = 10f;

    IEnumerator Start()
    {
        while (JourneyManager.Instance != null && (JourneyManager.Instance.IsIntroPending || JourneyManager.Instance.IsTransitioning)) yield return null;
        while (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) yield return null;
        if (report != null) report.Refresh();
        if (doctor != null) { doctor.TalkFrom(0); yield return null; while (doctor.IsTalking) yield return null; }
        if (quiz != null) { quiz.Begin(); while (!quiz.IsDone) yield return null; }
        if (report != null) report.Refresh();
        if (doctor != null) { doctor.TalkFrom(goodbyeNode); yield return null; while (doctor.IsTalking) yield return null; }
        var stage = JourneyManager.Instance != null ? JourneyManager.Instance.CurrentStage : null;
        if (GuideNarrator.Instance != null && stage != null && stage.outroLines != null && stage.outroLines.Length > 0)
            yield return GuideNarrator.Instance.PlayRoutine(stage.outroLines);
        yield return new WaitForSeconds(restartAfter);
        if (JourneyManager.Instance != null && !JourneyManager.Instance.IsTransitioning) JourneyManager.Instance.Restart();
    }
}
