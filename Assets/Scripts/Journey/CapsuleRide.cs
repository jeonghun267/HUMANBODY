using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 탐험 캡슐(보트) 탑승. 스플라인을 따라 자동 이동하며 지정 지점에서 잠시 멈춰 내레이션을 들려준다.
/// 기존 BoatInteraction + SimpleTrainMover 를 통합하고, 정차 지점(RideStop) 개념을 추가.
/// </summary>
[AddComponentMenu("인체탐험/탐험 캡슐") ]
public class CapsuleRide : MonoBehaviour
{
    [System.Serializable]
    public class RideStop
    {
        [Tooltip("스플라인 상 위치 (0~1 정규화)")]
        [Range(0f, 1f)] public float normalizedPosition;
        public NarrationLine[] lines;
        [Tooltip("내레이션이 없을 때 정차 시간(초)")]
        public float waitSeconds = 3f;
        [Tooltip("정차 지점 도착 시 (괄약근 열기 등)")]
        public UnityEvent onReached = new UnityEvent();
        [Tooltip("안내가 끝나도 Release() 가 불릴 때까지 기다린다 (예: 유문이 열릴 때까지, 세균을 다 없앨 때까지)")]
        public bool holdUntilReleased;
        [Tooltip("Release() 가 오지 않아도 이 시간이 지나면 출발한다 (진행이 막히지 않게)")]
        public float maxHoldSeconds = 90f;
        [Tooltip("출발에 필요한 Release() 횟수 (예: 유문 열림 + 세균 퇴치 = 2)")]
        [Min(1)] public int releasesNeeded = 1;
    }

    [Header("경로")]
    public CinemachineSplineCart cart;
    [Tooltip("초당 진행량 (0~1 정규화 기준)")]
    public float speed = 0.03f;
    [Tooltip("출발/정지 가감속 시간(초) - 급가속은 멀미 원인")]
    public float easeSeconds = 1.2f;
    public RideStop[] stops;

    [Header("탑승 위치")]
    public Transform seatPoint;
    public Transform guideSeatPoint;
    public Transform getOffPoint;

    [Header("탑승 조건")]
    [Tooltip("이 거리 안에서 오른손 아래쪽 또는 왼손 아래쪽 버튼을 눌러 탑승합니다. 컴퓨터에서는 이 키를 누릅니다.")]
    public float interactionDistance = 3f;
    [Tooltip("최소 대기 시간과 입장 안내가 끝나면 자동 탑승")]
    public bool autoBoard;
    public float autoBoardDelay = 2f;

    [Header("멀미 완화")]
    [Tooltip("스플라인 기울기(피치/롤)를 따라 캡슐이 기울지 않도록 항상 수평(요 회전만) 유지")]
    public bool keepUpright = true;
    [Tooltip("잠수정이 방향을 바꾸는 최대 속도(도/초). 빠르게 돌면 멀미가 난다")]
    public float maxYawSpeed = 18f;

    [Header("도착 처리")]
    [Tooltip("경로 끝에 도착하면 다음 단계로 이동")]
    public bool advanceOnArrive = true;
    [Tooltip("도착 시 자동 하차")]
    public bool getOffOnArrive;

    public bool IsRiding { get; private set; }

    GameObject player;
    CharacterController playerCC;
    OVRPlayerController ovrController;
    Transform playerOriginalParent;
    FloatingFollower guideFollower;
    Transform guide;
    Transform guideOriginalParent;
    bool moving, arrived;
    int nextStop;
    float currentSpeed, lastYaw;
    bool controllerWasEnabled, movementWasEnabled, followerWasEnabled;
    int releases;
    ComfortLocomotion locomotion;

    /// <summary>holdUntilReleased 정차에서 다시 출발시킨다. 정차 전에 불려도 기억해 두었다가 바로 지나간다.</summary>
    public void Release() => releases++;

