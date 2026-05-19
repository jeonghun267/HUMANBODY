using UnityEngine;

public class NPCUI : MonoBehaviour
{
    public RectTransform targetB;          // UI (World Space Canvas)
    public Camera mainCamera;              // 카메라

    public float yOffset = 1.5f;           // UI Y 오프셋
    public float moveSmoothSpeed = 5f;     // UI 이동 보간 속도
    public float rotateSmoothSpeed = 5f;   // UI 회전 보간 속도

    private Vector3 targetPos;
    private Quaternion targetRot;

    void Update()
    {
        if (targetB != null && mainCamera != null)
        {
            // UI의 위치는 고정 (아무것도 하지 않음)

            // UI가 항상 카메라(플레이어)를 바라보게 회전
            Vector3 dirToCamera = mainCamera.transform.position - targetB.position;

            if (dirToCamera != Vector3.zero)
            {
                // [수정됨] 마이너스(-)를 제거했습니다. 
                // 이제 UI의 정면이 카메라를 향하게 됩니다.
                targetRot = Quaternion.LookRotation(dirToCamera.normalized);

                targetB.rotation = Quaternion.Slerp(targetB.rotation, targetRot, Time.deltaTime * rotateSmoothSpeed);
            }
        }
    }
}