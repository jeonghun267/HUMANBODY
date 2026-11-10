using UnityEngine;

/// <summary>씬 배경음(합성): 위 꾸르륵, 혈류 맥박 등. 씬에 하나 두면 시작 시 루프 재생.</summary>
[AddComponentMenu("인체탐험/환경 소리") ]
public class SceneAmbience : MonoBehaviour
{
    [Tooltip("환경음 종류를 선택합니다. 소화음 또는 맥박음")]
    public string clipName = "gurgle";
    [Range(0f, 1f)] public float volume = 0.35f;
    public bool spatial;
    public float pitch = 1f;

    void Start()
    {
        var src = Sfx.Loop(clipName, transform, volume, spatial);
        src.pitch = pitch;
    }
}
