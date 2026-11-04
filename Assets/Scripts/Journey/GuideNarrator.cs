using System;
using System.Collections;
using KoreanTyper;
using TMPro;
using UnityEngine;

/// <summary>
/// 병아리 의사 가이드의 내레이션 재생기. 음성(AudioSource) + 자막(TMP, 한글 타이핑 효과)을 동기화한다.
/// NPC(병아리) 오브젝트에 붙이고 DontDestroyOnLoad 로 씬을 따라다닌다.
/// 기존 DoctorController(Resources.Load 방식)를 대체.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("인체탐험/안내 음성 및 자막") ]
public class GuideNarrator : MonoBehaviour
{
    public static GuideNarrator Instance { get; private set; }

    [Header("자막 화면 (안내 캐릭터 머리 위 공간 화면)")]
    public GameObject speechBubble;
    public TextMeshProUGUI subtitleText;
    [Tooltip("모습 없이 목소리만 (안내 병아리를 보이지 않게 한 뒤): 소리가 한쪽에서 들리지 않게 머리 안에서 들린다")]
    public bool voiceOnly;
    GameObject defaultBubble; TextMeshProUGUI defaultText; // 장면 안내판에 자막 자리가 없을 때 쓰는 원래 말풍선

    [Header("타이핑 연출")]
    [Tooltip("초당 타이핑 글자 수 (자모 단위)")]
    public float charsPerSecond = 28f;
    [Tooltip("음성이 있을 때, 클립 길이에 맞춰 타이핑 속도를 자동 조절")]
    public bool fitTypingToVoice = true;
    [Tooltip("줄과 줄 사이 간격(초)")]
    public float gapBetweenLines = 0.4f;
    [Tooltip("마지막 줄 후 말풍선을 닫기까지 유지(초)")]
    public float holdAfterLast = 1.5f;

    [Header("애니메이션 (선택)")]
    public Animator animator;
    public string talkingBool = "IsTalking";

    [Header("스킵")]
    [Tooltip("오른손 위쪽 버튼으로 현재 안내를 건너뜁니다. 컴퓨터에서는 엔 키를 누릅니다.")]
    public bool allowSkip = true;

    public bool IsSpeaking { get; private set; }
    public event Action OnSpeechFinished;

    AudioSource audioSource;
    Coroutine current;
    Action completion;
    int session; // Skip/Stop 시 증가 → 외부 코루틴이 yield 중인 PlayRoutine 도 중단된다

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = voiceOnly ? 0f : 1f;
        audioSource.playOnAwake = false;
        defaultBubble = speechBubble; defaultText = subtitleText;
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    /// <summary>자막을 장면 안내판의 자막 판으로 보낸다 (안내판이 장면마다 새로 생긴다). 원래 말풍선은 숨긴다.</summary>
    public void UseSubtitles(GameObject panel, TextMeshProUGUI text)
    {
        if (panel == null || text == null) return;
        if (defaultBubble != null && defaultBubble != panel) defaultBubble.SetActive(false);
        speechBubble = panel; subtitleText = text;
        speechBubble.SetActive(IsSpeaking);
    }

    /// <summary>등록한 자막 판이 장면과 함께 사라졌으면 원래 말풍선으로 돌아간다.</summary>
    void EnsureSubtitles()
    {
        if (speechBubble == null || subtitleText == null) { speechBubble = defaultBubble; subtitleText = defaultText; }
    }

