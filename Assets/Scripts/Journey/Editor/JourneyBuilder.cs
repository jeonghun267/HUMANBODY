#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 소화 여정 씬 7개를 파츠 단위로 자동 조립하는 에디터 도구.
/// 메뉴: HumanBody/전체 빌드  →  JSON 데이터 임포트 → 씬 생성 → Build Settings 등록.
/// 모델 크기가 제각각이므로 Fit/Ground 헬퍼로 바운즈 기준 배치한다. 생성 후 세부 위치는 씬에서 다듬는다. 재실행하면 씬을 덮어쓴다.
/// </summary>
public static class JourneyBuilder
{
    const string ContentJson = "Assets/Data/journey_content.json";
    const string StageDir = "Assets/Data/Stages";
    const string SceneDir = "Assets/Scenes/Journey";
    const string ModelDir = "Assets/Models/Meshy";
    const string NarrationDir = "Assets/Sounds/Narration";
    const string GeneratedDir = "Assets/Data/Generated";
    const string PersistentPrefabPath = GeneratedDir + "/JourneyPersistent.prefab";
    const string PlayerPrefab = "Packages/com.meta.xr.sdk.core/Prefabs/OVRPlayerController.prefab";
    const string AcidVfxPrefab = "Assets/VFXPrefabs/Gastric Acid VFX.prefab";

    static readonly string[] RequiredTags = { "VRCamera", "Boat", "Hand", "Guide" };

    // 현재 빌드 중인 스테이지의 파츠 데이터 (id → name, line)
    static Dictionary<string, (string name, string line)> parts = new Dictionary<string, (string, string)>();
    static string stageId;
    static Transform labelTemplates;

    // ───────────────────────────── 메뉴 ─────────────────────────────

    [MenuItem("인체탐험/전체 생성 (자료 → 장면 → 빌드 등록)", priority = 0)]
    public static void BuildAll()
    {
        ImportStages();
        BuildScenes();
        RegisterBuildScenes();
        EditorUtility.DisplayDialog("인체탐험", "탐험 장면 7개를 생성했습니다. 로비 장면을 열어 확인하세요.", "확인");
    }

    [MenuItem("인체탐험/1. 단계 자료 불러오기", priority = 10)]
    public static void ImportStages()
    {
        EnsureTags();
        Directory.CreateDirectory(StageDir);
        var root = JObject.Parse(File.ReadAllText(ContentJson));
        foreach (var s in root["stages"])
        {
            string id = (string)s["stageId"];
            string path = $"{StageDir}/Stage_{id}.asset";
            var stage = AssetDatabase.LoadAssetAtPath<OrganStage>(path);
            if (stage == null) { stage = ScriptableObject.CreateInstance<OrganStage>(); AssetDatabase.CreateAsset(stage, path); }

            stage.stageId = id;
            stage.displayName = (string)s["displayName"];
            stage.sceneName = (string)s["sceneName"];
            stage.ridesCapsule = (bool?)s["ridesCapsule"] ?? false;
            ColorUtility.TryParseHtmlString((string)s["fadeColor"] ?? "#000000", out stage.fadeColor);
            stage.introLines = Lines(s["intro"], id, "intro");
            stage.outroLines = Lines(s["outro"], id, "outro");
            stage.summary = (string)s["summary"];
            stage.funFacts = s["funFacts"]?.Select(f => (string)f).ToArray() ?? Array.Empty<string>();
            EditorUtility.SetDirty(stage);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[JourneyBuilder] OrganStage 에셋 갱신 완료");
    }

    [MenuItem("인체탐험/2. 탐험 장면 생성 (7개)", priority = 11)]
    public static void BuildScenes() => BuildScenes(true);

    /// <param name="interactive">false 면 열린 씬 저장 여부를 묻지 않는다 (자동 빌드용 — 모달이 뜨면 원격에서 멈춘다)</param>
    public static void BuildScenes(bool interactive)
    {
        if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(SceneDir);
        Directory.CreateDirectory(GeneratedDir);
        Directory.CreateDirectory(GeneratedDir + "/Materials");
        AssetDatabase.Refresh();

        var root = JObject.Parse(File.ReadAllText(ContentJson));
        var stages = LoadStagesInOrder(root);
        var persistent = BuildPersistentPrefab(stages);

        foreach (var s in root["stages"])
        {
            stageId = (string)s["stageId"];
            parts = (s["parts"] ?? new JArray()).ToDictionary(p => (string)p["id"], p => ((string)p["name"], (string)p["line"]));
            var stage = stages.First(x => x.stageId == stageId);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            labelTemplates = new GameObject("LabelTemplates").transform;
            labelTemplates.gameObject.SetActive(false);
            new GameObject("PersistentBootstrap").AddComponent<PersistentBootstrap>().persistentPrefab = persistent;
            BuildCommon(stage);
            switch (stageId)
            {
                case "Lobby": BuildLobby(stages); break;
                case "Mouth": BuildMouth(); break;
                case "Esophagus": BuildEsophagus(); break;
                case "Stomach": BuildStomach(s); break;
                case "SmallIntestine": BuildSmallIntestine(s); break;
                case "LargeIntestine": BuildLargeIntestine(); break;
                case "Theater": BuildTheater(); break;
            }
            JourneyEnvironments.MatchEnclosures(stageId);
            JourneyEffectSetup.ConfigureScene(stageId);
            JourneyPolishSetup.ConfigureScene(stageId);
            JourneyRealism.ConfigureScene(stageId);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{stage.sceneName}.unity");
        }
        EditorSceneManager.OpenScene($"{SceneDir}/{stages[0].sceneName}.unity");
        Debug.Log("[JourneyBuilder] 씬 생성 완료");
    }

    [MenuItem("인체탐험/3. 빌드 설정에 탐험 장면 등록", priority = 12)]
    public static void RegisterBuildScenes()
    {
        var root = JObject.Parse(File.ReadAllText(ContentJson));
        var list = root["stages"].Select(s => new EditorBuildSettingsScene($"{SceneDir}/{(string)s["sceneName"]}.unity", true)).ToList();
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[JourneyBuilder] Build Settings 에 {list.Count}개 씬 등록 (기존 목록 대체)");
    }

    // ───────────────────────────── 데이터 헬퍼 ─────────────────────────────

    static List<OrganStage> LoadStagesInOrder(JObject root) =>
        root["stages"].Select(s => AssetDatabase.LoadAssetAtPath<OrganStage>($"{StageDir}/Stage_{(string)s["stageId"]}.asset")).ToList();

    static NarrationLine Line(string text, string voiceName) => new NarrationLine
    {
        text = text,
        voice = FindVoice(voiceName),
        holdSeconds = Mathf.Clamp((text ?? "").Length * 0.13f, 3f, 12f),
    };

    static NarrationLine[] Lines(JToken arr, string sid, string key)
    {
        if (arr == null) return Array.Empty<NarrationLine>();
        return arr.Select((t, i) => Line((string)t, $"{sid}_{key}_{i + 1}")).ToArray();
    }

    static AudioClip FindVoice(string baseName)
    {
        foreach (var ext in new[] { ".mp3", ".wav", ".ogg" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{NarrationDir}/{baseName}{ext}");
            if (clip != null) return clip;
        }
        return null;
    }

    static string[] Strings(JToken t) => t?.Select(x => (string)x).ToArray() ?? Array.Empty<string>();

    static void EnsureTags()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        foreach (var tag in RequiredTags)
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
            if (exists) continue;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }
        tagManager.ApplyModifiedProperties();
    }

    // ───────────────────────────── 공통 씬 요소 ─────────────────────────────

    static void BuildCommon(OrganStage stage)
    {
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        bool bright = stageId == "Lobby" || stageId == "Theater";
        light.color = bright ? new Color(1f, 0.98f, 0.95f) : new Color(1f, 0.85f, 0.8f);
        light.intensity = stageId == "Lobby" ? 1.2f : 0.6f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        light.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = stage.fadeColor * 2f + Color.white * 0.15f;
        RenderSettings.ambientEquatorColor = stage.fadeColor * 1.5f + Color.white * 0.1f;
        RenderSettings.ambientGroundColor = stage.fadeColor;
        RenderSettings.fog = !bright;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = stage.fadeColor * 2f;
        RenderSettings.fogDensity = 0.006f;

        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = GetOrCreateVolumeProfile(stage);

        if (!bright)
        {
            // 장기 내부의 가까운 모형을 밝히는 조명
            InteriorLight(new Vector3(0, 3f, 3f));
        }

        JourneyEnvironments.Configure(stage);
        new GameObject("SpawnPoint").transform.position = Vector3.zero;
    }

