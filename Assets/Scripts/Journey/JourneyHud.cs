using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체험 안내판(HUD): 지금 있는 기관, 소화 경로에서의 위치, 해야 할 일, 이동 진행.
/// 잠수정 장면에서는 조종대 화면에 붙어 있고, 걷는 장면에서는 시선 아래쪽에 느리게 따라온다(머리에 고정하면 멀미가 난다).
/// 미니게임은 SetGoal / SetStatus 로 할 일과 점수를 바꾼다.
/// </summary>
public class JourneyHud : MonoBehaviour
{
    public static JourneyHud Instance { get; private set; }

    public TextMeshProUGUI title, route, goal, status;
    public RectTransform progressFill;
    [Tooltip("소화 경로 단계 점과 이름 (지나온 곳 · 지금 · 남은 곳 색이 다르다)")] public Image[] stepDots;
    public TextMeshProUGUI[] stepLabels;
    public Color passedColor = new Color(.16f, .55f, .78f), currentColor = new Color(.5f, .9f, 1f), upcomingColor = new Color(1f, 1f, 1f, .22f);
    [Tooltip("이 장면의 이름 (진행 관리자가 없을 때)")] public string stageName;
    [Tooltip("소화 경로 순서 (이름)")] public string[] routeNames = { "입", "식도", "위", "소장", "대장" };
    [TextArea(1, 3)] public string goalText;
    [Tooltip("있으면 이동 진행률을 보여 준다")] public CapsuleRide ride;

    [Header("걷는 장면: 시선 아래에 따라오기")]
    public bool followGaze;
    public float followDistance = 1.1f;
    [Tooltip("시선보다 몇 도 아래에 둘지")] public float followDown = 32f;
    public float followSmoothing = 2.5f;

    Transform eye;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        // 제목은 단계 이름(예: 병원 진료실), 경로 점은 이 장면의 짧은 이름(예: 병원)으로 맞춘다.
        string current = stageName;
        string heading = current;
        if (JourneyManager.Instance != null && JourneyManager.Instance.CurrentStage != null && !string.IsNullOrEmpty(JourneyManager.Instance.CurrentStage.displayName))
            heading = JourneyManager.Instance.CurrentStage.displayName;
        if (title != null) title.text = heading;
        if (route != null) route.text = RouteText(routeNames, current);
        ShowSteps(current);
        SetGoal(goalText);
        SetStatus(status != null ? status.text : "");
        if (followGaze) Snap();
    }

    /// <summary>"입 › 식도 › [위] › 소장 › 대장" — 지금 있는 기관만 밝게.</summary>
    public static string RouteText(string[] names, string current)
    {
        var parts = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
            parts[i] = names[i] == current ? $"<color=#7FE3FF><b>{names[i]}</b></color>" : $"<color=#8A9AA3>{names[i]}</color>";
        return string.Join("  ›  ", parts);
    }

    /// <summary>경로 단계 점: 지나온 기관은 바다빛 파랑, 지금 기관은 밝은 하늘색으로 조금 크게, 남은 기관은 흐리게.</summary>
    public void ShowSteps(string current)
    {
        int index = System.Array.IndexOf(routeNames, current);
        for (int i = 0; i < routeNames.Length; i++)
        {
            var color = i < index ? passedColor : i == index ? currentColor : upcomingColor;
            if (stepDots != null && i < stepDots.Length && stepDots[i] != null)
            {
                stepDots[i].color = color;
                stepDots[i].rectTransform.localScale = Vector3.one * (i == index ? 1.35f : 1f);
            }
            if (stepLabels != null && i < stepLabels.Length && stepLabels[i] != null)
            {
                stepLabels[i].color = i == index ? currentColor : i < index ? new Color(.75f, .85f, .9f) : new Color(1f, 1f, 1f, .4f);
                stepLabels[i].fontStyle = i == index ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    public void SetGoal(string text) { if (goal != null) goal.text = text ?? ""; }
    [Tooltip("상태 글자를 감싼 칩 — 글자가 없으면 숨긴다")] public GameObject statusChip;

    public void SetStatus(string text)
    {
        if (status != null) status.text = text ?? "";
        if (statusChip != null) statusChip.SetActive(!string.IsNullOrEmpty(text));
    }

    void LateUpdate()
    {
        // 흡수 게임처럼 상태 글자를 직접 쓰는 곳도 있으므로 매 프레임 칩 표시를 맞춘다.
        if (statusChip != null && status != null && statusChip.activeSelf == string.IsNullOrEmpty(status.text)) statusChip.SetActive(!statusChip.activeSelf);
        if (progressFill != null && ride != null && ride.cart != null)
        {
            var a = progressFill.anchorMax; a.x = Mathf.Clamp01(ride.cart.SplinePosition); progressFill.anchorMax = a;
        }
        if (followGaze && Eye() != null)
        {
            Target(out var pos, out var rot);
            float k = 1 - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k), Quaternion.Slerp(transform.rotation, rot, k));
        }
    }

    Transform Eye()
    {
        if (eye == null && Camera.main != null) eye = Camera.main.transform;
        return eye;
    }

    void Snap() { if (Eye() == null) return; Target(out var pos, out var rot); transform.SetPositionAndRotation(pos, rot); }

    void Target(out Vector3 pos, out Quaternion rot)
    {
        var flat = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f) flat = eye.up;
        var yaw = Quaternion.LookRotation(flat.normalized);
        var dir = yaw * Quaternion.Euler(followDown, 0, 0) * Vector3.forward;
        pos = eye.position + dir * followDistance;
        rot = Quaternion.LookRotation(dir);
    }
}
