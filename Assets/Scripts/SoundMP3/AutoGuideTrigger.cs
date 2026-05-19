using UnityEngine;

public class AutoGuideTrigger : MonoBehaviour
{
    [Tooltip("여기에 설명하고 싶은 장기 이름을 영어로 적으세요 (예: Stomach, Liver)")]
    public string targetOrgan = "Stomach";

    // ImageGenerator 변수 삭제됨 (스크립트가 없으므로)

    void Start()
    {
        // 초기화할 게 없으므로 비워둠
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어(VRCamera)가 들어오면
        if (other.CompareTag("VRCamera"))
        {
            Debug.Log($"[AutoGuide] 플레이어 입장! 대상 장기: {targetOrgan}");

            // [중요] ImageGenerator가 삭제되었습니다.
            // 이제 여기서 무엇을 할지 결정해야 합니다.

            // 옵션 1: 단순히 로그만 띄우고 끝냄 (현재 상태)

            // 옵션 2: OSCManager를 통해 다른 사람에게 "나 이거 보고 있어"라고 알림 (주석 해제해서 사용)
            /*
            if (OSCManager.Instance != null)
            {
                OSCManager.Instance.SendInteractionStatus(targetOrgan, true);
            }
            */

            // 옵션 3: 해당 장기 오브젝트를 찾아서 직접 켜기 (DisplayInfo 스크립트 등이 있다면)

            // 한 번 작동하고 꺼지게 설정 (중복 실행 방지)
            gameObject.SetActive(false);
        }
    }
}