using UnityEngine;

/// <summary>
/// 눈높이 맞춤: 헤드셋 없이(에디터 플레이·배치 검사) 돌리면 머리 추적 값이 0 이라 눈이 발 높이에 붙어 화면 아래 절반이 바닥으로 덮인다.
/// 그럴 때만 추적 공간을 서 있는 눈높이만큼 올리고, 헤드셋이 연결되면 되돌린다.
/// 기기에서는 바닥 기준 추적(체험자의 실제 눈높이)을 그대로 쓴다. 영속 프리팹의 플레이어에 붙인다.
/// </summary>
[AddComponentMenu("인체탐험/눈높이 맞춤")]
public class PlayerEyeHeight : MonoBehaviour
{
    [Tooltip("헤드셋 없이 돌릴 때 바닥에서 눈까지 높이(m)")]
    public float standingEyeHeight = 1.6f;

    Transform trackingSpace;
    bool raised;

    void Awake() => trackingSpace = transform.Find("OVRCameraRig/TrackingSpace");

    void LateUpdate()
    {
        bool noHeadset = !OVRManager.isHmdPresent;
        if (trackingSpace == null || noHeadset == raised) return;
        trackingSpace.localPosition += Vector3.up * (noHeadset ? standingEyeHeight : -standingEyeHeight);
        raised = noHeadset;
    }
}
