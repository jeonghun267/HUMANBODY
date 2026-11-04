using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 소장 벽을 빽빽하게 덮은 작은 융털. 이 장면은 장을 약 250배 키운 크기라 융털(실제 약 1mm)은 높이 15~24cm 손가락이다.
/// 벽 메시 표면에 고르게 뿌려(바닥 미즙 물길 아래는 뺀다) 3m 구간마다 메시 하나로 묶고,
/// 탐험가 눈 가까운 구간만 그린다(먼 곳은 벽의 융털 결 질감이 대신한다). 큰 메시 파일을 저장하지 않도록 장면을 열 때 만든다.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("인체탐험/융털 카펫")]
public class VilliCarpet : MonoBehaviour
{
    public MeshFilter wall;
    public Material material;
    [Tooltip("융털 사이 간격(m)")] public float spacing = .15f;
    public Vector2 height = new Vector2(.14f, .24f), radius = new Vector2(.035f, .05f);
    [Tooltip("이 각도보다 위를 향한 바닥(미즙 물길)은 비운다 — 위쪽 방향과의 내적")] public float floorSkip = .7f;
    [Tooltip("내용물이 흐르는 쪽(융털이 살짝 눕는다)")] public Vector3 flow = Vector3.forward;
    public float chunkLength = 3f;
    [Tooltip("눈에서 이 거리(m) 안의 구간만 그린다")] public float drawRange = 11f;
    public int seed = 7;

    readonly List<MeshRenderer> chunks = new List<MeshRenderer>();
    readonly List<Mesh> meshes = new List<Mesh>();
    public int VilliCount { get; private set; }
    public IReadOnlyList<MeshRenderer> Chunks => chunks;

    void OnEnable() { if (chunks.Count == 0) Build(); }
    void OnDisable() { Clear(); }

    void Clear()
    {
        foreach (var r in chunks) if (r != null) { if (Application.isPlaying) Destroy(r.gameObject); else DestroyImmediate(r.gameObject); }
        foreach (var m in meshes) if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
        chunks.Clear(); meshes.Clear();
    }

    /// <summary>벽 표면을 넓이에 비례해 뿌리고, 구간(벽 길이 방향 = 흐름 방향)마다 손가락 메시를 모은다.</summary>
    public void Build()
    {
        Clear();
        if (wall == null || wall.sharedMesh == null || material == null || !wall.sharedMesh.isReadable) return;
        var mesh = wall.sharedMesh; var m = wall.transform.localToWorldMatrix;
        var wv = mesh.vertices; var wn = mesh.normals; var wt = mesh.triangles;
        var rng = new System.Random(seed);
        float Rand() => (float)rng.NextDouble();
        var dir = flow.normalized;
        float minAlong = float.MaxValue, maxAlong = float.MinValue;
        var spots = new List<(Vector3 p, Vector3 n, float along)>();
        float perArea = 1f / (spacing * spacing);
        for (int i = 0; i < wt.Length; i += 3)
        {
            Vector3 a = m.MultiplyPoint3x4(wv[wt[i]]), b = m.MultiplyPoint3x4(wv[wt[i + 1]]), c = m.MultiplyPoint3x4(wv[wt[i + 2]]);
            float area = Vector3.Cross(b - a, c - a).magnitude * .5f;
            float want = area * perArea; int count = (int)want + (Rand() < want - (int)want ? 1 : 0);
            if (count == 0) continue;
            Vector3 na = m.MultiplyVector(wn[wt[i]]), nb = m.MultiplyVector(wn[wt[i + 1]]), nc = m.MultiplyVector(wn[wt[i + 2]]);
            for (int k = 0; k < count; k++)
            {
                float u = Rand(), v = Rand(); if (u + v > 1) { u = 1 - u; v = 1 - v; }
                var p = a + (b - a) * u + (c - a) * v;
                var n = (na * (1 - u - v) + nb * u + nc * v).normalized;
                if (Vector3.Dot(n, Vector3.up) > floorSkip) continue; // 바닥 미즙 물길
                float along = Vector3.Dot(p, dir);
                minAlong = Mathf.Min(minAlong, along); maxAlong = Mathf.Max(maxAlong, along);
                spots.Add((p, n, along));
            }
        }
        VilliCount = spots.Count;
        if (spots.Count == 0) return;

        int chunkCount = Mathf.Max(1, Mathf.CeilToInt((maxAlong - minAlong) / chunkLength));
        var buckets = new List<(Vector3 p, Vector3 n, float along)>[chunkCount];
        for (int i = 0; i < chunkCount; i++) buckets[i] = new List<(Vector3, Vector3, float)>();
        foreach (var s in spots) buckets[Mathf.Min(chunkCount - 1, (int)((s.along - minAlong) / chunkLength))].Add(s);

        for (int ci = 0; ci < chunkCount; ci++)
        {
            if (buckets[ci].Count == 0) continue;
            var built = Fingers(buckets[ci], Rand, dir);
            built.name = $"융털_카펫_{ci}";
            meshes.Add(built);
            var go = new GameObject(built.name) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.transform.SetParent(transform, false); // 정점이 세계 좌표라 이 물체는 원점·회전 없음·크기 1 에 둔다(편집기 빌더가 그렇게 만든다)
            go.AddComponent<MeshFilter>().sharedMesh = built;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            chunks.Add(r);
        }
    }

