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
    [Tooltip("범위로 뿌릴 때, 기준(잠수정) 가운데에서 이 반지름(m) 안에는 표적을 두지 않는다 (잠수정 유리 안·눈앞에 생기면 어지럽다). 떠다니는 폭과 표적 크기만큼 더 띄운다")]
    public float clearRadius = 2.1f;
    public CapsuleRide ride;
    [Tooltip("다 없애면 잠수정 정차를 푼다")] public bool releaseRide = true;
    public LineRenderer beam;
    public ParticleSystem hitBurst;
    public float range = 16f, aimRadius = .45f, cooldown = .2f, beamSeconds = .08f;
    [Tooltip("쏠 때 / 맞혔을 때 / 지킬 대상을 맞혔을 때 소리 (Sfx 이름)")] public string fireSound = "beam", hitSound = "pop", wrongSound = "wrong";
    [Tooltip("맞히면 없애는 대신 커지며 빛난다 (유익균에게 먹이 주기 등)")] public bool growOnHit;
    [Tooltip("먹이 주기에서 먹이가 날아올 무리 (대장의 식이섬유). 맞힌 표적에 가장 가까운 것이 날아가 먹힌다")] public FloatingSwarm feedSource;
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
    [System.Serializable] public class HitEvent : UnityEvent<int> { }
    [Tooltip("표적을 맞힐 때마다 (자리가 있으면 자리 번호, 없으면 맞힌 차례) — 병원 진찰 결과 모니터 등")] public HitEvent onHit = new HitEvent();

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
            float s = Random.Range(scaleRange.x, scaleRange.y);
            var local = spawnCenter + Vector3.Scale(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)), spawnExtents);
            float clear = clearRadius > 0 ? clearRadius + bob + s * .6f : 0f;
            if (local.magnitude < clear) local = (local.sqrMagnitude > 1e-4f ? local.normalized : Vector3.forward) * clear; // 잠수정 둘레 밖으로
            var go = Instantiate(prefab, origin.TransformPoint(local), Random.rotation, transform);
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
        if (growOnHit)
        {
            best.fed = true;
            var food = NearestFeed(best.t.position);
            if (food != null) StartCoroutine(Feed(food, best.t));
            else Grow(best.t);
        }
        else best.t.gameObject.SetActive(false);
        if (hitBurst != null) { hitBurst.transform.position = end; hitBurst.Emit(24); }
        Sfx.Play(hitSound, end, .8f, Random.Range(.95f, 1.1f));
        Haptics.Pulse(.7f, .12f);
        int hitIndex = spots != null && spots.Length > 0 ? best.index : total - remaining;
        remaining--;
        Record();
        onHit?.Invoke(hitIndex);
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus(Score());
        if (remaining > 0 && hitLines != null && hitIndex < hitLines.Length) Speak(new[] { hitLines[hitIndex] });
        if (remaining <= 0) StartCoroutine(FinishAfter(hitLines != null && hitIndex < hitLines.Length ? hitLines[hitIndex] : null));
    }

    IEnumerator MoveAside(Transform t)
    {
        yield return new WaitForSeconds(growOnHit ? .8f : .2f); // 배부른 유익균은 잠깐 보여 준다
        if (t == null) yield break;
        var basis = Basis;
        var side = Vector3.ProjectOnPlane(t.position - basis.position, basis.forward);
        if (side.sqrMagnitude < .09f) side = basis.right * (Random.value < .5f ? -1 : 1);
        Vector3 from = t.position, to = from + side.normalized * 2.5f + Vector3.up * .3f, size = t.localScale;
        for (float k = 0; k < 1 && t != null; k += Time.deltaTime / 1.4f)
        {
            float e = Mathf.SmoothStep(0, 1, k);
            t.position = Vector3.Lerp(from, to, e);
            t.localScale = size * (1 - e);
            yield return null;
        }
        if (t != null) t.gameObject.SetActive(false);
    }

    static void Grow(Transform germ) { germ.localScale *= 1.35f; germ.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0, 90, 0); }

    /// <summary>먹이 무리(식이섬유)에서 표적에 가장 가까운 것 (12m 안).</summary>
    Transform NearestFeed(Vector3 at)
    {
        if (feedSource == null) return null;
        Transform best = null; float bestDist = 12f * 12f;
        foreach (Transform c in feedSource.transform)
        {
            if (!c.gameObject.activeInHierarchy) continue;
            float d = (c.position - at).sqrMagnitude;
            if (d < bestDist) { best = c; bestDist = d; }
        }
        return best;
    }

    /// <summary>식이섬유가 맞힌 유익균에게 날아가 먹히고, 유익균이 커지며 빛난다.</summary>
    IEnumerator Feed(Transform food, Transform germ)
    {
        food.SetParent(null, true); // 무리의 위치 갱신에서 떼어 낸다
        Vector3 from = food.position, size = food.localScale;
        float t = 0, seconds = .7f;
        while (t < seconds && food != null && germ != null)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, t / seconds);
            food.position = Vector3.Lerp(from, germ.position, k) + Vector3.up * .4f * Mathf.Sin(k * Mathf.PI);
            food.localScale = size * (1 - .8f * k);
            yield return null;
        }
        if (food != null) Destroy(food.gameObject);
        if (germ == null) yield break;
        Grow(germ);
        if (hitBurst != null) { hitBurst.transform.position = germ.position; hitBurst.Emit(12); }
        Sfx.Play("ting", germ.position, .5f, 1.2f);
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
        // 길 위에 떠 있던 나머지(지킬 대상, 배부른 유익균)는 벽 쪽으로 비켜 사라진다. 그대로 두면 다시 출발한 잠수정이 그 사이를 뚫고 지나가 눈앞을 스친다.
        if (spots == null || spots.Length == 0)
            foreach (var g in germs) if (g.t != null && g.t.gameObject.activeSelf) StartCoroutine(MoveAside(g.t));
        Sfx.Play("success", Basis.position, .7f);
        if (JourneyHud.Instance != null) { JourneyHud.Instance.SetStatus(clearedStatus); JourneyHud.Instance.SetGoal(""); }
        Speak(clearedLines);
        onCleared?.Invoke();
        if (releaseRide && ride != null) ride.Release();
    }
}
