#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 입 장면을 실제 입 구조 비율로 다시 배치한다. 치열 폭 12m ≈ 실제 약 6cm 이므로 약 200배이며, 아래 상수는 실제 mm 값을 200배 한 것이다.
/// 위아래 치열 맞물림, 턱관절 회전축, 치열 안쪽 잇몸에 이어진 입천장·물렁입천장과 목젖, 치열 안을 채우는 혀(걷는 바닥 겸용),
/// 어금니 위 음식, 침이 나오는 구멍을 실제 모형 꼭짓점에서 계산하므로 여러 번 실행해도 같은 결과가 나온다.
/// </summary>
public static class JourneyMouthAnatomy
{
    const string ScenePath = "Assets/Scenes/Journey/01_Mouth.unity";
    const string Folder = "Assets/Data/Generated/MouthAnatomy";
    const string CaptureDir = "Tools/mouth_anatomy_captures";
    const string ReportPath = "Tools/mouth_anatomy_report.txt";

    const float OcclusalY = 4.05f;          // 위아래 어금니가 맞닿는 높이 (기존 혀 표면 높이와 비슷하게)
    const float UpperScale = 6.57f;         // 윗니 활이 아랫니 활보다 약 4% 커서 바깥쪽에서 덮는다
    const float LowerScale = 6.32f;
    const float UpperFrontZ = -2.9f;        // 윗앞니 바깥면
    const float Overjet = .8f;              // 윗앞니 바깥면과 아랫앞니 바깥면의 앞뒤 거리 (약 4mm)
    const float Overbite = .5f;             // 윗앞니가 아랫앞니를 위에서 덮는 높이 (약 2.5mm)
    const float RestGap = .9f;              // 평소 벌린 자세의 어금니 사이 (음식이 들어갈 틈, 약 4.5mm)
    const float BiteGap = .3f;              // 음식을 문 순간 어금니 사이 (눌린 음식 두께)
    const float CondyleUp = 4.4f;           // 턱관절은 씹는 면보다 약 2.2cm 위
    const float CondyleBack = 5f;           // 마지막 어금니보다 약 2.5cm 뒤
    const float GrindAngle = 5f;            // 다물 때 옆으로 비켜 가는 각도 (어금니에서 약 0.5m = 2.5mm)
    const float TongueBelowOcclusal = .25f; // 혀 윗면은 아랫어금니 씹는 면보다 조금 낮다
    const float TongueMargin = .2f;         // 혀 옆면과 아랫니 안쪽 사이 틈
    const float GumClip = 1.9f;             // 윗잇몸을 씹는 면 위 이 높이(치관 바로 위, 약 1cm)에서 다듬는다 — 탐험자 눈높이와 가까워 높으면 잘린 윗면이 보인다
    const int PalateCols = 40;
    // 입 안 벽(볼·입술·목구멍) 타원체: 볼 안쪽은 윗니 바깥에서 약 3m(입안뜰)
    static readonly Vector3 DomeC = new Vector3(0, OcclusalY + 1.5f, 3.6f), DomeR = new Vector3(9.5f, 9.5f, 13.5f);

    // 마지막 배치 결과 (검사·촬영용)
    static Layout last;

    class Layout
    {
        public float thetaRest, thetaBite, zTip, zBase, zSlope0, spawnZ, gatherZ, hardBack;
        public Vector3 pivot, chewSurface, freeEdge, throat;
        public Func<float, float> top, hw, thick;
        public List<string> notes = new List<string>();
    }

    // ───────────────────────────── 메뉴 / 배치모드 진입점 ─────────────────────────────

