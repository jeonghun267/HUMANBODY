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
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// 신체 장면 다섯 개를 실제 몸속(내시경 화면)처럼 보이게 한다.
/// 1) 점막 재질: 젖은 점액막 반사, 조직 속 붉은 산란, 혈관망, 기관별 표면 결(위샘 구멍·융털 등)
/// 2) 조명: 햇빛 대신 탐험가 전등 — 가까운 벽은 밝게, 먼 곳은 어둡게
/// 3) 캡슐 난간을 눈높이 아래로 내려 시야를 비운다
/// 기존 조직 재질 파일을 그대로 바꾸므로 장면의 재질 연결은 유지된다.
/// </summary>
public static partial class JourneyRealism
{
    public static readonly string[] BodyScenes = { "01_Mouth", "02_Esophagus", "03_Stomach", "04_SmallIntestine", "05_LargeIntestine" };
    const string SceneDir = "Assets/Scenes/Journey";
    const string Folder = "Assets/Data/Generated/Realism";
    const string TissueFolder = "Assets/Data/Generated/EnvironmentRepair";
    public const string ShaderName = "인체탐험/실사 점막";
    static readonly string[] OldTissueShaders = { "인체탐험/차분한 기관 조직", "인체탐험/기관 내부 벽" };

    // ───────────────────────────── 기관별 점막 설정 ─────────────────────────────

    public struct Organ
    {
        public string id, surface, baseHex, deepHex, scatterHex, vesselHex;
        public float detailScale, macroScale, normal, macroNormal, vessel, largeVessel, mottle, cavity;
        public float smoothness, coatSmoothness, coat, wrap, velvet, env;
        public float fogDensity, lampRange, lampIntensity, beamIntensity;
    }

    // 색은 내시경 사진 기준: 입은 분홍, 식도는 옅은 연어색, 위는 주황빛 빨강, 소장은 노르스름한 복숭아색, 대장은 옅은 분홍에 굵은 혈관.
    public static readonly Organ[] Organs =
    {
        new Organ { id = "Mouth", surface = "매끈", baseHex = "#C97A7C", deepHex = "#6E2433", scatterHex = "#F04A3C", vesselHex = "#A1323E",
            detailScale = 1.4f, macroScale = .22f, normal = .55f, macroNormal = .45f, vessel = .16f, largeVessel = .1f, mottle = .22f, cavity = .45f,
            smoothness = .6f, coatSmoothness = .92f, coat = 1f, wrap = .5f, velvet = 0, env = .3f,
            fogDensity = .02f, lampRange = 18, lampIntensity = 2.6f, beamIntensity = 3f },
        new Organ { id = "Esophagus", surface = "매끈", baseHex = "#D69A92", deepHex = "#7A3A40", scatterHex = "#F05A48", vesselHex = "#B0414A",
            detailScale = 1.5f, macroScale = .25f, normal = .5f, macroNormal = .55f, vessel = .3f, largeVessel = .2f, mottle = .18f, cavity = .4f,
            smoothness = .62f, coatSmoothness = .93f, coat = 1.1f, wrap = .45f, velvet = 0, env = .3f,
            fogDensity = .03f, lampRange = 24, lampIntensity = 3.5f, beamIntensity = 5f },
        new Organ { id = "Stomach", surface = "위샘", baseHex = "#D0664F", deepHex = "#5E1A22", scatterHex = "#FF5236", vesselHex = "#8E1E26",
            detailScale = 2.2f, macroScale = .2f, normal = .45f, macroNormal = .7f, vessel = .1f, largeVessel = .14f, mottle = .28f, cavity = .3f,
            smoothness = .55f, coatSmoothness = .94f, coat = 1.3f, wrap = .5f, velvet = 0, env = .35f,
            fogDensity = .016f, lampRange = 36, lampIntensity = 9f, beamIntensity = 16f },
        new Organ { id = "SmallIntestine", surface = "융털", baseHex = "#D49068", deepHex = "#7A3E2C", scatterHex = "#FF7A48", vesselHex = "#A2483A",
            detailScale = 1.3f, macroScale = .3f, normal = 1.1f, macroNormal = .5f, vessel = .05f, largeVessel = .08f, mottle = .2f, cavity = .7f,
            smoothness = .35f, coatSmoothness = .85f, coat = .55f, wrap = .55f, velvet = .45f, env = .2f,
            fogDensity = .03f, lampRange = 24, lampIntensity = 3.5f, beamIntensity = 5f },
        new Organ { id = "LargeIntestine", surface = "매끈", baseHex = "#DDA18E", deepHex = "#84444A", scatterHex = "#F06048", vesselHex = "#A4263A",
            detailScale = 1.3f, macroScale = .2f, normal = .45f, macroNormal = .5f, vessel = .32f, largeVessel = .36f, mottle = .15f, cavity = .35f,
            smoothness = .65f, coatSmoothness = .93f, coat = 1.2f, wrap = .45f, velvet = 0, env = .3f,
            fogDensity = .03f, lampRange = 24, lampIntensity = 3.5f, beamIntensity = 5f },
    };

    public static bool IsBody(string id) => Organs.Any(o => o.id == id);
    static Organ Get(string id) => Organs.First(o => o.id == id);
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    static string StageId(string sceneName) => sceneName.Substring(sceneName.IndexOf('_') + 1);

