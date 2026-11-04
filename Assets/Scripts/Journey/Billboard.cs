using UnityEngine;

/// <summary>World Space UI 등이 항상 플레이어 카메라를 향하도록 회전. NPCUI 와 달리 카메라를 자동으로 찾는다.</summary>
[AddComponentMenu("인체탐험/시선 방향 표시판") ]
public class Billboard : MonoBehaviour
{
    public float smooth = 8f;
    [Tooltip("위아래 기울임 없이 수직 축만 회전")]
    public bool yawOnly;

    Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = JourneyEye.Get();
            if (cam == null) return;
        }
        Vector3 dir = transform.position - cam.position;
        if (yawOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        var target = Quaternion.LookRotation(dir);
        transform.rotation = smooth > 0f ? Quaternion.Slerp(transform.rotation, target, Time.deltaTime * smooth) : target;
    }
}
