using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연동파 연출: 근육 링(프리팹)이 시작점→끝점으로 차례로 이동하며 조여졌다 풀리는 파동을 만든다.
/// 식도(위→아래), 소장/대장(앞→뒤) 터널에 사용. 링이 지나갈 때 터널 벽 PeristalsisPulse 와 위상을 맞추면 더 자연스럽다.
/// </summary>
[AddComponentMenu("인체탐험/연동 운동") ]
public class PeristalsisWave : MonoBehaviour
{
    [Header("링")]
    public GameObject ringPrefab;
    [Min(1)] public int ringCount = 3;
    public float ringScale = 1f;
    [Tooltip("0보다 크면 고리의 바깥 지름을 이 값(미터)에 맞춥니다. 고리 크기 설정보다 우선합니다.")]
    public float ringDiameter = 0f;
    [Tooltip("조여질 때 링이 작아지는 비율")]
    [Range(0.3f, 1f)] public float squeezeScale = 0.8f;

    [Header("경로 (로컬)")]
    public Vector3 startLocal = new Vector3(0, 0, 0);
    public Vector3 endLocal = new Vector3(0, -30, 0);
    [Tooltip("한 링이 끝까지 가는 데 걸리는 시간(초)")]
    public float travelSeconds = 8f;
    [Tooltip("링 진행 방향과 나란한 축으로 회전시킬 오일러")]
    public Vector3 ringEuler = Vector3.zero;

    [Header("파츠 설명 (선택)")]
    public PartLabel labelTemplate;

    readonly List<Transform> rings = new List<Transform>();
    readonly List<float> lastT = new List<float>();
    Transform player;

    void Start()
    {
        if (ringPrefab == null) return;
        for (int i = 0; i < ringCount; i++)
        {
            var r = Instantiate(ringPrefab, transform).transform;
            r.localRotation = Quaternion.Euler(ringEuler);
            r.name = $"MuscleRing_{i}";
            if (ringDiameter > 0f && i == 0)
            {
                var rs = r.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
                    float d = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (d > 0.001f) ringScale = ringDiameter / d * r.localScale.x;
                }
            }
            if (labelTemplate != null) PartLabel.Attach(r.gameObject, labelTemplate, 0.8f);
            rings.Add(r);
            lastT.Add(0f);
        }
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            float t = Mathf.Repeat(Time.time / travelSeconds + (float)i / rings.Count, 1f);
            rings[i].localPosition = Vector3.Lerp(startLocal, endLocal, t);
            // 플레이어 위치를 통과하는 순간 (진행도 기준) 효과음 + 햅틱
            if (player != null)
            {
                float axisLen = (endLocal - startLocal).magnitude;
                float playerT = axisLen > 0.01f ? Vector3.Dot(transform.InverseTransformPoint(player.position) - startLocal, (endLocal - startLocal) / axisLen) / axisLen : -1f;
                if (lastT[i] < playerT && t >= playerT && t - lastT[i] < 0.5f) { Sfx.Play("whoomp", player.position, 0.7f); Haptics.Pulse(0.5f, 0.25f); }
                lastT[i] = t;
            }
            // 중간에서 가장 세게 조여지고 양 끝에서 풀림
            float squeeze = Mathf.Lerp(1f, squeezeScale, Mathf.Sin(t * Mathf.PI));
            rings[i].localScale = Vector3.one * ringScale * squeeze;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.TransformPoint(startLocal), transform.TransformPoint(endLocal));
    }
}
