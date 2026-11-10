#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 떼로 나오는 세균·바이러스의 가벼운 판. 한 마리가 삼각형 1만 2천 개라 대장에 수십 마리가 뜨면 Quest 예산(약 75만)을 넘는다.
/// Tools/make_lite_models.py 가 텍스처 좌표를 지키며 줄인 메시(Tools/lite/{이름}.json)를 원래 관절 메시에 맞춰 놓고,
/// 가까운 원래 정점의 법선·뼈 가중치를 옮겨 받아 같은 움직임(JourneyCharacterMotion)과 같은 재질로 '{이름}_가벼움' 프리팹을 만든다.
/// </summary>
public static partial class JourneyRealism
{
    static readonly Dictionary<string, GameObject> litePrefabs = new Dictionary<string, GameObject>();

    /// <summary>가벼운 판이 있으면 그 프리팹, 없으면 null.</summary>
    static GameObject LitePrefab(string name)
    {
        if (litePrefabs.TryGetValue(name, out var cached)) return cached;
        litePrefabs[name] = null;
        string json = $"Tools/lite/{name}.json";
        var full = JourneyPolishSetup.AnimatedPrefab(name);
        var skin = full != null ? full.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        if (!File.Exists(json) || skin == null || skin.sharedMesh == null) return null;

        var mesh = LiteMesh(name, skin.sharedMesh, out string info);
        if (mesh == null) return null;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(full);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh = mesh;
        inst.name = name + "_가벼움";
        var prefab = SavePrefab(inst, name + "_가벼움");
        Debug.Log($"[가벼운 모형] {name}: {info}");
        return litePrefabs[name] = prefab;
    }

    /// <summary>
    /// Tools/lite/{name}.json(줄인 메시, glTF 좌표)을 원래 메시(src) 좌표에 맞춘 가벼운 메시. 축 순서·방향은 겹침으로 고르고,
    /// 법선·뼈 가중치(있으면)·UV(파일에 없으면)는 가까운 원래 정점 것을 옮겨 받는다. 파일이 없으면 null.
    /// </summary>
    static Mesh LiteMesh(string name, Mesh src, out string info)
    {
        info = "";
        string json = $"Tools/lite/{name}.json";
        if (!File.Exists(json) || src == null) return null;
        var data = JObject.Parse(File.ReadAllText(json));
        var gv = data["verts"].Select(a => new Vector3((float)a[0], (float)a[1], (float)a[2])).ToArray();
        var guv = data["uvs"].Select(a => new Vector2((float)a[0], (float)a[1])).ToArray(); // 비어 있으면 가까운 원래 정점 UV
        var tris = data["tris"].Select(t => (int)t).ToArray();
        var sv = src.vertices; var suv = src.uv; var sn = src.normals; var sw = src.boneWeights;

        // 좌표 맞추기: glTF 좌표 → 관절 메시 좌표. 축 순서·방향 후보마다 상자 크기·가운데를 맞춰 보고 원래 메시와 가장 잘 겹치는 것.
        var srcBounds = new Bounds(sv[0], Vector3.zero); foreach (var p in sv) srcBounds.Encapsulate(p);
        int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        var sample = Enumerable.Range(0, 300).Select(i => gv[i * gv.Length / 300]).ToArray();
        System.Func<Vector3, Vector3> best = null; float bestErr = float.MaxValue; string bestName = "";
        foreach (var o in orders)
        for (int s = 0; s < 8; s++)
        {
            var sign = new Vector3((s & 1) == 0 ? 1 : -1, (s & 2) == 0 ? 1 : -1, (s & 4) == 0 ? 1 : -1);
            Vector3 Axes(Vector3 p) => Vector3.Scale(new Vector3(p[o[0]], p[o[1]], p[o[2]]), sign);
            var b = new Bounds(Axes(gv[0]), Vector3.zero); foreach (var p in gv) b.Encapsulate(Axes(p));
            float scale = (srcBounds.size.x + srcBounds.size.y + srcBounds.size.z) / Mathf.Max(1e-6f, b.size.x + b.size.y + b.size.z);
            Vector3 centre = b.center;
            Vector3 Map(Vector3 p) => srcBounds.center + (Axes(p) - centre) * scale;
            float err = sample.Average(p => { var q = Map(p); float d = float.MaxValue; foreach (var v in sv) d = Mathf.Min(d, (v - q).sqrMagnitude); return Mathf.Sqrt(d); });
            if (err < bestErr) { bestErr = err; best = Map; bestName = $"[{o[0]}{o[1]}{o[2]}]×{sign}"; }
        }
        var verts = gv.Select(best).ToArray();

        // 가까운 원래 정점: 법선·뼈 가중치를 옮겨 받고, UV 위아래 방향을 정한다
        var nearest = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float d = float.MaxValue; int k = 0;
            for (int j = 0; j < sv.Length; j++) { float e = (sv[j] - verts[i]).sqrMagnitude; if (e < d) { d = e; k = j; } }
            nearest[i] = k;
        }
        float Median(IEnumerable<float> xs) { var l = xs.OrderBy(x => x).ToList(); return l[l.Count / 2]; }
        bool hasUv = guv.Length == verts.Length;
        bool flipV = hasUv && suv.Length == sv.Length &&
                     Median(Enumerable.Range(0, verts.Length).Select(i => Mathf.Abs(1 - guv[i].y - suv[nearest[i]].y))) <
                     Median(Enumerable.Range(0, verts.Length).Select(i => Mathf.Abs(guv[i].y - suv[nearest[i]].y)));
        var uv = hasUv ? guv.Select(u => flipV ? new Vector2(u.x, 1 - u.y) : u).ToArray()
                       : nearest.Select(k => suv.Length == sv.Length ? suv[k] : Vector2.zero).ToArray();
        var normals = nearest.Select(k => sn.Length == sv.Length ? sn[k] : Vector3.up).ToArray();

