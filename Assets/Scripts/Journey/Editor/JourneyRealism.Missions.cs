#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 치료 임무 이야기: 병원에서 의사 선생님(병아리 의사)에게 진찰을 받고, 초소형 치료 잠수정을 타고 몸속에 들어가
/// 기관마다 직접 치료한다 — 입(충치·치태 세균), 식도(역류 위산 방울), 위(헬리코박터균), 소장(영양소 흡수), 대장(해로운 균).
/// 마지막 장면에서 치료 보고서를 본다. 대사는 journey_content.json 의 stages[].missions 에서 읽는다(음성도 같은 이름).
/// </summary>
public static partial class JourneyRealism
{
    // ───────────────────────────── 대사 ─────────────────────────────

    static JObject content;
    static string MissionText(string stage, string key)
    {
        if (content == null) content = JObject.Parse(File.ReadAllText("Assets/Data/journey_content.json"));
        var s = content["stages"].FirstOrDefault(x => (string)x["stageId"] == stage);
        return (string)s?["missions"]?[key] ?? "";
    }

    /// <summary>임무 대사 한 줄 + 같은 이름의 음성(Assets/Sounds/Narration/{stage}_mission_{key}.mp3).</summary>
    static NarrationLine[] Mission(string stage, string key, float seconds = 5f)
    {
        var voice = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/Narration/{stage}_mission_{key}.mp3");
        return new[] { new NarrationLine { text = MissionText(stage, key), voice = voice, holdSeconds = seconds } };
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static void ClearListeners(UnityEventBase e, Object target)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--) if (e.GetPersistentTarget(i) == target) UnityEventTools.RemovePersistentListener(e, i);
    }

    static GermBlaster Hunt(string name)
    {
        var root = GameObject.Find(name) ?? new GameObject(name);
        var hunt = root.GetComponent<GermBlaster>(); if (hunt == null) hunt = root.AddComponent<GermBlaster>();
        hunt.beam = BeamLine(root.transform, "치료_광선", new Color(.4f, .95f, 1f), .016f);
        hunt.hitBurst = Burst(root.transform, "맞힘", Droplet("맞힘_반짝", new Color(.75f, 1f, .9f, .7f), rim: 1.4f, highlight: 1.6f), new Vector2(.04f, .1f), new Vector2(.5f, 1.4f), new Vector2(.35f, .8f));
        return hunt;
    }

    // ───────────────────────────── 손 치료 도구 ─────────────────────────────

    static GameObject ToolPrefab()
    {
        string path = $"{Folder}/치료_도구.prefab";
        var root = new GameObject("치료_도구");
        var metal = CockpitMaterial("도구_몸체", new Color(.75f, .82f, .86f), .7f, 0);
        var grip = CockpitMaterial("도구_손잡이", new Color(.12f, .15f, .17f), .3f, 0);
        var glow = CockpitMaterial("도구_빛", new Color(.1f, .3f, .35f), .2f, 1.4f);
        GameObject Piece(PrimitiveType type, string n, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false); g.transform.localPosition = pos; g.transform.localEulerAngles = euler; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; return g;
        }
        // 컨트롤러를 쥔 손 앞으로 뻗은 작은 광선기: 손잡이 · 몸통 · 빛나는 끝
        Piece(PrimitiveType.Cylinder, "손잡이", new Vector3(0, -.035f, -.01f), new Vector3(20, 0, 0), new Vector3(.03f, .05f, .03f), grip);
        Piece(PrimitiveType.Cylinder, "몸통", new Vector3(0, 0, .07f), new Vector3(90, 0, 0), new Vector3(.034f, .075f, .034f), metal);
        Piece(PrimitiveType.Cylinder, "띠", new Vector3(0, 0, .1f), new Vector3(90, 0, 0), new Vector3(.038f, .006f, .038f), glow);
        Piece(PrimitiveType.Sphere, "끝_빛", new Vector3(0, 0, .15f), Vector3.zero, new Vector3(.026f, .026f, .026f), glow);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void HandToolIn()
    {
        var go = GameObject.Find("손_치료_도구") ?? new GameObject("손_치료_도구");
        var tool = go.GetComponent<HandTool>(); if (tool == null) tool = go.AddComponent<HandTool>();
        tool.toolPrefab = ToolPrefab();
    }

    // ───────────────────────────── 표적 모형 ─────────────────────────────

    static GameObject SavePrefab(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>헬리코박터균: 휘어진 나선 막대 몸통과 한쪽 끝의 긴 편모 여러 가닥. (원점 = 몸 가운데, 크기 약 1)</summary>
    static GameObject HPyloriPrefab()
    {
        var root = new GameObject("헬리코박터균");
        var body = new GameObject("몸통"); body.transform.SetParent(root.transform, false);
        var mesh = Tube(t => new Vector3(Mathf.Cos(t * Mathf.PI * 5) * .14f, Mathf.Sin(t * Mathf.PI * 5) * .14f, (t - .5f) * 1f),
                        t => .1f * Mathf.Sqrt(Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 1.6f)), 90, 12);
        SaveMesh($"{Folder}/헬리코박터균_몸통.asset", ref mesh);
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var bodyMat = Lit("헬리코박터균", new Color(.56f, .58f, .46f), .75f); // 실제 세균처럼 옅은 올리브빛 회색, 젖은 윤기
        body.AddComponent<MeshRenderer>().sharedMaterial = bodyMat;
        var flagMat = Lit("헬리코박터균_편모", new Color(.72f, .73f, .64f), .5f);
        for (int k = 0; k < 5; k++)
        {
            float a = k * Mathf.PI * 2 / 5, phase = k * 1.3f;
            var f = Tube(t => new Vector3(Mathf.Cos(a) * (.05f + .25f * t) + Mathf.Sin(t * 9 + phase) * .05f * t,
                                          Mathf.Sin(a) * (.05f + .25f * t) + Mathf.Cos(t * 9 + phase) * .05f * t, .5f + t * .9f),
                         t => .018f * (1 - t * .6f), 40, 5);
            SaveMesh($"{Folder}/헬리코박터균_편모_{k}.asset", ref f);
            var g = new GameObject("편모_" + k); g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = f; g.AddComponent<MeshRenderer>().sharedMaterial = flagMat;
        }
        return SavePrefab(root, "헬리코박터균");
    }

    /// <summary>경로 path(t) 를 따라가는 관. radius(t) 로 굵기가 바뀐다.</summary>
    static Mesh Tube(System.Func<float, Vector3> path, System.Func<float, float> radius, int rows, int around)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int j = 0; j <= rows; j++)
        {
            float t = (float)j / rows;
            var p = path(t); var tangent = (path(Mathf.Min(1, t + .01f)) - path(Mathf.Max(0, t - .01f))).normalized;
            var side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > .9f ? Vector3.right : Vector3.up).normalized; var up = Vector3.Cross(side, tangent);
            for (int i = 0; i <= around; i++)
            {
                float a = (float)i / around * Mathf.PI * 2;
                v.Add(p + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius(t));
                if (j < rows && i < around) { int k = j * (around + 1) + i, n = k + around + 1; tri.AddRange(new[] { k, n, k + 1, k + 1, n, n + 1 }); }
            }
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    /// <summary>역류한 위산 방울: 맑은 연두빛 액체 방울.</summary>
    static GameObject AcidPrefab()
    {
        var root = new GameObject("역류_위산_방울");
        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere); drop.name = "방울"; Object.DestroyImmediate(drop.GetComponent<Collider>());
        drop.transform.SetParent(root.transform, false); drop.transform.localScale = new Vector3(.8f, 1f, .8f);
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/역류_위산.mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, $"{Folder}/역류_위산.mat"); }
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.EnableKeyword("_EMISSION"); mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(.82f, .9f, .35f, .55f)); mat.SetFloat("_Smoothness", .95f);
        mat.SetColor("_EmissionColor", new Color(.12f, .15f, .02f));
        EditorUtility.SetDirty(mat);
        drop.GetComponent<Renderer>().sharedMaterial = mat;
        return SavePrefab(root, "역류_위산_방울");
    }

    /// <summary>몸 스캔에서 아픈 곳을 알리는 빨간 점(맥박처럼 커졌다 작아진다).</summary>
    static GameObject HotspotPrefab()
    {
        var root = new GameObject("진찰_점");
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.name = "빛"; Object.DestroyImmediate(dot.GetComponent<Collider>());
        dot.transform.SetParent(root.transform, false); dot.transform.localScale = Vector3.one * .12f;
        dot.GetComponent<Renderer>().sharedMaterial = UnlitColor("진찰_점", new Color(1f, .3f, .25f));
        var pulse = dot.AddComponent<PeristalsisPulse>(); pulse.amplitude = .25f; pulse.period = 1.1f; pulse.axisWeight = Vector3.one;
        return SavePrefab(root, "진찰_점");
    }

    // ───────────────────────────── 입: 충치 → 치태 세균 → 씹기 ─────────────────────────────

    static void MouthMissions()
    {
        var treat = Object.FindFirstObjectByType<CavityTreatment>();
        if (treat == null) return;
        treat.startLines = Mission("Mouth", "cavity_start", 6f);
        treat.doneLines = Mission("Mouth", "cavity_done", 4f);
        treat.nextGoalText = "이에 붙은 치태 세균을 겨냥하고 방아쇠를 당겨 없애세요";

        // 치태 세균 자리: 탐험가 눈에서 보이는 아래·위 이 표면 여섯 곳 (서로 1m 이상 떨어지게)
        var spawn = GameObject.Find("SpawnPoint");
        var eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;
        var probes = new List<MeshCollider>();
        foreach (var mf in ToothSurfaces(false)) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; probes.Add(c); }
        Physics.SyncTransforms();
        var old = GameObject.Find("치태_세균_자리들"); if (old != null) Object.DestroyImmediate(old);
        var spots = new List<Transform>();
        for (float yaw = -80; yaw <= 80 && spots.Count < 6; yaw += 8)
        for (float pitch = 10; pitch <= 40 && spots.Count < 6; pitch += 10)
        {
            var dir = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
            RaycastHit best = default; MeshCollider bestCol = null; float bestDist = float.MaxValue;
            foreach (var c in probes) if (c.Raycast(new Ray(eye, dir), out var h, 7f) && h.distance > 1.6f && h.distance < bestDist) { best = h; bestCol = c; bestDist = h.distance; }
            if (bestCol == null || spots.Any(s => Vector3.Distance(s.position, best.point) < 1.1f)) continue;
            var spot = new GameObject("치태_세균_자리").transform;
            spot.SetPositionAndRotation(best.point + best.normal * .1f, Quaternion.LookRotation(best.normal));
            spot.SetParent(bestCol.transform, true); // 아래턱과 함께 움직인다
            spots.Add(spot);
        }
        foreach (var c in probes) Object.DestroyImmediate(c);

        var hunt = Hunt("치태_세균_없애기");
        hunt.spots = spots.ToArray();
        hunt.harmfulPrefabs = new[] { Prefab("Char_HarmfulBacteria") }.Where(p => p != null).ToArray();
        hunt.helpfulPrefabs = new GameObject[0];
        hunt.range = 8f; hunt.aimRadius = .3f; hunt.releaseRide = false;
        hunt.targetLabel = "치태 세균"; hunt.scoreKey = "plaque"; hunt.scoreLabel = "치태 세균";
        hunt.goalText = "이에 붙은 치태 세균을 겨냥하고 방아쇠를 당겨 없애세요";
        hunt.clearedStatus = "치태 세균을 모두 없앴어요!";
        hunt.startLines = new NarrationLine[0];
        hunt.clearedLines = Mission("Mouth", "plaque_done", 5f);

        // 입 진행: 씹기(음식이 어금니 위로, 턱이 씹고 침샘에서 침) → 양치를 안 하면?(시간 흐름: 찌꺼기에 세균이 불어나 충치가 생김)
        //          → 충치 치료 → 치태 세균 없애기(양치) → 꿀꺽 삼키기 → 식도
        treat.hiddenUntilBegin = true;
        var lapse = Ensure<DecayTimeLapse>(GameObject.Find("충치가_생기는_과정") ?? new GameObject("충치가_생기는_과정"));
        lapse.cavity = treat; lapse.spots = spots.ToArray();
        lapse.germPrefab = hunt.harmfulPrefabs.FirstOrDefault();
        lapse.residuePrefab = Prefab("Prop_FoodBolus");
        MouthGermSizes(lapse, hunt);
        lapse.acid = Burst(lapse.transform, "충치_산_거품", Droplet("세균_산", new Color(1f, .95f, .62f, .45f), rim: 1.5f, highlight: 1.6f), new Vector2(.03f, .07f), new Vector2(.05f, .2f), new Vector2(.8f, 1.4f));
        lapse.startLines = Mission("Mouth", "decay_start", 6f);
        lapse.cavityLines = Mission("Mouth", "decay_cavity", 6f);
        EditorUtility.SetDirty(lapse);

        var chewing = Object.FindFirstObjectByType<ChewingSequence>();
        var swallow = Object.FindFirstObjectByType<SwallowSequence>();
        if (chewing != null)
        {
            if (swallow != null) ClearListeners(chewing.onChewingFinished, swallow); // 예전: 씹고 바로 삼킴
            ClearListeners(chewing.onChewingFinished, lapse);
            UnityEventTools.AddPersistentListener(chewing.onChewingFinished, lapse.Begin);
            EditorUtility.SetDirty(chewing);
        }
        ClearListeners(lapse.onFinished, treat);
        UnityEventTools.AddPersistentListener(lapse.onFinished, treat.Begin);
        ClearListeners(treat.onTreated, hunt); ClearListeners(treat.onTreated, lapse);
        UnityEventTools.AddPersistentListener(treat.onTreated, lapse.ClearGerms); // 시간 흐름에서 생긴 세균을 임무 세균으로 바꾼다
        UnityEventTools.AddPersistentListener(treat.onTreated, hunt.Begin);
        foreach (var gate in Object.FindObjectsByType<MissionGate>(FindObjectsSortMode.None)) ClearListeners(hunt.onCleared, gate);
        if (swallow != null)
        {
            ClearListeners(hunt.onCleared, swallow);
            UnityEventTools.AddPersistentListener(hunt.onCleared, swallow.Begin);
        }

        // 씹기가 처음 할 일이라 씹기 구역은 처음부터 열려 있다 (예전엔 치료를 마쳐야 열렸다)
        var chew = GameObject.Find("ChewZone");
        var oldGate = chew != null ? chew.GetComponent<MissionGate>() : null;
        if (oldGate != null) { foreach (var c in chew.GetComponents<Collider>()) c.enabled = true; Object.DestroyImmediate(oldGate); }
        EditorUtility.SetDirty(treat); EditorUtility.SetDirty(hunt);
    }

    /// <summary>
    /// 입 세균·찌꺼기 크기. 이 하나가 1m 넘는 크기라 0.26m 세균은 탐험가 자리에서 점처럼 보여 '불어나는' 게 안 읽힌다.
    /// 시간 흐름의 세균과 치태 세균 임무의 세균은 같은 크기(이어서 바뀌어도 티 나지 않게).
    /// </summary>
    static void MouthGermSizes(DecayTimeLapse lapse, GermBlaster hunt)
    {
        const float germ = .36f, residue = .32f;
        var lite = Prefab("Char_HarmfulBacteria"); // 가벼운 판(삼각형 1만 2천 → 5천)
        if (lite != null) { lapse.germPrefab = lite; hunt.harmfulPrefabs = new[] { lite }; }
        lapse.germScale = germ; lapse.germsPerSpot = 4;
        if (lapse.residuePrefab != null)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(lapse.residuePrefab); var size = Bounds(probe).size; Object.DestroyImmediate(probe);
            lapse.residueScale = residue / Mathf.Max(.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        hunt.scaleRange = new Vector2(germ - .04f, germ + .04f);
        EditorUtility.SetDirty(lapse); EditorUtility.SetDirty(hunt);
    }

    /// <summary>입 장면에서 세균·찌꺼기 크기만 바꾼다 (다른 것은 그대로 — 편집기에서 손본 입 장면 보호). 배치용.</summary>
    public static void TuneMouthGerms()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{SceneDir}/01_Mouth.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        var lapse = Object.FindFirstObjectByType<DecayTimeLapse>();
        var hunt = GameObject.Find("치태_세균_없애기")?.GetComponent<GermBlaster>();
        if (lapse == null || hunt == null) { Debug.LogError("[입] 충치 과정·치태 세균 임무를 찾지 못함"); return; }
        MouthGermSizes(lapse, hunt);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[입] 세균 {lapse.germScale}m ×{lapse.germsPerSpot}, 치태 세균 {hunt.scaleRange}");
    }

    // ───────────────────────────── 식도: 역류 위산 방울 중화 ─────────────────────────────

    static void EsophagusMission()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) return;
        var hunt = Hunt("역류_위산_중화");
        hunt.ride = ride; hunt.basis = ride.transform; hunt.followBasis = true; hunt.releaseRide = false;
        hunt.harmfulPrefabs = new[] { AcidPrefab() }; hunt.helpfulPrefabs = new GameObject[0];
        hunt.harmfulCount = 8; hunt.spots = new Transform[0];
        // 잠수정 아래쪽(내려가는 방향)에서 따라 올라오는 듯 둘레에 떠 있다. 유리 바닥으로 내려다보며 쏜다.
        hunt.spawnCenter = new Vector3(0, -3.4f, 0); hunt.spawnExtents = new Vector3(1.7f, 1.3f, 1.7f);
        hunt.scaleRange = new Vector2(.28f, .42f); hunt.bob = .25f; hunt.range = 9f;
        hunt.targetLabel = "위산 방울"; hunt.scoreKey = "acid"; hunt.scoreLabel = "역류 위산 중화";
        hunt.goalText = "아래에서 올라오는 위산 방울을 겨냥해 중화제를 쏘세요";
        hunt.clearedStatus = "위산 방울을 모두 중화했어요!";
        hunt.startLines = new NarrationLine[0];
        hunt.clearedLines = Mission("Esophagus", "acid_done", 4f);

        // 출발 직후 정차해서 안내하고 임무를 시작한다 (내려가는 동안 계속 쏠 수 있다)
        var stops = (ride.stops ?? new CapsuleRide.RideStop[0]).Where(s => s.normalizedPosition > .13f || s.normalizedPosition < .11f).ToList();
        var start = new CapsuleRide.RideStop { normalizedPosition = .12f, lines = Mission("Esophagus", "acid_start", 5f), waitSeconds = 1f, onReached = new UnityEvent() };
        UnityEventTools.AddPersistentListener(start.onReached, hunt.Begin);
        stops.Add(start);
        ride.stops = stops.OrderBy(s => s.normalizedPosition).ToArray();
        EditorUtility.SetDirty(ride); EditorUtility.SetDirty(hunt);
    }

    // ───────────────────────────── 위: 헬리코박터균 퇴치 ─────────────────────────────

    static void StomachMission()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) return;
        var hold = ride.stops?.FirstOrDefault(s => s.holdUntilReleased);
        if (hold == null) return;
        var hunt = Hunt("헬리코박터균_퇴치");
        hunt.ride = ride; hunt.basis = ride.transform; hunt.followBasis = false; hunt.releaseRide = true;
        hunt.harmfulPrefabs = new[] { HPyloriPrefab() }; hunt.helpfulPrefabs = new GameObject[0];
        hunt.harmfulCount = 7; hunt.spots = new Transform[0];
        hunt.spawnCenter = new Vector3(0, -.4f, 4.2f); hunt.spawnExtents = new Vector3(4.2f, .9f, 3f);
        hunt.scaleRange = new Vector2(.45f, .6f); hunt.range = 14f;
        hunt.targetLabel = "헬리코박터균"; hunt.scoreKey = "hpylori"; hunt.scoreLabel = "헬리코박터균 퇴치";
        hunt.goalText = "위액 위를 떠다니는 나선 모양 헬리코박터균을 물리치세요";
        hunt.clearedStatus = "헬리코박터균을 모두 물리쳤어요!";
        hunt.startLines = Mission("Stomach", "hp_start", 5f);
        hunt.clearedLines = Mission("Stomach", "hp_done", 4f);
        // 유문 열림 + 균 퇴치 두 가지가 끝나야 잠수정이 출발한다
        hold.releasesNeeded = 2; hold.maxHoldSeconds = 180f;
        ClearListeners(hold.onReached, hunt);
        UnityEventTools.AddPersistentListener(hold.onReached, hunt.Begin);
        EditorUtility.SetDirty(ride); EditorUtility.SetDirty(hunt);
    }

    // ───────────────────────────── 병원 진료실 ─────────────────────────────

    const string SciFi = "Assets/Sci-Fi Styled Modular Pack/Prefabs";

    static GameObject PackProp(string name, Vector3 position, float yaw, float size, bool byHeight, Transform parent)
    {
        var path = AssetDatabase.FindAssets(name + " t:Prefab", new[] { SciFi }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
        if (path == null) { Debug.LogWarning("[병원] 소품 없음: " + name); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var b = Bounds(go);
        float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (current > 1e-3f) go.transform.localScale *= size / current;
        b = Bounds(go);
        go.transform.position += new Vector3(position.x - b.center.x, -b.min.y, position.z - b.center.z);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.position = center; go.transform.localScale = size;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>병원 진료실(벽·바닥·천장·조명·가구). 처음 병원과 마지막 진료실이 함께 쓴다.</summary>
    static Transform ClinicRoom()
    {
        var old = GameObject.Find("병원_진료실"); if (old != null) Object.DestroyImmediate(old);
        var room = new GameObject("병원_진료실").transform;
        const float halfX = 5f, front = -5.5f, back = 6.5f, height = 3.2f;
        // 하얀 방은 VR 에서 눈이 부시고 물건이 묻힌다: 회청색 바닥, 아래는 청록 회색 판·위는 베이지 회색 벽
        var floorMat = Lit("진료실_바닥", new Color(.42f, .47f, .52f), .5f);
        var wallMat = Lit("진료실_벽", new Color(.7f, .7f, .66f), .2f);
        var lowerMat = Lit("진료실_벽_아래", new Color(.3f, .45f, .48f), .35f);
        var ceilMat = Lit("진료실_천장", new Color(.66f, .68f, .7f), .1f);
        var stripe = CockpitMaterial("진료실_빛띠", new Color(.1f, .3f, .35f), .2f, .9f);
        Box(room, "바닥", new Vector3(0, -.05f, (front + back) / 2), new Vector3(halfX * 2, .1f, back - front), floorMat);
        Box(room, "천장", new Vector3(0, height + .05f, (front + back) / 2), new Vector3(halfX * 2, .1f, back - front), ceilMat);
        Box(room, "왼쪽_벽", new Vector3(-halfX - .05f, height / 2, (front + back) / 2), new Vector3(.1f, height, back - front), wallMat);
        Box(room, "오른쪽_벽", new Vector3(halfX + .05f, height / 2, (front + back) / 2), new Vector3(.1f, height, back - front), wallMat);
        Box(room, "뒤_벽", new Vector3(0, height / 2, back + .05f), new Vector3(halfX * 2, height, .1f), wallMat);
        Box(room, "앞_벽", new Vector3(0, height / 2, front - .05f), new Vector3(halfX * 2, height, .1f), wallMat);
        Box(room, "아래판_왼쪽", new Vector3(-halfX + .02f, .5f, (front + back) / 2), new Vector3(.04f, 1f, back - front), lowerMat);
        Box(room, "아래판_오른쪽", new Vector3(halfX - .02f, .5f, (front + back) / 2), new Vector3(.04f, 1f, back - front), lowerMat);
        Box(room, "아래판_뒤", new Vector3(0, .5f, back - .02f), new Vector3(halfX * 2, 1f, .04f), lowerMat);
        Box(room, "아래판_앞", new Vector3(0, .5f, front + .02f), new Vector3(halfX * 2, 1f, .04f), lowerMat);
        // 병원다운 허리 높이 하늘색 빛 띠
        Box(room, "빛띠_왼쪽", new Vector3(-halfX + .01f, 1f, (front + back) / 2), new Vector3(.02f, .05f, back - front), stripe);
        Box(room, "빛띠_오른쪽", new Vector3(halfX - .01f, 1f, (front + back) / 2), new Vector3(.02f, .05f, back - front), stripe);
        Box(room, "빛띠_뒤", new Vector3(0, 1f, back - .01f), new Vector3(halfX * 2, .05f, .02f), stripe);
        foreach (var c in room.GetComponentsInChildren<Collider>()) c.enabled = c.name == "바닥" || c.name.EndsWith("벽");

        // 실내 조명 (천장 LED 판은 ClinicDecor 에서 같은 자리에 단다 — 예전 막대등은 공장 같았다)
        var lampSpots = new[] { new Vector3(-2.5f, 0, -1.5f), new Vector3(2.5f, 0, -1.5f), new Vector3(-2.5f, 0, 3f), new Vector3(2.5f, 0, 3f) };
        foreach (var p in lampSpots)
        {
            var light = new GameObject("천장_조명").AddComponent<Light>(); light.transform.SetParent(room, false);
            light.type = LightType.Point; light.range = 7; light.intensity = .45f; light.color = new Color(1f, .97f, .92f); light.shadows = LightShadows.None;
            light.transform.position = new Vector3(p.x, height - .3f, p.z);
        }

        // 진료실 가구 (Sci-Fi 모듈 팩)
        PackProp("bed", new Vector3(-3.7f, 0, 1.2f), 90, 2.1f, false, room);
        PackProp("console", new Vector3(-2.5f, 0, 2.8f), 90, 1.1f, true, room);
        PackProp("cabinet", new Vector3(-4.6f, 0, -4.5f), 90, 2f, true, room); // 접수대 간호사 뒤를 가리지 않게 입구 쪽 구석
        PackProp("desk", new Vector3(3.7f, 0, 1.4f), -90, 1.7f, false, room);
        PackProp("decorative_chair", new Vector3(2.7f, 0, 1.4f), 90, .95f, true, room);
        PackProp("computer_station", new Vector3(4.5f, 0, -2.4f), -90, 1.8f, true, room);
        PackProp("decorative_plant", new Vector3(-4.4f, 0, 5.9f), 0, 1.3f, true, room);
        PackProp("decorative_plant", new Vector3(4.4f, 0, 5.9f), 0, 1.3f, true, room);
        PackProp("door_1", new Vector3(0, 0, front + .1f), 0, 2.4f, true, room);
        var screen = PackProp("big_screen", new Vector3(0, 0, back - .15f), 180, 2.8f, false, room);
        if (screen != null)
        {
            screen.transform.position += Vector3.up * .9f;
            // 하얗게 빛나는 화면은 앞의 하늘색 몸 스캔 홀로그램을 묻히고 눈부시다: 꺼진 모니터처럼 어두운 남색
            var dark = Lit("진료실_화면", new Color(.04f, .08f, .11f), .8f);
            foreach (var r in screen.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.ToLower().Contains("screen") ? dark : m).ToArray();
        }

        ClinicDecor(room, halfX, front, back, height, lampSpots);
        return room;
    }

    static void Hospital()
    {
        // 예전 전시관 물건은 숨긴다 (진행 연결은 그대로)
        foreach (var name in new[] { "Exhibits", "CeilingRing", "LobbyFloor", "Lobby_InfoKiosk", "Lobby_MouthArch", "Lobby_EntranceGate" })
        { var g = GameObject.Find(name); if (g != null) g.SetActive(false); }
        foreach (var station in Object.FindObjectsByType<ExhibitStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) station.enabled = false;

        // 몸 스캔 홀로그램: 가운데 소화 기관 홀로그램 (돌지 않게 해 진찰 점을 겨냥하기 쉽게).
        // 진료실을 다시 지을 때 함께 지워지지 않도록 방 밖(장면 맨 위)에 둔다. 예전 적용에서 지워졌으면 모형으로 다시 만든다.
        var holo = GameObject.Find("Hologram_DigestiveSystem");
        if (holo != null && holo.transform.parent != null && holo.transform.root.name == "병원_진료실") holo.transform.SetParent(null, true);
        if (holo == null)
        {
            var model = Prefab("Exhibit_DigestiveSystem");
            if (model != null) { holo = (GameObject)PrefabUtility.InstantiatePrefab(model); holo.name = "Hologram_DigestiveSystem"; }
        }
        var room = ClinicRoom();

        var table = GameObject.Find("Lobby_HologramTable");
        if (table != null) table.transform.position += new Vector3(0, 0, 1.2f) - new Vector3(table.transform.position.x, 0, table.transform.position.z);
        if (holo != null)
        {
            var rot = holo.GetComponent<Rotator>(); if (rot != null) rot.enabled = false;
            holo.transform.rotation = Quaternion.Euler(0, 180, 0);
            // 예전 탁자 모형에는 심장 장식이 붙어 있어 숨기고, 팩의 홀로그램 영사기 위 눈높이(바닥 1~2.2m)에 홀로그램을 띄운다.
            if (holo.transform.parent != null) holo.transform.SetParent(null, true);
            if (table != null) foreach (var r in table.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var r in holo.GetComponentsInChildren<Renderer>(true)) r.enabled = true; // 꺼진 렌더러는 크기를 잴 수 없다
            PackProp("projector", new Vector3(0, 0, 1.2f), 0, .85f, true, room);
            var hb = Bounds(holo);
            if (hb.size.y > 1e-3f) holo.transform.localScale *= 1.5f / hb.size.y;
            hb = Bounds(holo);
            holo.transform.position += new Vector3(0 - hb.center.x, .9f - hb.min.y, 1.2f - hb.center.z);
            // 불투명한 붉은 모형은 심장처럼 보이므로 하늘색 홀로그램 재질을 씌우되, 원래 그림의 명암은 남겨 기관 모양이 읽히게 한다
            var source = Prefab("Exhibit_DigestiveSystem")?.GetComponentInChildren<Renderer>()?.sharedMaterial;
            Texture organs = source == null ? null : source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            var hologram = ScanHologram(organs);
            int swapped = 0;
            if (hologram != null)
                foreach (var r in holo.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(_ => hologram).ToArray(); swapped++;
                    if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            Debug.Log($"[병원] 홀로그램 재질 교체: {holo.name} 렌더러 {swapped}개, 재질 {(hologram != null ? hologram.name : "없음")}");
        }
        else Debug.LogWarning("[병원] Hologram_DigestiveSystem 을 찾지 못함");

        // 초소형 치료 잠수정: 오른쪽 뒤 받침대 위. 진찰을 마치면 탑승구가 열린다.
        var dock = PackProp("torpedo_docking_station_1_side", new Vector3(2.4f, 0, 4.6f), 90, 2.4f, false, room);
        float dockTop = dock != null ? Bounds(dock).max.y : .5f;
        var podRoot = new GameObject("초소형_치료_잠수정").transform; podRoot.SetParent(room, false);
        podRoot.position = new Vector3(2.4f, dockTop + .95f, 4.6f); podRoot.localScale = Vector3.one * .55f;
        podRoot.rotation = Quaternion.Euler(0, 200, 0);
        PodVisual(podRoot);
        var glow = new GameObject("탑승_표시").transform; glow.SetParent(room, false);
        Part(glow, "고리", Torus(.75f, .03f, 64, 8), CockpitMaterial("탑승_빛", new Color(.1f, .35f, .4f), .2f, 1.8f), new Vector3(2.4f, .02f, 3.3f), Quaternion.identity);

        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) spawn.transform.SetPositionAndRotation(new Vector3(0, 0, -3.6f), Quaternion.identity);
        var portal = Object.FindFirstObjectByType<StageExitPortal>(FindObjectsInactive.Include);
        MissionGate gate = null;
        if (portal != null)
        {
            portal.gameObject.SetActive(true);
            portal.transform.position = new Vector3(2.4f, 1.2f, 3.3f);
            var box = portal.GetComponent<BoxCollider>(); if (box != null) { box.size = new Vector3(1.6f, 2.4f, 1.6f); box.center = Vector3.zero; }
            portal.transform.localScale = Vector3.one;
            gate = portal.GetComponent<MissionGate>(); if (gate == null) gate = portal.gameObject.AddComponent<MissionGate>();
            gate.colliders = portal.GetComponents<Collider>(); gate.autoOpenAfter = 240f; gate.openedIndicator = glow.gameObject;
        }

        // 진찰: 홀로그램의 이(위쪽) · 위(가운데 왼쪽) · 대장(아래) 빨간 점을 겨냥해 맞히면 의사 선생님이 원인을 알려 준다.
        var hunt = Hunt("몸_스캔_진찰");
        if (holo != null)
        {
            var b = Bounds(holo);
            var marks = new (string name, Vector3 at)[]
            {
                // 홀로그램은 사람 전신(팔 벌린 폭)이라 몸 비율로 잡는다: 입 ≈ 키의 91%, 위(몸 왼쪽 윗배) ≈ 66%, 대장(오른쪽 아랫배) ≈ 55%
                ("어금니", new Vector3(b.center.x, b.min.y + b.size.y * .91f, b.min.z - .05f)),
                ("위", new Vector3(b.center.x + b.size.y * .04f, b.min.y + b.size.y * .66f, b.min.z - .05f)),
                ("대장", new Vector3(b.center.x - b.size.y * .05f, b.min.y + b.size.y * .55f, b.min.z - .05f)),
            };
            // 다시 적용할 때 예전 자리 묶음이 쌓이지 않게 모두 지운다
            foreach (var oldSpots in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name == "진찰_자리들").ToArray())
                Object.DestroyImmediate(oldSpots.gameObject);
            var spotRoot = new GameObject("진찰_자리들").transform; spotRoot.SetParent(holo.transform.parent, true);
            hunt.spots = marks.Select(m => { var t = new GameObject("진찰_" + m.name).transform; t.SetParent(spotRoot, false); t.position = m.at; return t; }).ToArray();
        }
        hunt.harmfulPrefabs = new[] { HotspotPrefab() }; hunt.helpfulPrefabs = new GameObject[0];
        hunt.scaleRange = Vector2.one; hunt.range = 7f; hunt.aimRadius = .2f; hunt.bob = 0; hunt.releaseRide = false;
        hunt.beginOnStart = true; hunt.resetScore = true;
        hunt.targetLabel = "진찰"; hunt.scoreKey = ""; hunt.goalText = "몸 스캔에서 빨갛게 빛나는 곳을 겨냥하고 방아쇠를 당겨 진찰하세요";
        hunt.clearedStatus = "진찰 완료! 잠수정 옆 출발 버튼을 누르세요";
        hunt.startLines = new NarrationLine[0];
        hunt.hitLines = new[] { Mission("Lobby", "scan_tooth")[0], Mission("Lobby", "scan_stomach")[0], Mission("Lobby", "scan_colon")[0] };
        hunt.clearedLines = Mission("Lobby", "scan_done", 6f);
        if (gate != null) { ClearListeners(hunt.onCleared, gate); UnityEventTools.AddPersistentListener(hunt.onCleared, gate.Open); }
        // 사람 의사·간호사와 대화, 시작 대기 화면, 병원 소리
        // 큰 화면: 진찰 결과 모니터 (간호사에게 말한 증상 → 몸 스캔으로 찾은 곳 → 오늘의 치료 경로)
        var monitor = ClinicMonitorUi(room, false);
        ClearMissing(hunt.onHit); ClearMissing(hunt.onCleared);
        if (monitor != null) { UnityEventTools.AddPersistentListener(hunt.onHit, monitor.Found); UnityEventTools.AddPersistentListener(hunt.onCleared, monitor.ShowPlan); }
        HospitalPeople(room, hunt, gate);
        if (portal != null && gate != null)
        {
            // 탑승구에 걸어 들어가기만 해서는 떠나지 않는다 — 옆 조작대의 '출발' 버튼으로 (진찰 전에는 잠겨 있다)
            portal.walkInToDepart = false; EditorUtility.SetDirty(portal);
            BoardingConsoleUi(room, new Vector3(1.5f, 0, 2.65f), new Vector3(.8f, 0, 1.2f), portal, gate);
        }
        LobbySigns(room, new Vector3(-2.6f, 0, -2.2f), new Vector3(2.4f, 0, 3.3f), spawn != null ? spawn.transform.position : new Vector3(0, 0, -3.6f), 3.2f);
        ExhibitionDevices();
        var amb = GameObject.Find("병원_소리") ?? new GameObject("병원_소리");
        var sa = amb.GetComponent<SceneAmbience>(); if (sa == null) sa = amb.AddComponent<SceneAmbience>(); sa.clipName = "clinic"; sa.volume = .3f;

        // 차분하게 밝은 실내 (하얗게 타지 않게)
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.5f, .55f, .58f); RenderSettings.ambientEquatorColor = new Color(.4f, .44f, .46f); RenderSettings.ambientGroundColor = new Color(.26f, .28f, .3f);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.intensity = .55f; l.shadows = LightShadows.None; }
        EditorUtility.SetDirty(hunt);
    }

    /// <summary>몸 스캔 홀로그램 재질: 반투명하지만 또렷하게 빛나는 하늘색.</summary>
    static Material ScanHologram(Texture organs = null)
    {
        string path = $"{Folder}/몸스캔_홀로그램.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        // 거의 불투명하게(뒤 벽이 비쳐 모양이 흐려지지 않게), 기관 그림을 바탕·빛 모두에 써서 이·위·장 모양이 하늘색 명암으로 보인다
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 1);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.EnableKeyword("_EMISSION"); mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetTexture("_BaseMap", organs); mat.SetTexture("_EmissionMap", organs);
        mat.SetColor("_BaseColor", new Color(.55f, .88f, 1f, .9f)); mat.SetFloat("_Smoothness", .55f);
        mat.SetColor("_EmissionColor", new Color(.16f, .5f, .8f) * (organs != null ? 1.1f : .8f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat); return mat;
    }

    // ───────────────────────────── 치료 결과 ─────────────────────────────

    static void ReportRoom()
    {
        var old = GameObject.Find("치료_보고서"); if (old != null) Object.DestroyImmediate(old);
        var spawn = GameObject.Find("SpawnPoint");
        Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;
        var forward = spawn != null ? spawn.transform.forward : Vector3.forward;
        var go = new GameObject("치료_보고서", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        // 임무가 9~10가지로 늘어 예전 판(440)에서는 마지막 줄이 요약 줄과 겹쳐 판 밖으로 넘쳤다 → 판을 키우고 내용은 칸에 맞춰 글자를 줄인다
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(640, 560);
        rect.position = eye + forward * 2.2f - Vector3.up * .1f; rect.rotation = Quaternion.LookRotation(rect.position - eye); rect.localScale = Vector3.one * ReportScale;
        UiKit();
        var plate = go.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);
        var title = UiText(rect, "제목", new Vector2(28, -20), new Vector2(584, 56), 42, Color.white, FontStyles.Bold); title.text = "치료 보고서";
        var report = go.AddComponent<TreatmentReport>();
        report.body = UiText(rect, "내용", new Vector2(28, -92), new Vector2(584, 370), 28, new Color(.92f, .96f, .98f));
        report.body.enableAutoSizing = true; report.body.fontSizeMin = 20; report.body.fontSizeMax = 28;
        report.body.text = "충치 치료   완료\n치태 세균  6/6   완료\n역류 위산 중화  8/8   완료\n헬리코박터균 퇴치  7/7   완료\n영양소 흡수  18/18   완료\n해로운 균  8/8   완료";
        report.summary = UiText(rect, "요약", new Vector2(28, -476), new Vector2(584, 44), 30, new Color(.5f, .9f, 1f), FontStyles.Bold);
        report.summary.text = "모든 치료를 마쳤어요!";
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();
    }
}
#endif
