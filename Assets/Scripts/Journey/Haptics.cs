using System.Collections;
using UnityEngine;

/// <summary>컨트롤러 진동 헬퍼. 삼킴·연동파 통과·영양소 흡수 등 "몸으로 느끼는" 순간에 사용.</summary>
[AddComponentMenu("인체탐험/진동 효과") ]
public class Haptics : MonoBehaviour
{
    static Haptics instance;
    static Haptics Instance
    {
        get
        {
            if (instance == null) { var go = new GameObject("Haptics"); DontDestroyOnLoad(go); instance = go.AddComponent<Haptics>(); }
            return instance;
        }
    }

    /// <summary>양손 진동. strength 0~1, seconds 길이.</summary>
    public static void Pulse(float strength = 0.6f, float seconds = 0.15f, bool left = true, bool right = true)
    {
        Instance.StartCoroutine(Instance.Run(strength, seconds, left, right));
    }

    IEnumerator Run(float strength, float seconds, bool left, bool right)
    {
        if (left) OVRInput.SetControllerVibration(0.6f, strength, OVRInput.Controller.LTouch);
        if (right) OVRInput.SetControllerVibration(0.6f, strength, OVRInput.Controller.RTouch);
        yield return new WaitForSeconds(seconds);
        if (left) OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        if (right) OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}
