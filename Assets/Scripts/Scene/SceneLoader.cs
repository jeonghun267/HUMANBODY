using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환에 사용

[AddComponentMenu("인체탐험/장면 불러오기") ]
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [Tooltip("씬 전환 지연 시간 (초)")]
    [SerializeField]
    private float loadDelayTime = 1.5f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 지정한 씬으로 전환합니다.
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름 (Build Settings에 등록된 이름)</param>
    public void LoadNewScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private System.Collections.IEnumerator LoadSceneAsync(string sceneName)
    {
        Debug.Log($"씬 전환: {sceneName}");

        yield return new WaitForSeconds(loadDelayTime);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log($"씬 전환 완료: {sceneName}");
    }
}