using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 멀미 완화 비네트(터널링). 플레이어(또는 탑승 캡슐)의 이동/회전 속도에 비례해 주변 시야를 어둡게 한다.
/// 씬의 Global Volume 에 Vignette 오버라이드가 있어야 하며, 에셋을 건드리지 않도록 런타임 인스턴스 프로파일을 쓴다.
/// </summary>
[AddComponentMenu("인체탐험/멀미 완화 화면") ]
public class ComfortVignette : MonoBehaviour
{
    [Tooltip("정지 시 비네트 강도")]
    [Range(0f, 1f)] public float restIntensity = 0.2f;
    [Tooltip("최대 속도에서의 강도")]
    [Range(0f, 1f)] public float movingIntensity = 0.55f;
    [Tooltip("이 속도(미터/초)에서 최대 강도")]
    public float fullSpeed = 3f;
    [Tooltip("이 각속도(도/초)에서 최대 강도")]
    public float fullTurn = 120f;
    public float smooth = 6f;
    [Tooltip("이동/회전을 측정할 대상 (플레이어 루트). 머리 회전은 자연스러운 것이므로 카메라가 아니라 몸을 본다")]
    public Transform motionSource;
    [Tooltip("외부에서 강제로 켤 때 (삼킴 연출 등)")]
    [HideInInspector] public float forced;

    Vignette vignette;
    Volume volume;
    Vector3 lastPos; float lastYaw; float current;

    Transform Src => motionSource != null ? motionSource : transform;
    void Start() { lastPos = Src.position; lastYaw = Src.eulerAngles.y; }

    void LateUpdate()
    {
        if (volume == null) vignette = null; // 씬이 바뀌면 다시 찾는다
        if (vignette == null && !FindVignette()) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = (Src.position - lastPos).magnitude / dt;
        float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, Src.eulerAngles.y)) / dt;
        lastPos = Src.position; lastYaw = Src.eulerAngles.y;

        float k = Mathf.Max(Mathf.Clamp01(speed / fullSpeed), Mathf.Clamp01(turn / fullTurn), forced);
        float target = Mathf.Lerp(restIntensity, movingIntensity, k);
        current = Mathf.Lerp(current, target, dt * smooth);
        vignette.intensity.value = current;
    }

    bool FindVignette()
    {
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!v.isGlobal || v.sharedProfile == null) continue;
            if (v.profile.TryGet(out Vignette vg)) { volume = v; vignette = vg; vignette.active = true; return true; } // v.profile = 런타임 복사본
        }
        return false;
    }

    void OnDestroy() { if (volume != null && volume.profile != null) Destroy(volume.profile); }
}
