#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>연결된 자료의 식별자를 바꾸지 않고 프로젝트 전용 설정을 한글로 표시한다.</summary>
public class KoreanJourneyEditor : Editor
{
    public static readonly Dictionary<string, string> Fields = new Dictionary<string, string>
    {
        { "salivaEmitters", "모든 침샘 방출기" },
        { "grindAngle", "옆으로 가는 각도" }, { "chewPoint", "씹는 어금니 자리" }, { "gatherPoint", "혀 위 모으는 자리" }, { "moveSeconds", "음식 옮기는 시간" },
        { "characterRole", "캐릭터 역할" }, { "joints", "동작 관절" }, { "motionStrength", "동작 크기" },
        { "speaking", "말하는 중" }, { "dissolveRenderers", "표면 분해 대상" },
        { "contactSplash", "접촉 물방울" }, { "dropletsPerContact", "접촉당 물방울 수" },
        { "contactLimitPerFrame", "화면 갱신당 접촉 제한" }, { "cutVfx", "절단 조각 효과" }, { "openingSurface", "열리는 문 표면" },
        { "DebugStart", "시험 탑승 시작" },
        { "NPC_UI", "안내 캐릭터 화면" },
        { "absorbDuration", "흡수 시간" },
        { "absorbSfx", "흡수 효과음" },
        { "absorbTarget", "흡수 도착 지점" },
        { "absorbVfx", "흡수 시각 효과" },
        { "absorbedLine", "흡수 설명" },
        { "absorbedVoice", "흡수 안내 음성" },
        { "acidVfx", "위산 시각 효과" },
        { "activeDistance", "표시 거리" },
        { "advanceOnArrive", "도착하면 다음 단계로 이동" },
        { "advanceOnComplete", "완료하면 다음 단계로 이동" },
        { "advanceOnFinish", "끝나면 다음 단계로 이동" },
        { "alignToPath", "진행 방향으로 정렬" },
        { "allowSkip", "설명 건너뛰기 허용" },
        { "amplitude", "진폭" },
        { "angle", "기울기" },
        { "angleBias", "좌우 위치 각도" },
        { "animator", "애니메이션 제어기" },
        { "approachSeconds", "다가가는 시간" },
        { "arcColor", "이동 경로 색상" },
        { "audioSource", "소리 재생 장치" },
        { "autoBoard", "자동 탑승" },
        { "autoBoardDelay", "자동 탑승 대기 시간" },
        { "autoCloseAfter", "자동 닫힘 대기 시간" },
        { "axis", "회전 축" },
        { "axisWeight", "축별 수축 비율" },
        { "baseMoveRange", "기본 이동 범위" },
        { "baseMoveSpeed", "기본 이동 속도" },
        { "baseRotateSpeed", "기본 회전 속도" },
        { "betweenChains", "단백질 사이 대기 시간" },
        { "blendSeconds", "전환 시간" },
        { "blocker", "통과 차단 영역" },
        { "boatSpeed", "보트 이동 속도" },
        { "bobAmplitude", "떠오름 진폭" },
        { "bobHeight", "떠오름 높이" },
        { "bobSpeed", "떠오름 속도" },
        { "bodyText", "본문 표시 영역" },
        { "bolus", "음식 덩어리" },
        { "canvasRoot", "화면 최상위 개체" },
        { "cart", "경로 이동 장치" },
        { "charsPerSecond", "초당 표시 글자 수" },
        { "chewAudio", "씹는 소리 재생 장치" },
        { "chewClip", "씹는 효과음" },
        { "chewCount", "씹는 횟수" },
        { "chewsPerSecond", "초당 씹는 횟수" },
        { "chickAudioSource", "병아리 음성 재생 장치" },
        { "chickNPC", "안내 병아리" },
        { "chickSeatPoint", "병아리 좌석" },
        { "churnAmplitudeMultiplier", "소화 중 수축 배수" },
        { "churnAudio", "소화 소리 재생 장치" },
        { "chymeBlobs", "미즙 덩어리" },
        { "clipName", "환경음 종류" },
        { "closeDelay", "닫힘 대기 시간" },
        { "completeLines", "완료 안내" },
        { "count", "개수" },
        { "cutClip", "절단 효과음" },
        { "cutSeconds", "절단 시간" },
        { "degreesPerSecond", "초당 회전 각도" },
        { "delayAfterVideo", "영상 종료 후 대기 시간" },
        { "dialogueTexts", "안내 문구 목록" },
        { "digestDuration", "소화 시간" },
        { "disableBloomOnDevice", "기기에서 빛 번짐 끄기" },
        { "disableShadowsOnDevice", "기기에서 그림자 끄기" },
        { "disableSmoothOnDevice", "기기에서 연속 이동 끄기" },
        { "displayName", "표시 이름" },
        { "distance", "거리" },
        { "dontInterrupt", "다른 설명 도중 기다리기" },
        { "driftRange", "부유 범위" },
        { "driftSpeed", "부유 속도" },
        { "duration", "지속 시간" },
        { "easeSeconds", "출발과 정지 완화 시간" },
        { "emissionColor", "발광 색상" },
        { "emissionStrength", "발광 강도" },
        { "emissiveRenderer", "발광 표현 대상" },
        { "endLocal", "종점 상대 위치" },
        { "enzyme", "소화 효소" },
        { "epiglottis", "후두덮개" },
        { "epiglottisCloseAngle", "후두덮개 닫힘 각도" },
        { "explainOnce", "설명은 처음 한 번만" },
        { "fadeColor", "화면 전환 색상" },
        { "fadeDuration", "화면 전환 시간" },
        { "fadeSeconds", "화면 전환 시간" },
        { "fallDistance", "하강 거리" },
        { "fallSpeed", "하강 속도" },
        { "fallbackSeconds", "영상이 없을 때 대기 시간" },
        { "finalScale", "최종 크기 비율" },
        { "fitTypingToVoice", "음성 길이에 맞춰 글자 표시" },
        { "floatAmplitude", "상하 부유 진폭" },
        { "floatFrequency", "상하 부유 빈도" },
        { "floorPlane", "바닥" },
        { "flowSpeed", "흐름 속도" },
        { "flowVelocity", "흐름 방향과 속도" },
        { "followSpeed", "따라가는 속도" },
        { "foodBolus", "음식 덩어리" },
        { "foodBoluses", "음식 덩어리 목록" },
        { "foveation", "주변부 해상도 절약 수준" },
        { "fragmentLife", "조각 유지 시간" },
        { "fragmentPrefab", "조각 원본" },
        { "fragmentSpread", "조각 퍼짐 범위" },
        { "fragmentsPerChain", "단백질당 조각 수" },
        { "frequency", "초당 검사 횟수" },
        { "fullSpeed", "최대 효과가 되는 이동 속도" },
        { "fullTurn", "최대 효과가 되는 회전 속도" },
        { "funFacts", "알아두면 좋은 사실" },
        { "gapBetweenLines", "문장 사이 간격" },
        { "gazeDistance", "응시 거리" },
        { "gazeDwellSeconds", "응시 유지 시간" },
        { "gazeOrigin", "시선 기준점" },
        { "getOffOnArrive", "도착하면 자동 하차" },
        { "getOffPoint", "하차 지점" },
        { "groundMask", "이동 가능한 바닥 분류" },
        { "guideSeatPoint", "안내 캐릭터 좌석" },
        { "gulpClip", "삼키는 효과음" },
        { "height", "높이" },
        { "highlightColor", "강조 색상" },
        { "highlightRenderers", "강조 표현 대상" },
        { "highlightStrength", "강조 강도" },
        { "holdAfterLast", "마지막 문장 유지 시간" },
        { "holdSeconds", "자막 유지 시간" },
        { "id", "식별자" },
        { "includeExistingChildren", "기존 하위 개체 포함" },
        { "interactionDistance", "상호작용 거리" },
        { "intestineBackground", "장 내부 배경" },
        { "introDelay", "입장 안내 대기 시간" },
        { "introLines", "입장 안내" },
        { "introWait", "입장 안내 시작 대기 한도" },
        { "jawOpenAngle", "턱 벌림 각도" },
        { "keepUpright", "캡슐 수평 유지" },
        { "kinds", "영양소 종류" },
        { "labelRoot", "이름표 최상위 개체" },
        { "labelTemplate", "이름표 원본" },
        { "labelText", "이름표 글자" },
        { "layers", "검사 대상 분류" },
        { "line", "안내 문장" },
        { "lines", "안내 문장 목록" },
        { "loopSeconds", "한 바퀴 이동 시간" },
        { "mainCamera", "주 카메라" },
        { "maxDistance", "최대 거리" },
        { "maxGazeDistance", "최대 응시 거리" },
        { "motionSource", "움직임 측정 대상" },
        { "moveSmoothSpeed", "이동 반응 속도" },
        { "movingIntensity", "이동 중 가장자리 어둡기" },
        { "nextSceneName", "다음 장면 식별자" },
        { "normalizedPosition", "경로 진행 위치" },
        { "nutrientLayer", "영양소 분류" },
        { "onAllCut", "모두 절단한 뒤 실행" },
        { "onChewingFinished", "씹기가 끝난 뒤 실행" },
        { "onClosed", "닫힌 뒤 실행" },
        { "onComplete", "완료한 뒤 실행" },
        { "onDigested", "소화한 뒤 실행" },
        { "onEnter", "진입한 뒤 실행" },
        { "onFinished", "끝난 뒤 실행" },
        { "onOpened", "열린 뒤 실행" },
        { "onReached", "도착한 뒤 실행" },
        { "onRemotePressA", "원격 기본 버튼을 누르면 실행" },
        { "onRemotePressB", "원격 보조 버튼을 누르면 실행" },
        { "onSwallowed", "삼킨 뒤 실행" },
        { "oneShot", "한 번만 실행" },
        { "openScale", "열림 크기 배수" },
        { "openSeconds", "열리는 시간" },
        { "oscOrganId", "원격 통신 장기 식별자" },
        { "outroLines", "퇴장 안내" },
        { "overrideBody", "직접 입력한 설명" },
        { "overrideStageId", "이동할 단계 식별자" },
        { "overrideTitle", "직접 입력한 제목" },
        { "panel", "설명판" },
        { "partId", "부위 식별자" },
        { "period", "한 주기 시간" },
        { "persistentPrefab", "유지할 공통 개체 원본" },
        { "phaseOffset", "주기 시작 차이" },
        { "pitch", "음높이" },
        { "playAfterIntro", "입장 안내 후 재생" },
        { "playOutro", "퇴장 안내 재생" },
        { "player", "플레이어" },
        { "playerPullSeconds", "탐험가 이동 연출 시간" },
        { "prefab", "개체 원본" },
        { "prefabs", "개체 원본 목록" },
        { "progressFormat", "진행 문구 형식" },
        { "progressText", "진행 문구 표시 영역" },
        { "proteinChains", "단백질 사슬" },
        { "pullPlayer", "탐험가 경로 이동 (선택)" },
        { "pulseAmplitude", "맥동 진폭" },
        { "pulseSpeed", "맥동 속도" },
        { "pushSeconds", "밀어 넣는 시간" },
        { "randomPhase", "주기 무작위 배치" },
        { "receivePort", "수신 포트" },
        { "remoteMarker", "원격 관찰 표시" },
        { "repeatCooldown", "다시 설명하기까지 대기 시간" },
        { "restIntensity", "정지 중 가장자리 어둡기" },
        { "ride", "탑승 캡슐" },
        { "ridesCapsule", "캡슐을 타는 단계" },
        { "ringCount", "수축 고리 개수" },
        { "ringDiameter", "수축 고리 지름" },
        { "ringEuler", "수축 고리 회전 각도" },
        { "ringPrefab", "수축 고리 원본" },
        { "ringScale", "수축 고리 크기" },
        { "rotateSmoothSpeed", "회전 반응 속도" },
        { "rotateSpeed", "회전 속도" },
        { "rotationOffset", "모델 회전 보정" },
        { "salivaVfx", "침 시각 효과" },
        { "scaleRange", "크기 범위" },
        { "sceneName", "장면 식별자" },
        { "seatPoint", "플레이어 좌석" },
        { "sendPort", "송신 포트" },
        { "shrinkSeconds", "줄어드는 시간" },
        { "smooth", "반응 부드러움" },
        { "snapAngle", "단계 회전 각도" },
        { "snapDeadzone", "단계 회전 입력 기준" },
        { "snapTurnEnabled", "단계 회전 사용" },
        { "spatial", "공간 음향 사용" },
        { "spawnExtents", "생성 영역의 반쪽 크기" },
        { "specificNdiSender", "영상 전송 개체" },
        { "speechBubble", "말풍선" },
        { "speed", "이동 속도" },
        { "speedJitter", "속도 변화 범위" },
        { "spinSpeed", "자전 속도" },
        { "spline", "이동 곡선" },
        { "splineCart", "곡선 이동 장치" },
        { "squeezeScale", "수축 후 크기 비율" },
        { "stage", "단계 정보" },
        { "stageId", "단계 식별자" },
        { "stages", "단계 목록" },
        { "startHidden", "처음에는 숨기기" },
        { "startLocal", "시점 상대 위치" },
        { "stopOnExit", "벗어나면 설명 정지" },
        { "stops", "정차 지점" },
        { "subtitleText", "자막 표시 영역" },
        { "summary", "요약 설명" },
        { "swarm", "부유 개체 무리" },
        { "talkingBool", "말하기 애니메이션 변수" },
        { "targetB", "따라다닐 화면" },
        { "targetIP", "대상 네트워크 주소" },
        { "targetOrgan", "대상 장기" },
        { "targetTags", "반응할 대상 표식" },
        { "teleportEnabled", "순간 이동 사용" },
        { "text", "문구" },
        { "thresholds", "단계별 시작 진행도" },
        { "throatPoint", "목구멍 지점" },
        { "timePerText", "문장별 표시 시간" },
        { "titleText", "제목 표시 영역" },
        { "tongue", "혀" },
        { "tongueLiftAngle", "혀 들림 각도" },
        { "tongueLiftSeconds", "혀를 드는 시간" },
        { "transform", "개체 위치" },
        { "travelSeconds", "전체 이동 시간" },
        { "ttsClip", "안내 음성" },
        { "tubeLength", "관 길이" },
        { "tubeRadius", "관 안쪽 반지름" },
        { "uiFadePanel", "화면 전환 덮개" },
        { "upperJaw", "아래턱 회전축(턱관절)" },
        { "useFade", "부드러운 화면 전환 사용" },
        { "uvula", "목젖" },
        { "uvulaLiftAngle", "목젖 들림 각도" },
        { "virusModels", "미생물 모형 목록" },
        { "voice", "음성" },
        { "voiceLine", "첫 응시 안내" },
        { "volume", "음량" },
        { "waitSeconds", "대기 시간" },
        { "walls", "수축하는 벽" },
        { "wobbleRadius", "흔들림 반경" },
        { "wobbleSpeed", "흔들림 속도" },
        { "wrapAround", "영역 반대편에서 다시 등장" },
        { "yOffset", "높이 보정" },
        { "yawOnly", "수직 축으로만 회전" },
        { "infoPanel", "설명판" },
        { "infoText", "설명 표시 영역" },
        { "description", "설명 내용" },
        { "remoteHighlight", "원격 강조 대상" },
        { "target", "따라갈 대상" },
        { "moveSpeed", "이동 속도" },
        { "forwardDistance", "앞쪽 거리" },
        { "sideDistance", "옆쪽 거리" },
        { "minDistance", "최소 거리" },
        { "rotationCorrection", "회전 보정" },
        { "raycastDistance", "시선 검사 거리" },
        { "raycastFrequency", "초당 시선 검사 횟수" },
        { "remoteIP", "원격 네트워크 주소" },
        { "loadDelay", "장면 전환 지연" },
        { "infoPanelObject", "설명판 개체" },
        { "explanationTextComponent", "설명 표시 영역" },
        { "organExplanation", "기관 설명" },
        { "remoteHighlightObject", "원격 강조 대상" },
        { "forwardOffset", "앞쪽 거리" },
        { "rightOffset", "오른쪽 거리" },
        { "stopDistance", "정지 거리" },
        { "interactableTag", "상호작용 대상 표식" },
        { "loadDelayTime", "장면 전환 지연 시간" },
    };
    public static readonly Dictionary<string, string> Types = new Dictionary<string, string>
    {
        { "DisplayInfo", "설명 표시" },
        { "NPCController", "안내 캐릭터 제어" },
        { "NPCInteractionBridge", "안내 캐릭터 상호작용 연결" },
        { "RaycastInteractor", "시선 상호작용" },
        { "VRGazeInteractor", "가상현실 시선 상호작용" },
        { "BoatInteraction", "보트 탑승" },
        { "SimpleTrainMover", "경로 이동" },
        { "AutoDoor", "자동 문" },
        { "AutoRotate", "자동 회전" },
        { "FluidBobber", "소화액 움직임" },
        { "DescentController", "하강 연출" },
        { "Billboard", "시선 방향 표시판" },
        { "CapsuleRide", "탐험 캡슐" },
        { "ChewingSequence", "씹기 연출" },
        { "ComfortLocomotion", "편안한 이동" },
        { "TeleportArea", "이동 가능 바닥" },
        { "ComfortVignette", "멀미 완화 화면" },
        { "EnzymeCutSequence", "효소 절단 연출" },
        { "ExhibitStation", "전시물 안내" },
        { "FloatingSwarm", "부유 개체 무리" },
        { "GazeSelector", "시선 선택" },
        { "JourneyCharacterMotion", "캐릭터 관절 동작" }, { "JourneyDissolve", "음식 표면 분해" }, { "LiquidContactSplash", "액체 접촉 물방울" },
        { "GuideNarrator", "안내 음성 및 자막" },
        { "Haptics", "진동 효과" },
        { "JourneyManager", "탐험 진행 관리" },
        { "NarrationZone", "구역 안내" },
        { "Nutrient", "영양소" },
        { "NutrientAbsorption", "영양소 흡수 체험" },
        { "OrganStage", "탐험 단계 정보" },
        { "PartLabel", "부위 이름표" },
        { "PeristalsisPulse", "기관 수축" },
        { "PeristalsisWave", "연동 운동" },
        { "PersistentBootstrap", "공통 개체 불러오기" },
        { "QuestSettings", "가상현실 기기 최적화" },
        { "RideProgressSwitcher", "탑승 구간 전환" },
        { "Rotator", "회전 및 부유" },
        { "SceneAmbience", "환경 소리" },
        { "SphincterGate", "괄약근 문" },
        { "SplineFlow", "곡선을 따라 흐르기" },
        { "StageExitPortal", "다음 단계 입구" },
        { "StomachChurn", "위 소화 연출" },
        { "SwallowSequence", "음식 삼킴 관찰" },
        { "Sway", "흔들림" },
        { "TheaterEnding", "상영관 마무리" },
        { "WallAbsorption", "장벽 수분 흡수" },
        { "FloatingFollower", "떠다니며 따라오기" },
        { "NPCUI", "안내 화면 따라오기" },
        { "NPCUILook", "거리별 안내 화면" },
        { "SceneController", "장면 이동" },
        { "VirusGroupController", "미생물 무리 제어" },
        { "VRPlayerManager", "가상현실 플레이어 관리" },
        { "OSCBodyManager", "원격 몸동작 통신" },
        { "OSCManager", "원격 상호작용 통신" },
        { "SceneChanger", "장면 전환" },
        { "SceneLoader", "장면 불러오기" },
        { "SceneTransitionCleaner", "장면 전환 정리" },
        { "AutoGuideTrigger", "장기 설명 시작 구역" },
        { "ChickSpeechTrigger", "병아리 음성 시작 구역" },
        { "DoctorController", "박사 설명 제어" },
        { "LocationTrigger", "장소별 안내" },
    };
    static readonly Dictionary<string, string> Ids = new Dictionary<string, string>
    {
        {"Lobby", "로비"}, {"Mouth", "입"}, {"Esophagus", "식도"}, {"Stomach", "위"},
        {"SmallIntestine", "소장"}, {"LargeIntestine", "대장"}, {"Theater", "상영관"},
        {"Liver", "간"}, {"Heart", "심장"}, {"Lung", "폐"}, {"Lungs", "폐"},
        {"Glucose", "포도당"}, {"AminoAcid", "아미노산"}, {"FattyAcid", "지방산"},
        {"gurgle", "소화음"}, {"pulse", "맥박음"},
        {"00_Lobby", "로비 장면"}, {"01_Mouth", "입 장면"}, {"02_Esophagus", "식도 장면"},
        {"03_Stomach", "위 장면"}, {"04_SmallIntestine", "소장 장면"},
        {"05_LargeIntestine", "대장 장면"}, {"06_Theater", "상영관 장면"}
    };

