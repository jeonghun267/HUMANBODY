using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 음식 씹기와 여섯 침샘의 분비를 함께 시작하고 완료·중단 시 모두 정리한다.
/// 혀가 음식을 어금니 위로 옮기고, 아래턱이 턱관절을 축으로 다물며 씹는 쪽으로 살짝 비켜 갈아 준 뒤, 다 씹은 음식을 혀 위로 다시 모은다.
/// </summary>
[AddComponentMenu("인체탐험/씹기 연출")]
public class ChewingSequence : MonoBehaviour
{
    [Header("턱")]
    [Tooltip("턱관절 위치에 있는 아래턱 회전축 (아랫니가 자식)")]
    public Transform upperJaw;
    [Tooltip("벌린 자세에서 음식을 무는 자세까지 회전 각도 (음수면 앞쪽이 올라가며 다묾)")]
    public float jawOpenAngle = 18f;
    [Tooltip("다물 때 씹는 쪽으로 비켜 가는 각도 (어금니가 맷돌처럼 가는 옆 움직임)")]
    public float grindAngle = 0f;
    public float chewsPerSecond = 1.2f;
    [Header("음식")]
    public Transform foodBolus;
    [Tooltip("씹기 후 최종 크기 비율")]
    public float finalScale = .55f;
    public int chewCount = 8;
    [Tooltip("음식을 올려 씹을 아랫어금니 씹는 면 (아래턱과 함께 움직이는 자식)")]
    public Transform chewPoint;
    [Tooltip("다 씹은 음식을 모으는 혀 위 자리")]
    public Transform gatherPoint;
    [Tooltip("혀가 음식을 어금니로 옮기거나 다시 모으는 시간(초)")]
    public float moveSeconds = .9f;
    [Header("침")]
    public ParticleSystem salivaVfx;
    [Tooltip("함께 시작하고 멈출 모든 침샘 방출기")]
    public ParticleSystem[] salivaEmitters;
    public AudioSource chewAudio;
    public AudioClip chewClip;
    [Header("완료")]
    public UnityEvent onChewingFinished = new UnityEvent();
    [Header("직접 씹기")]
    [Tooltip("켜면 방아쇠나 그립을 누를 때마다 한 번씩 씹는다 (가만히 기다리지 않고 직접)")]
    public bool manualChew;
    [Tooltip("직접 씹기에서 이 시간 안에 안 누르면 저절로 씹는다 (막히지 않게)")]
    public float manualTimeout = 4f;
    /// <summary>직접 씹기에서 지금까지 씹은 횟수 / 전체.</summary>
    public int ChewsDone { get; private set; }
    bool running;
    Coroutine routine;
    Quaternion jawClosed;
    Vector3 bolusStart, bolusStartPosition, bolusRootOffset;
    readonly HashSet<ParticleSystem> systems = new HashSet<ParticleSystem>();

