#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// 기관마다 흐르는 액체: 벽을 타고 흐르는 점액막, 바닥에 고이거나 흘러가는 내용물(위액·미즙·대장 내용물),
/// 그리고 침·위액·담즙·이자액·점액 방울과 거품 입자.
/// </summary>
public static partial class JourneyRealism
{
    static void Fluids(string id)
    {
        WallFilm(id);
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        var path = ride != null && ride.cart != null ? ride.cart.Spline : null;
        if (id == "Stomach") Lake();
        if (id == "SmallIntestine" && path != null) River(id, path, "미즙_물길", .85f, new Color(.74f, .56f, .2f, .78f), new Color(.32f, .2f, .05f), .22f);
        if (id == "LargeIntestine" && path != null) River(id, path, "대장_내용물", .6f, new Color(.5f, .34f, .17f, .86f), new Color(.2f, .12f, .05f), .08f);
        Secretions(id, path);
    }

    /// <summary>벽을 타고 흐르는 액체 막: 입·식도·위는 아래로, 장은 내용물이 가는 방향으로.</summary>
    static void WallFilm(string id)
    {
        var mat = Mucosa(id);
        // 사방 벽 무늬가 한꺼번에 움직이면 몸이 움직이는 착각(멀미)이 생기므로, 반사만 아주 느리게 흐른다.
        (Vector3 dir, float speed, float strength) film = id switch
        {
            "Mouth" => (Vector3.down, .04f, .25f),
            "Esophagus" => (Vector3.down, .1f, .35f),
            "Stomach" => (Vector3.down, .04f, .3f),
            "SmallIntestine" => (Vector3.forward, .06f, .28f),
            _ => (Vector3.forward, .04f, .25f),
        };
        mat.SetVector("_FlowDir", film.dir); mat.SetFloat("_FlowSpeed", film.speed); mat.SetFloat("_FlowStrength", film.strength);
        EditorUtility.SetDirty(mat);
    }

