#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

public static class JourneyEffectValidation
{
    const string Prefix = "_효과검증_";
    static Action<bool, string> check;

    public static void PrepareScenes()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("검증용 소리 수신").AddComponent<AudioListener>();
        foreach (string id in new[] { "03_Stomach", "04_SmallIntestine", "05_LargeIntestine" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Journey/" + id + ".unity", OpenSceneMode.Additive);
            var roots = scene.GetRootGameObjects();
            var group = new GameObject(Prefix + id);
            SceneManager.MoveGameObjectToScene(group, scene);
            foreach (var root in roots)
            {
                // 실제 장면 자료를 쓰되 자동 안내, 탑승, 장면 전환은 검사에서 직접 제어한다.
                if (root.GetComponent<PersistentBootstrap>() != null) root.SetActive(false);
                foreach (var zone in root.GetComponentsInChildren<NarrationZone>(true)) zone.enabled = false;
                foreach (var portal in root.GetComponentsInChildren<StageExitPortal>(true)) portal.enabled = false;
                foreach (var ride in root.GetComponentsInChildren<CapsuleRide>(true)) ride.autoBoard = false;
                foreach (var sound in root.GetComponentsInChildren<SceneAmbience>(true)) sound.enabled = false;
                root.transform.SetParent(group.transform, true);
            }
            group.SetActive(id == "03_Stomach");
        }
    }

    static GameObject Group(string id) => Resources.FindObjectsOfTypeAll<GameObject>().Single(g => g.name == Prefix + id && g.scene.IsValid());

