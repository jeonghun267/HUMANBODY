using UnityEngine;

/// <summary>
/// 씬마다 하나씩 두는 부트스트랩. 영속 프리팹(JourneyManager + 플레이어 + 가이드)이 아직 없으면 생성한다.
/// 덕분에 에디터에서 중간 씬(예: 03_Stomach)부터 바로 재생해도 정상 동작한다.
/// </summary>
[AddComponentMenu("인체탐험/공통 개체 불러오기") ]
public class PersistentBootstrap : MonoBehaviour
{
    public GameObject persistentPrefab;
    static GameObject instance;

    void Awake()
    {
        if (instance != null || persistentPrefab == null) return;
        instance = Instantiate(persistentPrefab);
        instance.name = persistentPrefab.name;
        DontDestroyOnLoad(instance);

        // 이 씬의 SpawnPoint 로 플레이어 위치 맞춤 (첫 생성 시에는 sceneLoaded 콜백이 이미 지나갔으므로 직접 처리)
        var spawn = GameObject.Find("SpawnPoint");
        var player = GameObject.FindWithTag("Player");
        if (spawn != null && player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            if (cc != null) cc.enabled = true;
        }
    }
}
