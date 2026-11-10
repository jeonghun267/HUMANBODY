#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 조사용: 신체 장면 안에서 벽·잠수정·액체가 아닌 물체(떠다니거나 벽에 붙은 것)를 모두 찾아
/// 이름·원본 모형·위치·크기·벽까지 거리·역할(스크립트·이름표)을 적고, 하나씩 가까이서 찍는다.
/// 실행할 때 생겨나는 무리(세균·영양소·물방울 등)는 원본 모형을 따로 찍는다.
/// 결과: Tools/inventory/조사.tsv, Tools/inventory/*.png
/// </summary>
public static partial class JourneyRealism
{
    static readonly string[] KnownStructure =
    {
        "연속_기관벽", "통로_끝_벽", "StomachShell", "MouthDome", "ThroatCap", "관찰_잠수정", "체험_안내판", "위벽_충돌",
        "위액_웅덩이", "미즙_물길", "대장_내용물", "열리는_점막", "구멍_안쪽", "분문_입구", "십이지장_유두", "충수_입구", "회맹판",
        "LabelTemplates", "치료_광선", "맞힘", "치료_반짝임", "손_치료_도구",
    };

    public static void Inventory()
    {
        const string outDir = "Tools/inventory";
        if (Directory.Exists(outDir)) foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
        Directory.CreateDirectory(outDir);
        ShaderUtil.allowAsyncCompilation = false;
        var rows = new StringBuilder("장면\t번호\t물체\t원본\t위치\t크기(m)\t벽까지(m)\t상태\t역할\t경로\n");
        foreach (var scene in BodyScenes)
        {
            EditorSceneManager.OpenScene($"{SceneDir}/{scene}.unity", OpenSceneMode.Single);
            // 벽 = 이름이 알려진 벽 충돌체 + 점막 재질을 쓰는 모든 표면(볼·혀·목구멍 천장 등). 점막 표면에는 잠깐 충돌체를 붙여 잰다.
            var temp = new List<MeshCollider>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.GetComponent<Collider>() != null || r.GetComponent<MeshFilter>() == null) continue;
                bool tissue = r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == ShaderName) || r.name == "ThroatCap";
                if (!tissue) continue;
                var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh; temp.Add(mc);
            }
            var walls = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => temp.Contains(c) || c.name == "연속_기관벽" || c.name == "위벽_충돌" || c.name == "Part_Tongue" || c.name == "MouthDome").ToArray();
            Physics.SyncTransforms();

            // 물체 묶기: 렌더러마다 가장 바깥 모형(프리팹) 또는 이름표가 붙은 조상으로 묶는다.
            var owners = new Dictionary<GameObject, List<Renderer>>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;
                if (KnownStructure.Any(k => PathOf(r.transform).Contains(k))) continue;
                var label = r.GetComponentInParent<PartLabel>();
                var root = label != null ? label.gameObject : PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject) ?? r.gameObject;
                if (!owners.TryGetValue(root, out var list)) owners[root] = list = new List<Renderer>();
                list.Add(r);
            }

            int index = 0;
            foreach (var pair in owners.OrderBy(p => p.Key.transform.position.z))
            {
                var go = pair.Key; index++;
                var b = pair.Value[0].bounds; foreach (var r in pair.Value) b.Encapsulate(r.bounds);
                float wall = WallDistance(b, walls.Where(w => !w.transform.IsChildOf(go.transform)).ToArray());
                string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
                if (string.IsNullOrEmpty(source)) { var mf = pair.Value[0].GetComponent<MeshFilter>(); source = mf != null && mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) + " (" + mf.sharedMesh.name + ")" : ""; }
                var label = go.GetComponent<PartLabel>();
                var roles = go.GetComponentsInParent<MonoBehaviour>(true).Concat(go.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null)
                    .Select(m => m.GetType().Name).Where(n => n != "PartLabel").Distinct().Take(5);
                string role = (label != null ? $"이름표:{label.displayName}({label.partId}) " : "") + string.Join(",", roles);
                string path = PathOf(go.transform);
                // 가운데가 벽 면에서 얼마나 떨어졌나(음수 = 벽 속). 반쯤 묻혀 목만 내민 것(위샘 등)은 가운데가 벽 면 근처라 '붙음'이다.
                float half = b.extents.magnitude * .6f, centre = wall + half;
                string state = go.GetComponentInParent<GentleFloat>() != null ? "둥실 떠다님(의도)" : go.GetComponentInParent<HingeClose>() != null ? "경첩에 달림(의도)"
                             : go.GetComponentInParent<Sway>() != null ? "매달려 흔들림(의도)"
                             : go.GetComponentInParent<JourneyCharacterMotion>() != null || go.GetComponentInChildren<JourneyCharacterMotion>() != null ? "헤엄치는 캐릭터(의도)"
                             : go.name.Contains("Platform") ? "잠수정 정박대(의도)"
                             : path.Contains("벽_혈관") ? "벽 바로 밑 혈관(의도)"
                             : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > 4f ? "큰 구조물(입 바닥·천장·치열)"
                             : centre < -half * .8f ? "벽 속에 묻힘" : wall < .35f ? "벽에 붙음" : "떠 있음";
                string name = $"{scene}_{index:00}";
                rows.Append($"{scene}\t{index}\t{go.name}\t{source}\t{b.center.x:0.0},{b.center.y:0.0},{b.center.z:0.0}\t{b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0}\t{wall:0.00}\t{state}\t{role}\t{PathOf(go.transform)}\n");
                CloseUp(outDir, name, b);
            }

            // 실행 중에 생기는 무리의 원본 모형
            var spawned = new List<(string what, GameObject prefab)>();
            foreach (var s in Object.FindObjectsByType<FloatingSwarm>(FindObjectsSortMode.None)) foreach (var p in s.prefabs ?? new GameObject[0]) spawned.Add(("떠다니는 무리:" + s.name, p));
            foreach (var s in Object.FindObjectsByType<SplineFlow>(FindObjectsSortMode.None)) foreach (var p in s.prefabs ?? new GameObject[0]) spawned.Add(("흐르는 줄:" + s.name, p));
            foreach (var s in Object.FindObjectsByType<WallAbsorption>(FindObjectsSortMode.None)) foreach (var p in s.prefabs ?? new GameObject[0]) spawned.Add(("벽 흡수:" + s.name, p));
            foreach (var s in Object.FindObjectsByType<NutrientAbsorption>(FindObjectsSortMode.None)) foreach (var k in s.kinds ?? new NutrientAbsorption.NutrientKind[0]) spawned.Add(("영양소:" + k.displayName, k.prefab));
            foreach (var s in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None))
            {
                foreach (var p in s.harmfulPrefabs ?? new GameObject[0]) spawned.Add(("임무 표적:" + s.name, p));
                foreach (var p in s.helpfulPrefabs ?? new GameObject[0]) spawned.Add(("지킬 대상:" + s.name, p));
            }
            foreach (var s in Object.FindObjectsByType<EnzymeCutSequence>(FindObjectsSortMode.None)) if (s.fragmentPrefab != null) spawned.Add(("절단 조각:" + s.name, s.fragmentPrefab));
            int k2 = 0;
            foreach (var (what, prefab) in spawned.Where(x => x.prefab != null).GroupBy(x => x.prefab).Select(g => g.First()))
            {
                k2++;
                string name = $"{scene}_실행_{k2:00}";
                rows.Append($"{scene}\t실행{k2}\t{prefab.name}\t{AssetDatabase.GetAssetPath(prefab)}\t-\t-\t-\t실행 중 생성\t{what}\t-\n");
                Studio(outDir, name, prefab);
            }
        }
        File.WriteAllText($"{outDir}/조사.tsv", rows.ToString(), new UTF8Encoding(true));
        Debug.Log("[JourneyRealism] 물체 조사 완료 → " + outDir);
    }

    /// <summary>
    /// 물체 가운데에서 여러 방향으로 벽까지 잰 가장 가까운 거리 − 물체 반경. 음수면 벽 속.
    /// 벽 뒷면도 맞힌다: 가운데가 벽 면 뒤(벽 속)에 있으면 바깥으로 쏜 광선이 뒷면만 지나가서, 뒷면을 못 맞히면 먼 반대쪽 벽을 재게 된다.
    /// 뒷면을 맞히면(광선과 면 방향이 같으면) 그만큼 벽 속에 있는 것이다.
    /// </summary>
    static float WallDistance(Bounds b, MeshCollider[] walls)
    {
        float best = float.MaxValue, half = b.extents.magnitude * .6f;
        var dirs = new List<Vector3>();
        for (int i = 0; i < 26; i++) { var d = new Vector3((i % 3) - 1, ((i / 3) % 3) - 1, (i / 9) - 1); if (d != Vector3.zero) dirs.Add(d.normalized); }
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        foreach (var w in walls)
        foreach (var d in dirs)
            if (w.Raycast(new Ray(b.center, d), out var hit, 50f))
            {
                float signed = Vector3.Dot(hit.normal, d) > 0 ? -hit.distance : hit.distance;
                if (Mathf.Abs(signed) < Mathf.Abs(best)) best = signed;
            }
        Physics.queriesHitBackfaces = backfaces;
        return best == float.MaxValue ? 99f : best - half;
    }

    static void CloseUp(string dir, string name, Bounds b)
    {
        float size = Mathf.Max(.3f, b.extents.magnitude);
        var cam = Camera.main != null ? Camera.main.transform.position : b.center + new Vector3(0, .5f, -3);
        var spawn = GameObject.Find("SpawnPoint");
        var from = spawn != null ? spawn.transform.position + Vector3.up * 1.6f : cam;
        var dirToObj = (b.center - from); if (dirToObj.sqrMagnitude < .01f) dirToObj = Vector3.forward;
        var pos = b.center - dirToObj.normalized * (size * 2.4f + .4f);
        ShootSmall(dir, name, pos, Quaternion.LookRotation(b.center - pos));
    }

    static void ShootSmall(string dir, string name, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("_InvCam"); var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = 40; cam.nearClipPlane = .02f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.Skybox;
        foreach (var lamp in Object.FindObjectsByType<BodyHeadlamp>(FindObjectsSortMode.None)) lamp.PlaceAt(cam.transform);
        var rt = new RenderTexture(480, 360, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(480, 360, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 480, 360), 0, 0); tex.Apply(); RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }

    /// <summary>원본 모형을 장면 밖 빈 곳에 놓고 밝은 조명으로 찍는다.</summary>
    static void Studio(string dir, string name, GameObject prefab)
    {
        var at = new Vector3(0, -400, 0);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.transform.position = at;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Object.DestroyImmediate(go); return; }
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var light = new GameObject("_InvLight").AddComponent<Light>(); light.type = LightType.Point; light.range = 50; light.intensity = 3;
        float size = Mathf.Max(.2f, b.extents.magnitude);
        var pos = b.center + new Vector3(.4f, .5f, -1f).normalized * (size * 3f);
        light.transform.position = pos + Vector3.up * size;
        ShootSmall(dir, name, pos, Quaternion.LookRotation(b.center - pos));
        Object.DestroyImmediate(light.gameObject); Object.DestroyImmediate(go);
    }
}
#endif
