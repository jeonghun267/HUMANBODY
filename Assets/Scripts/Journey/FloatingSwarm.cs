using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미생물/적혈구/영양소 같은 오브젝트 무리를 지정 부피 안에 뿌리고 자연스럽게 떠다니게 한다.
/// 기존 VirusGroupController(수동 등록 방식)를 프리팹 자동 생성 방식으로 확장.
/// </summary>
[AddComponentMenu("인체탐험/부유 개체 무리") ]
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

    class Member
    {
        public Transform t;
        public Vector3 home;
        public Vector3 axis;
        public float offset, speedMul, rangeMul, rotMul;
    }

    readonly List<Member> members = new List<Member>();

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
    }

    void Update()
    {
        float time = Time.time;
        Vector3 flowStep = flowVelocity * Time.deltaTime;
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
