using UnityEngine;
using TMPro; // TextMeshPro 관련

public class DisplayInfo : MonoBehaviour
{
    [Header("설명 UI")]
    [Tooltip("설명 UI 패널 게임오브젝트")]
    [SerializeField]
    private GameObject infoPanelObject;

    [Tooltip("설명 텍스트(TextMeshProUGUI)")]
    [SerializeField]
    private TextMeshProUGUI explanationTextComponent;

    [TextArea(3, 10)]
    [Tooltip("해당 객체에 대한 설명 텍스트")]
    [SerializeField]
    private string organExplanation = "이 영역에 기관 설명이 표시됩니다. 편집해서 사용하세요.";

    [Header("원격 강조 표시")]
    [Tooltip("원격 사용자가 볼 때 강조할 오브젝트")]
    [SerializeField]
    private GameObject remoteHighlightObject;

    // 객체의 ID (기본: 게임오브젝트 이름)
    public string OrganID => gameObject.name;

    void Start()
    {
        if (infoPanelObject != null) infoPanelObject.SetActive(false);
        if (remoteHighlightObject != null) remoteHighlightObject.SetActive(false);

        if (explanationTextComponent != null)
        {
            explanationTextComponent.text = organExplanation;
        }
    }

    // 정보 표시
    public void ShowInfo()
    {
        Debug.Log($"정보 표시: {gameObject.name}");
        if (infoPanelObject != null)
        {
            infoPanelObject.SetActive(true);
        }

        // 필요 시 OSC 전송 코드 추가:
        // OSCManager.Instance?.SendInteractionStatus(OrganID, true); 
    }

    // 정보 숨기기
    public void HideInfo()
    {
        Debug.Log($"정보 숨김: {gameObject.name}");
        if (infoPanelObject != null)
        {
            infoPanelObject.SetActive(false);
        }

        // 필요 시 OSC 전송 코드 추가:
        // OSCManager.Instance?.SendInteractionStatus(OrganID, false);
    }

    // 원격 사용자의 상호작용 상태 표시
    public void ShowRemoteStatus()
    {
        if (infoPanelObject == null || !infoPanelObject.activeSelf)
        {
            Debug.Log($"원격 상태 표시: {gameObject.name}");
            if (remoteHighlightObject != null)
            {
                remoteHighlightObject.SetActive(true);
            }
        }
    }

    // 원격 상태 숨김
    public void HideRemoteStatus()
    {
        Debug.Log($"원격 상태 숨김: {gameObject.name}");
        if (remoteHighlightObject != null)
        {
            remoteHighlightObject.SetActive(false);
        }
    }
}                               