    [MenuItem("인체탐험/입 장면 해부학 배치 적용", priority = 16)]
    public static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = ApplyToScene();
        EditorUtility.DisplayDialog("인체탐험", string.Join("\n", report.Where(l => l.StartsWith("실패:")).DefaultIfEmpty("입 장면 배치와 검사를 마쳤습니다. " + ReportPath + " 를 확인하세요.")), "확인");
    }

    /// <summary>배치모드: Unity.exe -batchmode -projectPath . -executeMethod JourneyMouthAnatomy.ApplyBatch</summary>
    public static void ApplyBatch()
    {
        int code = 0;
        try { if (ApplyToScene().Any(l => l.StartsWith("실패:"))) code = 2; }
        catch (Exception ex) { Debug.LogException(ex); File.AppendAllText(ReportPath, "\n실패: " + ex + "\n", new UTF8Encoding(false)); code = 1; }
        EditorApplication.Exit(code);
    }

    static List<string> ApplyToScene()
    {
        var report = new List<string>();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        // 기존 후처리(재질, 침 입자 연결, 음식 분해 재질)를 그대로 거치게 한다. 그 안에서 Configure 가 호출된다.
        JourneyPolishSetup.ConfigureScene("Mouth");
        report.AddRange(last.notes);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        report.AddRange(Verify());
        Capture();
        report.Add("실패 수: " + report.Count(l => l.StartsWith("실패:")));
        File.WriteAllLines(ReportPath, report, new UTF8Encoding(false));
        return report;
    }

    // ───────────────────────────── 배치 ─────────────────────────────

    /// <summary>열린 입 장면을 해부학 배치로 맞춘다. JourneyEnvironmentRepair.Configure("Mouth") 에서 호출.</summary>
    public static void Configure()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var L = last = new Layout();
        var upper = Need("Part_UpperTeethArch"); var lower = Need("Part_LowerTeethArch");
        var jaw = Need("LowerJawPivot").transform;
        var chew = Object.FindFirstObjectByType<ChewingSequence>();
        var swallow = Object.FindFirstObjectByType<SwallowSequence>();

        // 1. 윗니 활: 치관이 아래, 앞니가 -Z. 어금니 끝이 씹는 높이에 오게.
        var upperFilter = upper.GetComponentsInChildren<MeshFilter>().First();
        var upperOriginal = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(upper))).OfType<Mesh>().FirstOrDefault()
                            ?? upperFilter.sharedMesh;
        upperFilter.sharedMesh = upperOriginal;
        upper.transform.SetParent(null, true);
        upper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(180, 0, 0));
        upper.transform.localScale = Vector3.one * UpperScale;
        var uv = WorldVerts(upper);
        float uMinZ = uv.Min(v => v.z), uDepth = uv.Max(v => v.z) - uMinZ;
        float uMolarTip = uv.Where(v => v.z > uMinZ + .6f * uDepth).Min(v => v.y);
        upper.transform.position = new Vector3(-(uv.Min(v => v.x) + uv.Max(v => v.x)) / 2, OcclusalY - uMolarTip, UpperFrontZ - uMinZ);
        uv = WorldVerts(upper);
        float uIncisorTip = uv.Where(v => v.z < UpperFrontZ + .15f * uDepth && Mathf.Abs(v.x) < 1.5f).Min(v => v.y);
        // Meshy 윗니 모형은 잇몸이 잇몸선 위로 6m 까지 날개처럼 퍼지고 안쪽에 판이 있어 이와 잇몸이 따로 노는 것처럼 보인다.
        // 씹는 면 위 GumClip(치관 바로 위 잇몸) 위를 잘라 내고, 그 윗단을 입천장 덮개가 덮는다. 원본 모형 파일은 그대로 둔다.
        var trimmed = TrimAbove(upperOriginal, upperFilter.transform.localToWorldMatrix, OcclusalY + GumClip);
        SaveMesh(Folder + "/윗니_잇몸_다듬음.asset", ref trimmed);
        upperFilter.sharedMesh = trimmed;
        foreach (var mc in upper.GetComponentsInChildren<MeshCollider>()) { mc.sharedMesh = null; mc.sharedMesh = trimmed; }
        uv = WorldVerts(upper);

        // 2. 아랫니 활 (다문 자세): 씹는 면의 앞뒤 기울기를 윗니와 평행하게(앞니 덮임 2.5mm), 앞니는 윗앞니 뒤에.
        lower.transform.SetParent(null, true);
        jaw.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        lower.transform.localScale = Vector3.one * LowerScale;
        float target = (uIncisorTip - OcclusalY) + Overbite;
        float lo = -30f, hi = 30f;
        for (int i = 0; i < 40; i++) { float mid = (lo + hi) / 2; if (LowerSlope(lower, mid) < target) lo = mid; else hi = mid; }
        float tilt = (lo + hi) / 2;
        lower.transform.rotation = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.Euler(0, 180, 0);
        lower.transform.position = Vector3.zero;
        var lv = WorldVerts(lower);
        float lMinZ = lv.Min(v => v.z), lDepth = lv.Max(v => v.z) - lMinZ;
        float lMolarTop = lv.Where(v => v.z > lMinZ + .6f * lDepth).Max(v => v.y);
        lower.transform.position = new Vector3(-(lv.Min(v => v.x) + lv.Max(v => v.x)) / 2, OcclusalY - lMolarTop, UpperFrontZ + Overjet - lMinZ);
        lv = WorldVerts(lower);
        float lMaxZ = lv.Max(v => v.z);
        L.notes.Add($"배치: 아랫니 활 기울기 {tilt:F1}° (윗니와 씹는 면 평행, 앞니 덮임 {Overbite}m)");

        // 3. 턱관절 회전축: 마지막 어금니 뒤·위. 아랫니를 그 자식으로.
        L.pivot = new Vector3(0, OcclusalY + CondyleUp, lMaxZ + CondyleBack);
        jaw.SetPositionAndRotation(L.pivot, Quaternion.identity);
        lower.transform.SetParent(jaw, true);

        // 씹는 자리: 왼쪽 아래 첫째 큰어금니 씹는 면 가운데
        var zone = lv.Where(v => v.x < -1f && v.z > lMaxZ - 3.8f && v.z < lMaxZ - 2.0f).ToList();
        float zoneTop = zone.Max(v => v.y);
        var crown = zone.Where(v => v.y > zoneTop - .3f).ToList();
        var M = new Vector3(crown.Average(v => v.x), zoneTop - .05f, crown.Average(v => v.z));
        L.thetaRest = SolveGap(uv, M, RestGap);
        L.thetaBite = SolveGap(uv, M, BiteGap);
        var chewPoint = Child(jaw, "음식_씹는_어금니_자리");
        chewPoint.position = M; chewPoint.rotation = Quaternion.identity;
        jaw.localRotation = Quaternion.AngleAxis(L.thetaRest, Vector3.right);
        L.chewSurface = chewPoint.position;
        L.notes.Add($"배치: 턱관절 축 {Fmt(L.pivot)}, 평소 벌림 {L.thetaRest:F1}°, 음식 무는 각도 {L.thetaBite:F1}°");

        // 4. 혀: 세 자세(벌림, 무는 순간, 옆으로 비킨 순간)의 아랫니 안쪽 경계에 맞춰 만든다.
        var poses = new List<Vector3>();
        foreach (var q in new[] {
            Quaternion.AngleAxis(L.thetaRest, Vector3.right),
            Quaternion.AngleAxis(L.thetaBite, Vector3.right),
            Quaternion.AngleAxis(L.thetaRest, Vector3.right) * Quaternion.AngleAxis(GrindAngle, Vector3.up) * Quaternion.AngleAxis((L.thetaBite - L.thetaRest) * .707f, Vector3.right),
            Quaternion.AngleAxis(L.thetaRest, Vector3.right) * Quaternion.AngleAxis(-GrindAngle, Vector3.up) * Quaternion.AngleAxis((L.thetaBite - L.thetaRest) * .707f, Vector3.right) })
        {
            jaw.localRotation = q;
            poses.AddRange(WorldVerts(lower));
        }
        jaw.localRotation = Quaternion.AngleAxis(L.thetaRest, Vector3.right);
        BuildTongue(L, poses, lMaxZ);

        // 5. 입천장(단단한 입천장 + 물렁입천장): 윗니 안쪽 잇몸에 이어 붙인다.
        BuildPalate(L, uv);

        // 6. 목젖은 물렁입천장 끝 가운데에 매달고, 후두덮개는 혀뿌리 뒤에 세운다.
        PlaceUvulaAndEpiglottis(L);

        // 7. 음식, 시작 지점, 씹기 구역, 목구멍 지점
        PlaceFoodAndFlow(L, chew, swallow, chewPoint);

        // 8. 침샘 모형을 지우고 침이 나오는 구멍(귀밑샘·턱밑샘·혀밑샘)에서 침이 나오게
        PlaceSalivaOpenings(L, uv, lower);

        // 9. 입 안 벽(볼·입술·목구멍)과 조명, 이·목젖 이름판
        var dome = GameObject.Find("MouthDome");
        if (dome != null) { dome.transform.SetPositionAndRotation(DomeC, Quaternion.identity); dome.transform.localScale = DomeR * 2; }
        var light = GameObject.Find("InteriorLight");
        if (light != null) light.transform.position = new Vector3(0, OcclusalY + 3f, 3.5f);
        // 이름판은 씹는 쪽(왼쪽) 작은어금니 안쪽, 탐험자가 바라보는 곳에 둔다 (위아래 이 사이 틈에 겹치지 않게 위·아래로 나눔).
        RepositionLabel(upper, 0, new Vector3(L.chewSurface.x + .6f, OcclusalY + .75f, L.chewSurface.z - 1.6f));
        RepositionLabel(lower, 0, new Vector3(L.chewSurface.x + .3f, L.chewSurface.y + .3f, L.chewSurface.z - 1.6f));

        Physics.SyncTransforms();
    }

    static float LowerSlope(GameObject lower, float tilt)
    {
        lower.transform.rotation = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.Euler(0, 180, 0);
        var v = WorldVerts(lower);
        float minZ = v.Min(p => p.z), depth = v.Max(p => p.z) - minZ;
        float molar = v.Where(p => p.z > minZ + .6f * depth).Max(p => p.y);
        float incisor = v.Where(p => p.z < minZ + .15f * depth && Mathf.Abs(p.x) < 1.5f).Max(p => p.y);
        return incisor - molar;
    }

    /// <summary>턱관절 축 회전각(음수 = 벌림)에서 씹는 자리와 그 위 윗어금니 끝 사이 높이가 gap 이 되는 각도.</summary>
    static float SolveGap(List<Vector3> upperVerts, Vector3 contact, float gap)
    {
        float lo = -30f, hi = 5f; // lo: 크게 벌림(틈 큼), hi: 다묾
        for (int i = 0; i < 40; i++)
        {
            float mid = (lo + hi) / 2;
            if (GapAt(upperVerts, contact, mid) > gap) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    static float GapAt(List<Vector3> upperVerts, Vector3 contact, float theta)
    {
        var p = last.pivot + Quaternion.AngleAxis(theta, Vector3.right) * (contact - last.pivot);
        float above = upperVerts.Where(u => (u.x - p.x) * (u.x - p.x) + (u.z - p.z) * (u.z - p.z) < .49f).Select(u => u.y).DefaultIfEmpty(OcclusalY).Min();
        return above - p.y;
    }

    // ───────────────────────────── 혀 ─────────────────────────────

    static void BuildTongue(Layout L, List<Vector3> lowerPoses, float lMaxZ)
    {
        float topOral = OcclusalY - TongueBelowOcclusal;
        float bandLo = topOral - 2.2f, bandHi = topOral + .45f;
        const float step = .15f;
        var zs = new List<float>(); var widths = new List<float>();
        // 벌린 자세에서는 아랫니가 뒤로 약 0.5m 물러나므로 마지막 어금니보다 1m 뒤까지 본다.
        float zFrom = lowerPoses.Min(v => v.z), zArchEnd = lMaxZ + 1f;
        float zTip = float.NaN;
        for (float z = zFrom; z < zArchEnd + .01f; z += step)
        {
            var band = lowerPoses.Where(v => Mathf.Abs(v.z - z) < .2f && v.y > bandLo && v.y < bandHi).ToList();
            if (band.Count == 0) continue;
            if (band.Any(v => Mathf.Abs(v.x) < .6f)) { zTip = float.NaN; zs.Clear(); widths.Clear(); continue; } // 앞니 안쪽보다 앞
            var left = band.Where(v => v.x < -.05f).Select(v => -v.x).DefaultIfEmpty(float.PositiveInfinity).Min();
            var right = band.Where(v => v.x > .05f).Select(v => v.x).DefaultIfEmpty(float.PositiveInfinity).Min();
            float w = Mathf.Min(left, right) - TongueMargin;
            if (float.IsInfinity(w)) continue;
            if (float.IsNaN(zTip)) zTip = z - .05f;
            zs.Add(z); widths.Add(Mathf.Clamp(w, .2f, 3.7f));
        }
        if (zs.Count < 10) throw new InvalidOperationException("아랫니 안쪽 경계를 찾지 못함");
        // 들쭉날쭉한 치아 안쪽면을 부드럽게
        var smooth = widths.Select((w, i) => Enumerable.Range(i - 3, 7).Where(j => j >= 0 && j < widths.Count).Min(j => widths[j]) * .5f
                                             + Enumerable.Range(i - 2, 5).Where(j => j >= 0 && j < widths.Count).Average(j => widths[j]) * .5f).ToArray();
        float hwLast = smooth.Last();
        var zArr = zs.ToArray();
        L.zTip = zTip; L.zBase = lMaxZ + 4.2f; L.zSlope0 = lMaxZ - 1.2f;
        float zBase = L.zBase, zSlope0 = L.zSlope0;
        const float tipRadius = 2.2f;
        L.hw = z =>
        {
            if (z <= zTip) return 0f;
            float w;
            if (z >= zArr[zArr.Length - 1]) w = hwLast * (1f - .22f * Mathf.Clamp01((z - zArr[zArr.Length - 1]) / (zBase - zArr[zArr.Length - 1])));
            else if (z <= zArr[0]) w = smooth[0];
            else
            {
                // 빈 단면을 건너뛴 곳이 있어 간격이 고르지 않으므로 이진 탐색
                int i = Array.BinarySearch(zArr, z); if (i < 0) i = ~i - 1;
                i = Mathf.Clamp(i, 0, zArr.Length - 2);
                w = Mathf.Lerp(smooth[i], smooth[i + 1], Mathf.InverseLerp(zArr[i], zArr[i + 1], z));
            }
            float d = z - zTip;
            if (d < tipRadius) w = Mathf.Min(w, tipRadius * Mathf.Sqrt(Mathf.Max(0, 1 - (tipRadius - d) * (tipRadius - d) / (tipRadius * tipRadius))));
            return w;
        };
        L.top = z =>
        {
            float y = topOral;
            float d = z - zTip;
            if (d < 2.2f) y -= .5f * (1 - Mathf.Clamp01(d / 2.2f)) * (1 - Mathf.Clamp01(d / 2.2f));
            if (z > zSlope0) { float t = Mathf.Clamp01((z - zSlope0) / (zBase - zSlope0)); y -= 3.3f * t * t * (3 - 2 * t); }
            return y;
        };
        // 혀끝은 얇고, 혀뿌리는 목구멍 쪽으로 내려가며 얇아진다 (입 안 벽 밖으로 나가지 않게).
        L.thick = z => 3.2f * Mathf.Lerp(.4f, 1f, Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - zTip) / 2.5f)))
                             * (1f - .45f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - zSlope0) / (zBase - zSlope0))));

        // 고리 단면을 길이 방향으로 이어 붙인 닫힌 혀 메시
        const int around = 36;
        var rings = new List<float>();
        for (float z = zTip + .03f; z < zBase; z += .15f) rings.Add(z);
        rings.Add(zBase);
        var verts = new List<Vector3>(); var tris = new List<int>();
        foreach (float z in rings)
        {
            float w = Mathf.Max(.02f, L.hw(z)), top = L.top(z), bottom = top - L.thick(z);
            float crownDrop = Mathf.Min(.4f, .15f * w);
            for (int j = 0; j < around; j++)
            {
                float a = j * 2 * Mathf.PI / around, c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = w * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / 3f);
                float yr = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / 3f);
                float u = x / w, topAt = top - crownDrop * u * u;
                float y = Mathf.Lerp(bottom, topAt, (yr + 1) / 2);
                if (yr > .5f)
                {
                    float up = (yr - .5f) * 2;
                    if (z < zSlope0) y -= .06f * Mathf.Exp(-(x / .35f) * (x / .35f)) * up; // 가운데 고랑(정중구)
                    y += .022f * Mathf.Sin(x * 7.1f + z * 5.3f) * Mathf.Sin(z * 6.7f - x * 3.1f) * up; // 유두 돌기 결
                }
                verts.Add(new Vector3(x, y, z));
            }
        }
        for (int i = 0; i < rings.Count - 1; i++)
            for (int j = 0; j < around; j++)
            {
                int a = i * around + j, b = i * around + (j + 1) % around, c = (i + 1) * around + j, d = (i + 1) * around + (j + 1) % around;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        int tip = verts.Count; verts.Add(new Vector3(0, L.top(zTip) - L.thick(zTip) * .5f, zTip - .02f));
        for (int j = 0; j < around; j++) tris.AddRange(new[] { tip, j, (j + 1) % around });
        int end = verts.Count, lastRing = (rings.Count - 1) * around; verts.Add(new Vector3(0, L.top(zBase) - L.thick(zBase) * .5f, zBase));
        for (int j = 0; j < around; j++) tris.AddRange(new[] { end, lastRing + (j + 1) % around, lastRing + j });
        var mesh = MakeMesh("혀_해부형", verts, tris, faceUp: true, probe: rings.Count / 3 * around + around / 4);
        SaveMesh(Folder + "/혀_해부형.asset", ref mesh);

        // 혀 회전축: 혀끝 쪽. 삼킬 때 뒤쪽이 들려 음식을 목구멍으로 민다.
        var pivot = Need("TonguePivot").transform;
        pivot.SetPositionAndRotation(new Vector3(0, L.top(zTip + .6f) - L.thick(zTip + .6f) * .5f, zTip + .6f), Quaternion.identity);
        var tongue = Replace("Part_Tongue", pivot);
        tongue.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tongue.transform.localScale = Vector3.one;
        Renderable(tongue, mesh, collider: true);
        if (tongue.GetComponent<TeleportArea>() == null) tongue.AddComponent<TeleportArea>();
        var walk = GameObject.Find("TongueWalkSurface"); if (walk != null) Object.DestroyImmediate(walk);

        // 가장자리 보호벽: 키보드 이동 시 혀 옆으로 떨어지지 않게 혀 가장자리를 따라 0.45m. 화면에 보이지 않는다.
        var rim = new List<Vector3>();
        for (float z = zSlope0 + .8f; z > zTip + .15f; z -= .2f) rim.Add(new Vector3(-(L.hw(z) - .08f), L.top(z) - Mathf.Min(.4f, .15f * L.hw(z)), z));
        for (float z = zTip + .15f; z <= zSlope0 + .8f; z += .2f) rim.Add(new Vector3(L.hw(z) - .08f, L.top(z) - Mathf.Min(.4f, .15f * L.hw(z)), z));
        var gv = new List<Vector3>(); var gt = new List<int>();
        for (int i = 0; i < rim.Count; i++)
        {
            var p = rim[i]; gv.Add(p - Vector3.up * .1f); gv.Add(p + Vector3.up * .45f);
            int n = (i + 1) % rim.Count, a = i * 2, b = n * 2;
            gt.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a, b, a + 1, b, b + 1, a + 1 });
        }
        var guardMesh = new Mesh { name = "혀 가장자리 보호벽" }; guardMesh.SetVertices(gv); guardMesh.SetTriangles(gt, 0); guardMesh.RecalculateBounds();
        SaveMesh(Folder + "/혀_가장자리_보호벽.asset", ref guardMesh);
        var guard = GameObject.Find("혀_가장자리_보호벽(보이지 않음)") ?? new GameObject("혀_가장자리_보호벽(보이지 않음)");
        guard.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var gc = Comp<MeshCollider>(guard); gc.sharedMesh = null; gc.sharedMesh = guardMesh;

        L.notes.Add($"배치: 혀 길이 {zBase - zTip:F1}m, 최대 폭 {2 * hwLast:F1}m, 윗면 높이 {topOral:F2} (아랫어금니 씹는 면 {TongueBelowOcclusal}m 아래), 걷는 바닥 = 혀 모양 그대로");
    }

    // ───────────────────────────── 입천장 ─────────────────────────────

    static void BuildPalate(Layout L, List<Vector3> uv)
    {
        // 윗니 모형의 잇몸은 속이 빈 껍데기라 잘린 윗단 안쪽이 보인다. 그래서 입천장은
        // ① 잇몸 윗단 전체(안쪽 끝~바깥 끝)를 덮는 평평한 덮개와 ② 안쪽 끝에서 솟는 둥근 지붕을 한 면으로 만든다.
        const float step = .25f;
        float clip = OcclusalY + GumClip;
        var rowsZ = new List<float>(); var rowIn = new List<float>(); var rowOut = new List<float>(); var rowY = new List<float>(); var rowLin = new List<float>();
        float zMin = uv.Min(v => v.z), zMax = uv.Max(v => v.z);
        for (float z = zMin + .1f; z <= zMax; z += step)
        {
            var slice = uv.Where(v => Mathf.Abs(v.z - z) < .25f).ToList();
            var gumBand = slice.Where(v => v.y > OcclusalY + 1f).ToList();      // 치관 위 잇몸 높이
            if (slice.Count < 10 || gumBand.Count == 0) continue;
            float yj = Mathf.Min(clip, slice.Max(v => v.y)) + .02f;           // 잇몸 윗단 바로 위 덮개 높이
            float outerX = gumBand.Max(v => Mathf.Abs(v.x));
            bool front = gumBand.Any(v => Mathf.Abs(v.x) < .5f);             // 앞니 쪽: 잇몸이 가운데를 가로지름
            float innerX = front ? 0f : Mathf.Min(gumBand.Where(v => v.x < -.05f).Select(v => -v.x).DefaultIfEmpty(outerX).Min(),
                                                  gumBand.Where(v => v.x > .05f).Select(v => v.x).DefaultIfEmpty(outerX).Min());
            // 안쪽 잇몸 벽 꼭대기: 바깥 벽보다 낮으면 그 사이로 빈 속이 보이므로 여기서 덮개까지 막을 세운다.
            float yLin = front ? yj : Mathf.Min(yj, gumBand.Where(v => Mathf.Abs(v.x) < innerX + .6f && v.y < clip - .05f).Select(v => v.y).DefaultIfEmpty(yj).Max() + .02f); // 눌린 윗단 제외
            rowsZ.Add(z); rowIn.Add(innerX); rowOut.Add(outerX + .08f); rowY.Add(yj); rowLin.Add(yLin);
        }
        if (rowsZ.Count < 8) throw new InvalidOperationException("윗니 잇몸 윗단을 찾지 못함");
        // 지붕 시작(안쪽 끝)은 이웃 중 작은 값, 덮개 바깥 끝은 큰 값 → 어느 쪽도 잇몸 안쪽으로 물러나 틈이 생기지 않게.
        var inner = rowIn.Select((x, i) => Enumerable.Range(i - 1, 3).Where(j => j >= 0 && j < rowIn.Count).Min(j => rowIn[j])).ToList();
        var outerW = rowOut.Select((x, i) => Enumerable.Range(i - 1, 3).Where(j => j >= 0 && j < rowOut.Count).Max(j => rowOut[j])).ToList();
        var yEdge = rowY.Select((x, i) => Enumerable.Range(i - 2, 5).Where(j => j >= 0 && j < rowY.Count).Average(j => rowY[j])).ToList();
        var yLinS = rowLin.Select((x, i) => Enumerable.Range(i - 1, 3).Where(j => j >= 0 && j < rowLin.Count).Min(j => rowLin[j])).ToList();
        int firstOpen = Mathf.Max(0, inner.FindIndex(x => x > .5f));
        float zOpen = rowsZ[firstOpen], zHardBack = rowsZ.Last();
        float yApex = OcclusalY + 3.5f; // 단단한 입천장 꼭대기: 씹는 면 위 약 1.75cm

        // 물렁입천장: 단단한 입천장 뒤 3.2m, 가운데는 비스듬히 내려가고 양옆은 혀 옆까지 내려가 목구멍 문(구개궁)을 만든다.
        const float softLength = 3.2f;
        float outBack = outerW.Last(), inBack = inner.Last(), yBack = yLinS.Last();
        var allZ = new List<float>(rowsZ);
        for (float z = zHardBack + step; z <= zHardBack + softLength + .01f; z += step) allZ.Add(z);
        const int cols = PalateCols;
        var verts = new List<Vector3>();
        for (int r = 0; r < allZ.Count; r++)
        {
            // 단면: 가운데 둥근 지붕(안쪽 잇몸 벽 꼭대기 yLin 에서 시작) → 짧은 막(yLin→덮개) → 바깥 끝까지 평평한 덮개(yCap)
            float z = allZ[r], hwIn, hwOut, yLin, yCap, ym;
            if (r < rowsZ.Count)
            {
                hwIn = inner[r]; hwOut = outerW[r]; yCap = yEdge[r]; yLin = Mathf.Min(yLinS[r], yCap);
                ym = yLin + (yApex - yLin) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - zOpen) / 3.2f));
            }
            else
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - zHardBack) / softLength));
                hwOut = Mathf.Lerp(outBack, inBack + .3f, t); hwIn = Mathf.Lerp(inBack, hwOut, t);
                yLin = yCap = yBack - 4.6f * t; ym = yApex - 2.3f * t;
            }
            for (int c = 0; c <= cols; c++)
            {
                float u = -1 + 2f * c / cols, x = u * hwOut, ax = Mathf.Abs(x);
                float y;
                if (hwIn < .05f) y = yCap;
                else if (ax <= hwIn) y = yLin + (ym - yLin) * (1 - Mathf.Pow(ax / hwIn, 2.4f));
                else if (ax < hwIn + .25f) y = Mathf.Lerp(yLin, yCap, (ax - hwIn) / .25f);
                else y = yCap;
                if (r < rowsZ.Count && ax < hwIn)
                {
                    float uIn = ax / hwIn;
                    y -= .03f * Mathf.Exp(-(x / .3f) * (x / .3f)); // 정중 솔기
                    float frontFade = Mathf.Clamp01(1 - (z - zOpen) / (.45f * Mathf.Max(1f, zHardBack - zOpen)));
                    y -= .09f * Mathf.Max(0, Mathf.Sin((z - zOpen) * 2 * Mathf.PI / .85f + .8f * ax)) * Mathf.Sqrt(1 - uIn) * frontFade; // 가로 주름(구개 주름)
                    y -= .12f * Mathf.Exp(-((x / .35f) * (x / .35f) + ((z - zOpen - .5f) / .4f) * ((z - zOpen - .5f) / .4f))); // 앞니 뒤 작은 돌기(절치 유두)
                }
                verts.Add(new Vector3(x, y, z));
            }
        }
        // 덮개 가장자리(양옆 + 앞니 쪽 첫 줄)를 가장 가까운 잇몸 윗단 꼭짓점에 붙이고 바깥으로 0.1m 더 내밀어 잘린 단면을 덮는다.
        float zc = (zOpen + zHardBack) / 2;
        var gum = uv.Where((v, i) => i % 2 == 0 && v.y > clip - .6f).ToArray();
        void Snap(int index, int inside)
        {
            var p = verts[index];
            Vector3 best = p; float bestD = float.MaxValue;
            foreach (var g in gum) { if (Mathf.Abs(g.y - p.y) > .6f) continue; float d = (g - p).sqrMagnitude; if (d < bestD) { bestD = d; best = g; } }
            if (bestD == float.MaxValue || bestD < .08f * .08f) return;
            var outward = new Vector3(best.x, 0, best.z - zc).normalized;
            var moved = new Vector3(best.x, Mathf.Max(best.y, p.y), best.z) + outward * .1f;
            verts[index] = moved;
            if (inside >= 0) verts[inside] = Vector3.Lerp(verts[inside], moved, .35f);
        }
        for (int r = 0; r < rowsZ.Count; r++)
        {
            Snap(r * (cols + 1), r * (cols + 1) + 1);
            Snap(r * (cols + 1) + cols, r * (cols + 1) + cols - 1);
        }
        for (int c = 1; c < cols; c++) Snap(c, cols + 1 + c);
        var tris = new List<int>();
        for (int r = 0; r < allZ.Count - 1; r++)
            for (int c = 0; c < cols; c++)
            {
                int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
                tris.AddRange(new[] { a, b, d, b, e, d });
            }
        int probeRow = Mathf.Clamp(firstOpen + (rowsZ.Count - firstOpen) / 2, 0, allZ.Count - 1);
        var mesh = MakeMesh("입천장_잇몸연결형", verts, tris, faceUp: false, probe: probeRow * (cols + 1) + cols / 2);
        SaveMesh(Folder + "/입천장_잇몸연결형.asset", ref mesh);
        var palate = Replace("Part_Palate", null);
        palate.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); palate.transform.localScale = Vector3.one;
        Renderable(palate, mesh, collider: true);
        float zEnd = allZ.Last();
        L.freeEdge = new Vector3(0, yApex - 2.3f, zEnd);
        L.hardBack = zHardBack;
        L.notes.Add($"배치: 입천장 = 잇몸 윗단 덮개 + 둥근 지붕(꼭대기 씹는 면 위 {yApex - OcclusalY:F2}m), 단단한 입천장 끝 z={zHardBack:F1}, 목젖 매달린 곳 {Fmt(L.freeEdge)}");
    }

    // ───────────────────────────── 목젖 · 후두덮개 ─────────────────────────────

    static void PlaceUvulaAndEpiglottis(Layout L)
    {
        var uvula = Need("Part_Uvula"); var uvulaPivot = Need("UvulaPivot").transform;
        uvulaPivot.SetPositionAndRotation(L.freeEdge + Vector3.back * .15f, Quaternion.identity);
        uvula.transform.SetParent(uvulaPivot, true);
        uvula.transform.localRotation = Quaternion.identity;
        uvula.transform.localScale = Vector3.one; float h = Bounds(uvula).size.y; uvula.transform.localScale = Vector3.one * (2.4f / Mathf.Max(.01f, h));
        var b = Bounds(uvula);
        uvula.transform.position += new Vector3(uvulaPivot.position.x - b.center.x, uvulaPivot.position.y + .25f - b.max.y, uvulaPivot.position.z - b.center.z);
        float uvulaTip = Bounds(uvula).min.y;

        var epi = Need("Part_Epiglottis"); var epiPivot = Need("EpiglottisPivot").transform;
        float topY = uvulaTip - .8f, z = L.zBase + .8f;
        var eb = Bounds(epi); float eh = eb.size.y;
        epiPivot.SetPositionAndRotation(new Vector3(0, topY - eh, z), Quaternion.identity);
        epi.transform.SetParent(epiPivot, true);
        eb = Bounds(epi);
        epi.transform.position += new Vector3(-eb.center.x, epiPivot.position.y - eb.min.y, z - eb.center.z);
        L.throat = new Vector3(0, (uvulaTip + L.top(L.zBase)) / 2, L.zBase + .15f);
        RepositionLabel(uvula, 0, new Vector3(0, uvulaTip - .45f, uvulaPivot.position.z)); // 위쪽은 물렁입천장 속이라 끝 아래에
        RepositionLabel(epi, .3f);
        L.notes.Add($"배치: 목젖 끝 높이 {uvulaTip:F2}, 후두덮개 끝 {topY:F2} (혀뿌리 뒤 z={z:F1})");
    }

    // ───────────────────────────── 음식 · 진행 ─────────────────────────────

    static void PlaceFoodAndFlow(Layout L, ChewingSequence chew, SwallowSequence swallow, Transform chewPoint)
    {
        L.spawnZ = L.zTip + 1.3f; L.gatherZ = L.spawnZ + 3f;
        Physics.SyncTransforms();
        var spawn = Need("SpawnPoint").transform;
        spawn.SetPositionAndRotation(new Vector3(0, TongueSurface(L, 0, L.spawnZ) + .02f, L.spawnZ), Quaternion.identity);

        var gather = GameObject.Find("음식_모으는_혀_자리") ?? new GameObject("음식_모으는_혀_자리");
        gather.transform.SetPositionAndRotation(new Vector3(0, TongueSurface(L, 0, L.gatherZ), L.gatherZ), Quaternion.identity);
        var bolus = Need("Prop_FoodBolus");
        var b = Bounds(bolus);
        bolus.transform.position += gather.transform.position - new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * .02f;
        RepositionLabel(bolus, .4f);

        var throat = Need("ThroatPoint").transform; throat.position = L.throat;
        if (chew != null)
        {
            chew.chewPoint = chewPoint; chew.gatherPoint = gather.transform;
            chew.jawOpenAngle = L.thetaRest - L.thetaBite; chew.grindAngle = GrindAngle;
            EditorUtility.SetDirty(chew);
        }
        if (swallow != null)
        {
            swallow.tongueLiftAngle = -6f;   // 혀끝 축 → 혀 뒤쪽이 약 1.3m 들림
            swallow.uvulaLiftAngle = -40f;   // 목젖이 뒤·위로 올라가 콧길을 막음
            swallow.epiglottisCloseAngle = 70f; // 후두덮개 끝이 뒤·아래로 접혀 기관 입구를 덮음
            EditorUtility.SetDirty(swallow);
        }
        var zone = GameObject.Find("ChewZone");
        if (zone != null)
        {
            zone.transform.position = new Vector3(0, L.top(L.gatherZ) + 1.5f, L.gatherZ - .4f);
            var box = zone.GetComponent<BoxCollider>(); box.center = Vector3.zero; box.size = new Vector3(2 * L.hw(L.gatherZ), 3f, 3.2f);
        }
        RepositionLabel(Need("Part_Tongue"), .5f, new Vector3(0, L.top(L.spawnZ + 2.2f), L.spawnZ + 2.2f));
    }

    // ───────────────────────────── 침 나오는 구멍 ─────────────────────────────

    static void PlaceSalivaOpenings(Layout L, List<Vector3> uv, GameObject lower)
    {
        var emitters = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("Saliva_")).OrderBy(p => p.name).ToArray();
        if (emitters.Length != 6) throw new InvalidOperationException("침 방출기 6개를 찾지 못함: " + emitters.Length);
        var root = (GameObject.Find("SalivaryGlands") ?? new GameObject("SalivaryGlands")).transform;
        var oldLabels = root.GetComponentsInChildren<PartLabel>(true).Where(p => p.partId == "SalivaryGland" && p.name.StartsWith("Prop_SalivaGland")).ToList();
        float uBackZ = uv.Max(v => v.z), uOuter = uv.Max(v => Mathf.Abs(v.x));
        float Cheek(float y, float z) => DomeR.x * Mathf.Sqrt(Mathf.Max(0, 1 - Sq((y - DomeC.y) / DomeR.y) - Sq((z - DomeC.z) / DomeR.z)));
        float zParotid = uBackZ - 1.6f, yParotid = OcclusalY + 1.2f;
        float zSub = L.zTip + 2f;
        // 턱밑샘관 구멍은 혀끝과 아랫앞니 안쪽면 사이 바닥에 둔다.
        float yCar = L.top(L.zTip) - .6f;
        float zLing = WorldVerts(lower).Where(v => Mathf.Abs(v.x) < .9f && Mathf.Abs(v.y - yCar) < .35f && v.z < L.zTip + .5f).Select(v => v.z).DefaultIfEmpty(L.zTip - .3f).Max();
        float zCar = Mathf.Max(zLing + .12f, L.zTip - .05f);
        var spots = new (string name, Vector3 pos, Vector3 normal, Vector3 aim, float size)[6];
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1 : 1;
            // 귀밑샘관 구멍: 위 둘째 큰어금니 맞은편 볼 안쪽
            var p0 = new Vector3(side * (Cheek(yParotid, zParotid) - .12f), yParotid, zParotid);
            spots[s] = ("귀밑샘관_구멍", p0, new Vector3(-side, 0, 0), new Vector3(side * (uOuter - .4f), OcclusalY + .3f, zParotid), .38f);
            // 턱밑샘관 구멍(혀밑 언덕): 혀끝 앞 바닥, 혀주름띠 양옆
            var p1 = new Vector3(side * .45f, yCar, zCar);
            spots[2 + s] = ("턱밑샘관_구멍", p1, Vector3.up, p1 + new Vector3(side * .1f, 1f, .5f), .26f);
            // 혀밑샘관 구멍: 혀 옆면과 아랫니 사이 틈 아래쪽(혀밑 주름). 틈(0.2m)보다 작게 해 잇몸에 묻히지 않게.
            var p2 = new Vector3(side * (L.hw(zSub) + .06f), L.top(zSub) - .9f, zSub);
            spots[4 + s] = ("혀밑샘관_구멍", p2, Vector3.up, p2 + new Vector3(0, 1f, .3f), .18f);
        }
        var tissue = JourneyEnvironmentRepair.Tissue("Mouth");
        for (int i = 0; i < 6; i++)
        {
            // 구멍 자리(크기 1): 이름표·시선 충돌·침 방출기를 담는다. 납작한 돌기 모양은 자식으로 따로 둬 방출 모양이 찌그러지지 않게.
            string name = spots[i].name + (i % 2 == 0 ? "_왼쪽" : "_오른쪽");
            var anchor = Child(root, name);
            anchor.SetPositionAndRotation(spots[i].pos, Quaternion.FromToRotation(Vector3.up, spots[i].normal));
            anchor.localScale = Vector3.one;
            var bump = anchor.Find("돌기");
            if (bump == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = "돌기";
                Object.DestroyImmediate(g.GetComponent<Collider>());
                bump = g.transform; bump.SetParent(anchor, false);
            }
            bump.localPosition = Vector3.zero; bump.localRotation = Quaternion.identity;
            bump.localScale = new Vector3(spots[i].size, spots[i].size * .42f, spots[i].size);
            var r = bump.GetComponent<MeshRenderer>(); r.sharedMaterial = tissue; r.shadowCastingMode = ShadowCastingMode.Off;
            var sc = Comp<SphereCollider>(anchor.gameObject); sc.isTrigger = true; sc.radius = .35f; sc.center = Vector3.zero;
            var em = emitters[i];
            em.transform.SetParent(anchor, true);
            em.transform.position = spots[i].pos + spots[i].normal * .08f;
            em.transform.rotation = Quaternion.LookRotation((spots[i].aim - spots[i].pos).normalized);
            em.transform.localScale = Vector3.one;
            // 이름표: 쌍마다 하나 (귀밑·턱밑·혀밑)
            if (i % 2 == 0 && anchor.GetComponent<PartLabel>() == null && oldLabels.Count > 0)
            {
                var src = oldLabels[Mathf.Min(i / 2, oldLabels.Count - 1)];
                var label = anchor.gameObject.AddComponent<PartLabel>(); EditorUtility.CopySerialized(src, label);
                if (src.labelRoot != null) src.labelRoot.transform.SetParent(anchor, true);
            }
            if (anchor.GetComponent<PartLabel>() is PartLabel pl && pl.labelRoot != null)
            {
                pl.labelRoot.transform.position = spots[i].pos + Vector3.up * .55f;
                pl.labelRoot.transform.rotation = Quaternion.identity;
                SetWorldScale(pl.labelRoot.transform, .003f);
            }
        }
        // 볼·턱 밑 살 속에 있어 입 안에서는 보이지 않는 침샘 모형은 지운다.
        foreach (Transform child in root.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Prop_SalivaGland")) Object.DestroyImmediate(child.gameObject);
        L.notes.Add("배치: 침샘 모형 6개 제거 → 침 나오는 구멍 6곳(귀밑샘관·턱밑샘관·혀밑샘관 좌우)에서 분비");
    }

    // ───────────────────────────── 검사 ─────────────────────────────

    public static List<string> Verify()
    {
        var r = new List<string>(); var L = last;
        void Check(bool ok, string text) => r.Add((ok ? "통과: " : "실패: ") + text);
        Physics.SyncTransforms();
        var chew = Object.FindFirstObjectByType<ChewingSequence>();
        var jaw = chew.upperJaw; var rest = jaw.localRotation;
        var upper = Need("Part_UpperTeethArch"); var lower = Need("Part_LowerTeethArch");
        var uv = WorldVerts(upper);
        var M = chew.chewPoint.position;
        float gapRest = GapAt(uv, Quaternion.Inverse(Quaternion.AngleAxis(L.thetaRest, Vector3.right)) * (M - L.pivot) + L.pivot, L.thetaRest);
        float gapBite = GapAt(uv, Quaternion.Inverse(Quaternion.AngleAxis(L.thetaRest, Vector3.right)) * (M - L.pivot) + L.pivot, L.thetaRest - chew.jawOpenAngle);
        Check(Mathf.Abs(gapRest - RestGap) < .08f, $"평소 자세 어금니 틈 {gapRest:F2}m (목표 {RestGap})");
        Check(Mathf.Abs(gapBite - BiteGap) < .08f, $"음식 무는 순간 어금니 틈 {gapBite:F2}m (목표 {BiteGap}) — 위아래 이가 실제로 만남");
        jaw.localRotation = rest * Quaternion.AngleAxis(-chew.jawOpenAngle, Vector3.right);
        float travel = Vector3.Distance(chew.chewPoint.position, M);
        jaw.localRotation = rest;
        Check(travel > .5f, $"씹는 어금니 이동 거리 {travel:F2}m (앞니만 움직이지 않음)");
        Check(L.pivot.z - M.z > 4f && L.pivot.y - M.y > 3f, $"턱관절 축이 어금니 뒤 {L.pivot.z - M.z:F1}m, 위 {L.pivot.y - M.y:F1}m");
        Check(Vector3.Dot(upper.transform.up, lower.transform.up) < -.9f && Vector3.Dot(upper.transform.forward, lower.transform.forward) > .9f, "위아래 치관이 서로 마주봄, 앞니 방향 일치");

        // 혀와 아랫니가 씹는 동안 겹치지 않음 (벌림·무는 순간·옆 비킴)
        int hits = 0;
        foreach (var q in new[] { rest, rest * Quaternion.AngleAxis(-chew.jawOpenAngle, Vector3.right),
                                  rest * Quaternion.AngleAxis(-chew.grindAngle, Vector3.up) * Quaternion.AngleAxis(-chew.jawOpenAngle * .707f, Vector3.right),
                                  rest * Quaternion.AngleAxis(chew.grindAngle, Vector3.up) * Quaternion.AngleAxis(-chew.jawOpenAngle * .707f, Vector3.right) })
        {
            jaw.localRotation = q;
            foreach (var v in WorldVerts(lower))
                if (v.z > L.zTip && v.z < L.zBase && Mathf.Abs(v.x) < L.hw(v.z) - .02f && v.y < L.top(v.z) - .05f && v.y > L.top(v.z) - L.thick(v.z)) hits++;
        }
        jaw.localRotation = rest;
        Check(hits == 0, $"씹는 동안 아랫니가 혀를 파고드는 점 {hits}개");

        // 입천장이 잇몸에 붙음 (가장자리 꼭짓점과 가장 가까운 윗니 꼭짓점 거리)
        var palate = Need("Part_Palate").GetComponent<MeshFilter>().sharedMesh;
        var pv = palate.vertices; int cols = PalateCols + 1, rows = pv.Length / cols;
        float hardBack = L.hardBack, worst = 0f;
        var uvSample = uv.Where((v, i) => i % 2 == 0).ToArray();
        for (int row = 0; row < rows; row++)
            foreach (int c in row == 0 ? Enumerable.Range(0, cols) : new[] { 0, cols - 1 }) // 첫 줄은 앞니 안쪽에 붙는 앞 가장자리 전체
            {
                var p = pv[row * cols + c]; if (p.z > hardBack) continue;
                float d = uvSample.Min(u => (u - p).sqrMagnitude); worst = Mathf.Max(worst, Mathf.Sqrt(d));
            }
        Check(worst < .5f, $"입천장 가장자리와 윗잇몸 사이 최대 거리 {worst:F2}m (틈 없음)");
        var eye = Need("SpawnPoint").transform.position + Vector3.up * 1.6f;
        float clearance = pv.Where(p => Mathf.Abs(p.x) < 1.5f && p.z > L.spawnZ - 1 && p.z < L.gatherZ + 2).Select(p => p.y).DefaultIfEmpty(99).Min() - eye.y;
        Check(clearance > .4f, $"탐험자 눈높이 위 입천장 여유 {clearance:F2}m");

        var uvula = Need("Part_Uvula");
        Check(Mathf.Abs(Bounds(uvula).max.y - (L.freeEdge.y + .25f)) < .1f && Mathf.Abs(Bounds(uvula).center.z - L.freeEdge.z) < .5f, "목젖이 물렁입천장 끝 가운데에 매달림");
        Check(Bounds(uvula).min.y - L.top(Bounds(uvula).center.z) > .3f, $"목젖 끝과 혀뿌리 사이 틈 {Bounds(uvula).min.y - L.top(Bounds(uvula).center.z):F2}m");

        // 걷는 바닥 = 혀
        foreach (var (name, z) in new[] { ("시작 지점", L.spawnZ), ("음식 자리", L.gatherZ), ("혀 옆쪽", L.spawnZ + 2) })
        {
            float x = name == "혀 옆쪽" ? L.hw(z) * .7f : 0f;
            // 입천장 아래(눈높이)에서 아래로 쏘고, 위에 놓인 음식은 건너뛴다
            var down = Physics.RaycastAll(new Vector3(x, L.top(z) + 1.6f, z), Vector3.down, 6, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(chew.foodBolus)).OrderBy(h => h.distance).ToArray();
            bool ok = down.Length > 0; var hit = ok ? down[0] : default;
            Check(ok && hit.collider.name == "Part_Tongue" && hit.collider.GetComponentInParent<TeleportArea>() != null, $"{name} 아래 바닥이 혀 표면 (맞은 것: {(ok ? hit.collider.name : "없음")}, 높이 {(ok ? hit.point.y : 0):F2})");
        }
        Check(GameObject.Find("TongueWalkSurface") == null, "혀와 모양이 다른 평평한 걷는 상자 제거");
        var spawn = Need("SpawnPoint").transform.position;
        Check(Physics.Raycast(spawn + Vector3.up, Vector3.down, out var sh, 3, ~0, QueryTriggerInteraction.Ignore) && sh.collider.name == "Part_Tongue" && spawn.y - sh.point.y < .1f, $"시작 지점 발이 혀에 닿음 (틈 {spawn.y - sh.point.y:F2}m)");

        // 음식
        var bolus = chew.foodBolus.gameObject; var bb = Bounds(bolus);
        Check(Mathf.Abs(bb.min.y - L.top(bb.center.z)) < .15f, $"음식이 혀 위에 놓임 (틈 {bb.min.y - L.top(bb.center.z):F2}m)");
        Check(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).All(t => !t.name.StartsWith("Prop_SalivaGland")), "입 안에 떠 있던 침샘 모형 없음");
        Check(chew.salivaEmitters.Length == 6 && chew.salivaEmitters.All(p => p != null && p.transform.parent != null && p.transform.parent.name.Contains("샘관_구멍")), "침 방출기 6개가 침 나오는 구멍에 연결");

        // 삼킴 동작 방향
        var swallow = Object.FindFirstObjectByType<SwallowSequence>();
        var tongue = swallow.tongue; var t0 = tongue.localRotation;
        var baseTop = new Vector3(0, L.top(L.zBase - 1.5f), L.zBase - 1.5f); var spawnTop = new Vector3(0, L.top(L.spawnZ), L.spawnZ);
        var rot = Quaternion.AngleAxis(swallow.tongueLiftAngle, Vector3.right);
        float baseRise = (tongue.position + rot * (baseTop - tongue.position)).y - baseTop.y, spawnRise = (tongue.position + rot * (spawnTop - tongue.position)).y - spawnTop.y;
        Check(baseRise > .8f && Mathf.Abs(spawnRise) < .3f, $"삼킬 때 혀 뒤쪽 {baseRise:F2}m 들림, 탐험자 발밑 {spawnRise:F2}m");
        var uvTip = Bounds(uvula).min; var up = swallow.uvula.position;
        var uvMoved = up + Quaternion.AngleAxis(swallow.uvulaLiftAngle, Vector3.right) * (uvTip - up);
        Check(uvMoved.z > uvTip.z + .5f && uvMoved.y > uvTip.y, $"목젖이 뒤({uvMoved.z - uvTip.z:F2}m)·위({uvMoved.y - uvTip.y:F2}m)로 올라감");
        var epiTop = Bounds(Need("Part_Epiglottis")).max; var ep = swallow.epiglottis.position;
        var epMoved = ep + Quaternion.AngleAxis(swallow.epiglottisCloseAngle, Vector3.right) * (new Vector3(0, epiTop.y, ep.z) - ep);
        Check(epMoved.z > ep.z + .5f && epMoved.y < epiTop.y, "후두덮개 끝이 뒤·아래로 접혀 기관 입구를 덮음");

        // 모두 입 안 벽(볼·입술·목구멍) 안쪽
        var dome = GameObject.Find("MouthDome").transform; var R = dome.lossyScale / 2;
        float outside = 0;
        foreach (var go in new[] { upper, lower, Need("Part_Tongue"), Need("Part_Palate"), uvula, Need("Part_Epiglottis") })
            foreach (var v in WorldVerts(go))
            {
                var d = v - dome.position; float k = Sq(d.x / R.x) + Sq(d.y / R.y) + Sq(d.z / R.z);
                outside = Mathf.Max(outside, k);
            }
        Check(outside < 1f, $"모든 부위가 입 안 벽 안쪽 (최대 {Mathf.Sqrt(outside):F2})");
        return r;
    }

    // ───────────────────────────── 촬영 ─────────────────────────────

    static void Capture()
    {
        Directory.CreateDirectory(CaptureDir);
        var L = last;
        var eye = Need("SpawnPoint").transform.position + Vector3.up * 1.6f;
        Shoot("1_탐험자_앞(목구멍쪽)", eye, eye + Vector3.forward * 8 + Vector3.down * 1.2f, 75);
        Shoot("2_탐험자_왼쪽(씹는쪽)", eye, eye + new Vector3(-8, -1.6f, 2), 75);
        Shoot("3_탐험자_위(입천장)", eye, eye + new Vector3(0, 8, 2), 80);
        Shoot("4_탐험자_뒤(앞니쪽)", eye + Vector3.forward * 2, eye + Vector3.back * 8 + Vector3.down, 75);
        // 바깥에서 보는 단면: 입 안 벽과 안개를 잠시 끈다.
        var dome = GameObject.Find("MouthDome").GetComponent<Renderer>(); dome.enabled = false;
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        var center = new Vector3(0, OcclusalY + 1, 4);
        Shoot("5_바깥_옆(단면)", center + new Vector3(24, 3, 0), center, 50);
        Shoot("6_바깥_앞", center + new Vector3(0, 2, -26), center, 50);
        var hidden = Need("Part_Tongue").GetComponentsInChildren<Renderer>().Concat(Need("Part_LowerTeethArch").GetComponentsInChildren<Renderer>()).Where(x => x.enabled).ToList();
        foreach (var x in hidden) x.enabled = false;
        Shoot("7_아래에서_본_윗니·잇몸·입천장(혀·아랫니_숨김)", center + new Vector3(0, -13, 2), center + new Vector3(0, 3, 2.5f), 70);
        foreach (var x in hidden) x.enabled = true;
        var chew = Object.FindFirstObjectByType<ChewingSequence>(); var jaw = chew.upperJaw; var rest = jaw.localRotation;
        var bolus = chew.foodBolus; var bp = bolus.position; var bs = bolus.localScale;
        var bb = Bounds(bolus.gameObject); var rootOffset = bp - new Vector3(bb.center.x, bb.min.y, bb.center.z);
        bolus.position = chew.chewPoint.position + rootOffset;
        Shoot("9_씹기_벌린자세_옆단면", chew.chewPoint.position + new Vector3(-9, 1.5f, -1), chew.chewPoint.position + Vector3.up * .5f, 45);
        jaw.localRotation = rest * Quaternion.AngleAxis(-chew.jawOpenAngle, Vector3.right);
        bolus.localScale = Vector3.Scale(bs, new Vector3(1.15f, .8f, 1));
        bolus.position = chew.chewPoint.position + Vector3.Scale(rootOffset, new Vector3(1.15f, .8f, 1));
        Shoot("10_씹기_무는순간_옆단면", chew.chewPoint.position + new Vector3(-9, 1.5f, -1), chew.chewPoint.position + Vector3.up * .5f, 45);
        // 탐험자 시점은 입 안 벽과 안개를 켜고
        dome.enabled = true; RenderSettings.fog = fog;
        Shoot("11_씹기_무는순간(탐험자)", eye + new Vector3(-1, 0, 2), chew.chewPoint.position, 60);
        jaw.localRotation = rest; bolus.localScale = bs; bolus.position = chew.chewPoint.position + rootOffset;
        Shoot("8_씹기_벌린자세(음식이_어금니위,탐험자)", eye + new Vector3(-1, 0, 2), chew.chewPoint.position, 60);
        dome.enabled = false; RenderSettings.fog = false;
        jaw.localRotation = rest; bolus.localScale = bs; bolus.position = bp;
        var sw = Object.FindFirstObjectByType<SwallowSequence>();
        var tr = sw.tongue.localRotation; var ur = sw.uvula.localRotation; var er = sw.epiglottis.localRotation;
        sw.tongue.localRotation = tr * Quaternion.Euler(sw.tongueLiftAngle, 0, 0);
        sw.uvula.localRotation = ur * Quaternion.Euler(sw.uvulaLiftAngle, 0, 0);
        sw.epiglottis.localRotation = er * Quaternion.Euler(sw.epiglottisCloseAngle, 0, 0);
        Shoot("12_삼킴_자세_옆단면", center + new Vector3(24, 0, 4), center + new Vector3(0, -1, 6), 45);
        sw.tongue.localRotation = tr; sw.uvula.localRotation = ur; sw.epiglottis.localRotation = er;
        Shoot("13_평소_자세_옆단면(비교)", center + new Vector3(24, 0, 4), center + new Vector3(0, -1, 6), 45);
        dome.enabled = true; RenderSettings.fog = fog;
    }

    static void Shoot(string name, Vector3 eye, Vector3 target, float fov)
    {
        var go = new GameObject("촬영 카메라"); var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
        cam.fieldOfView = fov; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.11f, .15f, .2f); cam.nearClipPlane = .02f; cam.farClipPlane = 200;
        var rt = new RenderTexture(960, 720, 24); var tex = new Texture2D(960, 720, TextureFormat.RGB24, false); var old = RenderTexture.active;
        try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); tex.Apply(); File.WriteAllBytes(CaptureDir + "/" + name + ".png", tex.EncodeToPNG()); }
        finally { RenderTexture.active = old; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); }
    }

    // ───────────────────────────── 도우미 ─────────────────────────────

    /// <summary>실제 혀 표면 높이 (가운데 고랑·돌기 포함). 음식 등 다른 물체는 건너뛴다.</summary>
    static float TongueSurface(Layout L, float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, L.top(z) + 1.5f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        var tongue = hits.Where(h => h.collider.name == "Part_Tongue").OrderBy(h => h.distance).ToArray();
        return tongue.Length > 0 ? tongue[0].point.y : L.top(z);
    }

    static GameObject Need(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) throw new InvalidOperationException("입 장면에서 찾을 수 없음: " + name);
        return go;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        return t;
    }

    static float Sq(float x) => x * x;
    static string Fmt(Vector3 v) => $"({v.x:F1}, {v.y:F1}, {v.z:F1})";

    static List<Vector3> WorldVerts(GameObject go)
    {
        var list = new List<Vector3>();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null || !mf.GetComponent<MeshRenderer>().enabled) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices) list.Add(m.MultiplyPoint3x4(v));
        }
        return list;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer && r.enabled && !(r.GetComponentInParent<Canvas>() != null)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static Mesh MakeMesh(string name, List<Vector3> verts, List<int> tris, bool faceUp, int probe)
    {
        var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
        // 레이캐스트·충돌은 앞면 기준이므로 혀 윗면은 위, 입천장은 아래(입 안쪽)를 향하게 맞춘다.
        bool up = mesh.normals[Mathf.Clamp(probe, 0, verts.Count - 1)].y > 0;
        if (up != faceUp)
        {
            for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 모든 꼭짓점이 월드 높이 maxY 위인 삼각형을 지우고, 남은 삼각형 중 maxY 위로 튀어나온 꼭짓점은 maxY 로 눌러
    /// 잘린 단면이 톱니처럼 들쭉날쭉하지 않고 수평선이 되게 한 사본 (UV·법선 유지).
    /// </summary>
    static Mesh TrimAbove(Mesh src, Matrix4x4 toWorld, float maxY)
    {
        var v = src.vertices; var n = src.normals; var t = src.tangents; var c = src.colors;
        var toLocal = toWorld.inverse;
        var clamped = v.Select(p => { var w = toWorld.MultiplyPoint3x4(p); if (w.y <= maxY) return p; w.y = maxY; return toLocal.MultiplyPoint3x4(w); }).ToArray();
        var uv0 = new List<Vector2>(); src.GetUVs(0, uv0); var uv1 = new List<Vector2>(); src.GetUVs(1, uv1);
        var keepTri = new List<int>[src.subMeshCount];
        var map = new int[v.Length]; for (int i = 0; i < map.Length; i++) map[i] = -1;
        var order = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tri = src.GetTriangles(s); var kept = new List<int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                if (toWorld.MultiplyPoint3x4(v[tri[i]]).y > maxY && toWorld.MultiplyPoint3x4(v[tri[i + 1]]).y > maxY && toWorld.MultiplyPoint3x4(v[tri[i + 2]]).y > maxY) continue;
                for (int k = 0; k < 3; k++) { int o = tri[i + k]; if (map[o] < 0) { map[o] = order.Count; order.Add(o); } kept.Add(map[o]); }
            }
            keepTri[s] = kept;
        }
        var mesh = new Mesh { name = src.name + "_잇몸다듬음", indexFormat = order.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(order.Select(i => clamped[i]).ToList());
        if (n.Length == v.Length) mesh.SetNormals(order.Select(i => n[i]).ToList());
        if (t.Length == v.Length) mesh.SetTangents(order.Select(i => t[i]).ToList());
        if (c.Length == v.Length) mesh.SetColors(order.Select(i => c[i]).ToList());
        if (uv0.Count == v.Length) mesh.SetUVs(0, order.Select(i => uv0[i]).ToList());
        if (uv1.Count == v.Length) mesh.SetUVs(1, order.Select(i => uv1[i]).ToList());
        mesh.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(keepTri[s], s);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SaveMesh(string path, ref Mesh mesh)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, path);
        EditorUtility.SetDirty(mesh);
    }

    /// <summary>Meshy 모형을 코드로 만든 형상으로 바꾼다. 이름표(내용·이름판)는 그대로 옮긴다.</summary>
    static GameObject Replace(string name, Transform parent)
    {
        var old = GameObject.Find(name);
        if (old != null && !PrefabUtility.IsPartOfPrefabInstance(old) && old.GetComponent<MeshFilter>() != null)
        {
            if (parent != null && old.transform.parent != parent) old.transform.SetParent(parent, true);
            return old;
        }
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (old != null)
        {
            var label = old.GetComponent<PartLabel>();
            if (label != null)
            {
                var copy = go.AddComponent<PartLabel>(); EditorUtility.CopySerialized(label, copy);
                if (label.labelRoot != null) label.labelRoot.transform.SetParent(go.transform, true);
            }
            Object.DestroyImmediate(old);
        }
        return go;
    }

    static void Renderable(GameObject go, Mesh mesh, bool collider)
    {
        var mf = Comp<MeshFilter>(go); mf.sharedMesh = mesh;
        var mr = Comp<MeshRenderer>(go); mr.sharedMaterial = JourneyEnvironmentRepair.Tissue("Mouth");
        if (collider) { var mc = Comp<MeshCollider>(go); mc.sharedMesh = null; mc.sharedMesh = mesh; }
    }

    /// <summary>편집기의 GetComponent 는 없을 때 가짜 null 을 돌려줘 ?? 가 통하지 않으므로 명시적으로 비교한다.</summary>
    static T Comp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void SetWorldScale(Transform t, float world)
    {
        var p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(world / Mathf.Max(1e-4f, Mathf.Abs(p.x)), world / Mathf.Max(1e-4f, Mathf.Abs(p.y)), world / Mathf.Max(1e-4f, Mathf.Abs(p.z)));
    }

    /// <summary>부위가 옮겨진 뒤 이름판을 부위 위로 다시 올리고, 늘어나거나 눌리지 않게 월드 크기를 맞춘다.</summary>
    static void RepositionLabel(GameObject go, float height, Vector3? anchor = null)
    {
        var label = go.GetComponent<PartLabel>();
        if (label == null || label.labelRoot == null) return;
        var b = Bounds(go);
        var p = anchor ?? new Vector3(b.center.x, b.max.y, b.center.z);
        label.labelRoot.transform.position = p + Vector3.up * height;
        label.labelRoot.transform.rotation = Quaternion.identity;
        SetWorldScale(label.labelRoot.transform, .003f);
    }
}
#endif
