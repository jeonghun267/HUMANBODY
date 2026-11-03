using UnityEngine;

[AddComponentMenu("인체탐험/자동 회전") ]
public class AutoRotate : MonoBehaviour
{
    [Header("한 바퀴 도는 데 걸리는 시간 (초)")]
    public float duration = 5.0f; // 기본 5초

    void Update()
    {
        // 360도를 시간으로 나누면 초당 회전 속도가 나옴
        float speed = 360f / duration;

        // Y축(위쪽)을 기준으로 회전 (Space.World를 써야 비스듬히 안 돌고 똑바로 돔!)
        transform.Rotate(Vector3.up * speed * Time.deltaTime, Space.World);
    }
}