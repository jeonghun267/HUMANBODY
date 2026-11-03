> **탐험가 설정 통일:** 플레이어는 작아진 탐험가이며 삐약이와 함께 음식의 이동 경로를 관찰한다. 입의 음식 삼킴 뒤에는 플레이어를 끌어당기지 않고 안내와 화면 전환으로 식도에 이동한다. 최신 설정과 대사는 [설정 기준](Tools/EXPLORER_PERSPECTIVE.md)을 따른다.

> **2026-09-22 후속 점검:** 캡슐/영속 오브젝트, 내레이션 완료, 텔레포트 수명, 초기 씬 인트로를 수정했다. 회귀 13개와 실제 7개 씬의 가속 흐름 29개를 검사했다. 상세 결과·재실행 방법·검증 한계는 [Tools/VALIDATION.md](Tools/VALIDATION.md) 참고. 아래 내용은 최초 인수인계 당시의 기록이며, 현재 검증 상태는 후속 보고서를 우선한다. Quest 실기·시각/음성 품질은 여전히 미검증이다.
# HUMANBODY 인수인계 문서 (2026-09-22)

> 인체탐험 VR(Meta Quest) — 소화기관 여정 리뉴얼. 이 문서 하나로 **현재 상태, 만든 것, 쓰는 법, 안 된 것, 다음 할 일**을 모두 파악할 수 있게 정리했다.

---

## 0. 30초 요약

- 기존 4개 씬(MouseScene/OrganScene/SmallIntestine/Toilet)을 **로비 → 입 → 식도 → 위 → 소장 → 대장 → 상영관 7개 씬**으로 전면 재구성.
- 모든 3D 에셋을 **Meshy AI 생성 모델 54개**로 교체(파츠 단위: 혀·윗니·아랫니·목젖·후두덮개·침샘·괄약근·위샘·펩신·융털 단면·모세혈관·유미관·세균·찌꺼기 3단계 …).
- **내레이션 61줄**(대본 JSON → edge-tts 한국어 음성) + 파츠별 응시 설명 + 병아리 의사 가이드.
- 씬은 손으로 만들지 않고 **에디터 스크립트(`JourneyBuilder`)가 자동 조립**한다. 대본/모델을 바꾸면 메뉴 한 번으로 7개 씬이 재생성된다.
- 검증 상태: **컴파일 OK, 씬 생성 OK, 에디터 카메라 캡처로 배치/조명 검수 OK, 겹침 자동검사 OK.** 단, **Play 모드 끝까지 실제 플레이 검증은 아직 안 됨** (원격 작업 + Unity 에디터 hang 문제로 불가).
- 모든 변경은 **아직 커밋되지 않았다** (`git status` 로 21개 항목). 첫 작업으로 커밋 권장.

---

## 1. 폴더/파일 맵 (이번에 새로 만든 것)

```
HUMANBODY/
├── HANDOFF.md                     ← 이 문서
├── README.md                      ← 새 구조로 갱신됨
├── Assets/
│   ├── Data/
│   │   ├── journey_content.json   ★ 대본(인트로/아웃트로/파츠 설명/요약/알아두기) — 내용 수정은 여기서
│   │   ├── Stages/Stage_*.asset   OrganStage ScriptableObject 7개 (JSON에서 자동 생성, 직접 수정 X)
│   │   ├── Volumes/Volume_*.asset URP 볼륨 프로파일(블룸/비네트/톤매핑) 씬별
│   │   └── Generated/             JourneyPersistent.prefab(매니저+플레이어+가이드), 머티리얼, RT (자동 생성)
│   ├── Models/Meshy/*.glb         Meshy 생성 모델 54개 (glTFast 임포트)
│   ├── Scenes/Journey/0*_*.unity  ★ 자동 생성 씬 7개 (직접 편집해도 되지만 재빌드하면 덮어써짐)
│   ├── Scripts/Journey/           ★ 새 런타임 스크립트 30개 (아래 3장)
│   │   └── Editor/                JourneyBuilder(씬 조립), JourneyAutoBuild(플래그 트리거), JourneyCapture(검수 캡처), JourneyOverlapReport(겹침 검사)
│   ├── Sounds/Narration/*.mp3     TTS 음성 61개 (이름 규칙 = {stageId}_{key}_{n} / {stageId}_part_{id})
│   ├── Scripts/  (구 스크립트)     구 씬 호환용으로 유지. 새 씬은 사용 안 함
│   └── Scenes/*.unity (구 씬 4개)  Build Settings 에서 빠짐. 참고용
├── Tools/
│   ├── meshy.py                   Meshy API 클라이언트 (balance / text / image / batch)
│   ├── meshy_manifest*.json       생성한 모델 프롬프트 목록 (v1 29개, parts 26개, fix 4개)
│   ├── make_narration.py          journey_content.json → mp3 (edge-tts)
│   ├── compile_check.py           Unity 없이 dotnet build 로 컴파일 검증
│   ├── meshy_thumbs.py            Meshy 썸네일 contact sheet
│   ├── captures/*.png             씬별 6방향 검수 캡처 (최신)
│   ├── overlap_report.txt         겹침 검사 결과 (최신)
│   ├── rejected/                  품질 불량으로 뺀 Meshy 모델 5개 (재사용 가능)
│   ├── manifest.backup.json       패키지 제거 전 manifest 백업
│   └── unity_*.log                배치모드 실행 로그
└── Packages/manifest.json         com.unity.ai.assistant / ai.generators / 2d.enhancers 제거됨 (7장 참고)
```

