using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 삼킴 연출: 혀가 뒤로 젖혀지며 식괴를 목구멍으로 밀고, 목젖이 올라가고, 후두덮개가 기관을 덮는다.
/// 음식이 삼켜진 뒤 탐험가에게 다음 경로를 안내하고 화면 전환으로 식도에 이동한다.
/// </summary>
[AddComponentMenu("인체탐험/음식 삼킴 관찰") ]
public class SwallowSequence : MonoBehaviour
{
    [Header("파츠")]
    public Transform tongue;
    public Transform bolus;
    public Transform uvula;
    public Transform epiglottis;
    [Tooltip("식괴가 도달할 목구멍 지점")]
    public Transform throatPoint;

    [Header("타이밍")]
    public float tongueLiftSeconds = 0.8f;
    public float pushSeconds = 1.2f;
    public float playerPullSeconds = 0.9f;
    [Tooltip("혀 뒤쪽이 들리는 각도 (상대 가로 축)")]
    public float tongueLiftAngle = -25f;
    public float uvulaLiftAngle = 40f;
    public float epiglottisCloseAngle = 70f;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip gulpClip;

    [Header("완료")]
    [Tooltip("선택 사항: 음식 삼킴 뒤 탐험가를 목구멍 지점으로 이동. 기본 탐험에서는 끄고 화면 전환으로 다음 장소에 이동합니다.")]
    public bool pullPlayer = false;
    public bool advanceOnFinish = true;
    public UnityEvent onSwallowed = new UnityEvent();
    [Header("삼킴 타이밍")]
    [Tooltip("켜면 후두덮개가 닫히는 순간 방아쇠를 당겨야 삼킨다 (일찍 당기면 사레). 세 번 실패하거나 시간이 지나면 저절로 넘어간다")]
    public bool timingGame;
    public float timingLimit = 15f;

    bool running;

    public void Begin()
    {
        if (!running) StartCoroutine(Run());
    }

    /// <summary>후두덮개가 천천히 닫혔다 열렸다 한다. 닫힌 순간(70% 이상) 방아쇠를 당기면 성공.</summary>
    IEnumerator Timing(Quaternion epiStart)
    {
        int fails = 0; float start = Time.time; bool ok = false;
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetGoal("후두덮개가 기도를 덮는 순간 방아쇠를 당겨 꿀꺽 삼키세요");
        yield return null;
        while (!ok && fails < 3 && Time.time - start < timingLimit)
        {
            float k = .5f + .5f * Mathf.Sin((Time.time - start) * 2.2f - Mathf.PI / 2);
            epiglottis.localRotation = epiStart * Quaternion.Euler(epiglottisCloseAngle * k, 0, 0);
            bool closed = k > .7f;
            if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus(closed ? "<color=#7FE3FF>지금!</color>" : "기다려…");
            if (JourneyAim.TriggerDown())
            {
                if (closed) { ok = true; Sfx.Play("correct", epiglottis.position, .8f); }
                else { fails++; Sfx.Play("incorrect", epiglottis.position, .8f); Haptics.Pulse(.9f, .3f); if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus("콜록! 사레들렸어요. 닫힐 때 눌러요"); yield return new WaitForSeconds(1.2f); }
            }
            yield return null;
        }
        JourneyScore.Record("swallow", "꿀꺽 삼키기", ok ? 1 : 0, 1);
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus(ok ? "꿀꺽 성공!" : "후두덮개가 닫히는 걸 잘 봐 두자!");
        epiglottis.localRotation = epiStart;
    }