    static Material LiquidSurface(string name, Color color, Color deep, float speed)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("인체탐험/흐르는 액체")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color); mat.SetColor("_DeepColor", deep); mat.SetFloat("_FlowSpeed", speed);
        mat.SetTexture("_DetailMap", DetailTexture("매끈"));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SurfaceObject(string name, Mesh mesh, Material material)
    {
        SaveMesh($"{Folder}/{name}.asset", ref mesh);
        var go = GameObject.Find(name) ?? new GameObject(name);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    /// <summary>위 바닥에 고인 위액. 낮은 위주름 사이 골에 차고, 볏은 수면 위로 드러난다. 잠수정은 이 위에 반쯤 잠겨 떠간다.</summary>
    static void Lake()
    {
        const float level = .4f, halfX = 11f, zFrom = -10f, zTo = 13f; const int nx = 22, nz = 46;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int j = 0; j <= nz; j++)
        for (int i = 0; i <= nx; i++)
        {
            float x = Mathf.Lerp(-halfX, halfX, (float)i / nx), z = Mathf.Lerp(zFrom, zTo, (float)j / nz);
            v.Add(new Vector3(x, level, z)); uv.Add(new Vector2(x, z));
            if (i < nx && j < nz) { int k = j * (nx + 1) + i, n = k + nx + 1; tri.AddRange(new[] { k, n, k + 1, k + 1, n, n + 1 }); }
        }
        var mesh = new Mesh { name = "위액 웅덩이" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SurfaceObject("위액_웅덩이", mesh, LiquidSurface("위액_수면", new Color(.74f, .74f, .4f, .8f), new Color(.24f, .2f, .06f), .06f)); // 밑 점막 결이 비쳐 벌집처럼 보이지 않게 진하게, 연둣빛 노란 위액
    }

    /// <summary>관 바닥을 따라 흘러가는 내용물 수면. 이동 경로를 따라가며, 가장자리는 벽 속에 묻힌다. uv 는 미터(가로, 흐른 거리).</summary>
    static void River(string id, SplineContainer path, string name, float depth, Color color, Color deep, float speed)
    {
        float length = path.CalculateLength(), radius = 3.15f, half = 3.1f;
        int rows = Mathf.CeilToInt(length * 1.24f / .5f);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int j = 0; j <= rows; j++)
        {
            float t = Mathf.Lerp(-.12f, 1.12f, (float)j / rows);
            Vector3 start = path.EvaluatePosition(0), end = path.EvaluatePosition(1);
            Vector3 center, tangent;
            if (t < 0) { tangent = ((Vector3)path.EvaluateTangent(0)).normalized; center = start + tangent * t * length; }
            else if (t > 1) { tangent = ((Vector3)path.EvaluateTangent(1)).normalized; center = end + tangent * (t - 1) * length; }
            else { center = path.EvaluatePosition(t); tangent = ((Vector3)path.EvaluateTangent(t)).normalized; }
            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            float y = center.y - radius + depth;
            for (int s = 0; s <= 1; s++)
            {
                float side = s == 0 ? -half : half;
                var p = center + right * side; p.y = y;
                v.Add(p); uv.Add(new Vector2(side, t * length));
            }
            if (j < rows) { int k = j * 2; tri.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SurfaceObject(name, mesh, LiquidSurface(name + "_수면", color, deep, speed));
    }

    // ───────────────────────────── 분비물 입자 ─────────────────────────────

    static Material Droplet(string name, Color color, bool strand = false, float rim = 1f, float highlight = 1.3f)
    {
        string path = $"{Folder}/방울_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("인체탐험/점액 방울")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Strand", strand ? 1 : 0); mat.SetFloat("_Rim", rim); mat.SetFloat("_Highlight", highlight);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>끈적한 분비물: 느리게 흘러나와 중력에 늘어지고(늘인 입자), 가닥(꼬리)을 남긴다. 발생 시점·양은 기존 연출 그대로 둔다.</summary>
    static void Viscous(ParticleSystem ps, Material drop, Material strand, Vector2 size, Vector2 speed, float gravity, Vector2 life, float stretch = .1f)
    {
        var main = ps.main;
        main.startColor = Color.white;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.gravityModifier = gravity;
        var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .6f), new Keyframe(.15f, 1), new Keyframe(1, .7f)));
        var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = Mathf.Max(.6f, speed.y * 1.6f); limit.dampen = .12f; // 점성
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = drop; renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = stretch; renderer.lengthScale = 1.15f; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var trails = ps.trails;
        trails.enabled = strand != null;
        if (strand != null)
        {
            trails.mode = ParticleSystemTrailMode.PerParticle; trails.ratio = .35f; trails.lifetime = .25f; trails.minVertexDistance = .04f;
            trails.dieWithParticles = true; trails.inheritParticleColor = true; trails.sizeAffectsWidth = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(1, 0)));
            renderer.trailMaterial = strand;
        }
        EditorUtility.SetDirty(ps);
    }

    /// <summary>계속 맺혀 흐르는 방울·거품용 입자(새로 만드는 것). 장면 시작부터 계속 나온다.</summary>
    static ParticleSystem Ambient(string name, Vector3 position, Quaternion rotation, Material drop, float rate, Vector2 size, Vector2 speed, float gravity, Vector2 life, ParticleSystemShapeType shape, Vector3 shapeScale, bool stretch)
    {
        var go = GameObject.Find(name) ?? new GameObject(name);
        go.transform.SetPositionAndRotation(position, rotation);
        var ps = go.GetComponent<ParticleSystem>(); if (ps == null) ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white; main.maxParticles = 120;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.gravityModifier = gravity;
        var emission = ps.emission; emission.rateOverTime = rate;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = shape; sh.scale = shapeScale; sh.angle = 12; sh.radius = .1f;
        var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(.2f, 1), new Keyframe(.85f, 1), new Keyframe(1, 0)));
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = drop; renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = .12f; renderer.lengthScale = 1.1f; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        EditorUtility.SetDirty(ps);
        return ps;
    }

    static void Secretions(string id, SplineContainer path)
    {
        var saliva = Droplet("침", new Color(.86f, .93f, 1f, .5f));
        var salivaStrand = Droplet("침_가닥", new Color(.88f, .94f, 1f, .45f), strand: true);
        var mucus = Droplet("점액", new Color(.92f, .9f, .86f, .5f));
        var acid = Droplet("위산", new Color(1f, .95f, .62f, .45f));
        var pepsin = Droplet("펩신", new Color(.86f, 1f, .82f, .42f));
        var bile = Droplet("담즙", new Color(.62f, .72f, .12f, .85f));
        var bileStrand = Droplet("담즙_가닥", new Color(.6f, .7f, .12f, .75f), strand: true);
        var pancreas = Droplet("이자액", new Color(.9f, .95f, 1f, .42f));
        var bubble = Droplet("거품", new Color(1f, 1f, .92f, .3f), rim: 1.7f, highlight: 1.8f);

        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = ps.name;
            if (n.StartsWith("Saliva_")) Viscous(ps, saliva, salivaStrand, new Vector2(.05f, .13f), new Vector2(.25f, .7f), .55f, new Vector2(1.2f, 2.2f));
            else if (n == "GlandSecretion_0") Viscous(ps, mucus, null, new Vector2(.06f, .12f), new Vector2(.15f, .35f), .7f, new Vector2(2f, 3f));
            else if (n == "GlandSecretion_1") Viscous(ps, acid, null, new Vector2(.05f, .1f), new Vector2(.15f, .35f), .8f, new Vector2(2f, 3f));
            else if (n == "GlandSecretion_2") Viscous(ps, pepsin, null, new Vector2(.05f, .1f), new Vector2(.15f, .35f), .8f, new Vector2(2f, 3f));
            else if (n.StartsWith("위산_분비_")) { var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = acid; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .1f; var m = ps.main; m.startColor = Color.white; }
            else if (n == "BileFlow") Viscous(ps, bile, bileStrand, new Vector2(.07f, .14f), new Vector2(.4f, .9f), .35f, new Vector2(1.5f, 2.5f));
            else if (n == "PancreaticJuice") Viscous(ps, pancreas, null, new Vector2(.05f, .1f), new Vector2(.4f, .8f), .3f, new Vector2(1.2f, 2f));
            else if (n == "액체_접촉_물방울") ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = id == "SmallIntestine" ? bile : id == "Stomach" ? acid : saliva;
        }

        if (id == "Stomach")
        {
            // 위액 수면에서 올라오는 거품
            Ambient("위액_거품", new Vector3(0, .42f, 1.5f), Quaternion.Euler(-90, 0, 0), bubble, 14, new Vector2(.04f, .12f), new Vector2(.05f, .15f), -.02f, new Vector2(1.5f, 3f), ParticleSystemShapeType.Box, new Vector3(14, 16, .1f), false);
        }
        if (id == "Esophagus" && path != null)
        {
            // 식도 벽을 타고 흘러내리는 점액 방울
            for (int i = 0; i < 6; i++)
            {
                float t = .1f + i * .15f, angle = i * 2.4f;
                Vector3 c = path.EvaluatePosition(t);
                var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var wall = c + dir * (TubeRadius("Esophagus", 3.4f, angle, t * path.CalculateLength()) - .25f);
                Ambient($"점액_흐름_{i}", wall, Quaternion.LookRotation(Vector3.down), mucus, 5, new Vector2(.06f, .14f), new Vector2(.3f, .8f), .5f, new Vector2(2f, 3.5f), ParticleSystemShapeType.Cone, Vector3.one, true);
            }
        }
        if ((id == "SmallIntestine" || id == "LargeIntestine") && path != null)
        {
            float length = path.CalculateLength();
            int count = Mathf.Max(2, Mathf.FloorToInt(length / 9f));
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5f) / count;
                Vector3 c = path.EvaluatePosition(t);
                if (id == "SmallIntestine")
                {
                    float level = c.y - 3.15f + .87f;
                    Ambient($"장_거품_{i}", new Vector3(c.x, level, c.z), Quaternion.Euler(-90, 0, 0), bubble, 5, new Vector2(.04f, .1f), new Vector2(.05f, .12f), -.015f, new Vector2(1.5f, 2.5f), ParticleSystemShapeType.Box, new Vector3(3.5f, 6, .1f), false);
                }
                else
                {
                    var ceiling = TubeWall(id, path, c.z, (90 + (i % 2 == 0 ? 25 : -25)) * Mathf.Deg2Rad, out var inward) + inward * .2f;
                    Ambient($"대장_점액_방울_{i}", ceiling, Quaternion.LookRotation(Vector3.down), mucus, 2.5f, new Vector2(.06f, .13f), new Vector2(.05f, .2f), .6f, new Vector2(2.5f, 4f), ParticleSystemShapeType.Cone, Vector3.one, true);
                }
            }
        }
    }
}
#endif
