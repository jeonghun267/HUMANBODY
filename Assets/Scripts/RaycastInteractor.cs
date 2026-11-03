using UnityEngine;

[AddComponentMenu("인체탐험/시선 상호작용") ]
public class RaycastInteractor : MonoBehaviour
{
    private Transform raycastOrigin;

    [Tooltip("레이캐스트 최대 거리")]
    [SerializeField]
    private float maxDistance = 10f;

    [Tooltip("레이캐스트 빈도 (초당 횟수)")]
    [SerializeField]
    private float raycastFrequency = 10f;

    private float lastRaycastTime;

    private GameObject currentLookTarget = null;
    private GameObject previousLookTarget = null;

    [Tooltip("상호작용 대상 태그")]
    [SerializeField]
    private string interactableTag = "InteractableOrgan";

    void Start()
    {
        raycastOrigin = this.transform;
    }

    void Update()
    {
        if (Time.time > lastRaycastTime + (1f / raycastFrequency))
        {
            PerformRaycast();
            lastRaycastTime = Time.time;
        }
    }

    private void PerformRaycast()
    {
        RaycastHit hit;
        if (Physics.Raycast(raycastOrigin.position, raycastOrigin.forward, out hit, maxDistance))
        {
            if (hit.collider.CompareTag(interactableTag))
            {
                currentLookTarget = hit.collider.gameObject;

                if (currentLookTarget != previousLookTarget)
                {
                    if (previousLookTarget != null)
                    {
                        previousLookTarget.GetComponent<DisplayInfo>()?.HideInfo();
                        // OSC 전송 예시:
                        // OSCManager.Instance?.SendInteractionStatus(previousLookTarget.name, false);
                    }

                    currentLookTarget.GetComponent<DisplayInfo>()?.ShowInfo();
                    // OSC 전송 예시:
                    // OSCManager.Instance?.SendInteractionStatus(currentLookTarget.name, true);

                    previousLookTarget = currentLookTarget;
                }
            }
            else
            {
                HandleLookExit();
            }
        }
        else
        {
            HandleLookExit();
        }
    }

    private void HandleLookExit()
    {
        if (previousLookTarget != null)
        {
            previousLookTarget.GetComponent<DisplayInfo>()?.HideInfo();
            // OSC 전송 예시:
            // OSCManager.Instance?.SendInteractionStatus(previousLookTarget.name, false);

            previousLookTarget = null;
        }
        currentLookTarget = null;
    }
}