    public static string Label(SerializedProperty p)
    {
        if (p.propertyType == SerializedPropertyType.ArraySize) return "개수";
        if (p.name.StartsWith("data[", StringComparison.Ordinal))
        {
            int start = p.name.IndexOf('[') + 1;
            if (int.TryParse(p.name.Substring(start).TrimEnd(']'), out int index)) return "항목 " + (index + 1);
        }
        return Fields.TryGetValue(p.name, out string label) ? label : p.displayName;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        float width = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Max(165, EditorGUIUtility.currentViewWidth * 0.48f);
        try
        {
            if (Types.TryGetValue(target.GetType().Name, out string title))
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var p = serializedObject.GetIterator();
            if (p.NextVisible(true))
            {
                do
                {
                    if (p.name == "m_Script") continue;
                    Draw(p.Copy());
                } while (p.NextVisible(false));
            }
            serializedObject.ApplyModifiedProperties();
        }
        finally { EditorGUIUtility.labelWidth = width; }
    }

    static void Draw(SerializedProperty p)
    {
        var label = new GUIContent(Label(p), p.tooltip);
        // 사용자가 내부 식별자까지 고치지 않아도 익숙한 장기 이름으로 고를 수 있다.
        string[] keys = null;
        if (p.propertyType == SerializedPropertyType.String)
        {
            if (p.name == "clipName") keys = new[] { "gurgle", "pulse" };
            else if (p.name == "stageId" || p.name == "overrideStageId" || p.name == "targetOrgan" || p.name == "oscOrganId")
                keys = new[] { "Lobby", "Mouth", "Esophagus", "Stomach", "SmallIntestine", "LargeIntestine", "Theater", "Liver", "Heart", "Lungs" };
            else if (p.name == "sceneName" || p.name == "nextSceneName")
                keys = new[] { "00_Lobby", "01_Mouth", "02_Esophagus", "03_Stomach", "04_SmallIntestine", "05_LargeIntestine", "06_Theater" };
        }
        if (keys != null)
        {
            keys = new[] { "" }.Concat(keys).ToArray();
            int current = Array.IndexOf(keys, p.stringValue);
            var options = keys.Select(k => new GUIContent(k.Length == 0 ? "지정 안 함" : Ids[k])).Concat(new[] { new GUIContent("직접 입력") }).ToArray();
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, label, p);
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(rect, label, p.isExpanded || current < 0 ? keys.Length : current, options);
            if (EditorGUI.EndChangeCheck())
            {
                p.isExpanded = next == keys.Length;
                if (next < keys.Length) p.stringValue = keys[next];
            }
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
            if (next == keys.Length) EditorGUILayout.PropertyField(p, new GUIContent("내부 연결값", "기존 자료와 연결되는 식별자입니다."));
            return;
        }
        if (p.name == "foveation" && p.propertyType == SerializedPropertyType.Enum)
        {
            var names = new Dictionary<string, string> { {"Off", "끄기"}, {"Low", "낮음"}, {"Medium", "보통"}, {"High", "높음"}, {"HighTop", "높음 (상단 우선)"} };
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, label, p);
            EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.Popup(rect, label, p.enumValueIndex, p.enumNames.Select(n => new GUIContent(names.TryGetValue(n, out var ko) ? ko : n)).ToArray());
            if (EditorGUI.EndChangeCheck()) p.enumValueIndex = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
            return;
        }
        // 이벤트 연결기는 유니티 기본 편집기를 사용해야 기존 함수 연결 및 되돌리기가 유지된다.
        bool eventField = p.propertyType == SerializedPropertyType.Generic && p.FindPropertyRelative("m_PersistentCalls") != null;
        if (eventField || p.propertyType != SerializedPropertyType.Generic)
        {
            EditorGUILayout.PropertyField(p, label, true);
            return;
        }
        bool expanded = EditorGUILayout.PropertyField(p, label, false);
        if (!expanded) return;
        EditorGUI.indentLevel++;
        try
        {
            var child = p.Copy();
            var end = p.GetEndProperty();
            if (child.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    Draw(child.Copy());
                } while (child.NextVisible(false));
            }
        }
        finally { EditorGUI.indentLevel--; }
    }
}

