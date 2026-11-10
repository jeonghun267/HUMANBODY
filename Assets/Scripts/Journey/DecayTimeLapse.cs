using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 양치를 안 하면 어떻게 될까(입): 씹기가 끝나면 시간을 빨리 돌려, 이 사이에 남은 음식 찌꺼기를 세균이 먹고 불어나고
/// 세균이 내뿜는 산에 이가 녹아 까만 충치가 생기는 과정을 보여 준다. 다 보여 주면 충치 치료를 시작한다.
/// 이때 생긴 세균은 치태 세균 임무가 시작될 때 걷어 낸다(임무가 같은 자리에 세균을 다시 띄운다).
/// </summary>
[AddComponentMenu("인체탐험/충치가 생기는 과정")]
public class DecayTimeLapse : MonoBehaviour
{
    public CavityTreatment cavity;
    [Tooltip("음식 찌꺼기가 남고 세균이 불어날 이 표면 자리 (치태 세균 자리)")] public Transform[] spots;
    public GameObject residuePrefab, germPrefab;
    [Tooltip("찌꺼기·세균 하나의 크기 배율 (원본 모형 기준)")] public float residueScale = .15f, germScale = .25f;
    [Tooltip("자리마다 늘어나는 최대 세균 수")] public int germsPerSpot = 3;
    [Tooltip("세균이 내뿜는 산 거품 (충치 자리에서)")] public ParticleSystem acid;
    public NarrationLine[] startLines, cavityLines;
    [Tooltip("화면에 차례로 보여 줄 시간 흐름")] public string[] clock = { "1시간 뒤", "하루 뒤", "일주일 뒤", "한 달 뒤" };
    public float stepSeconds = 2.2f;
    [TextArea] public string goalText = "양치를 안 하면 이가 어떻게 되는지 지켜보세요";
    public UnityEvent onFinished = new UnityEvent();

    readonly List<Transform> germs = new List<Transform>(), residues = new List<Transform>();
    readonly Dictionary<Transform, int> perSpot = new Dictionary<Transform, int>();
    readonly Dictionary<Transform, Vector3> full = new Dictionary<Transform, Vector3>(); // 다 자란 크기
    bool running, finished;
    public bool IsRunning => running;
    public bool IsFinished => finished;

    public void Begin() { if (!running && !finished && isActiveAndEnabled) StartCoroutine(Run()); }

    IEnumerator Run()
    {
        running = true;
        var narrator = GuideNarrator.Instance;
        while (narrator != null && narrator.IsSpeaking) yield return null; // 씹기 안내를 끊지 않게
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetGoal(goalText);
        if (narrator != null && startLines != null && startLines.Length > 0) narrator.Play(startLines);

        // 1) 이 사이에 음식 찌꺼기가 남는다
        if (spots != null && residuePrefab != null)
            foreach (var s in spots) if (s != null) residues.Add(Spawn(residuePrefab, s, Random.insideUnitCircle * .08f));
        yield return Grow(residues, .7f);

        // 2) 시간이 흐르며 세균이 찌꺼기를 먹고 불어나고, 세균이 내뿜는 산에 충치가 점점 커진다
        int steps = Mathf.Max(1, clock.Length);
        var where = cavity != null && cavity.decay != null ? cavity.decay.position : transform.position;
        for (int k = 0; k < steps; k++)
        {
            if (JourneyHud.Instance != null && k < clock.Length) JourneyHud.Instance.SetStatus("시간이 흘러… " + clock[k]);
            Sfx.Play("whoosh", where, .35f, 1.25f);
            var born = new List<Transform>();
            if (spots != null && germPrefab != null)
                foreach (var s in spots)
                {
                    if (s == null) continue;
                    perSpot.TryGetValue(s, out int n);
                    if (n >= germsPerSpot) continue;
                    perSpot[s] = n + 1;
                    var g = Spawn(germPrefab, s, n == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * .2f);
                    var bob = g.gameObject.AddComponent<GentleFloat>(); bob.bob = .02f; bob.drift = .02f; bob.tilt = 8f; bob.spin = 25f; bob.period = 2.5f;
                    born.Add(g); germs.Add(g);
                }
            float t = 0, from = (float)k / steps, to = (float)(k + 1) / steps;
            var shrink = residues.ConvertAll(r => r != null ? r.localScale : Vector3.zero);
            while (t < stepSeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / stepSeconds), grow = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .8f));
                foreach (var g in born) if (g != null) g.localScale = full[g] * grow;
                for (int i = 0; i < residues.Count; i++) if (residues[i] != null) residues[i].localScale = shrink[i] * Mathf.Lerp(1, .7f, u); // 세균이 찌꺼기를 먹는다
                if (cavity != null) cavity.Reveal(Mathf.Lerp(from, to, u));
                if (acid != null && Random.value < .25f) { acid.transform.position = where; acid.Emit(1); }
                yield return null;
            }
        }
        if (cavity != null) cavity.Reveal(1);
        foreach (var r in residues) if (r != null) Destroy(r.gameObject);
        residues.Clear();
        if (JourneyHud.Instance != null) JourneyHud.Instance.SetStatus("");

        // 3) 충치가 생겼다 → 치료 시작
        while (narrator != null && narrator.IsSpeaking) yield return null;
        if (narrator != null && cavityLines != null && cavityLines.Length > 0) yield return narrator.PlayRoutine(cavityLines);
        running = false; finished = true;
        onFinished?.Invoke();
    }

    /// <summary>시간 흐름에서 생긴 세균을 걷어 낸다 (치태 세균 임무가 같은 자리에 다시 띄운다).</summary>
    public void ClearGerms()
    {
        foreach (var g in germs) if (g != null) Destroy(g.gameObject);
        germs.Clear();
    }

    /// <summary>바로 끝낸다 (건너뛰기·자동 검사용).</summary>
    public void FinishNow()
    {
        if (finished) return;
        StopAllCoroutines();
        foreach (var r in residues) if (r != null) Destroy(r.gameObject);
        residues.Clear();
        if (cavity != null) cavity.Reveal(1);
        running = false; finished = true;
        onFinished?.Invoke();
    }

    Transform Spawn(GameObject prefab, Transform spot, Vector2 offset)
    {
        var go = Instantiate(prefab, spot);
        var t = go.transform;
        t.position = spot.position + spot.right * offset.x + spot.up * offset.y + spot.forward * .04f;
        t.rotation = spot.rotation * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), 0);
        float parent = Mathf.Max(1e-4f, spot.lossyScale.x);
        full[t] = prefab.transform.localScale * ((prefab == residuePrefab ? residueScale : germScale) / parent);
        t.localScale = Vector3.zero;
        return t;
    }

    IEnumerator Grow(List<Transform> items, float seconds)
    {
        float t = 0;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds));
            foreach (var i in items) if (i != null) i.localScale = full[i] * k;
            yield return null;
        }
    }
}
