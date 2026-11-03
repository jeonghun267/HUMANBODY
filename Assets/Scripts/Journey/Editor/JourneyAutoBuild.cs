#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원격 작업용 자동 빌드 트리거. 프로젝트 루트에 `Library/journey_autobuild.flag` 파일이 있으면
/// 스크립트 리로드 직후 JourneyBuilder.BuildAll 을 한 번 실행하고 플래그를 지운다.
/// (에디터에 직접 접근할 수 없을 때 파일만 만들어 두면 다음 리컴파일 때 씬이 재생성된다.)
/// 리로드 직후 가져오기가 아직 진행 중이면 놓치지 않도록, 에디터가 한가할 때까지 2초마다 다시 확인한다.
/// </summary>
public static class JourneyAutoBuild
{
    const string FlagPath = "Library/journey_autobuild.flag"; // Temp/ 는 에디터 종료 시 비워지므로 Library 사용
    const string CaptureFlagPath = "Library/journey_capture.flag";
    const string OverlapFlagPath = "Library/journey_overlap.flag";
    const string RealismFlagPath = "Library/journey_realism.flag";
    static double nextCheck;

    [InitializeOnLoadMethod]
    static void OnLoad()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!File.Exists(FlagPath) && !File.Exists(CaptureFlagPath) && !File.Exists(OverlapFlagPath) && !File.Exists(RealismFlagPath)) return;
        {
            if (File.Exists(FlagPath))
            {
                File.Delete(FlagPath);
                Debug.Log("[JourneyAutoBuild] 플래그 감지 → 전체 빌드 시작");
                try { JourneyBuilder.ImportStages(); JourneyBuilder.BuildScenes(false); JourneyBuilder.RegisterBuildScenes(); }
                catch (System.Exception e) { Debug.LogException(e); }
                File.WriteAllText("Library/journey_autobuild.done", System.DateTime.Now.ToString("s"));
            }
            if (File.Exists(CaptureFlagPath))
            {
                File.Delete(CaptureFlagPath);
                Debug.Log("[JourneyAutoBuild] 캡처 플래그 감지 → 씬 캡처 시작");
                try { JourneyCapture.CaptureAll(); }
                catch (System.Exception e) { Debug.LogException(e); }
                File.WriteAllText("Library/journey_capture.done", System.DateTime.Now.ToString("s"));
            }
            if (File.Exists(OverlapFlagPath))
            {
                File.Delete(OverlapFlagPath);
                try { JourneyOverlapReport.Run(); }
                catch (System.Exception e) { Debug.LogException(e); }
                File.WriteAllText("Library/journey_overlap.done", System.DateTime.Now.ToString("s"));
            }
            if (File.Exists(RealismFlagPath))
            {
                File.Delete(RealismFlagPath);
                Debug.Log("[JourneyAutoBuild] 실사 플래그 감지 → 점막·조명 적용 후 촬영");
                try { JourneyRealism.ApplyAndCapture(); }
                catch (System.Exception e) { Debug.LogException(e); }
                File.WriteAllText("Library/journey_realism.done", System.DateTime.Now.ToString("s"));
            }
        }
    }
}
#endif
