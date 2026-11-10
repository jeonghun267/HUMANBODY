using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>플레이어가 구역에 들어오면 입장 설명 뒤 구역 내레이션과 연출을 진행한다.</summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("인체탐험/구역 안내") ]
public class NarrationZone : MonoBehaviour
{
    [Header("내레이션")]
    public NarrationLine[] lines;
    [Header("동작")]
    public bool oneShot = true;
    public bool advanceOnFinish;
    public bool stopOnExit;
    public string oscOrganId;
    [Header("이벤트")]
    public UnityEvent onEnter = new UnityEvent();
    public UnityEvent onFinished = new UnityEvent();

    static readonly string[] PlayerTags = { "Player", "VRCamera", "MainCamera" };
    bool consumed, active, narrationStarted;
    int playbackVersion;
    GuideNarrator narrator;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (consumed || active || !IsPlayer(other)) return;
        if (oneShot) consumed = true;
        active = true;
        onEnter?.Invoke();
        if (!string.IsNullOrEmpty(oscOrganId) && OSCManager.Instance != null)
            OSCManager.Instance.SendInteractionStatus(oscOrganId, true);
        StartCoroutine(PlayWhenReady(++playbackVersion));
    }

    IEnumerator PlayWhenReady(int version)
    {
        while (JourneyManager.Instance != null && JourneyManager.Instance.IsIntroPending)
            yield return null;
        if (version != playbackVersion) yield break;
        narrator = GuideNarrator.Instance;
        if (narrator == null) { Finish(version); yield break; }
        narrationStarted = true;
        narrator.Play(lines, () => Finish(version));
    }

    void OnTriggerExit(Collider other)
    {
        if (stopOnExit && active && IsPlayer(other)) Cancel();
    }

    void OnDisable() => Cancel();

    void Cancel()
    {
        playbackVersion++;
        StopAllCoroutines();
        if (active && narrationStarted && narrator != null) narrator.Stop();
        active = false;
        narrationStarted = false;
        narrator = null;
    }

    void Finish(int version)
    {
        if (version != playbackVersion || !active || !isActiveAndEnabled) return;
        active = false;
        narrationStarted = false;
        narrator = null;
        onFinished?.Invoke();
        if (advanceOnFinish && JourneyManager.Instance != null) JourneyManager.Instance.GoToNext();
    }

    static bool IsPlayer(Collider c)
    {
        foreach (var t in PlayerTags) if (c.CompareTag(t)) return true;
        return c.GetComponentInParent<OVRCameraRig>() != null;
    }
}
