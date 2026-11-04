using System.Collections;
using UnityEngine;

/// <summary>
/// 충치 치료. 어금니 씹는 면의 까만 충치를 파란 치료 광선으로 비추고 방아쇠를 누르고 있으면
/// 썩은 부분이 줄어들어 사라지고 하얀 충전재로 메워진다.
/// hiddenUntilBegin 이면 처음엔 충치가 없고, 충치가 생기는 과정(DecayTimeLapse)이 Reveal 로 키운 뒤 Begin 으로 치료를 시작한다.
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
    [TextArea] public string nextGoalText = "";
    public NarrationLine[] startLines, doneLines;
    [Tooltip("처음엔 충치가 없다가 Reveal 로 생기고, Begin 을 불러야 치료를 시작한다 (충치가 생기는 과정을 먼저 보여 줄 때)")]
    public bool hiddenUntilBegin;
    [Tooltip("치료가 끝나면 (다음 임무 시작 등)")] public UnityEngine.Events.UnityEvent onTreated = new UnityEngine.Events.UnityEvent();

    float progress;
    bool done, begun;
    AudioSource hum;
    Vector3 decayScale;
    public bool IsTreating => begun && !done;
    public bool IsDone => done;

    void Awake()
    {
        if (decay != null) decayScale = decay.localScale;
        if (hiddenUntilBegin) Reveal(0);
    }

    IEnumerator Start()
    {
        if (filling != null) filling.enabled = false;
        JourneyScore.Record("cavity", "충치 치료", 0, 1);
        if (hiddenUntilBegin) yield break;
        // 입장 안내가 끝난 뒤 알려 준다 (안내를 끊지 않게)
        while (JourneyManager.Instance != null && (JourneyManager.Instance.IsIntroPending || JourneyManager.Instance.IsTransitioning)) yield return null;
        while (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) yield return null;
        Begin();
    }

    /// <summary>충치가 생기는 정도 (0 = 없음, 1 = 다 생김).</summary>
    public void Reveal(float amount)
    {
        if (decay == null || done) return;
        decay.gameObject.SetActive(amount > .01f);
        decay.localScale = decayScale * Mathf.Clamp01(amount);
    }

    /// <summary>치료를 시작한다: 충치를 다 보이게 하고 목표·안내를 띄운다.</summary>
    public void Begin()
    {
        if (done || begun) return;
        begun = true;
        Reveal(1);
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetGoal(goalText);
        if (GuideNarrator.Instance != null && startLines != null && startLines.Length > 0) GuideNarrator.Instance.Play(startLines);
    }

    /// <summary>바로 치료를 마친다 (건너뛰기·자동 검사용).</summary>
    public void TreatNow() { if (!done) { begun = true; Finish(); } }

    void Update()
    {
        if (done || !begun || decay == null) return;
        var ray = JourneyAim.Ray();
        bool aimed = JourneyAim.Near(ray, decay.position, aimRadius, range, out _);
        bool working = aimed && JourneyAim.TriggerHeld();
        if (beam != null)
        {
            beam.enabled = JourneyAim.TriggerHeld();
            if (beam.enabled) JourneyAim.Beam(beam, ray.origin + ray.direction * .1f, aimed ? decay.position : ray.origin + ray.direction * range);
        }
        if (hum == null) { hum = Sfx.Loop("cure", decay, 0f, true); }
        hum.volume = Mathf.MoveTowards(hum.volume, working ? .45f : 0f, Time.deltaTime * 3f);
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
        if (hum != null) hum.volume = 0;
        if (sparkle != null) sparkle.Emit(30);
        Sfx.Play("success", transform.position, .8f, 1f);
        Haptics.Pulse(.6f, .2f);
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetStatus("충치 치료 완료!"); JourneyHud.Instance.SetGoal(nextGoalText); }
        JourneyScore.Record("cavity", "충치 치료", 1, 1);
        StartCoroutine(AfterTreated());
    }

    IEnumerator AfterTreated()
    {
        // 완료 대사를 끝까지 들려준 뒤 다음 임무를 시작한다 (다음 임무 안내가 이 대사를 끊지 않게).
        if (GuideNarrator.Instance != null && doneLines != null && doneLines.Length > 0) yield return GuideNarrator.Instance.PlayRoutine(doneLines);
        onTreated?.Invoke();
    }
}
