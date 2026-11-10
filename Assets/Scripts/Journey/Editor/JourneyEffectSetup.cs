#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

public static class JourneyEffectSetup
{
    const string Folder = "Assets/Data/Generated/Effects";
    const string ShaderName = "인체탐험/흡수 입자";

    [MenuItem("인체탐험/5. 흡수와 위산 효과 연결", priority = 14)]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new List<string>();
        try
        {
            foreach (var id in new[] { "03_Stomach", "04_SmallIntestine", "05_LargeIntestine" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Journey/" + id + ".unity");
                ConfigureScene(id.Substring(3));
                if (id.EndsWith("Stomach"))
                {
                    var churn = Object.FindFirstObjectByType<StomachChurn>();
                    if (churn.acidVfx == null || churn.acidVfx.Length != 3 || churn.acidVfx.Any(p => p == null || p.main.playOnAwake))
                        throw new Exception("위산 연결 또는 자동 재생 설정 오류");
                }
                else if (id.EndsWith("SmallIntestine"))
                {
                    if (Object.FindFirstObjectByType<NutrientAbsorption>().absorbVfx == null) throw new Exception("영양소 효과 연결 오류");
                }
                else if (Object.FindFirstObjectByType<WallAbsorption>().absorbVfx == null) throw new Exception("수분 효과 연결 오류");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add("통과: " + id + " 효과 연결 및 저장");
            }
            AssetDatabase.SaveAssets();
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("입자 셰이더 오류");
            report.Add("통과: 입자 셰이더 컴파일");
            report.Add("실패 수: 0");
            File.WriteAllLines("Tools/effect_setup_validation.txt", report);
        }
        finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    public static void ConfigureScene(string id)
    {
        if (id != "Stomach" && id != "SmallIntestine" && id != "LargeIntestine") return;
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var root = GameObject.Find("JourneyEffects") ?? new GameObject("JourneyEffects");
        if (id == "Stomach")
        {
            var churn = Object.FindFirstObjectByType<StomachChurn>();
            if (churn == null) throw new Exception("위 소화 연출 없음");
            // 기존 그래프 원본은 보존하며, 장면 인스턴스의 자동 재생을 끈다.
            // 가상현실 기기에서도 사용할 수 있는 가벼운 입자 세 개를 소화 연출에 연결한다.
            var digestion = GameObject.Find("Digestion");
            if (digestion != null)
                foreach (var graph in digestion.GetComponentsInChildren<VisualEffect>(true))
                {
                    graph.Stop();
                    graph.enabled = false;
                    graph.gameObject.SetActive(false);
                    EditorUtility.SetDirty(graph);
                }
            var systems = new List<ParticleSystem>();
            for (int i = 0; i < 3; i++)
            {
                var ps = System(root.transform, "위산_분비_" + (i + 1));
                ps.transform.SetPositionAndRotation(new Vector3(-3 + i * 3, 4, 3), Quaternion.Euler(90, 0, 0));
                Common(ps, new Color(.94f, .86f, .42f, .8f), 96);
                var main = ps.main;
                main.loop = true; main.duration = 1;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(.07f, .15f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.gravityModifier = .2f;
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 24; shape.radius = .28f;
                var emission = ps.emission; emission.rateOverTime = 28;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = Material(false);
                systems.Add(ps);
            }
            churn.acidVfx = systems.ToArray();
            if (churn.churnAudio == null)
            {
                var sound = Child(root.transform, "소화_효과음");
                churn.churnAudio = sound.GetComponent<AudioSource>(); if (churn.churnAudio == null) churn.churnAudio = sound.AddComponent<AudioSource>();
            }
            churn.churnAudio.playOnAwake = false; churn.churnAudio.loop = true;
            churn.churnAudio.volume = .22f; churn.churnAudio.spatialBlend = .5f;
            churn.churnAudio.transform.position = new Vector3(0, 2, 2);
            churn.churnAudio.minDistance = 2; churn.churnAudio.maxDistance = 18;
            EditorUtility.SetDirty(churn);
        }
        else if (id == "SmallIntestine")
        {
            var absorb = Object.FindFirstObjectByType<NutrientAbsorption>();
            var ps = System(root.transform, "영양소_흡수_빛");
            Common(ps, new Color(1f, .86f, .34f, .95f), 512);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.055f, .13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .75f);
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .07f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .15f));
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Material(false);
            absorb.absorbVfx = ps;
            EditorUtility.SetDirty(absorb);
        }
        else
        {
            var water = Object.FindFirstObjectByType<WallAbsorption>();
            var ps = System(root.transform, "수분_흡수_파문");
            Common(ps, new Color(.35f, .86f, 1f, .9f), 96);
            var main = ps.main;
            main.startRotation3D = true;
            main.startLifetime = .85f; main.startSize = .9f; main.startSpeed = 0;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .15f, 1, 1.6f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.mesh = RippleMesh();
            renderer.enableGPUInstancing = false;
            renderer.sharedMaterial = Material(true);
            water.absorbVfx = ps;
            EditorUtility.SetDirty(water);
        }
    }

    static GameObject Child(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }

    static ParticleSystem System(Transform parent, string name)
    {
        var go = Child(parent, name);
        var particle = go.GetComponent<ParticleSystem>(); if (particle == null) particle = go.AddComponent<ParticleSystem>(); return particle;
    }

    static void Common(ParticleSystem ps, Color color, int max)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = max; main.startColor = color;
        main.gravityModifier = 0;
        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 0;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        var tint = ps.colorOverLifetime; tint.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.6f, .6f), new GradientAlphaKey(0, 1) });
        tint.color = gradient;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.minParticleSize = 0; renderer.maxParticleSize = .35f;
        EditorUtility.SetDirty(ps);
    }

    static Material Material(bool ring)
    {
        string path = Folder + (ring ? "/수분_파문.mat" : "/소화_빛입자.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find(ShaderName);
        if (shader == null) throw new Exception("입자 셰이더를 찾을 수 없음");
        mat = new Material(shader);
        mat.SetFloat("_Ring", ring ? 1 : 0);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Mesh RippleMesh()
    {
        string path = Folder + "/파문_평면.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "파문 평면" };
        mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0,1,2,0,2,3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
#endif



