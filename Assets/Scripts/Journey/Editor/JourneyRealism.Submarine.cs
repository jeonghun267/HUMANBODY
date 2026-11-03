#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// 관찰 잠수정: 탁자 같던 발판·난간 대신 탐험가를 감싸는 둥근 유리 잠수정에 태운다.
/// 눈은 관 한가운데(이동 경로)에 오고, 유리창 너머로 점막을 본다. 감싸는 뼈대는 멀미를 줄이는 고정 기준이 된다.
/// </summary>
public static partial class JourneyRealism
{
    const float EyeHeight = 1.6f; // 좌석(발) → 눈
    const float PodRadius = 1.7f;

    static void Submarines(string id)
    {
        if (id == "Stomach") StomachRide();
        foreach (var ride in Object.FindObjectsByType<CapsuleRide>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Pod(ride, id);
    }

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        return t;
    }

    static void Pod(CapsuleRide ride, string id)
    {
        var capsule = ride.transform;
        var rail = capsule.Find("Railing"); if (rail != null) rail.gameObject.SetActive(false);
        foreach (var filter in capsule.GetComponentsInChildren<MeshFilter>(true))
            if (AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith("Prop_ExplorerCapsule.glb")) { var r = filter.GetComponent<Renderer>(); if (r != null) r.enabled = false; }

        var pod = Child(capsule, "관찰_잠수정");
        // 캡슐 모형의 크기 보정이 자식에 곱해지지 않도록 실제 미터 단위로 되돌린다.
        var ls = capsule.lossyScale;
        pod.localPosition = Vector3.zero; pod.localRotation = Quaternion.identity;
        pod.localScale = new Vector3(1 / Mathf.Max(1e-4f, ls.x), 1 / Mathf.Max(1e-4f, ls.y), 1 / Mathf.Max(1e-4f, ls.z));

        // 눈 = 이동 경로(관 한가운데). 좌석은 그만큼 아래, 안내 병아리는 오른쪽 앞 조종대 위.
        if (ride.seatPoint != null) ride.seatPoint.position = capsule.position - Vector3.up * EyeHeight;
        if (ride.guideSeatPoint != null) ride.guideSeatPoint.position = capsule.position + capsule.rotation * new Vector3(.62f, -.32f, .72f);

        var screen = PodVisual(pod);

        // 대장: 굳어 가는 찌꺼기는 잠수정 안이 아니라 앞쪽 바닥 내용물 위에 떠서 함께 나아간다.
        var waste = capsule.Find("WasteStages");
        if (waste != null) waste.position = capsule.position + capsule.rotation * new Vector3(0, -2.3f, 3.6f);
        // 소장: 흡수 진행 표시는 계기판 화면 자리로.
        var progress = capsule.Find("ProgressUI");
        if (progress != null) progress.SetPositionAndRotation(screen.position + screen.forward * -.01f, screen.rotation);
    }