    static VolumeProfile GetOrCreateVolumeProfile(OrganStage stage)
    {
        Directory.CreateDirectory("Assets/Data/Volumes");
        string path = $"Assets/Data/Volumes/Volume_{stage.stageId}.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p != null) return p;
        p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        var bloom = p.Add<Bloom>(true); bloom.intensity.value = 0.6f; bloom.threshold.value = 1.1f; bloom.scatter.value = 0.65f;
        var vig = p.Add<Vignette>(true); vig.intensity.value = 0.25f; vig.smoothness.value = 0.4f;
        var ca = p.Add<ColorAdjustments>(true); ca.postExposure.value = 0.1f; ca.saturation.value = 8f; ca.contrast.value = 6f;
        var tone = p.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.ACES;
        foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    // ───────────────────────────── 모델/배치 헬퍼 ─────────────────────────────

    static GameObject LoadModel(params string[] candidates)
    {
        foreach (var c in candidates)
        {
            var animated = JourneyPolishSetup.AnimatedPrefab(c);
            if (animated != null) return animated;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{c}.glb");
            if (p != null) return p;
        }
        return null;
    }

    /// <summary>모델 인스턴스. 없으면 자리표시 큐브. size 를 주면 가장 긴 변이 size(m) 가 되도록 스케일.</summary>
    static GameObject Model(string name, Vector3 pos, float size = 0f, Vector3? euler = null, Transform parent = null, bool collider = false, string fallback = null)
    {
        var prefab = LoadModel(fallback == null ? new[] { name } : new[] { name, fallback });
        GameObject go;
        if (prefab == null)
        {
            Debug.LogWarning($"[JourneyBuilder] 모델 없음: {name}.glb → 자리표시 큐브");
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name + " (MISSING)";
        }
        else { go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name = name; }

        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
        if (size > 0f) Fit(go, size);
        if (collider) AddMeshColliders(go);
        return go;
    }

    static void AddMeshColliders(GameObject go)
    {
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>가장 긴 변이 size 가 되도록 균등 스케일.</summary>
    static void Fit(GameObject go, float size)
    {
        var b = WorldBounds(go);
        float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (longest > 0.0001f) go.transform.localScale *= size / longest;
    }

    /// <summary>바운즈 아래가 y 에 닿도록 내려놓기.</summary>
    static GameObject Ground(GameObject go, float y = 0f)
    {
        var b = WorldBounds(go);
        go.transform.position += Vector3.up * (y - b.min.y);
        return go;
    }

    /// <summary>바운즈 중심을 pos 로.</summary>
    static GameObject Center(GameObject go, Vector3 pos)
    {
        var b = WorldBounds(go);
        go.transform.position += pos - b.center;
        return go;
    }

    /// <summary>모델의 가장 긴 축을 지정 방향으로 돌린다 (터널 세그먼트 정렬용).</summary>
    static void AlignLongest(GameObject go, Vector3 worldDir)
    {
        go.transform.rotation = Quaternion.identity;
        var b = WorldBounds(go);
        Vector3 axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : (b.size.y >= b.size.z ? Vector3.up : Vector3.forward);
        go.transform.rotation = Quaternion.FromToRotation(axis, worldDir.normalized);
    }

    static GameObject Trigger(string name, Vector3 pos, Vector3 size, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return go;
    }

    static NarrationZone Zone(string name, Vector3 pos, Vector3 size, string[] lines, string key, bool oneShot = true)
    {
        var z = Trigger(name, pos, size).AddComponent<NarrationZone>();
        z.oneShot = oneShot;
        z.lines = Lines(JArray.FromObject(lines ?? Array.Empty<string>()), stageId, key);
        return z;
    }

    static StageExitPortal Portal(Vector3 pos, Vector3 size)
    {
        var p = Trigger("ExitPortal", pos, size).AddComponent<StageExitPortal>();
        p.playOutro = true;
        return p;
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Transform parent = null, bool emissive = false, float smoothness = 0.5f)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = LitMaterial(name, color, emissive, smoothness);
        return go;
    }

    /// <summary>씬에서 직접 만든 머티리얼은 저장되지 않으므로 항상 에셋으로 만든다.</summary>
    static Material LitMaterial(string name, Color color, bool emissive = false, float smoothness = 0.5f, bool transparent = false, bool doubleSided = false)
    {
        string path = $"{GeneratedDir}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        if (emissive) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 2f); }
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0); mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        if (doubleSided) mat.SetFloat("_Cull", (float)CullMode.Off);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>스케일된 모델에 월드 단위 크기의 BoxCollider 를 붙인다 (로컬 size 는 lossyScale 로 나눔).</summary>
    static BoxCollider WorldBox(GameObject go, Vector3 worldSize)
    {
        var box = go.AddComponent<BoxCollider>();
        var ls = go.transform.lossyScale;
        box.size = new Vector3(worldSize.x / Mathf.Max(0.0001f, ls.x), worldSize.y / Mathf.Max(0.0001f, ls.y), worldSize.z / Mathf.Max(0.0001f, ls.z));
        return box;
    }

    /// <summary>플레이어를 감싸는 셸(돔/외피)은 그림자를 드리우면 안이 새까매지므로 그림자·콜라이더를 끈다.</summary>
    static GameObject Enclosure(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        return go;
    }

    /// <summary>장기 안을 밝히는 따뜻한 포인트 라이트 (그림자 없음).</summary>
    static Light InteriorLight(Vector3 pos, float range = 30f, float intensity = 2.5f)
    {
        var l = new GameObject("InteriorLight").AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, 0.9f, 0.85f); l.shadows = LightShadows.None;
        l.transform.position = pos;
        return l;
    }