    /// <summary>끝이 둥근 사각 손가락(삼각형 12개 — 15cm 크기라 각이 보이지 않는다): 밑동은 벽 속 2cm, 끝은 흐름 쪽으로 조금 휜다. 정점은 세계 좌표.</summary>
    static Mesh Fingers(List<(Vector3 p, Vector3 n, float along)> spots, System.Func<float> rand, Vector3 flow)
    {
        const int sides = 4;
        var v = new List<Vector3>(spots.Count * 11); var nrm = new List<Vector3>(spots.Count * 11); var t = new List<int>(spots.Count * 45);
        foreach (var (p, n, _) in spots)
        {
            float h = Mathf.Lerp(.14f, .24f, rand()), r = Mathf.Lerp(.035f, .05f, rand());
            var jitter = new Vector3(rand() - .5f, rand() - .5f, rand() - .5f);
            var lean = Vector3.ProjectOnPlane(flow, n).normalized * (.15f + .2f * rand()) + Vector3.ProjectOnPlane(jitter, n) * .2f;
            var axis = (n + lean).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > .9f ? Vector3.right : Vector3.up).normalized; var up = Vector3.Cross(side, axis);
            float twist = rand() * Mathf.PI * 2;
            int start = v.Count;
            Vector3 basePt = p - n * .02f, mid = p + axis * (h * .72f), tip = p + axis * h;
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = twist + i * Mathf.PI * 2 / sides; var d = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add((ring == 0 ? basePt : mid) + d * (ring == 0 ? r : r * .85f)); nrm.Add((d + axis * (ring == 0 ? 0 : .35f)).normalized);
                }
            v.Add(tip); nrm.Add(axis);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides, b0 = start + i, b1 = start + j, m0 = start + sides + i, m1 = start + sides + j, apex = start + sides * 2;
                t.Add(b0); t.Add(m0); t.Add(b1); t.Add(b1); t.Add(m0); t.Add(m1); // 옆면
                t.Add(m0); t.Add(apex); t.Add(m1);                                 // 둥근 끝
            }
        }
        var mesh = new Mesh { indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
        return mesh;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return; // 편집기(촬영)에서는 모두 보인다
        var eye = JourneyEye.Get();
        if (eye == null) return;
        float range2 = drawRange * drawRange; var at = eye.position;
        foreach (var r in chunks) { bool show = r.bounds.SqrDistance(at) < range2; if (r.enabled != show) r.enabled = show; }
    }
}
