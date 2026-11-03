using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("인체탐험/장면 전환") ]
public class SceneChanger : MonoBehaviour
{
    [Header("1. 이동 설정")]
    public string nextSceneName;
    public List<string> targetTags = new List<string> { "Player", "VRCamera", "Boat" };

    [Header("2. 페이드 설정")]
    public bool useFade = true;
    public float fadeDuration = 1.0f; // 뚝 끊기는 게 싫으면 최소 0.5~1초는 줘야 함
    public Color fadeColor = Color.black; // 여기서 혈관 색상(빨강 등)으로 변경 가능!
    public Image uiFadePanel;

    private bool isTriggered = false;

    private void Start()
    {
        if (uiFadePanel != null)
        {
            uiFadePanel.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
            uiFadePanel.raycastTarget = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        if (targetTags.Contains(other.tag))
        {
            isTriggered = true;
            StartCoroutine(TransitionRoutine());
        }
    }

    private IEnumerator TransitionRoutine()
    {
        if (useFade)
        {
            // OVR 페이드 (VR 렌즈)
            var ovrFade = FindAnyObjectByType<OVRScreenFade>();
            if (ovrFade != null)
            {
                ovrFade.fadeColor = fadeColor; // OVR 페이드 색상도 맞춰줌
                ovrFade.fadeTime = fadeDuration;
                ovrFade.FadeOut();
            }

            // UI 패널 페이드 (네가 넣은 PANEL)
            if (uiFadePanel != null)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    float alpha = Mathf.Clamp01(timer / fadeDuration);
                    // 검은색(0,0,0) 고정이 아니라 설정한 fadeColor로 변하게 수정!
                    uiFadePanel.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
                    yield return null;
                }
            }
            else yield return new WaitForSeconds(fadeDuration);
        }

        // 비동기 로딩 시작
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);

        // 로딩 중에 화면이 멈추는 걸 방지하기 위해 끝까지 대기
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}