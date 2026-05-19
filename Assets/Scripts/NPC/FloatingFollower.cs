using UnityEngine;

public class FloatingFollower : MonoBehaviour
{
    [Header("타겟 설정")]
    public Transform player;        // CenterEyeAnchor (자동으로 찾음)

    [Header("위치 설정")]
    public float distance = 3.0f;   // 플레이어와의 거리
    public float height = -0.2f;    // 플레이어 눈 높이 기준 높낮이
    public float angleBias = 30f;   // 플레이어 시야에서 오른쪽(+) or 왼쪽(-)으로 치우칠 각도

    [Header("움직임 설정")]
    public float followSpeed = 5f;  // 반응 속도
    public float rotateSpeed = 10f;

    [Header("둥둥 뜨는 효과")]
    public float floatAmplitude = 0.5f;
    public float floatFrequency = 1f;

    [Header("회전 보정")]
    public float rotationOffset = -90f;

    // [중요] 씬이 시작될 때 플레이어를 자동으로 찾는 로직 추가
    void Start()
    {
        // 1. 만약 인스펙터에 이미 연결되어 있다면 통과
        if (player != null) return;

        // 2. 연결이 안 되어 있다면(씬 전환으로 끊김), 'Player' 태그로 찾기 시도
        GameObject foundPlayer = GameObject.FindWithTag("Player");

        if (foundPlayer != null)
        {
            // 3. OVRPlayerController 내부의 진짜 눈(Camera) 위치인 CenterEyeAnchor를 찾아야 함
            // 보통 구조: OVRPlayerController -> OVRCameraRig -> TrackingSpace -> CenterEyeAnchor
            // (Find는 자식 오브젝트 이름을 경로로 찾을 수 있음)
            Transform centerEye = foundPlayer.transform.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");

            if (centerEye != null)
            {
                player = centerEye;
            }
            else
            {
                // CenterEyeAnchor를 못 찾았다면, 아쉬운 대로 플레이어 몸통을 타겟으로 잡음
                player = foundPlayer.transform;
                Debug.LogWarning("[FloatingFollower] CenterEyeAnchor를 찾지 못해 Player 몸통을 따라갑니다.");
            }
        }
        else
        {
            Debug.LogError("[FloatingFollower] 씬에서 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다! OVRPlayerController의 태그를 확인하세요.");
        }
    }

    // Update 대신 LateUpdate 사용 (떨림 방지 핵심)
    void LateUpdate()
    {
        if (player == null) return;

        // --- 1. 목표 위치 계산 (플레이어의 '수평' 시선 기준) ---

        // 플레이어가 보는 방향에서 Y축(고개 들기/숙이기)을 제거함
        Vector3 playerLookDir = player.forward;
        playerLookDir.y = 0;
        playerLookDir.Normalize();

        // 플레이어 위치 + (방향 * 거리) 적용
        // Quaternion.Euler(0, angleBias, 0)을 곱해서 시선 정면이 아니라 살짝 옆에 위치시킴
        Vector3 targetPos = player.position + (Quaternion.Euler(0, angleBias, 0) * playerLookDir * distance);

        // 높이 설정 (플레이어 눈높이 + 설정한 높이 + 둥둥 효과)
        float finalY = player.position.y + height + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        targetPos.y = finalY;

        // 부드럽게 이동
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);


        // --- 2. 회전 (항상 플레이어 쳐다보기) ---
        Vector3 directionToPlayer = player.position - transform.position;
        directionToPlayer.y = 0; // 병아리가 고개를 쳐들거나 숙이지 않게 수평 유지

        if (directionToPlayer != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
            Quaternion correction = Quaternion.Euler(0, rotationOffset, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation * correction, Time.deltaTime * rotateSpeed);
        }
    }
}