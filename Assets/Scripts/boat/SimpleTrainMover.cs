using UnityEngine;
using Unity.Cinemachine; // 버전에 따라 namespace가 다를 수 있음 (Cinemachine 혹은 Unity.Cinemachine)

[AddComponentMenu("인체탐험/경로 이동") ]
public class SimpleTrainMover : MonoBehaviour
{
    // 네가 스크린샷에 띄워놓은 그 컴포넌트를 가져올 변수
    public CinemachineSplineCart cart;
    public float speed = 0.5f; // 기차 속도

    void Update()
    {
        // 매 프레임마다 속도만큼 거리를 증가시킴 (앞으로 이동)
        if (cart != null)
        {
            cart.SplinePosition += speed * Time.deltaTime;
        }
    }
}