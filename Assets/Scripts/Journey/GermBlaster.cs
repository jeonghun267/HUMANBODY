using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 겨냥해서 맞히는 치료 임무. 손 치료 도구로 겨냥하고 방아쇠를 당겨 표적을 없앤다.
/// 대장의 해로운 균, 위의 헬리코박터균, 식도의 역류 위산 방울, 입의 치태 세균, 병원의 몸 스캔 진찰 점에 쓴다.
/// - 표적은 정해진 자리(spots)에 하나씩 두거나, 기준(잠수정) 앞 범위에 흩어 놓는다.
/// - followBasis 면 잠수정과 함께 움직인다(식도처럼 내려가는 동안).
/// - 지키는 대상(helpful)을 맞히면 안내만 하고 없애지 않는다.
/// - 다 없애면 onCleared, 잠수정 정차를 풀고(releaseRide), 치료 기록(JourneyScore)에 남긴다.
/// </summary>
[AddComponentMenu("인체탐험/겨냥 치료 임무")]
public class GermBlaster : MonoBehaviour
{
    public GameObject[] harmfulPrefabs, helpfulPrefabs;
    public int harmfulCount = 8, helpfulCount = 3;
    [Tooltip("표적을 하나씩 둘 자리 (있으면 개수 대신 이 자리 수만큼)")] public Transform[] spots;
    [Tooltip("표적이 나타날 범위 (기준 기준)")] public Vector3 spawnCenter = new Vector3(0, 0, 6.5f), spawnExtents = new Vector3(2f, 1.4f, 2.5f);
    public Vector2 scaleRange = new Vector2(.35f, .55f);
    [Tooltip("범위의 기준. 비우면 ride, 그것도 없으면 이 물체")] public Transform basis;
    [Tooltip("표적이 기준과 함께 움직인다 (내려가는 잠수정 주변)")] public bool followBasis;
    [Tooltip("떠다니는 폭(m)")] public float bob = .35f;
    public CapsuleRide ride;
    [Tooltip("다 없애면 잠수정 정차를 푼다")] public bool releaseRide = true;
    public LineRenderer beam;
    public ParticleSystem hitBurst;
    public float range = 16f, aimRadius = .45f, cooldown = .2f, beamSeconds = .08f;
    [Tooltip("쏠 때 / 맞혔을 때 / 지킬 대상을 맞혔을 때 소리 (Sfx 이름)")] public string fireSound = "beam", hitSound = "pop", wrongSound = "wrong";
    [Tooltip("맞히면 없애는 대신 커지며 빛난다 (유익균에게 먹이 주기 등)")] public bool growOnHit;
    [Tooltip("표적 위에 띄울 이름 (효소 짝 맞추기의 음식 이름 등). 비우면 띄우지 않는다")] public string harmfulTag, helpfulTag;
    [Tooltip("모형마다 다른 이름 (prefabs 와 같은 순서). 있으면 위 이름 대신 쓴다")] public string[] harmfulTags = new string[0], helpfulTags = new string[0];
    public TMPro.TMP_FontAsset tagFont;

    [Header("안내")]
    public string targetLabel = "해로운 균";
    [TextArea] public string goalText = "해로운 균을 겨냥하고 방아쇠를 당겨 물리치세요";
    public string clearedStatus = "해로운 균을 모두 물리쳤어요!";
    public NarrationLine[] startLines, helpfulHitLines, clearedLines;
    [Tooltip("안내를 병아리 조수 대신 이 사람(의사 등)이 말한다")] public HospitalNpc speakerNpc;
    [Tooltip("자리(spots)마다 맞혔을 때 들려줄 대사 (진찰 결과 등). 자리가 없으면 맞힌 차례대로")] public NarrationLine[] hitLines;

    [Header("기록")]
    public string scoreKey;
    public string scoreLabel;
    [Tooltip("장면이 시작되면 안내가 끝난 뒤 저절로 시작")] public bool beginOnStart;
    [Tooltip("새 여정의 시작이면 치료 기록을 지운다 (병원)")] public bool resetScore;
    public UnityEvent onCleared = new UnityEvent();

    class Germ { public Transform t; public Vector3 home; public float phase, scale; public bool harmful, fed; public int index; }
    readonly List<Germ> germs = new List<Germ>();
    int remaining, total;
    float nextShot, beamOff, warnAfter;
    bool active, done;

