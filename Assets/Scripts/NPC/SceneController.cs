using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("인체탐험/장면 이동") ]
public class SceneController : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneProcess(sceneName));
    }

    private IEnumerator LoadSceneProcess(string sceneName)
    {
        // 1. 씬 전환 효과 시작 (예: 페이드 아웃)
        // yield return StartCoroutine(FadeOut()); 

        // 2. 비동기 씬 로드 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 로드 완료 후 바로 넘어가지 않게 대기

        // 3. 로딩이 90% 될 때까지 대기 (씬 로드 중)
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // 4. 로드 완료, 씬 활성화
        op.allowSceneActivation = true;

        // 5. 씬이 완전히 바뀔 때까지 한 프레임 대기 (안전장치)
        yield return null;

        // 6. 전환 효과 마무리 (예: 페이드 인)
        // yield return StartCoroutine(FadeIn());

        // 7. ★ 여기서 삭제해야 함 (할 일 다 끝남)
        Destroy(gameObject);
    }
}