    /// <summary>기관의 점막 재질. 기존 조직 재질 파일을 그대로 쓰므로 그 재질을 쓰던 모든 부위가 함께 바뀐다.</summary>
    public static Material Mucosa(string id)
    {
        var o = Get(id);
        Directory.CreateDirectory(TissueFolder);
        string path = $"{TissueFolder}/{id}_조직.mat";
        var shader = Shader.Find(ShaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("실사 점막 셰이더 오류: " + ShaderName);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        if (mat.shader != shader) mat.shader = shader;
        mat.enableInstancing = true;
        mat.SetColor("_BaseColor", Hex(o.baseHex));
        mat.SetColor("_DeepColor", Hex(o.deepHex));
        mat.SetColor("_ScatterColor", Hex(o.scatterHex));
        mat.SetColor("_VesselColor", Hex(o.vesselHex));
        mat.SetTexture("_DetailMap", DetailTexture(o.surface));
        mat.SetFloat("_DetailScale", o.detailScale);
        mat.SetFloat("_MacroScale", o.macroScale);
        mat.SetFloat("_NormalStrength", o.normal);
        mat.SetFloat("_MacroNormalStrength", o.macroNormal);
        mat.SetFloat("_VesselStrength", o.vessel);
        mat.SetFloat("_LargeVesselStrength", o.largeVessel);
        mat.SetFloat("_Mottle", o.mottle);
        mat.SetFloat("_Cavity", o.cavity);
        mat.SetFloat("_Smoothness", o.smoothness);
        mat.SetFloat("_CoatSmoothness", o.coatSmoothness);
        mat.SetFloat("_CoatStrength", o.coat);
        mat.SetFloat("_Wrap", o.wrap);
        mat.SetFloat("_Velvet", o.velvet);
        mat.SetFloat("_EnvReflection", o.env);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────────────────────────── 점막 결 자료 생성 ─────────────────────────────
    // R·G 굴곡 방향, B 높이, A 혈관. 모든 무늬가 가장자리에서 이어지도록 주기 함수로 만든다.

    static Texture2D DetailTexture(string surface)
    {
        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/점막결_{surface}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int N = 512;
        int seed = surface == "위샘" ? 23 : surface == "융털" ? 37 : 11;
        var h = new float[N * N];
        var vessel = new float[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = (float)x / N, v = (float)y / N;
            float broad = Fbm(u, v, 6, 4, seed);
            float height;
            if (surface == "위샘")
            {
                // 위 점막의 작은 섬(위소구)과 그 사이 고랑, 섬 위의 위샘 구멍
                Worley(u, v, 14, seed + 1, out float f1, out float f2);
                float plateau = Smooth(.02f, .35f, f2 - f1);
                Worley(u, v, 64, seed + 2, out float p1, out _);
                float pit = 1 - Smooth(0, .28f, p1);
                height = .22f + .55f * plateau * (.8f + .2f * broad) + .2f * broad - .16f * pit * plateau;
            }
            else if (surface == "융털")
            {
                // 촘촘히 솟은 융털 끝
                Worley(u, v, 56, seed + 1, out float f1, out _);
                float dome = Mathf.Pow(Mathf.Clamp01(1 - f1 / .75f), .7f);
                height = .2f + .65f * dome + .15f * broad;
            }
            else
            {
                // 매끈한 점막의 잔물결과 아주 작은 오돌토돌함
                height = .3f + .45f * broad + .25f * Fbm(u, v, 64, 2, seed + 3);
            }

            // 혈관: 휘어진 세포 경계선 위에서 일부 구간만 남겨 가지 친 혈관처럼 보이게 한다
            float wu = u + .06f * (Fbm(u, v, 4, 3, seed + 5) - .5f), wv = v + .06f * (Fbm(u, v, 4, 3, seed + 9) - .5f);
            float big = Line(wu, wv, 5, seed + 11, .05f) * Smooth(.4f, .6f, Fbm(u, v, 3, 2, seed + 13));
            float mid = Line(wu, wv, 13, seed + 15, .035f) * Smooth(.35f, .6f, Fbm(u, v, 5, 2, seed + 17));
            float cap = Line(wu, wv, 30, seed + 19, .03f) * Smooth(.45f, .6f, Fbm(u, v, 8, 2, seed + 21));
            float ves = Mathf.Max(big, Mathf.Max(.75f * mid, .45f * cap));
            vessel[y * N + x] = ves;
            h[y * N + x] = Mathf.Clamp01(height + .05f * ves);
        }

        // 높이 기울기를 굴곡 방향으로 바꾼다. 결 종류마다 기울기 크기가 다르므로 평균 기울기를 맞춘다.
        var gx = new float[N * N]; var gy = new float[N * N]; double sum = 0;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
            float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
            gx[y * N + x] = dx; gy[y * N + x] = dy; sum += Math.Sqrt(dx * dx + dy * dy);
        }
        float k = .45f / Mathf.Max(1e-5f, (float)(sum / (N * N)));
        var pixels = new Color32[N * N];
        for (int i = 0; i < N * N; i++)
        {
            var n = new Vector3(-gx[i] * k, -gy[i] * k, 1).normalized;
            pixels[i] = new Color32((byte)Mathf.RoundToInt((n.x * .5f + .5f) * 255), (byte)Mathf.RoundToInt((n.y * .5f + .5f) * 255),
                (byte)Mathf.RoundToInt(h[i] * 255), (byte)Mathf.RoundToInt(vessel[i] * 255));
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(pixels); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
    static int Wrap(int i, int n) => ((i % n) + n) % n;
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }
    }
    static float ValueNoise(float u, float v, int period, int seed)
    {
        float px = u * period, py = v * period;
        int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
        float fx = px - ix, fy = py - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int x0 = Wrap(ix, period), x1 = Wrap(ix + 1, period), y0 = Wrap(iy, period), y1 = Wrap(iy + 1, period);
        float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
        float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }
    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = .5f, norm = 0;
        for (int o = 0; o < octaves; o++) { sum += ValueNoise(u, v, period << o, seed + o * 31) * amp; norm += amp; amp *= .5f; }
        return sum / norm;
    }
    /// <summary>주기 세포 무늬. f1·f2 는 가장 가까운·두 번째로 가까운 점까지의 거리(세포 한 칸 = 1).</summary>
    static void Worley(float u, float v, int cells, int seed, out float f1, out float f2)
    {
        float px = u * cells, py = v * cells;
        int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(py);
        f1 = f2 = 9;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int gx = cx + dx, gy = cy + dy, hx = Wrap(gx, cells), hy = Wrap(gy, cells);
            float fx = gx + .1f + .8f * Hash(hx, hy, seed), fy = gy + .1f + .8f * Hash(hx, hy, seed + 7);
            float d = Mathf.Sqrt((px - fx) * (px - fx) + (py - fy) * (py - fy));
            if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
        }
    }
    static float Line(float u, float v, int cells, int seed, float width)
    {
        Worley(u, v, cells, seed, out float f1, out float f2);
        return 1 - Smooth(0, width * cells / 5f, f2 - f1);
    }

    // ───────────────────────────── 관 모양 기관의 주름 ─────────────────────────────

    /// <summary>
    /// 관 벽까지의 거리. angle 은 단면 둘레 각도, along 은 이동 경로를 따라간 거리(m).
    /// 이동 경로에서 사방 2.6m 이상 비워야 하므로(통로 검사) 주름은 그 안으로 들어오지 않는다.
    /// </summary>
    public static float TubeRadius(string id, float radius, float angle, float along)
    {
        const float Tau = Mathf.PI * 2;
        if (id == "Esophagus")
        {
            // 식도: 비어 있을 때 접혀 있는 세로 주름 8줄. 길이를 따라 조금씩 휘어지고 높이도 달라진다.
            float meander = .09f * Mathf.Sin(along * .21f) + .05f * Mathf.Sin(along * .53f + 1.3f);
            float ridge = Mathf.Pow(.5f + .5f * Mathf.Cos(8 * (angle + meander)), 3);
            float height = .55f * (.75f + .25f * Mathf.Sin(along * .37f + angle * 3));
            return radius - height * ridge + .04f * Mathf.Sin(angle * 23 + along * 1.7f);
        }
        if (id == "SmallIntestine")
        {
            // 소장: 관을 가로로 두르는 돌림 주름(윤상주름). 한 바퀴를 다 돌지 않는 초승달 모양이 엇갈려 놓인다.
            const float spacing = 1.6f, width = .22f;
            float wall = radius;
            int nearest = Mathf.RoundToInt(along / spacing);
            for (int k = nearest - 1; k <= nearest + 1; k++)
            {
                float center = k * spacing + (Hash(k, 1, 51) - .5f) * .5f;
                float across = (along - center) / width;
                if (Mathf.Abs(across) > 3) continue;
                float facing = Hash(k, 2, 51) * Tau, coverage = Mathf.Lerp(-.75f, -.25f, Hash(k, 3, 51));
                float crescent = Smooth(coverage, coverage + .5f, Mathf.Cos(angle - facing));
                float height = Mathf.Lerp(.38f, .5f, Hash(k, 4, 51)) * (.88f + .12f * Mathf.Sin(angle * 9 + k));
                wall -= height * crescent * Mathf.Exp(-across * across);
            }
            return wall;
        }
        if (id == "LargeIntestine")
        {
            // 대장: 세로 띠(결장띠) 3줄이 벽을 당겨 단면이 둥근 삼각형이 되고, 띠 사이 칸마다 팽기가 부풀며 반달 주름이 칸을 나눈다.
            const float spacing = 2.6f, sector = Tau / 3;
            float rel = Mathf.Repeat(angle - Mathf.PI * .5f, Tau);
            int index = Mathf.FloorToInt(rel / sector);
            float within = rel / sector - index;
            float lobe = Mathf.Sin(Mathf.PI * within);
            float band = Mathf.Exp(-Mathf.Pow(Mathf.Min(within, 1 - within) * sector / .12f, 2));
            float x = Mathf.Repeat(along / spacing + index * .37f, 1);
            float gap = Mathf.Min(x, 1 - x) * spacing;
            float fold = .45f * lobe * Mathf.Exp(-gap * gap / (.3f * .3f));
            float bulge = .3f * lobe * Mathf.Pow(Mathf.Sin(Mathf.PI * x), 2);
            return radius + bulge - fold - .2f * band;
        }
        return radius;
    }

    // ───────────────────────────── 위 내부 ─────────────────────────────
    // 탐험가는 위 몸통 바닥(높이 0)에 서서 유문(z 9.5) 쪽을 본다. 뒤쪽 위로는 식도가 들어오는 분문이 있다.

    /// <summary>z 위치의 위 단면: 중심 높이, 좌우 반지름, 위·아래 반지름, 아래쪽 단면의 각진 정도.</summary>
    static void StomachSection(float z, out float yc, out float rx, out float up, out float low, out float power)
    {
        float antrum = Smooth(2f, 9.5f, z); // 몸통 → 전정부 깔때기
        yc = Mathf.Lerp(4f, 1.8f, antrum);
        rx = Mathf.Lerp(9f, 2.7f, antrum);
        up = Mathf.Lerp(6.5f, 2.7f, antrum);
        low = Mathf.Lerp(4.05f, 2.7f, antrum); // 몸통 바닥이 높이 0 근처에 오도록
        power = Mathf.Lerp(3f, 2f, antrum);    // 바닥을 조금 넓고 평평하게
        float close = 1;
        if (z < -5) close = Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Pow((z + 5) / 6f, 2)));        // 위저부의 둥근 끝
        if (z > 12.5f) close = Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Pow((z - 12.5f) / 2.5f, 2))); // 십이지장 입구 너머
        float pylorus = 1 - .2f * Mathf.Exp(-Mathf.Pow((z - 9.5f) / .6f, 2));                  // 유문 조임
        rx *= close * pylorus; up *= close * pylorus; low *= close * pylorus;
    }

    /// <summary>둘레 각도(0 = 오른쪽, 90° = 위)와 z 의 위벽 점. inward 는 단면 중심 쪽 방향.</summary>
    static Vector3 StomachWall(float angle, float z, out Vector3 inward)
    {
        StomachSection(z, out float yc, out float rx, out float up, out float low, out float power);
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        float x = s >= 0 ? rx * c : rx * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2 / power);
        float y = s >= 0 ? yc + up * s : yc - low * Mathf.Pow(Mathf.Abs(s), 2 / power);
        var point = new Vector3(x, y, z);
        var flat = new Vector3(-x, yc - y, 0);
        inward = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.up;

        // 위주름: 큰굽이(바닥·옆벽)를 따라 유문 쪽으로 길게 달리며 서로 갈라지고 합쳐진다. 위쪽(위저부·작은굽이)은 낮다.
        float meander = .18f * Mathf.Sin(z * .35f + angle * 2) + .1f * Mathf.Sin(z * .9f + angle * 5);
        float ridge = Mathf.Max(Mathf.Pow(.5f + .5f * Mathf.Cos(26 * angle + meander * 6), 4),
                                .6f * Mathf.Pow(.5f + .5f * Mathf.Cos(13 * angle + 1.7f + meander * 9), 6));
        float greater = Smooth(.35f, -.55f, s);
        float height = Mathf.Lerp(.12f, .75f, greater) * Mathf.Lerp(1, .7f, Smooth(4, 9.5f, z));
        bool walkway = s < -.55f && Mathf.Abs(x) < 5.5f && z > -7.5f && z < 6.5f;
        if (walkway) height *= .3f; // 걷는 바닥은 발이 걸리지 않게 낮게
        float fade = Smooth(-11f, -8f, z) * Smooth(15f, 12.5f, z);
        return point + inward * height * ridge * fade;
    }

    static void StomachChamber()
    {
        var shell = GameObject.Find("StomachShell");
        if (shell == null) return;
        var pivot = new Vector3(0, 3, 2);
        const int around = 160, rows = 200; const float zStart = -11f, zEnd = 15f;
        var v = new Vector3[(around + 1) * (rows + 1)];
        var triangles = new List<int>(around * rows * 6);
        for (int j = 0; j <= rows; j++)
        {
            float z = Mathf.Lerp(zStart, zEnd, (float)j / rows);
            for (int i = 0; i <= around; i++)
            {
                int index = j * (around + 1) + i;
                v[index] = StomachWall((float)i / around * Mathf.PI * 2, z, out _) - pivot;
                if (j < rows && i < around) { int next = index + around + 1; triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 }); }
            }
        }
        var mesh = new Mesh { name = "위주름이 있는 위 내부" };
        mesh.vertices = v; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveMesh($"{Folder}/위_내부.asset", ref mesh);

        shell.transform.SetPositionAndRotation(pivot, Quaternion.identity);
        shell.transform.localScale = Vector3.one;
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = shell.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = Mucosa("Stomach"); renderer.shadowCastingMode = ShadowCastingMode.Off;

        // 벽이 숨 쉬듯 수축해도 발밑이 흔들리지 않게, 충돌은 움직이지 않는 별도 물체가 맡는다.
        var floor = GameObject.Find("위벽_충돌") ?? new GameObject("위벽_충돌");
        floor.transform.SetPositionAndRotation(pivot, Quaternion.identity);
        floor.isStatic = true;
        var collider = floor.GetComponent<MeshCollider>();
        if (collider == null) collider = floor.AddComponent<MeshCollider>();
        collider.sharedMesh = null; collider.sharedMesh = mesh;

        var platform = GameObject.Find("StomachPlatform");
        if (platform != null)
        {
            foreach (var r in platform.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in platform.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        // 위샘 모형은 벽 속에 반쯤 묻고, 주름 이름표는 실제 주름이 있는 오른쪽 아래 벽으로 옮긴다.
        var walls = GameObject.Find("Walls");
        if (walls != null)
        {
            foreach (var t in walls.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Part_GastricGland")
                {
                    StomachSection(t.position.z, out float yc, out _, out _, out _, out _);
                    float angle = Mathf.Atan2(t.position.y - yc, t.position.x);
                    var wall = StomachWall(angle, t.position.z, out var inward);
                    t.position = wall - inward * .1f;
                }
                else if (t.name == "Part_RugaeWallPanel")
                {
                    var label = t.GetComponent<PartLabel>();
                    if (label == null) continue;
                    var wall = StomachWall(-25 * Mathf.Deg2Rad, 1.5f, out var inward);
                    t.position = wall + inward * .6f;
                    if (label.labelRoot != null) label.labelRoot.transform.position = t.position + Vector3.up * .5f;
                }
            }
        }
        Cardia();
    }

    /// <summary>식도가 들어오는 분문: 뒤쪽 천장의 오므린 입구. 탐험가가 내려온 곳이라는 걸 보여 준다.</summary>
    static void Cardia()
    {
        var wall = StomachWall(100 * Mathf.Deg2Rad, -6f, out var inward);
        Opening("분문_입구", wall, inward, Mucosa("Stomach"), radius: 2.2f, folds: 10, foldHeight: .14f, depth: .55f, mound: 0);
    }

    /// <summary>
    /// 벽에 난 구멍(분문·십이지장 유두·충수 입구·회맹판). 가장자리는 벽 속에 묻히고, 가운데는 벽 안쪽으로 파이며,
    /// mound 만큼 둔덕이 솟고 folds 개의 방사 주름이 가운데로 모인다. 구멍 바닥에는 빛이 닿지 않는 어두운 점을 둔다.
    /// </summary>
    static GameObject Opening(string name, Vector3 at, Vector3 inward, Material material, float radius, int folds, float foldHeight, float depth, float mound)
    {
        var go = GameObject.Find(name) ?? new GameObject(name);
        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-inward)); // 앞(+z) = 벽 속
        go.transform.localScale = Vector3.one;
        const int around = 64, rings = 14;
        var v = new Vector3[(around + 1) * (rings + 1)];
        var triangles = new List<int>();
        for (int r = 0; r <= rings; r++)
        for (int a = 0; a <= around; a++)
        {
            float u = (float)r / rings, phi = (float)a / around * Mathf.PI * 2;
            float fold = foldHeight * Mathf.Pow(.5f + .5f * Mathf.Cos(folds * phi + u * 2), 3) * Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.4f));
            float pit = depth * Mathf.Exp(-u * u / (.2f * .2f));
            float hill = mound * Mathf.Exp(-u * u / (.55f * .55f));
            float rr = radius * u * (1 - .06f * Mathf.Cos(folds * phi) * (1 - u));
            int index = r * (around + 1) + a;
            v[index] = new Vector3(Mathf.Cos(phi) * rr, Mathf.Sin(phi) * rr, pit - hill - fold + .1f * Mathf.Pow(u, 4));
            if (r < rings && a < around) { int next = index + around + 1; triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 }); }
        }
        var mesh = new Mesh { name = name };
        mesh.vertices = v; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveMesh($"{Folder}/{name}.asset", ref mesh);
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off;

        var hole = go.transform.Find("구멍_안쪽");
        if (hole == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "구멍_안쪽";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(go.transform, false); hole = sphere.transform;
        }
        hole.localPosition = new Vector3(0, 0, depth - mound * .6f + .02f);
        hole.localScale = new Vector3(radius * .16f, radius * .16f, .05f);
        var holeRenderer = hole.GetComponent<Renderer>(); holeRenderer.sharedMaterial = HoleMaterial(); holeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static Material HoleMaterial()
    {
        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/구멍_안쪽.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        // 구멍 안은 빛이 닿지 않는 어둠: 반짝이는 재질은 기본 반사(푸른 하늘)를 비춰 구멍 가운데 파란 줄이 보였다 → 반사 없는 검정
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, path); }
        if (mat.shader != unlit) mat.shader = unlit;
        mat.SetColor("_BaseColor", new Color(.02f, .005f, .008f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────────────────────────── 관 기관 안의 배치 ─────────────────────────────

    /// <summary>관 기관 벽 위의 점. 이동 경로가 z 로 나아가므로 z 에 맞는 경로 위치를 찾고, 그 단면에서 angle 방향의 벽을 구한다.</summary>
    static Vector3 TubeWall(string id, SplineContainer path, float z, float angle, out Vector3 inward)
    {
        float lo = 0, hi = 1;
        for (int k = 0; k < 30; k++) { float mid = (lo + hi) * .5f; if (((Vector3)path.EvaluatePosition(mid)).z < z) lo = mid; else hi = mid; }
        float t = (lo + hi) * .5f;
        Vector3 center = path.EvaluatePosition(t);
        var dir = Vector3.right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
        inward = -dir;
        return center + dir * TubeRadius(id, id == "Esophagus" ? 3.4f : 3.15f, angle, t * path.CalculateLength());
    }

    /// <summary>공중에 떠 있던 부위 모형을 숨기고, 이름표만 벽의 실제 자리로 옮긴다(응시 대상은 작은 구).</summary>
    static void MoveLabelToWall(GameObject part, Vector3 point)
    {
        foreach (var r in part.GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) r.enabled = false;
        foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = false;
        var children = part.GetComponentsInChildren<ParticleSystem>().Select(p => (p.transform, p.transform.position, p.transform.rotation)).ToArray();
        part.transform.position = point;
        foreach (var (t, pos, rot) in children) t.SetPositionAndRotation(pos, rot); // 분비물 위치는 따로 정한다
        var anchor = part.GetComponent<SphereCollider>();
        if (anchor == null) anchor = part.AddComponent<SphereCollider>();
        anchor.enabled = true; anchor.isTrigger = true; anchor.center = Vector3.zero;
        anchor.radius = .7f / Mathf.Max(.01f, part.transform.lossyScale.x);
        var label = part.GetComponent<PartLabel>();
        if (label != null && label.labelRoot != null) label.labelRoot.transform.position = point + Vector3.up * .5f;
    }

    static void SmallIntestineLayout(SplineContainer path)
    {
        var tissue = Mucosa("SmallIntestine");
        // 십이지장 유두: 담즙·이자액이 나오는 왼쪽 벽의 작은 둔덕. 떠 있던 합류부 모형 대신 벽에 만든다.
        var duo = GameObject.Find("Part_DuodenumJunction");
        if (duo != null)
        {
            var wall = TubeWall("SmallIntestine", path, 6f, 200 * Mathf.Deg2Rad, out var inward);
            var papilla = Opening("십이지장_유두", wall, inward, tissue, radius: 1.1f, folds: 3, foldHeight: .05f, depth: .22f, mound: .45f);
            MoveLabelToWall(duo, wall + inward * .5f);
            var tip = wall + inward * .3f;
            foreach (var ps in duo.GetComponentsInChildren<ParticleSystem>(true))
                ps.transform.SetPositionAndRotation(tip + (ps.name == "PancreaticJuice" ? Vector3.forward * .15f : Vector3.zero), Quaternion.LookRotation(inward + Vector3.forward * .3f));
        }
        // 벽 전체가 융털이라 떠 있던 융털 덩어리는 숨기고, 이름표는 오른쪽 위 벽으로.
        var villi = GameObject.Find("Villi");
        if (villi != null)
            foreach (Transform tile in villi.transform)
            {
                if (tile.GetComponent<PartLabel>() != null) MoveLabelToWall(tile.gameObject, TubeWall("SmallIntestine", path, tile.position.z, 35 * Mathf.Deg2Rad, out var inward) + inward * .4f);
                else foreach (var r in tile.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var sway = tile.GetComponent<Sway>(); if (sway != null) sway.enabled = false;
            }
        // 확대 전시(융털 단면)는 실제 벽 바닥에 세운다.
        var zoom = GameObject.Find("VillusZoom");
        if (zoom != null)
        {
            float angle = -Mathf.Acos(Mathf.Clamp(zoom.transform.position.x / 3.15f, -1, 1));
            var floor = TubeWall("SmallIntestine", path, zoom.transform.position.z, angle, out _);
            zoom.transform.position = new Vector3(zoom.transform.position.x, floor.y + .1f, zoom.transform.position.z);
        }
    }

    static void LargeIntestineLayout(SplineContainer path)
    {
        var tissue = Mucosa("LargeIntestine");
        // 충수: 맹장 벽의 작은 구멍. 관 한가운데 떠 있던 충수 모형 대신 구멍을 만든다.
        var appendix = GameObject.Find("Part_Appendix");
        if (appendix != null)
        {
            float z = appendix.transform.position.z;
            var wall = TubeWall("LargeIntestine", path, z, 235 * Mathf.Deg2Rad, out var inward);
            Opening("충수_입구", wall, inward, tissue, radius: .9f, folds: 5, foldHeight: .05f, depth: .45f, mound: .08f);
            MoveLabelToWall(appendix, wall + inward * .45f);
        }
        // 회맹판: 소장이 대장으로 들어오는 입술 모양 입구. 탐험가가 방금 지나온 곳이다.
        var start = (Vector3)path.EvaluatePosition(0);
        var valve = TubeWall("LargeIntestine", path, start.z + 2.5f, 160 * Mathf.Deg2Rad, out var valveInward);
        Opening("회맹판", valve, valveInward, tissue, radius: 1.5f, folds: 2, foldHeight: .18f, depth: .4f, mound: .3f);
    }

    /// <summary>
    /// 잠수정을 기다리는 출발 발판(…StartPlatform, EsophagusTopPlatform)은 잠수정과 같은 금속으로 바꾼다.
    /// 분홍 원판이 점막처럼 보이지 않고, 전등 바로 옆에서도 하얗게 타지 않는다(잠수정 재질은 장면 조명을 받지 않는다).
    /// </summary>
    static void StartPlatforms()
    {
        var metal = CockpitMaterial("출발_발판", new Color(.12f, .15f, .17f), .4f, 0);
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.EndsWith("Platform")).Select(t => t.gameObject))
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = metal;
    }

    // ───────────────────────────── 입: 치아와 잇몸 ─────────────────────────────

    /// <summary>치열 모형은 치아와 잇몸이 한 그림에 들어 있다. 그림의 색으로 둘을 나눠 법랑질은 젖은 흰색, 잇몸은 점막으로 그린다.</summary>
    static void Teeth()
    {
        foreach (var name in new[] { "Part_UpperTeethArch", "Part_LowerTeethArch" })
        {
            var arch = GameObject.Find(name);
            if (arch == null) continue;
            string path = $"{Folder}/치아_잇몸_{name}.mat";
            foreach (var renderer in arch.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    bool converted = src.shader.name == ShaderName;
                    string texProperty = converted ? "_MainTex" : src.HasProperty("baseColorTexture") ? "baseColorTexture" : "_BaseMap";
                    if (!src.HasProperty(texProperty)) continue;
                    var tex = src.GetTexture(texProperty);
                    Vector2 scale = src.GetTextureScale(texProperty), offset = src.GetTextureOffset(texProperty);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(Mucosa("Mouth")); AssetDatabase.CreateAsset(mat, path); }
                    else mat.CopyPropertiesFromMaterial(Mucosa("Mouth"));
                    mat.SetTexture("_MainTex", tex); mat.SetTextureScale("_MainTex", scale); mat.SetTextureOffset("_MainTex", offset);
                    mat.SetFloat("_Teeth", 1); mat.EnableKeyword("_TEETH");
                    mat.SetColor("_BaseColor", Hex("#D98A86"));
                    mat.SetFloat("_DetailScale", 2.4f);
                    EditorUtility.SetDirty(mat);
                    mats[i] = mat;
                }
                renderer.sharedMaterials = mats;
            }
        }
    }

    static void SaveMesh(string path, ref Mesh mesh)
    {
        Directory.CreateDirectory(Folder);
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, path);
    }

    // ───────────────────────────── 장면 적용 ─────────────────────────────

    [MenuItem("인체탐험/7. 실사 점막과 몸속 조명 적용", priority = 17)]
    public static void Apply() => ApplyScenes();

    static bool ApplyScenes()
    {
        // 편집기에서 실행하면 장면을 차례로 열므로, 저장 안 한 작업은 먼저 물어보고 끝나면 원래 장면으로 돌아간다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string previous = EditorSceneManager.GetActiveScene().path;
        StyleSpeechBubble(); PlayerEye(); AudioPass();
        // 신체 장면 + 시작(병원 진료실)과 끝(치료 결과)
        foreach (var name in new[] { "00_Lobby" }.Concat(BodyScenes).Concat(new[] { "06_Theater" }))
        {
            var scene = EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            ConfigureScene(StageId(name));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode && !string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("[JourneyRealism] 실사 점막·몸속 조명 적용 완료");
        return true;
    }

    /// <summary>적용 후 촬영. 배치모드와 편집기 플래그(Library/journey_realism.flag)에서 사용.</summary>
    public static void ApplyAndCapture()
    {
        string previous = EditorSceneManager.GetActiveScene().path;
        if (!ApplyScenes()) return;
        Capture();
        if (Application.isBatchMode) Inspect();
        if (!Application.isBatchMode && !string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
    }

    /// <summary>열린 장면 하나에 적용한다. JourneyBuilder 가 장면을 새로 만들 때도 마지막에 호출한다.</summary>
    public static void ConfigureScene(string id)
    {
        // 시작은 병원 진료실(진찰 → 초소형 치료 잠수정), 끝은 치료 결과 보고서
        if (id == "Lobby") { Hospital(); HandToolIn(); Hud(id); StyleLabels(); return; }
        if (id == "Theater") { EndingClinic(); StyleLabels(); return; }
        if (!IsBody(id)) return;
        var o = Get(id);
        var mucosa = Mucosa(id);

        // 예전 조직 셰이더를 쓰던 남은 재질(가까운 벽 등)도 점막으로 바꾼다.
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && OldTissueShaders.Contains(mats[i].shader.name)) { mats[i] = mucosa; changed = true; }
            if (changed) renderer.sharedMaterials = mats;
        }

        if (id == "Esophagus" || id == "SmallIntestine" || id == "LargeIntestine")
        {
            var ride = Object.FindFirstObjectByType<CapsuleRide>();
            if (ride != null && ride.cart != null && ride.cart.Spline != null)
            {
                JourneyEnvironmentRepair.Tube(id, ride.cart.Spline);
                if (id == "SmallIntestine") SmallIntestineLayout(ride.cart.Spline);
                if (id == "LargeIntestine") LargeIntestineLayout(ride.cart.Spline);
            }
        }
        if (id == "Stomach") StomachChamber();
        if (id == "Mouth") { Teeth(); NewTeeth(); }
        foreach (var gate in Object.FindObjectsByType<SphincterGate>(FindObjectsInactive.Include, FindObjectsSortMode.None)) JourneyEnvironmentRepair.GateSurface(gate, id);
        StartPlatforms();

        Lighting(o);
        LowerRailing();
        Submarines(id);
        Fluids(id);
        Anatomy(id); // 부위마다 제 역할·자리(벽에 심기, 둥실 떠다님, 혈관 속 적혈구)
        Experience(id);
        // 치료 임무 (기관마다 직접 하는 일)
        HandToolIn();
        if (id == "Mouth") { MouthMissions(); MouthActions(); }
        if (id == "Esophagus") EsophagusMission();
        if (id == "Stomach") StomachMission();
        if (id == "SmallIntestine") EnzymeMission();
        if (id == "LargeIntestine") FiberMission();
        Ambience(id);
        StyleLabels(); // 판을 STILL 키트 한 벌로
    }

    static void Lighting(Organ o)
    {
        Color baseColor = Hex(o.baseHex), deep = Hex(o.deepHex);
        // 몸 안에는 햇빛이 없다. 방향광은 캐릭터 윤곽만 살짝 잡는 정도로 낮춘다.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional) { light.intensity = .1f; light.color = Color.Lerp(Color.white, baseColor, .4f); light.shadows = LightShadows.None; }
            else if (light.name == "InteriorLight") { light.intensity = .6f; light.color = Color.Lerp(Color.white, baseColor, .3f); }
        }

        var root = GameObject.Find("몸속_조명") ?? new GameObject("몸속_조명");
        var headlamp = root.GetComponent<BodyHeadlamp>();
        if (headlamp == null) headlamp = root.AddComponent<BodyHeadlamp>();
        headlamp.lamp = LampChild(root.transform, "탐험가_전등", LightType.Point);
        headlamp.lamp.range = o.lampRange; headlamp.lamp.intensity = o.lampIntensity; headlamp.lamp.color = new Color(1f, .95f, .88f);
        headlamp.beam = LampChild(root.transform, "탐험가_앞_빛", LightType.Spot);
        // 안쪽 원뿔을 좁게, 바깥을 넓게 두어 벽에 동그란 빛 자국이 생기지 않게 한다.
        headlamp.beam.range = o.lampRange * 2f; headlamp.beam.intensity = o.beamIntensity; headlamp.beam.spotAngle = 110; headlamp.beam.innerSpotAngle = 10;
        headlamp.beam.color = new Color(1f, .96f, .9f);
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) { headlamp.lamp.transform.position = spawn.transform.position + Vector3.up * 1.7f; headlamp.beam.transform.position = headlamp.lamp.transform.position; }

        // 먼 곳은 점막색이 짙게 가라앉는다. 주변광은 아주 약하게만 남겨 전등이 닿지 않는 곳도 형태는 보이게 한다.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = baseColor * .3f;
        RenderSettings.ambientEquatorColor = baseColor * .24f;
        RenderSettings.ambientGroundColor = deep * .18f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = deep * .28f;
        RenderSettings.fogDensity = o.fogDensity;
        RenderSettings.reflectionIntensity = .35f;
        // 몸속에는 하늘이 없다: 관 끝 틈 등으로 하늘(주황 원판과 지평선 줄)이 비치지 않게 검은 하늘로 둔다
        RenderSettings.skybox = BlackSky();
    }

    static Material BlackSky()
    {
        string path = $"{Folder}/몸속_검은_하늘.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetFloat("_Exposure", 0f); mat.SetFloat("_SunSize", 0f); mat.SetColor("_GroundColor", Color.black); mat.SetColor("_SkyTint", Color.black);
        EditorUtility.SetDirty(mat); return mat;
    }

    static Light LampChild(Transform root, string name, LightType type)
    {
        var t = root.Find(name);
        var light = t != null ? t.GetComponent<Light>() : null;
        if (light == null) { var go = new GameObject(name); go.transform.SetParent(root, false); light = go.AddComponent<Light>(); }
        light.type = type; light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel;
        return light;
    }

    /// <summary>캡슐 난간 가로대가 눈높이 근처에서 시야를 가로지르던 것을 허리 아래로 내리고 어두운 금속색으로 바꾼다.</summary>
    static void LowerRailing()
    {
        foreach (var ride in Object.FindObjectsByType<CapsuleRide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rail = ride.transform.Find("Railing");
            if (rail == null) continue;
            var metal = RailMaterial();
            foreach (Transform part in rail)
            {
                var p = part.localPosition; var s = part.localScale;
                if (part.name == "RailPost") { part.localScale = new Vector3(s.x, .3f, s.z); part.localPosition = new Vector3(p.x, .3f, p.z); }
                else if (part.name == "RailBar") part.localPosition = new Vector3(p.x, .6f, p.z);
                else if (part.name == "FrontFrame") part.localPosition = new Vector3(p.x, .62f, p.z);
                else continue;
                var r = part.GetComponent<Renderer>(); if (r != null) { r.sharedMaterial = metal; r.shadowCastingMode = ShadowCastingMode.Off; }
            }
        }
    }

    static Material RailMaterial()
    {
        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/캡슐_난간.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", new Color(.16f, .2f, .22f));
        mat.SetFloat("_Metallic", .6f);
        mat.SetFloat("_Smoothness", .45f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────────────────────────── 검수 촬영 ─────────────────────────────

    /// <summary>탐험가 눈높이(캡슐 장면은 좌석 위)에서 전등을 켠 채 촬영한다. 결과: Tools/realism_captures/</summary>
    public static void Capture()
    {
        const string outDir = "Tools/realism_captures";
        Directory.CreateDirectory(outDir);
        // 처음 쓰는 셰이더 조합은 편집기가 뒤에서 컴파일하는 동안 임시 색으로 그린다. 촬영은 컴파일을 기다린다.
        ShaderUtil.allowAsyncCompilation = false;
        foreach (var name in new[] { "00_Lobby" }.Concat(BodyScenes).Concat(new[] { "06_Theater" }))
        {
            EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            var ride = Object.FindFirstObjectByType<CapsuleRide>();
            if (ride != null && ride.cart != null && ride.cart.Spline != null)
            {
                var spline = ride.cart.Spline;
                // 잠수정을 그 지점으로 옮겨 놓고, 탐험가 눈(이동 경로 = 잠수정 한가운데)에서 유리창 너머를 찍는다.
                var capsule = ride.transform;
                Vector3 homePos = capsule.position; Quaternion homeRot = capsule.rotation;
                foreach (var (t, label) in new[] { (.03f, "출발"), (.5f, "중간") })
                {
                    Vector3 pos = spline.EvaluatePosition(t);
                    Vector3 dir = ((Vector3)spline.EvaluateTangent(t)).normalized;
                    var flat = new Vector3(dir.x, 0, dir.z);
                    // 식도처럼 아래로 내려가는 통로는 진행 방향(아래)을 바라본다.
                    bool vertical = flat.magnitude < .5f;
                    var yaw = Quaternion.LookRotation(vertical ? Vector3.forward : flat);
                    capsule.SetPositionAndRotation(pos, yaw);
                    var look = vertical ? Quaternion.LookRotation(Vector3.down, Vector3.forward) : yaw;
                    Shoot(outDir, name, label + "_앞", pos, look * Quaternion.Euler(vertical ? 0 : 12, 0, 0));
                    Shoot(outDir, name, label + "_옆", pos, look * Quaternion.Euler(vertical ? -60 : 8, vertical ? 0 : -70, 0));
                    // 부위 배치 확인: 잠수정과 함께 가는 찌꺼기, 출발점에서 올려다본 후두덮개
                    var waste = GameObject.Find("WasteStages");
                    if (waste != null) Shoot(outDir, name, label + "_찌꺼기", pos, Quaternion.LookRotation(waste.transform.position - pos), 70f);
                    var lid = GameObject.Find("후두덮개_경첩");
                    if (lid != null && label == "출발") Shoot(outDir, name, "후두덮개", pos, Quaternion.LookRotation(lid.transform.position + Vector3.down * .8f - pos), 75f);
                }
                var zoom = GameObject.Find("VillusZoom");
                if (zoom != null)
                {
                    // 융털 확대 모형을 지나가며 보는 자리(모형보다 4m 앞의 이동 경로)
                    float lo = 0, hi = 1;
                    for (int k = 0; k < 30; k++) { float mid = (lo + hi) * .5f; if (((Vector3)spline.EvaluatePosition(mid)).z < zoom.transform.position.z - 4f) lo = mid; else hi = mid; }
                    Vector3 at = spline.EvaluatePosition(lo);
                    capsule.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.forward));
                    var shell = zoom.transform.Find("융털_겉");
                    var target = shell != null ? Bounds(shell.gameObject).center : zoom.transform.position + Vector3.up * .3f;
                    Shoot(outDir, name, "융털", at, Quaternion.LookRotation(target - at), 60f);
                }
                capsule.SetPositionAndRotation(homePos, homeRot);
            }
            else
            {
                var spawn = GameObject.Find("SpawnPoint");
                Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;
                Shoot(outDir, name, "앞", eye, Quaternion.Euler(5, 0, 0));
                Shoot(outDir, name, "옆", eye, Quaternion.Euler(5, -70, 0));
                Shoot(outDir, name, "위", eye, Quaternion.Euler(-55, 0, 0));
                var cavity = GameObject.Find("충치_자리");
                if (cavity != null) Shoot(outDir, name, "충치", eye, Quaternion.LookRotation(cavity.transform.position - eye), 50f);
                var teeth = GameObject.Find("새_아랫니");
                if (teeth != null) Shoot(outDir, name, "아랫니", eye, Quaternion.LookRotation(Bounds(teeth).center - eye), 80f);
                // 씹기: 음식을 아래 어금니에 올린 벌린 자세와, 다물어 문 자세 (윗니가 음식 위에 닿아야 한다)
                var chew = Object.FindFirstObjectByType<ChewingSequence>();
                if (chew != null && chew.upperJaw != null && chew.chewPoint != null && chew.foodBolus != null)
                {
                    var jaw = chew.upperJaw; var rest = jaw.localRotation; var food = chew.foodBolus; var home = food.position;
                    var fb = Bounds(food.gameObject); var root = food.position - new Vector3(fb.center.x, fb.min.y, fb.center.z);
                    food.position = chew.chewPoint.position + root;
                    var side = chew.chewPoint.position + new Vector3(chew.chewPoint.position.x < 0 ? 2.4f : -2.4f, 1.3f, -1.6f); // 혀 위 반대편에서 비스듬히
                    Shoot(outDir, name, "씹기_벌림", side, Quaternion.LookRotation(chew.chewPoint.position - side), 55f);
                    jaw.localRotation = rest * Quaternion.AngleAxis(-chew.jawOpenAngle, Vector3.right);
                    food.position = chew.chewPoint.position + root;
                    Shoot(outDir, name, "씹기_다묾", side, Quaternion.LookRotation(chew.chewPoint.position - side), 55f);
                    jaw.localRotation = rest; food.position = home;
                }
                var holo = GameObject.Find("Hologram_DigestiveSystem");
                if (holo != null)
                {
                    // 시작 대기 판이 앞을 가리므로 잠깐 끄고 찍는다
                    var attract = GameObject.Find("체험_시작_대기"); if (attract != null) attract.SetActive(false);
                    Shoot(outDir, name, "홀로그램", eye, Quaternion.LookRotation(Bounds(holo).center - eye), 50f);
                    if (attract != null) attract.SetActive(true);
                }
            }
        }
        Debug.Log($"[JourneyRealism] 촬영 완료 → {outDir}");
    }

    public static void Shoot(string dir, string scene, string view, Vector3 pos, Quaternion rot, float fov = 90f)
    {
        var go = new GameObject("_CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        // 실행 전에는 안내판 글자 모양이 아직 만들어지지 않았을 수 있다.
        Canvas.ForceUpdateCanvases();
        foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
        foreach (var lamp in Object.FindObjectsByType<BodyHeadlamp>(FindObjectsSortMode.None)) lamp.PlaceAt(cam.transform);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes($"{dir}/{scene}_{view}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }

    // ───────────────────────────── 현황 기록 ─────────────────────────────

    /// <summary>장면별 렌더러 셰이더·조명·안개 현황을 Tools/realism_inspect.txt 에 기록한다.</summary>
    public static void Inspect()
    {
        var report = new StringBuilder();
        foreach (var name in BodyScenes)
        {
            EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            report.AppendLine($"=== {name} ===");
            report.AppendLine($"안개 {RenderSettings.fog} {RenderSettings.fogMode} 색 {RenderSettings.fogColor} 밀도 {RenderSettings.fogDensity}");
            report.AppendLine($"주변광 {RenderSettings.ambientMode} 하늘 {RenderSettings.ambientSkyColor} 지평 {RenderSettings.ambientEquatorColor} 땅 {RenderSettings.ambientGroundColor} 반사 {RenderSettings.reflectionIntensity}");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.AppendLine($"조명 {PathOf(light.transform)} {light.type} 세기 {light.intensity} 범위 {light.range} 색 {light.color} 켜짐 {light.isActiveAndEnabled}");
            var groups = new Dictionary<string, List<string>>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!renderer.enabled) continue;
                foreach (var mat in renderer.sharedMaterials)
                {
                    string key = mat == null ? "(없음)" : $"{mat.shader.name} | {mat.name}";
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<string>();
                    list.Add($"{PathOf(renderer.transform)} [{renderer.bounds.size.x:0.0}x{renderer.bounds.size.y:0.0}x{renderer.bounds.size.z:0.0}]");
                }
            }
            foreach (var g in groups.OrderBy(g => g.Key))
                report.AppendLine($"  {g.Key} ×{g.Value.Count}: {string.Join(", ", g.Value.Take(6))}");
        }
        Directory.CreateDirectory("Tools");
        File.WriteAllText("Tools/realism_inspect.txt", report.ToString());
        Debug.Log("[JourneyRealism] 현황 기록 → Tools/realism_inspect.txt");
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
#endif