    /// <summary>잠수정 겉모습(유리창·뼈대·바닥·조종대). pod 의 원점 = 탐험가 눈. 계기판 화면 자리를 돌려준다.</summary>
    static Transform PodVisual(Transform pod)
    {
        var frame = CockpitMaterial("잠수정_뼈대", new Color(.17f, .21f, .24f), .6f, 0);
        var floorMat = CockpitMaterial("잠수정_바닥", new Color(.09f, .11f, .13f), .3f, 0);
        var glow = CockpitMaterial("잠수정_빛띠", new Color(.05f, .12f, .15f), .2f, .7f);

        var glass = Child(pod, "관찰창");
        var gf = glass.GetComponent<MeshFilter>(); if (gf == null) gf = glass.gameObject.AddComponent<MeshFilter>();
        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gf.sharedMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        var gr = glass.GetComponent<MeshRenderer>(); if (gr == null) gr = glass.gameObject.AddComponent<MeshRenderer>();
        gr.sharedMaterial = GlassMaterial(); gr.shadowCastingMode = ShadowCastingMode.Off; gr.receiveShadows = false;
        glass.localPosition = new Vector3(0, -.05f, 0); glass.localScale = Vector3.one * PodRadius * 2;

        float Lat(float y) => Mathf.Sqrt(Mathf.Max(.01f, PodRadius * PodRadius - (y + .05f) * (y + .05f)));
        Part(pod, "허리_고리", Torus(Lat(-.45f), .045f, 96, 10), frame, new Vector3(0, -.45f, 0), Quaternion.identity);
        Part(pod, "위_해치_고리", Torus(Lat(1.45f), .05f, 64, 10), frame, new Vector3(0, 1.45f, 0), Quaternion.identity);
        // 세로 뼈대는 정면을 비우고 ±45° 에 둔다(창틀처럼 시야를 나눈다).
        Part(pod, "세로_뼈대_오른쪽", Torus(PodRadius - .01f, .032f, 128, 8), frame, new Vector3(0, -.05f, 0), Quaternion.Euler(0, 45, 90));
        Part(pod, "세로_뼈대_왼쪽", Torus(PodRadius - .01f, .032f, 128, 8), frame, new Vector3(0, -.05f, 0), Quaternion.Euler(0, -45, 90));
        // 바닥은 고리 모양 발판: 가운데는 유리라 식도에서 내려갈 때 발밑 통로가 보인다.
        Part(pod, "바닥판", Arc(.3f, .62f, -180, 180, .04f, 64), floorMat, new Vector3(0, -EyeHeight - .02f, 0), Quaternion.identity);
        // 조종대: 허리 높이 앞쪽의 둥근 판과 가장자리 빛 띠
        Part(pod, "조종대", Arc(.62f, .98f, -55, 55, .07f, 40), frame, new Vector3(0, -.62f, 0), Quaternion.identity);
        Part(pod, "조종대_빛띠", Arc(.95f, .985f, -55, 55, .02f, 40), glow, new Vector3(0, -.545f, 0), Quaternion.identity);
        var screen = Child(pod, "계기판_화면");
        screen.localPosition = new Vector3(0, -.5f, .66f);
        screen.localRotation = Quaternion.LookRotation(screen.localPosition - Vector3.zero);
        return screen;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 localPos, Quaternion localRot)
    {
        var t = Child(parent, name);
        t.localPosition = localPos; t.localRotation = localRot; t.localScale = Vector3.one;
        SaveMesh($"{Folder}/잠수정_{name}.asset", ref mesh);
        var mf = t.GetComponent<MeshFilter>(); if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = t.GetComponent<MeshRenderer>(); if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    static Mesh Torus(float major, float minor, int around, int tube)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= around; i++)
        for (int j = 0; j <= tube; j++)
        {
            float a = (float)i / around * Mathf.PI * 2, b = (float)j / tube * Mathf.PI * 2;
            var ring = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v.Add(ring * (major + Mathf.Cos(b) * minor) + Vector3.up * Mathf.Sin(b) * minor);
            if (i < around && j < tube) { int k = i * (tube + 1) + j, n = k + tube + 1; tri.AddRange(new[] { k, n, k + 1, k + 1, n, n + 1 }); }
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Mesh Disc(float radius, float thickness, int around)
    {
        var v = new List<Vector3> { Vector3.zero, Vector3.down * thickness }; var tri = new List<int>();
        for (int i = 0; i <= around; i++)
        {
            float a = (float)i / around * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            v.Add(d); v.Add(d + Vector3.down * thickness);
            if (i < around) { int k = 2 + i * 2; tri.AddRange(new[] { 0, k + 2, k, 1, k + 1, k + 3, k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    /// <summary>앞(+z) 기준 from~to 도 사이의 둥근 판(조종대).</summary>
    static Mesh Arc(float inner, float outer, float from, float to, float thickness, int segments)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            v.Add(d * inner); v.Add(d * outer); v.Add(d * inner + Vector3.down * thickness); v.Add(d * outer + Vector3.down * thickness);
            if (i < segments)
            {
                int k = i * 4, n = k + 4;
                tri.AddRange(new[] { k, n, k + 1, k + 1, n, n + 1 });         // 윗면
                tri.AddRange(new[] { k + 2, k + 3, n + 2, k + 3, n + 3, n + 2 }); // 아랫면
                tri.AddRange(new[] { k + 1, n + 1, k + 3, k + 3, n + 1, n + 3 }); // 바깥 가장자리
                tri.AddRange(new[] { k, k + 2, n, n, k + 2, n + 2 });         // 안쪽 가장자리
            }
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Material CockpitMaterial(string name, Color color, float shine, float glow)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("인체탐험/잠수정 내부")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Shine", shine); mat.SetFloat("_Glow", glow);
        mat.SetColor("_GlowColor", new Color(.25f, .85f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GlassMaterial()
    {
        string path = $"{Folder}/관찰창_유리.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("인체탐험/관찰창 유리")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_DropMap", DropTexture());
        mat.SetFloat("_DropStrength", .3f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>유리 바깥에 맺힌 물방울 자료: R = 방울 모양, G = 방울 안 반사점. 가장자리가 이어지는 256 칸.</summary>
    static Texture2D DropTexture()
    {
        string path = $"{Folder}/유리_물방울.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int N = 256, cells = 6;
        var pixels = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float px = (float)x / N * cells, py = (float)y / N * cells, shape = 0, glint = 0;
            int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(py);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy, hx = Wrap(gx, cells), hy = Wrap(gy, cells);
                if (Hash(hx, hy, 71) < .35f) continue; // 빈 칸
                float r = Mathf.Lerp(.12f, .38f, Hash(hx, hy, 72));
                float fx = gx + .5f + (Hash(hx, hy, 73) - .5f) * .5f, fy = gy + .5f + (Hash(hx, hy, 74) - .5f) * .5f;
                float ex = (px - fx) / r, ey = (py - fy) / (r * 1.25f); // 아래로 살짝 늘어진 방울
                float d = Mathf.Sqrt(ex * ex + ey * ey);
                if (d > 1) continue;
                shape = Mathf.Max(shape, Smooth(1f, .72f, d) * (.45f + .55f * Smooth(.4f, .95f, d)));
                float hxp = ex + .35f, hyp = ey - .4f;
                glint = Mathf.Max(glint, Mathf.Exp(-(hxp * hxp + hyp * hyp) * 22));
            }
            pixels[y * N + x] = new Color32((byte)(shape * 255), (byte)(glint * 255), 0, 255);
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(pixels); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────────────────────── 위: 걷기 대신 잠수정으로 위액 위를 떠간다 ─────────────────────────────

    static void StomachRide()
    {
        var existing = Object.FindFirstObjectByType<CapsuleRide>();
        CapsuleRide ride = existing;
        if (ride == null)
        {
            // 분문 아래(뒤쪽)에서 출발해 위액 위를 지나 유문을 통과한다. 유문 앞에서 소화가 끝나 문이 열릴 때까지 기다린다.
            var knots = new[] { new Vector3(0, 1.7f, -4.6f), new Vector3(.9f, 1.75f, -1.2f), new Vector3(-.5f, 1.7f, 2.4f), new Vector3(.2f, 1.75f, 6f), new Vector3(0, 1.8f, 9.5f), new Vector3(0, 1.7f, 12.8f) };
            var container = new GameObject("Ride Spline").AddComponent<SplineContainer>();
            container.Spline.Clear();
            foreach (var k in knots) container.Spline.Add(new BezierKnot(k), TangentMode.AutoSmooth);
            var capsule = new GameObject("Prop_ExplorerCapsule");
            capsule.tag = "Boat";
            capsule.transform.position = knots[0];
            var cart = capsule.AddComponent<CinemachineSplineCart>();
            cart.Spline = container; cart.PositionUnits = PathIndexUnit.Normalized; cart.AutomaticDolly.Enabled = false;
            ride = capsule.AddComponent<CapsuleRide>();
            ride.cart = cart;
            ride.seatPoint = Child(capsule.transform, "SeatPoint");
            ride.guideSeatPoint = Child(capsule.transform, "GuideSeatPoint");
            ride.getOffPoint = Child(capsule.transform, "GetOffPoint");
            ride.getOffPoint.position = knots[0] + new Vector3(1.8f, -1.7f, 0);
        }
        ride.stops = new[]
        {
            new CapsuleRide.RideStop { normalizedPosition = .42f, lines = new NarrationLine[0], waitSeconds = 1f, onReached = new UnityEvent(), holdUntilReleased = true, maxHoldSeconds = 120f },
        };
        ride.autoBoard = true; ride.autoBoardDelay = 4f; ride.speed = .018f; ride.advanceOnArrive = true;

        var gate = Object.FindFirstObjectByType<SphincterGate>();
        if (gate != null)
        {
            for (int i = gate.onOpened.GetPersistentEventCount() - 1; i >= 0; i--)
                if (gate.onOpened.GetPersistentTarget(i) is CapsuleRide) UnityEventTools.RemovePersistentListener(gate.onOpened, i);
            UnityEventTools.AddPersistentListener(gate.onOpened, ride.Release);
        }
        // 잠수정에 탄 동안은 몸 충돌이 꺼져 출구 문이 감지하지 못하므로, 도착 처리로 다음 단계에 간다.
        foreach (var portal in Object.FindObjectsByType<StageExitPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None)) portal.gameObject.SetActive(false);

        // 잠수정 길목에 있던 단백질 사슬을 양옆 위액 위로 비킨다.
        var cut = Object.FindFirstObjectByType<EnzymeCutSequence>();
        if (cut != null && cut.proteinChains != null)
        {
            var spots = new[] { new Vector3(-3.4f, 1f, 4.2f), new Vector3(3.3f, 1f, 5.4f), new Vector3(-3f, 1.1f, 6.8f) };
            for (int i = 0; i < cut.proteinChains.Length && i < spots.Length; i++) if (cut.proteinChains[i] != null) cut.proteinChains[i].position = spots[i];
        }
    }
}
#endif
