#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class JourneyPerspective
{
    const string ChangePath = "Tools/explorer_dialogue_changes.json";
    const string SceneDir = "Assets/Scenes/Journey";
    static readonly List<string> Report = new List<string>();
    static readonly HashSet<string> Seen = new HashSet<string>();
    static JArray Changes;
    static int failures, updated;

    [MenuItem("인체탐험/6. 탐험가 설정과 대사 적용", priority = 15)]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Changes = JArray.Parse(File.ReadAllText(ChangePath));
        updated = 0;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            JourneyBuilder.ImportStages();
            foreach (var path in Directory.GetFiles(SceneDir, "*.unity").OrderBy(p => p))
            {
                var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (Owned(component)) Rewrite(component);
                if (scene.name == "01_Mouth")
                {
                    var swallow = Object.FindFirstObjectByType<SwallowSequence>();
                    if (swallow == null) throw new InvalidOperationException("음식 삼킴 연출을 찾을 수 없습니다.");
                    swallow.pullPlayer = false;
                    EditorUtility.SetDirty(swallow);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Tools/explorer_perspective_apply.txt", "탐험가 설정과 일곱 장면 저장 완료\n기존 장면 안내 갱신: " + updated + "\n", new UTF8Encoding(false));
        }
        finally
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene(SceneDir + "/00_Lobby.unity");
        }
    }

    static bool Owned(MonoBehaviour value) => value != null && KoreanJourneyEditor.Types.ContainsKey(value.GetType().Name);

    static void Rewrite(Object value)
    {
        using (var so = new SerializedObject(value))
        {
            var p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.String) continue;
                foreach (var c in Changes)
                    if (p.stringValue == (string)c["before"])
                    {
                        p.stringValue = (string)c["after"];
                        updated++;
                        // 길어진 대사의 무음 대체 표시 시간도 함께 갱신한다.
                        if (p.name == "text")
                        {
                            string parent = p.propertyPath.Substring(0, p.propertyPath.Length - 4);
                            var hold = so.FindProperty(parent + "holdSeconds");
                            if (hold != null) hold.floatValue = Mathf.Max(2f, p.stringValue.Length * .13f);
                        }
                        break;
                    }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static void VerifyAndRunJourney()
    {
        Verify();
        JourneyValidation.Run();
    }

    public static void Verify()
    {
        Changes = JArray.Parse(File.ReadAllText(ChangePath));
        Report.Clear(); Seen.Clear(); failures = 0;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var path in Directory.GetFiles("Assets/Data/Stages", "Stage_*.asset"))
                Inspect(AssetDatabase.LoadAssetAtPath<OrganStage>(path.Replace('\\', '/')));
            foreach (var path in Directory.GetFiles(SceneDir, "*.unity").OrderBy(p => p))
            {
                var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (Owned(component)) Inspect(component);
                if (scene.name == "01_Mouth")
                {
                    var swallow = Object.FindFirstObjectByType<SwallowSequence>();
                    Check(swallow != null && !swallow.pullPlayer && swallow.advanceOnFinish,
                        "음식 삼킴 관찰 뒤 탐험가를 끌어당기지 않고 다음 장소로 전환");
                    var chew = Object.FindFirstObjectByType<ChewingSequence>();
                    Check(chew != null && chew.onChewingFinished.GetPersistentEventCount() > 0,
                        "기존 음식 씹기·삼킴 진행 연결 유지");
                }
                if (scene.name == "03_Stomach")
                {
                    var churn = Object.FindFirstObjectByType<StomachChurn>();
                    Check(churn != null && churn.acidVfx.Length == 3 && churn.acidVfx.All(p => p != null), "위산 효과 연결 유지");
                }
                if (scene.name == "04_SmallIntestine")
                    Check(Object.FindFirstObjectByType<NutrientAbsorption>().absorbVfx != null, "영양소 흡수 효과 연결 유지");
                if (scene.name == "05_LargeIntestine")
                    Check(Object.FindFirstObjectByType<WallAbsorption>().absorbVfx != null, "수분 파문 효과 연결 유지");
                Check(RenderSettings.skybox != null, scene.name + " 환경 배경 유지");
            }
            foreach (var c in Changes)
                Check(Seen.Contains((string)c["after"]), "실제 장면·단계 자료에 새 안내 존재: " + ((string)c["voice"] ?? "흡수 진행 문구"));
        }
        catch (Exception ex)
        {
            Check(false, ex.ToString());
            Debug.LogException(ex);
        }
        finally
        {
            Report.Add("실패 수: " + failures);
            File.WriteAllLines("Tools/explorer_perspective_validation.txt", Report, new UTF8Encoding(false));
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene(SceneDir + "/00_Lobby.unity");
        }
        if (failures > 0) throw new InvalidOperationException("탐험가 설정 검사에서 오류가 발견되었습니다.");
    }

    static void Inspect(Object value)
    {
        if (value == null) { Check(false, "검사 대상 자료 없음"); return; }
        using (var so = new SerializedObject(value))
        {
            var p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.String) continue;
                foreach (var c in Changes)
                {
                    if (p.stringValue == (string)c["before"])
                        Check(false, "이전 안내가 남아 있음: " + value.name + "/" + p.propertyPath);
                    if (p.stringValue != (string)c["after"]) continue;
                    Seen.Add(p.stringValue);
                    string voice = (string)c["voice"];
                    if (voice == null) continue;
                    string parent = p.propertyPath.Substring(0, p.propertyPath.Length - p.name.Length);
                    var voiceProperty = so.FindProperty(parent + "voice");
                    var clip = voiceProperty?.objectReferenceValue as AudioClip;
                    string expected = "Assets/Sounds/Narration/" + voice + ".mp3";
                    Check(clip != null && AssetDatabase.GetAssetPath(clip) == expected && clip.length > .1f && clip.samples > 0,
                        "새 자막과 음성 연결: " + voice);
                }
            }
        }
    }

    static void Check(bool ok, string label)
    {
        Report.Add((ok ? "통과: " : "실패: ") + label);
        if (!ok) failures++;
    }
}
#endif
