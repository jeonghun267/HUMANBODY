#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>장면 생성 시와 기존 장면 편집 시 같은 환경 설정을 사용한다.</summary>
public static class JourneyEnvironments
{
    public const string MaterialDir = "Assets/Data/Environments";
    public const string ShaderName = "인체탐험/환경 배경";

    public struct Preset
    {
        public string id, name, description, baseHex, lightHex, deepHex, accentHex;
        public float mode, folds, strength, rings, detail, tunnel, exposure;
        public Preset(string id, string name, string description, string baseHex, string lightHex, string deepHex,
            string accentHex, float mode, float folds, float strength, float rings, float detail, float tunnel, float exposure = 1)
        {
            this.id = id; this.name = name; this.description = description;
            this.baseHex = baseHex; this.lightHex = lightHex; this.deepHex = deepHex; this.accentHex = accentHex;
            this.mode = mode; this.folds = folds; this.strength = strength; this.rings = rings;
            this.detail = detail; this.tunnel = tunnel; this.exposure = exposure;
        }
    }

    public static readonly Preset[] Presets =
    {
        new Preset("Lobby", "로비_채광돔", "밝은 전시관의 청록빛 채광 돔", "#C4DBDD", "#F2F7EF", "#63838E", "#71C5C6", 0, 8, 0, 0, 0, 0),
        new Preset("Mouth", "입_분홍점막", "분홍색 구강 점막과 부드러운 굴곡", "#B65363", "#EEAAA0", "#6B223B", "#EDAC9E", 1, 6, .36f, .10f, .18f, .12f),
        new Preset("Esophagus", "식도_세로근육", "식도 안쪽의 길게 이어진 근육 주름", "#94435A", "#D98889", "#401B32", "#CA6E78", 1, 10, .80f, .08f, .12f, .82f),
        new Preset("Stomach", "위_위벽주름", "따뜻한 자주색 위벽과 넓은 점막 주름", "#9A4546", "#E29A7C", "#4F1E30", "#DF9872", 1, 8, .82f, .52f, .30f, .24f),
        new Preset("SmallIntestine", "소장_융털조직", "산호색 장벽과 촘촘한 융털 느낌의 표면 결", "#B36B50", "#E7B58A", "#66352C", "#E9BD86", 1, 18, .64f, .60f, .85f, .58f),
        new Preset("LargeIntestine", "대장_분절주름", "차분한 적갈색 장벽과 둥근 분절 주름", "#865761", "#BC908F", "#462C43", "#B98696", 1, 6, .72f, .96f, .12f, .52f),
        new Preset("Theater", "상영관_남색천장", "짙은 남색 돔과 은은한 별빛", "#151F3C", "#31456B", "#080C1C", "#749AAE", 2, 6, 0, 0, 0, 0),
    };

    public static Preset GetPreset(string id)
    {
        foreach (var p in Presets) if (p.id == id) return p;
        throw new ArgumentException("배경 설정을 찾을 수 없는 단계: " + id);
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

    public static Material GetMaterial(string id)
    {
        var p = GetPreset(id);
        string path = MaterialDir + "/" + p.name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();
        var shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("환경 배경 셰이더를 사용할 수 없습니다.");
        var mat = new Material(shader) { name = p.name, enableInstancing = true };
        mat.SetFloat("_Mode", p.mode);
        mat.SetColor("_BaseColor", Hex(p.baseHex));
        mat.SetColor("_LightColor", Hex(p.lightHex));
        mat.SetColor("_DeepColor", Hex(p.deepHex));
        mat.SetColor("_AccentColor", Hex(p.accentHex));
        mat.SetFloat("_FoldCount", p.folds);
        mat.SetFloat("_FoldStrength", p.strength);
        mat.SetFloat("_RingStrength", p.rings);
        mat.SetFloat("_Detail", p.detail);
        mat.SetFloat("_Tunnel", p.tunnel);
        mat.SetFloat("_Exposure", p.exposure);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void MatchEnclosures(string id)
    {
        // 기존 충돌 영역과 연출 구성 요소는 유지하고 배경 표현만 교체한다.
        if (id == "Lobby" || id == "Theater")
        {
            var cover = GameObject.Find(id == "Lobby" ? "CeilingRing" : "Env_TheaterDome");
            if (cover != null)
                foreach (var renderer in cover.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
        if (id != "Mouth" && id != "Stomach") return;
        var shell = GameObject.Find(id == "Mouth" ? "MouthDome" : "StomachShell");
        if (shell == null) return;
        var p = GetPreset(id);
        string path = MaterialDir + "/" + p.name + "_가까운벽.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("인체탐험/기관 내부 벽");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("기관 내부 벽 셰이더 오류");
            material = new Material(shader);
            material.CopyPropertiesFromMaterial(GetMaterial(id));
            material.name = p.name + "_가까운벽";
            AssetDatabase.CreateAsset(material, path);
        }
        foreach (var renderer in shell.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
    }

    public static void Configure(OrganStage stage)
    {
        var p = GetPreset(stage.stageId);
        RenderSettings.skybox = GetMaterial(stage.stageId);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        if (stage.stageId == "Lobby")
        {
            RenderSettings.ambientSkyColor = Hex("#C0D8D9");
            RenderSettings.ambientEquatorColor = Hex("#99AEB4");
            RenderSettings.ambientGroundColor = Hex("#626F79");
        }
        else if (stage.stageId == "Theater")
        {
            RenderSettings.ambientSkyColor = Hex("#65748F");
            RenderSettings.ambientEquatorColor = Hex("#424E65");
            RenderSettings.ambientGroundColor = Hex("#242D43");
        }
        else
        {
            RenderSettings.ambientSkyColor = Color.Lerp(Hex(p.lightHex), Color.white, .15f) * .66f;
            RenderSettings.ambientEquatorColor = Hex(p.lightHex) * .4f;
            RenderSettings.ambientGroundColor = Hex(p.deepHex) * .6f;
        }
        bool organ = p.mode == 1;
        RenderSettings.fog = organ;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(Hex(p.baseHex), Hex(p.lightHex), .42f);
        RenderSettings.fogDensity = .004f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = stage.stageId == "Theater" ? .25f : .5f;
    }
}
#endif
