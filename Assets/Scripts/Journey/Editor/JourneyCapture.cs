#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 여정 씬을 열어 플레이어 시점(SpawnPoint + 눈높이) 및 몇 개 각도에서 렌더링해 PNG 로 저장한다.
/// 배치모드에서도 동작: Unity.exe -batchmode -executeMethod JourneyCapture.CaptureAll
/// 결과: Tools/captures/{scene}_{view}.png
/// </summary>
public static class JourneyCapture
{
    const string SceneDir = "Assets/Scenes/Journey";
    const string OutDir = "Tools/captures";

    [MenuItem("인체탐험/장면 촬영 (검수용 이미지)", priority = 30)]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutDir);
        foreach (var path in Directory.GetFiles(SceneDir, "*.unity"))
        {
            var scene = EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single);
            string name = Path.GetFileNameWithoutExtension(path);
            var spawn = GameObject.Find("SpawnPoint");
            Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;

            // 플레이어 시점 4방향 + 위에서 내려다본 전경
            Shoot(name, "front", eye, Quaternion.Euler(5, 0, 0));
            Shoot(name, "left", eye, Quaternion.Euler(5, -70, 0));
            Shoot(name, "right", eye, Quaternion.Euler(5, 70, 0));
            Shoot(name, "back", eye, Quaternion.Euler(5, 180, 0));
            Shoot(name, "up", eye, Quaternion.Euler(-60, 0, 0));
            Shoot(name, "overview", eye + new Vector3(-14, 12, -14), Quaternion.LookRotation((eye + Vector3.forward * 6) - (eye + new Vector3(-14, 12, -14))), 60f);
        }
        Debug.Log($"[JourneyCapture] 캡처 완료 → {OutDir}");
    }

    static void Shoot(string scene, string view, Vector3 pos, Quaternion rot, float fov = 90f)
    {
        var go = new GameObject("_CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        foreach (var lamp in Object.FindObjectsByType<BodyHeadlamp>(FindObjectsSortMode.None)) lamp.PlaceAt(cam.transform);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes($"{OutDir}/{scene}_{view}.png", tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
    }
}
#endif