---

## 2. 도구 사용법 (반드시 읽을 것)

### 2.1 씬 만들기 / 다시 만들기
Unity 메뉴 **HumanBody → 전체 빌드 (데이터 → 씬 → Build Settings)**
1. `journey_content.json` → `Stage_*.asset` 갱신 (음성 mp3 자동 연결)
2. 영속 프리팹 생성 → 7개 씬 조립 → 저장
3. Build Settings 에 7개 씬 등록 (기존 목록 대체)

개별 메뉴: `1. 스테이지 데이터 임포트`, `2. 여정 씬 생성`, `3. Build Settings 등록`, `씬 캡처`, `겹침 검사`.

**Unity 를 열지 않고(배치모드)** 돌리려면 Unity 가 닫힌 상태에서:
```bash
"/c/Program Files/Unity/Hub/Editor/6000.2.1f1/Editor/Unity.exe" -batchmode -projectPath "C:/Users/admin_14/HUMANBODY" -executeMethod JourneyBuilder.BuildAll -quit -logFile Tools/unity_batch_build.log
```
같은 방식으로 `JourneyCapture.CaptureAll`, `JourneyOverlapReport.Run` 실행 가능.

**에디터가 열려 있는데 원격이라 메뉴를 못 누를 때**: 파일만 만들어 두면 다음 스크립트 리로드 때 자동 실행된다.
```
Library/journey_autobuild.flag   → 전체 빌드
Library/journey_capture.flag     → 씬 캡처
Library/journey_overlap.flag     → 겹침 검사
```
(완료되면 같은 이름의 `.done` 파일이 생긴다. `Temp/` 는 종료 시 비워지므로 반드시 `Library/`.)

### 2.2 대본 수정 → 음성 재생성
1. `Assets/Data/journey_content.json` 수정 (stages[].intro / outro / parts[].line / absorbed / churn / game / complete)
2. `python Tools/make_narration.py` (새 줄만 생성, `--force` 로 전부 재생성, `--voice ko-KR-InJoonNeural` 등으로 목소리 변경)
3. HumanBody → 전체 빌드 (음성이 Stage 에셋/이름표에 다시 연결됨)
- 라이드 정차 멘트·씹기 멘트는 `make_narration.py` 의 `EXTRA` 딕셔너리와 `JourneyBuilder` 의 `Stop(...)`/`Zone(...)` 텍스트에 각각 있음 — 둘 다 고쳐야 함 (개선 여지: JSON 으로 통합)

