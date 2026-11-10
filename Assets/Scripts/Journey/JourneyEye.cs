using UnityEngine;

/// <summary>
/// 탐험가 눈(헤드셋 가운데 카메라). 영속 리그는 CenterEyeAnchor 에 'VRCamera' 태그를 달아서
/// Camera.main(MainCamera 태그)이 비어 있다 → 눈이 필요한 곳(전등·안내판·사람 시선·조준 등)은 모두 여기서 찾는다.
/// </summary>
public static class JourneyEye
{
    static Transform cached;
    static float nextSearch;

    /// <summary>눈 위치(없으면 null). 장면이 바뀌어 사라지면 다시 찾는다(찾기는 0.5초에 한 번까지).</summary>
    public static Transform Get()
    {
        if (cached != null && cached.gameObject.activeInHierarchy) return cached;
        if (Time.unscaledTime < nextSearch) return null;
        nextSearch = Time.unscaledTime + .5f;
        var tagged = GameObject.FindWithTag("VRCamera");
        cached = tagged != null ? tagged.transform : UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
        if (cached != null) nextSearch = 0;
        return cached;
    }

    /// <summary>눈에 붙은 카메라 (조준 기준 등).</summary>
    public static Camera EyeCamera()
    {
        var t = Get();
        return t != null ? t.GetComponent<Camera>() : null;
    }
}
