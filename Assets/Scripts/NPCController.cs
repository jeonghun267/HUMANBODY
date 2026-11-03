using UnityEngine;

[AddComponentMenu("인체탐험/안내 캐릭터 제어") ]
public class NPCController : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static NPCController Instance;

    [Header("안내 캐릭터 따라오기")]
    [Tooltip("안내 캐릭터가 따라갈 대상 위치 기준 (예: 가상현실 카메라)")]
    [SerializeField]
    private Transform target;

    [Tooltip("이동 속도")]
    [SerializeField]
    private float moveSpeed = 3.0f;

    [Tooltip("안내 캐릭터가 대상의 '앞'에 위치할 거리")]
    [SerializeField]
    private float forwardOffset = 1.0f;

    [Tooltip("안내 캐릭터가 대상의 '오른쪽'에 위치할 거리 (양수: 오른쪽, 음수: 왼쪽)")]
    [SerializeField]
    private float rightOffset = 0.75f;

    // ★ [추가됨] 최소 거리 유지 (이 거리 안으로 들어오면 멈춤)
    [Header("거리 제한 (너무 가깝지 않게)")]
    [Tooltip("플레이어와 이 거리보다 가까워지면 이동을 멈춥니다.")]
    [SerializeField]
    private float stopDistance = 1.5f;

    // ★ [추가됨] 자세 교정 (모델이 누워있을 때 사용)
    [Header("자세 교정 (눕는 문제 해결)")]
    [Tooltip("모델이 누워있다면 가로 축에 -90 또는 90을 입력하세요.")]
    [SerializeField]
    private Vector3 rotationOffset = new Vector3(-90, 0, 0);

    // NPC의 움직임 상태를 제어하는 변수
    private bool isMoving = true;

    // NPC가 플레이어를 바라보는지 여부를 제어하는 변수
    private bool isInteracting = false;

    void Awake()
    {
        // 싱글톤 로직
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        GameObject playerCam = GameObject.FindGameObjectWithTag("VRCamera");
        if (playerCam == null)
        {
            // VRCamera 태그를 못 찾으면 메인 카메라라도 찾음
            if (Camera.main != null)
            {
                playerCam = Camera.main.gameObject;
            }
        }

        if (playerCam != null)
        {
            SetTarget(playerCam.transform);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // 움직임 토글
    public void ToggleMovement()
    {
        isMoving = !isMoving;
        Debug.Log($"NPC Movement Toggled: {isMoving}");
    }

    // 상호작용 시작
    public void StartInteraction()
    {
        if (target != null)
        {
            isInteracting = true;
            Debug.Log("NPC: Player를 바라봅니다.");
        }
    }

    // 상호작용 종료
    public void StopInteraction()
    {
        isInteracting = false;
        Debug.Log("NPC: 원래의 추적 회전으로 돌아갑니다.");
    }

    void Update()
    {
        if (target == null || !isMoving) return;

        // =======================================================
        // 1. NPC 이동 로직
        // =======================================================

        // ★ [거리 체크 추가] 현재 플레이어와의 거리를 잽니다.
        float currentDistance = Vector3.Distance(transform.position, target.position);

        // 지정된 stopDistance보다 멀리 있을 때만 이동 계산을 합니다. (너무 가까우면 이동 안 함)
        if (currentDistance > stopDistance)
        {
            Vector3 offsetForward = target.forward * forwardOffset;
            Vector3 offsetRight = target.right * rightOffset;

            Vector3 targetPosition = target.position + offsetForward + offsetRight;

            // 목표 지점과의 미세 거리 체크 (떨림 방지)
            if (Vector3.Distance(transform.position, targetPosition) > 0.05f)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Time.deltaTime * moveSpeed
                );
            }
        }

        // =======================================================
        // 2. NPC 회전 로직
        // =======================================================

        Quaternion targetRotation;

        if (isInteracting)
        {
            // ★ A. 상호작용 모드: 플레이어를 바라봅니다.
            Vector3 lookDirection = target.position - transform.position;
            lookDirection.y = 0; // 고개 숙임 방지

            if (lookDirection != Vector3.zero)
            {
                // 기본적으로 바라보는 회전
                targetRotation = Quaternion.LookRotation(lookDirection);

                // ★ [교정 적용] 변수(rotationOffset)를 더해서 강제로 일으켜 세움
                targetRotation *= Quaternion.Euler(rotationOffset);
            }
            else
            {
                targetRotation = transform.rotation;
            }
        }
        else
        {
            // ★ B. 원래 모드: 플레이어와 같은 방향을 바라봅니다.
            Vector3 playerEuler = target.rotation.eulerAngles;

            // ★ [교정 적용] 하드코딩(-90)을 지우고 변수(rotationOffset) 사용
            targetRotation = Quaternion.Euler(
                rotationOffset.x,   // X축 교정 (눕는 문제 해결)
                playerEuler.y,      // Y축은 플레이어 따라가기
                rotationOffset.z    // Z축 교정
            );
        }

        // 회전 적용 (부드럽게)
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * moveSpeed * 5f
        );
    }
}