using UnityEngine;

public class ChickSpeechTrigger : MonoBehaviour
{
    [Header("설정")]
    public AudioSource chickAudioSource; // 병아리 목소리 나오는 곳
    public AudioClip ttsClip;            // "위장으로 가자!" 음성 파일

    private void OnTriggerEnter(Collider other)
    {
        // 정훈이가 말한 대로 태그를 "VRCamera"로 고정!
        if (other.CompareTag("VRCamera"))
        {
            Debug.Log(" VRCamera 감지됨! 병아리 멘트 시작!");

            if (chickAudioSource != null && ttsClip != null)
            {
                chickAudioSource.clip = ttsClip;
                chickAudioSource.Play();
            }
            else
            {
                Debug.LogWarning(" 오디오 소스나 클립이 비어있어!");
            }

            // 한 번 밟으면 재작동 안 하게 끄기
            gameObject.SetActive(false);
        }
    }
}