# 인체탐험 VR 프로젝트 (HUMANBODY)

플레이어는 병아리 의사 삐약이와 함께 음식의 이동 경로를 따라가는 작은 탐험가다. 입과 위에서는 주변을 둘러보고, 식도·소장·대장에서는 탐험 캡슐을 타며 소화와 흡수를 관찰한다. [설정과 대사 기준](Tools/EXPLORER_PERSPECTIVE.md)

> AI × VR 인체 탐험관 — 사용자가 직접 소화기관 내부를 체험하며 인체 구조를 배우는 Meta Quest VR 콘텐츠

![Unity](https://img.shields.io/badge/Unity-6000.2.1f1-black?logo=unity)
![Platform](https://img.shields.io/badge/Platform-Meta%20Quest%20%7C%20PC-blue)
![Render Pipeline](https://img.shields.io/badge/URP-Universal%20RP-orange)
![Language](https://img.shields.io/badge/C%23-.NET-purple)

---

## 프로젝트 소개

기존 VR 박물관은 전시품의 시각 정보와 텍스트 설명을 일방적으로 제공하는 데 그쳐 몰입감과 학습 효과가 떨어진다는 한계가 있었습니다. 본 프로젝트는 **생성형 AI**와 **VR**을 결합하여, 관람자가 직접 **인체 소화기관 내부를 여행**하며 능동적으로 정보를 체험할 수 있는 교육 콘텐츠로 설계되었습니다.

병아리 의사(NPC)가 가이드로 따라다니며 각 기관별로 음성·UI 설명을 제공하고, 사용자는 **보트를 타고 식도→위→소장→대장**으로 이어지는 소화 경로를 따라 이동합니다. 마지막 단계에서는 AI 생성 이미지/영상이 360도 상영관에서 재생되어 학습 내용을 정리해 줍니다.

---

## 개발 환경

| 항목 | 내용 |
|---|---|
| Engine | **Unity 6000.2.1f1** (Unity 6) |
| Render Pipeline | URP (Universal Render Pipeline) |
| Target Platform | **Meta Quest 2/3** (Android) + PC 빌드 |
| XR SDK | Meta XR SDK / Oculus Integration / Unity XRI |
| Input | OVRInput, Unity Input System |
| Language | C# |
| Networking | OSC + NDI (다중 사용자 상태 동기화 / 영상 송출) |
| 주요 패키지 | Cinemachine, KinoBloom, NamuFX, ToonyColorsPro, KoreanTyper |

---

## 주요 기능 (2026-09 리뉴얼)

### 여정 구조 — 소화 경로 풀코스
```
00_Lobby → 01_Mouth → 02_Esophagus → 03_Stomach → 04_SmallIntestine → 05_LargeIntestine → 06_Theater
```
- `JourneyManager` — `OrganStage`(ScriptableObject) 순서만 보고 다음 씬으로 전환. 페이드/스폰/입장·퇴장 내레이션 자동 처리
- `PersistentBootstrap` — 매니저·플레이어·병아리 가이드를 담은 영속 프리팹을 어느 씬에서 시작해도 생성

### 가이드 & 설명
- `GuideNarrator` — 병아리 의사의 음성(edge-tts ko-KR) + 말풍선 자막(KoreanTyper 타이핑) 동기 재생
- `PartLabel` — 씬 안의 모든 파츠(혀, 윗니, 융털, 적혈구…)에 이름표 + 한 줄 설명. `GazeSelector`(시선)로 활성화
- `NarrationZone` / `StageExitPortal` — 구역 진입 내레이션, 출구 포털
- `ExhibitStation` — 로비 전시대 정보 패널(요약 + 알아두기)

### 씬별 연출
| 씬 | 파츠 | 애니메이션 |
|---|---|---|
| 입 | 혀·윗니·아랫니·목젖·후두덮개·침샘 3쌍·식괴 | `ChewingSequence`(턱 씹기, 침 분비) → `SwallowSequence`(혀가 식괴를 보내는 모습을 관찰한 뒤 탐험가는 화면 전환으로 식도에 이동) |
| 식도 | 근육 터널·연동파 링·분문 | `PeristalsisWave`(수축 링 하강), `SphincterGate`(분문 개폐), `CapsuleRide` 정차 설명 |
| 위 | 위벽 주름·위샘 3종·점액층·펩신·단백질 사슬·유문 | `StomachChurn`(음식→미즙), `EnzymeCutSequence`(펩신 절단), 유문 열림 → `SplineFlow` 미즙 배출 |
| 소장 | 십이지장 합류부·융털 타일·**융털 단면(모세혈관+적혈구 흐름, 유미관+지방산)**·영양소 3종 | `SplineFlow` 혈류, `NutrientAbsorption` 응시 흡수 미니게임 |
| 대장 | 팽대 벽·유익균/유해균·식이섬유·물방울·찌꺼기 3단계·충수 | `FloatingSwarm`, `WallAbsorption`(수분 흡수), `RideProgressSwitcher`(묽음→고형) |
| 상영관 | 소화계 홀로그램 | 경로 따라 흐르는 빛 + 영상 → `TheaterEnding` → 로비 복귀 |

### 에셋 파이프라인
- **3D 모델**: Meshy text-to-3D (`Tools/meshy.py`, 매니페스트 `Tools/meshy_manifest*.json`) → `Assets/Models/Meshy/*.glb` (glTFast 임포트)
- **내레이션**: `Assets/Data/journey_content.json` 대본 → `Tools/make_narration.py`(edge-tts) → `Assets/Sounds/Narration/*.mp3`
- **씬 자동 조립**: Unity 메뉴 `HumanBody/전체 빌드` (`JourneyBuilder`) — 데이터 임포트 → 7개 씬 생성 → Build Settings 등록. 모델 크기는 바운즈 기준으로 자동 정렬(Fit/Ground/AlignLongest)

### 외부 연동 (OSC / NDI)
- `OSCManager` — `/interaction/{organID}` 주소로 다른 사용자/시연 컨트롤러와 관람 상태 동기화
- `OSCBodyManager` — 외부 모션캡처/바디 트래킹 데이터 수신용 인터페이스

---

## 씬 구성

```
Assets/Scenes/Journey/
├── 00_Lobby.unity           전시관 로비 (전시대 6개, 홀로그램, 입 모양 입구)
├── 01_Mouth.unity           입 (씹기 → 삼킴)
├── 02_Esophagus.unity       식도 (캡슐 하강 라이드)
├── 03_Stomach.unity         위 (휘젓기, 효소 절단, 유문)
├── 04_SmallIntestine.unity  소장 (라이드 + 융털 확대 + 흡수 게임)
├── 05_LargeIntestine.unity  대장 (세균, 수분 흡수, 찌꺼기)
└── 06_Theater.unity         정리 상영관
Assets/Scenes/                (구 버전: MouseScene / OrganScene / SmallIntestine / Toilet)
```

---

## 폴더 구조

```
HUMANBODY/
├── Assets/
│   ├── Scenes/Journey/    새 여정 씬 7종 (자동 생성)
│   ├── Scripts/Journey/   여정 로직 (JourneyManager, GuideNarrator, PartLabel, 연출 스크립트, Editor/JourneyBuilder)
│   ├── Scripts/           구 버전 스크립트 (구 씬 호환용)
│   ├── Data/              journey_content.json, Stages/(OrganStage), Volumes/, Generated/(프리팹·머티리얼)
│   ├── Models/Meshy/      Meshy 생성 GLB 모델
│   ├── Sounds/Narration/  TTS 내레이션
│   ├── Character/ Enviroment/  구 Sketchfab 모델 (구 씬용)
│   ├── XR / XRI / MetaXR / Oculus   Meta Quest 통합
│   └── Resources/         런타임 로딩용
├── Tools/                 meshy.py, make_narration.py, 매니페스트
├── Packages/              UPM 의존성
└── ProjectSettings/
```

---

## 빌드 방법

### 씬 생성 (최초 1회 / 대본·모델 변경 시)
1. Unity 메뉴 **HumanBody → 전체 빌드**
2. `Assets/Scenes/Journey/00_Lobby` 열고 Play (중간 씬에서 Play 해도 동작)

### Meta Quest (Android)
1. Unity Hub에서 `6000.2.1f1` 에디터 + **Android Build Support** 설치
2. `File > Build Profiles` → **Android** 선택, `XR Plug-in Management` → **Oculus** 체크
3. Quest를 USB로 연결 (개발자 모드) → `Build And Run`

### PC (Quest Link)
1. Build Profile을 **Windows** 로 전환, Quest Link/Air Link 연결 후 `Build And Run`

---

## 콘텐츠 진행 흐름

```
[로비] 전시대 응시 → 정보 패널 / 병아리 인트로 → 입 모양 입구
   ↓
[입] 혀 위에 서서 치아·침샘 관찰 → 음식 씹기·삼킴 관찰 → 안내 후 화면 전환으로 식도에 이동
   ↓
[식도] 캡슐 하강, 연동파 링 관찰, 분문 열림
   ↓
[위] 위산·위샘·점액층, 펩신이 단백질 절단, 음식 → 미즙, 유문 열림
   ↓
[소장] 캡슐 라이드, 십이지장 분비, 융털 확대(적혈구 흐름), 영양소 흡수 게임
   ↓
[대장] 유익균/유해균, 수분 흡수, 찌꺼기 굳어짐
   ↓
[상영관] 홀로그램 경로 복습 → 영상 → 로비 복귀
```

---

## 기술적 도전 과제

- **씬 간 플레이어 영속화** — `VRPlayerManager` 싱글톤 + `SpawnPoint` 자동 정렬로 OVRPlayerController 재초기화 문제 해결
- **VR 페이드 동기화** — `OVRScreenFade` + UI Canvas 페이드 병행으로 깜빡임 제거
- **보트 탑승 좌표 처리** — `SetParent` 시 `CharacterController` 일시 비활성화로 콜리전 충돌 회피
- **다중 사용자 관람 상태 공유** — OSC 프로토콜로 다른 헤드셋에 현재 관람 중인 기관 브로드캐스트
- **그룹 오브젝트 자연 움직임** — 각 바이러스의 축/속도/범위/회전 방향을 모두 랜덤화하여 인공적 패턴 제거

---

## 시연 영상

[유튜브 시연 영상 보기](https://www.youtube.com/watch?v=oTahLEX3NXo)

---

## 개발자

- **오정훈** ([@jeonghun267](https://github.com/jeonghun267))
- 소속: PolyTech

---

## 라이선스

본 프로젝트는 학습/포트폴리오 목적으로 공개됩니다. 일부 에셋(Meta XR SDK, ToonyColorsPro, NamuFX, Yughues Free Fur Materials, Sketchfab 모델 등)은 각 라이선스를 따릅니다.
