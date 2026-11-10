using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배경 음악 (STILL 처럼): 병원 진료실은 밝고 차분한 곡, 몸속은 신비롭고 잔잔한 곡 (Resources/Sfx/bgm_*, Tools/make_sfx.py 합성 — 라이선스 걱정 없음).
/// 장면이 바뀌면 두 곡을 천천히 엇갈려 바꾸고, 안내 음성·사람 대사가 나오거나 잠수정으로 이동하는 동안은 음악을 줄인다(덕킹).
/// 영속 프리팹(장면이 바뀌어도 남는 쪽)에 하나 둔다.
/// </summary>
[AddComponentMenu("인체탐험/배경 음악")]
public class JourneyMusic : MonoBehaviour
{
    public static JourneyMusic Instance { get; private set; }

    public string clinicTrack = "bgm_clinic", bodyTrack = "bgm_body";
    [Tooltip("병원 곡을 트는 장면 — 나머지(몸속)는 몸속 곡")] public string[] clinicScenes = { "00_Lobby", "06_Theater" };
    [Range(0f, 1f)] public float clinicVolume = .28f, bodyVolume = .2f;
    [Tooltip("말소리가 나오는 동안 음악 크기 (배)")] [Range(0f, 1f)] public float duckTo = .35f;
    [Tooltip("곡이 바뀔 때 엇갈려 바뀌는 시간 (초)")] public float fadeSeconds = 2.5f;
    [Tooltip("말소리에 맞춰 줄이고 되살리는 시간 (초)")] public float duckSeconds = .4f;

    readonly AudioSource[] decks = new AudioSource[2];
    readonly float[] gain = new float[2];
    readonly float[] trackVolume = new float[2];
    int live;
    float duck = 1f;
    HospitalNpc[] npcs = new HospitalNpc[0];

    public string CurrentTrack { get; private set; }
    public AudioClip CurrentClip => decks[live] != null ? decks[live].clip : null;
    /// <summary>말소리·이동 때문에 줄어든 상태인지 (검사용).</summary>
    public bool Ducked { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        for (int i = 0; i < decks.Length; i++)
        {
            var src = decks[i] = gameObject.AddComponent<AudioSource>();
            src.loop = true; src.playOnAwake = false; src.spatialBlend = 0f; src.volume = 0f; src.priority = 16;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void Start() => Pick(SceneManager.GetActiveScene());

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == SceneManager.GetActiveScene()) Pick(scene);
    }

    void Pick(Scene scene)
    {
        npcs = FindObjectsByType<HospitalNpc>(FindObjectsSortMode.None);
        bool clinic = Array.IndexOf(clinicScenes, scene.name) >= 0;
        string track = clinic ? clinicTrack : bodyTrack;
        if (track == CurrentTrack) return;
        CurrentTrack = track;
        live = 1 - live;
        var deck = decks[live];
        deck.clip = Sfx.Get(track);
        trackVolume[live] = clinic ? clinicVolume : bodyVolume;
        gain[live] = 0f;
        deck.volume = 0f;
        deck.Play();
    }

    void Update()
    {
        bool talking = GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking;
        foreach (var npc in npcs) if (npc != null && npc.IsTalking) { talking = true; break; }
        bool travelling = JourneyManager.Instance != null && JourneyManager.Instance.IsTransitioning;
        Ducked = talking || travelling;
        float dt = Time.unscaledDeltaTime;
        duck = Mathf.MoveTowards(duck, Ducked ? duckTo : 1f, dt / Mathf.Max(.01f, duckSeconds));
        for (int i = 0; i < decks.Length; i++)
        {
            var deck = decks[i];
            if (deck == null || deck.clip == null) continue;
            gain[i] = Mathf.MoveTowards(gain[i], i == live ? 1f : 0f, dt / Mathf.Max(.01f, fadeSeconds));
            deck.volume = gain[i] * trackVolume[i] * duck;
            if (i != live && gain[i] <= 0f && deck.isPlaying) deck.Stop();
        }
    }
}
