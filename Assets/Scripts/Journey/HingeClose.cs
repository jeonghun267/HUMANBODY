using System.Collections;
using UnityEngine;

/// <summary>
/// 경첩으로 닫히는 판(후두덮개): 장면에 들어오고 delay 초 뒤, 열린 각도에서 닫힌 각도로 천천히 닫히며 소리와 침방울을 낸다.
/// 음식이 기도로 들어가지 않게 덮는 모습을 보여 준다.
/// </summary>
[AddComponentMenu("인체탐험/경첩 닫힘")]
public class HingeClose : MonoBehaviour
{
    public Quaternion open = Quaternion.identity, closed = Quaternion.identity;
    [Tooltip("장면 전환이 끝나고 닫히기 시작할 때까지(초)")] public float delay = 1.5f;
    [Tooltip("닫히는 데 걸리는 시간(초)")] public float seconds = 1.4f;
    public string sound = "gulp";
    public ParticleSystem splash;

    IEnumerator Start()
    {
        transform.rotation = open;
        while (JourneyManager.Instance != null && JourneyManager.Instance.IsTransitioning) yield return null;
        yield return new WaitForSeconds(delay);
        if (!string.IsNullOrEmpty(sound)) Sfx.Play(sound, transform.position, .6f, .8f);
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(open, closed, Mathf.SmoothStep(0, 1, t / seconds));
            yield return null;
        }
        transform.rotation = closed;
        if (splash != null) splash.Play();
    }
}
