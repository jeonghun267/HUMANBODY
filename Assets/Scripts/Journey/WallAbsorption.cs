using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 수분 흡수 연출: 물방울(프리팹)이 관 중심에서 생겨나 벽 쪽으로 흘러가다 벽에 닿으면 작아지며 흡수된다.
/// 터널 중심축을 따라 길게 배치. 대장(물), 소장(영양소가 벽으로) 등에 사용.
/// </summary>
[AddComponentMenu("인체탐험/장벽 수분 흡수") ]
public class WallAbsorption : MonoBehaviour
{
    public GameObject[] prefabs;
    [Min(1)] public int count = 30;
    [Tooltip("관의 안쪽 반지름 (여기 닿으면 흡수)")]
    public float tubeRadius = 2.5f;
    [Tooltip("관 길이 (상대 앞뒤 축 방향)")]
    public float tubeLength = 40f;
    public Vector2 driftSpeed = new Vector2(0.3f, 0.7f);
    public float shrinkSeconds = 0.6f;
    public Vector2 scaleRange = new Vector2(0.15f, 0.3f);
    [Tooltip("흡수될 때 벽에 잠깐 남는 파문 파티클 (선택)")]
    public ParticleSystem absorbVfx;
    [Tooltip("각 물방울에 붙일 이름표/설명 (선택)")]
    public PartLabel labelTemplate;

    class Drop { public Transform t; public Vector2 dir; public float speed, scale, shrink; }
    readonly List<Drop> drops = new List<Drop>();

    void Start()
    {
        for (int i = 0; i < count; i++) Spawn(true);
    }

    void Spawn(bool randomRadius)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        var p = prefabs[Random.Range(0, prefabs.Length)];
        if (p == null) return;
        var go = Instantiate(p, transform);
        if (labelTemplate != null) PartLabel.Attach(go, labelTemplate, 0.6f);
        var d = new Drop { t = go.transform, dir = Random.insideUnitCircle.normalized, speed = Random.Range(driftSpeed.x, driftSpeed.y), scale = Random.Range(scaleRange.x, scaleRange.y), shrink = -1f };
        float r = randomRadius ? Random.Range(0f, tubeRadius * 0.8f) : Random.Range(0f, tubeRadius * 0.2f);
        d.t.localPosition = new Vector3(d.dir.x * r, d.dir.y * r, Random.Range(-tubeLength * 0.5f, tubeLength * 0.5f));
        d.t.localScale = Vector3.one * d.scale;
        drops.Add(d);
    }

    void Update()
    {
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            if (d.t == null) { drops.RemoveAt(i); continue; }
            Vector3 lp = d.t.localPosition;

            if (d.shrink >= 0f)
            {
                d.shrink += Time.deltaTime;
                float k = 1f - d.shrink / shrinkSeconds;
                d.t.localScale = Vector3.one * d.scale * Mathf.Max(0f, k);
                if (k <= 0f)
                {
                    // 재사용: 중심 근처에서 다시 시작
                    d.dir = Random.insideUnitCircle.normalized; d.shrink = -1f;
                    float r = Random.Range(0f, tubeRadius * 0.2f);
                    d.t.localPosition = new Vector3(d.dir.x * r, d.dir.y * r, Random.Range(-tubeLength * 0.5f, tubeLength * 0.5f));
                    d.t.localScale = Vector3.one * d.scale;
                }
                continue;
            }

            Vector2 radial = new Vector2(lp.x, lp.y) + d.dir * (d.speed * Time.deltaTime);
            lp.x = radial.x; lp.y = radial.y;
            lp.y += Mathf.Sin(Time.time * 2f + i) * 0.002f;
            d.t.localPosition = lp;
            d.t.Rotate(Vector3.up, 30f * Time.deltaTime);

            if (radial.magnitude >= tubeRadius)
            {
                d.shrink = 0f;
                // 경계를 넘은 위치 대신 실제 흡수 경계에 파문을 붙인다.
                Vector2 boundary = radial.normalized * tubeRadius;
                Vector3 localPoint = new Vector3(boundary.x, boundary.y, lp.z);
                Vector3 normal = transform.worldToLocalMatrix.transpose.MultiplyVector(new Vector3(-boundary.x, -boundary.y, 0)).normalized;
                d.t.localPosition = localPoint;
                JourneyParticleBurst.Emit(absorbVfx, transform.TransformPoint(localPoint) + normal * .025f, 1, normal);
                if (Random.value < 0.35f) Sfx.Play("drip", d.t.position, 0.35f, Random.Range(0.8f, 1.3f));
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(tubeRadius * 2, tubeRadius * 2, tubeLength));
    }
}