    void Awake()
    {
        if (chewAudio == null) chewAudio = GetComponent<AudioSource>();
        if (chewAudio == null) chewAudio = gameObject.AddComponent<AudioSource>();
        chewAudio.playOnAwake = false; chewAudio.spatialBlend = 1; chewAudio.volume = .8f;
        if (chewClip == null) chewClip = Sfx.Get("crunch");
        Collect(); StopSaliva(true);
    }
    void Collect()
    {
        systems.Clear();
        if (salivaVfx != null) systems.Add(salivaVfx);
        if (salivaEmitters != null) foreach (var p in salivaEmitters) if (p != null) systems.Add(p);
    }
    public void Begin()
    {
        if (running || !isActiveAndEnabled) return;
        Collect();
        if (upperJaw != null) jawClosed = upperJaw.localRotation;
        if (foodBolus != null)
        {
            bolusStart = foodBolus.localScale;
            bolusStartPosition = foodBolus.position;
            // 음식 밑면 가운데에서 모형 원점까지의 거리. 크기가 줄어도 밑면이 이·혀 표면에 닿게 한다.
            var rs = foodBolus.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                bolusRootOffset = foodBolus.position - new Vector3(b.center.x, b.min.y, b.center.z);
            }
            else bolusRootOffset = Vector3.zero;
        }
        running = true;
        routine = StartCoroutine(Run());
    }
    Vector3 RootOn(Vector3 surface)
    {
        var ratio = new Vector3(foodBolus.localScale.x / Mathf.Max(1e-4f, bolusStart.x), foodBolus.localScale.y / Mathf.Max(1e-4f, bolusStart.y), foodBolus.localScale.z / Mathf.Max(1e-4f, bolusStart.z));
        return surface + Vector3.Scale(bolusRootOffset, ratio);
    }
    IEnumerator Move(Transform target, float lift)
    {
        if (foodBolus == null || target == null) yield break;
        Vector3 from = foodBolus.position;
        float t = 0, seconds = Mathf.Max(.05f, moveSeconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds));
            foodBolus.position = Vector3.Lerp(from, RootOn(target.position), k) + Vector3.up * lift * Mathf.Sin(k * Mathf.PI);
            yield return null;
        }
        foodBolus.position = RootOn(target.position);
    }
    IEnumerator Run()
    {
        foreach (var p in systems) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); p.Play(true); }
        // 혀가 음식을 씹는 쪽 어금니 위로 밀어 올린다.
        if (chewPoint != null) yield return Move(chewPoint, .6f);
        float side = chewPoint != null && upperJaw != null ? Mathf.Sign(chewPoint.position.x - upperJaw.position.x) : 0f;
        float duration = 1f / Mathf.Max(.1f, chewsPerSecond);
        int count = Mathf.Max(1, chewCount);
        for (int i = 0; i < count; i++)
        {
            ChewsDone = i;
            if (manualChew)
            {
                if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus($"꼭꼭 씹기 {i} / {count}");
                float waitUntil = Time.time + manualTimeout;
                yield return null;
                while (Time.time < waitUntil && !ChewPressed()) yield return null;
                Haptics.Pulse(.5f, .08f);
            }
            if (chewAudio != null && chewClip != null) chewAudio.PlayOneShot(chewClip);
            else if (foodBolus != null) Sfx.Play("crunch", foodBolus.position, .8f, Random.Range(.9f, 1.1f));
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float phase = Mathf.Clamp01(t / duration);
                // k: 0 = 벌린 자세, 1 = 음식을 문 자세 (주기 가운데)
                float k = Mathf.Sin(phase * Mathf.PI);
                // 다무는 동안 씹는 쪽으로 비켜 있다가 무는 순간 가운데로 돌아오며 갈아 준다.
                float lateral = phase < .5f ? Mathf.Sin(phase * 2f * Mathf.PI) : 0f;
                if (upperJaw != null)
                    upperJaw.localRotation = jawClosed * Quaternion.AngleAxis(-side * grindAngle * lateral, Vector3.up) * Quaternion.AngleAxis(-jawOpenAngle * k, Vector3.right);
                if (foodBolus != null)
                {
                    float s = Mathf.Lerp(1, finalScale, (i + phase) / count);
                    foodBolus.localScale = Vector3.Scale(bolusStart, new Vector3(s*(1+.15f*k), s*(1-.2f*k), s));
                    if (chewPoint != null) foodBolus.position = RootOn(chewPoint.position);
                }
                yield return null;
            }
        }
        if (upperJaw != null) upperJaw.localRotation = jawClosed;
        if (foodBolus != null) foodBolus.localScale = bolusStart * finalScale;
        ChewsDone = count;
        if (manualChew && JourneyHud.Instance != null) JourneyHud.Instance.SetStatus($"꼭꼭 씹기 {count} / {count}");
        // 다 씹은 음식을 혀 가운데로 모아 삼킬 준비를 한다.
        if (gatherPoint != null) yield return Move(gatherPoint, .4f);
        StopSaliva(false);
        running = false; routine = null;
        onChewingFinished?.Invoke();
    }
    static bool ChewPressed() =>
        JourneyAim.TriggerDown() ||
        OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.All) ||
        OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger, OVRInput.Controller.All);

    void StopSaliva(bool clear)
    {
        foreach (var p in systems) if (p != null)
        {
            p.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            var contact = p.GetComponent<LiquidContactSplash>();
            if (clear && contact != null && contact.contactSplash != null) contact.contactSplash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (chewAudio != null) chewAudio.Stop();
    }
    void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        StopSaliva(true);
        if (running)
        {
            if (upperJaw != null) upperJaw.localRotation = jawClosed;
            if (foodBolus != null) { foodBolus.localScale = bolusStart; foodBolus.position = bolusStartPosition; }
        }
        running = false; routine = null;
    }
}
