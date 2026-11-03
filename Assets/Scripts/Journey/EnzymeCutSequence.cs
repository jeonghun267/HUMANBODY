using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 효소 절단 연출: 펩신(효소 캐릭터)이 단백질 사슬로 다가가 '싹둑' 자르면 사슬이 사라지고 아미노산 조각들이 흩어진다.
/// 위 단계에서 StomachChurn 과 함께 실행. Begin() 으로 시작, 반복 가능.
/// </summary>
[AddComponentMenu("인체탐험/효소 절단 연출") ]
public class EnzymeCutSequence : MonoBehaviour
{
    [Header("파츠")]
    public Transform enzyme;
    public Transform[] proteinChains;
    public GameObject fragmentPrefab;
    [Min(1)] public int fragmentsPerChain = 5;

    [Header("타이밍")]
    public float approachSeconds = 1.2f;
    public float cutSeconds = 0.4f;
    public float betweenChains = 0.8f;
    public float fragmentSpread = 0.8f;
    public float fragmentLife = 6f;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip cutClip;
    [Header("절단 순간의 작은 조각") ]
    public ParticleSystem cutVfx;

    public UnityEvent onAllCut = new UnityEvent();
    bool running;
    JourneyCharacterMotion motion;
    readonly System.Collections.Generic.List<GameObject> fragments = new System.Collections.Generic.List<GameObject>();

    public void Begin()
    {
        if (!running && isActiveAndEnabled) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        motion = enzyme != null ? enzyme.GetComponentInChildren<JourneyCharacterMotion>() : null;
        proteinChains ??= new Transform[0];
        foreach (var chain in proteinChains)
        {
            if (chain == null || !chain.gameObject.activeSelf) continue;

            // 접근
            if (enzyme != null)
            {
                Vector3 from = enzyme.position;
                Vector3 to = chain.position + Vector3.up * 0.3f;
                float t = 0f;
                while (t < approachSeconds)
                {
                    t += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, t / approachSeconds);
                    enzyme.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
                    Vector3 look = to - enzyme.position; look.y = 0;
                    if (look != Vector3.zero) enzyme.rotation = Quaternion.Slerp(enzyme.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);
                    yield return null;
                }
                // 싹둑 (살짝 눌렸다 튕김)
                Vector3 s = enzyme.localScale;
                t = 0f;
                while (t < cutSeconds)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Sin(Mathf.Clamp01(t / Mathf.Max(.01f, cutSeconds)) * Mathf.PI);
                    if (motion != null) motion.SetCut(k);
                    yield return null;
                }
                enzyme.localScale = s;
                if (motion != null) motion.SetCut(0);
            }

            if (audioSource != null && cutClip != null) audioSource.PlayOneShot(cutClip);
            else Sfx.Play("snip", chain.position, 0.9f);
            Vector3 center = chain.position;
            if (cutVfx != null) JourneyParticleBurst.Emit(cutVfx, center, 18);
            chain.gameObject.SetActive(false);
            for (int i = 0; i < fragmentsPerChain && fragmentPrefab != null; i++)
            {
                var f = Instantiate(fragmentPrefab, center, Random.rotation, transform);
                fragments.Add(f);
                StartCoroutine(Scatter(f.transform, center + Random.insideUnitSphere * fragmentSpread));
            }
            yield return new WaitForSeconds(betweenChains);
        }
        running = false;
        onAllCut?.Invoke();
    }

    void OnDisable()
    {
        StopAllCoroutines(); running = false;
        if (motion != null) motion.SetCut(0);
        if (cutVfx != null) cutVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var f in fragments) if (f != null) Destroy(f);
        fragments.Clear();
    }

    IEnumerator Scatter(Transform f, Vector3 target)
    {
        Vector3 from = f.position; float t = 0f;
        while (t < 0.6f && f != null)
        {
            t += Time.deltaTime;
            f.position = Vector3.Lerp(from, target, 1f - Mathf.Pow(1f - t / 0.6f, 3f));
            yield return null;
        }
        float life = 0f;
        while (life < fragmentLife && f != null)
        {
            life += Time.deltaTime;
            f.position = target + new Vector3(Mathf.Sin(Time.time * 1.3f + t), Mathf.Cos(Time.time * 0.9f), Mathf.Sin(Time.time * 1.1f)) * 0.08f;
            f.Rotate(Vector3.up, 40f * Time.deltaTime);
            yield return null;
        }
        if (f != null) { fragments.Remove(f.gameObject); Destroy(f.gameObject); }
    }
}
