using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[AddComponentMenu("인체탐험/보트 탑승") ]
public class BoatInteraction : MonoBehaviour
{
    [Header("필수 연결 (자동으로 못 찾을 경우를 대비해 놔두지만, 비워도 됨)")]
    public GameObject player;       // OVRPlayerController
    public GameObject chickNPC;     // 병아리

    [Header("위치 설정 (이건 꼭 연결해야 함!)")]
    public Transform seatPoint;     // 플레이어 앉을 자리
    public Transform getOffPoint;   // 내릴 위치
    public Transform chickSeatPoint; // 병아리 앉을 자리

    [Header("이동 경로")]
    public CinemachineSplineCart splineCart;

    [Header("설정")]
    public float interactionDistance = 15.0f;
    public float boatSpeed = 5.0f;

    // 내부 변수
    private bool isRiding = false;
    private CharacterController playerCC;
    private OVRPlayerController ovrScript;
    private FloatingFollower chickFollower;
    private Transform originalParent;

    public bool DebugStart = false;

    void Start()
    {
        // [디버그] OVRManager 확인 (이게 없으면 VR 입력이 안 먹힘)
        if (FindObjectOfType<OVRManager>() == null)
        {
            Debug.LogError("[BoatDebug] 씬에 'OVRManager'가 없습니다! 입력이 작동하지 않을 수 있습니다.");
        }
        else
        {
            Debug.Log("[BoatDebug] OVRManager 감지됨. 입력 준비 완료.");
        }

        // 1. 플레이어 자동 찾기
        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindWithTag("Player");
            if (foundPlayer != null)
            {
                player = foundPlayer;
                Debug.Log($"[BoatDebug] 태그로 플레이어 찾음: {player.name}");
            }
            else
            {
                Debug.LogError("[BoatDebug] 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
            }
        }

        // 플레이어 컴포넌트 가져오기
        if (player != null)
        {
            playerCC = player.GetComponent<CharacterController>();
            ovrScript = player.GetComponent<OVRPlayerController>();
        }

        // 2. 병아리 자동 찾기
        if (chickNPC == null)
        {
            // 씬에 있는 병아리 스크립트를 찾아서 그 주인을 데려옴
            FloatingFollower foundChickScript = FindObjectOfType<FloatingFollower>();
            if (foundChickScript != null)
            {
                chickNPC = foundChickScript.gameObject;
                chickFollower = foundChickScript;
                Debug.Log($"[BoatDebug] 병아리 찾음: {chickNPC.name}");
            }
            else
            {
                Debug.LogWarning("[BoatDebug] 병아리(FloatingFollower)를 찾을 수 없습니다.");
            }
        }
        else
        {
            // 수동으로 연결했다면 컴포넌트만 가져옴
            chickFollower = chickNPC.GetComponent<FloatingFollower>();
        }
    }

