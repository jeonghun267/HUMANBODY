using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미생물/적혈구/영양소 같은 오브젝트 무리를 지정 부피 안에 뿌리고 자연스럽게 떠다니게 한다.
/// 기존 VirusGroupController(수동 등록 방식)를 프리팹 자동 생성 방식으로 확장.
/// </summary>
[AddComponentMenu("인체탐험/부유 개체 무리") ]
[DefaultExecutionOrder(100)] // 잠수정(CapsuleRide)이 LateUpdate 에서 돈 뒤의 눈 자리로 비켜 간다
public class FloatingSwarm : MonoBehaviour
{
    [Header("생성")]
    public GameObject[] prefabs;
    [Min(0)] public int count = 20;
    [Tooltip("이 오브젝트 기준 로컬 부피 (상자)")]
    public Vector3 spawnExtents = new Vector3(3, 2, 3);
    public Vector2 scaleRange = new Vector2(0.8f, 1.3f);
    [Tooltip("씬에 이미 배치된 자식들도 무리에 포함")]
    public bool includeExistingChildren = true;
    [Tooltip("각 개체에 붙일 이름표/설명 (선택)")]
    public PartLabel labelTemplate;

    [Header("움직임")]
    public float driftRange = 0.3f;
    public float driftSpeed = 0.6f;
    public float rotateSpeed = 15f;
    [Tooltip("전체 무리가 흐르는 방향/속도 (장 내용물 흐름 등). 0 이면 제자리 부유")]
    public Vector3 flowVelocity = Vector3.zero;
    [Tooltip("흐름에 따라 생성 영역을 벗어나면 반대편에서 다시 등장")]
    public bool wrapAround = true;
    [Tooltip("탐험가 눈(주 카메라)에서 이 거리 안으로는 들어오지 않는다 (잠수정 유리 안·눈앞을 지나가면 어지럽다). 0 이면 끔")]
    public float keepOutRadius = 1.2f;

    class Member
    {
        public Transform t;
        public Vector3 home;
        public Vector3 axis;
        public float offset, speedMul, rangeMul, rotMul;
        public Vector3 centre; public float radius; // 모형 가운데(원점 기준)와 감싸는 공 반지름 — 긴 식이섬유는 끝이 멀리 뻗는다
    }

    readonly List<Member> members = new List<Member>();
    Transform eye;
    float nextEyeSearch;

    void Start()
    {
        if (includeExistingChildren)
            foreach (Transform c in transform) Register(c);

        for (int i = 0; i < count && prefabs != null && prefabs.Length > 0; i++)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;
            var go = Instantiate(prefab, transform);
            go.transform.localPosition = RandomInExtents();
            go.transform.localRotation = Random.rotation;
            go.transform.localScale = prefab.transform.localScale * Random.Range(scaleRange.x, scaleRange.y);
            if (labelTemplate != null) PartLabel.Attach(go, labelTemplate, 0.6f);
            Register(go.transform);
        }
    }

    void Register(Transform t)
    {
        members.Add(new Member
        {
            t = t, home = t.localPosition,
            axis = Random.onUnitSphere,
            offset = Random.Range(0f, 100f),
            speedMul = Random.Range(0.7f, 1.3f),
            rangeMul = Random.Range(0.6f, 1.4f),
            rotMul = Random.Range(-1.5f, 1.5f),
        });
        Measure(members[members.Count - 1]);
    }

    /// <summary>모형 크기를 메시로 잰다 (움직이는 캐릭터의 renderer.bounds 는 부모 크기가 걸리면 틀린다).</summary>
    static void Measure(Member m)
    {
        bool any = false; Bounds b = default;
        foreach (var r in m.t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            var mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            var mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = m.t.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(local, Vector3.zero); any = true; } else b.Encapsulate(local);
            }
        }
        m.centre = any ? b.center : Vector3.zero;
        m.radius = any ? Vector3.Scale(b.extents, Abs(m.t.lossyScale)).magnitude : 0f;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    void Update()
    {
        float time = Time.time;
        Vector3 flowStep = flowVelocity * Time.deltaTime;
        if (keepOutRadius > 0 && (eye == null || !eye.gameObject.activeInHierarchy) && time >= nextEyeSearch)
        {
            nextEyeSearch = time + 1f;
            eye = JourneyEye.Get(); // 탐험가 눈(CenterEyeAnchor)
        }
        foreach (var m in members)
        {
            if (m.t == null || m.t.parent != transform) continue; // 떼어낸 멤버(흡수 중 등)는 건너뜀
            if (flowStep != Vector3.zero)
            {
                m.home += flowStep;
                if (wrapAround) m.home = Wrap(m.home);
            }
            float tt = time * driftSpeed * m.speedMul + m.offset;
            float r = driftRange * m.rangeMul;
            m.t.localPosition = m.home + new Vector3(Mathf.Sin(tt) * r, Mathf.Cos(tt * 0.85f) * r, Mathf.Sin(tt * 1.15f) * r * 0.5f);
            m.t.Rotate(m.axis, rotateSpeed * m.rotMul * Time.deltaTime, Space.Self);
        }
        KeepOut(); // 떠다니는 자리로 옮긴 직후에도 밀어내 둔다(한 프레임도 눈 안쪽에 있지 않게)
    }

    // 잠수정이 Update 에서 움직이므로, 이번 프레임에 옮겨진 눈 자리로 LateUpdate 에서 한 번 더 민다.
    void LateUpdate() => KeepOut();

    /// <summary>눈 둘레 공 밖으로 밀어낸다 (공 표면을 따라 미끄러지듯 비켜 간다). 모형 끝까지 공 밖에 있게 모형 반지름만큼 더 멀리.</summary>
    void KeepOut()
    {
        if (eye == null || keepOutRadius <= 0) return;
        foreach (var m in members)
        {
            if (m.t == null || m.t.parent != transform) continue;
            var centre = m.t.TransformPoint(m.centre);
            var away = centre - eye.position;
            float d = away.magnitude, keep = keepOutRadius + m.radius;
            if (d < keep) m.t.position += (d > 1e-3f ? away / d : Vector3.up) * (keep - d);
        }
    }

    Vector3 RandomInExtents() => new Vector3(
        Random.Range(-spawnExtents.x, spawnExtents.x),
        Random.Range(-spawnExtents.y, spawnExtents.y),
        Random.Range(-spawnExtents.z, spawnExtents.z));

    Vector3 Wrap(Vector3 p)
    {
        for (int i = 0; i < 3; i++)
        {
            float e = spawnExtents[i];
            if (e <= 0f) continue;
            if (p[i] > e) p[i] = -e; else if (p[i] < -e) p[i] = e;
        }
        return p;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, spawnExtents * 2f);
    }
}
