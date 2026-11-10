using UnityEngine;

[AddComponentMenu("인체탐험/가상현실 시선 상호작용") ]
public class VRGazeInteractor : MonoBehaviour
{
    [Tooltip("레이캐스트의 최대 거리")]
    public float maxGazeDistance = 50f;

    // 이전에 바라보던 NPC (시선이 벗어났을 때 StopInteraction을 호출하기 위해 필요)
    private NPCController lastLookedNPC = null;

    void Update()
    {
        // VR 카메라의 위치와 방향을 기준으로 Ray를 발사합니다.
        Ray gazeRay = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // 레이캐스트를 발사하여 충돌한 오브젝트가 있는지 확인합니다.
        if (Physics.Raycast(gazeRay, out hit, maxGazeDistance))
        {
            // 충돌한 오브젝트에서 NPCController 컴포넌트를 가져옵니다.
            NPCController currentNPC = hit.collider.GetComponent<NPCController>();

            if (currentNPC != null)
            {
                // 1. NPC를 바라보는 경우: 상호작용 시작!
                if (lastLookedNPC == null)
                {
                    // 이전에 아무도 안 보고 있다가 NPC를 처음 보기 시작했을 때
                    currentNPC.StartInteraction();
                    lastLookedNPC = currentNPC;
                }
                else if (lastLookedNPC != currentNPC)
                {
                    // 다른 NPC를 바라보았다면, 이전 NPC는 멈추고 새로운 NPC 시작
                    lastLookedNPC.StopInteraction();
                    currentNPC.StartInteraction();
                    lastLookedNPC = currentNPC;
                }
                // (이미 보고 있던 NPC라면 아무것도 하지 않음)
            }
            else
            {
                // 2. 다른 오브젝트를 바라보는 경우: 상호작용 종료!
                if (lastLookedNPC != null)
                {
                    lastLookedNPC.StopInteraction();
                    lastLookedNPC = null;
                }
            }
        }
        else
        {
            // 3. 아무것도 바라보지 않는 경우: 상호작용 종료!
            if (lastLookedNPC != null)
            {
                lastLookedNPC.StopInteraction();
                lastLookedNPC = null;
            }
        }
    }
}