    public bool IsActive => active;
    public int Remaining => remaining;

    IEnumerator Start()
    {
        if (resetScore) JourneyScore.Reset();
        if (!beginOnStart) yield break;
        while (JourneyManager.Instance != null && (JourneyManager.Instance.IsIntroPending || JourneyManager.Instance.IsTransitioning)) yield return null;
        while (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) yield return null;
        Begin();
    }

    Transform Basis => basis != null ? basis : ride != null ? ride.transform : transform;

    /// <summary>임무 시작 (정차 지점 도착, 앞 임무 완료 등에서 호출).</summary>
    public void Begin()
    {
        if (active || done) return;
        active = true;
        if (spots != null && spots.Length > 0) SpawnAtSpots();
        else { Spawn(harmfulPrefabs, harmfulCount, true); Spawn(helpfulPrefabs, helpfulCount, false); }
        total = remaining = germs.FindAll(g => g.harmful).Count;
        if (total == 0) { Finish(); return; }
        Record();
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetGoal(goalText); JourneyHud.Instance.SetStatus(Score()); }
        Speak(startLines);
    }

    /// <summary>안내 말하기: 지정한 사람이 있으면 그 사람이, 없으면 병아리 조수가.</summary>
    void Speak(NarrationLine[] lines)
    {
        if (lines == null || lines.Length == 0) return;
        if (speakerNpc != null) { foreach (var l in lines) speakerNpc.StartCoroutine(speakerNpc.SayRoutine(l)); return; }
        if (GuideNarrator.Instance != null) GuideNarrator.Instance.Play(lines);
    }

    void SpawnAtSpots()
    {
        if (harmfulPrefabs == null || harmfulPrefabs.Length == 0) return;
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] == null) continue;
            var prefab = harmfulPrefabs[i % harmfulPrefabs.Length];
            if (prefab == null) continue;
            var go = Instantiate(prefab, spots[i].position, spots[i].rotation, spots[i]); // 자리(이·턱)와 함께 움직인다
            float s = Random.Range(scaleRange.x, scaleRange.y);
            go.transform.localScale = Vector3.one * s / Mathf.Max(1e-4f, spots[i].lossyScale.x);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            germs.Add(new Germ { t = go.transform, home = Vector3.zero, phase = Random.value * 10f, scale = s, harmful = true, index = i });
        }
    }

    void Spawn(GameObject[] prefabs, int count, bool harmful)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        var origin = Basis;
        for (int i = 0; i < count; i++)
        {
            var prefab = prefabs[i % prefabs.Length];
            if (prefab == null) continue;
            var local = spawnCenter + Vector3.Scale(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)), spawnExtents);
            var go = Instantiate(prefab, origin.TransformPoint(local), Random.rotation, transform);
            float s = Random.Range(scaleRange.x, scaleRange.y);
            go.transform.localScale = Vector3.one * s;
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false; // 맞힘 판정은 광선 거리로 한다
            germs.Add(new Germ { t = go.transform, home = followBasis ? local : go.transform.position, phase = Random.value * 10f, scale = s, harmful = harmful });
            var tags = harmful ? harmfulTags : helpfulTags;
            Tag(go.transform, tags != null && tags.Length > 0 ? tags[i % prefabs.Length % tags.Length] : harmful ? harmfulTag : helpfulTag, s);
        }
    }

    /// <summary>표적 위에 작은 이름표 (항상 탐험가 쪽을 본다).</summary>
    void Tag(Transform target, string text, float size)
    {
        if (string.IsNullOrEmpty(text)) return;
        var go = new GameObject("이름표", typeof(RectTransform));
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.up * .9f; go.transform.localScale = Vector3.one * (.01f / Mathf.Max(.05f, size));
        var label = go.AddComponent<TMPro.TextMeshPro>();
        label.text = text; label.fontSize = 22; label.alignment = TMPro.TextAlignmentOptions.Center; label.color = new Color(1f, .95f, .7f);
        if (tagFont != null) label.font = tagFont;
        label.rectTransform.sizeDelta = new Vector2(30, 6);
        go.AddComponent<Billboard>();
    }

    string Score() => $"{targetLabel} {total - remaining} / {total}";
    void Record() => JourneyScore.Record(scoreKey, string.IsNullOrEmpty(scoreLabel) ? targetLabel : scoreLabel, total - remaining, total);

    /// <summary>남은 표적을 한꺼번에 없앤다 (건너뛰기·자동 검사용).</summary>
    public void DefeatAll() { if (active) { remaining = 0; Record(); Finish(); } }

    void Update()
    {
        if (beam != null && beam.enabled && Time.time > beamOff) beam.enabled = false;
        if (!active) return;
        float time = Time.time;
        var origin = Basis;
        foreach (var g in germs)
        {
            if (g.t == null || !g.t.gameObject.activeSelf) continue;
            // 천천히 떠다니며 맴돈다 (빠르게 움직이면 맞히기 어렵고 어지럽다)
            var drift = new Vector3(Mathf.Sin(time * .6f + g.phase), Mathf.Sin(time * .45f + g.phase * 1.7f) * .6f, Mathf.Cos(time * .5f + g.phase)) * bob;
            if (spots != null && spots.Length > 0) g.t.localPosition = drift * .25f / Mathf.Max(1e-4f, g.t.parent != null ? g.t.parent.lossyScale.x : 1);
            else g.t.position = (followBasis ? origin.TransformPoint(g.home) : g.home) + drift;
            g.t.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
        }
        if (!JourneyAim.TriggerDown() || time < nextShot) return;
        nextShot = time + cooldown;
        var ray = JourneyAim.Ray();
        Germ best = null; float bestAlong = float.MaxValue;
        foreach (var g in germs)
        {
            if (g.t == null || !g.t.gameObject.activeSelf || g.fed) continue;
            if (JourneyAim.Near(ray, g.t.position, aimRadius + g.scale * .6f, range, out float along) && along < bestAlong) { best = g; bestAlong = along; }
        }
        Vector3 end = best != null ? best.t.position : ray.origin + ray.direction * range;
        JourneyAim.Beam(beam, ray.origin + ray.direction * .1f, end);
        beamOff = time + beamSeconds;
        Haptics.Pulse(.35f, .06f);
        Sfx.Play(fireSound, ray.origin, .45f, Random.Range(.95f, 1.08f));
        if (best == null) return;
        if (!best.harmful)
        {
            Sfx.Play(wrongSound, end, .6f, 1f);
            if (time > warnAfter && helpfulHitLines != null && helpfulHitLines.Length > 0) { Speak(helpfulHitLines); warnAfter = time + 6f; }
            return;
        }
        if (growOnHit) { best.fed = true; best.t.localScale *= 1.35f; best.t.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0, 90, 0); }
        else best.t.gameObject.SetActive(false);
        if (hitBurst != null) { hitBurst.transform.position = end; hitBurst.Emit(24); }
        Sfx.Play(hitSound, end, .8f, Random.Range(.95f, 1.1f));
        Haptics.Pulse(.7f, .12f);
        int hitIndex = spots != null && spots.Length > 0 ? best.index : total - remaining;
        remaining--;
        Record();
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus(Score());
        if (remaining > 0 && hitLines != null && hitIndex < hitLines.Length) Speak(new[] { hitLines[hitIndex] });
        if (remaining <= 0) StartCoroutine(FinishAfter(hitLines != null && hitIndex < hitLines.Length ? hitLines[hitIndex] : null));
    }

    IEnumerator FinishAfter(NarrationLine last)
    {
        // 마지막 진찰 대사를 먼저 들려준 뒤 마무리 대사로 넘어간다.
        if (last != null && speakerNpc != null) yield return speakerNpc.SayRoutine(last);
        else if (last != null && GuideNarrator.Instance != null) yield return GuideNarrator.Instance.PlayRoutine(new[] { last });
        Finish();
    }

    void Finish()
    {
        if (done) return;
        active = false; done = true;
        foreach (var g in germs) if (g.t != null && g.harmful && !growOnHit) g.t.gameObject.SetActive(false);
        Sfx.Play("success", Basis.position, .7f);
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetStatus(clearedStatus); JourneyHud.Instance.SetGoal(""); }
        Speak(clearedLines);
        onCleared?.Invoke();
        if (releaseRide && ride != null) ride.Release();
    }
}
