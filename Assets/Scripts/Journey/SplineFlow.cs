using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 스플라인을 따라 오브젝트가 계속 흘러가는 연출. 혈관 속 적혈구, 유미관 속 지방, 유문을 빠져나가는 미즙 등에 사용.
/// 각 오브젝트는 서로 다른 속도/오프셋/살짝 흔들림을 가져 자연스럽게 보인다.
/// </summary>
[AddComponentMenu("인체탐험/곡선을 따라 흐르기") ]
public class SplineFlow : MonoBehaviour
{
    public SplineContainer spline;
    public GameObject[] prefabs;
    [Min(0)] public int count = 12;
    [Tooltip("스플라인 한 바퀴에 걸리는 시간(초)")]
    public float loopSeconds = 10f;
    public Vector2 speedJitter = new Vector2(0.8f, 1.2f);
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    [Tooltip("경로에서 벗어나는 흔들림 반경")]
    public float wobbleRadius = 0.15f;
    public float wobbleSpeed = 2f;
    [Tooltip("진행 방향을 바라보게 회전")]
    public bool alignToPath = true;
    public float spinSpeed = 60f;
    [Tooltip("각 개체에 붙일 이름표/설명 (선택)")]
    public PartLabel labelTemplate;

    class Item { public Transform t; public float offset, speed, phase, spin; public Vector3 wobbleAxis; }
    readonly List<Item> items = new List<Item>();

    void Start()
    {
        if (spline == null || prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;
            var go = Instantiate(prefab, transform);
            go.transform.localScale = prefab.transform.localScale * Random.Range(scaleRange.x, scaleRange.y);
            if (labelTemplate != null) PartLabel.Attach(go, labelTemplate, 0.6f);
            items.Add(new Item
            {
                t = go.transform,
                offset = (float)i / count,
                speed = Random.Range(speedJitter.x, speedJitter.y),
                phase = Random.Range(0f, 100f),
                spin = Random.Range(-1f, 1f),
                wobbleAxis = Random.onUnitSphere,
            });
            if (startHidden) go.SetActive(false);
        }
        if (startHidden) enabled = false;
    }

    /// <summary>UnityEvent 에서 호출용: 흐름 시작/정지 (개체는 미리 생성돼 있고 이동만 멈춘다).</summary>
    public void Begin() { enabled = true; foreach (var it in items) if (it.t != null) it.t.gameObject.SetActive(true); }
    public void Halt() { enabled = false; foreach (var it in items) if (it.t != null) it.t.gameObject.SetActive(false); }

    [Tooltip("시작 시 개체를 숨겨 두고 시작 동작을 기다립니다.")]
    public bool startHidden;

    void Update()
    {
        if (spline == null) return;
        foreach (var it in items)
        {
            if (it.t == null) continue;
            float u = Mathf.Repeat(Time.time / loopSeconds * it.speed + it.offset, 1f);
            Vector3 pos = spline.EvaluatePosition(u);
            Vector3 tan = ((Vector3)spline.EvaluateTangent(u)).normalized;
            Vector3 wobble = Vector3.Cross(tan, it.wobbleAxis).normalized * (Mathf.Sin(Time.time * wobbleSpeed + it.phase) * wobbleRadius);
            it.t.position = pos + wobble;
            if (alignToPath && tan != Vector3.zero) it.t.rotation = Quaternion.LookRotation(tan) * Quaternion.AngleAxis(Time.time * spinSpeed * it.spin, Vector3.forward);
            else it.t.Rotate(Vector3.up, spinSpeed * it.spin * Time.deltaTime, Space.World);
        }
    }
}
