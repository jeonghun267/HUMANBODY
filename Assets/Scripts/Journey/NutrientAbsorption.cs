using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 소장 단계 미니 체험: 떠다니는 영양소(포도당/아미노산/지방산)를 시선 또는 손으로 융털에 흡수시킨다.
/// 컨트롤러 트리거를 쓰지 않고 '응시 후 A 버튼' 또는 '손 닿기'로 동작해 어린이도 쉽게 참여할 수 있다.
/// 모두 흡수하면 가이드가 정리 멘트를 하고(선택) 다음 단계로 넘어간다.
/// </summary>
[AddComponentMenu("인체탐험/영양소 흡수 체험") ]
public class NutrientAbsorption : MonoBehaviour
{
    [System.Serializable]
    public class NutrientKind
    {
        public string id = "Glucose";
        public string displayName = "포도당";
        public GameObject prefab;
        public int count = 5;
        [TextArea(1, 3)] public string absorbedLine = "포도당은 우리 몸의 에너지원이에요!";
        public AudioClip absorbedVoice;
        [Tooltip("흡수될 때 그리는 빛 꼬리 색")] public Color trailColor = new Color(1f, .85f, .4f);
    }

    [Header("영양소")]
    public NutrientKind[] kinds;
    public FloatingSwarm swarm;

    [Header("흡수 방법")]
    public Transform gazeOrigin;
    public float gazeDistance = 8f;
    [Tooltip("응시만으로 흡수되기까지 시간(초). 0 이면 버튼 필요")]
    public float gazeDwellSeconds = 1.2f;
    public LayerMask nutrientLayer = ~0;

    [Header("흡수 연출")]
    public Transform absorbTarget;
    public float absorbDuration = 0.6f;
    public ParticleSystem absorbVfx;
    public AudioClip absorbSfx;
    [Tooltip("흡수될 때 빛 꼬리 재질 (비우면 꼬리 없음)")] public Material trailMaterial;
    [Tooltip("장벽 충돌체: 융털 모형이 멀면 가장 가까운 벽(벽 전체가 융털)으로 빨려 들어간다")] public Collider wall;

    [Header("화면")]
    public TextMeshProUGUI progressText;
    public string progressFormat = "흡수 관찰 {0} / {1}";

    [Header("완료")]
    public NarrationLine[] completeLines;
    public bool advanceOnComplete = true;
    public UnityEvent onComplete = new UnityEvent();

    readonly List<Nutrient> nutrients = new List<Nutrient>();
    readonly Dictionary<string, bool> explained = new Dictionary<string, bool>();
    Nutrient gazed;
    float gazeTimer;
    int absorbed, total;
    AudioSource sfx;
    bool completed;

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 0f;
        if (gazeOrigin == null) gazeOrigin = JourneyEye.Get();

