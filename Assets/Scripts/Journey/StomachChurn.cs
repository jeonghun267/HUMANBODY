using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>소화하는 동안 위산과 위벽 수축을 재생하고, 완료 또는 중단 시 정리한다.</summary>
[AddComponentMenu("인체탐험/위 소화 연출")]
public class StomachChurn : MonoBehaviour
{
    [Header("음식 → 미즙")]
    public GameObject[] foodBoluses;
    public GameObject[] chymeBlobs;
    [Tooltip("교체에 걸리는 시간(초)")]
    public float digestDuration = 8f;

    [Header("위산 / 벽")]
    public ParticleSystem[] acidVfx;
    public PeristalsisPulse[] walls;
    [Tooltip("소화 중 벽 수축 진폭 배수")]
    public float churnAmplitudeMultiplier = 2.5f;
    public AudioSource churnAudio;

    [Header("완료")]
    public UnityEvent onDigested = new UnityEvent();

    bool running;
    Coroutine routine;
    float[] baseAmplitudes;
    Vector3[] foodScales, chymeScales;
    bool[] foodActive, chymeActive;
    JourneyDissolve[] dissolves;

    void Awake()
    {
        StopEffects(true);
        if (churnAudio != null && churnAudio.clip == null) churnAudio.clip = Sfx.Get("gurgle");
    }

    public void Begin()
    {
        if (running || !isActiveAndEnabled) return;
        running = true;
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        acidVfx ??= new ParticleSystem[0];
        walls ??= new PeristalsisPulse[0];
        foodBoluses ??= new GameObject[0];
        chymeBlobs ??= new GameObject[0];
        baseAmplitudes = new float[walls.Length];
        for (int i = 0; i < walls.Length; i++)
            if (walls[i] != null)
            {
                baseAmplitudes[i] = walls[i].amplitude;
                walls[i].amplitude = Mathf.Min(0.3f, baseAmplitudes[i] * churnAmplitudeMultiplier);
            }

        dissolves = new JourneyDissolve[foodBoluses.Length];
        foodScales = new Vector3[foodBoluses.Length];
        foodActive = new bool[foodBoluses.Length];
        for (int i = 0; i < foodBoluses.Length; i++)
            if (foodBoluses[i] != null)
            {
                foodScales[i] = foodBoluses[i].transform.localScale;
                foodActive[i] = foodBoluses[i].activeSelf;
                dissolves[i] = foodBoluses[i].GetComponent<JourneyDissolve>();
                if (dissolves[i] != null) dissolves[i].SetProgress(0);
            }
        chymeScales = new Vector3[chymeBlobs.Length];
        chymeActive = new bool[chymeBlobs.Length];
        for (int i = 0; i < chymeBlobs.Length; i++)
            if (chymeBlobs[i] != null)
            {
                chymeScales[i] = chymeBlobs[i].transform.localScale;
                chymeActive[i] = chymeBlobs[i].activeSelf;
                chymeBlobs[i].SetActive(true);
                chymeBlobs[i].transform.localScale = Vector3.zero;
            }

        foreach (var effect in acidVfx)
            if (effect != null)
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.Play(true);
            }
        if (churnAudio != null) churnAudio.Play();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, digestDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            for (int i = 0; i < foodBoluses.Length; i++)
                if (foodBoluses[i] != null)
                {
                    foodBoluses[i].transform.localScale = foodScales[i] * (dissolves[i] != null ? 1f - .25f * k : 1f - k);
                    if (dissolves[i] != null) dissolves[i].SetProgress(k);
                }
            for (int i = 0; i < chymeBlobs.Length; i++)
                if (chymeBlobs[i] != null) chymeBlobs[i].transform.localScale = chymeScales[i] * k;
            yield return null;
        }

        foreach (var food in foodBoluses) if (food != null) food.SetActive(false);
        StopEffects(false);
        running = false;
        routine = null;
        ClearSnapshot();
        onDigested?.Invoke();
    }

    void StopEffects(bool clear)
    {
        if (acidVfx != null)
            foreach (var effect in acidVfx)
                if (effect != null)
                    effect.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        if (walls != null && baseAmplitudes != null)
            for (int i = 0; i < Mathf.Min(walls.Length, baseAmplitudes.Length); i++)
                if (walls[i] != null) walls[i].amplitude = baseAmplitudes[i];
        if (churnAudio != null) churnAudio.Stop();
    }

    void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        StopEffects(true);
        if (running)
        {
            Restore(foodBoluses, foodScales, foodActive);
            if (dissolves != null) foreach (var d in dissolves) if (d != null) d.SetProgress(0);
            Restore(chymeBlobs, chymeScales, chymeActive);
        }
        routine = null;
        running = false;
        ClearSnapshot();
    }

    static void Restore(GameObject[] objects, Vector3[] scales, bool[] active)
    {
        if (objects == null || scales == null || active == null) return;
        for (int i = 0; i < Mathf.Min(objects.Length, scales.Length); i++)
            if (objects[i] != null)
            {
                objects[i].transform.localScale = scales[i];
                objects[i].SetActive(active[i]);
            }
    }

    void ClearSnapshot()
    {
        baseAmplitudes = null;
        foodScales = null; chymeScales = null;
        foodActive = null; chymeActive = null;
    }
}

