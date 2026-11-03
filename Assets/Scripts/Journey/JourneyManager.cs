using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 소화 여정 전체 흐름(로비 → 입 → 식도 → 위 → 소장 → 대장 → 상영관)을 관리하는 싱글톤.
/// 씬 이름을 각 씬에 하드코딩하지 않고 OrganStage 순서만 보고 다음 씬으로 넘어간다.
/// 기존 SceneChanger / SceneLoader / SceneController 를 대체한다.
/// </summary>
[AddComponentMenu("인체탐험/탐험 진행 관리") ]
public class JourneyManager : MonoBehaviour
{
    public static JourneyManager Instance { get; private set; }

    [Header("여정 순서 (빌드 설정 순서와 동일하게)")]
    public List<OrganStage> stages = new List<OrganStage>();

    [Header("전환 연출")]
    public float fadeDuration = 1.0f;
    [Tooltip("씬 로드 후 입장 내레이션을 시작하기까지 대기(초)")]
    public float introDelay = 1.5f;

    public OrganStage CurrentStage { get; private set; }
    public int CurrentIndex { get; private set; } = -1;
    public bool IsTransitioning { get; private set; }
    public bool IsIntroPending { get; private set; }

    int enteredSceneHandle = -1;
    Coroutine intro;

    public event Action<OrganStage> OnStageEntered;
    public event Action<OrganStage> OnStageLeaving;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) { SceneManager.sceneLoaded -= HandleSceneLoaded; Instance = null; }
    }

    void Start()
    {
        // 에디터에서 중간 씬부터 재생해도 현재 단계를 자동으로 맞춘다.
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    /// <summary>현재 단계의 퇴장 내레이션 후 다음 단계로.</summary>
    public void GoToNext() => GoToNext(true);

    /// <summary>다음 단계로. playOutro=false 면 퇴장 내레이션을 건너뛴다 (연출이 이미 재생한 경우).</summary>
    public void GoToNext(bool playOutro)
    {
        if (IsTransitioning) return;
        int next = CurrentIndex + 1;
        if (next >= stages.Count) { Debug.Log("[Journey] 마지막 단계입니다."); return; }
        StartCoroutine(TransitionTo(next, playOutro));
    }

    public void GoTo(string stageId, bool playOutro = false)
    {
        int idx = stages.FindIndex(s => s.stageId == stageId);
        if (idx < 0) { Debug.LogError($"[Journey] 단계 없음: {stageId}"); return; }
        if (!IsTransitioning) StartCoroutine(TransitionTo(idx, playOutro));
    }

    public void Restart() => GoTo(stages[0].stageId);

    IEnumerator TransitionTo(int index, bool playOutro)
    {
        IsTransitioning = true;
        if (intro != null) StopCoroutine(intro);
        intro = null;
        IsIntroPending = false;
        OrganStage target = stages[index];

        if (CurrentStage != null)
        {
            OnStageLeaving?.Invoke(CurrentStage);
            if (playOutro && GuideNarrator.Instance != null && CurrentStage.outroLines != null && CurrentStage.outroLines.Length > 0)
                yield return GuideNarrator.Instance.PlayRoutine(CurrentStage.outroLines);
        }

        // 페이드 아웃 (OVRScreenFade 가 있으면 사용, 없으면 대기만)
        var fade = OVRScreenFade.instance != null ? OVRScreenFade.instance : FindAnyObjectByType<OVRScreenFade>();
        if (fade != null)
        {
            fade.fadeColor = target.fadeColor;
            fade.fadeTime = fadeDuration;
            fade.FadeOut();
        }
        yield return new WaitForSeconds(fadeDuration);

        CurrentIndex = index;
        CurrentStage = target;
        AsyncOperation op = SceneManager.LoadSceneAsync(target.sceneName);
        while (!op.isDone) yield return null;
        // sceneLoaded 콜백에서 페이드 인 + 인트로 재생
        IsTransitioning = false;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene != SceneManager.GetActiveScene() || enteredSceneHandle == scene.handle) return;
        enteredSceneHandle = scene.handle;
        if (intro != null) StopCoroutine(intro);
        MovePlayerToSpawn();
        SyncStageToActiveScene();
        if (CurrentStage == null) return;

        var fade = FindAnyObjectByType<OVRScreenFade>();
        if (fade != null) { fade.fadeColor = CurrentStage.fadeColor; fade.fadeTime = fadeDuration; fade.FadeIn(); }

        IsIntroPending = true;
        OnStageEntered?.Invoke(CurrentStage);
        intro = StartCoroutine(PlayIntroAfterDelay(CurrentStage));
    }

    IEnumerator PlayIntroAfterDelay(OrganStage stage)
    {
        yield return new WaitForSeconds(introDelay);
        if (CurrentStage != stage) yield break;
        if (GuideNarrator.Instance != null && stage.introLines != null && stage.introLines.Length > 0)
            yield return GuideNarrator.Instance.PlayRoutine(stage.introLines);
        IsIntroPending = false;
        intro = null;
    }

    /// <summary>씬의 SpawnPoint 로 영속 플레이어를 옮긴다 (기존 VRPlayerManager 역할).</summary>
    static void MovePlayerToSpawn()
    {
        var spawn = GameObject.Find("SpawnPoint");
        var player = GameObject.FindWithTag("Player");
        if (spawn == null || player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        if (cc != null) cc.enabled = true;
    }

    void SyncStageToActiveScene()
    {
        string active = SceneManager.GetActiveScene().name;
        int idx = stages.FindIndex(s => s.sceneName == active);
        if (idx >= 0) { CurrentIndex = idx; CurrentStage = stages[idx]; }
    }
}
