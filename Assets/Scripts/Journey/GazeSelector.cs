using UnityEngine;

/// <summary>
/// 카메라(CenterEyeAnchor)에 붙이는 시선 선택기. 바라보는 IGazeTarget(ExhibitStation, PartLabel)을 활성화하고, 시선을 떼면 해제한다.
/// 기존 RaycastInteractor 의 태그 방식 대신 컴포넌트 검색을 쓴다.
/// </summary>
[AddComponentMenu("인체탐험/시선 선택") ]
public class GazeSelector : MonoBehaviour
{
    public float maxDistance = 12f;
    [Tooltip("초당 레이캐스트 횟수")]
    public float frequency = 15f;
    [Tooltip("시선을 떼고 이 시간(초) 뒤에 닫음 - 잠깐 눈길 돌려도 유지")]
    public float closeDelay = 0.8f;
    public LayerMask layers = ~0;

    IGazeTarget current;
    float nextCast, lostTime = -1f;

    void Update()
    {
        if (Time.time < nextCast) return;
        nextCast = Time.time + 1f / frequency;
        if (current is MonoBehaviour dead && dead == null) { current = null; lostTime = -1f; } // 대상이 파괴된 경우

        IGazeTarget hit = null;
        if (Physics.Raycast(transform.position, transform.forward, out var h, maxDistance, layers, QueryTriggerInteraction.Collide))
            hit = h.collider.GetComponentInParent<IGazeTarget>();

        if (hit != null)
        {
            lostTime = -1f;
            if (hit != current) { current?.OnGazeExit(); current = hit; current.OnGazeEnter(); }
            return;
        }

        if (current == null) return;
        if (lostTime < 0f) lostTime = Time.time;
        if (Time.time - lostTime >= closeDelay) { current.OnGazeExit(); current = null; lostTime = -1f; }
    }
}
