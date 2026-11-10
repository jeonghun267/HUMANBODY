#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 병원 진료실 꾸미기 (처음 병원과 마지막 진료실이 함께 쓴다): 타일 바닥·격자 천장·LED 조명판·걸레받이,
/// 벽 그림(소화 여행 포스터 4장·양치 3·3·3·소화 습관·시력표·키 재기·간판·비상구), 벽시계, 침대 커튼과 레일, 수액 걸이,
/// 대기 의자, 세면대와 거울, 손 소독제, 휴지통, 빈 선반의 약 상자와 병.
/// 그림은 Tools/make_clinic_art.py 가 만든 한 장(진료실_그림.png)에 모여 있고, 작은 소품은 재질별로 한 메시로 합쳐 그리기 수를 줄인다.
/// </summary>
public static partial class JourneyRealism
{
    const string ClinicArt = "Assets/Texture/Clinic";

    /// <summary>재질별로 모으는 메시 (면·상자·원기둥·원판). 면의 앞쪽은 normal 방향, 그림 칸은 그림판 UV.</summary>
    sealed class DecorMesh
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        public bool Empty => v.Count == 0;

        public void Quad(Vector3 c, Vector3 normal, Vector3 up, float w, float h, Rect r)
        {
            normal = normal.normalized; up = Vector3.ProjectOnPlane(up, normal).normalized;
            Vector3 right = Vector3.Cross(normal, up) * (w / 2), u = up * (h / 2);
            int k = v.Count;
            v.Add(c - right - u); v.Add(c + right - u); v.Add(c - right + u); v.Add(c + right + u);
            for (int i = 0; i < 4; i++) n.Add(normal);
            uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMin, r.yMax)); uv.Add(new Vector2(r.xMax, r.yMax));
            t.AddRange(new[] { k, k + 3, k + 1, k + 3, k, k + 2 });
        }

        public void Box(Vector3 c, Vector3 size, Quaternion rot, Rect r, bool bottom = true)
        {
            var e = size / 2;
            Quad(c + rot * new Vector3(e.x, 0, 0), rot * Vector3.right, rot * Vector3.up, size.z, size.y, r);
            Quad(c - rot * new Vector3(e.x, 0, 0), rot * Vector3.left, rot * Vector3.up, size.z, size.y, r);
            Quad(c + rot * new Vector3(0, 0, e.z), rot * Vector3.forward, rot * Vector3.up, size.x, size.y, r);
            Quad(c - rot * new Vector3(0, 0, e.z), rot * Vector3.back, rot * Vector3.up, size.x, size.y, r);
            Quad(c + rot * new Vector3(0, e.y, 0), rot * Vector3.up, rot * Vector3.forward, size.x, size.z, r);
            if (bottom) Quad(c - rot * new Vector3(0, e.y, 0), rot * Vector3.down, rot * Vector3.forward, size.x, size.z, r);
        }

        public void Box(Vector3 c, Vector3 size, Rect r, bool bottom = true) => Box(c, size, Quaternion.identity, r, bottom);

        /// <summary>둥근 판: 그림 칸을 원 안에 그대로 비춘다 (시계 판).</summary>
        public void Disc(Vector3 c, Vector3 normal, Vector3 up, float radius, int seg, Rect r)
        {
            normal = normal.normalized; up = Vector3.ProjectOnPlane(up, normal).normalized;
            var right = Vector3.Cross(normal, up);
            int k = v.Count;
            v.Add(c); n.Add(normal); uv.Add(r.center);
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2, x = Mathf.Cos(a), y = Mathf.Sin(a);
                v.Add(c + (right * x + up * y) * radius); n.Add(normal);
                uv.Add(new Vector2(r.center.x + x * r.width / 2, r.center.y + y * r.height / 2));
                if (i < seg) t.AddRange(new[] { k, k + 2 + i, k + 1 + i });
            }
        }

        public void Cylinder(Vector3 a, Vector3 b, float radius, int seg, Rect r, bool caps = true)
        {
            var axis = (b - a).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var side2 = Vector3.Cross(axis, side);
            int k = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = (float)i / seg * Mathf.PI * 2; var d = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                v.Add(a + d * radius); v.Add(b + d * radius); n.Add(d); n.Add(d);
                float u = Mathf.Lerp(r.xMin, r.xMax, (float)i / seg);
                uv.Add(new Vector2(u, r.yMin)); uv.Add(new Vector2(u, r.yMax));
                if (i < seg) { int p = k + i * 2; t.AddRange(new[] { p, p + 2, p + 1, p + 2, p + 3, p + 1 }); }
            }
            if (caps) { Disc(b, axis, side, radius, seg, new Rect(r.center, Vector2.zero)); Disc(a, -axis, side, radius, seg, new Rect(r.center, Vector2.zero)); }
        }

        /// <summary>천: 바닥 길을 따라 주름(sin)이 잡힌 세로 띠. 앞뒤 두 면을 다 만든다(한쪽만 그리는 재질이라).</summary>
        public void Pleats(IList<Vector3> path, float bottom, float top, float period, float depth, Rect r)
        {
            var pts = new List<(Vector3 p, Vector3 nrm)>();
            float along = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) along += Vector3.Distance(path[i - 1], path[i]);
                var dir = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]); dir.y = 0; dir.Normalize();
                var side = Vector3.Cross(Vector3.up, dir);
                float phase = along / period * Mathf.PI * 2;
                var p = path[i] + side * Mathf.Sin(phase) * depth;
                var slope = Mathf.Cos(phase) * depth * Mathf.PI * 2 / period; // 주름 비탈만큼 면이 기운다
                pts.Add((p, (side - dir * slope).normalized));
            }
            for (int face = 0; face < 2; face++)
            {
                int k = v.Count; float s = face == 0 ? 1 : -1;
                for (int i = 0; i < pts.Count; i++)
                {
                    var (p, nrm) = pts[i];
                    float flare = 1 + .25f * Mathf.Sin(i * 1.7f); // 아래 끝은 조금씩 다르게 벌어진다
                    v.Add(new Vector3(p.x, bottom, p.z) + nrm * .01f * flare); v.Add(new Vector3(p.x, top, p.z));
                    n.Add(nrm * s); n.Add(nrm * s);
                    float u = Mathf.Lerp(r.xMin, r.xMax, (float)i / Mathf.Max(1, pts.Count - 1));
                    uv.Add(new Vector2(u, r.yMin)); uv.Add(new Vector2(u, r.yMax));
                    if (i < pts.Count - 1)
                    {
                        int q = k + i * 2;
                        if (face == 0) t.AddRange(new[] { q, q + 1, q + 2, q + 2, q + 1, q + 3 });
                        else t.AddRange(new[] { q, q + 2, q + 1, q + 2, q + 3, q + 1 });
                    }
                }
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ── 그림판(2048²) 칸: Tools/make_clinic_art.py 와 같은 자리 (왼쪽 위 원점, 픽셀). 번짐을 막으려 2px 안쪽만 쓴다.
    static Rect Px(int x, int y, int w, int h) => new Rect((x + 2) / 2048f, 1 - (y + h - 2) / 2048f, (w - 4) / 2048f, (h - 4) / 2048f);
    static Rect OrganPoster(int i) => Px(i * 512, 0, 512, 724);
    static readonly Rect BrushPoster = Px(0, 724, 512, 724), HabitPoster = Px(512, 724, 512, 724), EyeChart = Px(1024, 724, 384, 724), HeightChart = Px(1408, 724, 128, 724);
    static readonly Rect SignClinic = Px(1536, 724, 512, 96), SignDesk = Px(1536, 820, 512, 96), SignPod = Px(1536, 916, 512, 96);
    static readonly Rect ExitSign = Px(1536, 1012, 256, 96), LedPanel = Px(1792, 1012, 256, 96), MirrorArt = Px(1536, 1108, 256, 96), ClockFace = Px(0, 1448, 512, 512);
    /// <summary>색 견본: 0 흰색, 1 밝은 회색, 2 짙은 회색, 3 청록, 4 민트, 5 하늘, 6 분홍, 7 노랑, 8 빨강, 9 초록, 10 갈색, 11 검정.</summary>
    static Rect Swatch(int i) => new Rect((512 + i * 128 + 64) / 2048f, 1 - (1448 + 64) / 2048f, 0, 0);
    static Rect BoxLabel(int i) => Px(512 + i * 256, 1576, 256, 128);

    static Texture2D ClinicTexture(string file, bool repeat, int maxSize)
    {
        string path = $"{ClinicArt}/{file}.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
        var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (imp != null && (imp.wrapMode != wrap || imp.maxTextureSize != maxSize || imp.anisoLevel != 8 || !imp.mipmapEnabled))
        {
            imp.wrapMode = wrap; imp.maxTextureSize = maxSize; imp.anisoLevel = 8; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ArtMaterial(string name, bool glow)
    {
        var mat = Lit(name, Color.white, glow ? .5f : .22f);
        var art = ClinicTexture("진료실_그림", false, 2048);
        mat.SetTexture("_BaseMap", art);
        if (glow)
        {
            // 뒤에서 빛나는 간판·조명판: 그림 그대로 빛나게 (방 조명에 상관없이 읽힌다)
            mat.EnableKeyword("_EMISSION"); mat.SetTexture("_EmissionMap", art); mat.SetColor("_EmissionColor", Color.white * .85f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(mat); return mat;
    }

    static MeshRenderer DecorObject(Transform parent, string name, DecorMesh mesh, Material material)
    {
        var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
        if (mesh.Empty) return null;
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var m = mesh.Build(name); SaveMesh($"{Folder}/{name}.asset", ref m);
        m.UploadMeshData(false); // 같은 실행 안에서 이미 올라간 예전 모양으로 찍히지 않게 (자산에 덮어쓴 뒤)
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return mr;
    }

    /// <summary>벽에 거는 그림: 테두리 판(액자) + 그 앞의 그림 면. at = 벽면 위의 그림 가운데, normal = 방 안쪽.</summary>
    static void WallArt(DecorMesh frame, DecorMesh art, Vector3 at, Vector3 normal, float w, float h, Rect r, int frameSwatch = 2, float border = .03f, float depth = .025f)
    {
        var rot = Quaternion.LookRotation(normal);
        frame.Box(at + normal * depth / 2, new Vector3(w + border * 2, h + border * 2, depth), rot, Swatch(frameSwatch), false);
        art.Quad(at + normal * (depth + .002f), normal, Vector3.up, w, h, r);
    }

    static void ClinicDecor(Transform room, float halfX, float front, float back, float height, Vector3[] lampSpots)
    {
        var art = new DecorMesh(); var glow = new DecorMesh(); var metal = new DecorMesh();
        GameObject Prop(string name) => room.Cast<Transform>().FirstOrDefault(c => c.name == name)?.gameObject;

        // ── 바닥·천장: 60cm 타일 (그림 한 장 = 4×4 타일 = 2.4m)
        var floor = room.Find("바닥")?.GetComponent<Renderer>();
        if (floor != null)
        {
            var mat = floor.sharedMaterial; mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .42f);
            mat.SetTexture("_BaseMap", ClinicTexture("진료실_바닥", true, 1024)); mat.SetTextureScale("_BaseMap", new Vector2(halfX * 2 / 2.4f, (back - front) / 2.4f));
            EditorUtility.SetDirty(mat);
        }
        var ceiling = room.Find("천장")?.GetComponent<Renderer>();
        if (ceiling != null)
        {
            var mat = ceiling.sharedMaterial; mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .05f);
            mat.SetTexture("_BaseMap", ClinicTexture("진료실_천장", true, 1024)); mat.SetTextureScale("_BaseMap", new Vector2(halfX * 2 / 2.4f, (back - front) / 2.4f));
            EditorUtility.SetDirty(mat);
        }
        // 천장 LED 조명판 (예전 공장 같은 막대등 대신 병원 천장에 묻힌 판)
        foreach (var p in lampSpots)
        {
            art.Box(new Vector3(p.x, height - .02f, p.z), new Vector3(.68f, .04f, 1.28f), Swatch(1));
            glow.Quad(new Vector3(p.x, height - .043f, p.z), Vector3.down, Vector3.forward, .6f, 1.2f, LedPanel);
        }
        foreach (var p in new[] { new Vector3(-1.2f, 0, 2.2f), new Vector3(1.4f, 0, -2.6f) }) // 화재 감지기
        {
            art.Cylinder(new Vector3(p.x, height - .035f, p.z), new Vector3(p.x, height, p.z), .07f, 16, Swatch(0));
            glow.Disc(new Vector3(p.x, height - .036f, p.z + .03f), Vector3.down, Vector3.forward, .006f, 8, Swatch(8));
        }

        // ── 걸레받이 (아래 판 앞, 바닥에서 10cm)
        float inX = halfX - .04f, inBack = back - .04f, inFront = front + .04f;
        art.Box(new Vector3(-inX + .012f, .05f, (front + back) / 2), new Vector3(.024f, .1f, back - front - .08f), Swatch(2), false);
        art.Box(new Vector3(inX - .012f, .05f, (front + back) / 2), new Vector3(.024f, .1f, back - front - .08f), Swatch(2), false);
        art.Box(new Vector3(0, .05f, inBack - .012f), new Vector3(halfX * 2 - .08f, .1f, .024f), Swatch(2), false);
        art.Box(new Vector3(0, .05f, inFront + .012f), new Vector3(halfX * 2 - .08f, .1f, .024f), Swatch(2), false);

        // ── 침대: 천장 레일과 커튼(안쪽 면만 닫고, 들어오는 쪽과 방 쪽은 끝으로 걷어 두어 침대가 보인다), 수액 걸이
        var bed = Prop("bed");
        float curtainZ0 = .2f;
        if (bed != null)
        {
            var bb = Bounds(bed);
            float wallX = -halfX, x1 = Mathf.Min(bb.max.x + .45f, -1.6f), z0 = bb.min.z - .5f, z1 = bb.max.z + .5f, railY = 2.45f;
            var console = Prop("console");
            if (console != null) { var cb = Bounds(console); if (cb.min.x < x1 + .1f && cb.min.z < z1 + .1f && cb.max.z > z0) z1 = Mathf.Min(z1, cb.min.z - .12f); }
            curtainZ0 = z0;
            metal.Box(new Vector3((wallX + x1) / 2, railY, z0), new Vector3(x1 - wallX, .03f, .04f), Swatch(0));
            metal.Box(new Vector3((wallX + x1) / 2, railY, z1), new Vector3(x1 - wallX, .03f, .04f), Swatch(0));
            metal.Box(new Vector3(x1, railY, (z0 + z1) / 2), new Vector3(.04f, .03f, z1 - z0 + .04f), Swatch(0));
            foreach (var hang in new[] { new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3((wallX + x1) / 2, 0, z0), new Vector3((wallX + x1) / 2, 0, z1) })
                metal.Cylinder(new Vector3(hang.x, railY, hang.z), new Vector3(hang.x, height, hang.z), .008f, 8, Swatch(0), false);
            List<Vector3> Line(Vector3 a, Vector3 b) { int count = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / .03f)); return Enumerable.Range(0, count + 1).Select(i => Vector3.Lerp(a, b, (float)i / count)).ToList(); }
            float bottom = .38f, top = railY - .03f;
            art.Pleats(Line(new Vector3(wallX + .06f, 0, z1), new Vector3(x1 - .05f, 0, z1)), bottom, top, .2f, .045f, Swatch(4));      // 안쪽: 닫음
            art.Pleats(Line(new Vector3(wallX + .06f, 0, z0), new Vector3(wallX + .48f, 0, z0)), bottom, top, .09f, .06f, Swatch(4));   // 들어오는 쪽: 벽으로 걷음
            art.Pleats(Line(new Vector3(x1, 0, z1 - .46f), new Vector3(x1, 0, z1 - .03f)), bottom, top, .09f, .06f, Swatch(4));        // 방 쪽: 안쪽 끝으로 걷음
            // 수액 걸이: 바퀴 다섯 개 받침 + 기둥 + 고리 + 수액 주머니
            var iv = new Vector3(Mathf.Max(wallX + .35f, bb.min.x + .25f), 0, z1 - .3f);
            for (int i = 0; i < 5; i++)
            {
                var d = Quaternion.Euler(0, i * 72, 0) * Vector3.forward * .26f;
                metal.Cylinder(iv + Vector3.up * .07f, iv + d + Vector3.up * .05f, .012f, 6, Swatch(0), false);
                art.Cylinder(iv + d + new Vector3(0, .025f, 0), iv + d + new Vector3(0, .025f, 0) + Quaternion.Euler(0, i * 72, 0) * Vector3.right * .03f, .025f, 10, Swatch(11));
            }
            metal.Cylinder(iv + Vector3.up * .06f, iv + Vector3.up * 1.95f, .012f, 10, Swatch(0));
            metal.Cylinder(iv + new Vector3(-.14f, 1.9f, 0), iv + new Vector3(.14f, 1.9f, 0), .007f, 6, Swatch(0));
            art.Box(iv + new Vector3(.1f, 1.74f, 0), new Vector3(.12f, .2f, .035f), Swatch(5));
            art.Cylinder(iv + new Vector3(.1f, 1.64f, 0), new Vector3(bb.min.x + .55f, bb.max.y + .02f, Mathf.Min(bb.max.z - .15f, iv.z - .1f)), .004f, 6, Swatch(0), false);
        }

        // ── 소화 여행 포스터 4장: 큰 화면 양옆 뒤 벽 (오른쪽 두 장은 들어선 자리에서 잠수정 너머로 보인다)
        // (왼쪽 벽 안쪽은 침대 커튼에 가려 보이지 않았다)
        var screen = Prop("big_screen");
        float screenLeft = screen != null ? Bounds(screen).min.x : -1.4f, screenRight = screen != null ? Bounds(screen).max.x : 1.4f;
        // 큰 화면(진찰 모니터) 양옆에 두 장씩: 왼쪽 입·위, 오른쪽 소장·대장 (사용자 요청)
        WallArt(art, art, new Vector3(screenLeft - 1.65f, 1.72f, back), Vector3.back, .52f, .735f, OrganPoster(0));
        WallArt(art, art, new Vector3(screenLeft - .8f, 1.72f, back), Vector3.back, .52f, .735f, OrganPoster(1));
        WallArt(art, art, new Vector3(screenRight + .8f, 1.72f, back), Vector3.back, .52f, .735f, OrganPoster(2));
        WallArt(art, art, new Vector3(screenRight + 1.65f, 1.72f, back), Vector3.back, .52f, .735f, OrganPoster(3));
        // 왼쪽 벽 접수대 옆(커튼 앞)에 소화 습관 포스터, 자리가 없으면 앞벽
        float nearFrom = -1.5f, nearTo = curtainZ0 - .12f;
        bool habitsLeft = nearTo - nearFrom >= .6f;
        if (habitsLeft) WallArt(art, art, new Vector3(-halfX, 1.72f, (nearFrom + nearTo) / 2), Vector3.right, .52f, .735f, HabitPoster);

        // ── 앞벽(문 쪽): 소화 습관 포스터, 손 소독제, 대기 의자와 양치 3·3·3, 비상구 표시, 입구 매트
        var frontN = Vector3.forward;
        if (!habitsLeft) WallArt(art, art, new Vector3(-2.3f, 1.72f, front), frontN, .52f, .735f, HabitPoster);
        var door = Prop("door_1"); var db = door != null ? Bounds(door) : new Bounds(new Vector3(0, 1.2f, front), new Vector3(1.4f, 2.4f, .2f));
        float exitY = Mathf.Min(height - .2f, db.max.y + .2f);
        float exitZ = Mathf.Max(front + .062f, db.max.z + .01f); // 문틀이 튀어나온 만큼 판을 두껍게 해 표시가 뜨지 않게
        art.Box(new Vector3(0, exitY, (front + exitZ) / 2), new Vector3(.54f, .22f, exitZ - front - .002f), Swatch(0));
        glow.Quad(new Vector3(0, exitY, exitZ), frontN, Vector3.up, .5f, .1875f, ExitSign);
        var gel = new Vector3(db.min.x - .45f, 1.15f, front);
        metal.Box(gel + new Vector3(0, 0, .015f), new Vector3(.13f, .26f, .03f), Swatch(0));
        art.Box(gel + new Vector3(0, .01f, .07f), new Vector3(.1f, .2f, .08f), Swatch(0));
        art.Box(gel + new Vector3(0, .13f, .07f), new Vector3(.03f, .04f, .03f), Swatch(3));
        art.Box(gel + new Vector3(0, -.12f, .06f), new Vector3(.08f, .015f, .06f), Swatch(1));
        float benchX = db.max.x + 1.55f, benchZ = front + .3f;
        WallArt(art, art, new Vector3(benchX, 1.72f, front), frontN, .52f, .735f, BrushPoster);
        metal.Box(new Vector3(benchX, .4f, benchZ), new Vector3(1.75f, .05f, .06f), Swatch(1));
        foreach (float lx in new[] { benchX - .7f, benchX + .7f })
        {
            metal.Box(new Vector3(lx, .2f, benchZ), new Vector3(.05f, .4f, .05f), Swatch(1));
            metal.Box(new Vector3(lx, .015f, benchZ), new Vector3(.06f, .03f, .5f), Swatch(1));
        }
        for (int i = -1; i <= 1; i++)
        {
            float sx = benchX + i * .56f;
            art.Box(new Vector3(sx, .45f, benchZ + .02f), new Vector3(.5f, .06f, .44f), Swatch(3));
            art.Box(new Vector3(sx, .74f, benchZ - .2f), new Vector3(.5f, .44f, .05f), Quaternion.Euler(-8, 0, 0), Swatch(3));
            metal.Box(new Vector3(sx, .5f, benchZ - .17f), new Vector3(.06f, .2f, .03f), Swatch(1));
        }
        art.Box(new Vector3(0, .005f, front + .62f), new Vector3(1.4f, .01f, .9f), Swatch(2), false);

        // ── 오른쪽 벽: 시력표·키 재기, 벽시계(책상 위), 세면대와 거울·휴지·비누, 휴지통
        var rightN = Vector3.left;
        var station = Prop("computer_station"); float chartZ = station != null ? Mathf.Max(-1.0f, Bounds(station).max.z + .25f) : -1.0f;
        WallArt(art, art, new Vector3(halfX - .045f, 1.2f, chartZ), rightN, .21f, 1.2f, HeightChart, 0, .012f, .02f); // 아래 판(1m) 앞에 붙인 판
        WallArt(art, art, new Vector3(halfX, 1.55f, chartZ + .62f), rightN, .36f, .68f, EyeChart, 1);
        var clockAt = new Vector3(halfX, 2.45f, 1.5f);
        art.Cylinder(clockAt, clockAt + rightN * .035f, .215f, 40, Swatch(2));
        art.Disc(clockAt + rightN * .036f, rightN, Vector3.up, .2f, 40, ClockFace);
        float sinkZ = front + 1.2f, sinkX = halfX - .27f;
        art.Box(new Vector3(sinkX, .42f, sinkZ), new Vector3(.54f, .84f, .7f), Swatch(0), false);
        art.Box(new Vector3(sinkX - .02f, .86f, sinkZ), new Vector3(.6f, .04f, .76f), Swatch(1));
        art.Quad(new Vector3(sinkX - .05f, .881f, sinkZ), Vector3.up, Vector3.forward, .36f, .46f, Swatch(5));
        metal.Cylinder(new Vector3(halfX - .07f, .88f, sinkZ), new Vector3(halfX - .07f, 1.08f, sinkZ), .018f, 12, Swatch(0));
        metal.Cylinder(new Vector3(halfX - .07f, 1.06f, sinkZ), new Vector3(halfX - .22f, 1.03f, sinkZ), .014f, 10, Swatch(0));
        WallArt(art, art, new Vector3(halfX, 1.55f, sinkZ), rightN, .55f, .75f, MirrorArt, 1, .025f, .02f);
        art.Box(new Vector3(halfX - .07f, 1.28f, sinkZ + .62f), new Vector3(.14f, .34f, .3f), Swatch(0));
        art.Box(new Vector3(halfX - .05f, 1.14f, sinkZ - .5f), new Vector3(.08f, .18f, .1f), Swatch(0));
        art.Box(new Vector3(halfX - .09f, 1.22f, sinkZ - .5f), new Vector3(.04f, .02f, .02f), Swatch(9));
        var bin = new Vector3(halfX - .3f, 0, front + .35f);
        art.Cylinder(bin, bin + Vector3.up * .44f, .15f, 20, Swatch(0));
        art.Cylinder(bin + Vector3.up * .44f, bin + Vector3.up * .47f, .155f, 20, Swatch(1));
        art.Box(bin + new Vector3(-.17f, .03f, 0), new Vector3(.08f, .02f, .1f), Swatch(2));

        // ── 뒤 벽: 입·위 포스터 위에 '소화기내과 진료실' 간판 (큰 화면 위는 천장까지 자리가 없어 화면 뒤에 숨었다)
        var signAt = new Vector3(screenLeft - 1.22f, 2.55f, back);
        art.Box(signAt + Vector3.back * .03f, new Vector3(1.46f, .32f, .06f), Swatch(2));
        glow.Quad(signAt + Vector3.back * .062f, Vector3.back, Vector3.up, 1.4f, .2625f, SignClinic);

        // ── 걸어 다니므로 새로 둔 소품(대기 의자·세면대·휴지통)에도 부딪히게 (커튼은 지나갈 수 있게 둔다)
        var oldSolid = room.Find("진료실_장식_충돌"); if (oldSolid != null) Object.DestroyImmediate(oldSolid.gameObject);
        var solid = new GameObject("진료실_장식_충돌"); solid.transform.SetParent(room, false);
        void Solid(Vector3 c, Vector3 size) { var box = solid.AddComponent<BoxCollider>(); box.center = c; box.size = size; }
        Solid(new Vector3(benchX, .45f, benchZ - .02f), new Vector3(1.8f, .9f, .5f));
        Solid(new Vector3(sinkX - .02f, .45f, sinkZ), new Vector3(.6f, .9f, .76f));
        Solid(bin + Vector3.up * .24f, new Vector3(.32f, .48f, .32f));

        // ── 빈 선반 채우기
        var cabinet = Prop("cabinet");
        if (cabinet != null) FillShelves(cabinet, art);

        DecorObject(room, "진료실_장식", art, ArtMaterial("진료실_그림", false));
        DecorObject(room, "진료실_장식_빛", glow, ArtMaterial("진료실_그림_빛", true));
        DecorObject(room, "진료실_장식_금속", metal, ClinicMetal());
        WallClockHands(room, clockAt + rightN * .04f, rightN);
    }

    static Material ClinicMetal()
    {
        var mat = Lit("진료실_금속", new Color(.78f, .8f, .83f), .72f);
        mat.SetFloat("_Metallic", .55f); EditorUtility.SetDirty(mat); return mat;
    }

    /// <summary>시계 바늘(실제 시각으로 도는 세 개): 몸체의 +z 는 벽 쪽, 바늘은 보는 쪽(-z)으로 조금씩 띄운다.</summary>
    static void WallClockHands(Transform room, Vector3 at, Vector3 normal)
    {
        var old = room.Find("벽시계"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var clock = new GameObject("벽시계"); clock.transform.SetParent(room, false);
        clock.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-normal, Vector3.up));
        var wc = clock.AddComponent<WallClock>();
        var mat = ArtMaterial("진료실_그림", false);
        Transform Hand(string name, float length, float width, float lift, int swatch)
        {
            var h = new GameObject(name).transform; h.SetParent(clock.transform, false);
            var dm = new DecorMesh(); dm.Box(new Vector3(0, length / 2 - .025f, -lift), new Vector3(width, length, .004f), Swatch(swatch));
            var m = dm.Build("벽시계_" + name); SaveMesh($"{Folder}/벽시계_{name}.asset", ref m);
            h.gameObject.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = h.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return h;
        }
        wc.hour = Hand("시침", .12f, .016f, .004f, 11);
        wc.minute = Hand("분침", .17f, .011f, .009f, 11);
        wc.second = Hand("초침", .18f, .004f, .014f, 8);
    }

    /// <summary>
    /// 선반 모형의 위를 보는 넓은 면(선반 판)을 높이별로 찾아, 그 위에 약 상자·약병·서류철을 한 줄씩 올린다.
    /// 다음 선반까지의 높이를 넘지 않게 하고, 맨 위 지붕 면에는 올리지 않는다.
    /// </summary>
    static void FillShelves(GameObject cabinet, DecorMesh art)
    {
        var levels = new Dictionary<int, (float area, Bounds box)>();
        foreach (var mf in cabinet.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue; // 편집기에서는 읽기 꺼진 메시도 읽힌다
            var verts = mesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray(); var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                var cross = Vector3.Cross(b - a, c - a); float area = cross.magnitude / 2;
                if (area < 1e-4f || cross.normalized.y < .95f) continue;
                int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3 / .02f);
                var box = new Bounds(a, Vector3.zero); box.Encapsulate(b); box.Encapsulate(c);
                if (levels.TryGetValue(key, out var l)) { l.box.Encapsulate(box); levels[key] = (l.area + area, l.box); } else levels[key] = (area, box);
            }
        }
        var cb = Bounds(cabinet);
        var shelves = levels.Where(kv => kv.Value.area > .1f).OrderBy(kv => kv.Key).Select(kv => (y: kv.Key * .02f, box: kv.Value.box)).ToList();
        var rng = new System.Random(11);
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        int placed = 0;
        for (int s = 0; s < shelves.Count; s++)
        {
            var (y, box) = shelves[s];
            float ceilingY = s + 1 < shelves.Count ? shelves[s + 1].y : cb.max.y;
            if (y > cb.max.y - .05f || ceilingY - y < .16f) continue; // 지붕이거나 너무 낮은 칸
            float maxH = Mathf.Min(.3f, ceilingY - y - .05f);
            bool alongX = box.size.x >= box.size.z;
            float from = (alongX ? box.min.x : box.min.z) + .07f, to = (alongX ? box.max.x : box.max.z) - .07f;
            float depthMid = alongX ? box.center.z : box.center.x, depth = (alongX ? box.size.z : box.size.x) * .6f;
            Vector3 At(float along, float h) => alongX ? new Vector3(along, y + h / 2, depthMid) : new Vector3(depthMid, y + h / 2, along);
            for (float p = from; p < to - .06f;)
            {
                int kind = rng.Next(3);
                if (kind == 0) // 약 상자
                {
                    float w = R(.1f, .18f), h = Mathf.Min(maxH, R(.08f, .16f)), d = Mathf.Min(depth, R(.08f, .12f));
                    if (p + w > to) break;
                    art.Box(At(p + w / 2, h), alongX ? new Vector3(w, h, d) : new Vector3(d, h, w), BoxLabel(rng.Next(6)));
                    p += w + R(.015f, .04f);
                }
                else if (kind == 1) // 약병 (흰 병·갈색 병, 색 뚜껑)
                {
                    float r = R(.03f, .045f), h = Mathf.Min(maxH, R(.1f, .18f));
                    if (p + r * 2 > to) break;
                    var c = At(p + r, h); c.y = y;
                    art.Cylinder(c, c + Vector3.up * h * .85f, r, 12, Swatch(rng.Next(2) == 0 ? 0 : 10));
                    art.Cylinder(c + Vector3.up * h * .85f, c + Vector3.up * h, r * .7f, 12, Swatch(new[] { 3, 8, 9, 0 }[rng.Next(4)]));
                    p += r * 2 + R(.015f, .035f);
                }
                else // 세워 둔 서류철 몇 권
                {
                    int count = rng.Next(2, 5); float h = Mathf.Min(maxH, R(.2f, .28f)), d = Mathf.Min(depth, .22f);
                    for (int k = 0; k < count && p + .04f < to; k++)
                    {
                        art.Box(At(p + .02f, h), alongX ? new Vector3(.035f, h, d) : new Vector3(d, h, .035f), Swatch(new[] { 3, 5, 7, 6, 9 }[rng.Next(5)]));
                        p += .04f;
                    }
                    p += R(.02f, .05f);
                }
                placed++;
            }
        }
        Debug.Log($"[병원] 선반 {shelves.Count}개 면 중 물건 {placed}묶음");
    }

    /// <summary>병원(처음 장면)에만: 접수대와 잠수정 탑승구 위에 천장에 매단 양면 안내판.</summary>
    static void LobbySigns(Transform room, Vector3 desk, Vector3 boarding, Vector3 spawn, float height)
    {
        var glow = new DecorMesh(); var frame = new DecorMesh(); var metal = new DecorMesh();
        void Hanging(Vector3 at, float w, float h, Rect r)
        {
            var face = spawn - at; face.y = 0; face.Normalize();
            var rot = Quaternion.LookRotation(face);
            frame.Box(at, new Vector3(w + .06f, h + .06f, .04f), rot, Swatch(2));
            glow.Quad(at + face * .021f, face, Vector3.up, w, h, r);
            glow.Quad(at - face * .021f, -face, Vector3.up, w, h, r);
            var side = Vector3.Cross(Vector3.up, face) * (w / 2 - .08f);
            foreach (var s in new[] { side, -side }) metal.Cylinder(at + s + Vector3.up * (h / 2 + .03f), new Vector3(at.x + s.x, height, at.z + s.z), .004f, 6, Swatch(0), false);
        }
        Hanging(new Vector3(desk.x, 2.55f, desk.z), 1.0f, .1875f, SignDesk);
        Hanging(new Vector3(boarding.x, 2.6f, boarding.z), 1.4f, .2625f, SignPod);
        DecorObject(room, "병원_매단_안내판", glow, ArtMaterial("진료실_그림_빛", true));
        DecorObject(room, "병원_매단_안내판_틀", frame, ArtMaterial("진료실_그림", false));
        DecorObject(room, "병원_매단_안내판_줄", metal, ClinicMetal());
    }
    /// <summary>병원·마지막 진료실만 다시 꾸민다 (배치: JourneyRealism.ClinicDecorAndShots, 촬영은 이어서 ClinicShots).</summary>
    public static void ClinicDecorAndShots()
    {
        ShaderUtil.allowAsyncCompilation = false;
        foreach (var name in new[] { "00_Lobby", "06_Theater" })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            ConfigureScene(StageId(name));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[병원] 꾸미기 적용 끝 — 촬영은 새 실행에서 JourneyRealism.ClinicShots (같은 실행에서 찍으면 덮어쓴 메시의 예전 모양이 찍힌다)");
    }

    /// <summary>병원 둘러보기 촬영 (배치, 꾸미기 적용과 따로 실행): Tools/realism_captures/00_Lobby_꾸밈_*.png</summary>
    public static void ClinicShots()
    {
        ShaderUtil.allowAsyncCompilation = false;
        const string dir = "Tools/realism_captures";
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{SceneDir}/00_Lobby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        foreach (var name in new[] { "체험_시작_대기", "체험_안내판" }) { var g = GameObject.Find(name); if (g != null) g.SetActive(false); } // 촬영에서만 (저장 안 함)
        void Look(string view, Vector3 eye, Vector3 target, float fov) => Shoot(dir, "00_Lobby", "꾸밈_" + view, eye, Quaternion.LookRotation(target - eye), fov);
        Look("앞", new Vector3(0, 1.6f, -3.6f), new Vector3(0, 1.5f, 3f), 80);
        Look("왼쪽", new Vector3(.5f, 1.6f, -1.5f), new Vector3(-5, 1.5f, 3f), 80);
        Look("오른쪽", new Vector3(-.5f, 1.6f, -1f), new Vector3(5, 1.5f, -1.5f), 80);
        Look("뒤", new Vector3(0, 1.6f, 1f), new Vector3(0, 1.3f, -5.5f), 80);
        Look("침대", new Vector3(-1f, 1.6f, -1.2f), new Vector3(-4, 1f, 1.3f), 70);
        Look("선반", new Vector3(-2.4f, 1.5f, -2.9f), new Vector3(-4.6f, 1f, -4.5f), 60);
        var nurseNpc = Object.FindObjectsByType<HospitalNpc>(FindObjectsSortMode.None).FirstOrDefault(n => n.npcName == "간호사");
        if (nurseNpc != null && nurseNpc.head != null)
        {
            // 모형 뿌리는 세우는 과정에서 돌아가 있으므로 앞쪽은 들어선 자리 쪽(수평)으로 잡는다
            var face = nurseNpc.head.position; var toward = Vector3.ProjectOnPlane(new Vector3(0, 0, -3.6f) - face, Vector3.up).normalized;
            Look("간호사", face + toward * 1.3f + Vector3.up * .05f, face - Vector3.up * .25f, 45);
            Look("간호사_얼굴", face + toward * .55f, face, 35);
        }
        Look("세면대",new Vector3(2.6f, 1.6f, -2.6f), new Vector3(5, 1.2f, -4.3f), 65);
        Look("시계_시력표", new Vector3(2.2f, 1.6f, .4f), new Vector3(5, 1.8f, .3f), 70);
        Look("천장", new Vector3(0, 1.6f, -2f), new Vector3(0, 3.2f, 1.5f), 80);
        // 큰 화면 진찰 결과 모니터: 진찰 중(이 하나 찾음)을 들어선 자리에서, 진찰 끝(치료 경로)을 홀로그램 뒤에서 정면으로
        var mon = Object.FindFirstObjectByType<ClinicMonitor>();
        if (mon != null)
        {
            void Refresh() { foreach (var t in mon.GetComponentsInChildren<TMPro.TMP_Text>()) t.ForceMeshUpdate(); }
            mon.SetSymptom("이도 시리고\n배도 아파요"); mon.Found(0); if (mon.plan != null) mon.plan.alpha = 0; Refresh();
            Look("TV_진찰중", new Vector3(0, 1.6f, -3.6f), new Vector3(0, 1.9f, 6.3f), 45);
            mon.Found(1); mon.Found(2); mon.ShowPlan(); if (mon.plan != null) mon.plan.alpha = 1; Refresh();
            Look("TV_진찰끝", new Vector3(0, 1.8f, 2.6f), new Vector3(0, 1.85f, 6.3f), 55);
        }
        // 잠수정 탑승구 옆 출발 버튼: 진찰 전(잠김) · 진찰 뒤(열림, 가리킨 모습)
        var board = Object.FindFirstObjectByType<BoardingConsole>();
        if (board != null && board.button != null)
        {
            var at = board.transform.position; var eyeAt = new Vector3(.95f, 1.6f, 1.25f);
            Look("출발버튼_잠김", eyeAt, at, 40);
            Look("출발버튼_멀리", new Vector3(.3f, 1.6f, -.6f), at + Vector3.up * .3f, 60);
            board.button.front.color = board.readyTint; board.button.ring.enabled = true;
            if (board.status != null) { board.status.text = board.readyText; board.status.ForceMeshUpdate(); }
            Look("출발버튼_열림", eyeAt, at, 40);
        }
        // 의사 대화창의 선택지(입체 버튼): 촬영에서만 열고 두 개를 띄운다 (첫째는 가리킨 모습)
        var doctor = Object.FindObjectsByType<HospitalNpc>(FindObjectsSortMode.None).FirstOrDefault(n => n.npcName == "의사 선생님");
        if (doctor != null && doctor.panel != null && doctor.choiceTemplate != null)
        {
            doctor.panel.SetActive(true);
            if (doctor.lineText != null) { doctor.lineText.text = "어서 와요. 몸 스캔으로 아픈 곳을 같이 찾아볼까요?"; doctor.lineText.ForceMeshUpdate(); }
            foreach (var (label, i) in new[] { ("네, 보여 주세요", 0), ("몸 스캔이 뭐예요?", 1) })
            {
                var c = Object.Instantiate(doctor.choiceTemplate, doctor.choiceRoot); c.SetActive(true);
                ((RectTransform)c.transform).anchoredPosition = new Vector2(0, -i * 64);
                var t = c.GetComponentInChildren<TMPro.TMP_Text>(); t.text = label; t.ForceMeshUpdate();
                var d = c.GetComponent<DepthButton>(); if (d != null && i == 0) { d.ring.enabled = true; d.front.color = d.hoverTint; }
            }
            var p = doctor.panel.transform;
            p.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(p.position - new Vector3(0, 1.6f, -3.6f), Vector3.up));
            Look("대화_선택지", p.position - p.forward * 1.5f + p.right * .55f, p.position - Vector3.up * .1f, 55);
        }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{SceneDir}/06_Theater.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        Shoot(dir, "06_Theater", "꾸밈_앞", new Vector3(0, 1.6f, -3.6f), Quaternion.LookRotation(new Vector3(0, -.1f, 1)), 80);
        Shoot(dir, "06_Theater", "TV_치료결과", new Vector3(0, 1.8f, 2.6f), Quaternion.LookRotation(new Vector3(0, 1.85f, 6.3f) - new Vector3(0, 1.8f, 2.6f)), 55);
        var quizPanel = GameObject.Find("퀴즈판");
        if (quizPanel != null)
        {
            var q = quizPanel.transform; var o = q.Find("O_판")?.GetComponent<DepthButton>();
            if (o != null) { o.ring.enabled = true; o.front.color = o.hoverTint; } // O 를 가리킨 모습
            var target = q.position - q.up * .25f;
            Shoot(dir, "06_Theater", "OX_입체버튼", target - q.forward * 1.3f + q.right * .6f + Vector3.up * .25f, Quaternion.LookRotation(target - (target - q.forward * 1.3f + q.right * .6f + Vector3.up * .25f)), 50);
        }
        Debug.Log("[병원] 촬영 끝");
    }
    /// <summary>포스터 자리 점검 (배치): 포스터 바로 앞에서 찍고, 눈→포스터 광선에 걸리는 렌더러를 적는다 → Tools/realism_probe.txt</summary>
    public static void ClinicProbe()
    {
        ShaderUtil.allowAsyncCompilation = false;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{SceneDir}/00_Lobby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        foreach (var name in new[] { "체험_시작_대기", "체험_안내판" }) { var g = GameObject.Find(name); if (g != null) g.SetActive(false); }
        var report = new System.Text.StringBuilder();
        var targets = new (string name, Vector3 at, Vector3 normal)[]
        {
            ("뒤벽_입", new Vector3(-3.06f, 1.72f, 6.5f), Vector3.back), ("왼벽_습관", new Vector3(-5f, 1.72f, -.98f), Vector3.right),
            ("오른벽_대장", new Vector3(5f, 1.72f, 3f), Vector3.left), ("오른벽_시력표", new Vector3(5f, 1.55f, -.38f), Vector3.left),
        };
        foreach (var (name, at, normal) in targets)
        {
            var eye = at + normal * 1.5f;
            Shoot("Tools/realism_captures", "00_Lobby", "점검_" + name, eye, Quaternion.LookRotation(-normal), 60);
            var ray = new Ray(eye, -normal);
            report.Append($"\n[{name}] {eye:F2} → {at:F2}\n");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray, out float d) && d < 1.6f))
            { r.bounds.IntersectRay(ray, out float d); report.Append($"  {d:0.00}m {r.transform.root.name}/{r.name} ({r.GetType().Name}, 재질 {(r.sharedMaterial != null ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "-")}, 크기 {r.bounds.size:F2})\n"); }
        }
        Shoot("Tools/realism_captures", "00_Lobby", "점검_들어선자리", new Vector3(0, 1.6f, -3.6f), Quaternion.LookRotation(new Vector3(0, -.1f, 6.6f)), 80);
        foreach (float dist in new[] { 4f, 7f, 10f })
            Shoot("Tools/realism_captures", "00_Lobby", $"점검_거리_{dist:0}m", new Vector3(-2.6f, 1.6f, 6.5f - dist), Quaternion.LookRotation(Vector3.forward), 60);
        var deco = GameObject.Find("진료실_장식")?.GetComponent<MeshRenderer>();
        var tex = deco != null ? deco.sharedMaterial.GetTexture("_BaseMap") as Texture2D : null;
        report.Append($"\n장식 렌더러: {(deco != null ? deco.bounds.ToString() : "없음")}, 그림 {(tex != null ? $"{tex.width}x{tex.height} 밉 {tex.mipmapCount} {tex.format} 스트리밍 {tex.streamingMipmaps}" : "없음")}\n");
        System.IO.File.WriteAllText("Tools/realism_probe.txt", report.ToString(), new System.Text.UTF8Encoding(true));
    }
    /// <summary>
    /// 큰 화면 위의 진찰 결과 모니터 (화면 판 재질 '진료실_화면' 이 그려지는 부분의 실제 크기에 맞춘다).
    /// 들어선 자리에서 보면 몸 스캔 홀로그램이 화면 가운데를 가리므로 내용은 왼쪽(증상·심전도)·오른쪽(진찰 결과)·아래(치료 경로)에 둔다.
    /// ending 이면 마지막 진료실용(처음부터 모두 치료 완료).
    /// </summary>
    static ClinicMonitor ClinicMonitorUi(Transform room, bool ending)
    {
        var old = room.Find("진찰_모니터"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var screen = room.Cast<Transform>().FirstOrDefault(c => c.name == "big_screen")?.gameObject;
        if (screen == null) { Debug.LogWarning("[병원] 큰 화면 없음 — 모니터를 못 단다"); return null; }
        Bounds face = default; bool any = false;
        foreach (var r in screen.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var mats = r.sharedMaterials; var verts = mf.sharedMesh.vertices;
            for (int sub = 0; sub < mats.Length && sub < mf.sharedMesh.subMeshCount; sub++)
            {
                if (mats[sub] == null || mats[sub].name != "진료실_화면") continue;
                foreach (int i in mf.sharedMesh.GetIndices(sub))
                {
                    var p = r.transform.TransformPoint(verts[i]);
                    if (!any) { face = new Bounds(p, Vector3.zero); any = true; } else face.Encapsulate(p);
                }
            }
        }
        if (!any) face = Bounds(screen);
        float w = face.size.x * .94f, h = face.size.y * .92f;
        const float W = 1200f; float H = W * h / Mathf.Max(.1f, w);
        var go = new GameObject("진찰_모니터", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(room, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(W, H);
        rect.position = new Vector3(face.center.x, face.center.y, face.min.z - .012f); rect.rotation = Quaternion.identity; rect.localScale = Vector3.one * (w / W);
        UiKit();
        var line = go.AddComponent<UnityEngine.UI.Image>(); Sliced(line, KitLine, new Color(.45f, .88f, 1f, .22f), .7f);
        var head = UiChild(rect, "머리", Vector2.zero, new Vector2(W, 104)).gameObject.AddComponent<UnityEngine.UI.Image>(); Sliced(head, KitPanel, new Color(.06f, .17f, .23f, .95f), .6f);
        var monitor = go.AddComponent<ClinicMonitor>();
        monitor.title = UiText(rect, "제목", new Vector2(40, -18), new Vector2(520, 70), 60, new Color(.5f, .9f, 1f), TMPro.FontStyles.Bold); monitor.title.text = ending ? "치료 결과" : "진찰 기록";
        var dept = UiText(rect, "과", new Vector2(W - 560, -30), new Vector2(520, 50), 38, new Color(.7f, .8f, .85f)); dept.text = "소화기내과 · 진료실"; dept.alignment = TMPro.TextAlignmentOptions.TopRight;

        // 왼쪽: 증상 · 심전도 · 활력
        var colL = 40f; var colW = 380f;
        UiText(rect, "증상_제목", new Vector2(colL, -140), new Vector2(colW, 50), 40, new Color(.5f, .9f, 1f), TMPro.FontStyles.Bold).text = ending ? "치료 전 증상" : "환자 증상";
        monitor.symptom = UiText(rect, "증상", new Vector2(colL, -196), new Vector2(colW, 170), 54, Color.white, TMPro.FontStyles.Bold);
        monitor.symptom.text = ending ? "이가 시리고\n배가 아팠어요" : "접수를 기다려요";
        UiText(rect, "심전도_제목", new Vector2(colL, -390), new Vector2(colW, 44), 32, new Color(.6f, .7f, .75f)).text = "심전도";
        var ecg = UiChild(rect, "심전도", new Vector2(colL, -440), new Vector2(colW, 140)).gameObject.AddComponent<EcgLine>();
        ecg.color = new Color(.45f, 1f, .62f, .95f); ecg.thickness = 5f; ecg.raycastTarget = false;
        UiText(rect, "활력", new Vector2(colL, -592), new Vector2(colW, 50), 34, new Color(.6f, .7f, .75f)).text = "맥박 78 · 체온 36.5℃";

        // 오른쪽: 몸 스캔 진찰 결과 (자리 번호 0 이, 1 위, 2 대장)
        float colR = W - 40 - colW;
        UiText(rect, "결과_제목", new Vector2(colR, -140), new Vector2(colW, 50), 40, new Color(.5f, .9f, 1f), TMPro.FontStyles.Bold).text = ending ? "진찰했던 곳" : "몸 스캔 진찰";
        var rows = new[] { ("이", "충치를 찾았어요"), ("위", "헬리코박터균"), ("대장", "해로운 균이 많아요") };
        monitor.findings = rows.Select((row, i) =>
        {
            float y = -205 - i * 140;
            var dot = UiChild(rect, "점_" + row.Item1, new Vector2(colR, y - 8), new Vector2(48, 48)).gameObject.AddComponent<UnityEngine.UI.Image>();
            dot.sprite = KitDot; dot.raycastTarget = false; dot.color = ending ? monitor.doneColor : monitor.waitColor; // 실행 전(편집기)에도 제 상태로
            UiText(rect, "이름_" + row.Item1, new Vector2(colR + 66, y), new Vector2(colW - 66, 60), 48, Color.white, TMPro.FontStyles.Bold).text = row.Item1;
            var result = UiText(rect, "결과_" + row.Item1, new Vector2(colR + 66, y - 58), new Vector2(colW - 66, 52), 36, new Color(1f, .78f, .72f));
            result.text = ending ? "치료 완료" : "?";
            return new ClinicMonitor.Finding { dot = dot, result = result, foundText = row.Item2 };
        }).ToArray();
        monitor.status = UiText(rect, "상태", new Vector2(colR, -630), new Vector2(colW, 90), 34, new Color(.5f, .9f, 1f));
        monitor.status.text = ending ? "모두 건강해졌어요!" : "몸 스캔에서 아픈 곳을 찾아요";

        // 아래: 오늘의 치료 경로 (진찰이 끝나면 나타난다)
        float planY = Mathf.Max(730f, H - 200f);
        var plan = UiChild(rect, "치료_경로", new Vector2(40, -planY), new Vector2(W - 80, 180));
        monitor.plan = plan.gameObject.AddComponent<CanvasGroup>();
        UiText(plan, "제목", Vector2.zero, new Vector2(W - 80, 46), 38, new Color(.5f, .9f, 1f), TMPro.FontStyles.Bold).text = ending ? "치료한 곳" : "오늘의 치료 경로";
        var steps = new[] { ("입", "충치 치료"), ("식도", "위산 막기"), ("위", "세균 퇴치"), ("소장", "영양소 흡수"), ("대장", "유익균 돕기") };
        float gap = 44f, chipW = (W - 80 - gap * (steps.Length - 1)) / steps.Length;
        monitor.planSteps = steps.Select((st, i) =>
        {
            var chip = UiChild(plan, "칸_" + st.Item1, new Vector2(i * (chipW + gap), -56), new Vector2(chipW, 116));
            var img = chip.gameObject.AddComponent<UnityEngine.UI.Image>(); Sliced(img, KitPanel, ending ? ClinicMonitor.StepDone : ClinicMonitor.StepTodo, .8f);
            var name = UiText(chip, "기관", new Vector2(0, -8), new Vector2(chipW, 58), 46, Color.white, TMPro.FontStyles.Bold); name.text = st.Item1; name.alignment = TMPro.TextAlignmentOptions.Top;
            var job = UiText(chip, "일", new Vector2(0, -66), new Vector2(chipW, 40), 28, new Color(.85f, .95f, 1f)); job.text = st.Item2; job.alignment = TMPro.TextAlignmentOptions.Top;
            if (i < steps.Length - 1) { var arrow = UiText(plan, "화살표_" + i, new Vector2((i + 1) * (chipW + gap) - gap, -82), new Vector2(gap, 60), 40, new Color(.6f, .75f, .85f), TMPro.FontStyles.Bold); arrow.text = "›"; arrow.alignment = TMPro.TextAlignmentOptions.Top; }
            return img;
        }).ToArray();
        monitor.completeOnStart = ending;
        foreach (var t in go.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) t.ForceMeshUpdate();
        Debug.Log($"[병원] 진찰 모니터: 화면 {face.size.x:0.00}×{face.size.y:0.00}m → 판 {W}×{H:0}");
        return monitor;
    }

    /// <summary>지워진 물체를 가리키는 장면 연결을 걷어 낸다 (다시 적용할 때 예전 모니터 연결이 쌓이지 않게).</summary>
    static void ClearMissing(UnityEngine.Events.UnityEventBase e)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--) if (e.GetPersistentTarget(i) == null) UnityEditor.Events.UnityEventTools.RemovePersistentListener(e, i);
    }
    /// <summary>
    /// 잠수정 탑승구 옆 조작대: 기둥 + 다가오는 쪽으로 기운 윗판 + 판 위 입체 '출발' 버튼(BoardingConsole).
    /// viewer = 다가오는 쪽의 한 점 (조작대가 그쪽을 본다).
    /// </summary>
    static BoardingConsole BoardingConsoleUi(Transform room, Vector3 at, Vector3 viewer, StageExitPortal portal, MissionGate gate)
    {
        foreach (var name in new[] { "탑승_조작대", "탑승_조작대_판" }) { var o = room.Find(name); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var face = viewer - at; face.y = 0; face.Normalize();
        var rot = Quaternion.LookRotation(face);
        var plateRot = rot * Quaternion.Euler(35f, 0, 0); // 윗면이 다가오는 사람 쪽으로 35° 기운다
        var plateCenter = at + Vector3.up * 1.05f;
        var body = new DecorMesh();
        body.Box(at + Vector3.up * .5f, new Vector3(.34f, 1f, .26f), rot, Swatch(2));
        body.Box(at + Vector3.up * .02f, new Vector3(.52f, .04f, .42f), rot, Swatch(1));
        body.Box(plateCenter, new Vector3(.64f, .05f, .46f), plateRot, Swatch(2));
        var stand = DecorObject(room, "탑승_조작대", body, ArtMaterial("진료실_그림", false));
        var solid = stand.gameObject.AddComponent<BoxCollider>(); solid.center = at + Vector3.up * .55f; solid.size = new Vector3(.52f, 1.1f, .52f); // 걸어서 통과하지 않게

        var n = plateRot * Vector3.up;
        var go = new GameObject("탑승_조작대_판", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(room, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(580, 420);
        rect.position = plateCenter + n * .028f;
        rect.rotation = Quaternion.LookRotation(-n, -(plateRot * Vector3.forward)); // 글 위쪽 = 판의 먼 가장자리
        rect.localScale = Vector3.one * .001f;
        UiKit();
        var plate = go.AddComponent<UnityEngine.UI.Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>(); Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);
        var title = UiText(rect, "제목", new Vector2(24, -18), new Vector2(532, 46), 32, new Color(.5f, .9f, 1f), TMPro.FontStyles.Bold); title.text = "초소형 치료 잠수정 · 탑승"; title.alignment = TMPro.TextAlignmentOptions.Top;
        var console = go.AddComponent<BoardingConsole>();
        var b = UiChild(rect, "출발_버튼", new Vector2(90, -86), new Vector2(400, 180));
        console.button = DepthButtonUi(b, "출발!", 80, console.lockedTint, console.lockedTint, 16f);
        var box = b.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(200, -90, 0); box.size = new Vector3(400, 180, 24);
        console.buttonBox = box;
        console.status = UiText(rect, "상태", new Vector2(24, -292), new Vector2(532, 110), 32, Color.white, TMPro.FontStyles.Bold);
        console.status.alignment = TMPro.TextAlignmentOptions.Top; console.status.text = console.lockedText;
        console.portal = portal; console.gate = gate;
        foreach (var t in go.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) t.ForceMeshUpdate();
        EditorUtility.SetDirty(console);
        return console;
    }
}
#endif
