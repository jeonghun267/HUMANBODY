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
/// 성능 예산 어림(Quest): 장면마다 탐험가 시점 몇 곳에서 시야(한쪽 눈 약 100°)에 드는 삼각형·그리기 호출·반투명 겹침을 센다.
/// 실행 중에 생기는 것(흐르는 적혈구, 떠다니는 무리, 임무 표적 등)은 모두 보인다고 치고 더한다(넉넉하게).
/// 기준(Quest 2, 72Hz 권장 어림): 삼각형 75만, 그리기 200. 실제 기기 측정을 대신하지는 못한다.
/// 결과: Tools/realism_budget.txt
/// </summary>
public static partial class JourneyRealism
{
    const int TriangleBudget = 750000, DrawBudget = 200;

    public static void Budget()
    {
        var report = new StringBuilder("장면\t시점\t삼각형\t그리기\t반투명 겹침(화면 몇 장)\t판정\n");
        var spawnNotes = new StringBuilder();
        foreach (var name in new[] { "00_Lobby" }.Concat(BodyScenes).Concat(new[] { "06_Theater" }))
        {
            EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            var (spawnTris, spawnDraws, spawnText) = Spawned();
            spawnNotes.Append($"{name}: 실행 중 생김 삼각형 {spawnTris:N0} · 그리기 {spawnDraws} ({spawnText})\n");
            foreach (var (view, pos, rot) in Views())
            {
                var (tris, draws, overdraw) = Visible(pos, rot);
                tris += spawnTris; draws += spawnDraws;
                string verdict = tris <= TriangleBudget && draws <= DrawBudget && overdraw <= 3 ? "여유"
                               : tris <= TriangleBudget * 1.3f && draws <= DrawBudget * 1.3f && overdraw <= 4 ? "주의" : "초과";
                report.Append($"{name}\t{view}\t{tris:N0}\t{draws}\t{overdraw:0.0}\t{verdict}\n");
            }
        }
        report.Append("\n").Append(spawnNotes);
        report.Append($"\n기준: 삼각형 {TriangleBudget:N0}, 그리기 {DrawBudget}, 반투명 겹침 3장 이하(잠수정 유리 1장 포함). 반투명 겹침은 화면에 걸친 상자 넓이로 어림한 값(입자 제외).\n");
        File.WriteAllText("Tools/realism_budget.txt", report.ToString(), new UTF8Encoding(true));
        Debug.Log("[JourneyRealism] 성능 예산 어림 → Tools/realism_budget.txt");
    }

