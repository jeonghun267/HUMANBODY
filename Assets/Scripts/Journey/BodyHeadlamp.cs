using UnityEngine;

/// <summary>
/// 몸속 장면의 탐험가 전등. 몸 안에는 햇빛이 없으므로 내시경처럼 눈 위치에서 빛을 비춘다.
/// 가까운 점막은 젖은 반사로 빛나고 멀수록 어두워져 깊이가 느껴진다.
/// 전등은 장면에 들어 있어 장면을 떠나면 함께 사라진다(영속 플레이어에 붙이지 않는다).
/// </summary>
public class BodyHeadlamp : MonoBehaviour
{
    [Tooltip("눈 주변을 고르게 비추는 전등")] public Light lamp;
    [Tooltip("바라보는 방향 멀리까지 비추는 전등 (없어도 됨)")] public Light beam;
    [Tooltip("눈 기준 전등 위치")] public Vector3 offset = new Vector3(0f, 0.12f, 0.05f);

    Transform eye;

    void LateUpdate()
    {
        if (eye == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            eye = cam.transform;
        }
        PlaceAt(eye);
    }

    static readonly int LampId = Shader.PropertyToID("_JourneyLamp");

    /// <summary>주어진 눈(카메라) 위치로 전등을 옮긴다. 편집기 촬영에서도 사용한다.</summary>
    public void PlaceAt(Transform target)
    {
        var pos = target.TransformPoint(offset);
        if (lamp != null) lamp.transform.SetPositionAndRotation(pos, target.rotation);
        if (beam != null) beam.transform.SetPositionAndRotation(pos, target.rotation);
        // 분비물 입자(점액 방울)는 조명 계산 없이 이 값으로 밝기를 정한다.
        Shader.SetGlobalVector(LampId, new Vector4(pos.x, pos.y, pos.z, lamp != null ? lamp.intensity : 0f));
    }

    void OnDisable() => Shader.SetGlobalVector(LampId, Vector4.zero);
}
