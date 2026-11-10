using UnityEngine;
using System.Collections;

[AddComponentMenu("인체탐험/박사 설명 제어") ]
public class DoctorController : MonoBehaviour
{
    private AudioSource audioSource;
    // playerHead 변수 삭제 (이제 필요 없음)

    void Start()
    {
        // 오디오 소스 가져오기 (말은 해야 하니까!)
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
        }
    }

    // Update / LateUpdate 함수 삭제! 
    // (이제 병아리는 스크립트로 절대 회전하지 않음)

    // ★ 설명하는 기능은 유지! ★
    public void PlayExplanation(string organName)
    {
        string audioFileName = "Explain_" + organName;
        AudioClip clip = Resources.Load<AudioClip>(audioFileName);

        if (clip != null)
        {
            if (audioSource.isPlaying) audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log("[Doctor] 삐약! 회전 없이 설명만 합니다: " + organName);
        }
        else
        {
            Debug.LogWarning("[Doctor] 파일 없음: " + audioFileName);
        }
    }
}