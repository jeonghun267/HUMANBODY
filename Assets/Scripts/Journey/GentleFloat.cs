using UnityEngine;

/// <summary>
/// 액체 속에 뜬 물체가 둥실둥실: 처음 자리를 중심으로 천천히 오르내리고, 조금 흐르고, 살짝 기울며 돈다.
/// 멀미가 나지 않게 아주 느리고 작게 움직인다. 부모 기준(localPosition)이라 잠수정에 붙은 물체도 함께 따라간다.
/// </summary>
[AddComponentMenu("인체탐험/둥실 떠다님")]
public class GentleFloat : MonoBehaviour
{
    [Tooltip("오르내림 폭(m)")] public float bob = .06f;
    [Tooltip("한 번 오르내리는 시간(초)")] public float period = 4f;
    [Tooltip("좌우·앞뒤로 흐르는 폭(m)")] public float drift = .04f;
    [Tooltip("기울어짐(도)")] public float tilt = 5f;
    [Tooltip("천천히 도는 속도(도/초)")] public float spin;

    Vector3 home; Quaternion rest; float phase;

    void Awake()
    {
        home = transform.localPosition; rest = transform.localRotation;
        phase = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time * Mathf.PI * 2 / Mathf.Max(.5f, period) + phase;
        transform.localPosition = home + new Vector3(Mathf.Sin(t * .53f) * drift, Mathf.Sin(t) * bob, Mathf.Cos(t * .41f) * drift);
        transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * .7f) * tilt, (Time.time + phase) * spin, Mathf.Cos(t * .6f) * tilt);
    }
}
