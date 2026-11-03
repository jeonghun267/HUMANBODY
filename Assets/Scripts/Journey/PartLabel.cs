using TMPro;
using UnityEngine;

/// <summary>시선 상호작용 대상 공통 인터페이스 (GazeSelector 가 찾는다).</summary>
public interface IGazeTarget
{
    void OnGazeEnter();
    void OnGazeExit();
}

/// <summary>
/// 씬 안의 개별 파츠(혀, 윗니, 융털, 적혈구 …)에 붙이는 이름표.
/// 바라보면 이름표가 뜨고 발광 하이라이트 + 가이드가 한 줄 설명(음성/자막)을 해 준다.
/// </summary>
[AddComponentMenu("인체탐험/부위 이름표") ]
public class PartLabel : MonoBehaviour, IGazeTarget
{
    [Header("내용")]
    public string partId = "Tongue";
    public string displayName = "혀";
    public NarrationLine line;

    [Header("동작")]
    [Tooltip("설명은 처음 한 번만 (이름표는 항상)")]
    public bool explainOnce = true;
    [Tooltip("처음 한 번만 설명하는 설정을 끈 경우, 다시 설명하기까지 최소 간격(초)")]
    public float repeatCooldown = 20f;
    [Tooltip("가이드가 다른 설명 중이면 끼어들지 않음")]
    public bool dontInterrupt = true;

    [Header("표시")]
    public GameObject labelRoot;
    public TextMeshProUGUI labelText;
    public Color highlightColor = new Color(1f, 0.9f, 0.5f);
    [Range(0f, 3f)] public float highlightStrength = 0.6f;

    /// <summary>생성된 오브젝트(적혈구, 링, 물방울 …)에 템플릿 내용을 복사해 붙인다. 콜라이더가 없으면 구 트리거를 만든다.</summary>
    public static PartLabel Attach(GameObject go, PartLabel template, float colliderRadius = 0.5f)
    {
        if (go == null || template == null) return null;
        var l = go.GetComponent<PartLabel>() ?? go.AddComponent<PartLabel>();
        l.partId = template.partId; l.displayName = template.displayName; l.line = template.line;
        l.explainOnce = template.explainOnce; l.repeatCooldown = template.repeatCooldown; l.dontInterrupt = template.dontInterrupt;
        l.highlightColor = template.highlightColor; l.highlightStrength = template.highlightStrength;
        if (go.GetComponentInChildren<Collider>() == null)
        {
            // 콜라이더 반지름은 로컬 단위이므로 렌더러 바운즈(월드)를 오브젝트 스케일로 나눠 맞춘다
            var c = go.AddComponent<SphereCollider>(); c.isTrigger = true;
            var rs = go.GetComponentsInChildren<Renderer>();
            float world = colliderRadius;
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); world = Mathf.Max(0.15f, b.extents.magnitude * 0.8f); c.center = go.transform.InverseTransformPoint(b.center); }
            float s = Mathf.Max(0.0001f, Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.y, go.transform.lossyScale.z));
            c.radius = world / s;
        }
        return l;
    }

    Renderer[] renderers;
    MaterialPropertyBlock block;
    bool explained;
    float lastExplained = -999f;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        // glTF 로 들어온 URP Lit 재질은 _EMISSION 키워드가 꺼져 있어 프로퍼티 블록의 이미션이 무시된다 → 켜 둔다
        foreach (var r in renderers) foreach (var m in r.materials) if (m != null && m.HasProperty(EmissionId)) m.EnableKeyword("_EMISSION");
        if (labelText != null) labelText.text = displayName;
        if (labelRoot != null) labelRoot.SetActive(false);
    }

    public void OnGazeEnter()
    {
        if (labelRoot != null) labelRoot.SetActive(true);
        SetHighlight(true);

        bool canExplain = explainOnce ? !explained : Time.time - lastExplained > repeatCooldown;
        if (!canExplain || GuideNarrator.Instance == null || line == null || string.IsNullOrEmpty(line.text)) return;
        if (dontInterrupt && GuideNarrator.Instance.IsSpeaking) return;

        explained = true;
        lastExplained = Time.time;
        GuideNarrator.Instance.Play(line);
        var motion = GuideNarrator.Instance.GetComponentInChildren<JourneyCharacterMotion>();
        if (motion != null) motion.Focus(transform);
    }

    public void OnGazeExit()
    {
        if (labelRoot != null) labelRoot.SetActive(false);
        SetHighlight(false);
    }

    void SetHighlight(bool on)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, on ? highlightColor * highlightStrength : Color.black);
            if (on) block.SetColor(BaseColorId, Color.Lerp(Color.white, highlightColor, 0.35f) * 1.25f); else block.Clear();
            r.SetPropertyBlock(block);
        }
    }
}
