using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 괄약근(분문/유문) 개폐 연출. 닫힌 상태에서 Open() 하면 링이 넓어지며 중앙이 열리고,
/// 통과를 막던 콜라이더가 꺼진다. 평소엔 살짝 맥동한다.
/// </summary>
[AddComponentMenu("인체탐험/괄약근 문") ]
public class SphincterGate : MonoBehaviour
{
    [Header("개폐")]
    [Tooltip("열렸을 때 가로와 세로 스케일 배수")]
    public float openScale = 1.6f;
    public float openSeconds = 1.5f;
    [Tooltip("열린 뒤 자동으로 닫히기까지(초). 0 이면 열린 채 유지")]
    public float autoCloseAfter = 0f;
    public Collider blocker;
    [Tooltip("문 가운데의 구멍이 실제로 넓어지는 표면입니다.")]
    public SkinnedMeshRenderer openingSurface;

    [Header("맥동")]
    public float pulseAmplitude = 0.03f;
    public float pulseSpeed = 1.5f;

    [Header("이벤트")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public bool IsOpen { get; private set; }
    Vector3 baseScale;
    float openAmount;

    void Awake() { baseScale = transform.localScale; }

    void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude * (1f - openAmount);
        float s = Mathf.Lerp(1f, openScale, openAmount) * pulse;
        if (openingSurface != null)
        {
            transform.localScale = baseScale * pulse;
            openingSurface.SetBlendShapeWeight(0, openAmount * 100f);
        }
        else transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
    }

    public void Open()
    {
        if (IsOpen) return;
        StopAllCoroutines();
        StartCoroutine(Animate(1f));
    }

    public void Close()
    {
        if (!IsOpen) return;
        StopAllCoroutines();
        StartCoroutine(Animate(0f));
    }

    IEnumerator Animate(float target)
    {
        float from = openAmount, t = 0f;
        while (t < openSeconds)
        {
            t += Time.deltaTime;
            openAmount = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / openSeconds));
            yield return null;
        }
        openAmount = target;
        IsOpen = target > 0.5f;
        if (blocker != null) blocker.enabled = !IsOpen;
        if (IsOpen) { onOpened?.Invoke(); if (autoCloseAfter > 0f) { yield return new WaitForSeconds(autoCloseAfter); Close(); } }
        else onClosed?.Invoke();
    }
}
