using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("인체탐험/가상현실 플레이어 관리") ]
public class VRPlayerManager : MonoBehaviour
{
    // 싱글톤: 플레이어는 게임 전체에 단 하나만 존재해야 함
    public static VRPlayerManager instance;

    void Awake()
    {
        // 1. 중복 방지 로직
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바껴도 파괴되지 않음
        }
        else
        {
            // 만약 이미 넘어온 플레이어가 있는데, 씬에 또 다른 플레이어가 있다면?
            // (보통 비활성화하라고 했지만, 만약 켜져 있어도 충돌 안 나게 파괴함)
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        // 씬 로딩 완료 이벤트를 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬 로딩이 끝나면 자동으로 실행되는 함수
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 2. 새 씬에서 'SpawnPoint'라는 이름의 오브젝트 찾기
        GameObject spawnPoint = GameObject.Find("SpawnPoint");

        if (spawnPoint != null)
        {
            // OVRPlayerController 이동 시 물리 충돌 오류 방지를 위해 잠시 CC 끔
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 위치와 회전을 SpawnPoint로 강제 이동
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;

            Debug.Log($"[VRPlayerManager] '{scene.name}' 씬의 SpawnPoint로 이동했습니다.");

            // 이동 후 다시 켬
            if (cc != null) cc.enabled = true;
        }
        else
        {
            // SpawnPoint가 없으면 그냥 그 자리에 있음 (로그만 남김)
            Debug.Log($"[VRPlayerManager] '{scene.name}' 씬에 'SpawnPoint'가 없습니다. 현재 위치 유지.");
        }
    }
}