    /// <summary>탐험가가 실제로 보는 자리: 잠수정 장면은 이동 경로 몇 곳(진행 방향), 걷는 장면은 시작 자리(앞·옆).</summary>
    static IEnumerable<(string, Vector3, Quaternion)> Views()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride != null && ride.cart != null && ride.cart.Spline != null)
        {
            var spline = ride.cart.Spline;
            foreach (var t in new[] { .03f, .25f, .5f, .75f, .95f })
            {
                Vector3 pos = spline.EvaluatePosition(t);
                Vector3 dir = ((Vector3)spline.EvaluateTangent(t)).normalized;
                var flat = new Vector3(dir.x, 0, dir.z);
                bool vertical = flat.magnitude < .5f;
                var look = vertical ? Quaternion.LookRotation(Vector3.down, Vector3.forward) : Quaternion.LookRotation(flat);
                ride.transform.SetPositionAndRotation(pos, vertical ? Quaternion.LookRotation(Vector3.forward) : look);
                yield return ($"경로 {t:0.00}", pos, look);
            }
        }
        else
        {
            var spawn = GameObject.Find("SpawnPoint");
            Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * EyeHeight;
            yield return ("앞", eye, Quaternion.Euler(5, 0, 0));
            yield return ("옆", eye, Quaternion.Euler(5, -70, 0));
            yield return ("뒤", eye, Quaternion.Euler(5, 180, 0));
        }
    }

    static (int tris, int draws, float overdraw) Visible(Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("_BudgetCam"); var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = 100; cam.aspect = 1; cam.nearClipPlane = .05f; cam.farClipPlane = 300;
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        int tris = 0, draws = 0; float overdraw = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
            if (r is ParticleSystemRenderer pr)
            {
                var ps = r.GetComponent<ParticleSystem>();
                draws++; tris += (ps != null ? ps.main.maxParticles : 50) * (pr.renderMode == ParticleSystemRenderMode.Mesh && pr.mesh != null ? Triangles(pr.mesh) : 2);
                continue;
            }
            var mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) { tris += Triangles(mesh); draws += mesh.subMeshCount; }
            else draws++;
            if (r.sharedMaterials.Any(m => m != null && m.renderQueue >= 2500)) overdraw += ScreenShare(cam, r.bounds);
        }
        draws += Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled && c.isRootCanvas); // 안내판 하나당 대략 한 번
        // 융털 카펫 구간(숨긴 물체라 위 찾기에 안 잡힌다): 실행 중처럼 눈 가까운 구간만 센다
        foreach (var carpet in Object.FindObjectsByType<VilliCarpet>(FindObjectsSortMode.None))
            foreach (var chunk in carpet.Chunks)
            {
                if (chunk == null || chunk.bounds.SqrDistance(pos) > carpet.drawRange * carpet.drawRange || !GeometryUtility.TestPlanesAABB(planes, chunk.bounds)) continue;
                var cm = chunk.GetComponent<MeshFilter>().sharedMesh; if (cm != null) { tris += Triangles(cm); draws++; }
            }
        Object.DestroyImmediate(go);
        return (tris, draws, overdraw);
    }

    static int Triangles(Mesh mesh)
    {
        long n = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) n += mesh.GetIndexCount(i) / 3;
        return (int)n;
    }

    /// <summary>상자가 화면에서 차지하는 비율(0~1). 카메라가 상자 안이나 뒤에 걸치면 화면 전체로 친다.</summary>
    static float ScreenShare(Camera cam, Bounds b)
    {
        if (b.Contains(cam.transform.position)) return 1;
        float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
        for (int i = 0; i < 8; i++)
        {
            var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var v = cam.WorldToViewportPoint(c);
            if (v.z <= 0) return 1;
            x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
        }
        return Mathf.Clamp01(Mathf.Clamp01(x1) - Mathf.Clamp01(x0)) * Mathf.Clamp01(Mathf.Clamp01(y1) - Mathf.Clamp01(y0));
    }

    /// <summary>실행 중에 생기는 것들의 삼각형·그리기 합 (모두 보인다고 친다).</summary>
    static (int tris, int draws, string text) Spawned()
    {
        int tris = 0, draws = 0; var parts = new List<string>();
        void Add(string what, IEnumerable<GameObject> prefabs, int count)
        {
            var list = prefabs?.Where(p => p != null).ToArray() ?? new GameObject[0];
            if (list.Length == 0 || count <= 0) return;
            int t = 0, d = 0;
            foreach (var p in list)
            {
                foreach (var f in p.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) { t += Triangles(f.sharedMesh); d += f.sharedMesh.subMeshCount; }
                foreach (var s in p.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null) { t += Triangles(s.sharedMesh); d += s.sharedMesh.subMeshCount; }
            }
            t /= list.Length; d = Mathf.Max(1, d / list.Length);
            tris += t * count; draws += d * count;
            parts.Add($"{what} {count}×{t:N0}");
        }
        foreach (var s in Object.FindObjectsByType<SplineFlow>(FindObjectsSortMode.None)) Add("흐름:" + s.name, s.prefabs, s.count);
        foreach (var s in Object.FindObjectsByType<FloatingSwarm>(FindObjectsSortMode.None)) Add("무리:" + s.name, s.prefabs, s.count);
        foreach (var s in Object.FindObjectsByType<WallAbsorption>(FindObjectsSortMode.None)) Add("흡수:" + s.name, s.prefabs, s.count);
        foreach (var s in Object.FindObjectsByType<NutrientAbsorption>(FindObjectsSortMode.None))
            foreach (var k in s.kinds ?? new NutrientAbsorption.NutrientKind[0]) Add("영양소:" + k.displayName, new[] { k.prefab }, k.count);
        foreach (var h in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None))
        {
            Add("표적:" + h.name, h.harmfulPrefabs, h.spots != null && h.spots.Length > 0 ? h.spots.Length : h.harmfulCount);
            Add("지킬 것:" + h.name, h.helpfulPrefabs, h.spots != null && h.spots.Length > 0 ? 0 : h.helpfulCount);
        }
        foreach (var l in Object.FindObjectsByType<DecayTimeLapse>(FindObjectsSortMode.None))
        {
            int n = l.spots != null ? l.spots.Length : 0;
            Add("충치 과정 세균", new[] { l.germPrefab }, n * l.germsPerSpot);
            Add("충치 과정 찌꺼기", new[] { l.residuePrefab }, n);
        }
        return (tris, draws, string.Join(", ", parts));
    }
}
#endif