### 2.3 모델 추가/교체 (Meshy)
- API 키: `C:\Users\admin_14\Desktop\msyapi.txt` (환경변수 `MESHY_API_KEY` 도 됨). **채팅/로그에 절대 출력 금지.**
- 잔액 확인: `python Tools/meshy.py balance` → 현재 **485 크레딧** (text-to-3d preview+refine = 15/모델, 3~4분)
- 배치: 매니페스트 JSON 에 `{name, prompt, polycount}` 추가 → `python Tools/meshy.py batch Tools/meshy_manifest_parts.json --parallel 3` (이미 있는 파일은 건너뜀) → `Assets/Models/Meshy/{name}.glb`
- 썸네일 검수: `python Tools/meshy_thumbs.py <출력폴더>` (로그 파일 경로는 스크립트 안에서 바꿔야 함)
- **Meshy 특성**: "내부/터널/단면" 프롬프트는 외형 전체를 만들어 버리는 경향 → 파츠 단위로 뽑아 조립하는 게 정답. 관(tube) 모델은 지름>길이로 나올 수 있어 `AlignTubeAxis` 가 축을 자동 판별함.
- 빌더는 모델 이름으로 찾는다: `Model("Part_Tongue", ...)`. 파일이 없으면 자리표시 큐브 `(MISSING)`.

### 2.4 컴파일 검증 (Unity 없이)
```bash
python Tools/compile_check.py
```
Unity 가 생성한 csproj 를 이용해 dotnet build. 새 .cs 파일이 csproj 에 아직 없으면 임시로 추가해서 빌드한다.

---

## 3. 런타임 아키텍처 (Assets/Scripts/Journey)

### 흐름
```
PersistentBootstrap (각 씬) ── 없으면 JourneyPersistent 프리팹 생성(DontDestroyOnLoad)
   └ JourneyManager   씬 순서(OrganStage 리스트) · 전환(페이드→로드→스폰→인트로) · GoToNext()/GoTo()
   └ Player (OVRPlayerController)  + ComfortLocomotion(텔레포트/스냅턴) + QuestSettings + ScreenFade
       └ CenterEyeAnchor: GazeSelector(응시) + ComfortVignette
       └ Left/RightHandAnchor/HandTouch: "Hand" 태그 트리거 (영양소 손 흡수)
   └ Guide_ChickDoctor  FloatingFollower(따라다님) + GuideNarrator(음성+자막) + SpeechBubble(Billboard)
```

| 스크립트 | 역할 |
|---|---|
| `OrganStage` | 단계 데이터(SO): id·씬 이름·인트로/아웃트로·요약·알아두기·페이드색 |
| `JourneyManager` | 싱글톤. `GoToNext(bool playOutro)`. sceneLoaded 에서 SpawnPoint 로 이동·페이드인·인트로 |
| `GuideNarrator` | 음성+KoreanTyper 자막 동기 재생. `Play()`는 이전 것을 끊되 구독자에 '끝남'을 알림. B/N 스킵 |
| `NarrationZone` | 트리거 진입 → 내레이션 → onFinished/advance |
| `StageExitPortal` | 출구 트리거 → GoToNext |
| `PartLabel` / `IGazeTarget` / `GazeSelector` | 파츠 응시 → 이름표+하이라이트+한 줄 설명(1회) |
| `ExhibitStation` | 로비 전시대 정보 패널(요약+알아두기 타이핑) |
| `CapsuleRide` | 스플라인 라이드. 정차(RideStop.onReached), 가감속, **항상 수평 유지(keepUpright)**, 자동탑승 시 수동 하차 금지 |
| `ChewingSequence` → `SwallowSequence` | 입: 씹기 → (아웃트로 멘트) → 페이드하며 목구멍으로 끌려감 → 식도 |
| `PeristalsisWave` / `PeristalsisPulse` | 연동파 링 이동(플레이어 통과 시 소리+진동) / 벽 맥동 |
| `SphincterGate` | 분문/유문 개폐(+블로커 콜라이더) |
| `StomachChurn` / `EnzymeCutSequence` | 음식→미즙, 펩신이 단백질 사슬 절단 |
| `NutrientAbsorption` / `Nutrient` | 응시(1초) 또는 검지 트리거 또는 손 접촉으로 흡수, 종류별 첫 흡수 시 설명 |
| `FloatingSwarm` / `SplineFlow` / `WallAbsorption` / `RideProgressSwitcher` | 세균 무리 / 혈관 속 적혈구 흐름 / 물방울 벽 흡수 / 찌꺼기 3단계 |
| `TheaterEnding` | 인트로 끝난 뒤 영상 → 끝나면 아웃트로 → 로비로 |
| `ComfortLocomotion` / `ComfortVignette` / `QuestSettings` / `Haptics` / `Sfx` / `SceneAmbience` | 멀미·성능·촉각·합성 효과음 |
| `Billboard` / `Rotator` / `Sway` | 유틸 |

