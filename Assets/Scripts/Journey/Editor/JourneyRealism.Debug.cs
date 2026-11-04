#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>검사용: 치아 재질을 여러 보기로 찍어 색이 어디서 틀어지는지 확인한다. 결과: Tools/realism_captures/검사_*.png</summary>
public static partial class JourneyRealism
{
    public static void ApplyCaptureAndDebug() { ApplyAndCapture(); DebugTeeth(); }

    /// <summary>배치모드: 대본(journey_content.json)을 단계 자료로 불러온 뒤 적용·촬영.</summary>
    public static void ImportApplyAndCapture() { JourneyBuilder.ImportStages(); ApplyAndCapture(); }

    /// <summary>배치모드: 적용·촬영 뒤 장면 속 물체 조사까지.</summary>
    public static void ApplyCaptureAndInventory() { ApplyAndCapture(); Inventory(); Budget(); }

    /// <summary>
    /// 지정한 장면만 다시 적용한다 (배치: -realismScenes 03_Stomach,04_SmallIntestine).
    /// 다른 장면(편집기에서 직접 손보고 저장한 장면 등)은 건드리지 않는다.
    /// </summary>
    public static void ApplySome()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-realismScenes");
        if (i < 0 || i + 1 >= args.Length) { Debug.LogError("[JourneyRealism] -realismScenes 장면이름,장면이름 이 필요함"); return; }
        ShaderUtil.allowAsyncCompilation = false;
        PlayerEye(); // 영속 프리팹의 플레이어 눈높이(모든 장면 공통)
        foreach (var name in args[i + 1].Split(','))
        {
            var scene = EditorSceneManager.OpenScene($"{SceneDir}/{name.Trim()}.unity", OpenSceneMode.Single);
            ConfigureScene(StageId(name.Trim()));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[JourneyRealism] {name.Trim()} 만 다시 적용");
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>대사 자료(journey_content.json)를 단계 에셋에 다시 불러온 뒤 적용·촬영·조사·성능 어림까지 (배치용).</summary>
    public static void ImportApplyCaptureAndInventory() { JourneyBuilder.ImportStages(); ApplyCaptureAndInventory(); }

    public static void DebugTeeth()
    {
        const string outDir = "Tools/realism_captures";
        Directory.CreateDirectory(outDir);
        EditorSceneManager.OpenScene($"{SceneDir}/01_Mouth.unity", OpenSceneMode.Single);
        var spawn = GameObject.Find("SpawnPoint");
        Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;
        var mats = new[] { $"{Folder}/치아_잇몸_Part_UpperTeethArch.mat", $"{Folder}/치아_잇몸_Part_LowerTeethArch.mat" };
        void SetAll(string prop, float value) { foreach (var p in mats) { var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m != null) m.SetFloat(prop, value); } }
        float env = AssetDatabase.LoadAssetAtPath<Material>(mats[0])?.GetFloat("_EnvReflection") ?? .3f;
        float flow = AssetDatabase.LoadAssetAtPath<Material>(mats[0])?.GetFloat("_FlowStrength") ?? 0;
        foreach (var (view, label) in new[] { (1f, "바탕색"), (2f, "잇몸판정"), (3f, "원본그림"), (4f, "법선") })
        {
            SetAll("_DebugView", view);
            Shoot(outDir, "검사", label, eye, Quaternion.Euler(5, 0, 0));
        }
        SetAll("_DebugView", 0);
        SetAll("_EnvReflection", 0); Shoot(outDir, "검사", "주변반사끔", eye, Quaternion.Euler(5, 0, 0)); SetAll("_EnvReflection", env);
        SetAll("_FlowStrength", 0); Shoot(outDir, "검사", "흐름끔", eye, Quaternion.Euler(5, 0, 0)); SetAll("_FlowStrength", flow);
        Shoot(outDir, "검사", "전부켬", eye, Quaternion.Euler(5, 0, 0));
        foreach (var p in mats) { var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m == null) continue; Debug.Log($"[치아검사] {p} 그림={m.GetTexture("_MainTex")} ST={m.GetVector("_MainTex_ST")} 키워드={string.Join(",", m.shaderKeywords)} 바탕={m.GetColor("_BaseColor")} 법랑={m.GetColor("_EnamelColor")} 기준={m.GetFloat("_GumThreshold")}"); }
        foreach (var name in new[] { "Part_UpperTeethArch", "Part_LowerTeethArch" })
        {
            var go = GameObject.Find(name); if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf != null ? mf.sharedMesh : null;
                Debug.Log($"[치아검사] {r.name} 재질수={r.sharedMaterials.Length} 메시={(mesh != null ? mesh.name : "-")} 정점={(mesh != null ? mesh.vertexCount : 0)} uv0={(mesh != null && mesh.uv.Length > 0)} 법선={(mesh != null && mesh.normals.Length > 0)} 색={(mesh != null && mesh.colors.Length > 0)}");
            }
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
