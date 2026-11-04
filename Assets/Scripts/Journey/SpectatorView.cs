using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// 관람객 화면 (PC 에 헤드셋을 연결한 체험관용). 모니터에는 체험자 시야를 흔들림 없이 부드럽게 따라가는 화면과
/// 지금 기관·치료 진행을 보여 준다. Quest 단독 실행(안드로이드)에서는 아무것도 하지 않는다.
/// </summary>
[AddComponentMenu("인체탐험/관람객 화면")]
public class SpectatorView : MonoBehaviour
{
    public float smoothing = 4f;
    public float fieldOfView = 75f;
    public TMP_FontAsset font;
    static SpectatorView instance;
    Camera view;
    TextMeshProUGUI info;
    RectTransform panel;

    void Start()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if (Application.platform == RuntimePlatform.Android || Application.isBatchMode || (ExhibitionSession.Settings != null && !ExhibitionSession.Settings.spectatorView)) { enabled = false; return; }
        DontDestroyOnLoad(gameObject);
        XRSettings.gameViewRenderMode = GameViewRenderMode.None; // 모니터는 이 카메라가 그린다
        view = new GameObject("관람객_카메라").AddComponent<Camera>();
        view.transform.SetParent(transform, false);
        view.stereoTargetEye = StereoTargetEyeMask.None; view.targetDisplay = 0; view.depth = 50; view.fieldOfView = fieldOfView;
        view.nearClipPlane = .05f;
        var canvasGo = new GameObject("관람객_안내", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = .3f;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var bg = new GameObject("바탕", typeof(Image)).GetComponent<Image>(); bg.transform.SetParent(canvasGo.transform, false);
        bg.color = new Color(.05f, .11f, .15f, .8f);
        var rect = panel = bg.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(30, -30); rect.sizeDelta = new Vector2(560, 300);
        info = new GameObject("내용", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); info.transform.SetParent(bg.transform, false);
        var ir = info.rectTransform; ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(22, 16); ir.offsetMax = new Vector2(-22, -16);
        info.fontSize = 30; info.color = Color.white; if (font != null) info.font = font;
    }

    void LateUpdate()
    {
        if (view == null) return;
        var cam = JourneyEye.EyeCamera();
        if (cam != null && cam != view)
        {
            float k = 1 - Mathf.Exp(-smoothing * Time.deltaTime);
            view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position, cam.transform.position, k), Quaternion.Slerp(view.transform.rotation, Quaternion.LookRotation(cam.transform.forward, Vector3.up), k));
        }
        if (info == null || Time.frameCount % 15 != 0) return;
        var sb = new StringBuilder();
        var stage = JourneyManager.Instance != null ? JourneyManager.Instance.CurrentStage : null;
        sb.AppendLine($"<b><color=#7FE3FF>지금: {(stage != null ? stage.displayName : "-")}</color></b>");
        foreach (var e in JourneyScore.Entries) sb.AppendLine($"{e.label}  {e.done}/{e.total}{(e.Complete ? "  <color=#7FE3FF>완료</color>" : "")}");
        info.text = sb.ToString();
        // 판은 글 줄 수에 맞춘다 (병원에서는 한 줄뿐이라 높은 빈 판이 화면 왼쪽 위를 가렸다)
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, info.GetPreferredValues(info.text, panel.sizeDelta.x - 44, 0).y + 32);
    }
}
