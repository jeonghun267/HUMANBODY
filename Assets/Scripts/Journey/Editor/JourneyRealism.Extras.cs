#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 더 '하는' 체험과 소리: 새 이 모형(Sketchfab), 직접 씹기·삼킴 타이밍, 효소 짝 맞추기, 유익균 먹이 주기, 기관별 배경음.
/// </summary>
public static partial class JourneyRealism
{
    // ───────────────────────────── 새 이 모형 ─────────────────────────────

    /// <summary>
    /// Sketchfab "Human teeth"(Dessen, CC BY 4.0)로 Meshy 치열을 바꾼다. 위아래 32개 이가 맞물린 모형(잇몸에 입천장·입바닥 판이 붙음)이다.
    /// 씹는 면이 기울어 있어 먼저 수평으로 눕히고, 이 하나하나(이어진 조각)를 높이로 윗니 16·아랫니 16 으로 나눈다. 잇몸은 이 둘레 띠만 남긴다.
    /// 예전 치열 자리(턱관절 맞춤이 끝난 위치)에 폭·앞뒤 길이를 맞춰 붙이고, 아랫니는 아래턱과 함께 움직인다.
    /// </summary>
    static void NewTeeth()
    {
        var upperOld = GameObject.Find("Part_UpperTeethArch"); var lowerOld = GameObject.Find("Part_LowerTeethArch");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PeopleDir}/HumanTeeth.glb");
        if (upperOld == null || lowerOld == null || prefab == null) return;
        foreach (var n in new[] { "새_윗니", "새_아랫니" }) foreach (var o in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name == n).ToArray()) Object.DestroyImmediate(o.gameObject);

        // 예전 치열은 다시 적용할 때 이미 렌더러가 꺼져 있어(꺼진 렌더러는 크기가 0) 메시 자체로 잰다. 충치 조각 같은 작은 것은 빼고 가장 큰 메시만.
        MeshFilter Arch(GameObject arch) => arch.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).OrderByDescending(f => f.sharedMesh.vertexCount).First();
        var upperArch = Arch(upperOld); var lowerArch = Arch(lowerOld);
        var upperTarget = MeshBounds(upperArch); var lowerTarget = MeshBounds(lowerArch);
        bool oldApexBack = ApexAtMinZ(upperArch.sharedMesh.vertices.Select(x => upperArch.transform.TransformPoint(x)).ToList());
        foreach (var r in upperOld.GetComponentsInChildren<Renderer>(true).Concat(lowerOld.GetComponentsInChildren<Renderer>(true))) if (r is MeshRenderer) r.enabled = false;

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); temp.transform.localScale = Vector3.one;
        var filters = temp.GetComponentsInChildren<MeshFilter>();
        Bounds Measure() => filters.Select(f => f.GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        // 이 모형은 Unity 에 들어오면 x축으로 90° 누워 있다(치열 높이가 z, 앞뒤가 y). 가장 얇은 축(높이)이 z 면 세운다.
        // (Sketchfab 뷰어·glTF 원본 좌표에서는 높이가 y — 원본 좌표 = x축 -90° 돌린 것)
        var all = Measure();
        if (all.size.z < all.size.x && all.size.z < all.size.y) { temp.transform.rotation = Quaternion.Euler(-90, 0, 0); all = Measure(); }
        bool IsGum(MeshFilter f) { var m = f.GetComponent<Renderer>().sharedMaterial; return m != null && m.name.ToLower().Contains("gum"); }
        var teeth = Geometry(filters.First(f => !IsGum(f))); var gum = Geometry(filters.First(IsGum));
        Object.DestroyImmediate(temp);

        // 씹는 면을 수평으로: 이 정점들이 가장 얇게 퍼진 방향(씹는 면의 법선)을 위로 돌린다.
        var level = Quaternion.FromToRotation(ThinnestAxis(teeth.v), Vector3.up);
        Vector3 c = teeth.v.Aggregate(Vector3.zero, (a, p) => a + p) / teeth.v.Count;
        foreach (var g in new[] { teeth, gum })
        {
            for (int i = 0; i < g.v.Count; i++) g.v[i] = level * (g.v[i] - c);
            for (int i = 0; i < g.n.Count; i++) g.n[i] = level * g.n[i];
        }

        // 이 하나하나는 서로 이어지지 않은 조각이다. 조각 가운데 높이를 줄 세워 가장 크게 벌어진 틈에서 윗니·아랫니로 나눈다.
        var piece = Pieces(teeth);
        var heights = piece.Distinct().ToDictionary(p => p, p => Enumerable.Range(0, teeth.v.Count).Where(i => piece[i] == p).Average(i => teeth.v[i].y));
        var sorted = heights.Values.OrderBy(h => h).ToList();
        int gapAt = Enumerable.Range(0, sorted.Count - 1).OrderByDescending(k => sorted[k + 1] - sorted[k]).First();
        float gap = (sorted[gapAt] + sorted[gapAt + 1]) / 2;
        bool UpperTooth(int tri) => heights[piece[teeth.t[tri]]] > gap;

        // 잇몸은 가까운 이의 뿌리 쪽 절반(윗니는 위, 아랫니는 아래)에 있는 것만 남긴다. 한 덩어리 잇몸에 붙은 입천장·입바닥 판과
        // 볼 안쪽 막(씹는 면을 가로질러 이 위로 얇은 막처럼 보인다)이 빠진다.
        var pieceMin = heights.Keys.ToDictionary(p => p, p => float.MaxValue); var pieceMax = heights.Keys.ToDictionary(p => p, p => float.MinValue);
        for (int i = 0; i < teeth.v.Count; i++) { pieceMin[piece[i]] = Mathf.Min(pieceMin[piece[i]], teeth.v[i].y); pieceMax[piece[i]] = Mathf.Max(pieceMax[piece[i]], teeth.v[i].y); }
        float reach = all.size.x * .06f, r2 = reach * reach;
        var nearest = new int[gum.v.Count]; var rootSide = new bool[gum.v.Count];
        for (int i = 0; i < gum.v.Count; i++)
        {
            float best = float.MaxValue;
            for (int k = 0; k < teeth.v.Count; k++) { float d = (gum.v[i] - teeth.v[k]).sqrMagnitude; if (d < best) { best = d; nearest[i] = k; } }
            int p = piece[nearest[i]]; float mid = (pieceMin[p] + pieceMax[p]) / 2;
            rootSide[i] = best < r2 && (heights[p] > gap ? gum.v[i].y > mid : gum.v[i].y < mid);
        }
        bool Band(int tri) => rootSide[gum.t[tri]] && rootSide[gum.t[tri + 1]] && rootSide[gum.t[tri + 2]];
        bool UpperGum(int tri) => heights[piece[nearest[gum.t[tri]]]] > gap;

        bool newApexBack = ApexAtMinZ(teeth.v);
        int upperCount = heights.Values.Count(h => h > gap);
        Debug.Log($"[이] 원본 상자 {all.size}, 이 {heights.Count}개 (윗니 {upperCount} · 아랫니 {heights.Count - upperCount}), 잇몸 띠 삼각형 {Enumerable.Range(0, gum.t.Count / 3).Count(k => Band(k * 3))}/{gum.t.Count / 3}, 앞니 방향 예전 {(oldApexBack ? "-z" : "+z")} / 새 {(newApexBack ? "-z" : "+z")}");

        var upper = TeethArch("새_윗니", "위", Subset(teeth, UpperTooth), Subset(gum, k => Band(k) && UpperGum(k)));
        var lower = TeethArch("새_아랫니", "아래", Subset(teeth, k => !UpperTooth(k)), Subset(gum, k => Band(k) && !UpperGum(k)));
        // 두 치열은 모형 그대로의 맞물림을 지키도록 같은 변환을 쓴다: 앞니 방향을 예전 치열과 맞추고(필요하면 반 바퀴),
        // 예전 두 치열의 평균 폭·앞뒤 길이에 맞추고, 씹는 틈을 예전 윗니 아래끝과 아랫니 위끝의 가운데 높이에 둔다.
        // (상자 끝끼리 따로 맞추면 예전 치열 상자가 위아래로 1.6m 겹쳐 있어 새 이끼리 파고든다)
        var arches = new[] { upper, lower };
        foreach (var root in arches) if (oldApexBack != newApexBack) root.rotation = Quaternion.Euler(0, 180, 0);
        Bounds Both() { var b = Bounds(upper.gameObject); b.Encapsulate(Bounds(lower.gameObject)); return b; }
        var both = Both();
        float sx = (upperTarget.size.x + lowerTarget.size.x) / 2 / Mathf.Max(1e-4f, both.size.x), sz = (upperTarget.size.z + lowerTarget.size.z) / 2 / Mathf.Max(1e-4f, both.size.z);
        foreach (var root in arches) root.localScale = new Vector3(sx, sx, sz);
        both = Both();
        float biteY = (upperTarget.min.y + lowerTarget.max.y) / 2;
        var centre = (upperTarget.center + lowerTarget.center) / 2;
        foreach (var root in arches) root.position += new Vector3(centre.x - both.center.x, biteY - (root.position.y + gap * sx), centre.z - both.center.z);
        upper.SetParent(upperOld.transform, true);
        lower.SetParent(lowerOld.transform, true); // 아래턱과 함께 움직인다
        var ub = Bounds(upper.gameObject); var lb = Bounds(lower.gameObject);
        Debug.Log($"[이] 예전 윗니 {upperTarget.size} → 새 윗니 {ub.size} @ {ub.center}, 예전 아랫니 {lowerTarget.size} → 새 아랫니 {lb.size} @ {lb.center}");
    }

    class TeethGeometry { public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<int> t = new List<int>(); }

    static TeethGeometry Geometry(MeshFilter f)
    {
        var g = new TeethGeometry(); var m = f.transform.localToWorldMatrix; var mesh = f.sharedMesh;
        g.v.AddRange(mesh.vertices.Select(x => m.MultiplyPoint3x4(x)));
        var normals = mesh.normals;
        if (normals.Length == mesh.vertexCount) g.n.AddRange(normals.Select(x => m.MultiplyVector(x).normalized));
        g.t.AddRange(mesh.triangles);
        return g;
    }

    /// <summary>점들이 가장 얇게 퍼진 방향(공분산의 가장 작은 고유 벡터, 위쪽을 향하게). 이동 거듭제곱법.</summary>
    static Vector3 ThinnestAxis(List<Vector3> pts)
    {
        var c = pts.Aggregate(Vector3.zero, (a, p) => a + p) / pts.Count;
        var m = new float[3, 3];
        foreach (var p in pts) { var d = p - c; for (int i = 0; i < 3; i++) for (int k = 0; k < 3; k++) m[i, k] += d[i] * d[k] / pts.Count; }
        float shift = m[0, 0] + m[1, 1] + m[2, 2];
        var v = Vector3.up;
        for (int it = 0; it < 200; it++)
        {
            var w = new Vector3(shift * v.x - (m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z),
                                shift * v.y - (m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z),
                                shift * v.z - (m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z));
            v = w.normalized;
        }
        return v.y < 0 ? -v : v;
    }

    /// <summary>삼각형으로 이어진 조각마다 번호를 붙인다(정점별 조각 번호).</summary>
    static int[] Pieces(TeethGeometry g)
    {
        var parent = Enumerable.Range(0, g.v.Count).ToArray();
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int i = 0; i < g.t.Count; i += 3) { int a = Find(g.t[i]); parent[Find(g.t[i + 1])] = a; parent[Find(g.t[i + 2])] = a; }
        return Enumerable.Range(0, g.v.Count).Select(Find).ToArray();
    }

    /// <summary>keep(삼각형 첫 인덱스) 가 참인 삼각형만, 쓰는 정점만 모은다.</summary>
    static TeethGeometry Subset(TeethGeometry g, System.Func<int, bool> keep)
    {
        var s = new TeethGeometry(); var remap = new Dictionary<int, int>();
        for (int i = 0; i < g.t.Count; i += 3)
        {
            if (!keep(i)) continue;
            for (int k = 0; k < 3; k++)
            {
                int src = g.t[i + k];
                if (!remap.TryGetValue(src, out int dst)) { dst = s.v.Count; remap[src] = dst; s.v.Add(g.v[src]); if (g.n.Count == g.v.Count) s.n.Add(g.n[src]); }
                s.t.Add(dst);
            }
        }
        return s;
    }

    /// <summary>이·잇몸 두 조각으로 된 치열 하나.</summary>
    static Transform TeethArch(string name, string label, TeethGeometry teeth, TeethGeometry gum)
    {
        var root = new GameObject(name).transform;
        foreach (var (geo, part) in new[] { (teeth, "이"), (gum, "잇몸") })
        {
            if (geo.v.Count == 0) continue;
            var mesh = new Mesh { name = $"치아_{label}_{part}" };
            mesh.indexFormat = geo.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(geo.v); mesh.SetTriangles(geo.t, 0);
            if (geo.n.Count == geo.v.Count) mesh.SetNormals(geo.n); else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh($"{Folder}/치아_{label}_{part}.asset", ref mesh);
            var go = new GameObject(part); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            // 원본 재질은 색이 없는 흰 기본 재질이라 몸속에서 회색 덩어리로 보인다: 법랑질은 젖은 상아색, 잇몸은 입 점막
            var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.sharedMaterial = part == "잇몸" ? Mucosa("Mouth") : Lit("새_이_법랑질", new Color(.93f, .9f, .82f), .72f);
        }
        return root;
    }

    static Bounds MeshBounds(MeshFilter f)
    {
        var mb = f.sharedMesh.bounds; var m = f.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return b;
    }

    /// <summary>U자 치열에서 앞니 쪽(꼭짓점)이 z 가 작은 쪽이면 true. 꼭짓점 쪽은 가운데로 모이고, 어금니 쪽은 양옆으로 벌어져 있다.</summary>
    static bool ApexAtMinZ(IList<Vector3> pts)
    {
        float zmin = pts.Min(p => p.z), zmax = pts.Max(p => p.z), cx = pts.Average(p => p.x), band = (zmax - zmin) * .12f;
        float spreadMin = pts.Where(p => p.z < zmin + band).Select(p => Mathf.Abs(p.x - cx)).DefaultIfEmpty(0).Average();
        float spreadMax = pts.Where(p => p.z > zmax - band).Select(p => Mathf.Abs(p.x - cx)).DefaultIfEmpty(0).Average();
        return spreadMin < spreadMax;
    }

    /// <summary>충치·치태 자리를 찾을 때 쓰는 이 표면 (새 이 모형이 있으면 그것).</summary>
    static MeshFilter[] ToothSurfaces(bool lowerOnly)
    {
        var names = lowerOnly ? new[] { "새_아랫니" } : new[] { "새_아랫니", "새_윗니" };
        var list = names.Select(GameObject.Find).Where(g => g != null).SelectMany(g => g.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "이")).ToArray();
        if (list.Length > 0) return list;
        var old = (lowerOnly ? new[] { "Part_LowerTeethArch" } : new[] { "Part_LowerTeethArch", "Part_UpperTeethArch" }).Select(GameObject.Find).Where(g => g != null);
        return old.Select(g => g.GetComponentInChildren<MeshFilter>()).Where(f => f != null).ToArray();
    }

    // ───────────────────────────── 입: 직접 씹기 · 삼킴 타이밍 ─────────────────────────────

    static void MouthActions()
    {
        var chew = Object.FindFirstObjectByType<ChewingSequence>();
        if (chew != null) { chew.manualChew = true; EditorUtility.SetDirty(chew); }
        var swallow = Object.FindFirstObjectByType<SwallowSequence>();
        if (swallow != null) { swallow.timingGame = true; EditorUtility.SetDirty(swallow); }
        var zone = GameObject.Find("ChewZone")?.GetComponent<NarrationZone>();
        if (zone != null) { zone.lines = Mission("Mouth", "chew_start", 4f); EditorUtility.SetDirty(zone); }
    }

    // ───────────────────────────── 소장: 효소 짝 맞추기 ─────────────────────────────

    static void EnzymeMission()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) return;
        var foods = new[] { ("녹말", GlucosePrefab(), "아밀레이스", "enzyme_starch"), ("단백질", Prefab("Part_ProteinChain"), "트립신", "enzyme_protein"), ("지방", Prefab("Prop_FattyAcid"), "라이페이스", "enzyme_fat") };
        var rounds = new List<GermBlaster>();
        for (int i = 0; i < foods.Length; i++)
        {
            var (food, prefab, enzyme, key) = foods[i];
            var others = foods.Where((_, k) => k != i).ToArray();
            var hunt = Hunt($"효소_짝맞추기_{i + 1}");
            hunt.ride = ride; hunt.basis = ride.transform; hunt.followBasis = false; hunt.spots = new Transform[0];
            hunt.harmfulPrefabs = new[] { prefab }; hunt.harmfulCount = 4; hunt.harmfulTags = new[] { food };
            hunt.helpfulPrefabs = others.Select(o => o.Item2).ToArray(); hunt.helpfulCount = 4; hunt.helpfulTags = others.Select(o => o.Item1).ToArray();
            hunt.spawnCenter = new Vector3(0, 0, 5.5f); hunt.spawnExtents = new Vector3(2f, 1.1f, 1.8f); hunt.scaleRange = new Vector2(.4f, .5f); hunt.range = 12f;
            hunt.tagFont = KoreanFont();
            hunt.targetLabel = food; hunt.goalText = $"{enzyme} 효소로 {food}에만 맞히세요";
            hunt.clearedStatus = $"{food} 분해 완료!";
            hunt.hitSound = "fizz";
            hunt.startLines = i == 0 ? new NarrationLine[0] : Mission("SmallIntestine", key, 4f);
            hunt.helpfulHitLines = Mission("SmallIntestine", "enzyme_wrong", 3f);
            hunt.clearedLines = i == foods.Length - 1 ? Mission("SmallIntestine", "enzyme_done", 4f) : new NarrationLine[0];
            hunt.releaseRide = i == foods.Length - 1;
            hunt.scoreKey = i == foods.Length - 1 ? "enzymes" : ""; hunt.scoreLabel = "효소 짝 맞추기";
            rounds.Add(hunt);
        }
        for (int i = 0; i < rounds.Count - 1; i++) { ClearListeners(rounds[i].onCleared, rounds[i + 1]); UnityEventTools.AddPersistentListener(rounds[i].onCleared, rounds[i + 1].Begin); }
        var stops = (ride.stops ?? new CapsuleRide.RideStop[0]).Where(s => !s.holdUntilReleased).ToList();
        var hold = new CapsuleRide.RideStop { normalizedPosition = .34f, lines = Mission("SmallIntestine", "enzyme_starch", 4f), waitSeconds = .5f, onReached = new UnityEvent(), holdUntilReleased = true, maxHoldSeconds = 180f };
        UnityEventTools.AddPersistentListener(hold.onReached, rounds[0].Begin);
        stops.Add(hold);
        ride.stops = stops.OrderBy(s => s.normalizedPosition).ToArray();
        EditorUtility.SetDirty(ride);
    }

    // ───────────────────────────── 대장: 유익균 먹이 주기 ─────────────────────────────

    static void FiberMission()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) return;
        var hunt = Hunt("유익균_먹이주기");
        hunt.ride = ride; hunt.basis = ride.transform; hunt.followBasis = false; hunt.spots = new Transform[0];
        hunt.harmfulPrefabs = new[] { Prefab("Char_Probiotic") }; hunt.harmfulCount = 5; hunt.harmfulTag = "유익균";
        hunt.helpfulPrefabs = new[] { Prefab("Char_HarmfulBacteria") }; hunt.helpfulCount = 2; hunt.helpfulTag = "해로운 균";
        hunt.growOnHit = true; hunt.tagFont = KoreanFont();
        hunt.spawnCenter = new Vector3(0, -.3f, 5.5f); hunt.spawnExtents = new Vector3(2f, 1.1f, 1.8f); hunt.scaleRange = new Vector2(.35f, .45f); hunt.range = 12f;
        hunt.targetLabel = "배부른 유익균"; hunt.goalText = "초록 유익균에게 식이섬유를 쏴서 먹이를 주세요";
        hunt.clearedStatus = "유익균이 튼튼해졌어요!"; hunt.fireSound = "whoosh"; hunt.hitSound = "ting";
        hunt.startLines = Mission("LargeIntestine", "fiber_start", 5f);
        hunt.helpfulHitLines = Mission("LargeIntestine", "fiber_wrong", 3f);
        hunt.clearedLines = Mission("LargeIntestine", "fiber_done", 4f);
        hunt.releaseRide = true; hunt.scoreKey = "fiber"; hunt.scoreLabel = "유익균 먹이 주기";
        var stops = (ride.stops ?? new CapsuleRide.RideStop[0]).ToList();
        stops.RemoveAll(s => s.holdUntilReleased && s.normalizedPosition > .6f);
        var hold = new CapsuleRide.RideStop { normalizedPosition = .74f, lines = new NarrationLine[0], waitSeconds = .5f, onReached = new UnityEvent(), holdUntilReleased = true, maxHoldSeconds = 150f };
        UnityEventTools.AddPersistentListener(hold.onReached, hunt.Begin);
        stops.Add(hold);
        ride.stops = stops.OrderBy(s => s.normalizedPosition).ToArray();
        EditorUtility.SetDirty(ride);
    }

    // ───────────────────────────── 소리 ─────────────────────────────

    static void Ambience(string id)
    {
        void Add(string name, string clip, float volume)
        {
            var go = GameObject.Find(name) ?? new GameObject(name);
            var a = go.GetComponent<SceneAmbience>(); if (a == null) a = go.AddComponent<SceneAmbience>();
            a.clipName = clip; a.volume = volume;
        }
        Add("몸_심장소리", "heartbeat", .16f);
        if (id == "Esophagus") Add("관_흐름소리", "flow", .22f);
        if (id == "SmallIntestine" || id == "LargeIntestine") { Add("장_꾸르륵", "gurgle", .22f); Add("관_흐름소리", "flow", .15f); }
        // 잠수정 엔진 소리 (탐험가와 함께 움직인다)
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride != null)
        {
            var hum = ride.transform.Find("잠수정_소리") ?? new GameObject("잠수정_소리").transform;
            hum.SetParent(ride.transform, false);
            var a = hum.GetComponent<SceneAmbience>(); if (a == null) a = hum.gameObject.AddComponent<SceneAmbience>();
            a.clipName = "subhum"; a.volume = .16f;
        }
    }
}
#endif
