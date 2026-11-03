using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// 이 스크립트가 NPC 오브젝트에 붙어있어야 합니다.
[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(XRSimpleInteractable))]
[AddComponentMenu("인체탐험/안내 캐릭터 상호작용 연결") ]
public class NPCInteractionBridge : MonoBehaviour
{
    private NPCController npcController;
    private XRSimpleInteractable interactable;

    void Start()
    {
        // 필요한 컴포넌트들을 가져옵니다.
        npcController = GetComponent<NPCController>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
        {
            // NPC를 바라보기 시작했을 때 (Hover Enter)
            interactable.firstHoverEntered.AddListener(OnHoverEnter);

            // NPC에서 시선을 돌렸을 때 (Hover Exit)
            interactable.lastHoverExited.AddListener(OnHoverExit);
        }
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        // NPCController에게 플레이어를 바라보라고 명령
        if (npcController != null)
        {
            npcController.StartInteraction();
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        // NPCController에게 원래 추적 모드로 돌아가라고 명령
        if (npcController != null)
        {
            npcController.StopInteraction();
        }
    }
}