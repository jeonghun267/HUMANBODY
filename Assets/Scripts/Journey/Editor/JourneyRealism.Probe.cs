#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 시선 확인용: 장면의 이동 경로 한 점에서 앞·뒤를 찍고, 화면 가운데 광선에 걸리는 물체를 가까운 순서로 적는다.
/// 배치: -probeScene 04_SmallIntestine -probeT 0.34 → Tools/realism_captures/탐침_*.png, Tools/realism_probe.txt
/// </summary>
public static partial class JourneyRealism
{
    public static void Probe()
    {
        var args = System.Environment.GetCommandLineArgs();
        string Arg(string key, string fallback) { int i = System.Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        string scene = Arg("-probeScene", "04_SmallIntestine"); float t = float.Parse(Arg("-probeT", "0.34"), System.Globalization.CultureInfo.InvariantCulture);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.OpenScene($"{SceneDir}/{scene}.unity", OpenSceneMode.Single);
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null || ride.cart == null || ride.cart.Spline == null) { Debug.LogError("[탐침] 이동 경로 없음"); return; }
        var spline = ride.cart.Spline;
        Vector3 pos = spline.EvaluatePosition(t); Vector3 dir = ((Vector3)spline.EvaluateTangent(t)).normalized;
        ride.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up).normalized));
        var report = new StringBuilder($"{scene} t={t} 위치 {pos:F1} 진행 {dir:F2}\n");
        foreach (var (label, d) in new[] { ("앞", dir), ("뒤", -dir) })
        {
            Shoot("Tools/realism_captures", scene, "탐침_" + label, pos, Quaternion.LookRotation(d), 75f);
            var ray = new Ray(pos, d);
            var hits = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer))
                .Select(r => (r, ok: r.bounds.IntersectRay(ray, out float dist), dist: r.bounds.IntersectRay(ray, out float d2) ? d2 : -1))
                .Where(x => x.ok).OrderBy(x => x.dist).Take(25);
            report.Append($"\n[{label}] 가운데 광선에 걸리는 물체(상자 기준, 가까운 순)\n");
            foreach (var (r, _, dist) in hits) report.Append($"  {dist,6:0.0}m  {PathOf(r.transform)}  ({r.GetType().Name}, 크기 {r.bounds.size:F1}, 재질 {(r.sharedMaterial != null ? r.sharedMaterial.name : "-")})\n");
        }
        File.WriteAllText("Tools/realism_probe.txt", report.ToString(), new UTF8Encoding(true));
        Debug.Log("[탐침] → Tools/realism_probe.txt");
    }
}
#endif
