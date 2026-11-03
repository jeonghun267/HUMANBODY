using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대장 속 해로운 균 물리치기. 잠수정이 멈추면 앞쪽에 해로운 균(세균·바이러스)과 유익균이 떠다닌다.
/// 면역 광선으로 해로운 균만 맞혀 없애고, 유익균을 맞히면 "유익균은 지켜 주자"고 알려 준다. 다 없애면 잠수정이 다시 출발한다.
/// </summary>
[AddComponentMenu("인체탐험/해로운 균 물리치기")]
public class GermBlaster : MonoBehaviour
{
    public GameObject[] harmfulPrefabs, helpfulPrefabs;
    public int harmfulCount = 8, helpfulCount = 3;
    [Tooltip("잠수정 앞 균이 나타날 범위 (잠수정 기준)")] public Vector3 spawnCenter = new Vector3(0, 0, 6.5f), spawnExtents = new Vector3(2f, 1.4f, 2.5f);
    public Vector2 scaleRange = new Vector2(.35f, .55f);
    public CapsuleRide ride;
    public LineRenderer beam;
    public ParticleSystem hitBurst;
    public float range = 16f, aimRadius = .45f, cooldown = .2f, beamSeconds = .08f;
    [TextArea] public string goalText = "해로운 균을 겨냥하고 방아쇠를 당겨 물리치세요";
    public NarrationLine[] startLines, helpfulHitLines, clearedLines;

    class Germ { public Transform t; public Vector3 home; public float phase, scale; public bool harmful; }
    readonly List<Germ> germs = new List<Germ>();
    int remaining, total;
    float nextShot, beamOff, warnAfter;
    bool active, done;

    /// <summary>정차 지점 도착 시 호출(라이드 정차 이벤트).</summary>
    public void Begin()
    {
        if (active || done) return;
        active = true;
        var basis = ride != null ? ride.transform : transform;
        Spawn(harmfulPrefabs, harmfulCount, true, basis);
        Spawn(helpfulPrefabs, helpfulCount, false, basis);
        total = remaining = germs.FindAll(g => g.harmful).Count;
        if (total == 0) { Finish(); return; }
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetGoal(goalText); JourneyHud.Instance.SetStatus(Score()); }
        if (GuideNarrator.Instance != null && startLines != null && startLines.Length > 0) GuideNarrator.Instance.Play(startLines);
    }

    void Spawn(GameObject[] prefabs, int count, bool harmful, Transform basis)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var prefab = prefabs[i % prefabs.Length];
            if (prefab == null) continue;
            var local = spawnCenter + Vector3.Scale(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)), spawnExtents);
            var go = Instantiate(prefab, basis.TransformPoint(local), Random.rotation, transform);
            float s = Random.Range(scaleRange.x, scaleRange.y);
            go.transform.localScale = Vector3.one * s;
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false; // 맞힘 판정은 광선 거리로 한다
            germs.Add(new Germ { t = go.transform, home = go.transform.position, phase = Random.value * 10f, scale = s, harmful = harmful });
        }
    }

    string Score() => $"해로운 균 {total - remaining} / {total}";

    public bool IsActive => active;
    public int Remaining => remaining;

    /// <summary>남은 해로운 균을 한꺼번에 없앤다 (건너뛰기·자동 검사용).</summary>
    public void DefeatAll() { if (active) { remaining = 0; Finish(); } }

    void Update()
    {
        if (beam != null && beam.enabled && Time.time > beamOff) beam.enabled = false;
        if (!active) return;
        float time = Time.time;
        foreach (var g in germs)
        {
            if (g.t == null || !g.t.gameObject.activeSelf) continue;
            // 천천히 떠다니며 맴돈다 (빠르게 움직이면 맞히기 어렵고 어지럽다)
            g.t.position = g.home + new Vector3(Mathf.Sin(time * .6f + g.phase), Mathf.Sin(time * .45f + g.phase * 1.7f) * .6f, Mathf.Cos(time * .5f + g.phase)) * .35f;
            g.t.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
        }
        if (!JourneyAim.TriggerDown() || time < nextShot) return;
        nextShot = time + cooldown;
        var ray = JourneyAim.Ray();
        Germ best = null; float bestAlong = float.MaxValue;
        foreach (var g in germs)
        {
            if (g.t == null || !g.t.gameObject.activeSelf) continue;
            if (JourneyAim.Near(ray, g.t.position, aimRadius + g.scale * .6f, range, out float along) && along < bestAlong) { best = g; bestAlong = along; }
        }
        Vector3 end = best != null ? best.t.position : ray.origin + ray.direction * range;
        JourneyAim.Beam(beam, ray.origin + ray.direction * .1f, end);
        beamOff = time + beamSeconds;
        Haptics.Pulse(.35f, .06f);
        if (best == null) return;
        if (!best.harmful)
        {
            Sfx.Play("ting", end, .5f, .7f);
            if (time > warnAfter && GuideNarrator.Instance != null && helpfulHitLines != null && helpfulHitLines.Length > 0) { GuideNarrator.Instance.Play(helpfulHitLines); warnAfter = time + 6f; }
            return;
        }
        best.t.gameObject.SetActive(false);
        if (hitBurst != null) { hitBurst.transform.position = end; hitBurst.Emit(24); }
        Sfx.Play("snip", end, .7f, 1.2f);
        Haptics.Pulse(.7f, .12f);
        remaining--;
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus(Score());
        if (remaining <= 0) Finish();
    }

    void Finish()
    {
        active = false; done = true;
        foreach (var g in germs) if (g.t != null && g.harmful) g.t.gameObject.SetActive(false);
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetStatus("해로운 균을 모두 물리쳤어요!"); JourneyHud.Instance.SetGoal(""); }
        if (GuideNarrator.Instance != null && clearedLines != null && clearedLines.Length > 0) GuideNarrator.Instance.Play(clearedLines);
        if (ride != null) ride.Release();
    }
}