        // 감김 방향: 면 법선이 옮겨 받은 법선과 같은 쪽이면 그대로
        int agree = 0;
        for (int t = 0; t < tris.Length; t += 3)
        {
            var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
            agree += Vector3.Dot(n, normals[tris[t]] + normals[tris[t + 1]] + normals[tris[t + 2]]) >= 0 ? 1 : -1;
        }
        if (agree < 0) for (int t = 0; t < tris.Length; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);

        var mesh = new Mesh { name = name + "_가벼운메시" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0); mesh.SetNormals(normals);
        if (sw.Length == sv.Length) mesh.boneWeights = nearest.Select(k => sw[k]).ToArray();
        mesh.bindposes = src.bindposes;
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh($"{Folder}/{name}_가벼운메시.asset", ref mesh);
        info = $"삼각형 {src.triangles.Length / 3} → {tris.Length / 3}, 축 {bestName}, 겹침 오차 {bestErr / srcBounds.size.magnitude:P1}, UV {(hasUv ? (flipV ? "위아래 뒤집음" : "그대로") : "가까운 원래 정점")}, 감김 {(agree < 0 ? "뒤집음" : "그대로")}";
        return mesh;

    }

    /// <summary>원래 모형과 가벼운 판을 같은 조명으로 찍어 비교하고(Tools/lite/*.png), 성능 어림을 다시 낸다 (배치용).</summary>
    public static void LitePreviewAndBudget()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
        ShaderUtil.allowAsyncCompilation = false;
        foreach (var name in new[] { "Char_Probiotic", "Char_HarmfulBacteria", "Char_Virus" })
        {
            var full = JourneyPolishSetup.AnimatedPrefab(name); var light = LitePrefab(name);
            if (full != null) Studio("Tools/lite", name + "_원래", full);
            if (light != null) Studio("Tools/lite", name + "_가벼움", light);
        }
        Budget();
    }

    /// <summary>무리(떠다니는 세균 등)의 모형을 가벼운 판으로 바꾼다.</summary>
    static void LiteSwarms()
    {
        foreach (var swarm in Object.FindObjectsByType<FloatingSwarm>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (swarm.prefabs == null) continue;
            bool changed = false;
            for (int i = 0; i < swarm.prefabs.Length; i++)
            {
                var light = swarm.prefabs[i] != null ? LitePrefab(swarm.prefabs[i].name) : null;
                if (light != null) { swarm.prefabs[i] = light; changed = true; }
            }
            if (changed) EditorUtility.SetDirty(swarm);
        }
    }
}
#endif
