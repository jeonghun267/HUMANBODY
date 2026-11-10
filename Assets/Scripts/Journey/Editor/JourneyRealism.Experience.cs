#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>체험 요소: 안내판(HUD), 입의 충치 치료, 대장의 해로운 균 물리치기.</summary>
public static partial class JourneyRealism
{
    static readonly Dictionary<string, string> Goals = new Dictionary<string, string>
    {
        { "Lobby", "몸 스캔에서 아픈 곳을 찾아 진찰한 뒤, 초소형 치료 잠수정에 타세요" },
        { "Mouth", "혀 위의 음식 쪽으로 걸어가 꼭꼭 씹어 보세요" },
        { "Esophagus", "아래에서 올라오는 위산 방울을 겨냥해 중화제를 쏘세요" },
        { "Stomach", "헬리코박터균을 물리치고, 음식이 녹아 유문이 열리면 출발해요" },
        { "SmallIntestine", "떠다니는 영양소를 손으로 가리키고 방아쇠를 당겨 흡수시키세요" },
        { "LargeIntestine", "유익균은 지키고 해로운 균만 물리치세요" },
    };
    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "Lobby", "병원" }, { "Mouth", "입" }, { "Esophagus", "식도" }, { "Stomach", "위" }, { "SmallIntestine", "소장" }, { "LargeIntestine", "대장" },
    };
    static readonly string[] Route = { "병원", "입", "식도", "위", "소장", "대장" };

    static void Experience(string id)
    {
        var hud = Hud(id);
        if (id == "Mouth") Cavity();
        if (id == "LargeIntestine") Germs();
        if (id == "SmallIntestine")
        {
            // 흡수 진행은 안내판 상태 줄에 보여 주고, 예전 떠 있던 글자는 끈다.
            var absorb = Object.FindFirstObjectByType<NutrientAbsorption>();
            if (absorb != null && hud != null) absorb.progressText = hud.status;
            var old = GameObject.Find("ProgressUI"); if (old != null) old.SetActive(false);
        }
    }

    static TMP_FontAsset KoreanFont()
    {
        foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("KoreanTyper") || path.Contains("Korean") || path.Contains("Nanum") || path.Contains("Noto")) return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
        return null;
    }

    static RectTransform UiChild(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return rect;
    }

    static TextMeshProUGUI UiText(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var text = UiChild(parent, name, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = KoreanFont() ?? text.font; text.fontSize = fontSize; text.color = color; text.fontStyle = style;
        text.enableWordWrapping = true; text.richText = true; text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// 안내판: 잠수정 장면은 조종대 위 화면(눈에서 0.86m, 25° 아래), 걷는 장면은 시선 24° 아래 1m 에 게으르게 따라온다.
    /// 글자는 가상현실에서 편하게 읽히는 크기(본문 약 1.8°, 작은 글자 약 1.5°)로 맞춘다.
    /// </summary>
    static JourneyHud Hud(string id)
    {
        var old = GameObject.Find("체험_안내판"); if (old != null) Object.DestroyImmediate(old);
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        var anchor = ride != null ? ride.transform.Find("관찰_잠수정/계기판_화면") : null;

        var go = new GameObject("체험_안내판", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(560, 270);
        var hud = go.AddComponent<JourneyHud>();
        if (anchor != null)
        {
            // 예전 화면 자리(37° 아래)는 고개를 많이 숙여야 해서 25° 아래로 올린다 (조종대 판보다 위)
            anchor.localPosition = ScreenSpot; anchor.localRotation = Quaternion.LookRotation(ScreenSpot);
            rect.SetParent(anchor, false); rect.localPosition = Vector3.zero; rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * .0008f; // 0.86m 앞에서 폭 45cm(약 29°) — 시야를 너무 덮지 않게
            hud.ride = ride; hud.cockpit = true; hud.cockpitOffset = ScreenSpot;
        }
        else
        {
            var spawn = GameObject.Find("SpawnPoint");
            rect.position = (spawn != null ? spawn.transform.position : Vector3.zero) + new Vector3(0, 1.0f, .9f);
            rect.localScale = Vector3.one * .0012f;
            hud.followGaze = true; hud.followDistance = 1f; hud.followDown = 24f; hud.recenterAngle = 20f;
        }
        hud.stageName = Names[id];
        hud.goalText = Goals[id];
        hud.routeNames = Route;

        // STILL 과 같은 판: 어두운 바다빛 둥근 판 + 옅은 하늘색 2px 외곽선(첫 자식이라 글자 밑에 깔린다)
        UiKit();
        var plate = go.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);

        hud.title = UiText(rect, "기관", new Vector2(24, -14), new Vector2(300, 54), 42, Color.white, FontStyles.Bold);
        // 상태 칩: 점수·진행률 (비어 있으면 숨긴 것처럼 흐리다)
        var chip = UiChild(rect, "상태_칩", new Vector2(316, -20), new Vector2(220, 44)).gameObject.AddComponent<Image>();
        Sliced(chip, KitPanel, new Color(PrimaryTint.r, PrimaryTint.g, PrimaryTint.b, .35f), 1f);
        hud.status = UiText(chip.transform, "상태", new Vector2(10, -5), new Vector2(200, 34), 24, new Color(.75f, .95f, 1f), FontStyles.Bold);
        hud.status.alignment = TextAlignmentOptions.Center; hud.status.enableWordWrapping = false; hud.status.overflowMode = TextOverflowModes.Ellipsis;
        hud.statusChip = chip.gameObject;

        // 소화 경로 단계: 점과 이름 (입 · 식도 · 위 · 소장 · 대장)
        int count = hud.routeNames.Length;
        hud.stepDots = new Image[count]; hud.stepLabels = new TextMeshProUGUI[count];
        const float left = 44, right = 516;
        var track = UiChild(rect, "경로_선", new Vector2(left, -88), new Vector2(right - left, 2)).gameObject.AddComponent<Image>();
        track.color = new Color(1, 1, 1, .15f); track.raycastTarget = false;
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(left, right, count == 1 ? .5f : (float)i / (count - 1));
            var dot = UiChild(rect, "경로_점_" + i, new Vector2(x - 9, -80), new Vector2(18, 18)).gameObject.AddComponent<Image>();
            dot.sprite = KitDot; dot.raycastTarget = false; dot.rectTransform.pivot = new Vector2(.5f, .5f); dot.rectTransform.anchoredPosition = new Vector2(x, -89);
            var label = UiText(rect, "경로_이름_" + i, new Vector2(x - 45, -102), new Vector2(90, 30), 22, Color.white);
            label.alignment = TextAlignmentOptions.Top; label.text = hud.routeNames[i];
            hud.stepDots[i] = dot; hud.stepLabels[i] = label;
        }

        hud.goal = UiText(rect, "할일", new Vector2(24, -140), new Vector2(512, 84), 27, new Color(.92f, .96f, .98f));
        if (ride != null)
        {
            var barBack = UiChild(rect, "진행_바탕", new Vector2(24, -238), new Vector2(512, 12)).gameObject.AddComponent<Image>();
            Sliced(barBack, KitPanel, new Color(1, 1, 1, .07f), 3f);
            var fill = UiChild(barBack.transform, "진행", Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>(); Sliced(fillImage, KitPanel, PrimaryTint, 3f);
            hud.progressFill = fill;
        }
        // 자막 판: 안내판 바로 위(시선과 안내판 사이). 안내 병아리 대신 목소리 내레이션의 자막이 여기 나온다
        var sub = UiChild(rect, "자막", new Vector2(0, 118), new Vector2(560, 104));
        var subPlate = sub.gameObject.AddComponent<Image>(); Sliced(subPlate, KitPanel, PlateFill, 1.4f);
        var subLine = UiChild(sub, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(); Stretch(subLine.rectTransform); Sliced(subLine, KitLine, PlateLine, 1.4f);
        hud.subtitle = UiText(sub, "글", new Vector2(20, -12), new Vector2(520, 80), 28, Color.white);
        hud.subtitle.alignment = TextAlignmentOptions.Center; hud.subtitle.text = "";
        hud.subtitlePanel = sub.gameObject; sub.gameObject.SetActive(false);

        // 실행 전(편집기·촬영)에도 내용이 보이도록 미리 채운다. 실행하면 JourneyHud 가 다시 채운다.
        hud.title.text = Names[id];
        hud.goal.text = Goals[id];
        hud.SetStatus("");
        hud.ShowSteps(Names[id]);
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();
        return hud;
    }

    // ───────────────────────────── 안내판 그림 (STILL 의 UI 키트와 같은 방식) ─────────────────────────────
    // 판·외곽선·점을 직접 그려 모서리 반경을 맞춘다. 색도 STILL 과 같다.

    static readonly Color PlateFill = new Color(.05f, .11f, .15f, .94f);
    static readonly Color PlateLine = new Color(.45f, .88f, 1f, .40f);
    static readonly Color PrimaryTint = new Color(.16f, .55f, .78f, 1f);
    const string KitFolder = Folder + "/UI";
    static Sprite KitPanel => AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Kit_Panel.png");
    static Sprite KitLine => AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Kit_Line.png");
    static Sprite KitDot => AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Kit_Dot.png");

    static void UiKit()
    {
        System.IO.Directory.CreateDirectory(KitFolder);
        KitSprite(KitFolder + "/Kit_Panel.png", 48, 14, (x, y) => KitFill(RoundedRect(x, y, 48, 10)));
        KitSprite(KitFolder + "/Kit_Line.png", 96, 28, (x, y) => { float d = RoundedRect(x, y, 96, 24); return KitFill(d) * Mathf.Clamp01(.5f + d + 2f); });
        KitSprite(KitFolder + "/Kit_Dot.png", 32, 0, (x, y) => KitFill(Mathf.Sqrt((x - 16) * (x - 16) + (y - 16) * (y - 16)) - 15));
    }

    /// <summary>둥근 사각형 경계까지의 부호 있는 거리(px). 안쪽이 음수.</summary>
    static float RoundedRect(float x, float y, int size, float radius)
    {
        float half = size * .5f, qx = Mathf.Abs(x - half) - (half - radius), qy = Mathf.Abs(y - half) - (half - radius);
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
    }
    static float KitFill(float distance) => Mathf.Clamp01(.5f - distance);

    static void KitSprite(string path, int size, int border, System.Func<float, float, float> alpha)
    {
        if (System.IO.File.Exists(path)) return;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x + .5f, y + .5f) * 255));
        tex.SetPixels32(pixels); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static void Sliced(Image image, Sprite sprite, Color color, float multiplier)
    {
        image.sprite = sprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = multiplier;
        image.fillCenter = true; image.color = color; image.raycastTarget = false;
    }

    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    static GameObject Prefab(string name)
    {
        var light = LitePrefab(name); // 떼로 나오는 세균·바이러스는 가벼운 판
        if (light != null) return light;
        var animated = JourneyPolishSetup.AnimatedPrefab(name);
        return animated != null ? animated : AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Meshy/{name}.glb");
    }

    static Material UnlitColor(string name, Color color)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color); EditorUtility.SetDirty(mat); return mat;
    }

    static LineRenderer BeamLine(Transform parent, string name, Color color, float width)
    {
        var t = Child(parent, name);
        var line = t.GetComponent<LineRenderer>(); if (line == null) line = t.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = UnlitColor(name, color); line.widthMultiplier = width; line.positionCount = 2; line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
        return line;
    }

    static ParticleSystem Burst(Transform parent, string name, Material material, Vector2 size, Vector2 speed, Vector2 life)
    {
        var t = Child(parent, name);
        var ps = t.GetComponent<ParticleSystem>(); if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y); main.gravityModifier = -.03f; main.startColor = Color.white;
        var emission = ps.emission; emission.rateOverTime = 0;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .1f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    static NarrationLine[] Say(string text, float seconds = 5f) => new[] { new NarrationLine { text = text, holdSeconds = seconds } };

    // ───────────────────────────── 입: 충치 치료 ─────────────────────────────

    static void Cavity()
    {
        var spawn = GameObject.Find("SpawnPoint");
        var filter = ToothSurfaces(true).FirstOrDefault(); // 새 이 모형의 아랫니 (없으면 예전 치열)
        if (spawn == null || filter == null || filter.sharedMesh == null) return;
        var lower = filter.transform;
        // 예전 자리는 먼저 지운다(못 찾았을 때 숨겨진 예전 치열 위에 검은 조각만 떠 있지 않게)
        foreach (var stale in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name == "충치_자리").ToArray()) Object.DestroyImmediate(stale.gameObject);

        // 탐험가 오른쪽 가까운 아래 어금니의 씹는 면을 찾는다(임시 충돌로 위에서 아래로 쏘아 본다).
        var probe = filter.gameObject.AddComponent<MeshCollider>(); probe.sharedMesh = filter.sharedMesh;
        Physics.SyncTransforms();
        Vector3 origin = spawn.transform.position; RaycastHit best = default; float bestScore = float.MaxValue;
        // 씹는 면 한가운데: 둘레 여덟 방향도 비슷한 높이의 위를 향한 면이어야 한다(이 사이 틈이나 모서리는 피한다).
        // 실제 어금니처럼 교두(봉우리)가 있는 모형은 평평한 곳이 좁아, 못 찾으면 조건을 조금씩 푼다.
        bool Flat(Vector3 center, float height, float radius, float tolerance)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4;
                var from = center + new Vector3(Mathf.Cos(a) * radius, 8f, Mathf.Sin(a) * radius);
                if (!probe.Raycast(new Ray(from, Vector3.down), out var near, 20f) || near.normal.y < .5f || Mathf.Abs(near.point.y - height) > tolerance) return false;
            }
            return true;
        }
        foreach (var (radius, tolerance, upright) in new[] { (.32f, .15f, .85f), (.22f, .2f, .75f), (.15f, .28f, .6f) })
        {
            foreach (float side in new[] { 1f, -1f })
            for (float dx = 1.5f; dx <= 5f; dx += .15f)
            for (float dz = -2f; dz <= 3f; dz += .15f)
            {
                var from = origin + new Vector3(side * dx, 8f, dz);
                if (!probe.Raycast(new Ray(from, Vector3.down), out var hit, 20f) || hit.normal.y < upright) continue;
                if (!Flat(new Vector3(hit.point.x, 0, hit.point.z), hit.point.y, radius, tolerance)) continue;
                float score = Mathf.Abs(Vector3.Distance(new Vector3(hit.point.x, origin.y, hit.point.z), origin) - 2.6f) + (side < 0 ? .5f : 0);
                if (score < bestScore) { bestScore = score; best = hit; }
            }
            if (bestScore < float.MaxValue) { Debug.Log($"[JourneyRealism] 충치 자리: {best.point} (평평한 반지름 {radius})"); break; }
        }
        if (bestScore == float.MaxValue) { Object.DestroyImmediate(probe); Debug.LogWarning("[JourneyRealism] 충치 자리를 찾지 못함"); return; }

        var site = new GameObject("충치_자리").transform;
        site.SetPositionAndRotation(best.point, Quaternion.FromToRotation(Vector3.up, best.normal));
        site.SetParent(lower, true); // 씹을 때 아래턱과 함께 움직인다

        // 이 표면을 따라 붙도록 각 점을 위에서 이 모형으로 내려 찍는다(스티커처럼 떠 보이지 않게).
        Vector3 Conform(Vector3 local, float lift)
        {
            var from = site.TransformPoint(local + Vector3.up * .6f);
            // 교두(봉우리) 옆면이나 옆 이에 맞은 점은 쓰지 않는다(가시처럼 튀어나온다): 위를 향한 면, 가운데 높이에서 멀지 않은 점만
            if (probe.Raycast(new Ray(from, -site.up), out var hit, 2f) && Vector3.Dot(hit.normal, site.up) > .45f)
            {
                var p = site.InverseTransformPoint(hit.point);
                return new Vector3(p.x, Mathf.Clamp(p.y, -.12f, .12f) + lift, p.z);
            }
            return new Vector3(local.x, lift, local.z);
        }

        // 충치: 살짝 검은 구멍과 그 둘레의 거무스름한 자국 (가장자리가 고르지 않다)
        var decay = new GameObject("충치").transform; decay.SetParent(site, false);
        // 이 표면과 깊이가 겹쳐 깜빡이지 않도록 조금씩 띄운다(이 하나가 1m 넘는 크기라 몇 cm 는 보이지 않는다).
        Blob(decay, "충치_자국", .34f, .03f, 0f, Lit("충치_자국", new Color(.2f, .16f, .12f), .35f), 61, Conform);
        Blob(decay, "충치_검은부분", .2f, .055f, .025f, Lit("충치_검은부분", new Color(.045f, .035f, .03f), .3f), 62, Conform);
        var fillingT = new GameObject("충전재").transform; fillingT.SetParent(site, false);
        Blob(fillingT, "충전재_면", .27f, .04f, -.008f, Lit("충전재", new Color(.94f, .93f, .89f), .85f), 63, Conform);
        Object.DestroyImmediate(probe);
        var oldStain = decay.Find("충치_갈색자국"); if (oldStain != null) Object.DestroyImmediate(oldStain.gameObject);

        var root = GameObject.Find("충치_치료") ?? new GameObject("충치_치료");
        var treat = root.GetComponent<CavityTreatment>(); if (treat == null) treat = root.AddComponent<CavityTreatment>();
        treat.decay = decay;
        treat.filling = fillingT.GetComponentInChildren<Renderer>();
        treat.filling.enabled = false; // 치료가 끝나야 보인다
        treat.beam = BeamLine(root.transform, "치료_광선", new Color(.45f, .62f, 1f), .012f); // 치과 광중합기의 파란 빛
        treat.sparkle = Burst(root.transform, "치료_반짝임", Droplet("반짝임", new Color(.8f, .9f, 1f, .8f), rim: 1.2f, highlight: 2.5f), new Vector2(.03f, .07f), new Vector2(.2f, .6f), new Vector2(.4f, .8f));
        treat.startLines = Say("어금니에 까만 충치가 보여! 파란 치료 광선으로 충치를 비추고 방아쇠를 꾹 누르고 있어 봐.", 6f);
        treat.doneLines = Say("충치를 깨끗하게 치료했어! 단 걸 먹은 뒤에는 꼭 이를 닦자.", 5f);
        treat.goalText = "어금니의 까만 충치를 치료 광선으로 비추고 방아쇠를 누르고 있으세요";
        EditorUtility.SetDirty(treat);
    }

    /// <summary>가장자리가 울퉁불퉁한 얇은 조각. conform 으로 표면에 붙이고, depth 만큼 가운데가 파인다.</summary>
    static void Blob(Transform parent, string name, float radius, float lift, float depth, Material material, int seed, System.Func<Vector3, float, Vector3> conform)
    {
        const int around = 40, rings = 6;
        var v = new List<Vector3> { conform(Vector3.zero, lift - depth) }; var tri = new List<int>();
        for (int r = 1; r <= rings; r++)
        for (int a = 0; a < around; a++)
        {
            float u = (float)r / rings, phi = (float)a / around * Mathf.PI * 2;
            float edge = radius * (1 + .22f * (Fbm(phi / (Mathf.PI * 2), .5f, 5, 2, seed) - .5f) * 2);
            v.Add(conform(new Vector3(Mathf.Cos(phi) * edge * u, 0, Mathf.Sin(phi) * edge * u), lift - depth * (1 - u * u)));
        }
        for (int a = 0; a < around; a++) tri.AddRange(new[] { 0, 1 + (a + 1) % around, 1 + a });
        for (int r = 1; r < rings; r++)
        for (int a = 0; a < around; a++)
        {
            int k = 1 + (r - 1) * around + a, k2 = 1 + (r - 1) * around + (a + 1) % around, n = k + around, n2 = k2 + around;
            tri.AddRange(new[] { k, k2, n, n, k2, n2 });
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SaveMesh($"{Folder}/{name}.asset", ref mesh);
        var t = Child(parent, name); t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var mf = t.GetComponent<MeshFilter>(); if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = t.GetComponent<MeshRenderer>(); if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Material Lit(string name, Color color, float smoothness)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness); mat.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(mat); return mat;
    }

    // ───────────────────────────── 대장: 해로운 균 물리치기 ─────────────────────────────

    static void Germs()
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) return;
        var root = GameObject.Find("균_물리치기") ?? new GameObject("균_물리치기");
        var blaster = root.GetComponent<GermBlaster>(); if (blaster == null) blaster = root.AddComponent<GermBlaster>();
        blaster.harmfulPrefabs = new[] { Prefab("Char_HarmfulBacteria"), Prefab("Char_Virus") }.Where(p => p != null).ToArray();
        blaster.helpfulPrefabs = new[] { Prefab("Char_Probiotic") }.Where(p => p != null).ToArray();
        blaster.ride = ride;
        blaster.beam = BeamLine(root.transform, "면역_광선", new Color(.4f, .95f, 1f), .02f);
        blaster.hitBurst = Burst(root.transform, "균_사라짐", Droplet("균_사라짐", new Color(.75f, 1f, .85f, .7f), rim: 1.4f, highlight: 1.6f), new Vector2(.04f, .12f), new Vector2(.6f, 1.6f), new Vector2(.4f, .9f));
        blaster.startLines = Mission("LargeIntestine", "germ_start", 7f);
        blaster.helpfulHitLines = Mission("LargeIntestine", "germ_helpful", 3.5f);
        blaster.clearedLines = Mission("LargeIntestine", "germ_done", 5f);
        blaster.targetLabel = "해로운 균"; blaster.scoreKey = "germs"; blaster.scoreLabel = "해로운 균 퇴치";
        blaster.goalText = "해로운 균을 겨냥하고 방아쇠를 당겨 물리치세요 (초록 유익균은 지켜요)";
        blaster.clearedStatus = "해로운 균을 모두 물리쳤어요!";
        blaster.releaseRide = true; blaster.followBasis = false; blaster.spots = new Transform[0];

        // 기존 정차 사이(.45)에 '균을 다 없앨 때까지 기다리는' 정차를 넣는다.
        var stops = (ride.stops ?? new CapsuleRide.RideStop[0]).Where(s => !s.holdUntilReleased).ToList();
        var hold = new CapsuleRide.RideStop { normalizedPosition = .45f, lines = new NarrationLine[0], waitSeconds = .5f, onReached = new UnityEvent(), holdUntilReleased = true, maxHoldSeconds = 150f };
        UnityEventTools.AddPersistentListener(hold.onReached, blaster.Begin);
        stops.Add(hold);
        ride.stops = stops.OrderBy(s => s.normalizedPosition).ToArray();
        EditorUtility.SetDirty(ride); EditorUtility.SetDirty(blaster);
    }
}
#endif