    void Start()
    {
        if (cart != null) { cart.AutomaticDolly.Enabled = false; cart.PositionUnits = UnityEngine.Splines.PathIndexUnit.Normalized; cart.SplinePosition = 0f; cart.UpdateMethod = CinemachineSplineCart.UpdateMethods.Update; }
        lastYaw = transform.eulerAngles.y;
        player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerCC = player.GetComponent<CharacterController>();
            ovrController = player.GetComponent<OVRPlayerController>();
            locomotion = player.GetComponent<ComfortLocomotion>();
        }
        if (GuideNarrator.Instance != null)
        {
            guide = GuideNarrator.Instance.transform;
            guideFollower = guide.GetComponent<FloatingFollower>();
        }
        // 씬이 바뀌기 전에 반드시 하차: DontDestroyOnLoad 인 플레이어/가이드가 씬 오브젝트(좌석) 밑에 있으면 같이 파괴된다.
        if (JourneyManager.Instance != null) JourneyManager.Instance.OnStageLeaving += HandleStageLeaving;
        if (autoBoard) StartCoroutine(AutoBoard());
    }

    void OnDestroy()
    {
        if (JourneyManager.Instance != null) JourneyManager.Instance.OnStageLeaving -= HandleStageLeaving;
    }

    void HandleStageLeaving(OrganStage _) => GetOff();

    IEnumerator AutoBoard()
    {
        yield return new WaitForSeconds(autoBoardDelay);
        // 탐험가의 입장 안내가 끝난 뒤 출발해 정차 지점 안내가 도입 대사를 끊지 않게 한다.
        while (JourneyManager.Instance != null &&
               (JourneyManager.Instance.IsIntroPending || JourneyManager.Instance.IsTransitioning))
            yield return null;
        Board();
    }

    void Update()
    {
        if (player == null) return;

        // 자동 탑승 라이드에서는 수동 승하차를 막는다 (A 버튼은 다른 상호작용과 겹치고, 관 한가운데서 내리면 진행이 끊긴다)
        bool pressed = !autoBoard && (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.All)
                       || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame));
        if (pressed)
        {
            if (IsRiding) GetOff();
            else if (Vector3.Distance(player.transform.position, transform.position) <= interactionDistance) Board();
        }

        if (cart != null && (moving || currentSpeed > 0.0001f))
        {
            // 목표 속도로 부드럽게 가감속
            float target = moving ? speed : 0f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, speed / Mathf.Max(0.05f, easeSeconds) * Time.deltaTime);
            cart.SplinePosition = Mathf.Min(1f, cart.SplinePosition + currentSpeed * Time.deltaTime);
            // 정차 판정은 실제로 주행 중일 때만 (감속 중에 다음 정차를 겹쳐 시작하지 않도록)
            if (moving && stops != null && nextStop < stops.Length && cart.SplinePosition >= stops[nextStop].normalizedPosition)
                StartCoroutine(PauseAtStop(stops[nextStop++]));
            else if (moving && !arrived && cart.SplinePosition >= 1f) Arrive();
        }
    }

    void LateUpdate()
    {
        if (!keepUpright) return;
        // 스플라인 카트가 진행 방향으로 기울여 놓은 회전을 요(yaw)만 남기고 되돌린다 — 수직 하강(식도)에서도 플레이어는 늘 똑바로 선다
        Vector3 f = transform.forward; f.y = 0f;
        // 멀미 완화: 굽은 길에서 잠수정이 홱 돌지 않도록 초당 maxYawSpeed 도까지만 천천히 돈다
        if (f.sqrMagnitude > 0.01f) lastYaw = Mathf.MoveTowardsAngle(lastYaw, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, maxYawSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, lastYaw, 0f);
    }

    public void Board()
    {
        if (IsRiding || player == null) return;
        IsRiding = true;

        controllerWasEnabled = playerCC != null && playerCC.enabled;
        movementWasEnabled = ovrController != null && ovrController.enabled;
        if (playerCC != null) playerCC.enabled = false;
        if (ovrController != null) ovrController.enabled = false;
        if (locomotion != null) locomotion.CancelTeleport();
        playerOriginalParent = player.transform.parent;
        player.transform.SetParent(seatPoint != null ? seatPoint : transform, false);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;

        if (guide != null)
        {
            followerWasEnabled = guideFollower != null && guideFollower.enabled;
            if (guideFollower != null) guideFollower.enabled = false;
            guideOriginalParent = guide.parent;
            guide.SetParent(guideSeatPoint != null ? guideSeatPoint : transform, false);
            guide.localPosition = Vector3.zero;
            guide.localRotation = Quaternion.identity;
        }

        nextStop = 0;
        arrived = false;
        moving = true;
        Sfx.Play("whoosh", transform.position, 0.5f, 0.8f);
        Haptics.Pulse(0.4f, 0.2f);
    }

    public void GetOff()
    {
        if (!IsRiding) return;
        IsRiding = false;
        moving = false;

        player.transform.SetParent(playerOriginalParent, true);
        if (getOffPoint != null) { player.transform.position = getOffPoint.position; player.transform.rotation = getOffPoint.rotation; }
        if (playerCC != null) playerCC.enabled = controllerWasEnabled;
        if (ovrController != null) ovrController.enabled = movementWasEnabled;

        if (guide != null)
        {
            guide.SetParent(guideOriginalParent, true);
            if (guideFollower != null) guideFollower.enabled = followerWasEnabled;
        }
    }

    IEnumerator PauseAtStop(RideStop stop)
    {
        moving = false;
        stop.onReached?.Invoke();
        if (GuideNarrator.Instance != null && stop.lines != null && stop.lines.Length > 0)
            yield return GuideNarrator.Instance.PlayRoutine(stop.lines);
        else
            yield return new WaitForSeconds(stop.waitSeconds);
        if (stop.holdUntilReleased)
        {
            float until = Time.time + stop.maxHoldSeconds;
            while (releases < stop.releasesNeeded && Time.time < until) yield return null;
            releases = Mathf.Max(0, releases - stop.releasesNeeded);
        }
        if (IsRiding) moving = true;
    }

    void Arrive()
    {
        arrived = true;
        moving = false;
        if (getOffOnArrive || advanceOnArrive) GetOff();
        if (advanceOnArrive && JourneyManager.Instance != null) JourneyManager.Instance.GoToNext();
    }
}