    /// <summary>발광 구 프리팹 (홀로그램 빛, 분비물 등).</summary>
    static GameObject GlowSpherePrefab(string name, Color color, float size)
    {
        string path = $"{GeneratedDir}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var go = Primitive(PrimitiveType.Sphere, name, Vector3.zero, Vector3.one * size, color, emissive: true);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ───────────────────────────── 파츠 이름표 ─────────────────────────────

    /// <summary>파츠에 이름표 + 설명 + 콜라이더를 붙인다. id 는 journey_content.json 의 parts[].id.</summary>
    static PartLabel Label(GameObject go, string id, bool meshCollider = true, float labelHeight = 0.4f)
    {
        if (go == null) return null;
        if (!parts.TryGetValue(id, out var data)) { Debug.LogWarning($"[JourneyBuilder] parts 데이터 없음: {stageId}/{id}"); data = ("이름 없음", ""); }
        var l = go.AddComponent<PartLabel>();
        l.partId = id; l.displayName = data.name;
        l.line = Line(data.line, $"{stageId}_part_{id}");
        if (go.GetComponentInChildren<Collider>() == null)
        {
            if (meshCollider) AddMeshColliders(go);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                // 반지름은 로컬 단위 → 월드 바운즈를 스케일로 나눠 맞춘다
                var wb = WorldBounds(go);
                float s = Mathf.Max(0.0001f, go.transform.lossyScale.x);
                var sc = go.AddComponent<SphereCollider>(); sc.isTrigger = true;
                sc.radius = Mathf.Max(0.15f, wb.extents.magnitude * 0.8f) / s;
                sc.center = go.transform.InverseTransformPoint(wb.center);
            }
        }

        // 이름표 캔버스 (바운즈 위)
        var b = WorldBounds(go);
        var canvasGo = new GameObject("Label", typeof(Canvas));
        canvasGo.transform.SetParent(go.transform, false);
        canvasGo.transform.position = new Vector3(b.center.x, b.max.y + labelHeight, b.center.z);
        canvasGo.transform.localScale = Vector3.one * 0.003f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);
        canvasGo.AddComponent<Billboard>().yawOnly = true;
        var bg = new GameObject("Bg", typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(canvasGo.transform, false); bg.color = new Color(0.05f, 0.08f, 0.15f, 0.8f);
        Stretch(bg.rectTransform);
        var txt = new GameObject("Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        txt.transform.SetParent(canvasGo.transform, false);
        txt.fontSize = 36; txt.alignment = TextAlignmentOptions.Center; txt.color = new Color(1f, 0.95f, 0.7f); txt.text = data.name;
        Stretch(txt.rectTransform);
        AssignKoreanFont(txt);
        l.labelRoot = canvasGo; l.labelText = txt;
        canvasGo.SetActive(false);
        return l;
    }

    /// <summary>동적으로 생성되는 개체용 템플릿 (FloatingSwarm/SplineFlow 등이 복사).</summary>
    static PartLabel LabelTemplate(string id)
    {
        if (!parts.TryGetValue(id, out var data)) data = ("이름 없음", "");
        var go = new GameObject($"Template_{id}");
        go.transform.SetParent(labelTemplates, false);
        var l = go.AddComponent<PartLabel>();
        l.partId = id; l.displayName = data.name; l.line = Line(data.line, $"{stageId}_part_{id}");
        return l;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    static void AssignKoreanFont(TMP_Text text)
    {
        foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("KoreanTyper") || path.Contains("Korean") || path.Contains("Nanum") || path.Contains("Noto"))
            { text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); return; }
        }
    }

    static ParticleSystem Emitter(string name, Vector3 pos, Color color, float size, float rate, float speed, Transform parent = null, bool cone = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startColor = color; main.startSize = size; main.startSpeed = speed; main.startLifetime = 2.5f; main.gravityModifier = 0.15f; main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = cone ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere; shape.angle = 25f; shape.radius = 0.1f;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial($"Particle_{name}", color);
        return ps;
    }

    static Material ParticleMaterial(string name, Color color)
    {
        string path = $"{GeneratedDir}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader); AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f); mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent; mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────────────────────────── 터널 / 라이드 ─────────────────────────────

    /// <summary>관 모델의 축(나머지 두 변과 가장 다른 변)을 worldDir 로 돌린다. 지름이 길이보다 큰 짧은 관도 처리.</summary>
    static void AlignTubeAxis(GameObject go, Vector3 worldDir)
    {
        go.transform.rotation = Quaternion.identity;
        var sz = WorldBounds(go).size;
        float dx = Mathf.Abs(sz.x - sz.y) + Mathf.Abs(sz.x - sz.z);
        float dy = Mathf.Abs(sz.y - sz.x) + Mathf.Abs(sz.y - sz.z);
        float dz = Mathf.Abs(sz.z - sz.x) + Mathf.Abs(sz.z - sz.y);
        Vector3 axis = dx >= dy && dx >= dz ? Vector3.right : (dy >= dz ? Vector3.up : Vector3.forward);
        go.transform.rotation = Quaternion.FromToRotation(axis, worldDir.normalized);
    }

    /// <summary>터널 세그먼트를 worldDir 방향으로 실제 길이만큼 이어 붙이고 연동운동 펄스를 건다. 단면 지름을 diameter 에 맞춘다.</summary>
    static GameObject Tunnel(string model, string fallback, int count, Vector3 start, Vector3 worldDir, float diameter, out PeristalsisPulse[] pulses, out float totalLength)
    {
        var parent = new GameObject("Tunnel");
        pulses = new PeristalsisPulse[count];
        Vector3 dir = worldDir.normalized;
        Vector3 ax = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
        float cursor = 0f;
        for (int i = 0; i < count; i++)
        {
            var seg = Model(model, Vector3.zero, 0f, null, parent.transform, fallback: fallback);
            AlignTubeAxis(seg, dir);
            var b = WorldBounds(seg);
            float cross = Mathf.Max(b.size.x * (1 - ax.x), b.size.y * (1 - ax.y), b.size.z * (1 - ax.z));
            if (cross > 0.001f) seg.transform.localScale *= diameter / cross;
            b = WorldBounds(seg);
            float len = b.size.x * ax.x + b.size.y * ax.y + b.size.z * ax.z;
            Center(seg, start + dir * (cursor + len * 0.5f));
            cursor += len * 0.96f; // 살짝 겹쳐 이음새 감춤
            AddMeshColliders(seg);
            foreach (var r in seg.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = seg.AddComponent<PeristalsisPulse>();
            p.phaseOffset = (float)i / count;
            p.axisWeight = Vector3.one - ax;
            p.emissiveRenderer = seg.GetComponentInChildren<Renderer>();
            pulses[i] = p;
        }
        totalLength = cursor;
        return parent;
    }

    static CapsuleRide Ride(Vector3[] knots, CapsuleRide.RideStop[] stops, bool autoBoard, float delay, float speed)
    {
        var container = new GameObject("Ride Spline").AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();
        foreach (var k in knots) spline.Add(new BezierKnot(k), TangentMode.AutoSmooth);

        var capsule = Model("Prop_ExplorerCapsule", knots[0], 2.2f);
        capsule.tag = "Boat";
        var cart = capsule.AddComponent<CinemachineSplineCart>();
        cart.Spline = container;
        cart.PositionUnits = PathIndexUnit.Normalized;
        cart.AutomaticDolly.Enabled = false;

        var seat = new GameObject("SeatPoint").transform; seat.SetParent(capsule.transform, true); seat.position = capsule.transform.position + new Vector3(0, 0.2f, 0);
        var guideSeat = new GameObject("GuideSeatPoint").transform; guideSeat.SetParent(capsule.transform, true); guideSeat.position = capsule.transform.position + new Vector3(0.7f, 1.3f, 0.6f);
        var off = new GameObject("GetOffPoint").transform; off.SetParent(capsule.transform, true); off.position = capsule.transform.position + new Vector3(1.8f, 0, 0);
        foreach (var t in new[] { seat, guideSeat, off }) { var l = capsule.transform.lossyScale; t.localScale = new Vector3(1f / l.x, 1f / l.y, 1f / l.z); }

        // 멀미 완화용 고정 참조물: 좌석 주변 난간 4개 + 앞 프레임
        var rail = new GameObject("Railing").transform; rail.SetParent(capsule.transform, false); rail.localPosition = new Vector3(0, 0.2f, 0);
        // 캡슐 모델의 Fit 스케일이 자식에 곱해지지 않도록 역보정 (난간은 항상 실제 미터 단위)
        var ls = capsule.transform.lossyScale;
        rail.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
        rail.localPosition = new Vector3(0, 0.2f / Mathf.Max(0.0001f, ls.y), 0);
        var railColor = new Color(0.85f, 0.9f, 0.95f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            var post = Primitive(PrimitiveType.Cylinder, "RailPost", Vector3.zero, new Vector3(0.04f, 0.5f, 0.04f), railColor, rail, smoothness: 0.8f);
            post.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.8f, 0.5f, Mathf.Cos(a) * 0.8f);
            UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            var bar = Primitive(PrimitiveType.Cube, "RailBar", Vector3.zero, new Vector3(1.13f, 0.04f, 0.04f), railColor, rail, smoothness: 0.8f);
            bar.transform.localPosition = new Vector3(Mathf.Sin(a + 0.785f) * 0.566f, 1.0f, Mathf.Cos(a + 0.785f) * 0.566f);
            bar.transform.localRotation = Quaternion.Euler(0, -(i * 90f + 45f), 0);
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
        }
        var frame = Primitive(PrimitiveType.Cube, "FrontFrame", Vector3.zero, new Vector3(1.4f, 0.05f, 0.05f), railColor, rail, smoothness: 0.8f);
        frame.transform.localPosition = new Vector3(0, 1.15f, 0.9f); // 눈높이(1.8) 아래
        UnityEngine.Object.DestroyImmediate(frame.GetComponent<Collider>());

        var ride = capsule.AddComponent<CapsuleRide>();
        ride.cart = cart; ride.seatPoint = seat; ride.guideSeatPoint = guideSeat; ride.getOffPoint = off;
        ride.stops = stops; ride.autoBoard = autoBoard; ride.autoBoardDelay = delay; ride.speed = speed; ride.advanceOnArrive = true;
        return ride;
    }

    static CapsuleRide.RideStop Stop(float pos, string text, string key) => new CapsuleRide.RideStop
    {
        normalizedPosition = pos,
        lines = new[] { Line(text, $"{stageId}_{key}") },
        onReached = new UnityEngine.Events.UnityEvent(),
    };

    static SplineContainer Spline(string name, Vector3[] knots, bool closed, Transform parent = null)
    {
        var c = new GameObject(name).AddComponent<SplineContainer>();
        c.transform.SetParent(parent, false);
        c.Spline.Clear();
        foreach (var k in knots) c.Spline.Add(new BezierKnot(k), TangentMode.AutoSmooth);
        c.Spline.Closed = closed;
        return c;
    }

    // ───────────────────────────── 영속 프리팹 (매니저 + 플레이어 + 가이드) ─────────────────────────────

    static GameObject BuildPersistentPrefab(List<OrganStage> stages)
    {
        var rootGo = new GameObject("JourneyPersistent");

        var jm = new GameObject("JourneyManager").AddComponent<JourneyManager>();
        jm.transform.SetParent(rootGo.transform, false);
        jm.stages = stages;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        GameObject player;
        if (playerPrefab != null) player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        else { player = new GameObject("OVRPlayerController (MISSING)"); var cam = new GameObject("Camera").AddComponent<Camera>(); cam.transform.SetParent(player.transform); cam.tag = "MainCamera"; }
        player.name = "Player";
        player.tag = "Player";
        player.transform.SetParent(rootGo.transform, false);
        var ovrPc = player.GetComponent<OVRPlayerController>();
        if (ovrPc != null) { ovrPc.SnapRotation = true; ovrPc.RotationRatchet = 30f; ovrPc.Acceleration = 0.06f; }
        player.AddComponent<ComfortLocomotion>();
        player.AddComponent<QuestSettings>();
        var eye = player.transform.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (eye != null)
        {
            eye.tag = "VRCamera";
            eye.gameObject.AddComponent<GazeSelector>();
            eye.gameObject.AddComponent<ComfortVignette>().motionSource = player.transform;
            // OVRScreenFade 는 자기 MeshFilter/Renderer 를 만들므로 이미 렌더러가 있는 CenterEyeAnchor 대신 자식에 둔다
            var fadeGo = new GameObject("ScreenFade");
            fadeGo.transform.SetParent(eye, false);
            var fade = fadeGo.AddComponent<OVRScreenFade>();
            fade.fadeOnStart = true; fade.fadeTime = 1f;
            if (eye.GetComponent<Collider>() == null) { var sc = eye.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.3f; }
        }

        // 손 콜라이더: 영양소를 손으로 만져 흡수 (Nutrient.OnTriggerEnter 의 "Hand" 태그)
        foreach (var handPath in new[] { "OVRCameraRig/TrackingSpace/LeftHandAnchor", "OVRCameraRig/TrackingSpace/RightHandAnchor" })
        {
            var hand = player.transform.Find(handPath);
            if (hand == null) continue;
            var hc = new GameObject("HandTouch"); hc.transform.SetParent(hand, false); hc.tag = "Hand";
            var sc = hc.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.08f;
            var rb = hc.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        }

        BuildGuide().transform.SetParent(rootGo.transform, true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PersistentPrefabPath);
        UnityEngine.Object.DestroyImmediate(rootGo);
        return prefab;
    }

    static GameObject BuildGuide()
    {
        var guide = Model("Char_ChickDoctor", new Vector3(1.2f, 0.9f, -4.5f), 1.0f);
        guide.name = "Guide_ChickDoctor";
        guide.tag = "Guide";
        var follower = guide.AddComponent<FloatingFollower>();
        follower.distance = 2.2f; follower.height = 0f; follower.angleBias = 25f; follower.floatAmplitude = 0.08f; follower.rotationOffset = 0f;

        var audio = guide.AddComponent<AudioSource>();
        audio.spatialBlend = 1f; audio.minDistance = 1f; audio.maxDistance = 15f;
        var narrator = guide.AddComponent<GuideNarrator>();

        var b = WorldBounds(guide);
        var canvasGo = new GameObject("SpeechBubble", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(guide.transform, false);
        canvasGo.transform.position = new Vector3(b.center.x, b.max.y + 0.25f, b.center.z);
        canvasGo.transform.localScale = Vector3.one * 0.0023f / Mathf.Max(0.0001f, guide.transform.lossyScale.x); // 폭 520 → 약 1.2m
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(520, 200);
        canvasGo.AddComponent<Billboard>().yawOnly = true;

        var bg = new GameObject("Background", typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(canvasGo.transform, false); bg.color = new Color(1f, 1f, 1f, 0.92f);
        Stretch(bg.rectTransform);
        var text = new GameObject("Subtitle", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(canvasGo.transform, false);
        text.fontSize = 34; text.color = new Color(0.1f, 0.1f, 0.15f); text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
        Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(20, 16); text.rectTransform.offsetMax = new Vector2(-20, -16);
        AssignKoreanFont(text);

        narrator.speechBubble = canvasGo;
        narrator.subtitleText = text;
        return guide;
    }

    // ───────────────────────────── 0. 로비 ─────────────────────────────

    static void BuildLobby(List<OrganStage> stages)
    {
        Primitive(PrimitiveType.Plane, "LobbyFloor", Vector3.zero, new Vector3(4, 1, 4), new Color(0.92f, 0.94f, 0.97f), smoothness: 0.7f);
        var ring = Primitive(PrimitiveType.Cylinder, "CeilingRing", new Vector3(0, 7, 0), new Vector3(30, 0.15f, 30), new Color(0.75f, 0.85f, 1f), emissive: true);
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, 0, -6);

        var exhibits = new (string model, float size, OrganStage data, string title, string partId)[]
        {
            ("Exhibit_DigestiveSystem", 1.7f, stages[0], "소화기관 전체", "DigestiveSystem"),
            ("Prop_Tooth", 0.6f, stages[1], "어금니", null),
            ("Prop_SalivaGland", 0.7f, stages[1], "침샘", null),
            ("Exhibit_HumanTorsoCutaway", 1.7f, stages[3], "몸속 들여다보기", "Torso"),
            ("Prop_Villus", 0.8f, stages[4], "융털", null),
            ("Char_Probiotic", 0.7f, stages[5], "장내 세균", null),
        };
        var exhibitRoot = new GameObject("Exhibits").transform;
        for (int i = 0; i < exhibits.Length; i++)
        {
            float ang = Mathf.Lerp(-110f, 110f, (float)i / (exhibits.Length - 1)) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(ang) * 7f, 0, Mathf.Cos(ang) * 7f);
            var ped = Ground(Model("Lobby_Pedestal", pos, 1.1f, new Vector3(0, -ang * Mathf.Rad2Deg, 0), exhibitRoot, fallback: "Lobby_ExhibitPedestal"));
            float top = WorldBounds(ped).max.y;
            var e = exhibits[i];
            var m = Ground(Model(e.model, pos, e.size, null, ped.transform), top + 0.05f);
            m.AddComponent<Rotator>().degreesPerSecond = new Vector3(0, 15, 0);

            var station = m.AddComponent<ExhibitStation>();
            station.stage = e.data; station.overrideTitle = e.title;
            station.highlightRenderers = m.GetComponentsInChildren<Renderer>();
            if (e.partId != null && parts.TryGetValue(e.partId, out var pd)) station.voiceLine = Line(pd.line, $"{stageId}_part_{e.partId}");
            AddMeshColliders(m);
            BuildInfoPanel(station, pos + Vector3.up * (top + e.size + 0.8f), Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0));
        }

        // 중앙 홀로그램 테이블 + 미니 소화계 홀로그램
        var table = Ground(Model("Lobby_HologramTable", Vector3.zero, 1.6f));
        var holo = Ground(Model("Exhibit_DigestiveSystem", Vector3.zero, 1.2f, null, table.transform), WorldBounds(table).max.y + 0.2f);
        holo.name = "Hologram_DigestiveSystem";
        holo.AddComponent<Rotator>().degreesPerSecond = new Vector3(0, 25, 0);
        foreach (var r in holo.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LitMaterial("Hologram", new Color(0.4f, 0.8f, 1f, 0.45f), emissive: true, transparent: true);

        Ground(Model("Lobby_InfoKiosk", new Vector3(-4, 0, -4), 1.8f, new Vector3(0, 45, 0)));

        // 입구 아치 + 출구 포털
        var gate = Ground(Model("Lobby_MouthArch", new Vector3(0, 0, 11), 8f, new Vector3(0, 180, 0), fallback: "Lobby_EntranceGate"));
        Portal(new Vector3(0, 1.5f, 11.5f), new Vector3(5, 4, 1.5f));
    }

    static void BuildInfoPanel(ExhibitStation station, Vector3 pos, Quaternion rot)
    {
        var canvasGo = new GameObject("InfoPanel", typeof(Canvas));
        canvasGo.transform.position = pos; canvasGo.transform.rotation = rot;
        canvasGo.transform.localScale = Vector3.one * 0.004f;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 420);

        var bg = new GameObject("Background", typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(canvasGo.transform, false); bg.color = new Color(0.05f, 0.1f, 0.2f, 0.85f);
        Stretch(bg.rectTransform);

        var title = new GameObject("Title", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(canvasGo.transform, false);
        title.fontSize = 44; title.fontStyle = FontStyles.Bold; title.color = new Color(0.6f, 0.9f, 1f);
        var tr = title.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -20); tr.sizeDelta = new Vector2(-40, 60);

        var body = new GameObject("Body", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        body.transform.SetParent(canvasGo.transform, false);
        body.fontSize = 26; body.color = Color.white; body.textWrappingMode = TextWrappingModes.Normal;
        Stretch(body.rectTransform); body.rectTransform.offsetMin = new Vector2(24, 20); body.rectTransform.offsetMax = new Vector2(-24, -90);
        AssignKoreanFont(title); AssignKoreanFont(body);

        station.panel = canvasGo; station.titleText = title; station.bodyText = body;
        canvasGo.SetActive(false);
    }

    // ───────────────────────────── 1. 입 ─────────────────────────────

    static void BuildMouth()
    {
        // 혀: 길이 10m 로 눕히고(긴 축 = Z), 바닥에 놓은 뒤 표면 높이를 레이캐스트로 찾아 그 위에 플레이어를 세운다
        var tongue = Model("Part_Tongue", Vector3.zero, 10f);
        AlignLongest(tongue, Vector3.forward);
        Center(tongue, new Vector3(0, 0, 3f)); Ground(tongue, -0.2f);
        AddMeshColliders(tongue);
        Physics.SyncTransforms();
        float surfaceY = 0.6f;
        if (Physics.Raycast(new Vector3(0, 30f, 1f), Vector3.down, out var hit, 60f)) surfaceY = hit.point.y;
        var tonguePivot = new GameObject("TonguePivot"); tonguePivot.transform.position = new Vector3(0, surfaceY, 8f); // 뿌리(뒤쪽) 피벗
        tongue.transform.SetParent(tonguePivot.transform, true);
        Label(tongue, "Tongue", meshCollider: false, labelHeight: 0.6f);
        // 보이지 않는 안전 바닥 (혀 표면 높이)
        var safety = new GameObject("TongueWalkSurface"); safety.transform.position = new Vector3(0, surfaceY - 0.05f, 2f);
        var sbc = safety.AddComponent<BoxCollider>(); sbc.size = new Vector3(6f, 0.1f, 9f);
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, surfaceY + 0.05f, -0.5f);
        float eyeY = surfaceY + 1.6f;

        // 치아 아치: 아랫니는 혀 높이 주변, 윗니는 눈높이 위 3m. 아래턱 피벗(뒤쪽 경첩)이 씹기 회전.
        var lower = Model("Part_LowerTeethArch", Vector3.zero, 12f); AlignLongest(lower, Vector3.right);
        lower.transform.rotation = Quaternion.Euler(0, 180, 0) * lower.transform.rotation; // U 의 열린 쪽이 목구멍(+Z)을 향하도록
        Center(lower, new Vector3(0, surfaceY, 2.5f)); Ground(lower, surfaceY - 0.6f);
        var upper = Model("Part_UpperTeethArch", Vector3.zero, 12f); AlignLongest(upper, Vector3.right);
        upper.transform.rotation = Quaternion.Euler(180, 0, 0) * upper.transform.rotation; // 뒤집고(치관 아래) 열린 쪽 +Z
        Center(upper, new Vector3(0, eyeY + 2.6f, 2.5f));
        Label(lower, "LowerTeeth", labelHeight: 0.5f); Label(upper, "UpperTeeth", labelHeight: 0.5f);
        var jawPivot = new GameObject("LowerJawPivot"); jawPivot.transform.position = new Vector3(0, surfaceY, 8f);
        lower.transform.SetParent(jawPivot.transform, true);

        // 입 안 벽: 양면 렌더 돔 + 입천장
        var dome = Primitive(PrimitiveType.Sphere, "MouthDome", new Vector3(0, eyeY + 0.5f, 3f), new Vector3(16, 11, 20), new Color(0.85f, 0.45f, 0.5f));
        dome.GetComponent<Renderer>().sharedMaterial = LitMaterial("MouthDome", new Color(0.85f, 0.45f, 0.5f), smoothness: 0.8f, doubleSided: true);
        Enclosure(dome);
        var palate = Model("Part_Palate", Vector3.zero, 8f); AlignLongest(palate, Vector3.forward);
        palate.transform.rotation *= Quaternion.Euler(180, 0, 0); // 주름면이 아래(입 안)를 향하도록 (모델에 따라 조정)
        Center(palate, new Vector3(0, eyeY + 5.2f, 3f)); // 윗니 아치 위쪽, 돔 천장(eyeY+6) 아래

        // 목젖 (천장 뒤쪽, 흔들림) / 후두덮개 (혀 뿌리 뒤 아래)
        var uvula = Model("Part_Uvula", Vector3.zero, 1.6f); Center(uvula, new Vector3(0, eyeY + 1.9f, 8.5f));
        var uvulaPivot = new GameObject("UvulaPivot"); uvulaPivot.transform.position = new Vector3(0, WorldBounds(uvula).max.y, 8.5f);
        uvula.transform.SetParent(uvulaPivot.transform, true);
        uvulaPivot.AddComponent<Sway>().angle = 8f;
        Label(uvula, "Uvula");
        var epi = Model("Part_Epiglottis", Vector3.zero, 2.2f); Center(epi, new Vector3(0, surfaceY + 0.8f, 10f));
        var epiPivot = new GameObject("EpiglottisPivot"); epiPivot.transform.position = new Vector3(0, WorldBounds(epi).min.y, 10f);
        epi.transform.SetParent(epiPivot.transform, true);
        Label(epi, "Epiglottis");

        // 침샘 3쌍 (양옆 벽) + 침 분비
        var glandRoot = new GameObject("SalivaryGlands").transform;
        // 귀밑샘(위쪽 벽), 턱밑샘(아래턱 바깥), 혀밑샘(혀 앞 양옆) — 치아 아치(폭 12m, z 2.5 중심)와 겹치지 않는 자리
        var glandPositions = new[] { new Vector3(-7f, eyeY + 0.6f, -1f), new Vector3(7f, eyeY + 0.6f, -1f), new Vector3(-6.5f, surfaceY + 0.6f, 6.5f), new Vector3(6.5f, surfaceY + 0.6f, 6.5f), new Vector3(-2.75f, surfaceY - 0.35f, -0.6f), new Vector3(2.75f, surfaceY - 0.35f, -0.6f) }; // 혀밑샘은 혀끝 양옆(혀 폭 4.5m 바깥), 아치 안쪽
        var salivaEmitters = new List<ParticleSystem>();
        for (int i = 0; i < glandPositions.Length; i++)
        {
            var g = Model("Prop_SalivaGland", Vector3.zero, 1.1f, null, glandRoot); Center(g, glandPositions[i]);
            if (i == 0 || i == 2 || i == 4) Label(g, "SalivaryGland");
            var em = Emitter($"Saliva_{i}", glandPositions[i], new Color(0.75f, 0.9f, 1f, 0.6f), 0.06f, 8f, 1.2f, g.transform);
            em.transform.rotation = Quaternion.LookRotation(Vector3.down + (new Vector3(0, surfaceY, 2f) - glandPositions[i]).normalized * 0.5f);
            var main = em.main; main.playOnAwake = false; salivaEmitters.Add(em);
        }

        // 음식 덩어리 + 씹기 + 삼킴
        var bolus = Ground(Model("Prop_FoodBolus", new Vector3(0, 0, 3f), 1.2f), surfaceY);
        Label(bolus, "Bolus");
        var chew = new GameObject("ChewingSequence").AddComponent<ChewingSequence>();
        chew.upperJaw = jawPivot.transform; chew.jawOpenAngle = -14f; chew.foodBolus = bolus.transform; chew.salivaVfx = salivaEmitters[0]; chew.salivaEmitters = salivaEmitters.ToArray();
        var throat = new GameObject("ThroatPoint"); throat.transform.position = new Vector3(0, surfaceY + 1.2f, 11f);
        var swallow = new GameObject("SwallowSequence").AddComponent<SwallowSequence>();
        swallow.tongue = tonguePivot.transform; swallow.bolus = bolus.transform; swallow.uvula = uvulaPivot.transform; swallow.epiglottis = epiPivot.transform; swallow.throatPoint = throat.transform;
        swallow.tongueLiftAngle = -10f;
        swallow.pullPlayer = false; // 음식 삼킴을 관찰한 뒤 화면 전환으로 탐험 경로를 따라간다.

        // 진행: 입장 인트로 → 앞으로 걸어가면 씹기 멘트 → 씹기 → 삼킴 → 다음 씬
        var zone = Zone("ChewZone", new Vector3(0, surfaceY + 1.5f, 2.5f), new Vector3(6, 3, 3), new[] { "이가 음식을 꼭꼭 씹는 모습을 함께 살펴보자!" }, "chew");
        UnityEventTools.AddPersistentListener(zone.onFinished, chew.Begin);
        UnityEventTools.AddPersistentListener(chew.onChewingFinished, swallow.Begin);
        // 침샘의 시작과 종료는 씹기 연출에서 일괄 관리한다.
    }

    // ───────────────────────────── 2. 식도 (수직 하강 라이드) ─────────────────────────────

    static void BuildEsophagus()
    {
        const int segs = 6;
        Tunnel("Env_EsophagusTunnel", null, segs, new Vector3(0, 0, 0), Vector3.down, 5.5f, out var pulses, out float totalLen);
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, -3.5f, 0);
        Primitive(PrimitiveType.Cylinder, "EsophagusTopPlatform", new Vector3(0, -3.6f, 0), new Vector3(4, 0.1f, 4), new Color(0.8f, 0.4f, 0.45f));
        // 위쪽 입구 막기 (목구멍 천장)
        var cap = Primitive(PrimitiveType.Sphere, "ThroatCap", new Vector3(0, 1.5f, 0), new Vector3(7, 4, 7), new Color(0.8f, 0.4f, 0.45f));
        cap.GetComponent<Renderer>().sharedMaterial = LitMaterial("ThroatCap", new Color(0.8f, 0.4f, 0.45f), smoothness: 0.7f, doubleSided: true);
        Enclosure(cap);
        InteriorLight(new Vector3(0, -18f, 0), 40f, 2f);

        // 후두덮개: 위쪽 갈림길 표시
        var epi = Model("Part_Epiglottis", Vector3.zero, 1.5f); Center(epi, new Vector3(1.8f, -1.6f, 0.5f)); Label(epi, "Epiglottis");

        // 연동파 링
        var ring = LoadModel("Part_MuscleRing");
        var wave = new GameObject("PeristalsisWave").AddComponent<PeristalsisWave>();
        wave.ringPrefab = ring; wave.ringCount = 3; wave.ringDiameter = 5.0f; wave.squeezeScale = 0.75f;
        wave.startLocal = new Vector3(0, -3f, 0); wave.endLocal = new Vector3(0, -totalLen + 2f, 0); wave.travelSeconds = 9f; wave.ringEuler = new Vector3(90, 0, 0);
        wave.labelTemplate = LabelTemplate("MuscleRing");

        // 분문 (맨 아래) + 게이트
        float bottomY = -totalLen;
        var cardia = Model("Part_Sphincter", Vector3.zero, 4.5f); AlignLongest(cardia, Vector3.right);
        cardia.transform.rotation = Quaternion.Euler(90, 0, 0) * cardia.transform.rotation; Center(cardia, new Vector3(0, bottomY + 1f, 0));
        var gate = cardia.AddComponent<SphincterGate>(); gate.openScale = 1.7f; gate.openSeconds = 2f;
        gate.blocker = WorldBox(cardia, new Vector3(4, 0.4f, 4));
        Label(cardia, "Cardia", meshCollider: false, labelHeight: 0.8f);

        var stops = new[]
        {
            Stop(0.30f, "벽이 차례로 조여지는 모습이 보이지? 이게 연동 운동이야. 근육이 위에서 아래로 수축하면서 음식을 밀어내고 있어.", "ride_1"),
            Stop(0.80f, "식도 아래쪽에 도착했어. 저기 닫혀 있는 문이 보이지? 위로 들어가는 분문이야.", "ride_2"),
        };
        UnityEventTools.AddPersistentListener(stops[1].onReached, gate.Open);
        var knots = new[] { new Vector3(0, -3.5f, 0), new Vector3(0.6f, bottomY * 0.25f, 0.3f), new Vector3(-0.5f, bottomY * 0.5f, -0.3f), new Vector3(0.3f, bottomY * 0.75f, 0.2f), new Vector3(0, bottomY - 1f, 0) };
        Ride(knots, stops, autoBoard: true, delay: 7f, speed: 0.022f);
    }

    // ───────────────────────────── 3. 위 ─────────────────────────────

    static void BuildStomach(JToken json)
    {
        // 위 내부 = 양면 타원 셸 (Meshy 챔버 모델은 안에 들어갈 수 있는 형태가 아니라 외피로만 사용)
        var chamber = Primitive(PrimitiveType.Sphere, "StomachShell", new Vector3(0, 4f, 2f), new Vector3(18f, 13f, 22f), new Color(0.85f, 0.4f, 0.45f));
        chamber.GetComponent<Renderer>().sharedMaterial = LitMaterial("StomachWall", new Color(0.85f, 0.4f, 0.45f), doubleSided: true, smoothness: 0.8f);
        Enclosure(chamber);
        var wallPulse = chamber.AddComponent<PeristalsisPulse>(); wallPulse.amplitude = 0.02f; wallPulse.period = 5f; wallPulse.axisWeight = Vector3.one;
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, 0, -4);
        Primitive(PrimitiveType.Cylinder, "StomachPlatform", new Vector3(0, -0.1f, -3), new Vector3(7, 0.1f, 7), new Color(0.75f, 0.3f, 0.35f));

        // 위벽 주름 패널 4장 + 위샘 3개 (3색 분비)
        var wallRoot = new GameObject("Walls").transform;
        for (int i = 0; i < 4; i++)
        {
            float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(ang) * 8.2f, 3.5f, Mathf.Cos(ang) * 9.8f + 2f); // 셸 벽(9,6.5,11 반지름)에 붙임
            var panel = Model("Part_RugaeWallPanel", Vector3.zero, 7f, null, wallRoot); Center(panel, pos);
            panel.transform.rotation = Quaternion.LookRotation(new Vector3(0, 3.5f, 2f) - pos); // 안쪽(중심)을 향함
            if (i == 0) Label(panel, "Rugae", labelHeight: 0.6f);
            var pp = panel.AddComponent<PeristalsisPulse>(); pp.amplitude = 0.04f; pp.phaseOffset = i * 0.25f;
        }
        var glandColors = new[] { new Color(0.8f, 0.95f, 1f, 0.7f), new Color(1f, 0.85f, 0.3f, 0.8f), new Color(0.6f, 1f, 0.5f, 0.8f) }; // 점액 / 염산 / 펩신
        // 위샘은 벽에 붙는다: 좌·우 벽(셸 x반지름 9) + 천장(셸 y반지름 6.5, 중심 y4)
        var glandPositions = new[] { new Vector3(-8.2f, 4.5f, 1f), new Vector3(8.2f, 4.5f, 1f), new Vector3(0f, 9.6f, 2f) };
        var glandEulers = new[] { new Vector3(0, 0, -90), new Vector3(0, 0, 90), new Vector3(180, 0, 0) };
        for (int i = 0; i < 3; i++)
        {
            var pos = glandPositions[i];
            var gland = Model("Part_GastricGland", Vector3.zero, 1.6f, null, wallRoot); Center(gland, pos);
            gland.transform.rotation = Quaternion.Euler(glandEulers[i]);
            if (i == 0) Label(gland, "GastricGland");
            // 분비물은 벽에서 위 중심(0,3,2) 쪽으로
            var toCenter = (new Vector3(0, 3f, 2f) - pos).normalized;
            Emitter($"GlandSecretion_{i}", pos + toCenter * 0.8f, glandColors[i], 0.08f, 12f, 1.5f, gland.transform).transform.rotation = Quaternion.LookRotation(toCenter);
        }

        // 점액층: 반투명 내부 셸
        var mucus = Primitive(PrimitiveType.Sphere, "MucusLayer", new Vector3(0, 4f, 2f), new Vector3(16.5f, 12f, 20f), new Color(0.8f, 0.9f, 1f, 0.05f));
        mucus.GetComponent<Renderer>().sharedMaterial = LitMaterial("MucusLayer", new Color(0.8f, 0.9f, 1f, 0.05f), smoothness: 0.98f, transparent: true, doubleSided: true);
        Enclosure(mucus);
        var mucusLabel = new GameObject("MucusLabelAnchor"); mucusLabel.transform.position = new Vector3(-4.5f, 1.5f, 3f);
        var mlc = mucusLabel.AddComponent<SphereCollider>(); mlc.isTrigger = true; mlc.radius = 0.8f;
        Label(mucusLabel, "Mucus", meshCollider: false);

        // 음식 → 미즙 + 위산 VFX + 휘젓기
        var digestion = new GameObject("Digestion").transform;
        var foods = new List<GameObject>(); var chymes = new List<GameObject>();
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(Mathf.Cos(i * 1.57f) * 2.5f, 0.6f, Mathf.Sin(i * 1.57f) * 2.5f + 2f);
            foods.Add(Model("Prop_FoodBolus", p, 1f, null, digestion));
            var c = Model("Prop_ChymeBlob", p, 1f, null, digestion); c.SetActive(false); chymes.Add(c);
        }
        var acidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AcidVfxPrefab);
        for (int i = 0; i < 3 && acidPrefab != null; i++)
        {
            var a = (GameObject)PrefabUtility.InstantiatePrefab(acidPrefab);
            a.transform.SetParent(digestion, false); a.transform.position = new Vector3(-3 + i * 3, 4, 3);
        }
        var amb = new GameObject("Ambience").AddComponent<SceneAmbience>(); amb.clipName = "gurgle"; amb.volume = 0.3f;
        var churn = new GameObject("StomachChurn").AddComponent<StomachChurn>();
        churn.foodBoluses = foods.ToArray(); churn.chymeBlobs = chymes.ToArray(); churn.walls = new[] { wallPulse };

        // 펩신 + 단백질 사슬 절단
        var pepsin = Model("Char_Pepsin", new Vector3(3f, 1.5f, -1f), 1.1f); Label(pepsin, "Pepsin");
        var chains = new List<Transform>();
        for (int i = 0; i < 3; i++) { var ch = Model("Part_ProteinChain", new Vector3(-2f + i * 2f, 1.2f, 5f), 1.4f, null, digestion); chains.Add(ch.transform); }
        var cut = new GameObject("EnzymeCut").AddComponent<EnzymeCutSequence>();
        cut.enzyme = pepsin.transform; cut.proteinChains = chains.ToArray(); cut.fragmentPrefab = LoadModel("Prop_AminoAcid");

        // 효소 캐릭터 무리
        var enzymes = new GameObject("EnzymeSwarm").AddComponent<FloatingSwarm>();
        enzymes.transform.position = new Vector3(0, 3f, 2);
        enzymes.prefabs = new[] { LoadModel("Char_Enzyme") }; enzymes.count = 10; enzymes.spawnExtents = new Vector3(5, 2, 5); enzymes.scaleRange = new Vector2(0.3f, 0.5f);

        // 유문 (출구) + 게이트 + 미즙 흐름
        var pylorus = Model("Part_Sphincter", Vector3.zero, 4f); AlignLongest(pylorus, Vector3.right); Center(pylorus, new Vector3(0, 1.8f, 9.5f));
        var gate = pylorus.AddComponent<SphincterGate>(); gate.openScale = 1.6f;
        gate.blocker = WorldBox(pylorus, new Vector3(4, 4, 0.4f));
        Label(pylorus, "Pylorus", meshCollider: false, labelHeight: 0.6f);
        var chymeSpline = Spline("ChymeFlowSpline", new[] { new Vector3(0, 0.8f, 4f), new Vector3(0.4f, 1.2f, 7f), new Vector3(0, 1.8f, 9.5f), new Vector3(0, 1.6f, 12f) }, false);
        var flow = chymeSpline.gameObject.AddComponent<SplineFlow>();
        flow.spline = chymeSpline; flow.prefabs = new[] { LoadModel("Prop_ChymeBlob") }; flow.count = 6; flow.loopSeconds = 8f; flow.scaleRange = new Vector2(0.25f, 0.4f);
        flow.startHidden = true;
        UnityEventTools.AddPersistentListener(churn.onDigested, gate.Open);
        UnityEventTools.AddPersistentListener(gate.onOpened, flow.Begin);

        // 진행: 입장 → 휘젓기 구역 → churn + 효소 절단 → 유문 열림 → 출구
        var zone = Zone("ChurnZone", new Vector3(0, 1.5f, -3), new Vector3(7, 3, 7), Strings(json["churn"]), "churn");
        UnityEventTools.AddPersistentListener(zone.onFinished, churn.Begin);
        UnityEventTools.AddPersistentListener(zone.onFinished, cut.Begin);
        Portal(new Vector3(0, 1.8f, 11.5f), new Vector3(4, 4, 1.5f));
    }

    // ───────────────────────────── 4. 소장 (라이드 + 확대 구간 + 흡수 게임) ─────────────────────────────

    static void BuildSmallIntestine(JToken json)
    {
        const int segs = 9;
        Tunnel("Env_SmallIntestineTunnel", null, segs, new Vector3(0, 1.4f, 0), Vector3.forward, 5.5f, out _, out float endZ);
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, 0.3f, 2.5f);
        Primitive(PrimitiveType.Cylinder, "SmallIntestineStartPlatform", new Vector3(0, 0.2f, 2.5f), new Vector3(3, 0.05f, 3), new Color(0.85f, 0.45f, 0.5f));

        // 십이지장 합류부 (시작 구간 벽) + 두 분비 흐름
        var duo = Model("Part_DuodenumJunction", Vector3.zero, 3.5f); Center(duo, new Vector3(-2.2f, 1.5f, 6f)); duo.transform.rotation = Quaternion.LookRotation(Vector3.right);
        Label(duo, "Duodenum");
        Emitter("BileFlow", new Vector3(-1.8f, 1.5f, 6f), new Color(0.75f, 0.85f, 0.2f, 0.8f), 0.1f, 15f, 1.2f, duo.transform).transform.rotation = Quaternion.LookRotation(Vector3.right);
        Emitter("PancreaticJuice", new Vector3(-1.8f, 1.2f, 6.4f), new Color(0.9f, 0.95f, 1f, 0.7f), 0.08f, 15f, 1.2f, duo.transform).transform.rotation = Quaternion.LookRotation(Vector3.right);

        // 융털 타일 (벽면 곳곳, 살랑거림)
        var villiRoot = new GameObject("Villi").transform;
        float zoomZForTiles = endZ * 0.55f;
        for (int i = 0; i < 8; i++)
        {
            float z = 4f + i * 5.5f; float ang = (i * 73f) * Mathf.Deg2Rad;
            if (Mathf.Abs(z - zoomZForTiles) < 4.5f) continue; // 확대 구간(융털 단면) 자리는 비워 둔다
            var pos = new Vector3(Mathf.Sin(ang) * 2.45f, 1.4f + Mathf.Cos(ang) * 2.35f, z);
            var tile = Model("Part_VillusCluster", Vector3.zero, 1.8f, null, villiRoot); Center(tile, pos);
            tile.transform.rotation = Quaternion.LookRotation(Vector3.forward, new Vector3(0, 1.4f, z) - pos);
            tile.AddComponent<Sway>().angle = 3f;
            if (i == 1) Label(tile, "Villus", labelHeight: 0.5f);
        }

        // 확대 구간: 융털 단면(3.2m) + 모세혈관(적혈구 흐름) + 유미관(지방산 흐름). 관(지름 5.5, 중심 y1.4) 오른쪽 벽 앞, 바닥 위에 세운다
        float zoomZ = endZ * 0.55f;
        const float zh = 3.2f; float zs = zh / 5f; // 5m 기준 설계를 3.2m 로 축소
        float tubeFloor = 1.4f - 2.75f + 0.15f;
        var zoom = new GameObject("VillusZoom").transform; zoom.position = new Vector3(1.55f, tubeFloor, zoomZ);
        var cross = Model("Part_VillusCrossSection", Vector3.zero, zh, null, zoom); Center(cross, zoom.position + Vector3.up * zh * 0.5f); Ground(cross, tubeFloor);
        cross.transform.rotation = Quaternion.Euler(0, -90, 0);
        Vector3 Z(float x, float y, float z) => zoom.position + new Vector3(x * zs, y * zs, z * zs);
        var capSpline = Spline("CapillarySpline", new[] { Z(-0.6f, 0.6f, -0.5f), Z(-0.3f, 2.2f, -0.6f), Z(-0.4f, 3.8f, 0f), Z(-0.3f, 2.2f, 0.6f), Z(-0.6f, 0.6f, 0.5f) }, true, zoom);
        var capTube = Model("Part_CapillaryTube", Vector3.zero, 3.6f * zs, null, zoom); AlignLongest(capTube, Vector3.up); Center(capTube, Z(-0.45f, 2.2f, 0f));
        foreach (var r in capTube.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LitMaterial("CapillaryWall", new Color(1f, 0.3f, 0.3f, 0.35f), transparent: true, doubleSided: true, smoothness: 0.9f);
        Label(capTube, "Capillary", labelHeight: 0.3f);
        var blood = capSpline.gameObject.AddComponent<SplineFlow>();
        blood.spline = capSpline; blood.prefabs = new[] { LoadModel("Char_RedBloodCell") }; blood.count = 14; blood.loopSeconds = 7f; blood.scaleRange = new Vector2(0.12f * zs, 0.16f * zs); blood.wobbleRadius = 0.05f * zs;
        blood.labelTemplate = LabelTemplate("RedBloodCell");
        var lacSpline = Spline("LactealSpline", new[] { Z(-0.45f, 0.4f, 0f), Z(-0.45f, 3.9f, 0f) }, false, zoom);
        var lacTube = Model("Part_LactealTube", Vector3.zero, 3.6f * zs, null, zoom); AlignLongest(lacTube, Vector3.up); Center(lacTube, Z(-0.45f, 2.2f, 0f)); lacTube.transform.localScale *= 0.6f;
        foreach (var r in lacTube.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LitMaterial("LactealWall", new Color(0.7f, 1f, 0.8f, 0.35f), transparent: true, doubleSided: true, smoothness: 0.9f);
        Label(lacTube, "Lacteal", labelHeight: 0.3f);
        var fat = lacSpline.gameObject.AddComponent<SplineFlow>();
        fat.spline = lacSpline; fat.prefabs = new[] { LoadModel("Prop_FattyAcid") }; fat.count = 6; fat.loopSeconds = 6f; fat.scaleRange = new Vector2(0.1f * zs, 0.14f * zs); fat.wobbleRadius = 0.03f * zs;

        // 영양소 흡수 미니게임 (라이드 중 응시)
        var swarm = new GameObject("NutrientSwarm").AddComponent<FloatingSwarm>();
        swarm.transform.position = new Vector3(0, 1.5f, endZ * 0.5f);
        swarm.spawnExtents = new Vector3(1.8f, 1.2f, endZ * 0.42f); swarm.driftRange = 0.35f; swarm.count = 0; swarm.scaleRange = new Vector2(0.25f, 0.4f);
        var absorb = swarm.gameObject.AddComponent<NutrientAbsorption>();
        absorb.swarm = swarm; absorb.gazeDwellSeconds = 1.0f; absorb.advanceOnComplete = false; absorb.absorbTarget = capTube.transform;
        var absorbed = json["absorbed"] as JObject;
        absorb.kinds = new[]
        {
            Kind("Glucose", "포도당", "Prop_Glucose", 6, absorbed),
            Kind("AminoAcid", "아미노산", "Prop_AminoAcid", 6, absorbed),
            Kind("FattyAcid", "지방산", "Prop_FattyAcid", 6, absorbed),
        };
        absorb.completeLines = Lines(json["complete"], stageId, "complete");

        // 라이드
        var stops = new[]
        {
            Stop(0.12f, Strings(json["game"]).FirstOrDefault() ?? "", "game_1"),
            Stop(0.55f, "이 벽 전체가 융털로 덮여 있어. 융털 하나하나 안에는 아주 가는 혈관이 지나가서 영양소를 실어 날라.", "ride_1"),
        };
        var knots = new[] { new Vector3(0, 0.6f, 3f), new Vector3(0.6f, 0.6f, endZ * 0.25f), new Vector3(-0.4f, 0.7f, endZ * 0.55f), new Vector3(0.4f, 0.6f, endZ * 0.8f), new Vector3(0, 0.6f, endZ - 2f) };
        var ride = Ride(knots, stops, autoBoard: true, delay: 8f, speed: 0.016f);

        var ui = new GameObject("ProgressUI", typeof(Canvas));
        ui.transform.SetParent(ride.transform, true);
        ui.transform.position = ride.transform.position + new Vector3(0, 1.4f, 1.3f);
        { var l = ride.transform.lossyScale; ui.transform.localScale = new Vector3(0.003f / l.x, 0.003f / l.y, 0.003f / l.z); }
        ui.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ui.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 80);
        var txt = new GameObject("Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        txt.transform.SetParent(ui.transform, false); txt.fontSize = 36; txt.alignment = TextAlignmentOptions.Center; txt.color = new Color(1f, 0.95f, 0.6f);
        Stretch(txt.rectTransform); AssignKoreanFont(txt);
        absorb.progressText = txt;
    }

    static NutrientAbsorption.NutrientKind Kind(string id, string name, string model, int count, JObject absorbed) => new NutrientAbsorption.NutrientKind
    {
        id = id, displayName = name, count = count, prefab = LoadModel(model),
        absorbedLine = absorbed?[id]?.ToString() ?? "",
        absorbedVoice = FindVoice($"{stageId}_absorbed_{id}"),
    };

    // ───────────────────────────── 5. 대장 ─────────────────────────────

    static void BuildLargeIntestine()
    {
        const int segs = 7;
        var tunnel = Tunnel("Env_ColonPipe", "Env_EsophagusTunnel", segs, new Vector3(0, 1.6f, 0), Vector3.forward, 6.5f, out _, out float endZ);
        // 대장 재질로 통일 (식도 관을 대체로 쓴 경우에도 색이 맞도록)
        foreach (var r in tunnel.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LitMaterial("ColonWall", new Color(0.8f, 0.5f, 0.42f), smoothness: 0.75f, doubleSided: true);
        // 벽 안쪽에 팽대 패널을 둘러 디테일 보강
        var lining = new GameObject("HaustraLining").transform;
        for (int i = 0; i < 10; i++)
        {
            float z = 5f + i * 5f; float ang = (i * 61f) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(ang) * 3.0f, 1.6f + Mathf.Cos(ang) * 2.9f, z); // 관 반경 3.25 에 밀착
            var panel = Model("Part_HaustraWallPanel", Vector3.zero, 2.8f, null, lining); Center(panel, pos);
            panel.transform.rotation = Quaternion.LookRotation(Vector3.forward, new Vector3(0, 1.6f, z) - pos);
            panel.AddComponent<PeristalsisPulse>().phaseOffset = i * 0.1f;
            if (i == 2) Label(panel, "Haustra", labelHeight: 0.4f);
        }
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, 0.3f, 2.5f);
        Primitive(PrimitiveType.Cylinder, "LargeIntestineStartPlatform", new Vector3(0, 0.2f, 2.5f), new Vector3(3, 0.05f, 3), new Color(0.8f, 0.5f, 0.42f));

        // 충수 (시작 부분 옆) / 팽대 패널 라벨
        var appendix = Model("Part_Appendix", Vector3.zero, 2.0f); Center(appendix, new Vector3(-2.3f, 0.6f, 7.5f)); // 캡슐 출발 위치(z3)와 겹치지 않게 appendix.transform.rotation = Quaternion.LookRotation(Vector3.right); Label(appendix, "Appendix");

        var amb = new GameObject("Ambience").AddComponent<SceneAmbience>(); amb.clipName = "gurgle"; amb.volume = 0.2f; amb.pitch = 0.7f;

        // 세균 무리 + 식이섬유
        var good = new GameObject("ProbioticSwarm").AddComponent<FloatingSwarm>();
        good.transform.position = new Vector3(0, 1.8f, endZ * 0.5f);
        good.prefabs = new[] { LoadModel("Char_Probiotic") }; good.count = 30; good.spawnExtents = new Vector3(2.5f, 1.8f, endZ * 0.45f); good.scaleRange = new Vector2(0.2f, 0.4f); good.flowVelocity = new Vector3(0, 0, 0.3f);
        good.labelTemplate = LabelTemplate("Probiotic");
        var bad = new GameObject("HarmfulSwarm").AddComponent<FloatingSwarm>();
        bad.transform.position = new Vector3(0, 1.8f, endZ * 0.5f);
        bad.prefabs = new[] { LoadModel("Char_HarmfulBacteria"), LoadModel("Char_Virus") }; bad.count = 10; bad.spawnExtents = new Vector3(2.5f, 1.8f, endZ * 0.45f); bad.scaleRange = new Vector2(0.2f, 0.35f); bad.flowVelocity = new Vector3(0, 0, 0.3f);
        bad.labelTemplate = LabelTemplate("HarmfulBacteria");
        var fiber = new GameObject("FiberSwarm").AddComponent<FloatingSwarm>();
        fiber.transform.position = new Vector3(0, 1.5f, endZ * 0.5f);
        fiber.prefabs = new[] { LoadModel("Part_FiberStrand") }; fiber.count = 12; fiber.spawnExtents = new Vector3(2.5f, 1.5f, endZ * 0.45f); fiber.scaleRange = new Vector2(0.25f, 0.45f); fiber.flowVelocity = new Vector3(0, 0, 0.3f);

        // 수분 흡수 (물방울이 벽으로)
        var water = new GameObject("WaterAbsorption").AddComponent<WallAbsorption>();
        water.transform.position = new Vector3(0, 1.8f, endZ * 0.5f);
        water.prefabs = new[] { LoadModel("Prop_WaterDroplet") }; water.count = 35; water.tubeRadius = 2.6f; water.tubeLength = endZ * 0.9f;
        water.labelTemplate = LabelTemplate("Water");

        // 라이드 + 찌꺼기 3단계 (캡슐 앞에 붙어서 같이 이동)
        var stops = new[]
        {
            Stop(0.30f, "초록색 친구들이 유익균이야. 우리가 먹은 채소의 식이섬유를 먹고 비타민을 만들어 줘.", "ride_1"),
            Stop(0.65f, "보라색은 유해균. 평소엔 조용하지만, 인스턴트만 먹거나 잠을 못 자면 숫자가 확 늘어나서 배가 아파.", "ride_2"),
        };
        var knots = new[] { new Vector3(0, 0.8f, 3f), new Vector3(1, 0.8f, endZ * 0.25f), new Vector3(-1, 0.9f, endZ * 0.5f), new Vector3(0.5f, 0.8f, endZ * 0.78f), new Vector3(0, 0.8f, endZ - 2f) };
        var ride = Ride(knots, stops, autoBoard: true, delay: 8f, speed: 0.02f);
        var wasteRoot = new GameObject("WasteStages").transform; wasteRoot.SetParent(ride.transform, true); wasteRoot.position = ride.transform.position + new Vector3(-1.2f, 0.4f, 2.5f);
        { var l = ride.transform.lossyScale; wasteRoot.localScale = new Vector3(1f / l.x, 1f / l.y, 1f / l.z); }
        var w1 = Model("Prop_WasteLiquid", Vector3.zero, 1.0f, null, wasteRoot); w1.transform.localPosition = Vector3.zero;
        var w2 = Model("Prop_WasteSoft", Vector3.zero, 1.0f, null, wasteRoot); w2.transform.localPosition = Vector3.zero;
        var w3 = Model("Prop_WasteSolid", Vector3.zero, 1.1f, null, wasteRoot); w3.transform.localPosition = Vector3.zero;
        Label(w1, "Waste"); Label(w2, "Waste"); Label(w3, "Waste");
        var sw = wasteRoot.gameObject.AddComponent<RideProgressSwitcher>();
        sw.ride = ride; sw.stages = new[] { w1, w2, w3 }; sw.thresholds = new[] { 0f, 0.4f, 0.75f };
    }

    // ───────────────────────────── 6. 상영관 ─────────────────────────────

    static void BuildTheater()
    {
        var dome = Model("Env_TheaterDome", Vector3.zero, 28f); Center(dome, new Vector3(0, 7f, 0)); AddMeshColliders(dome);
        foreach (var r in dome.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = LitMaterial("TheaterDome", new Color(0.08f, 0.09f, 0.14f), doubleSided: true, smoothness: 0.3f); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        InteriorLight(new Vector3(0, 5f, -2f), 30f, 2f);
        Primitive(PrimitiveType.Cylinder, "TheaterFloor", new Vector3(0, -0.05f, 0), new Vector3(12, 0.05f, 12), new Color(0.1f, 0.1f, 0.14f));
        GameObject.Find("SpawnPoint").transform.position = new Vector3(0, 0, -4);

        // 홀로그램: 소화계 + 경로를 따라 흐르는 빛
        var holo = Ground(Model("Exhibit_DigestiveSystem", new Vector3(0, 0, 2f), 3.2f), 0.6f);
        holo.name = "Hologram";
        foreach (var r in holo.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LitMaterial("Hologram", new Color(0.4f, 0.8f, 1f, 0.45f), emissive: true, transparent: true);
        Label(holo, "Hologram", labelHeight: 0.5f);
        var hb = WorldBounds(holo);
        var path = Spline("HologramPath", new[]
        {
            new Vector3(hb.center.x, hb.max.y - 0.3f, hb.center.z), new Vector3(hb.center.x, hb.max.y - 1.0f, hb.center.z),
            new Vector3(hb.center.x - 0.4f, hb.center.y + 0.3f, hb.center.z), new Vector3(hb.center.x + 0.3f, hb.center.y - 0.3f, hb.center.z),
            new Vector3(hb.center.x - 0.3f, hb.center.y - 0.7f, hb.center.z), new Vector3(hb.center.x + 0.4f, hb.min.y + 0.5f, hb.center.z), new Vector3(hb.center.x, hb.min.y + 0.1f, hb.center.z),
        }, false);
        var light = path.gameObject.AddComponent<SplineFlow>();
        light.spline = path; light.prefabs = new[] { GlowSpherePrefab("GlowDot", new Color(1f, 0.9f, 0.4f), 0.12f) }; light.count = 10; light.loopSeconds = 6f; light.wobbleRadius = 0.02f; light.alignToPath = false;

        // 스크린 + VideoPlayer
        var screen = Primitive(PrimitiveType.Quad, "TheaterScreen", new Vector3(0, 4.5f, 9), new Vector3(16, 9, 1), Color.black);
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>($"{GeneratedDir}/TheaterScreen.renderTexture");
        if (rt == null) { rt = new RenderTexture(1920, 1080, 0); AssetDatabase.CreateAsset(rt, $"{GeneratedDir}/TheaterScreen.renderTexture"); }
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedDir}/Materials/TheaterScreen.mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, $"{GeneratedDir}/Materials/TheaterScreen.mat"); }
        mat.mainTexture = rt;
        screen.GetComponent<Renderer>().sharedMaterial = mat;
        var vp = screen.AddComponent<VideoPlayer>();
        vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt; vp.playOnAwake = false; vp.isLooping = false;
        var folders = new[] { "Assets/Videos", "Assets/Sounds" }.Where(AssetDatabase.IsValidFolder).ToArray();
        var clipGuid = folders.Length > 0 ? AssetDatabase.FindAssets("t:VideoClip", folders).FirstOrDefault() : null;
        if (clipGuid != null) vp.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(clipGuid));

        // 입장 내레이션이 끝나면 영상 재생 → 영상 끝나면 마무리 멘트 → 로비로 (TheaterEnding 이 순서를 관리)
        var ending = screen.AddComponent<TheaterEnding>();
        ending.player = vp; ending.playAfterIntro = true;

        Ground(Model("Env_Toilet", new Vector3(6, 0, -6), 1.3f, new Vector3(0, -30, 0)));
    }
}
#endif