    void Update()
    {
        if (!allowSkip || !IsSpeaking) return;
        bool pressed = OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.All)
                       || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.nKey.wasPressedThisFrame);
        if (pressed) Skip();
    }

    /// <summary>현재 내레이션을 건너뛰고 '끝남' 이벤트를 발생시킨다 (연출 흐름은 그대로 이어짐).</summary>
    public void Skip()
    {
        Stop(notifyFinished: true);
    }

    /// <summary>현재 재생 중인 내레이션을 끊고 새로 시작.</summary>
    public void Play(params NarrationLine[] lines) => BeginPlayback(lines, null);

    /// <summary>이번 재생에만 연결된 완료 콜백. 이전 재생의 중단 이벤트와 섞이지 않는다.</summary>
    public void Play(NarrationLine[] lines, Action onFinished) => BeginPlayback(lines, onFinished);

    int BeginPlayback(NarrationLine[] lines, Action onFinished)
    {
        Stop(notifyFinished: true);
        int mySession = ++session;
        completion = onFinished;
        IsSpeaking = true;
        SetTalking(true);
        EnsureSubtitles();
        if (speechBubble != null) speechBubble.SetActive(true);
        var routine = StartCoroutine(RunLines(lines, mySession));
        if (session == mySession && IsSpeaking) current = routine;
        return mySession;
    }

    /// <summary>다른 코루틴에서 yield 로 끝날 때까지 기다릴 수 있는 버전.</summary>
    public IEnumerator PlayRoutine(NarrationLine[] lines)
    {
        int mySession = BeginPlayback(lines, null);
        while (session == mySession && IsSpeaking) yield return null;
    }

    IEnumerator RunLines(NarrationLine[] lines, int mySession)
    {
        if (lines == null || lines.Length == 0) { FinishInternal(); yield break; }

        foreach (var line in lines)
        {
            if (session != mySession) yield break;
            if (line == null) continue;
            SetTalking(true);
            float duration = line.holdSeconds;
            if (line.voice != null)
            {
                audioSource.clip = line.voice;
                audioSource.Play();
                duration = line.voice.length;
            }

            yield return TypeLine(line.text, duration, mySession);
            if (session != mySession) yield break;

            // 음성이 자막보다 길면 끝날 때까지 대기
            while (line.voice != null && audioSource.isPlaying && session == mySession) yield return null;
            SetTalking(false);
            float gap = 0f;
            while (gap < gapBetweenLines && session == mySession) { gap += Time.deltaTime; yield return null; }
        }

        float hold = 0f;
        while (hold < holdAfterLast && session == mySession) { hold += Time.deltaTime; yield return null; }
        if (session == mySession) FinishInternal();
    }

    IEnumerator TypeLine(string text, float duration, int mySession)
    {
        text ??= string.Empty;
        int total = subtitleText != null ? text.GetTypingLength() : 0;
        float typeTime = subtitleText == null ? 0f : (fitTypingToVoice && duration > 0 ? Mathf.Min(duration * 0.85f, total / charsPerSecond) : total / charsPerSecond);
        float t = 0f;
        while (t < typeTime && session == mySession)
        {
            t += Time.deltaTime;
            subtitleText.text = text.Typing(Mathf.Clamp01(t / typeTime));
            yield return null;
        }
        if (session != mySession) yield break;
        if (subtitleText != null) subtitleText.text = text;
        float remain = duration - typeTime;
        while (remain > 0f && session == mySession) { remain -= Time.deltaTime; yield return null; }
    }

    /// <summary>내레이션 중단. notifyFinished 가 true 면 구독자에게 '끝남'을 알려 흐름이 멈추지 않게 한다.</summary>
    public void Stop(bool notifyFinished = false)
    {
        session++;
        if (current != null) StopCoroutine(current);
        current = null;
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        if (IsSpeaking) FinishInternal(invokeEvent: notifyFinished);
    }

    void FinishInternal(bool invokeEvent = true)
    {
        var finished = completion;
        completion = null;
        IsSpeaking = false;
        SetTalking(false);
        if (speechBubble != null) speechBubble.SetActive(false);
        if (subtitleText != null) subtitleText.text = string.Empty;
        current = null;
        if (invokeEvent) { finished?.Invoke(); OnSpeechFinished?.Invoke(); }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        completion = null;
    }

    void SetTalking(bool on)
    {
        if (animator != null && !string.IsNullOrEmpty(talkingBool)) animator.SetBool(talkingBool, on);
        var motion = GetComponentInChildren<JourneyCharacterMotion>();
        if (motion != null) motion.speaking = on;
    }
}
