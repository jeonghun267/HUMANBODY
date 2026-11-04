#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// 멀미 완화: 시야 가장자리 테두리 재질(빌드에 셰이더가 들어가도록 Resources 에 둔다)과
/// 잠수정 움직임 점검 보고서(Tools/realism_comfort.txt — 속도·가속·회전·내려가는 속도를 편안한 기준과 견준다).
/// </summary>
public static partial class JourneyRealism
{
    // 편안한 기준(가상현실 멀미 권장 어림): 일정 속도 2m/s, 빨라지고 느려질 때 1m/s², 회전 30°/s, 수직 이동 1m/s
    const float ComfortSpeed = 2f, ComfortAccel = 1f, ComfortYaw = 30f, ComfortVertical = 1f;

    static void ComfortAssets()
    {
        const string path = "Assets/Resources/멀미완화_테두리.mat";
        var shader = Shader.Find("인체탐험/멀미 완화 테두리");
        if (shader == null) { Debug.LogError("[멀미] 테두리 셰이더 없음"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader; mat.SetColor("_Color", new Color(0, 0, 0, 0));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>잠수정 장면마다 실제 속도(m/s)·가속·회전 한도·수직 하강 속도를 적는다 (배치용).</summary>
    public static void ComfortReport()
    {
        ComfortAssets();
        var report = new StringBuilder("장면\t경로 길이(m)\t속도(m/s)\t가속(m/s²)\t회전 한도(°/s)\t수직(m/s)\t똑바로 서기\t판정\n");
        foreach (var name in BodyScenes)
        {
            EditorSceneManager.OpenScene($"{SceneDir}/{name}.unity", OpenSceneMode.Single);
            var ride = Object.FindFirstObjectByType<CapsuleRide>();
            if (ride == null || ride.cart == null || ride.cart.Spline == null) continue;
            var spline = ride.cart.Spline;
            float length = spline.CalculateLength();
            float speed = ride.speed * length, accel = speed / Mathf.Max(.05f, ride.easeSeconds);
            float vertical = 0;
            for (int i = 0; i < 200; i++)
            {
                Vector3 a = spline.EvaluatePosition(i / 200f), b = spline.EvaluatePosition((i + 1) / 200f);
                float seg = Vector3.Distance(a, b);
                if (seg > 1e-4f) vertical = Mathf.Max(vertical, Mathf.Abs(b.y - a.y) / seg * speed);
            }
            bool ok = speed <= ComfortSpeed && accel <= ComfortAccel && ride.maxYawSpeed <= ComfortYaw && vertical <= ComfortVertical && ride.keepUpright;
            report.Append($"{name}\t{length:0.0}\t{speed:0.00}\t{accel:0.00}\t{ride.maxYawSpeed:0}\t{vertical:0.00}\t{(ride.keepUpright ? "예" : "아니오")}\t{(ok ? "편안" : "점검")}\n");
        }
        report.Append($"\n기준: 속도 ≤ {ComfortSpeed}m/s, 가속 ≤ {ComfortAccel}m/s², 회전 ≤ {ComfortYaw}°/s, 수직 ≤ {ComfortVertical}m/s, 잠수정이 기울지 않음.\n");
        File.WriteAllText("Tools/realism_comfort.txt", report.ToString(), new UTF8Encoding(true));
        AssetDatabase.SaveAssets();
        Debug.Log("[멀미] 잠수정 움직임 보고서 → Tools/realism_comfort.txt");
    }
}
#endif
