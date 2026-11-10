using UnityEngine;

/// <summary>
/// 라이드 진행도(0~1)에 따라 오브젝트 단계를 바꾼다. 대장에서 찌꺼기가 묽음→반고형→고형으로 굳어지는 연출 등.
/// stages[i] 는 진행도가 thresholds[i] 이상일 때 활성화 (마지막까지 누적이 아니라 하나만).
/// </summary>
[AddComponentMenu("인체탐험/탑승 구간 전환") ]
public class RideProgressSwitcher : MonoBehaviour
{
    public CapsuleRide ride;
    public GameObject[] stages;
    [Tooltip("각 단계가 시작되는 진행도. 단계 목록과 개수를 맞추고 작은 값부터 입력")]
    public float[] thresholds = { 0f, 0.4f, 0.75f };
    [Tooltip("단계 전환 시 크로스 스케일 시간(초)")]
    public float blendSeconds = 1f;

    int current = -1;
    float blend;
    Vector3[] baseScales;

    void Start()
    {
        baseScales = new Vector3[stages.Length];
        for (int i = 0; i < stages.Length; i++)
            if (stages[i] != null) { baseScales[i] = stages[i].transform.localScale; stages[i].SetActive(false); }
    }

    void Update()
    {
        if (ride == null || ride.cart == null) return;
        float p = ride.cart.SplinePosition;
        int idx = 0;
        for (int i = 0; i < thresholds.Length && i < stages.Length; i++) if (p >= thresholds[i]) idx = i;

        if (idx != current)
        {
            if (current >= 0 && stages[current] != null) stages[current].SetActive(false);
            current = idx; blend = 0f;
            if (stages[current] != null) { stages[current].SetActive(true); stages[current].transform.localScale = Vector3.zero; }
        }
        if (blend < blendSeconds && current >= 0 && stages[current] != null)
        {
            blend += Time.deltaTime;
            stages[current].transform.localScale = baseScales[current] * Mathf.SmoothStep(0f, 1f, blend / blendSeconds);
        }
    }
}
