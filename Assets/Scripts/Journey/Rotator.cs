using UnityEngine;

/// <summary>전시물 회전 + (선택) 상하 부유. 로비 전시대/홀로그램에 사용.</summary>
[AddComponentMenu("인체탐험/회전 및 부유") ]
public class Rotator : MonoBehaviour
{
    public Vector3 degreesPerSecond = new Vector3(0, 20, 0);
    public float bobAmplitude = 0f;
    public float bobSpeed = 1f;

    Vector3 basePos;
    void Awake() { basePos = transform.localPosition; }

    void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        if (bobAmplitude > 0f) transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
    }
}
