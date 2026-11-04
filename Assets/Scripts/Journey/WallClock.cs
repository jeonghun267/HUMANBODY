using System;
using UnityEngine;

/// <summary>진료실 벽시계: 실제 시각을 가리킨다. 시계 몸체의 앞(+z)이 벽 안쪽이라, 보는 사람에게 시계 방향 = z 축 음의 회전.</summary>
[AddComponentMenu("인체탐험/벽시계")]
public class WallClock : MonoBehaviour
{
    public Transform hour, minute, second;

    void Update()
    {
        var now = DateTime.Now;
        float s = now.Second, m = now.Minute + s / 60f, h = now.Hour % 12 + m / 60f;
        if (hour != null) hour.localRotation = Quaternion.Euler(0, 0, -h * 30f);
        if (minute != null) minute.localRotation = Quaternion.Euler(0, 0, -m * 6f);
        if (second != null) second.localRotation = Quaternion.Euler(0, 0, -s * 6f);
    }
}