### 씬별 구성 (JourneyBuilder 의 Build* 메서드와 1:1)
| 씬 | 이동 | 핵심 연출 | 다음 씬으로 |
|---|---|---|---|
| 00_Lobby | 걷기 | 전시대 6개(응시 정보패널), 홀로그램, 입 모양 아치 | 아치 통과(포털) |
| 01_Mouth | 걷기(혀 위) | ChewZone 진입 → 씹기 → 삼킴 | 삼킴 연출이 자동 전환 |
| 02_Esophagus | 캡슐(자동 탑승 7초 후) | 연동파 링, 정차 2회, 분문 열림 | 도착 시 자동 |
| 03_Stomach | 걷기 | ChurnZone → 위산·휘젓기·펩신 절단 → 유문 열림 → 미즙 흐름 | 유문 뒤 포털 |
| 04_SmallIntestine | 캡슐 | 십이지장 분비, 융털 확대(적혈구/지방산 흐름), 영양소 흡수 게임 | 도착 시 자동 |
| 05_LargeIntestine | 캡슐 | 유익균/유해균/식이섬유, 수분 흡수, 찌꺼기 굳어짐 | 도착 시 자동 |
| 06_Theater | 걷기 | 홀로그램 경로 빛 → 영상 → 마무리 | 로비로 Restart |

---

## 4. 검증 상태 — 무엇이 확인됐고 무엇이 안 됐나

### 확인됨
- 런타임/에디터 어셈블리 **컴파일 오류 0** (`Tools/compile_check.py`, 2026-09-22)
- 배치모드 전체 빌드 **예외 0, 모델 누락 0** (`Tools/unity_batch_build.log`)
- 7개 씬 6방향 캡처로 배치·조명 검수 (`Tools/captures/`) — 입/식도/위/소장/대장/로비/상영관 모두 의도대로
- 겹침 자동검사 (`Tools/overlap_report.txt`): 남은 항목은 모두 의도된 것 (U자 치아 AABB 안의 혀/식괴, 절단면 내부의 혈관, 벽에 매립된 패널, 찌꺼기 3단계 동일 위치)
- 코드리뷰 15건 수정 완료 (라이드 기울어짐, 위 씬 NRE 소프트락, A버튼 충돌, 콜라이더 스케일, 내레이션 끊김 시 이벤트 누락, 상영관 순서, 삼킴 페이드 순서, 하이라이트 키워드, 손 콜라이더, 링 지름 등)

### 확인 안 됨 (다음 담당자가 반드시 해야 할 것)
1. **Play 모드 end-to-end** — 로비→…→상영관 실제 플레이 (내레이션 동기, 씬 전환, 라이드, 씹기/삼킴, 흡수 게임, 유문 개방). 첫 Play 에서 런타임 에러가 나올 가능성이 높다. 콘솔 빨간 에러부터 잡을 것.
2. **VR 기기(Quest) / Meta XR Simulator** 에서 텔레포트·스냅턴·햅틱·손 흡수 동작
3. 합성 효과음(`Sfx`)의 실제 음질 — 코드로 만든 것이라 들어봐야 안다. 별로면 `SwallowSequence.gulpClip` 등 클립 필드에 파일을 넣으면 우선 사용됨
4. 애니메이션 피벗 (턱 회전축 `LowerJawPivot`, 혀 들림 `TonguePivot`, 목젖 `UvulaPivot`) — 실제 움직여 봐야 자연스러운지 알 수 있음
5. Quest 프레임 (아직 실기 빌드 없음)

---

## 5. 알려진 이슈

