using UnityEngine;

/// <summary>융털/목젖/후두덮개처럼 뿌리가 고정된 파츠가 살랑거리는 연출. 피벗이 뿌리 쪽에 있어야 자연스럽다.</summary>
[AddComponentMenu("인체탐험/흔들림") ]
public class Sway : MonoBehaviour
{
    public float angle = 6f;
    public float speed = 1.2f;
    public Vector3 axis = Vector3.right;
    [Tooltip("여러 개를 붙일 때 위상을 무작위로")]
    public bool randomPhase = true;

    Quaternion baseRot;
    float phase;

    void Awake()
    {
        baseRot = transform.localRotation;
        phase = randomPhase ? Random.Range(0f, 100f) : 0f;
    }

    void Update()
    {
        float a = Mathf.Sin(Time.time * speed + phase) * angle + Mathf.Sin(Time.time * speed * 2.3f + phase) * angle * 0.3f;
        transform.localRotation = baseRot * Quaternion.AngleAxis(a, axis);
    }
}