    void Update()
    {
        if (player == null) return; // 플레이어 없으면 작동 중지

        // --- [디버그 추가] 컨트롤러 연결 상태 및 아무 버튼이나 눌리는지 확인 ---
        // 이 로그가 안 뜨면 하드웨어 연결이나 OVRManager 문제임
        if (OVRInput.GetDown(OVRInput.Button.Any))
        {
            Debug.Log("[BoatDebug] VR 컨트롤러의 무언가(Any)가 눌렸습니다!");
        }
        // -------------------------------------------------------------

        // --- 1. 입력 처리 ---
        bool ridePressed = false;

        // [VR] A버튼 또는 X버튼
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.All))
        {
            Debug.Log("[BoatDebug] VR 입력 감지됨 (Button.One)");
            ridePressed = true;
        }

        // [PC] E키
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("[BoatDebug] PC 키보드 입력 감지됨 (E Key)");
            ridePressed = true;
        }

        if (DebugStart == true)
        {
            Debug.Log("[BoatDebug] Inspector 강제 실행");
            ridePressed = true;
            DebugStart = false; // 계속 실행 방지
        }

        if (ridePressed)
        {
            // 탑승/하차 시 거리 체크 (타고 있을 땐 거리 상관없이 하차 가능)
            Debug.Log("[BoatDebug] Test01 - ridePressed 진입");

            if (isRiding)
            {
                Debug.Log("[BoatDebug] 현재 탑승 중 -> 하차 시도");
                TryGetOff();
            }
            else
            {
                // 탈 때는 거리가 가까워야 함
                float dist = Vector3.Distance(player.transform.position, transform.position);

                Debug.Log($"[BoatDebug] 현재 거리: {dist} / 설정된 거리: {interactionDistance}");

                if (dist <= interactionDistance)
                {
                    Debug.Log("[BoatDebug] Test02 - 거리 통과! 탑승 시도");
                    TryGetOn();
                }
                else
                {
                    Debug.LogWarning("[BoatDebug] 거리가 너무 멉니다!");
                }
            }
        }

        // --- 2. 배 이동 ---
        if (isRiding && splineCart != null)
        {
            // 로그가 너무 많이 뜰 수 있으니 이동 중에는 가끔 뜨게 하거나 주석 처리
            // Debug.Log("Test03 - 이동 중"); 
            splineCart.SplinePosition += boatSpeed * Time.deltaTime;
        }
    }

    // 앞에 public을 붙여야 인스펙터 창에서 보입니다!
    public void TryGetOn()
    {
        Debug.Log("[BoatDebug] GetOn() 호출됨");
        GetOn();
    }

    public void TryGetOff()
    {
        Debug.Log("[BoatDebug] GetOff() 호출됨");
        GetOff();
    }

    // --- 탑승 로직 ---
    void GetOn()
    {
        if (player == null) return;

        isRiding = true;
        originalParent = player.transform.parent;

        // 1. 물리/이동 무력화
        if (ovrScript != null) ovrScript.enabled = false;
        if (playerCC != null) playerCC.enabled = false;

        // 2. 배에 태우기 (자식화)
        player.transform.SetParent(transform);

        // 3. 자리 잡기
        if (seatPoint != null)
        {
            player.transform.position = seatPoint.position;
            player.transform.rotation = seatPoint.rotation;
        }

        // 4. 병아리 태우기
        if (chickNPC != null)
        {
            chickNPC.transform.SetParent(transform);
            if (chickFollower != null) chickFollower.enabled = false; // 따라다니기 끄기

            if (chickSeatPoint != null)
            {
                chickNPC.transform.position = chickSeatPoint.position;
                chickNPC.transform.rotation = chickSeatPoint.rotation;
            }
        }
        Debug.Log("[BoatDebug] 탑승 완료 처리 끝");
    }

    // --- 하차 로직 ---
    void GetOff()
    {
        if (player == null) return;

        isRiding = false;

        // 1. 부모 관계 끊기 (원래 부모가 있으면 거기로, 없으면 null)
        player.transform.SetParent(originalParent);

        // 2. 하차 위치로 이동
        if (getOffPoint != null)
        {
            player.transform.position = getOffPoint.position;

            // 시선 정렬
            Vector3 rot = getOffPoint.rotation.eulerAngles;
            // VR에서는 고개(X축)는 건드리지 않고 몸통(Y축)만 돌려주는 게 자연스러움
            player.transform.rotation = Quaternion.Euler(0, rot.y, 0);
        }

        // 3. 물리/이동 복구 (반드시 위치 이동 후에 켜야 함)
        if (playerCC != null) playerCC.enabled = true;
        if (ovrScript != null) ovrScript.enabled = true;

        // 4. 병아리 하차
        if (chickNPC != null)
        {
            chickNPC.transform.SetParent(null); // 다시 자유의 몸으로

            // 하차 위치 옆에 병아리 배치
            if (getOffPoint != null)
                chickNPC.transform.position = getOffPoint.position + (transform.right * 1.0f);

            // 따라다니기 다시 켜기
            if (chickFollower != null)
            {
                chickFollower.enabled = true;
                // [중요] 병아리가 다시 켜질 때 '주인'을 놓치지 않게 재설정 (안전장치)
                chickFollower.player = player.transform.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
                if (chickFollower.player == null) chickFollower.player = player.transform;
            }
        }
        Debug.Log("[BoatDebug] 하차 완료 처리 끝");
    }
}