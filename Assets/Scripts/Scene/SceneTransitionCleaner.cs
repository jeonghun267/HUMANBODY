using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[AddComponentMenu("인체탐험/장면 전환 정리") ]
public class SceneTransitionCleaner : MonoBehaviour
{
    [Header("다음으로 넘어갈 씬 이름")]
    public string nextSceneName; // 인스펙터에서 Scene2, Scene3 등 직접 입력

    // 혹시 자동으로 못 찾을까 봐 불안하면 여기에 직접 넣어도 됩니다 (비워두면 자동 탐색)
    [Header("수동 설정 (비워두면 자동 감지)")]
    public GameObject specificNdiSender;

    public void GoToNextScene()
    {
        StartCoroutine(CleanAndLoad());
    }

    IEnumerator CleanAndLoad()
    {
        Debug.Log(" [Scene: {SceneManager.GetActiveScene().name}] NDI 정리 및 메모리 청소 시작...");

        // ---------------------------------------------------------
        // 1. NDI Sender 강제 종료 (수동 + 자동 감지)
        // ---------------------------------------------------------

        // A. 직접 넣어준 게 있다면 그것부터 끔
        if (specificNdiSender != null)
        {
            specificNdiSender.SetActive(false);
        }

        // B. 이름으로 찾아서 끄기 (NDI 에셋들은 보통 오브젝트 이름에 NDI나 Sender가 들어감)
        // Unity 6 최적화된 검색 방식
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            // 오브젝트 이름에 "NDI" 혹은 "Sender"가 포함되어 있고, 활성화된 상태라면
            if (obj.activeInHierarchy && (obj.name.Contains("NDI") || obj.name.Contains("Sender")))
            {
                // 혹시 모르니 자기 자신(스크립트가 붙은 버튼)은 끄면 안 됨
                if (obj != this.gameObject)
                {
                    obj.SetActive(false);
                }
            }
        }

        // ---------------------------------------------------------
        // 2. 그래픽 깨짐의 주범(GPU 텍스처) 초기화
        // ---------------------------------------------------------
        if (RenderTexture.active != null)
        {
            RenderTexture.active.Release();
            RenderTexture.active = null;
        }

        if (Camera.main != null)
        {
            Camera.main.targetTexture = null;
        }

        // 3. GPU가 정리할 시간 1프레임 대기
        yield return new WaitForEndOfFrame();

        // 4. 메모리(RAM) 대청소
        Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // 5. 안정성을 위해 0.1초 딜레이 후 이동
        yield return new WaitForSeconds(0.1f);

        Debug.Log($" {nextSceneName} 로드!");
        SceneManager.LoadScene(nextSceneName);
    }
}