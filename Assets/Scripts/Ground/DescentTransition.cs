using UnityEngine;
using UnityEngine.InputSystem; // [필수] New Input System 사용
using System.Collections;

[AddComponentMenu("인체탐험/하강 연출") ]
public class DescentController : MonoBehaviour
{
    [Header("1. 연결할 오브젝트들")]
    public GameObject floorPlane;       // 사라질 바닥 (Plane)
    public GameObject intestineBackground; // 위로 움직일 배경 (Sphere)

    [Header("2. 연출 설정")]
    public float fadeDuration = 2.0f;   // 바닥이 투명해지는 시간
    public float fallSpeed = 5.0f;      // 떨어지는 속도
    public float fallDistance = 20.0f;  // 떨어지는 거리

    [Header("3. 사운드 설정")]
    public AudioSource audioSource;     // 효과음을 낼 Audio Source 컴포넌트

    private bool isFalling = false;     // 중복 실행 방지
    private InputAction inputAction;    // 스페이스바 입력을 위한 변수

    void Awake()
    {
        // 스페이스바 입력 설정 (New Input System)
        inputAction = new InputAction(binding: "<Keyboard>/space");
        inputAction.performed += ctx => StartDescent(); // 키 누르면 함수 실행
        inputAction.Enable();
    }

    void OnDestroy()
    {
        // 종료 시 입력 감지 해제 (메모리 누수 방지)
        if (inputAction != null)
        {
            inputAction.Disable();
            inputAction.Dispose();
        }
    }

    // 외부에서 호출하거나 키 입력 시 실행되는 함수
    public void StartDescent()
    {
        if (isFalling) return;

        Debug.Log("하강 시퀀스 시작! (사운드 재생 포함)");
        isFalling = true;
        StartCoroutine(ProcessDescent());
    }

    IEnumerator ProcessDescent()
    {
        // 1. 사운드 재생 (오디오 소스가 연결되어 있다면)
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }
        else if (audioSource == null)
        {
            Debug.LogWarning("주의: AudioSource가 연결되지 않았습니다!");
        }

        // 2. 바닥 투명하게 만들기 (URP 호환)
        Renderer rend = null;
        if (floorPlane != null) rend = floorPlane.GetComponent<Renderer>();

        if (rend != null)
        {
            Material mat = rend.material;
            // URP 쉐이더 색상 프로퍼티 이름 찾기 (_BaseColor 혹은 _Color)
            string colorProperty = mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            Color startColor = mat.GetColor(colorProperty);

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(1.0f, 0.0f, timer / fadeDuration);

                Color newColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
                mat.SetColor(colorProperty, newColor);
                yield return null;
            }
        }

        // 바닥 완전히 끄기
        if (floorPlane != null) floorPlane.SetActive(false);

        // 3. 배경을 위로 올려서 떨어지는 느낌 주기
        if (intestineBackground != null)
        {
            Vector3 startPos = intestineBackground.transform.position;
            Vector3 endPos = startPos + (Vector3.up * fallDistance);

            float moveTimer = 0f;
            float duration = fallDistance / fallSpeed;

            while (moveTimer < duration)
            {
                moveTimer += Time.deltaTime;
                intestineBackground.transform.position = Vector3.Lerp(startPos, endPos, moveTimer / duration);
                yield return null;
            }
        }

        // 4. 사운드 서서히 끄기 (뚝 끊기지 않게)
        if (audioSource != null && audioSource.isPlaying)
        {
            float startVolume = audioSource.volume;
            float audioFadeTime = 1.0f;
            float audioTimer = 0f;

            while (audioTimer < audioFadeTime)
            {
                audioTimer += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, audioTimer / audioFadeTime);
                yield return null;
            }
            audioSource.Stop();
            audioSource.volume = startVolume; // 다음을 위해 볼륨 원상복구
        }

        Debug.Log("하강 완료! (대장 도착)");
        isFalling = false; // (필요시 재사용을 위해)
    }
}