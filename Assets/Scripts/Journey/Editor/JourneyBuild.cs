#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Quest 에 바로 설치해 볼 APK (배치용, -buildTarget Android 로 실행): 빌드 설정에 등록된 장면 7개 → Builds/HUMANBODY_Quest.apk.
/// 결과 요약은 Builds/build_report.txt. Builds 폴더와 apk 는 git 에 올리지 않는다(.gitignore).
/// 설치: adb install -r Builds/HUMANBODY_Quest.apk (또는 Meta Quest Developer Hub 로 끌어다 놓기)
/// </summary>
public static class JourneyBuild
{
    public static void QuestApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Directory.CreateDirectory("Builds");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = "Builds/HUMANBODY_Quest.apk",
            target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None,
        });
        var s = report.summary;
        var errors = report.steps.SelectMany(step => step.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(20);
        File.WriteAllText("Builds/build_report.txt",
            $"결과 {s.result}\n장면 {scenes.Length}개\n크기 {s.totalSize / (1024 * 1024)}MB\n걸린 시간 {s.totalTime}\n오류 {s.totalErrors} · 경고 {s.totalWarnings}\n\n" + string.Join("\n", errors));
        Debug.Log($"[빌드] {s.result} · {s.totalSize / (1024 * 1024)}MB · {s.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
#endif