    static IEnumerator Pause(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    static IEnumerator Until(Func<bool> condition, string label, float seconds = 5)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("효과 검사 시간 초과: " + label);
            yield return null;
        }
    }

    static ParticleSystem.Particle[] Particles(ParticleSystem ps)
    {
        var all = new ParticleSystem.Particle[ps.main.maxParticles];
        int count = ps.GetParticles(all);
        return all.Take(count).ToArray();
    }

    public static IEnumerator RunChecks(Action<bool, string> record)
    {
        check = record;
        var stomach = Group("03_Stomach");
        var small = Group("04_SmallIntestine");
        var large = Group("05_LargeIntestine");
        SceneManager.SetActiveScene(stomach.scene);
        yield return Pause(.1f);
        var churn = stomach.GetComponentInChildren<StomachChurn>();
        check(churn.acidVfx.Length == 3 && churn.acidVfx.All(p => p != null), "위산 방출기 세 개 연결");
        check(churn.acidVfx.All(p => !p.isPlaying && p.particleCount == 0), "소화 전 위산 자동 재생 없음");
        check(stomach.GetComponentsInChildren<VisualEffect>(true).All(v => !v.enabled && !v.gameObject.activeSelf), "기존 위산 그래프의 중복 자동 재생 차단");
        check(churn.churnAudio != null && churn.churnAudio.clip != null, "소화 효과음 준비");
        var amplitudes = churn.walls.Select(w => w.amplitude).ToArray();
        var foodScales = churn.foodBoluses.Select(f => f.transform.localScale).ToArray();
        int digested = 0;
        churn.onDigested.AddListener(() => digested++);
        churn.digestDuration = 1.2f;
        churn.Begin(); churn.Begin();
        yield return Until(() => churn.acidVfx.All(p => p.particleCount > 0), "위산 방출");
        check(churn.acidVfx.All(p => p.isEmitting), "소화 시작 시 위산 방출");
        check(churn.walls.Select((w, i) => w.amplitude > amplitudes[i]).All(v => v), "소화 중 위벽 수축 강화");
        yield return Pause(.35f);
        Capture("위산_분비", new Vector3(0, 3.2f, -3), new Vector3(0, 2.7f, 3), churn.acidVfx);
        yield return Until(() => digested == 1, "소화 완료", 8);
        check(digested == 1, "중복 시작 호출에도 소화 완료 이벤트 한 번");
        check(churn.acidVfx.All(p => !p.isEmitting), "소화 완료 시 위산 방출 정지");
        check(churn.walls.Select((w, i) => Mathf.Approximately(w.amplitude, amplitudes[i])).All(v => v), "소화 완료 후 위벽 수축 복구");
        yield return Until(() => churn.acidVfx.All(p => p.particleCount == 0), "남은 위산 입자 소멸", 4);
        check(true, "남은 위산 입자가 자연스럽게 소멸");

        // 완료된 음식을 검사에서 원상 복원하여 중간 취소와 다시 시작을 확인한다.
        for (int i = 0; i < churn.foodBoluses.Length; i++)
        {
            churn.foodBoluses[i].transform.localScale = foodScales[i];
            churn.foodBoluses[i].SetActive(true);
        }
        foreach (var c in churn.chymeBlobs) c.SetActive(false);
        churn.digestDuration = 4;
        churn.Begin();
        yield return Until(() => churn.acidVfx.Any(p => p.particleCount > 0), "두 번째 위산 방출");
        churn.enabled = false;
        check(churn.acidVfx.All(p => !p.isPlaying && p.particleCount == 0), "중단 시 위산 입자 즉시 정리");
        check(churn.churnAudio == null || !churn.churnAudio.isPlaying, "중단 시 소화 소리 정지");
        check(churn.walls.Select((w, i) => Mathf.Approximately(w.amplitude, amplitudes[i])).All(v => v), "중단 시 위벽 수축 복구");
        check(churn.foodBoluses.Select((f, i) => f.activeSelf && Vector3.Distance(f.transform.localScale, foodScales[i]) < .0001f).All(v => v),
            "중단 시 음식 크기와 표시 상태 복구");
        check(churn.chymeBlobs.All(c => !c.activeSelf), "중단 시 미즙 표시 상태 복구");
        yield return Pause(.2f);
        check(digested == 1, "중단된 소화는 완료 이벤트를 발생시키지 않음");
        churn.enabled = true; churn.digestDuration = .2f; churn.Begin();
        yield return Until(() => digested == 2, "중단 후 다시 시작");
        check(true, "중단 후 소화를 다시 시작하여 정상 완료");
        stomach.SetActive(false);

        small.SetActive(true);
        SceneManager.SetActiveScene(small.scene);
        yield return Pause(.1f);
        var absorption = small.GetComponentInChildren<NutrientAbsorption>();
        var burst = absorption.absorbVfx;
        check(burst != null && burst.main.simulationSpace == ParticleSystemSimulationSpace.World, "영양소 효과의 공간 기준 입자 연결");
        check(burst.particleCount == 0 && !burst.main.playOnAwake, "흡수 전 빛 입자 자동 재생 없음");
        var nutrients = Object.FindObjectsByType<Nutrient>(FindObjectsSortMode.None);
        check(nutrients.Length == 18, "실제 장면의 영양소 열여덟 개 생성");
        absorption.absorbDuration = .12f;
        absorption.advanceOnComplete = false;
        absorption.completeLines = new NarrationLine[0];
        int completed = 0;
        absorption.onComplete.AddListener(() => completed++);
        var endpoint = new GameObject("검증용 흡수 도착점").transform;
        Vector3 first = absorption.absorbTarget.position;
        endpoint.position = first;
        absorption.absorbTarget = endpoint;
        var n0 = nutrients[0];
        absorption.Absorb(n0); absorption.Absorb(n0);
        yield return Until(() => n0 == null, "첫 영양소 흡수");
        check(burst.particleCount == 20, "같은 영양소를 중복 선택해도 빛 입자는 한 번 발생");
        Vector3 second = first + Vector3.forward * 2;
        endpoint.position = second;
        var n1 = nutrients[1];
        absorption.Absorb(n1);
        yield return Until(() => n1 == null, "연속 영양소 흡수");
        var particles = Particles(burst);
        check(particles.Length >= 40, "연속 흡수에서도 새 빛 입자가 추가됨");
        check(particles.Any(p => Vector3.Distance(p.position, first) < .6f) && particles.Any(p => Vector3.Distance(p.position, second) < .6f),
            "앞선 흡수 입자가 새 흡수 위치로 이동하지 않음");
        var center = (first + second) * .5f;
        yield return Pause(.1f);
        Capture("영양소_흡수", center + new Vector3(2.5f, 1, -2.5f), center, new[] { burst });
        foreach (var nutrient in nutrients) if (nutrient != null) absorption.Absorb(nutrient);
        yield return Until(() => completed == 1, "전체 흡수 완료", 5);
        check(completed == 1 && Object.FindObjectsByType<Nutrient>(FindObjectsSortMode.None).Length == 0, "모든 영양소 흡수와 완료 이벤트 유지");
        yield return Until(() => burst.particleCount == 0, "흡수 입자 소멸", 3);
        check(true, "영양소 흡수 후 입자 잔류 없음");
        small.SetActive(false);
        Object.Destroy(endpoint.gameObject);

        var water = large.GetComponentInChildren<WallAbsorption>(true);
        water.count = 3;
        water.driftSpeed = new Vector2(9, 9);
        water.tubeLength = 2;
        large.SetActive(true);
        SceneManager.SetActiveScene(large.scene);
        yield return Until(() => water.absorbVfx.particleCount >= 2, "장벽 수분 흡수 파문", 3);
        var ripple = water.absorbVfx;
        var rings = Particles(ripple);
        check(ripple.main.simulationSpace == ParticleSystemSimulationSpace.World, "수분 파문 공간 기준 연결");
        check(rings.Length >= 2, "서로 다른 물방울의 파문 동시 생성");
        check(rings.All(p =>
        {
            var local = water.transform.InverseTransformPoint(p.position);
            return Mathf.Abs(new Vector2(local.x, local.y).magnitude - (water.tubeRadius - .025f)) < .01f;
        }), "수분 파문이 장벽 흡수 경계에 위치");
        check(rings.All(p =>
        {
            var local = water.transform.InverseTransformPoint(p.position);
            var inward = water.transform.TransformDirection(new Vector3(-local.x, -local.y, 0)).normalized;
            return Vector3.Dot(Quaternion.Euler(p.rotation3D) * Vector3.forward, inward) > .99f;
        }), "수분 파문이 장벽 면의 방향과 일치");
        var ring = rings.OrderByDescending(p => p.remainingLifetime).First();
        var normal = Quaternion.Euler(ring.rotation3D) * Vector3.forward;
        yield return Pause(.18f);
        Capture("수분_흡수_파문", ring.position + normal * 1.4f, ring.position, new[] { ripple });
        water.enabled = false;
        yield return Until(() => ripple.particleCount == 0, "수분 파문 소멸", 3);
        check(true, "수분 흡수 정지 후 파문이 자연스럽게 소멸");
        large.SetActive(false);
        var empty = SceneManager.CreateScene("효과검증종료");
        SceneManager.SetActiveScene(empty);
        foreach (var group in new[] { stomach, small, large })
        {
            var operation = SceneManager.UnloadSceneAsync(group.scene);
            yield return Until(() => operation.isDone, "효과 장면 해제");
        }
        check(burst == null && ripple == null && churn == null, "장면을 나가면 효과 개체가 남지 않음");
    }

    static void Capture(string name, Vector3 position, Vector3 target, ParticleSystem[] systems)
    {
        Directory.CreateDirectory("Tools/effect_captures");
        var go = new GameObject("효과 촬영 카메라");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        cam.fieldOfView = 55; cam.nearClipPlane = .02f; cam.farClipPlane = 100;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.026f, .04f, .065f, 1);
        cam.cullingMask = 1 << 31;
        var layers = systems.Select(p => p.gameObject.layer).ToArray();
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            for (int i = 0; i < systems.Length; i++) systems[i].gameObject.layer = 31;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes("Tools/effect_captures/" + name + ".png", tex.EncodeToPNG());
            check(systems.All(p => !ShaderUtil.ShaderHasError(p.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader)),
                name + " 실행 중 셰이더 오류 없음");
        }
        finally
        {
            for (int i = 0; i < systems.Length; i++) systems[i].gameObject.layer = layers[i];
            RenderTexture.active = old; cam.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
    }
}
#endif


