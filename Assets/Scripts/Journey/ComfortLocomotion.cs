using System.Collections;
using UnityEngine;

/// <summary>
/// 멀미 완화 이동: 텔레포트(오른손 스틱 앞으로 밀었다 놓기) + 스냅턴(왼손 스틱 좌우 30°).
/// OVRPlayerController 의 부드러운 이동은 VR 기기에서는 끄고, PC(에디터) 테스트에서는 키보드 이동을 위해 남겨 둔다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("인체탐험/편안한 이동") ]
public class ComfortLocomotion : MonoBehaviour
{
    [Header("텔레포트")]
    public bool teleportEnabled = true;
    public float maxDistance = 8f;
    public LayerMask groundMask = ~0;
    public Color arcColor = new Color(0.4f, 0.9f, 1f, 0.9f);
    [Tooltip("텔레포트 시 페이드 시간")]
    public float fadeSeconds = 0.15f;

    [Header("스냅턴")]
    public bool snapTurnEnabled = true;
    public float snapAngle = 30f;
    public float snapDeadzone = 0.7f;

    [Header("가상현실 에서 부드러운 이동 끄기")]
    public bool disableSmoothOnDevice = true;

    OVRPlayerController ovr;
    CharacterController cc;
    Transform hand;
    LineRenderer line;
    GameObject marker;
    Vector3 target; bool hasTarget, aiming, snapReady = true;
    OVRScreenFade teleportFade;
    bool teleportFading;

    void Start()
    {
        ovr = GetComponent<OVRPlayerController>();
        cc = GetComponent<CharacterController>();
        hand = transform.Find("OVRCameraRig/TrackingSpace/RightHandAnchor");
        bool onDevice = Application.platform == RuntimePlatform.Android || OVRManager.isHmdPresent;
        if (ovr != null && disableSmoothOnDevice && onDevice)
        {
            ovr.EnableLinearMovement = false;
            ovr.EnableRotation = false; // 스냅턴은 여기서 처리
        }

        var lgo = new GameObject("TeleportArc");
        lgo.transform.SetParent(transform, false);
        line = lgo.AddComponent<LineRenderer>();
        line.positionCount = 20; line.widthMultiplier = 0.02f; line.useWorldSpace = true;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = arcColor };
        line.startColor = line.endColor = arcColor;
        line.enabled = false;

        marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "TeleportMarker";
        marker.transform.SetParent(transform, true);
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
        marker.GetComponent<Renderer>().material = line.material;
        marker.SetActive(false);
    }

    void Update()
    {
        // 캡슐 탑승/삼킴 연출 중에는 이동과 회전을 잠근다.
        if (cc == null || !cc.enabled) { CancelTeleport(); return; }
        if (snapTurnEnabled) HandleSnapTurn();
        if (teleportEnabled) HandleTeleport();
    }

    public void CancelTeleport()
    {
        StopAllCoroutines();
        if (teleportFading && teleportFade != null) teleportFade.FadeIn();
        teleportFading = false;
        aiming = false;
        hasTarget = false;
        if (line != null) line.enabled = false;
        if (marker != null) marker.SetActive(false);
    }

    void OnDisable() => CancelTeleport();

    void HandleSnapTurn()
    {
        float x = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).x; // 왼손
        if (Mathf.Abs(x) < snapDeadzone) { snapReady = true; return; }
        if (!snapReady) return;
        snapReady = false;
        transform.Rotate(0f, Mathf.Sign(x) * snapAngle, 0f, Space.World);
    }

    void HandleTeleport()
    {
        if (hand == null) return;
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick); // 오른손
        bool pushing = stick.y > 0.6f;

        if (pushing)
        {
            aiming = true;
            hasTarget = ComputeArc(out target);
            line.enabled = true;
            marker.SetActive(hasTarget);
            if (hasTarget) marker.transform.position = target + Vector3.up * 0.02f;
        }
        else if (aiming)
        {
            aiming = false;
            line.enabled = false; marker.SetActive(false);
            if (hasTarget) StartCoroutine(Blink(target));
        }
    }

    bool ComputeArc(out Vector3 hitPoint)
    {
        Vector3 p = hand.position, v = hand.forward * 8f;
        hitPoint = Vector3.zero;
        for (int i = 0; i < line.positionCount; i++)
        {
            line.SetPosition(i, p);
            Vector3 next = p + v * 0.08f; v += Physics.gravity * 0.08f;
            if (Physics.Linecast(p, next, out var hit, groundMask, QueryTriggerInteraction.Ignore) && Vector3.Angle(hit.normal, Vector3.up) < 45f
                && Vector3.Distance(transform.position, hit.point) <= maxDistance && TeleportArea.Allows(hit.collider))
            {
                hitPoint = hit.point;
                for (int j = i + 1; j < line.positionCount; j++) line.SetPosition(j, hit.point);
                return true;
            }
            p = next;
        }
        return false;
    }

    IEnumerator Blink(Vector3 dest)
    {
        var fade = FindAnyObjectByType<OVRScreenFade>();
        teleportFade = fade;
        if (fade != null) { teleportFading = true; fade.fadeTime = fadeSeconds; fade.FadeOut(); yield return new WaitForSeconds(fadeSeconds); }
        if (cc == null || !cc.enabled) { CancelTeleport(); yield break; }
        // 눈 위치가 목적지에 오도록 몸을 이동 (카메라 오프셋 보정)
        var eye = transform.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        Vector3 offset = eye != null ? new Vector3(eye.position.x - transform.position.x, 0f, eye.position.z - transform.position.z) : Vector3.zero;
        if (cc != null) cc.enabled = false;
        transform.position = dest - offset + Vector3.up * (cc != null ? cc.height * 0.5f : 1f);
        if (cc != null) cc.enabled = true;
        Haptics.Pulse(0.3f, 0.05f, left: false);
        if (fade != null) { fade.FadeIn(); }
        teleportFading = false;
    }
}
