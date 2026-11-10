using System.Text;
using KoreanTyper;
using TMPro;
using UnityEngine;

/// <summary>
/// 로비 전시대. 플레이어가 전시물을 응시하면 정보 패널(제목/요약/알아두기)이 타이핑 효과로 열린다.
/// OrganStage 데이터를 그대로 재사용하므로 내용은 한 곳(Stage 에셋)에서만 관리한다.
/// 기존 DisplayInfo + RaycastInteractor 를 대체. 태그 "InteractableOrgan" 대신 이 컴포넌트 존재 여부로 판단.
/// </summary>
[AddComponentMenu("인체탐험/전시물 안내") ]
public class ExhibitStation : MonoBehaviour, IGazeTarget
{
    [Header("데이터")]
    public OrganStage stage;
    [Tooltip("단계 정보 대신 제목과 본문 직접 입력 (선택)")]
    public string overrideTitle;
    [TextArea(2, 5)] public string overrideBody;
    [Tooltip("처음 바라볼 때 가이드가 해 주는 한 줄 설명 (선택)")]
    public NarrationLine voiceLine;

    [Header("화면")]
    public GameObject panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public float charsPerSecond = 40f;

    [Header("하이라이트")]
    public Renderer[] highlightRenderers;
    public Color highlightColor = new Color(0.3f, 0.8f, 1f);
    [Range(0f, 3f)] public float highlightStrength = 0.8f;

    [Header("원격 사용자 표시 (원격 통신)")]
    public GameObject remoteMarker;

    public string OrganId => stage != null ? stage.stageId : gameObject.name;

    string cachedBody;
    float typeProgress;
    bool open, explained;
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    int cachedTypingLength;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (highlightRenderers != null)
            foreach (var r in highlightRenderers) if (r != null) foreach (var m in r.materials) if (m != null && m.HasProperty(EmissionId)) m.EnableKeyword("_EMISSION");
        if (panel != null) panel.SetActive(false);
        if (remoteMarker != null) remoteMarker.SetActive(false);
        cachedBody = BuildBody();
        cachedTypingLength = string.IsNullOrEmpty(cachedBody) ? 0 : cachedBody.GetTypingLength();
    }

    string BuildBody()
    {
        if (!string.IsNullOrEmpty(overrideBody)) return overrideBody;
        if (stage == null) return string.Empty;
        var sb = new StringBuilder(stage.summary);
        if (stage.funFacts != null && stage.funFacts.Length > 0)
        {
            sb.Append("\n");
            foreach (var f in stage.funFacts) if (!string.IsNullOrEmpty(f)) sb.Append("\n• ").Append(f);
        }
        return sb.ToString();
    }

    public void OnGazeEnter() => Open();
    public void OnGazeExit() => Close();

    public void Open()
    {
        if (open) return;
        open = true;
        typeProgress = 0f;
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = !string.IsNullOrEmpty(overrideTitle) ? overrideTitle : (stage != null ? stage.displayName : "전시물 안내");
        if (bodyText != null) bodyText.text = string.Empty;
        SetHighlight(true);
        if (OSCManager.Instance != null) OSCManager.Instance.SendInteractionStatus(OrganId, true);
        if (!explained && voiceLine != null && !string.IsNullOrEmpty(voiceLine.text) && GuideNarrator.Instance != null && !GuideNarrator.Instance.IsSpeaking)
        {
            explained = true;
            GuideNarrator.Instance.Play(voiceLine);
        }
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        if (panel != null) panel.SetActive(false);
        SetHighlight(false);
        if (OSCManager.Instance != null) OSCManager.Instance.SendInteractionStatus(OrganId, false);
    }

    /// <summary>다른 헤드셋 사용자가 이 전시물을 보고 있을 때 표시.</summary>
    public void SetRemoteViewing(bool viewing)
    {
        if (remoteMarker != null) remoteMarker.SetActive(viewing && !open);
    }

    void Update()
    {
        if (!open || bodyText == null || string.IsNullOrEmpty(cachedBody)) return;
        if (typeProgress >= 1f) return;
        typeProgress = Mathf.Min(1f, typeProgress + Time.deltaTime * charsPerSecond / Mathf.Max(1, cachedTypingLength));
        bodyText.text = cachedBody.Typing(typeProgress);
    }

    void SetHighlight(bool on)
    {
        if (highlightRenderers == null) return;
        foreach (var r in highlightRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, on ? highlightColor * highlightStrength : Color.black);
            if (on) block.SetColor(BaseColorId, Color.Lerp(Color.white, highlightColor, 0.35f) * 1.25f); else block.Clear();
            r.SetPropertyBlock(block);
        }
    }
}
