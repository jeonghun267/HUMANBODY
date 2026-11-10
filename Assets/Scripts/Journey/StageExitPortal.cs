using UnityEngine;

/// <summary>
/// 씬 끝의 출구. 플레이어(또는 탑승 중인 캡슐)가 닿으면 JourneyManager 로 다음 단계 이동.
/// 씬 이름을 직접 넣던 SceneChanger 를 대체 - 순서는 JourneyManager 가 가진다.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("인체탐험/다음 단계 입구") ]
public class StageExitPortal : MonoBehaviour
{
    [Tooltip("단계 정보에 등록된 퇴장 안내를 먼저 재생할지")]
    public bool playOutro = true;
    [Tooltip("특정 단계로 강제 이동 (비우면 다음 단계)")]
    public string overrideStageId;
    [Tooltip("닿기만 해도 떠난다. 끄면 Depart() 로만 (병원 잠수정: 출발 버튼)")]
    public bool walkInToDepart = true;

    bool used;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        bool isPlayer = other.CompareTag("Player") || other.CompareTag("VRCamera") || other.CompareTag("Boat")
                        || other.GetComponentInParent<OVRCameraRig>() != null;
        if (!isPlayer || !walkInToDepart) return;
        Depart();
    }

    /// <summary>다음 단계로 떠난다 (출발 버튼 등에서 부른다).</summary>
    public void Depart()
    {
        if (used || JourneyManager.Instance == null) return;
        used = true;
        if (string.IsNullOrEmpty(overrideStageId)) JourneyManager.Instance.GoToNext(playOutro);
        else JourneyManager.Instance.GoTo(overrideStageId, playOutro);
    }
}
