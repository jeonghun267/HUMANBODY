using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 헤드셋 없이(에디터 플레이·배치 검사) 돌릴 때의 눈:
/// - 눈높이: 머리 추적 값이 0 이라 눈이 발 높이에 붙어 화면 아래 절반이 바닥으로 덮인다 → 추적 공간을 서 있는 눈높이만큼 올리고, 헤드셋이 연결되면 되돌린다.
/// - 둘러보기: 마우스 오른쪽 단추를 누른 채 끌면 좌우(몸)·위아래(고개)로 본다. 걷기는 WASD(OVRPlayerController).
/// 기기에서는 바닥 기준 추적(체험자의 실제 눈높이)을 그대로 쓴다. 영속 프리팹의 플레이어에 붙인다.
/// </summary>
[AddComponentMenu("인체탐험/눈높이 맞춤")]
public class PlayerEyeHeight : MonoBehaviour
{
    [Tooltip("헤드셋 없이 돌릴 때 바닥에서 눈까지 높이(m)")]
    public float standingEyeHeight = 1.6f;
    [Tooltip("헤드셋 없을 때 마우스로 둘러보기 감도 (°/픽셀)")]
    public float mouseLook = .15f;

    Transform trackingSpace;
    bool raised;
    float pitch;

    void Awake() => trackingSpace = transform.Find("OVRCameraRig/TrackingSpace");

    void LateUpdate()
    {
        bool noHeadset = !OVRManager.isHmdPresent;
        if (trackingSpace != null && noHeadset != raised)
        {
            trackingSpace.localPosition += Vector3.up * (noHeadset ? standingEyeHeight : -standingEyeHeight);
            raised = noHeadset;
            if (!noHeadset) { pitch = 0; trackingSpace.localRotation = Quaternion.identity; }
        }
        if (!noHeadset || trackingSpace == null || Application.isBatchMode) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed) return;
        var delta = mouse.delta.ReadValue() * mouseLook;
        transform.Rotate(0f, delta.x, 0f, Space.World);
        // 헤드셋이 없으면 눈은 추적 공간의 원점(올린 높이)에 있으므로 추적 공간을 숙이면 눈 자리에서 고개를 끄덕인다
        pitch = Mathf.Clamp(pitch - delta.y, -70f, 70f);
        trackingSpace.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
