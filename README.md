# 인체탐험 VR 프로젝트 (HUMANBODY)

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

## 주요 기능

### 1. 가이드 NPC (병아리 의사)
- `NPCController` — 플레이어 시선 옆에 일정 거리(`forwardOffset`, `rightOffset`)로 따라오는 싱글톤 NPC
- `FloatingFollower` — 부드러운 부유/회전(`Lerp`), VR `CenterEyeAnchor` 자동 추적
- `NPCUI` / `NPCUILook` — World Space UI 캔버스가 항상 카메라를 향하도록 회전
- `DoctorController` — 기관별 음성 클립(`Resources/Explain_{OrganName}.mp3`)을 spatial audio로 재생

### 2. 씬 전환 시스템
- `SceneChanger` — 트리거 진입 시 OVRScreenFade + UI 패널 페이드 → 비동기 씬 로드
- `SceneLoader` / `SceneTransitionCleaner` — 씬 간 자원 정리
- `VRPlayerManager` — `DontDestroyOnLoad` 싱글톤 플레이어가 씬 전환 시 `SpawnPoint`로 자동 위치 보정

### 3. 보트 이동 (소화기관 내부 여행)
- `BoatInteraction` — VR A/X 버튼(또는 PC `E`)으로 탑승/하차, 플레이어·NPC 동시 SetParent
- `SimpleTrainMover` + Cinemachine `SplineCart` — 식도→위→장 경로를 따라 자동 이동
- 거리 기반 상호작용(`interactionDistance`)으로 자연스러운 탑승 트리거

### 4. 환경 인터랙션
- `AutoDoor` / `AutoRotate` — 트리거 기반 자동 문, 회전 오브젝트
- `FluidBobber` — 위액(GastricFluid) 표면 위 부유 효과
- `DescentTransition` — 식도 하강 연출
- `VirusGroupController` — 다수 바이러스 오브젝트의 개별 속도·축·범위 무작위화로 자연스러운 군집 움직임

### 5. 가이드 트리거 / 위치 기반 설명
- `AutoGuideTrigger` — `VRCamera` 태그가 콜라이더 진입 시 해당 기관(`targetOrgan`) 설명 자동 재생
- `LocationTrigger` / `ChickSpeechTrigger` — 위치별 NPC 대사·SFX 트리거
- `RaycastInteractor` / `VRGazeInteractor` — 시선/광선 기반 전시품 선택
- `DisplayInfo` — 선택된 기관의 정보 패널 표출

### 6. 외부 연동 (OSC / NDI)
- `OSCManager` — `/interaction/{organID}` 주소로 다른 사용자/시연 컨트롤러와 관람 상태 동기화
- `OSCBodyManager` — 외부 모션캡처/바디 트래킹 데이터 수신용 인터페이스

---

## 씬 구성

```
Assets/Scenes/
├── MouseScene.unity      입 / 식도 진입
├── OrganScene.unity      위장 (로비, 메인 전시관)
├── SmallIntestine.unity  소장
└── Toilet.unity          종착점 / 마무리 연출
```

진행 순서: `MouseScene` → `OrganScene` → `SmallIntestine` → `Toilet`

---

## 폴더 구조

```
HUMANBODY/
├── Assets/
│   ├── Scenes/            메인 씬 4종
│   ├── Scripts/           게임 로직 (NPC, Scene, Boat, Door, OSC 등)
│   ├── Prefabs/           재사용 오브젝트
│   ├── Character/         캐릭터 모델/애니메이션
│   ├── Enviroment/        배경 환경 에셋
│   ├── Boats/             보트/스플라인 에셋
│   ├── NamuFX/            VFX 라이브러리
│   ├── Sounds/            BGM / SFX / 내레이션
│   ├── KoreanTyper/       한글 타이핑 UI 효과
│   ├── XR / XRI /         Meta XR / Unity XRI 설정
│   ├── MetaXR / Oculus/   Meta Quest 통합
│   ├── NDI/               영상 송출
│   ├── Settings/          URP 렌더러/볼륨 프로파일
│   └── Resources/         런타임 로딩용 (음성 클립 등)
├── Packages/              UPM 의존성
├── ProjectSettings/       Unity 프로젝트 세팅
└── README.md
```

---

## 빌드 방법

### Meta Quest (Android)
1. Unity Hub에서 `6000.2.1f1` 에디터 + **Android Build Support** 설치
2. 프로젝트 열기 → `File > Build Profiles` → **Android** 선택
3. `XR Plug-in Management` → **Oculus** 체크
4. Quest를 USB로 연결 (개발자 모드 활성화)
5. `Build And Run` → `.apk` 자동 설치

### PC (Windows / Link 모드)
1. Build Profile을 **Windows / Mac / Linux** 로 전환
2. Quest를 PC에 Quest Link 또는 Air Link로 연결
3. `Build And Run`

---

## 콘텐츠 진행 흐름

```
[VR 기기 접속]
        ↓
[로비 입장] → 병아리 가이드 등장 / 안내판 표시
        ↓
[입(MouseScene) 진입] → 소화 시작 설명 / 보트 탑승
        ↓
[위(OrganScene)] → 위액 표면 효과 / 가스 / 음식 분해 시연
        ↓
[소장(SmallIntestine)] → 영양소 흡수 / 바이러스 인터랙션
        ↓
[대장 / Toilet] → 마무리 연출
        ↓
[360 영상 상영관] → AI 생성 내레이션 영상 재생
        ↓
[종료 / 로비 복귀]
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