[CustomEditor(typeof(DisplayInfo)), CanEditMultipleObjects]
public class DisplayInfoKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NPCController)), CanEditMultipleObjects]
public class NPCControllerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NPCInteractionBridge)), CanEditMultipleObjects]
public class NPCInteractionBridgeKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(RaycastInteractor)), CanEditMultipleObjects]
public class RaycastInteractorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(VRGazeInteractor)), CanEditMultipleObjects]
public class VRGazeInteractorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(BoatInteraction)), CanEditMultipleObjects]
public class BoatInteractionKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SimpleTrainMover)), CanEditMultipleObjects]
public class SimpleTrainMoverKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(AutoDoor)), CanEditMultipleObjects]
public class AutoDoorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(AutoRotate)), CanEditMultipleObjects]
public class AutoRotateKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(FluidBobber)), CanEditMultipleObjects]
public class FluidBobberKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(DescentController)), CanEditMultipleObjects]
public class DescentControllerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(Billboard)), CanEditMultipleObjects]
public class BillboardKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(CapsuleRide)), CanEditMultipleObjects]
public class CapsuleRideKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(ChewingSequence)), CanEditMultipleObjects]
public class ChewingSequenceKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(ComfortLocomotion)), CanEditMultipleObjects]
public class ComfortLocomotionKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(ComfortVignette)), CanEditMultipleObjects]
public class ComfortVignetteKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(EnzymeCutSequence)), CanEditMultipleObjects]
public class EnzymeCutSequenceKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(ExhibitStation)), CanEditMultipleObjects]
public class ExhibitStationKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(FloatingSwarm)), CanEditMultipleObjects]
public class FloatingSwarmKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(GazeSelector)), CanEditMultipleObjects]
public class GazeSelectorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(GuideNarrator)), CanEditMultipleObjects]
public class GuideNarratorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(Haptics)), CanEditMultipleObjects]
public class HapticsKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(JourneyManager)), CanEditMultipleObjects]
public class JourneyManagerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NarrationZone)), CanEditMultipleObjects]
public class NarrationZoneKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(Nutrient)), CanEditMultipleObjects]
public class NutrientKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NutrientAbsorption)), CanEditMultipleObjects]
public class NutrientAbsorptionKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(OrganStage)), CanEditMultipleObjects]
public class OrganStageKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(PartLabel)), CanEditMultipleObjects]
public class PartLabelKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(PeristalsisPulse)), CanEditMultipleObjects]
public class PeristalsisPulseKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(PeristalsisWave)), CanEditMultipleObjects]
public class PeristalsisWaveKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(PersistentBootstrap)), CanEditMultipleObjects]
public class PersistentBootstrapKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(QuestSettings)), CanEditMultipleObjects]
public class QuestSettingsKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(RideProgressSwitcher)), CanEditMultipleObjects]
public class RideProgressSwitcherKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(Rotator)), CanEditMultipleObjects]
public class RotatorKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SceneAmbience)), CanEditMultipleObjects]
public class SceneAmbienceKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SphincterGate)), CanEditMultipleObjects]
public class SphincterGateKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SplineFlow)), CanEditMultipleObjects]
public class SplineFlowKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(StageExitPortal)), CanEditMultipleObjects]
public class StageExitPortalKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(StomachChurn)), CanEditMultipleObjects]
public class StomachChurnKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SwallowSequence)), CanEditMultipleObjects]
public class SwallowSequenceKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(Sway)), CanEditMultipleObjects]
public class SwayKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(TheaterEnding)), CanEditMultipleObjects]
public class TheaterEndingKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(WallAbsorption)), CanEditMultipleObjects]
public class WallAbsorptionKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(FloatingFollower)), CanEditMultipleObjects]
public class FloatingFollowerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NPCUI)), CanEditMultipleObjects]
public class NPCUIKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(NPCUILook)), CanEditMultipleObjects]
public class NPCUILookKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SceneController)), CanEditMultipleObjects]
public class SceneControllerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(VirusGroupController)), CanEditMultipleObjects]
public class VirusGroupControllerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(VRPlayerManager)), CanEditMultipleObjects]
public class VRPlayerManagerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(OSCBodyManager)), CanEditMultipleObjects]
public class OSCBodyManagerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(OSCManager)), CanEditMultipleObjects]
public class OSCManagerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SceneChanger)), CanEditMultipleObjects]
public class SceneChangerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SceneLoader)), CanEditMultipleObjects]
public class SceneLoaderKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(SceneTransitionCleaner)), CanEditMultipleObjects]
public class SceneTransitionCleanerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(AutoGuideTrigger)), CanEditMultipleObjects]
public class AutoGuideTriggerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(ChickSpeechTrigger)), CanEditMultipleObjects]
public class ChickSpeechTriggerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(DoctorController)), CanEditMultipleObjects]
public class DoctorControllerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(LocationTrigger)), CanEditMultipleObjects]
public class LocationTriggerKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(JourneyCharacterMotion)), CanEditMultipleObjects]
public class JourneyCharacterMotionKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(JourneyDissolve)), CanEditMultipleObjects]
public class JourneyDissolveKoreanEditor : KoreanJourneyEditor { }
[CustomEditor(typeof(LiquidContactSplash)), CanEditMultipleObjects]
public class LiquidContactSplashKoreanEditor : KoreanJourneyEditor { }
#endif
