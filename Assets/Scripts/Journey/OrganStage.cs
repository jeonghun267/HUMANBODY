using UnityEngine;

/// <summary>
/// 소화 여정의 한 단계(로비/입/식도/위/소장/대장/상영관) 데이터.
/// Assets/Data/Stages/ 에 ScriptableObject 에셋으로 생성해서 JourneyManager 에 순서대로 등록한다.
/// </summary>
[CreateAssetMenu(menuName = "인체탐험/단계 정보", fileName = "Stage_")]
public class OrganStage : ScriptableObject
{
    [Header("식별")]
    [Tooltip("코드와 원격 통신을 연결하는 내부 식별자")]
    public string stageId = "Stomach";
    [Tooltip("화면 에 표시되는 한글 이름")]
    public string displayName = "위";
    [Tooltip("빌드 설정 에 등록된 씬 이름")]
    public string sceneName = "04_Stomach";

    [Header("입장 내레이션 (씬 로드 직후 자동 재생)")]
    public NarrationLine[] introLines;

    [Header("퇴장 내레이션 (다음 단계로 넘어가기 전)")]
    public NarrationLine[] outroLines;

    [Header("정보 패널")]
    [TextArea(2, 6)] public string summary;
    [Tooltip("알아두면 좋은 사실 - 정보 패널에 목록으로 표시")]
    [TextArea(1, 3)] public string[] funFacts;

    [Header("연출")]
    public Color fadeColor = Color.black;
    [Tooltip("이 단계에서 보트(캡슐)를 타고 이동하는가")]
    public bool ridesCapsule;
}

/// <summary>내레이션 한 줄 - 자막 텍스트 + (선택) 음성 클립.</summary>
[System.Serializable]
public class NarrationLine
{
    [TextArea(1, 4)] public string text;
    public AudioClip voice;
    [Tooltip("음성이 없을 때 자막을 유지할 시간(초). 음성이 있으면 음성 길이를 따릅니다.")]
    public float holdSeconds = 4f;
}