    IEnumerator Run()
    {
        running = true;
        // 목젖의 평소 흔들림이 들어 올리는 동작을 덮어쓰지 않게 삼키는 동안 멈춘다.
        var uvulaSway = uvula != null ? uvula.GetComponent<Sway>() : null;
        bool swayWasOn = uvulaSway != null && uvulaSway.enabled;
        if (uvulaSway != null) uvulaSway.enabled = false;
        Quaternion tongueStart = tongue != null ? tongue.localRotation : Quaternion.identity;
        Quaternion uvulaStart = uvula != null ? uvula.localRotation : Quaternion.identity;
        Quaternion epiStart = epiglottis != null ? epiglottis.localRotation : Quaternion.identity;
        if (timingGame && epiglottis != null) yield return Timing(epiStart);

        // 1. 혀 들림 + 목젖 상승 + 후두덮개 닫힘
        float t = 0f;
        while (t < tongueLiftSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / tongueLiftSeconds);
            if (tongue != null) tongue.localRotation = tongueStart * Quaternion.Euler(tongueLiftAngle * k, 0, 0);
            if (uvula != null) uvula.localRotation = uvulaStart * Quaternion.Euler(uvulaLiftAngle * k, 0, 0);
            if (epiglottis != null) epiglottis.localRotation = epiStart * Quaternion.Euler(epiglottisCloseAngle * k, 0, 0);
            yield return null;
        }

        // 2. 식괴가 목구멍으로
        if (audioSource != null && gulpClip != null) audioSource.PlayOneShot(gulpClip);
        else if (throatPoint != null) Sfx.Play("gulp", throatPoint.position, 1f);
        Haptics.Pulse(0.8f, 0.35f);
        if (bolus != null && throatPoint != null)
        {
            Vector3 from = bolus.position; Vector3 scale = bolus.localScale;
            t = 0f;
            while (t < pushSeconds)
            {
                t += Time.deltaTime;
                float k = t / pushSeconds;
                bolus.position = Vector3.Lerp(from, throatPoint.position, k * k);
                bolus.localScale = scale * (1f - 0.6f * k);
                yield return null;
            }
            bolus.gameObject.SetActive(false);
        }

        // 3. 음식 삼킴을 관찰한 뒤, 화면이 밝을 때 탐험 경로 이동 안내를 듣는다
        if (advanceOnFinish && JourneyManager.Instance != null && GuideNarrator.Instance != null)
        {
            var stage = JourneyManager.Instance.CurrentStage;
            if (stage != null && stage.outroLines != null && stage.outroLines.Length > 0)
                yield return GuideNarrator.Instance.PlayRoutine(stage.outroLines);
        }

        // 4. 선택적인 탐험가 경로 이동. 기본 장면에서는 끄고 다음 단계의 화면 전환을 사용한다.
        var player = GameObject.FindWithTag("Player");
        if (pullPlayer && player != null && throatPoint != null)
        {
            // 멀미 완화: 화면을 먼저 어둡게 한 뒤(비네트+페이드) 짧게 이동
            var vignette = player.GetComponentInChildren<ComfortVignette>();
            var fade = FindAnyObjectByType<OVRScreenFade>();
            if (vignette != null) vignette.forced = 1f;
            if (fade != null) { fade.fadeTime = playerPullSeconds * 0.8f; fade.FadeOut(); }
            var cc = player.GetComponent<CharacterController>();
            var ovr = player.GetComponent<OVRPlayerController>();
            if (cc != null) cc.enabled = false;
            if (ovr != null) ovr.enabled = false;
            Vector3 from = player.transform.position;
            t = 0f;
            while (t < playerPullSeconds)
            {
                t += Time.deltaTime;
                float k = t / playerPullSeconds;
                player.transform.position = Vector3.Lerp(from, throatPoint.position, k * k);
                yield return null;
            }
            if (cc != null) cc.enabled = true;
            if (ovr != null) ovr.enabled = true;
            if (vignette != null) vignette.forced = 0f;
        }

        // 5. 원위치 (다음 씬으로 넘어가지 않을 때를 위해)
        if (tongue != null) tongue.localRotation = tongueStart;
        if (uvula != null) uvula.localRotation = uvulaStart;
        if (epiglottis != null) epiglottis.localRotation = epiStart;
        if (uvulaSway != null) uvulaSway.enabled = swayWasOn;

        running = false;
        onSwallowed?.Invoke();
        if (advanceOnFinish && JourneyManager.Instance != null) JourneyManager.Instance.GoToNext(playOutro: false);
    }
}
