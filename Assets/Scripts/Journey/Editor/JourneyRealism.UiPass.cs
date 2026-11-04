#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 판(UGUI) 한 벌 맞추기: 모든 판을 STILL 키트(어두운 바다빛 둥근 판 + 옅은 하늘색 외곽선)로 통일하고,
/// 읽는 거리에서 글자가 편하게 읽히는 크기(본문 약 1.3~1.8°)로 키운다. 어두운 몸속에서 하얀 판은 눈부시므로 쓰지 않는다.
/// </summary>
public static partial class JourneyRealism
{
    const float NpcPanelScale = .0016f, NpcPromptScale = .0015f, QuizScale = .0019f, ReportScale = .002f;

    /// <summary>사람 대화창 자리: 머리 위 옆(오른쪽 0.85m) — 커진 판이 얼굴을 가리지 않게.</summary>
    static Vector3 NpcPanelSpot(GameObject person, float top) =>
        new Vector3(person.transform.position.x, top + .45f, person.transform.position.z) + person.transform.right * .85f;

    /// <summary>부위 이름표(예전 단색 판)를 키트 판으로.</summary>
    static void StyleLabels()
    {
        UiKit();
        foreach (var label in Object.FindObjectsByType<PartLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label.labelRoot == null) continue;
            var root = label.labelRoot.transform;
            var bg = root.Find("Bg")?.GetComponent<Image>();
            if (bg == null) continue;
            Sliced(bg, KitPanel, PlateFill, 1.4f);
            var line = root.Find("외곽선")?.GetComponent<Image>() ?? UiChild(root, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f);
            line.transform.SetSiblingIndex(bg.transform.GetSiblingIndex() + 1);
            if (label.labelText != null) { label.labelText.color = Color.white; label.labelText.fontStyle = FontStyles.Bold; }
            EditorUtility.SetDirty(label.labelRoot);
        }
    }

    /// <summary>안내 병아리 말풍선(영속 프리팹): 하얀 판 → 키트 판, 글자는 밝게.</summary>
    static void StyleSpeechBubble()
    {
        const string path = "Assets/Data/Generated/JourneyPersistent.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            UiKit();
            var bubble = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SpeechBubble");
            var bg = bubble != null ? bubble.Find("Background")?.GetComponent<Image>() : null;
            if (bg == null) { Debug.LogWarning("[판] 말풍선을 찾지 못함"); return; }
            Sliced(bg, KitPanel, PlateFill, 1.4f);
            var line = bubble.Find("외곽선")?.GetComponent<Image>() ?? UiChild(bubble, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f);
            line.transform.SetSiblingIndex(bg.transform.GetSiblingIndex() + 1);
            var text = bubble.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) { text.color = new Color(.95f, .97f, 1f); text.fontSize = 36; }
            // 안내 병아리는 보이지 않게(실사 몸속에서 만화 병아리가 늘 시야 가운데 떠 있었다): 목소리 내레이션만, 자막은 장면 안내판 위로
            var narrator = root.GetComponentInChildren<GuideNarrator>(true);
            if (narrator != null)
            {
                narrator.voiceOnly = true;
                foreach (var r in narrator.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var voice = narrator.GetComponent<AudioSource>(); if (voice != null) voice.spatialBlend = 0f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>이미 만든 장면의 사람 대화창·퀴즈판·보고서를 새 크기로 (장면을 다시 만들지 않고).</summary>
    static void ResizePanels()
    {
        foreach (var npc in Object.FindObjectsByType<HospitalNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var person = npc.gameObject; float top = PersonBounds(person).max.y; float s = Mathf.Max(1e-4f, person.transform.lossyScale.x);
            if (npc.panel != null) { npc.panel.transform.position = NpcPanelSpot(person, top); npc.panel.transform.localScale = Vector3.one * (NpcPanelScale / s); }
            if (npc.prompt != null) npc.prompt.transform.localScale = Vector3.one * (NpcPromptScale / s);
            EditorUtility.SetDirty(npc);
        }
        var quiz = GameObject.Find("퀴즈판"); if (quiz != null) quiz.transform.localScale = Vector3.one * QuizScale;
        var report = GameObject.Find("치료_보고서"); if (report != null) report.transform.localScale = Vector3.one * ReportScale;
    }

    /// <summary>
    /// 판만 다시 손본다 (배치용): 영속 프리팹 말풍선, 장면마다 안내판(다시 만듦)·이름표·사람 대화창·퀴즈·보고서.
    /// 다른 것은 건드리지 않아 편집기에서 손본 장면도 안전하다.
    /// </summary>
    public static void UiPass()
    {
        ShaderUtil.allowAsyncCompilation = false;
        StyleSpeechBubble();
        foreach (var name in new[] { "00_Lobby" }.Concat(BodyScenes).Concat(new[] { "06_Theater" }))
        {
            var scene = EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            string id = StageId(name);
            if (Goals.ContainsKey(id) && GameObject.Find("체험_안내판") != null)
            {
                var hud = Hud(id);
                var absorb = Object.FindFirstObjectByType<NutrientAbsorption>();
                if (absorb != null) { absorb.progressText = hud.status; EditorUtility.SetDirty(absorb); }
            }
            StyleLabels();
            ResizePanels();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[판] {name} 정리");
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
