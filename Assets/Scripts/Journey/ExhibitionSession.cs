using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 체험관 운영: 설정 파일(StreamingAssets/exhibition.json)을 읽어 한 회차를 관리한다.
/// - 시작 대기 화면: 방아쇠를 당길 때까지 안내를 시작하지 않는다.
/// - 회차 시간: 정해진 시간이 지나면 마무리 진료실로 보낸다.
/// - 자동 초기화: 일정 시간 아무 움직임이 없으면(헤드셋을 벗음) 처음 병원으로 돌아간다.
/// - 편의: 앉아서 하기(눈높이 올림), 왼손잡이(왼손 조준), 자막 크기.
/// 병원 장면에 하나 두면 처음 한 번 만들어져 장면이 바뀌어도 남는다.
/// </summary>
[AddComponentMenu("인체탐험/체험관 운영")]
public class ExhibitionSession : MonoBehaviour
{
    [Serializable]
    public class Config
    {
        public bool attractMode = true;
        public float sessionMinutes = 15f;
        public float idleResetSeconds = 120f;
        public bool seated;
        public float seatedEyeRaise = .45f;
        public bool leftHanded;
        public float subtitleScale = 1f;
        public bool spectatorView = true;
    }

    public static ExhibitionSession Instance { get; private set; }
    public static Config Settings { get; private set; } = new Config();
    public GameObject attractPanel;

    float sessionStart = -1, lastActive;
    Vector3 lastHeadPos; Quaternion lastHeadRot;
    bool sentToEnding, seatedApplied;

    void Awake()
    {
        if (Instance != null && Instance != this) { if (attractPanel != null) attractPanel.SetActive(Settings.attractMode); Destroy(this); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        Load();
        if (Application.isBatchMode) Settings.attractMode = false; // 자동 검사 중에는 대기 화면·시간 제한을 쓰지 않는다
        JourneyAim.LeftHanded = Settings.leftHanded;
        JourneyManager.HoldIntro = Settings.attractMode;
        if (attractPanel != null) { attractPanel.transform.SetParent(null, true); attractPanel.SetActive(Settings.attractMode); }
        lastActive = Time.time;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => OnScene();
    }

    static void Load()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "exhibition.json");
            if (File.Exists(path)) Settings = JsonUtility.FromJson<Config>(File.ReadAllText(path)) ?? new Config();
        }
        catch (Exception e) { Debug.LogWarning("[체험관] 설정 파일을 읽지 못해 기본값을 씁니다: " + e.Message); }
    }

    void OnScene()
    {
        var stage = JourneyManager.Instance != null ? JourneyManager.Instance.CurrentStage : null;
        if (stage != null && stage.stageId == "Lobby")
        {
            // 새 회차
            sessionStart = -1; sentToEnding = false;
            JourneyManager.HoldIntro = Settings.attractMode;
            var panel = GameObject.Find("체험_시작_대기");
            if (panel != null) { attractPanel = panel; panel.SetActive(Settings.attractMode); }
        }
        else
        {
            JourneyManager.HoldIntro = false; // 병원을 떠나면 안내를 막지 않는다
            if (sessionStart < 0) sessionStart = Time.time;
        }
        if (GuideNarrator.Instance != null && GuideNarrator.Instance.subtitleText != null && Settings.subtitleScale > 0)
            GuideNarrator.Instance.subtitleText.transform.localScale = Vector3.one * Settings.subtitleScale;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam != null && !seatedApplied && Settings.seated)
        {
            // 앉은 체험자: 카메라 장치를 그만큼 올려 서 있는 눈높이로 맞춘다
            var rig = cam.transform.parent != null ? cam.transform.parent : null; // TrackingSpace
            if (rig != null) { rig.localPosition += Vector3.up * Settings.seatedEyeRaise; seatedApplied = true; }
        }
        // 시작 대기: 방아쇠(또는 스페이스·클릭)로 시작
        if (JourneyManager.HoldIntro && (JourneyAim.TriggerDown() || OVRInput.GetDown(OVRInput.Button.One)))
        {
            JourneyManager.HoldIntro = false;
            if (attractPanel != null) attractPanel.SetActive(false);
            Sfx.Play("success", cam != null ? cam.transform.position : Vector3.zero, .6f);
            lastActive = Time.time;
        }
        // 움직임 감지 (헤드셋을 쓰고 있으면 머리는 늘 조금씩 움직인다)
        if (cam != null)
        {
            var t = cam.transform;
            if ((t.position - lastHeadPos).sqrMagnitude > .0004f || Quaternion.Angle(t.rotation, lastHeadRot) > 1.5f || AnyInput()) lastActive = Time.time;
            lastHeadPos = t.position; lastHeadRot = t.rotation;
        }
        var manager = JourneyManager.Instance;
        if (manager == null || manager.IsTransitioning || Application.isBatchMode) return;
        string id = manager.CurrentStage != null ? manager.CurrentStage.stageId : "";
        if (Settings.idleResetSeconds > 0 && id != "Lobby" && Time.time - lastActive > Settings.idleResetSeconds)
        {
            lastActive = Time.time;
            manager.Restart();
            return;
        }
        if (Settings.sessionMinutes > 0 && sessionStart > 0 && !sentToEnding && id != "Theater" && id != "Lobby" && Time.time - sessionStart > Settings.sessionMinutes * 60f)
        {
            sentToEnding = true;
            manager.GoTo("Theater");
        }
    }

    static bool AnyInput() =>
        OVRInput.Get(OVRInput.Button.Any) || (Keyboard.current != null && Keyboard.current.anyKey.isPressed) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
}
