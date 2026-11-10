using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 기기(Quest)에서만 적용되는 성능 설정: 72Hz, 고정 주시점 렌더링(FFR), 블룸 off, 그림자 off.
/// PC(에디터/Link)에서는 그대로 두어 화질을 유지한다. 영속 프리팹의 플레이어에 붙인다.
/// </summary>
[AddComponentMenu("인체탐험/가상현실 기기 최적화") ]
public class QuestSettings : MonoBehaviour
{
    public bool disableBloomOnDevice = true;
    public bool disableShadowsOnDevice = true;
    public OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;

    bool onDevice;

    void Awake()
    {
        onDevice = Application.platform == RuntimePlatform.Android;
        if (!onDevice) return;
        Application.targetFrameRate = 72;
        OVRManager.foveatedRenderingLevel = foveation;
        OVRManager.useDynamicFoveatedRendering = true;
        OVRPlugin.systemDisplayFrequency = 72f;
        // 프레임 유지: 질감은 한 단계 작은 해상도로(메모리·대역폭 절반 이하), 점막 재질은 가벼운 판(잔결 한 겹, 흐르는 막·주변 반사 없음)
        QualitySettings.globalTextureMipmapLimit = 1;
        Shader.EnableKeyword("_MUCOSA_LITE");
        SceneManager.sceneLoaded += (_, __) => ApplySceneSettings();
        ApplySceneSettings();
    }

    void ApplySceneSettings()
    {
        if (!onDevice) return;
        if (disableShadowsOnDevice)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
        if (disableBloomOnDevice)
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal && v.profile != null && v.profile.TryGet(out Bloom bloom)) bloom.active = false; // 런타임 복사본만 수정
    }
}
