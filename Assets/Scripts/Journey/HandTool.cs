using UnityEngine;

/// <summary>
/// 오른손에 드는 치료 도구(광선기). 장면마다 하나 두면 실행할 때 오른손 위치에 붙고, 장면을 떠나면 함께 사라진다.
/// 조준 광선은 JourneyAim(오른손 앞 방향)을 따른다. 컨트롤러가 없으면(시선 조준) 도구를 숨긴다.
/// </summary>
[AddComponentMenu("인체탐험/손 치료 도구")]
public class HandTool : MonoBehaviour
{
    public GameObject toolPrefab;
    public Vector3 localOffset = new Vector3(0, -.02f, .04f);
    GameObject tool;
    Transform hand;

    void LateUpdate()
    {
        if (hand == null)
        {
            var h = GameObject.Find("RightHandAnchor");
            if (h == null) return;
            hand = h.transform;
        }
        if (tool == null && toolPrefab != null)
        {
            tool = Instantiate(toolPrefab, hand);
            tool.transform.localPosition = localOffset; tool.transform.localRotation = Quaternion.identity;
        }
        if (tool != null) tool.SetActive(OVRInput.IsControllerConnected(OVRInput.Controller.RTouch));
    }

    // 손(영속 플레이어)에 붙은 도구는 장면과 함께 사라지지 않으므로 직접 지운다.
    void OnDestroy() { if (tool != null) Destroy(tool); }
}
