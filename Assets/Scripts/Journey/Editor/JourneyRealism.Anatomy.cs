#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// 장기 속 부위·물체에 제 역할과 자리를 준다. 지우지 않고:
/// 벽에서 나오는 것(위샘·혈관·융털·후두덮개)은 실제 벽면에 뿌리를 묻고 분비물·흐름을 내며,
/// 액체에 뜬 것(음식·단백질 사슬·찌꺼기)은 반쯤 잠겨 둥실둥실 흔들린다.
/// 적혈구는 사용자의 실사 적혈구 모형(Blood 프리팹의 원본)으로 반투명 혈관 안을 굴러 흐른다(장 안에 그냥 떠 있으면 출혈이다).
/// </summary>
public static partial class JourneyRealism
{
    static void Anatomy(string id)
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        var path = ride != null && ride.cart != null ? ride.cart.Spline : null;
        if (id == "Mouth") SalivaDrips();
        if (id == "Esophagus") Epiglottis();
        if (id == "Stomach") StomachAnatomy();
        if (id == "SmallIntestine" && path != null) SmallIntestineAnatomy(path);
        if (id == "LargeIntestine") WasteLumps();
    }

    // ───────────────────────────── 공통 ─────────────────────────────

    /// <summary>물체의 모든 메시 정점을 dir 방향으로 잰 최소·최대(월드).</summary>
    static void Extent(GameObject go, Vector3 dir, out float min, out float max)
    {
        min = float.MaxValue; max = float.MinValue;
        foreach (var f in go.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null || f.GetComponentInParent<Canvas>() != null) continue;
            var m = f.transform.localToWorldMatrix;
            foreach (var v in f.sharedMesh.vertices) { float d = Vector3.Dot(m.MultiplyPoint3x4(v), dir); min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
        }
        if (min > max) { min = max = Vector3.Dot(go.transform.position, dir); }
    }

    /// <summary>
    /// 벽에서 솟는 부위를 벽에 심는다: 모형의 +y 를 벽 안쪽(inward)으로 세우고 spin 만큼 돌린 뒤,
    /// 높이의 buried 비율만큼 벽 속에 묻는다. 돌려주는 값 = 벽 밖으로 나온 끝(분비물이 나오는 곳).
    /// </summary>
    static Vector3 PlantOnWall(GameObject go, Vector3 wall, Vector3 inward, float spin, float buried)
    {
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, inward) * Quaternion.Euler(0, spin, 0);
        go.transform.position = wall;
        var across = Vector3.ProjectOnPlane(wall - Bounds(go).center, inward);
        go.transform.position += across;
        Extent(go, inward, out float lo, out float hi);
        float h = hi - lo, wallDepth = Vector3.Dot(wall, inward);
        go.transform.position += inward * (wallDepth - (lo + h * buried));
        return wall + inward * h * (1 - buried);
    }

    static Material TransparentLit(string name, Color color, float smoothness, Color emission)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_Cull", 0); mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness); mat.SetFloat("_Metallic", 0);
        if (emission.maxColorComponent > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission); } else mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat); return mat;
    }

    static SplineContainer FlowSpline(string name, IList<Vector3> points, Transform parent)
    {
        var t = parent.Find(name); if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var container = t.GetComponent<SplineContainer>(); if (container == null) container = t.gameObject.AddComponent<SplineContainer>();
        container.Spline.Clear();
        foreach (var p in points) container.Spline.Add(new BezierKnot(t.InverseTransformPoint(p)), TangentMode.AutoSmooth);
        container.Spline.Closed = false;
        return container;
    }

    static Vector3 Catmull(IList<Vector3> p, float t)
    {
        float f = Mathf.Clamp01(t) * (p.Count - 1); int i = Mathf.Min(p.Count - 2, Mathf.FloorToInt(f)); float u = f - i;
        Vector3 a = p[Mathf.Max(0, i - 1)], b = p[i], c = p[i + 1], d = p[Mathf.Min(p.Count - 1, i + 2)];
        return .5f * (2 * b + (c - a) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (3 * b - a - 3 * c + d) * u * u * u);
    }

    static T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }

    // ───────────────────────────── 입 ─────────────────────────────

    /// <summary>침샘관 구멍 6곳(귀밑샘·턱밑샘·혀밑샘, 해부학 자리)에서 평소에도 침이 조금씩 맺혀 흐른다. 씹을 때는 기존 분비가 크게 나온다.</summary>
    static void SalivaDrips()
    {
        var saliva = Droplet("침", new Color(.86f, .93f, 1f, .5f));
        int i = 0;
        foreach (var em in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("Saliva_")).OrderBy(p => p.name).ToArray())
        {
            var drip = Ambient($"침_맺힘_{i++}", em.transform.position, em.transform.rotation, saliva, 1.2f, new Vector2(.03f, .07f), new Vector2(.05f, .18f), .35f, new Vector2(1.5f, 2.5f), ParticleSystemShapeType.Cone, Vector3.one * .3f, true);
            drip.transform.SetParent(em.transform.parent, true);
            var main = drip.main; main.maxParticles = 12;
        }
    }

    // ───────────────────────────── 식도 ─────────────────────────────

    /// <summary>
    /// 후두덮개: 식도 벽 속에 박혀 있던 모형을 출발점 위 앞벽(기도는 식도 앞쪽에 있다)의 기도 입구 위로 옮겨 경첩처럼 단다.
    /// 장면에 들어오면 열려 있다가 닫히며 기도를 덮는다(음식이 기도로 가지 않는 원리). 식도 꼭대기는 목구멍 천장으로 막는다.
    /// </summary>
    static void Epiglottis()
    {
        var epi = GameObject.Find("Part_Epiglottis");
        var tube = GameObject.Find("연속_기관벽")?.GetComponent<MeshCollider>();
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (epi == null || tube == null || ride == null || ride.cart == null || ride.cart.Spline == null) { Debug.LogWarning("[부위] 식도 후두덮개·관 벽·이동 경로를 찾지 못함"); return; }
        Vector3 start = ride.cart.Spline.EvaluatePosition(0);
        ThroatDome(start);
        DockRing();
        Physics.SyncTransforms();
        var from = start + Vector3.up * 2.3f;
        if (!tube.Raycast(new Ray(from, Vector3.forward), out var hit, 10f)) { Debug.LogWarning("[부위] 식도 앞벽을 찾지 못함"); return; }
        var at = hit.point; var inward = Vector3.back;
        const float hole = .9f, length = 2f;
        Opening("기도_입구", at, inward, Mucosa("Esophagus"), radius: hole, folds: 6, foldHeight: .05f, depth: .6f, mound: .12f);

        var wallUp = Vector3.ProjectOnPlane(Vector3.up, inward).normalized;
        var hinge = at + wallUp * hole * 1.05f + inward * .05f;
        var pivot = (GameObject.Find("후두덮개_경첩") ?? new GameObject("후두덮개_경첩")).transform;
        epi.transform.SetParent(null, true);
        pivot.SetParent(null); pivot.localScale = Vector3.one; pivot.SetPositionAndRotation(hinge, Quaternion.LookRotation(inward, wallUp));

        // 모형의 좁은 끝(+y, 줄기)이 경첩, 넓은 잎이 구멍 위를 덮는다. 얇은 축(z)은 안쪽을 향한다.
        epi.transform.rotation = Quaternion.LookRotation(inward, wallUp);
        Extent(epi, wallUp, out float lo, out float hi);
        epi.transform.localScale *= length / Mathf.Max(.01f, hi - lo);
        Extent(epi, wallUp, out lo, out hi);
        epi.transform.position += wallUp * (Vector3.Dot(hinge, wallUp) - hi);
        var side = Vector3.Cross(wallUp, inward).normalized;
        Extent(epi, side, out float slo, out float shi);
        Extent(epi, inward, out float ilo, out _);
        epi.transform.position += side * (Vector3.Dot(at, side) - (slo + shi) / 2) + inward * (Vector3.Dot(at, inward) + .06f - ilo);
        epi.transform.SetParent(pivot, true);

        var close = Ensure<HingeClose>(pivot.gameObject);
        close.closed = pivot.rotation;
        close.open = Quaternion.AngleAxis(-55f, side) * pivot.rotation;
        if (Vector3.Dot(Quaternion.AngleAxis(-55f, side) * -wallUp, inward) < 0) close.open = Quaternion.AngleAxis(55f, side) * pivot.rotation; // 열리면 잎 끝이 안쪽(목구멍)으로
        var splash = Burst(pivot, "닫힘_침방울", Droplet("침", new Color(.86f, .93f, 1f, .5f)), new Vector2(.04f, .09f), new Vector2(.3f, .8f), new Vector2(.6f, 1.2f));
        splash.transform.position = at + inward * .3f;
        var em = splash.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, 16) });
        var sm = splash.main; sm.gravityModifier = .5f;
        close.splash = splash;
        EditorUtility.SetDirty(close);

        var label = epi.GetComponent<PartLabel>();
        if (label != null && label.labelRoot != null) label.labelRoot.transform.position = hinge + inward * .7f + wallUp * .3f;
        Debug.Log($"[부위] 후두덮개: 기도 입구 {at} 위 경첩 {hinge}");
    }

    /// <summary>
    /// 식도 출발 발판: 탐험가는 처음부터 잠수정에 탄 채 내려가므로, 꽉 찬 원판은 내려가는 내내 머리 위를 막는 검은 천장이 된다.
    /// 잠수정보다 넓은 구멍이 뚫린 정박 고리로 바꿔, 올려다보면 후두덮개와 목구멍 천장이 보이게 한다.
    /// </summary>
    static void DockRing()
    {
        var plate = GameObject.Find("EsophagusTopPlatform");
        var mf = plate != null ? plate.GetComponent<MeshFilter>() : null;
        if (mf == null) return;
        var pos = plate.transform.position;
        plate.transform.SetPositionAndRotation(pos, Quaternion.identity); plate.transform.localScale = Vector3.one;
        var ring = Arc(PodRadius + .25f, 2.6f, -180, 180, .12f, 72); ring.name = "정박_고리";
        SaveMesh($"{Folder}/정박_고리.asset", ref ring);
        mf.sharedMesh = ring;
        foreach (var c in plate.GetComponents<Collider>()) c.enabled = false;
    }

    /// <summary>
    /// 식도 꼭대기를 덮는 목구멍 천장(안쪽을 향한 반구). 예전 구 모양 덮개는 바깥을 향한 면이라 점막 재질로는 안에서 보이지 않아
    /// 위를 올려다보면 텅 빈 어둠이었다. 관 위끝(출발점 위 6m)보다 조금 아래에서 관보다 넓게 시작해 틈이 보이지 않는다.
    /// </summary>
    static void ThroatDome(Vector3 start)
    {
        var cap = GameObject.Find("ThroatCap");
        if (cap != null) foreach (var r in cap.GetComponentsInChildren<Renderer>()) r.enabled = false;
        const int around = 64, rings = 16; const float radius = 4.1f, height = 2.6f;
        var center = start + Vector3.up * 5.7f;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int j = 0; j <= rings; j++)
        for (int i = 0; i <= around; i++)
        {
            float th = Mathf.PI * .5f * j / rings, ph = Mathf.PI * 2 * i / around;
            float wobble = 1 + .05f * Mathf.Sin(ph * 5 + j * .7f) * Mathf.Sin(th * 2); // 목구멍 벽의 굴곡
            v.Add(center + new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * radius * wobble, Mathf.Cos(th) * height, Mathf.Sin(th) * Mathf.Sin(ph) * radius * wobble));
            if (j < rings && i < around) { int k = j * (around + 1) + i, k2 = k + around + 1; tri.AddRange(new[] { k, k2, k + 1, k + 1, k2, k2 + 1 }); } // 안쪽을 향하게
        }
        var mesh = new Mesh { name = "목구멍_천장" }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SurfaceObject("목구멍_천장", mesh, Mucosa("Esophagus"));
    }

    // ───────────────────────────── 위 ─────────────────────────────

    static void StomachAnatomy()
    {
        var mucus = Droplet("점액", new Color(.92f, .9f, .86f, .5f));
        var acid = Droplet("위산", new Color(1f, .95f, .62f, .45f));
        var pepsin = Droplet("펩신", new Color(.86f, 1f, .82f, .42f));
        var bubble = Droplet("거품", new Color(1f, 1f, .92f, .3f), rim: 1.7f, highlight: 1.8f);

        // 1) 위샘: 뿌리(아래쪽 갈래)와 몸통 절반은 벽 속, 목과 구멍만 안쪽으로 내민다. 잠수정 길 양옆 눈높이 조금 위에 6개.
        var walls = GameObject.Find("Walls");
        var glands = walls == null ? new List<GameObject>() : walls.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Part_GastricGland").Select(t => t.gameObject).ToList();
        if (glands.Count > 0)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(glands[0]);
            var spots = new[] { (-1.5f, true), (-1.5f, false), (3.5f, true), (3.5f, false), (7.2f, true), (7.2f, false) };
            while (source != null && glands.Count < spots.Length)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(source, walls.transform);
                g.name = "Part_GastricGland"; g.transform.localScale = glands[0].transform.localScale; glands.Add(g);
            }
            var secretions = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.name.StartsWith("GlandSecretion_")).ToArray();
            var drops = new[] { mucus, acid, pepsin };
            for (int i = 0; i < Mathf.Min(glands.Count, spots.Length); i++)
            {
                var (z, right) = spots[i];
                float a = AngleForHeight(z, 2.4f, right);
                var wall = StomachWall(a, z, out var inward);
                var tip = PlantOnWall(glands[i], wall, inward, i * 67f, .55f);
                var flow = Quaternion.LookRotation((inward + Vector3.down * .35f).normalized);
                var own = secretions.FirstOrDefault(p => p.transform.IsChildOf(glands[i].transform));
                if (own != null) own.transform.SetPositionAndRotation(tip, flow);
                var ps = Ambient($"위샘_분비_{i}", tip, flow, drops[i % 3], 2.5f, new Vector2(.05f, .11f), new Vector2(.15f, .35f), .2f, new Vector2(2f, 3.5f), ParticleSystemShapeType.Cone, Vector3.one * .4f, true);
                ps.transform.SetParent(glands[i].transform, true);
                var label = glands[i].GetComponent<PartLabel>();
                if (label != null && label.labelRoot != null) label.labelRoot.transform.position = tip + inward * .5f + Vector3.up * .4f;
            }
            Debug.Log($"[부위] 위샘 {glands.Count}개를 벽에 심음");
        }

        // 2) 음식 덩어리 ↔ 미즙: 위액 웅덩이에 반쯤 잠겨 위의 꿈틀거림에 맞춰 출렁이고, 둘레에서 산 거품이 오른다. 잠수정 길(x=0)에서 비켜 둔다.
        var churn = Object.FindFirstObjectByType<StomachChurn>();
        if (churn != null && churn.foodBoluses != null)
        {
            const float lake = .4f;
            var spots = new[] { new Vector2(-3f, .2f), new Vector2(3.1f, 1.6f), new Vector2(-3.3f, 4.6f), new Vector2(3.2f, 5.8f) };
            for (int i = 0; i < Mathf.Min(spots.Length, churn.foodBoluses.Length); i++)
            {
                var pair = new[] { churn.foodBoluses[i], churn.chymeBlobs != null && i < churn.chymeBlobs.Length ? churn.chymeBlobs[i] : null };
                foreach (var go in pair)
                {
                    if (go == null) continue;
                    bool was = go.activeSelf; go.SetActive(true);
                    go.transform.position = new Vector3(spots[i].x, lake, spots[i].y);
                    var b = Bounds(go);
                    go.transform.position += new Vector3(spots[i].x - b.center.x, lake + b.size.y * .08f - b.center.y, spots[i].y - b.center.z);
                    var f = Ensure<GentleFloat>(go); f.bob = .04f; f.period = 3.6f; f.drift = .03f; f.tilt = 4f; f.spin = 0;
                    go.SetActive(was);
                }
                var food = churn.foodBoluses[i];
                if (food == null) continue;
                var fizz = Ambient($"음식_거품_{i}", food.transform.position + Vector3.up * .1f, Quaternion.Euler(-90, 0, 0), bubble, 4f, new Vector2(.03f, .08f), new Vector2(.05f, .12f), -.03f, new Vector2(1f, 2f), ParticleSystemShapeType.Circle, Vector3.one, false);
                var shape = fizz.shape; shape.radius = .55f;
                fizz.transform.SetParent(food.transform, true);
            }
        }

        // 3) 단백질 사슬: 위액 위에 떠서 천천히 흔들린다(펩신이 다가와 자르는 장면은 그대로).
        var cut = Object.FindFirstObjectByType<EnzymeCutSequence>();
        if (cut != null && cut.proteinChains != null)
            foreach (var chain in cut.proteinChains.Where(c => c != null))
            {
                var f = Ensure<GentleFloat>(chain.gameObject); f.bob = .08f; f.period = 5f; f.drift = .05f; f.tilt = 6f; f.spin = 4f;
            }
    }

    /// <summary>위 단면에서 벽 높이가 y 가 되는 둘레 각도(오른쪽 벽 또는 왼쪽 벽).</summary>
    static float AngleForHeight(float z, float y, bool right)
    {
        float best = 0, err = float.MaxValue;
        for (float deg = -80; deg <= 60; deg += 1)
        {
            float a = (right ? deg : 180 - deg) * Mathf.Deg2Rad;
            float e = Mathf.Abs(StomachWall(a, z, out _).y - y);
            if (e < err) { err = e; best = a; }
        }
        return best;
    }

    // ───────────────────────────── 소장 ─────────────────────────────

    static void SmallIntestineAnatomy(SplineContainer path)
    {
        var rbc = RbcPrefab();

        // 1) 융털 확대 모형: 실제 융털처럼 오른쪽 벽에 뿌리를 두고 안쪽으로 손가락처럼 솟는다. 잠수정 유리에 닿지 않을 만큼만 키운다.
        var zoom = GameObject.Find("VillusZoom");
        if (zoom != null)
        {
            float z = zoom.transform.position.z;
            var wall = TubeWall("SmallIntestine", path, z, -12 * Mathf.Deg2Rad, out var inward);
            Vector3 lane = (Vector3)path.EvaluatePosition(NearestT(path, z));
            const float podClear = 1.7f + .3f;
            float scale = .44f; // 3.2m → 1.4m
            while (scale > .2f && Vector3.Distance(wall + inward * 3.2f * scale, lane) < podClear) scale -= .02f;
            zoom.transform.localScale = Vector3.one * scale;
            zoom.transform.SetPositionAndRotation(wall - inward * .12f, Quaternion.FromToRotation(Vector3.up, inward) * Quaternion.Euler(0, 90, 0));
            foreach (var flow in zoom.GetComponentsInChildren<SplineFlow>(true))
                if (flow.name.Contains("Capillary")) { flow.prefabs = new[] { rbc }; flow.scaleRange = new Vector2(.16f, .2f); flow.spinSpeed = 40; EditorUtility.SetDirty(flow); }
            Debug.Log($"[부위] 융털 확대 모형: 벽 {wall}, 높이 {3.2f * scale:0.00}m");
        }

        // 2) 벽 속 혈관 3줄: 점막 바로 밑(돌림 주름의 뿌리 높이)을 따라 굽이치는 반투명 혈관 안을 적혈구가 굴러 흐른다.
        //    주름 사이 골에서는 드러나 보이고, 주름 볏 뒤로는 숨는다(주름 볏을 타고 넘으면 멀리서 공중에 뜬 실처럼 보인다).
        //    주변 반사(푸른 하늘빛)를 끄고 붉은 속빛을 줘서 멀리서도 피가 도는 혈관으로 읽히게 한다.
        var vessel = TransparentLit("혈관_벽", new Color(.62f, .05f, .08f, .5f), .55f, new Color(.32f, .02f, .03f));
        vessel.SetFloat("_EnvironmentReflections", 0); vessel.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        var specs = new[] { (150f, .14f, .5f), (212f, .38f, .78f), (32f, .6f, .93f) };
        var root = (GameObject.Find("벽_혈관") ?? new GameObject("벽_혈관")).transform;
        for (int k = 0; k < specs.Length; k++)
        {
            var (deg, from, to) = specs[k];
            var pts = new List<Vector3>();
            const int n = 24;
            for (int j = 0; j <= n; j++)
            {
                float t = Mathf.Lerp(from, to, (float)j / n);
                Vector3 center = path.EvaluatePosition(t);
                float ang = (deg + 10 * Mathf.Sin(j * .7f + k * 2)) * Mathf.Deg2Rad;
                var dir = Vector3.right * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang);
                pts.Add(center + dir * (3.15f - .12f)); // 관 기본 반지름(주름 없는 골 바닥) 바로 안쪽
            }
            var go = root.Find($"혈관_{k}")?.gameObject;
            if (go == null) { go = new GameObject($"혈관_{k}"); go.transform.SetParent(root, false); }
            var mesh = Tube(t => Catmull(pts, t), t => .15f * (1 - .25f * Mathf.Pow(2 * t - 1, 8)), n * 5, 12);
            mesh.name = $"혈관_{k}";
            SaveMesh($"{Folder}/혈관_{k}.asset", ref mesh);
            Ensure<MeshFilter>(go).sharedMesh = mesh;
            var mr = Ensure<MeshRenderer>(go); mr.sharedMaterial = vessel; mr.shadowCastingMode = ShadowCastingMode.Off;
            var spline = FlowSpline("흐름", pts.Where((_, j) => j % 2 == 0).ToList(), go.transform);
            var blood = Ensure<SplineFlow>(spline.gameObject);
            blood.spline = spline; blood.prefabs = new[] { rbc }; blood.count = 10; blood.scaleRange = new Vector2(.17f, .22f);
            blood.loopSeconds = spline.CalculateLength() / .55f; blood.wobbleRadius = .025f; blood.spinSpeed = 35; blood.alignToPath = true; blood.startHidden = false;
            EditorUtility.SetDirty(blood);
        }

        // 3) 포도당: 영양소 역할은 그대로, 바이러스로 보이던 가시 공 대신 육각 고리 분자.
        var glucose = GlucosePrefab();
        foreach (var absorb in Object.FindObjectsByType<NutrientAbsorption>(FindObjectsSortMode.None))
        {
            foreach (var kind in absorb.kinds ?? new NutrientAbsorption.NutrientKind[0]) if (kind.id == "Glucose") kind.prefab = glucose;
            EditorUtility.SetDirty(absorb);
        }
    }

    static float NearestT(SplineContainer path, float z)
    {
        float lo = 0, hi = 1;
        for (int k = 0; k < 30; k++) { float mid = (lo + hi) * .5f; if (((Vector3)path.EvaluatePosition(mid)).z < z) lo = mid; else hi = mid; }
        return (lo + hi) * .5f;
    }

    /// <summary>
    /// 사용자의 실사 적혈구 모형(Assets/Enviroment/red_blood_cells_erythrocytes.glb — Blood 프리팹 35개 묶음의 원본, 가운데가 오목한 원반).
    /// 지름 1m·가운데 원점으로 맞추고 GPU 인스턴싱 재질을 써서 여러 개를 그려도 가볍게 한다.
    /// </summary>
    static GameObject RbcPrefab()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enviroment/red_blood_cells_erythrocytes.glb");
        if (model == null) { Debug.LogWarning("[부위] 실사 적혈구 모형 없음 → Meshy 적혈구 사용"); return Prefab("Char_RedBloodCell"); }
        var root = new GameObject("적혈구_실사");
        var cell = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        PrefabUtility.UnpackPrefabInstance(cell, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); // 재질 바꾼 것이 그대로 저장되게
        var b = Bounds(cell);
        cell.transform.localScale *= 1f / Mathf.Max(1e-4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        b = Bounds(cell); cell.transform.position -= b.center;
        foreach (var r in cell.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sharedMaterials = r.sharedMaterials.Select(src =>
            {
                if (src == null) return src;
                string path = $"{Folder}/적혈구_실사_{src.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, path); } else mat.CopyPropertiesFromMaterial(src);
                mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
            }).ToArray();
        }
        return SavePrefab(root, "적혈구_실사");
    }

    // ───────────────────────────── 분자·덩어리 모형 ─────────────────────────────

    class Parts { public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<int> t = new List<int>(); }

    static void Ball(Parts p, Vector3 c, float r, int lon = 14, int lat = 9)
    {
        int start = p.v.Count;
        for (int j = 0; j <= lat; j++)
        for (int i = 0; i <= lon; i++)
        {
            float th = Mathf.PI * j / lat, ph = Mathf.PI * 2 * i / lon;
            var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
            p.v.Add(c + d * r); p.n.Add(d);
            if (i < lon && j < lat) { int k = start + j * (lon + 1) + i, k2 = k + lon + 1; p.t.AddRange(new[] { k, k + 1, k2, k + 1, k2 + 1, k2 }); }
        }
    }

    static void Stick(Parts p, Vector3 a, Vector3 b, float r, int sides = 8)
    {
        var axis = (b - a).normalized;
        var s = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized; var u = Vector3.Cross(axis, s);
        int start = p.v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float ph = Mathf.PI * 2 * i / sides; var d = s * Mathf.Cos(ph) + u * Mathf.Sin(ph);
            p.v.Add(a + d * r); p.n.Add(d); p.v.Add(b + d * r); p.n.Add(d);
            if (i < sides) { int k = start + i * 2; p.t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
    }

    /// <summary>
    /// 포도당 분자(고리 모양): 탄소 5개와 산소 1개가 육각 고리(의자꼴)를 이루고, 탄소마다 -OH, 끝 탄소에 -CH2OH 곁가지가 붙는다.
    /// 탄소는 꿀빛, 산소는 빨강, 수소는 흰색. 지름 약 1.9(예전 모형과 같은 크기).
    /// </summary>
    static GameObject GlucosePrefab()
    {
        Parts carbon = new Parts(), oxygen = new Parts(), hydrogen = new Parts(), bond = new Parts();
        const float ring = .42f, cr = .13f, or = .115f, hr = .065f, br = .038f;
        var atoms = new Vector3[6];
        for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3; atoms[k] = new Vector3(Mathf.Cos(a) * ring, (k % 2 == 0 ? .07f : -.07f), Mathf.Sin(a) * ring); }
        for (int k = 0; k < 6; k++) { Ball(k == 5 ? oxygen : carbon, atoms[k], k == 5 ? or : cr); Stick(bond, atoms[k], atoms[(k + 1) % 6], br); }
        for (int k = 0; k < 4; k++)
        {
            var radial = new Vector3(atoms[k].x, 0, atoms[k].z).normalized;
            var dir = (radial + (k % 2 == 0 ? Vector3.up : Vector3.down) * .75f).normalized;
            var o = atoms[k] + dir * .33f; var h = o + (dir + radial * .5f).normalized * .18f;
            Stick(bond, atoms[k], o, br); Stick(bond, o, h, br * .8f); Ball(oxygen, o, or * .8f); Ball(hydrogen, h, hr);
        }
        {
            var radial = new Vector3(atoms[4].x, 0, atoms[4].z).normalized;
            var c6 = atoms[4] + (radial * .75f + Vector3.up * .65f).normalized * .36f;
            var o6 = c6 + (radial * .3f + Vector3.up).normalized * .3f; var h6 = o6 + radial * .17f;
            Stick(bond, atoms[4], c6, br); Stick(bond, c6, o6, br); Stick(bond, o6, h6, br * .8f);
            Ball(carbon, c6, cr * .9f); Ball(oxygen, o6, or * .8f); Ball(hydrogen, h6, hr);
        }
        var groups = new[] { (carbon, Lit("포도당_탄소", new Color(.98f, .76f, .28f), .55f)), (oxygen, Lit("포도당_산소", new Color(.88f, .24f, .2f), .5f)),
                             (hydrogen, Lit("포도당_수소", new Color(.95f, .95f, .93f), .5f)), (bond, Lit("포도당_결합", new Color(.86f, .82f, .72f), .4f)) };
        var mesh = new Mesh { name = "포도당_분자", subMeshCount = groups.Length };
        var v = new List<Vector3>(); var nrm = new List<Vector3>();
        foreach (var (p, _) in groups) { v.AddRange(p.v); nrm.AddRange(p.n); }
        mesh.SetVertices(v); mesh.SetNormals(nrm);
        int offset = 0;
        for (int g = 0; g < groups.Length; g++) { mesh.SetTriangles(groups[g].Item1.t.Select(i => i + offset).ToList(), g); offset += groups[g].Item1.v.Count; }
        mesh.RecalculateBounds();
        SaveMesh($"{Folder}/포도당_분자.asset", ref mesh);
        var root = new GameObject("포도당_분자");
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = root.AddComponent<MeshRenderer>(); mr.sharedMaterials = groups.Select(g => g.Item2).ToArray(); mr.shadowCastingMode = ShadowCastingMode.Off;
        var size = mesh.bounds.size;
        root.transform.localScale = Vector3.one * 1.9f / Mathf.Max(.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        return SavePrefab(root, "포도당_분자");
    }

    /// <summary>
    /// 대장 찌꺼기 3단계(묽음 → 무름 → 단단함): 과자처럼 보이던 모형 대신 물기 많은 덩어리로 바꾸고, 잠수정 앞 내용물에 반쯤 잠겨 함께 흘러간다.
    /// 물이 빠지는 것을 보여 주려고 덩어리에서 물방울이 떠올라 벽 쪽으로 흩어진다.
    /// </summary>
    static void WasteLumps()
    {
        var waste = GameObject.Find("WasteStages");
        var sw = waste != null ? waste.GetComponent<RideProgressSwitcher>() : null;
        if (sw == null || sw.stages == null) return;
        var capsule = waste.transform.parent;
        // 내용물 수면(눈 아래 2.55m)에 반쯤 잠긴다. 앞쪽 멀리 두면 계기판에, 오른쪽은 조종대에 가려지므로 왼쪽 앞 유리 바닥 너머.
        if (capsule != null) waste.transform.position = capsule.position + capsule.rotation * new Vector3(-1.5f, -2.45f, 1.8f);
        var specs = new[]
        {
            ("묽음", new Color(.56f, .4f, .17f), .88f, new Vector3(1.25f, .32f, 1.1f), .16f, 0),
            ("무름", new Color(.43f, .28f, .12f), .62f, new Vector3(1.05f, .62f, .95f), .12f, 1),
            ("단단함", new Color(.3f, .19f, .08f), .38f, new Vector3(1.5f, .62f, .68f), .07f, 2),
        };
        for (int i = 0; i < Mathf.Min(specs.Length, sw.stages.Length); i++)
        {
            var stage = sw.stages[i]; if (stage == null) continue;
            var (name, color, wet, size, bumps, kind) = specs[i];
            bool was = stage.activeSelf; stage.SetActive(true);
            var lump = stage.transform.Find("덩어리");
            foreach (var r in stage.GetComponentsInChildren<MeshRenderer>(true)) if (lump == null || !r.transform.IsChildOf(lump)) r.enabled = false;
            if (lump == null) { lump = new GameObject("덩어리").transform; lump.SetParent(stage.transform, false); }
            var mesh = LumpMesh($"찌꺼기_{name}", size, bumps, kind, 70 + i);
            Ensure<MeshFilter>(lump.gameObject).sharedMesh = mesh;
            var mr = Ensure<MeshRenderer>(lump.gameObject); mr.sharedMaterial = Lit($"찌꺼기_{name}", color, wet); mr.shadowCastingMode = ShadowCastingMode.Off; mr.enabled = true;
            lump.rotation = waste.transform.rotation;
            lump.localScale = Vector3.one / Mathf.Max(1e-4f, stage.transform.lossyScale.x);
            lump.position = waste.transform.position;
            stage.SetActive(was);
        }
        var f = Ensure<GentleFloat>(waste); f.bob = .05f; f.period = 3.2f; f.drift = .03f; f.tilt = 5f; f.spin = 0;
        var water = Droplet("수분", new Color(.75f, .88f, 1f, .45f));
        var ps = Ambient("찌꺼기_수분_흡수", waste.transform.position + Vector3.up * .25f, Quaternion.Euler(-90, 0, 0), water, 5f, new Vector2(.04f, .09f), new Vector2(.25f, .5f), -.04f, new Vector2(2f, 3f), ParticleSystemShapeType.Hemisphere, Vector3.one, false);
        var shape = ps.shape; shape.radius = .5f;
        ps.transform.SetParent(waste.transform, true);
    }

    /// <summary>울퉁불퉁한 덩어리(원점 가운데, size 크기). kind 0 = 납작한 물기 덩어리, 1 = 둥근 덩어리, 2 = 마디가 진 길쭉한 덩어리.</summary>
    static Mesh LumpMesh(string name, Vector3 size, float bumps, int kind, int seed)
    {
        const int lon = 40, lat = 24;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int j = 0; j <= lat; j++)
        for (int i = 0; i <= lon; i++)
        {
            float th = Mathf.PI * j / lat, ph = Mathf.PI * 2 * i / lon;
            var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
            float noise = Fbm((float)i / lon, (float)j / lat, 6, 4, seed) - .5f;
            var p = Vector3.Scale(d * .5f, size) * (1 + bumps * 2 * noise);
            if (kind == 0) p.y = Mathf.Max(p.y, -size.y * .2f); // 물기 덩어리는 바닥이 퍼져 납작하다
            if (kind == 2) p *= 1 - .14f * (Mathf.Exp(-Mathf.Pow((p.x - size.x * .17f) / .06f, 2)) + Mathf.Exp(-Mathf.Pow((p.x + size.x * .17f) / .06f, 2))); // 마디 두 개
            v.Add(p);
            if (i < lon && j < lat) { int k = j * (lon + 1) + i, k2 = k + lon + 1; tri.AddRange(new[] { k, k + 1, k2, k + 1, k2 + 1, k2 }); }
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveMesh($"{Folder}/{name}.asset", ref mesh);
        return mesh;
    }
}
#endif
