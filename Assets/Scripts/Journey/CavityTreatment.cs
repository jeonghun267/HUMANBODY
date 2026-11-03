using System.Collections;
using UnityEngine;

/// <summary>
/// 충치 치료. 어금니 씹는 면의 까만 충치를 파란 치료 광선으로 비추고 방아쇠를 누르고 있으면
/// 썩은 부분이 줄어들어 사라지고 하얀 충전재로 메워진다. 입 장면 진행(씹기·삼키기)과는 따로 언제든 할 수 있다.
/// </summary>
[AddComponentMenu("인체탐험/충치 치료")]
public class CavityTreatment : MonoBehaviour
{
    public Transform decay;      // 충치(검은 얼룩과 갈색 자국)
    public Renderer filling;     // 메운 자리 — 치료가 끝나면 켜진다
    public ParticleSystem sparkle;
    public LineRenderer beam;
    public float holdSeconds = 3f, range = 7f, aimRadius = .5f;
    [TextArea] public string goalText = "어금니의 까만 충치를 치료 광선으로 비추고 방아쇠를 누르고 있으세요";
    public NarrationLine[] startLines, doneLines;

    float progress;
    bool done, announced;
    Vector3 decayScale;

    IEnumerator Start()
    {
        if (decay != null) decayScale = decay.localScale;
        if (filling != null) filling.enabled = false;
        // 입장 안내가 끝난 뒤 알려 준다 (안내를 끊지 않게)
        while (JourneyManager.Instance != null && (JourneyManager.Instance.IsIntroPending || JourneyManager.Instance.IsTransitioning)) yield return null;
        while (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) yield return null;
        if (done) yield break;
        announced = true;
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetGoal(goalText);
        if (GuideNarrator.Instance != null && startLines != null && startLines.Length > 0) GuideNarrator.Instance.Play(startLines);
    }

    void Update()
    {
        if (done || decay == null) return;
        var ray = JourneyAim.Ray();
        bool aimed = JourneyAim.Near(ray, decay.position, aimRadius, range, out _);
        bool working = aimed && JourneyAim.TriggerHeld();
        if (beam != null)
        {
            beam.enabled = JourneyAim.TriggerHeld();
            if (beam.enabled) JourneyAim.Beam(beam, ray.origin + ray.direction * .1f, aimed ? decay.position : ray.origin + ray.direction * range);
        }
        if (!working) return;
        progress += Time.deltaTime;
        float t = Mathf.Clamp01(progress / holdSeconds);
        decay.localScale = decayScale * Mathf.Lerp(1f, .15f, t);
        if (sparkle != null) { sparkle.transform.position = decay.position; if (Random.value < .5f) sparkle.Emit(2); }
        if (Random.value < .1f) Haptics.Pulse(.25f, .05f);
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus($"충치 치료 {Mathf.RoundToInt(t * 100)}%");
        if (t >= 1f) Finish();
    }

    void Finish()
    {
        done = true;
        if (decay != null) decay.gameObject.SetActive(false);
        if (filling != null) filling.enabled = true;
        if (beam != null) beam.enabled = false;
        if (sparkle != null) sparkle.Emit(30);
        Sfx.Play("ting", transform.position, .8f, 1.1f);
        Haptics.Pulse(.6f, .2f);
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetStatus("충치 치료 완료!"); JourneyHud.Instance.SetGoal(announced ? "혀 위의 음식 쪽으로 걸어가 씹어 보세요" : ""); }
        if (GuideNarrator.Instance != null && doneLines != null && doneLines.Length > 0) GuideNarrator.Instance.Play(doneLines);
    }
}
