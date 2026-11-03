using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 상영관 마무리: 영상이 끝나면 가이드의 마무리 멘트(OrganStage.outroLines)를 재생하고 로비로 돌아간다.
/// </summary>
[AddComponentMenu("인체탐험/상영관 마무리") ]
public class TheaterEnding : MonoBehaviour
{
    public VideoPlayer player;
    [Tooltip("입장 내레이션이 끝나면 자동으로 영상 시작")]
    public bool playAfterIntro = true;
    [Tooltip("내레이션이 시작되기를 기다리는 최대 시간(초)")]
    public float introWait = 3f;
    [Tooltip("영상 종료 후 마무리 멘트까지 대기(초)")]
    public float delayAfterVideo = 1.5f;
    [Tooltip("영상이 없거나 재생 실패 시 이 시간(초) 후 자동 종료")]
    public float fallbackSeconds = 20f;

    bool ended;

    void Start()
    {
        if (player != null && player.clip != null) { player.loopPointReached += _ => End(); if (playAfterIntro) StartCoroutine(PlayAfterIntro()); }
        else StartCoroutine(Fallback());
    }

    IEnumerator PlayAfterIntro()
    {
        // 인트로가 시작될 때까지 잠깐 기다렸다가, 말이 끝나면 재생
        float t = 0f;
        while (t < introWait && (GuideNarrator.Instance == null || !GuideNarrator.Instance.IsSpeaking)) { t += Time.deltaTime; yield return null; }
        while (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) yield return null;
        yield return new WaitForSeconds(0.5f);
        if (!ended) player.Play();
    }

    IEnumerator Fallback()
    {
        yield return new WaitForSeconds(fallbackSeconds);
        End();
    }

    void End()
    {
        if (ended) return;
        ended = true;
        StartCoroutine(EndRoutine());
    }

    IEnumerator EndRoutine()
    {
        yield return new WaitForSeconds(delayAfterVideo);
        if (JourneyManager.Instance == null) yield break;
        var stage = JourneyManager.Instance.CurrentStage;
        if (GuideNarrator.Instance != null && stage != null && stage.outroLines != null && stage.outroLines.Length > 0)
            yield return GuideNarrator.Instance.PlayRoutine(stage.outroLines);
        JourneyManager.Instance.Restart();
    }
}
