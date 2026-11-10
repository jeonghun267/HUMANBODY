#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 겹침 검사: 여정 씬마다 파츠(모델 인스턴스)의 월드 바운즈를 모아
///  (1) 파츠끼리 AABB 가 겹치는 쌍 (겹친 부피 / 작은 쪽 부피 비율)
///  (2) 터널/셸 안에 있어야 할 파츠가 벽 밖으로 튀어나온 경우
/// 를 Tools/overlap_report.txt 에 기록한다. 배치모드: -executeMethod JourneyOverlapReport.Run
/// </summary>
public static class JourneyOverlapReport
{
    const string SceneDir = "Assets/Scenes/Journey";
    const string OutPath = "Tools/overlap_report.txt";
    static readonly string[] PartPrefixes = { "Part_", "Prop_", "Char_", "Exhibit_", "Lobby_", "Hologram", "Guide_" };
    static readonly string[] EnvNames = { "WorldShell", "MouthDome", "StomachShell", "MucusLayer", "ThroatCap", "Env_TheaterDome", "Tunnel", "TheaterScreen", "LobbyFloor", "CeilingRing", "TongueFloor", "TongueWalkSurface" };

    class Part { public string name; public Bounds b; public string path; }

    [MenuItem("인체탐험/배치 겹침 검사", priority = 31)]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var path in Directory.GetFiles(SceneDir, "*.unity").OrderBy(p => p))
        {
            EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single);
            string scene = System.IO.Path.GetFileNameWithoutExtension(path);
            sb.AppendLine($"===== {scene}");

            var parts = new List<Part>();
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t => t.gameObject))
            {
                if (!IsPartRoot(go)) continue;
                var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && !(r is LineRenderer)).ToArray();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                parts.Add(new Part { name = go.name, b = b, path = HPath(go.transform) });
            }

            // (1) 파츠끼리 겹침
            var pairs = new List<(float ratio, Part a, Part c, Bounds inter)>();
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    if (IsAncestor(parts[i].path, parts[j].path) || IsAncestor(parts[j].path, parts[i].path)) continue;
                    if (!parts[i].b.Intersects(parts[j].b)) continue;
                    var inter = Intersection(parts[i].b, parts[j].b);
                    float vi = Vol(inter), vs = Mathf.Min(Vol(parts[i].b), Vol(parts[j].b));
                    float ratio = vs > 1e-6f ? vi / vs : 0f;
                    if (ratio >= 0.05f) pairs.Add((ratio, parts[i], parts[j], inter));
                }
            foreach (var p in pairs.OrderByDescending(p => p.ratio))
                sb.AppendLine($"  OVERLAP {p.ratio:P0}  {p.a.name} {F(p.a.b)}  <->  {p.c.name} {F(p.c.b)}");

            // (2) 관/셸 밖으로 튀어나온 파츠
            var tunnel = GameObject.Find("Tunnel");
            if (tunnel != null && tunnel.transform.childCount >= 2)
            {
                var segs = Enumerable.Range(0, tunnel.transform.childCount).Select(k => Bounds(tunnel.transform.GetChild(k).gameObject)).ToList();
                Vector3 axis = (segs[1].center - segs[0].center).normalized;
                Vector3 ax = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
                var s0 = segs[0].size;
                float radius = 0.5f * Mathf.Max(s0.x * (1 - ax.x), s0.y * (1 - ax.y), s0.z * (1 - ax.z));
                Vector3 c0 = segs[0].center;
                sb.AppendLine($"  [tunnel axis={axis} radius={radius:F2} start={c0}]");
                foreach (var p in parts)
                {
                    if (p.path.StartsWith("Tunnel/")) continue;
                    float maxD = 0f;
                    foreach (var corner in Corners(p.b))
                    {
                        Vector3 rel = corner - c0; rel -= axis * Vector3.Dot(rel, axis);
                        maxD = Mathf.Max(maxD, rel.magnitude);
                    }
                    if (maxD > radius * 1.02f) sb.AppendLine($"  OUTSIDE-TUBE {p.name}: 바운즈 최대 반경 {maxD:F2} > 관 반경 {radius:F2}  {F(p.b)}");
                }
            }
            var shell = GameObject.Find("StomachShell");
            if (shell != null)
            {
                var c = shell.transform.position; var r = shell.transform.lossyScale * 0.5f;
                foreach (var p in parts)
                {
                    float maxE = 0f;
                    foreach (var corner in Corners(p.b))
                    {
                        var d = corner - c;
                        maxE = Mathf.Max(maxE, (d.x * d.x) / (r.x * r.x) + (d.y * d.y) / (r.y * r.y) + (d.z * d.z) / (r.z * r.z));
                    }
                    if (maxE > 1.05f) sb.AppendLine($"  OUTSIDE-SHELL {p.name}: 타원 반경 비 {Mathf.Sqrt(maxE):F2}  {F(p.b)}");
                }
            }
            var spawn = GameObject.Find("SpawnPoint");
            if (spawn != null)
            {
                var eye = spawn.transform.position + Vector3.up * 1.6f;
                foreach (var p in parts)
                    if (p.b.Contains(eye) || p.b.SqrDistance(eye) < 0.35f * 0.35f)
                        sb.AppendLine($"  NEAR-SPAWN {p.name}: 플레이어 눈 위치와 겹침/35cm 이내  {F(p.b)}");
            }
            sb.AppendLine($"  ({parts.Count} parts, {pairs.Count} overlaps)");
        }
        File.WriteAllText(OutPath, sb.ToString());
        Debug.Log($"[JourneyOverlapReport] 완료 → {OutPath}\n{sb}");
    }

    static bool IsPartRoot(GameObject go)
    {
        if (System.Array.IndexOf(EnvNames, go.name) >= 0) return false;
        if (go.name.StartsWith("WorldShell") || go.name.StartsWith("Template_") || go.name == "LabelTemplates") return false;
        if (go.transform.parent != null && go.transform.parent.name == "Tunnel") return false;
        // 모델 인스턴스 루트(glTF) 또는 이름표/전시대가 붙은 오브젝트만 파츠로 본다 (모델 내부 노드는 제외)
        return PrefabUtility.IsAnyPrefabInstanceRoot(go) || go.name.EndsWith("(MISSING)") || go.GetComponent<PartLabel>() != null || go.GetComponent<ExhibitStation>() != null;
    }

    static bool IsAncestor(string a, string b) => b.StartsWith(a + "/");
    static string HPath(Transform t) => t.parent == null ? t.name : HPath(t.parent) + "/" + t.name;
    static Bounds Bounds(GameObject go) { var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    static float Vol(Bounds b) => Mathf.Max(0, b.size.x) * Mathf.Max(0, b.size.y) * Mathf.Max(0, b.size.z);
    static Bounds Intersection(Bounds a, Bounds b)
    {
        var min = Vector3.Max(a.min, b.min); var max = Vector3.Min(a.max, b.max);
        var r = new Bounds(); r.SetMinMax(min, max); return r;
    }
    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }
    static string F(Bounds b) => $"[c={b.center.x:F1},{b.center.y:F1},{b.center.z:F1} s={b.size.x:F1}x{b.size.y:F1}x{b.size.z:F1}]";
}
#endif
