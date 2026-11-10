using UnityEngine;

/// <summary>
/// 멀미 완화 테두리(터널링). 탐험가 몸(잠수정에 탄 플레이어)이 움직이거나, 빨라지거나, 돌 때 그만큼 시야 가장자리를 어둡게 좁힌다.
/// 예전에는 URP 후처리 비네트를 썼는데, 헤드셋 카메라는 후처리가 꺼져 있어(Quest 에서는 꺼 두는 게 맞다 — 화면 전체를 한 번 더 그린다)
/// 실제로는 보이지 않았다. 지금은 눈 앞 0.3m 에 붙은 고리 메시를 맨 마지막에 덮어 그린다(전용 셰이더 '인체탐험/멀미 완화 테두리').
/// 가만히 있을 때는 꺼진다(그리는 비용 없음).
/// </summary>
[AddComponentMenu("인체탐험/멀미 완화 화면") ]
public class ComfortVignette : MonoBehaviour
{
    [Tooltip("가만히 있을 때 세기 (0 = 끔)")]
    [Range(0f, 1f)] public float idleIntensity = 0f;
    [Tooltip("가장 빠르게 움직이거나 돌 때 세기 (가장자리 어둡기)")]
    [Range(0f, 1f)] public float movingIntensity = 0.55f;
    [Tooltip("이 속도(미터/초)에서 최대 세기")]
    public float fullSpeed = 3f;
    [Tooltip("이 가속도(미터/초²)에서 최대 세기 — 빨라지고 느려질 때가 가장 어지럽다")]
    public float fullAcceleration = 1.5f;
    [Tooltip("이 각속도(도/초)에서 최대 세기")]
    public float fullTurn = 120f;
    [Tooltip("세게 좁혔을 때 / 거의 열렸을 때 보이는 시야 반각(도)")]
    public float narrowAngle = 32f, wideAngle = 55f;
    public float smooth = 6f;
    [Tooltip("이동/회전을 측정할 대상 (플레이어 루트). 머리 회전은 자연스러운 것이므로 카메라가 아니라 몸을 본다")]
    public Transform motionSource;
    [Tooltip("외부에서 강제로 켤 때 (삼킴 연출 등)")]
    [HideInInspector] public float forced;

    const float Distance = .3f;
    Transform ring; MeshRenderer ringRenderer; Material material;
    Vector3 lastPos, lastVelocity; float lastYaw; float current;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Transform Src => motionSource != null ? motionSource : transform;

    void Start()
    {
        lastPos = Src.position; lastYaw = Src.eulerAngles.y;
        var shader = Shader.Find("인체탐험/멀미 완화 테두리");
        var template = Resources.Load<Material>("멀미완화_테두리"); // 빌드에 셰이더가 들어가도록 Resources 재질로도 둔다
        if (template != null) material = new Material(template);
        else if (shader != null) material = new Material(shader);
        if (material == null) { enabled = false; return; }
        var go = new GameObject("멀미완화_테두리");
        ring = go.transform; ring.SetParent(transform, false); ring.localPosition = new Vector3(0, 0, Distance);
        go.AddComponent<MeshFilter>().sharedMesh = RingMesh();
        ringRenderer = go.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = material; ringRenderer.enabled = false;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ringRenderer.receiveShadows = false;
    }

    /// <summary>안쪽 반지름 1(투명) → 1.6(어두움) → 4(어두움) 고리. 크기는 열린 정도에 맞춰 키운다.</summary>
    static Mesh RingMesh()
    {
        const int seg = 48; float[] radii = { 1f, 1.6f, 4f }; float[] alpha = { 0f, 1f, 1f };
        var v = new Vector3[seg * 3]; var c = new Color[seg * 3]; var t = new int[seg * 12];
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2 / seg; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int r = 0; r < 3; r++) { v[r * seg + i] = d * radii[r]; c[r * seg + i] = new Color(0, 0, 0, alpha[r]); }
            int n = (i + 1) % seg, k = i * 12;
            for (int r = 0; r < 2; r++)
            {
                int a0 = r * seg + i, a1 = r * seg + n, b0 = (r + 1) * seg + i, b1 = (r + 1) * seg + n;
                t[k + r * 6 + 0] = a0; t[k + r * 6 + 1] = b0; t[k + r * 6 + 2] = a1;
                t[k + r * 6 + 3] = a1; t[k + r * 6 + 4] = b0; t[k + r * 6 + 5] = b1;
            }
        }
        var mesh = new Mesh { name = "멀미완화_테두리" };
        mesh.vertices = v; mesh.colors = c; mesh.triangles = t;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f); // 늘 눈앞이라 잘려 나가지 않게
        return mesh;
    }

    void LateUpdate()
    {
        if (ring == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        var velocity = (Src.position - lastPos) / dt;
        float speed = velocity.magnitude, accel = (velocity - lastVelocity).magnitude / dt;
        float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, Src.eulerAngles.y)) / dt;
        lastPos = Src.position; lastVelocity = velocity; lastYaw = Src.eulerAngles.y;
        if (speed > 20f) { current = 0; return; } // 장면 전환 순간이동은 움직임이 아니다

        float k = Mathf.Max(Mathf.Max(Mathf.Clamp01(speed / fullSpeed), Mathf.Clamp01(accel / fullAcceleration)), Mathf.Max(Mathf.Clamp01(turn / fullTurn), forced));
        float target = Mathf.Lerp(idleIntensity, movingIntensity, k);
        current = Mathf.Lerp(current, target, 1 - Mathf.Exp(-smooth * dt));

        bool show = current > .01f;
        if (ringRenderer.enabled != show) ringRenderer.enabled = show;
        if (!show) return;
        float half = Mathf.Lerp(wideAngle, narrowAngle, k) * Mathf.Deg2Rad;
        ring.localScale = Vector3.one * (Distance * Mathf.Tan(half));
        material.SetColor(ColorId, new Color(0, 0, 0, current));
    }

    void OnDestroy() { if (material != null) Destroy(material); }
}