| 이슈 | 상태 / 대처 |
|---|---|
| **Unity 에디터 "Hold on" hang** (2회, 각 30분~2시간, 로그 정지) | 멈추기 직전 로그가 매번 Unity AI 패키지(`AiEditorToolsSdk`, `generators-beta.ai.unity.com ApiNoLongerSupported`) → `com.unity.ai.assistant`, `com.unity.ai.generators`, `com.unity.2d.enhancers` 를 manifest 에서 제거함 (백업 `Tools/manifest.backup.json`). 제거 후 hang 재발 여부 미확인. 재발하면 `Library/` 삭제 후 재임포트 시도 |
| **OpenXR 런타임 레지스트리 불일치** — 콘솔에 영어 에러 100줄+ (`MetaXRSimulator\v201.0\meta_openxr_simulator.json` 없음, 설치된 건 v205) | Unity 메뉴 **Meta → Meta XR Simulator → Activate/Deactivate** 한 번 눌러 레지스트리 갱신. 코드 문제 아님 |
| 콘솔 `The referenced script (Unknown) on this Behaviour is missing!` ×17 | 새 씬/프리팹에는 없음(검사함). 구 씬 또는 패키지 샘플 |
| Meshy 텍스처 2K PBR ×54 → Quest 메모리 부담 | 미처리. 임포트 설정 1K + ASTC 6x6 일괄 적용 필요 (glTFast 임포터라 텍스처가 서브에셋 → 포스트프로세서로 안 잡힘, 임포터 설정에서 처리) |
| 터널 모델 폴리 (소장 50k × 9 세그먼트) | 미처리. Meshy 로 저폴리 재생성(polycount 15k) 하거나 세그먼트 수 축소 |
| 대장 터널은 Meshy 가 관 형태를 못 만들어 **식도 관 모델을 갈색 재질로 재사용** + 팽대 패널 라이닝 | 원하면 `Env_ColonPipe` 재도전 (`Tools/rejected/` 에 실패작 있음) |
| 입천장 vs 윗니 아치 AABB 겹침 62% | 곡면끼리라 시각적으론 캡처에서 문제 없어 보임. Play 에서 확인 |
| `RideStop`/`Zone` 멘트 텍스트가 빌더 코드에 하드코딩 | 대본 수정 시 `make_narration.py` EXTRA 와 빌더 둘 다 수정 필요 → JSON 통합 권장 |
| 구 스크립트/구 씬/Sketchfab 모델 잔존 | 새 흐름은 사용 안 함. 정리는 나중에 (git status 의 D 항목들은 이 세션 이전부터 삭제된 Sketchfab 플러그인 메타) |

---

## 6. 다음 할 일 (우선순위)

1. **커밋** (`git add -A && git commit`) — 지금 21개 변경이 전부 미커밋
2. Unity 열기 → 콘솔 에러 확인 → Meta XR Simulator Activate → `00_Lobby` Play → 끝까지 진행하며 에러/이상 기록
3. 위 4장 "확인 안 됨" 5개 항목 검증
4. Quest 최적화: 텍스처 압축, 터널 폴리 감량, Mobile_Renderer 확인, 실기 빌드 후 프레임 측정
5. 콘텐츠 다듬기: 대본 톤, 라이드 속도/정차 위치(`Ride(...)` speed·stops), 조명 색, 파츠 위치 미세조정 (빌더 값 수정 → 재빌드)
6. 선택: 캡슐 모델에 좌석/콕핏 디테일, 병아리 애니메이션(`GuideNarrator.animator` + `IsTalking` bool 연결), 퀴즈 등 상영관 콘텐츠

---

## 7. 환경/계정 메모

- Unity **6000.2.1f1** (Hub 에 6000.2.13f1 / 6000.3.x 도 설치돼 있음 — 프로젝트 버전 유지할 것), URP, Meta XR SDK 83, glTFast 6.14, Cinemachine 3.1.5, Splines 2.0
- Python 3.14 (`edge-tts`, `Pillow` 설치됨), dotnet SDK 10
- Meshy: 키 파일 위치는 2.3 참고. 이 세션에서 총 ~835 크레딧 사용(모델 59개 생성, 5개 폐기)
- TTS 는 무료(edge-tts) — 계정 불필요. Higgsfield/Unity AI 등 유료 서비스는 사용 안 함
- Unity MCP(`mcp-unity`) 는 이 세션에서 권한 거절로 사용 못 함. 대신 배치모드 + 플래그 방식으로 전부 처리했음

