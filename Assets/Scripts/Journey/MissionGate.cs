using UnityEngine;

/// <summary>
/// 임무를 마칠 때까지 다음 진행(씹기 구역, 잠수정 탑승구 등)의 충돌 영역을 꺼 둔다.
/// 임무가 끝나면 Open(). 너무 오래 막히지 않도록 정해진 시간이 지나면 저절로 열린다.
/// </summary>
[AddComponentMenu("인체탐험/임무 관문")]
public class MissionGate : MonoBehaviour
{
    public Collider[] colliders;
    [Tooltip("이 시간(초)이 지나면 임무와 상관없이 연다. 0 이면 기다리기만 한다")] public float autoOpenAfter = 240f;
    [Tooltip("열릴 때 켤 표시(빛나는 탑승구 등)")] public GameObject openedIndicator;
    public bool IsOpen { get; private set; }

    void Awake() { foreach (var c in colliders) if (c != null) c.enabled = false; if (openedIndicator != null) openedIndicator.SetActive(false); }
    void Start() { if (autoOpenAfter > 0) Invoke(nameof(Open), autoOpenAfter); }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        CancelInvoke(nameof(Open));
        foreach (var c in colliders) if (c != null) c.enabled = true;
        if (openedIndicator != null) openedIndicator.SetActive(true);
    }
}