        Transform parent = swarm != null ? swarm.transform : transform;
        foreach (var k in kinds)
        {
            if (k.prefab == null) continue;
            for (int i = 0; i < k.count; i++)
            {
                var go = Instantiate(k.prefab, parent);
                Vector3 ext = swarm != null ? swarm.spawnExtents : Vector3.one * 2f;
                go.transform.localPosition = new Vector3(Random.Range(-ext.x, ext.x), Random.Range(-ext.y, ext.y), Random.Range(-ext.z, ext.z));
                var n = go.GetComponent<Nutrient>() ?? go.AddComponent<Nutrient>();
                n.kind = k; n.owner = this;
                if (go.GetComponent<Collider>() == null) { var sc = go.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.4f; }
                nutrients.Add(n);
            }
        }
        // FloatingSwarm.Start 가 자식을 무리에 등록하므로, 그 전에(Awake) 생성해 둔다.
        total = nutrients.Count;
    }

    void Start() { UpdateUI(); }

    void Update()
    {
        if (completed) return;
        if (gazeOrigin == null)
        {
            // 영속 리그가 이 오브젝트보다 늦게 생길 수 있으므로 매 프레임 재시도
            gazeOrigin = JourneyEye.Get();
            if (gazeOrigin == null) return;
        }

        // 손 치료 도구로 겨냥한다 (컨트롤러가 없으면 시선). 손으로 가리키고 방아쇠를 당기는 체험이 되도록.
        Nutrient hit = null;
        var aim = JourneyAim.Ray();
        if (Physics.Raycast(aim.origin, aim.direction, out var h, gazeDistance, nutrientLayer))
            hit = h.collider.GetComponentInParent<Nutrient>();

        if (hit != gazed) { gazed = hit; gazeTimer = 0f; }
        if (gazed == null || gazed.absorbed) return;

        gazed.SetHighlight(true);
        gazeTimer += Time.deltaTime;
        // A 버튼은 승하차/기타 상호작용과 겹치므로 검지 트리거 또는 응시 유지로만 흡수
        bool pressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.All);
        if (pressed || (gazeDwellSeconds > 0f && gazeTimer >= gazeDwellSeconds))
            Absorb(gazed);
    }

    public void Absorb(Nutrient n)
    {
        if (n == null || n.absorbed || completed) return;
        n.absorbed = true;
        absorbed++;
        UpdateUI();
        if (absorbSfx != null) sfx.PlayOneShot(absorbSfx);
        else Sfx.Play("ting", n.transform.position, 0.8f, Random.Range(0.95f, 1.15f));
        Haptics.Pulse(0.5f, 0.1f);
        StartCoroutine(AbsorbRoutine(n));

        if (GuideNarrator.Instance != null && !explained.ContainsKey(n.kind.id) && !string.IsNullOrEmpty(n.kind.absorbedLine))
        {
            explained[n.kind.id] = true;
            GuideNarrator.Instance.Play(new NarrationLine { text = n.kind.absorbedLine, voice = n.kind.absorbedVoice, holdSeconds = 3f });
        }

        if (absorbed >= total) StartCoroutine(CompleteRoutine());
    }

    IEnumerator AbsorbRoutine(Nutrient n)
    {
        Transform t = n.transform;
        t.SetParent(null, true); // FloatingSwarm 의 위치 갱신에서 분리
        Vector3 start = t.position, startScale = t.localScale;
        Vector3 end = AbsorbPoint(start);
        // 빛 꼬리를 그리며 빨려 들어간다 (먼 벽일수록 조금 더 오래)
        if (trailMaterial != null)
        {
            var trail = t.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial; trail.time = .45f; trail.minVertexDistance = .05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .14f), new Keyframe(1, 0));
            var c = n.kind != null ? n.kind.trailColor : Color.white;
            trail.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, alphaKeys = new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) } };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
        }
        float duration = absorbDuration * Mathf.Clamp(Vector3.Distance(start, end) / 2.5f, 1f, 2f);
        Vector3 lift = Vector3.up * Mathf.Min(.6f, Vector3.Distance(start, end) * .15f);
        float e = 0f;
        while (e < duration && t != null)
        {
            e += Time.deltaTime;
            float k = e / duration;
            t.position = Vector3.Lerp(start, end, k * k) + lift * Mathf.Sin(k * Mathf.PI);
            t.localScale = startScale * (1f - k * .85f);
            yield return null;
        }
        JourneyParticleBurst.Emit(absorbVfx, end, 20);
        if (t != null) Destroy(t.gameObject);
    }

    /// <summary>
    /// 빨려 들어갈 곳: 융털 모형(모세혈관)이 가까우면 그곳, 멀면 가장 가까운 장벽(벽 전체가 융털로 덮여 있다).
    /// 멀리 있는 모형으로 몇십 m 를 날아가면 순간이동처럼 보인다.
    /// </summary>
    Vector3 AbsorbPoint(Vector3 from)
    {
        const float near = 6f;
        if (absorbTarget != null && Vector3.Distance(from, absorbTarget.position) < near) return absorbTarget.position;
        if (wall != null)
        {
            float best = float.MaxValue; Vector3 point = Vector3.zero;
            for (int i = 0; i < 26; i++)
            {
                var d = new Vector3(i % 3 - 1, i / 3 % 3 - 1, i / 9 - 1);
                if (d == Vector3.zero) continue;
                if (wall.Raycast(new Ray(from, d.normalized), out var hit, 12f) && hit.distance < best) { best = hit.distance; point = hit.point - d.normalized * .05f; }
            }
            if (best < float.MaxValue) return point;
        }
        return absorbTarget != null ? absorbTarget.position : from + Vector3.down * 1.5f;
    }

    IEnumerator CompleteRoutine()
    {
        completed = true;
        yield return new WaitForSeconds(1f);
        if (GuideNarrator.Instance != null && completeLines != null && completeLines.Length > 0)
            yield return GuideNarrator.Instance.PlayRoutine(completeLines);
        onComplete?.Invoke();
        if (advanceOnComplete && JourneyManager.Instance != null) JourneyManager.Instance.GoToNext();
    }

    void UpdateUI()
    {
        if (progressText != null) progressText.text = string.Format(progressFormat, absorbed, total);
        JourneyScore.Record("nutrients", "영양소 흡수", absorbed, total);
    }
}
