#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class JourneyPresentation
{
    const string SceneDir = "Assets/Scenes/Journey";
    const string CaptureDir = "Tools/korean_environment_captures";
    const string ReportPath = "Tools/korean_environment_validation.txt";
    const string PrefabPath = "Assets/Data/Generated/JourneyPersistent.prefab";
    static readonly List<string> report = new List<string>();
    static int failures, translated;
    static readonly Regex Latin = new Regex("[A-Za-z]");
    static readonly HashSet<string> TextFields = new HashSet<string> { "text", "displayName", "summary", "funFacts", "overrideTitle", "overrideBody", "progressFormat", "absorbedLine", "organExplanation" };
    static readonly Dictionary<string, string> Replacements = new Dictionary<string, string>
    {
        { "소화관 전체 길이는 약 9m나 돼요.", "소화관 전체 길이는 약 9미터나 돼요." },
        { "식도는 입과 위를 잇는 약 25cm 길이의 근육 관이야. 캡슐을 타고 내려가 보자!", "식도는 입과 위를 잇는 약 25센티미터 길이의 근육 관이야. 캡슐을 타고 내려가 보자!" },
        { "위산은 pH 1~2로 아주 강한 산성이야. 하지만 위벽은 두꺼운 점액으로 보호되고 있어서 안전해.", "위산은 산성도 1~2로 아주 강한 산성이야. 하지만 위벽은 두꺼운 점액으로 보호되고 있어서 안전해." },
        { "소장은 길이가 6~7m나 되고, 영양소 대부분이 흡수되는 곳이야.", "소장은 길이가 6~7미터나 되고, 영양소 대부분이 흡수되는 곳이야." },
        { "융털은 1mm 정도로 아주 작지만 수백만 개나 있어요.", "융털은 1밀리미터 정도로 아주 작지만 수백만 개나 있어요." },
        { "융털 하나는 1mm도 안 되지만 수백만 개가 있어서 표면적이 엄청 넓어져.", "융털 하나는 1밀리미터도 안 되지만 수백만 개가 있어서 표면적이 엄청 넓어져." },
        { "대장은 길이 1.5m, 남은 물과 미네랄을 흡수해서 찌꺼기를 굳히는 곳이야.", "대장은 길이 1.5미터, 남은 물과 미네랄을 흡수해서 찌꺼기를 굳히는 곳이야." },
        { "유익균은 우리가 소화 못 하는 식이섬유를 먹고 비타민 K와 B를 만들어 줘.", "유익균은 우리가 소화 못 하는 식이섬유를 먹고 비타민 케이와 비를 만들어 줘." },
    };

    [MenuItem("인체탐험/4. 한글 안내와 환경 배경 적용", priority = 13)]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        report.Clear(); failures = 0; translated = 0;
        try
        {
            EnableKoreanEditor();
            CheckInspectorLabels();
            JourneyBuilder.ImportStages();
            foreach (var path in Directory.GetFiles("Assets/Data/Stages", "Stage_*.asset"))
            {
                var stage = AssetDatabase.LoadAssetAtPath<OrganStage>(path.Replace('\\', '/'));
                if (stage != null) ValidateObject(stage);
            }
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var cam in prefab.GetComponentsInChildren<Camera>(true))
                    Check(cam.clearFlags == CameraClearFlags.Skybox, "플레이어 카메라가 환경 배경을 표시함: " + cam.name);
                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (IsOwned(component)) { TranslateObject(component); ValidateObject(component); }
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            Directory.CreateDirectory(CaptureDir);
            foreach (var path in Directory.GetFiles(SceneDir, "*.unity").OrderBy(p => p))
            {
                var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(path);
                string id = sceneName.Substring(sceneName.IndexOf('_') + 1);
                var stage = AssetDatabase.LoadAssetAtPath<OrganStage>("Assets/Data/Stages/Stage_" + id + ".asset");
                int before = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "WorldShell_" + id) root.SetActive(false);
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (IsOwned(component)) { TranslateObject(component); ValidateObject(component); }
                    foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        string text = Replace(tmp.text);
                        if (tmp.text != text) { tmp.text = text; EditorUtility.SetDirty(tmp); }
                        CheckText(text, sceneName + "/" + tmp.name);
                        Check(tmp.font != null, stage.displayName + " 이름표 한글 글꼴 연결: " + tmp.name);
                    }
                }
                JourneyEnvironments.Configure(stage);
                JourneyEnvironments.MatchEnclosures(id);
                Check(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == JourneyEnvironments.ShaderName,
                    stage.displayName + " 전용 환경 배경 연결");
                Check(!ShaderUtil.ShaderHasError(RenderSettings.skybox.shader), stage.displayName + " 배경 셰이더 오류 없음");
                Check(before == scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length), stage.displayName + " 기존 개체와 배치 보존");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Capture(sceneName);
            }
            AssetDatabase.SaveAssets();
            report.Add("변경한 기존 안내 연결: " + translated);
            report.Add("배경 7종 / 장면별 7방향 촬영 / 기존 기관 모형과 이벤트 연결 유지");
            report.Add("기술 식별자, 파일명 및 외부 플러그인의 고유 항목은 연결 호환성을 위해 유지함.");
            report.Add("실제 가상현실 기기의 양안 표시와 입력은 별도 확인이 필요함.");
        }
        catch (Exception exception)
        {
            failures++;
            report.Add("실패: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            report.Add("실패 수: " + failures);
            Directory.CreateDirectory("Tools");
            File.WriteAllLines(ReportPath, report, new UTF8Encoding(false));
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene(SceneDir + "/00_Lobby.unity");
        }
        if (failures != 0) throw new InvalidOperationException("한글 및 환경 배경 검사에서 " + failures + "개 오류. 보고서를 확인하세요.");
        Debug.Log("[인체탐험] 한글 안내와 환경 배경 적용 및 촬영 완료");
    }

    static bool IsOwned(MonoBehaviour component) => component != null && KoreanJourneyEditor.Types.ContainsKey(component.GetType().Name);
    static void Check(bool ok, string text)
    {
        report.Add((ok ? "통과: " : "실패: ") + text);
        if (!ok) failures++;
    }
    static string Replace(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        foreach (var pair in Replacements) value = value.Replace(pair.Key, pair.Value);
        return value;
    }
    static void TranslateObject(Object value)
    {
        using (var so = new SerializedObject(value))
        {
            var p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.String) continue;
                string text = Replace(p.stringValue);
                if (text == p.stringValue) continue;
                p.stringValue = text; translated++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static void CheckText(string text, string path)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string plain = Regex.Replace(text, "<[^>]*>", "");
        Check(!Latin.IsMatch(plain), "한글 안내: " + path);
    }
    static void ValidateObject(Object value)
    {
        using (var so = new SerializedObject(value))
        {
            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                bool text = TextFields.Contains(p.name) || p.propertyPath.StartsWith("funFacts.Array.data", StringComparison.Ordinal);
                if (text && p.propertyType == SerializedPropertyType.String)
                    CheckText(p.stringValue, value.GetType().Name + "/" + p.propertyPath);
            }
        }
    }
    static void CheckInspectorLabels()
    {
        var types = typeof(OrganStage).Assembly.GetTypes().Where(t => KoreanJourneyEditor.Types.ContainsKey(t.Name));
        var seen = new HashSet<Type>();
        foreach (var type in types)
        {
            CheckFields(type, seen);
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                var menu = type.GetCustomAttribute<AddComponentMenu>();
                Check(menu != null && !Latin.IsMatch(menu.componentMenu), "구성 요소 한글 메뉴: " + KoreanJourneyEditor.Types[type.Name]);
            }
        }
    }
    static void CheckFields(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type)) return;
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (f.IsStatic || f.IsInitOnly || f.IsNotSerialized || f.GetCustomAttribute<HideInInspector>() != null) continue;
            if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
            if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
            Check(KoreanJourneyEditor.Fields.ContainsKey(f.Name), "설정 항목 한글 이름: " + type.Name + "/" + f.Name);
            foreach (var header in f.GetCustomAttributes<HeaderAttribute>()) Check(!Latin.IsMatch(header.header), "한글 설정 분류: " + header.header);
            var tip = f.GetCustomAttribute<TooltipAttribute>();
            if (tip != null) Check(!Latin.IsMatch(tip.tooltip), "한글 도움말: " + type.Name + "/" + f.Name);
            var nested = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType;
            if (nested.IsGenericType && nested.GetGenericTypeDefinition() == typeof(List<>)) nested = nested.GetGenericArguments()[0];
            if (nested.Assembly == typeof(OrganStage).Assembly && !typeof(Object).IsAssignableFrom(nested)) CheckFields(nested, seen);
        }
    }
    static void EnableKoreanEditor()
    {
        // 설치된 언어 팩을 사용한다. 내부 편집기 설정은 런타임 빌드에 포함되지 않는다.
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.LocalizationDatabase")).FirstOrDefault(t => t != null);
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var available = type?.GetMethod("GetAvailableEditorLanguages", flags)?.Invoke(null, null) as SystemLanguage[];
        if (available == null || !available.Contains(SystemLanguage.Korean))
            throw new InvalidOperationException("설치된 한글 편집기 언어 팩을 찾을 수 없습니다.");
        report.Add("시작 시 저장된 편집기 언어: " + type.GetProperty("currentEditorLanguage", flags).GetValue(null));
        const string backup = "Tools/editor_language_before.json";
        if (!File.Exists(backup))
            File.WriteAllText(backup, JsonUtility.ToJson(new LanguagePreferenceBackup
            {
                hadLanguage = EditorPrefs.HasKey("Editor.kEditorLocale"),
                language = EditorPrefs.GetString("Editor.kEditorLocale", ""),
                hadEnabled = EditorPrefs.HasKey("Editor.kEnableEditorLocalization"),
                enabled = EditorPrefs.GetBool("Editor.kEnableEditorLocalization", false)
            }, true), new UTF8Encoding(false));
        EditorPrefs.SetString("Editor.kEditorLocale", "Korean");
        EditorPrefs.SetBool("Editor.kEnableEditorLocalization", true);
        type.GetProperty("currentEditorLanguage", flags).SetValue(null, SystemLanguage.Korean);
        type.GetProperty("enableEditorLocalization", flags).SetValue(null, true);
        Check((SystemLanguage)type.GetProperty("currentEditorLanguage", flags).GetValue(null) == SystemLanguage.Korean,
            "유니티 편집기 한국어 선택");
        Check((bool)type.GetProperty("enableEditorLocalization", flags).GetValue(null), "유니티 편집기 번역 활성화");
        Check(EditorPrefs.GetString("Editor.kEditorLocale") == "Korean" && EditorPrefs.GetBool("Editor.kEnableEditorLocalization"), "편집기 한국어 설정 저장");
    }

    [Serializable]
    class LanguagePreferenceBackup
    {
        public bool hadLanguage, hadEnabled, enabled;
        public string language;
    }

    public static void VerifyAndRunJourney()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.LocalizationDatabase")).First(t => t != null);
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var language = (SystemLanguage)type.GetProperty("currentEditorLanguage", flags).GetValue(null);
        bool saved = EditorPrefs.GetString("Editor.kEditorLocale") == "Korean" && EditorPrefs.GetBool("Editor.kEnableEditorLocalization");
        File.WriteAllText("Tools/korean_editor_language_validation.txt",
            "새 편집기 프로세스의 한국어 설정 유지: " + (saved ? "통과" : "실패") + "\n" +
            "시작 시 실제 편집기 언어: " + (language == SystemLanguage.Korean ? "한국어" : language.ToString()) + "\n", new UTF8Encoding(false));
        if (!saved) throw new InvalidOperationException("한국어 편집기 설정이 유지되지 않았습니다.");
        JourneyValidation.Run();
    }

    static void Capture(string sceneName)
    {
        var spawn = GameObject.Find("SpawnPoint");
        var eye = (spawn == null ? Vector3.zero : spawn.transform.position) + Vector3.up * 1.6f;
        Shoot(sceneName, "front", eye, Quaternion.Euler(5, 0, 0));
        Shoot(sceneName, "left", eye, Quaternion.Euler(5, -70, 0));
        Shoot(sceneName, "right", eye, Quaternion.Euler(5, 70, 0));
        Shoot(sceneName, "back", eye, Quaternion.Euler(5, 180, 0));
        Shoot(sceneName, "up", eye, Quaternion.Euler(-60, 0, 0));
        var overview = eye + new Vector3(-14, 12, -14);
        Shoot(sceneName, "overview", overview, Quaternion.LookRotation((eye + Vector3.forward * 6) - overview), 60);
        Shoot(sceneName, "background", eye, Quaternion.Euler(-12, 45, 0), 100, true);
    }
    static void Shoot(string scene, string view, Vector3 pos, Quaternion rot, float fov = 90, bool backgroundOnly = false)
    {
        var go = new GameObject("_PresentationCamera");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 500;
        cam.clearFlags = CameraClearFlags.Skybox;
        if (backgroundOnly) cam.cullingMask = 0;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(CaptureDir + "/" + scene + "_" + view + ".png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = old;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
    }
}
#endif
