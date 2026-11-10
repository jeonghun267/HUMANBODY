# HUMANBODY 후속 점검 — 2026-09-22

Unity 6000.2.1f1에서 실행을 막거나 진행 순서를 깨뜨리는 문제를 수정했다. 기존 미커밋 작업을 유지했고, 이번 수정 파일의 적용 전 원본은 작업 폴더 `humanbody-fixes/original`에 보관했다.

## 수정 내용

- **CapsuleRide**: 가이드의 `transform.root`를 탑승시키면 JourneyPersistent 전체가 일반 씬으로 옮겨져 다음 씬에서 플레이어와 매니저까지 파괴됐다. 가이드 본체만 이동하고, 따라오기 동작을 탑승 중 끄며, 하차 때 이전 컴포넌트 활성 상태를 복원한다. 진행 중인 텔레포트도 취소한다.
- **GuideNarrator / NarrationZone**: 구역이 이전 내레이션의 중단 이벤트를 자기 완료로 받아 씹기/소화 연출을 너무 일찍 시작했다. 재생별 완료 콜백을 연결하고 비활성화된 구역의 콜백을 무효화한다. 구역 설명은 입장 인트로가 끝난 뒤 시작한다. 직접 기다리는 PlayRoutine도 같은 재생 세션을 사용한다.
- **JourneyManager**: 최초 sceneLoaded와 Start가 인트로를 중복 예약하지 않게 하고, 씬을 떠날 때 오래된 인트로 예약을 취소한다.
- **ComfortLocomotion**: 텔레포트 마커를 영속 플레이어 하위에 두어 씬 전환 뒤 참조가 사라지지 않게 한다. 탑승/삼킴 중 이동을 차단하고, 텔레포트 취소 시 해당 페이드의 복구를 요청한다.
- **StageExitPortal**: 일반 다음 씬 이동에도 `playOutro` 값을 전달한다.
- **compile_check.py**: C# 오류 문자열만 검사하던 방식은 SDK/MSBuild/패키지 복원 실패를 성공으로 보고할 수 있었다. 프로세스 종료 코드로 실패를 판정하고 예외 및 임시 파일 정리를 처리한다.

## 실행 결과

| 검사 | 결과 | 증거 |
|---|---|---|
| 수정 전 회귀 | 12개 중 8개 실패 | `validation_baseline.txt` |
| 수정 후 회귀 | 13개 통과, 실패 0 | `validation_fixed.txt` |
| 실제 씬 진행 | 29개 통과, 실패 0 | `validation_journey.txt` |
| 컴파일 검사 도구의 종료 코드 처리 | Python unittest 3개 통과 | `test_compile_check.py` |
| 실제 C# 컴파일 | Unity가 수정된 런타임/에디터 스크립트를 컴파일하고 Play 모드 진입 | `validation_fixed.log`, `validation_journey.log` |

씬 진행 검사는 `로비 → 입 → 식도 → 위 → 소장 → 대장 → 상영관 → 로비`를 로드하며 씹기/삼킴, 라이드, 유문 개방, 영양소 전체 흡수, 상영관의 영상 없는 경우 대체 종료를 검사했다. 매 전환에서 최초 플레이어와 영속 루트가 유지되는지도 검사한다.

**검사 범위:** 내레이션 데이터는 실행 중 복사본으로 짧게 바꾸고 이동 속도/대기 시간을 줄인다. 트리거·흡수 호출을 자동 실행하므로 사용자의 실제 걷기, 시선, 손, 컨트롤러 입력을 검증한 것은 아니다. 원본 대본·음성·씬 자산은 검사 때문에 다시 생성하거나 저장하지 않는다.

## 남은 확인 사항

- Quest 실기, 컨트롤러/손 입력, 멀미, 모델 배치와 턱/혀 애니메이션, 원본 음성 타이밍·음질, 영상 디코딩, 프레임 및 메모리는 미검증이다.
- 페이드의 최종 화면은 미검증이다. OVRScreenFade가 사용하는 `WaitForEndOfFrame`은 에디터 배치 모드에서 진행되지 않는다. Game 뷰/실기에서 확인해야 한다. [Unity 문서](https://docs.unity3d.com/kr/6000.0/ScriptReference/WaitForEndOfFrame.html)
- **OpenXR 런타임 경로 문제는 남아 있다.** Windows `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime`은 존재하지 않는 `C:\Program Files\MetaXRSimulator\v201.0\meta_openxr_simulator.json`을 가리킨다. 실제 설치본은 `v205.0`이다. 변경을 시도했으나 Windows 레지스트리 쓰기 권한이 없어 적용되지 않았다. Unity 로그의 `XR_ERROR_RUNTIME_UNAVAILABLE`은 이 문제와 함께 관찰됐다. 관리자 권한 PowerShell에서 설치본의 공식 스크립트를 실행한 후 VR 초기화를 다시 검사한다:

```powershell
& 'C:/Program Files/MetaXRSimulator/v205.0/activate_simulator.ps1' -MessageTime 0
```

이 설정을 고친 것만으로 기기 입력이나 실기 성능이 검증되는 것은 아니다. 현재 통과 결과는 VR 초기화 이후의 장치 검증을 의미하지 않는다.

## 재실행

Unity를 닫은 상태에서 프로젝트 루트의 PowerShell에서 실행한다. 검증 도구가 직접 종료하므로 `-quit`는 붙이지 않는다. 최대 실행 시간과 실패 시 종료 코드가 있다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.2.1f1/Editor/Unity.exe' -batchmode -projectPath (Get-Location).Path -executeMethod JourneyValidation.Run -journeyMode regression -journeyReport Tools/validation_fixed.txt -logFile Tools/validation_fixed.log

& 'C:/Program Files/Unity/Hub/Editor/6000.2.1f1/Editor/Unity.exe' -batchmode -projectPath (Get-Location).Path -executeMethod JourneyValidation.Run -journeyMode journey -journeyReport Tools/validation_journey.txt -logFile Tools/validation_journey.log

python Tools/test_compile_check.py
python Tools/compile_check.py
```

Unity 배치 로그에는 기기 초기화 등 환경 오류도 포함된다. 텍스트 검사 결과만 보고 VR 전체가 검증됐다고 판단하지 말 것. 마지막 `compile_check.py` 전체 dotnet 검사는 이번 세션에서 별도로 실행하지 않았고, 실제 컴파일은 Unity 실행으로 확인했다.

## 소화·흡수 효과 후속 구현

영양소 흡수 빛, 장벽 수분 파문, 위산과 소화 소리의 시작·종료 연동을 실제 장면에 적용했다. 효과 재생 검사 34개와 전체 탐험 진행 검사 30개가 실패 없이 통과했다. 자세한 동작과 촬영 화면은 [효과 구현 결과](EFFECTS.md)에 정리했다.


## 탐험가 설정과 대사 통일

플레이어를 작은 탐험가로 통일하고 대사·음성 19개를 갱신했다. 입에서는 음식 삼킴 관찰 뒤 안내와 화면 전환으로 이동하며, 캡슐은 도입 안내가 끝난 뒤 출발한다. 저장된 안내 연결, 회귀 및 전체 탐험 검사를 모두 통과했다. [설정과 적용 대사](EXPLORER_PERSPECTIVE.md)
