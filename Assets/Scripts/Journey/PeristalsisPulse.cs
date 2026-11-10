using UnityEngine;

/// <summary>
/// 장기 벽이 연동운동(peristalsis)하듯 천천히 수축/이완하는 연출.
/// 터널 세그먼트 여러 개에 붙이고 phaseOffset 을 다르게 주면 파동이 지나가는 느낌이 난다.
/// 셰이더 없이 스케일 + (선택) 머티리얼 이미션으로 처리해 Quest 에서도 가볍다.
/// </summary>
[AddComponentMenu("인체탐험/기관 수축") ]
public class PeristalsisPulse : MonoBehaviour
{
    [Header("수축 파형")]
    [Tooltip("한 번 수축-이완에 걸리는 시간(초)")]
    public float period = 4f;
    [Tooltip("최대 수축 비율 (0.05 = 5% 작아짐)")]
    [Range(0f, 0.3f)] public float amplitude = 0.05f;
    [Tooltip("수축 축 (터널이면 진행 방향 축을 0으로)")]
    public Vector3 axisWeight = new Vector3(1, 1, 0);
    [Tooltip("세그먼트별 위상 차이 (0~1)")]
    [Range(0f, 1f)] public float phaseOffset;

    [Header("이미션 맥동 (선택)")]
    public Renderer emissiveRenderer;
    public Color emissionColor = new Color(0.8f, 0.2f, 0.2f);
    [Range(0f, 3f)] public float emissionStrength = 0.6f;

    Vector3 baseScale;
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        baseScale = transform.localScale;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        // 0..1 사이를 부드럽게 오가는 값 (수축 시 잠깐 머무는 느낌을 위해 smoothstep)
        float t = Mathf.Repeat(Time.time / period + phaseOffset, 1f);
        float wave = Mathf.SmoothStep(0f, 1f, Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f);

        float shrink = 1f - amplitude * wave;
        transform.localScale = new Vector3(
            baseScale.x * Mathf.Lerp(1f, shrink, axisWeight.x),
            baseScale.y * Mathf.Lerp(1f, shrink, axisWeight.y),
            baseScale.z * Mathf.Lerp(1f, shrink, axisWeight.z));

        if (emissiveRenderer != null)
        {
            emissiveRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, emissionColor * (emissionStrength * wave));
            emissiveRenderer.SetPropertyBlock(block);
        }
    }
}
