using System.Net;
using UnityEngine;
using System.Collections.Generic;
// using [OSC Library Namespace]; // 실제 사용하는 OSC 에셋의 namespace를 여기에 추가하세요 (예: using UnityOSC;)

[AddComponentMenu("인체탐험/원격 상호작용 통신") ]
public class OSCManager : MonoBehaviour
{
    // 싱글톤 패턴
    public static OSCManager Instance;

    [Header("원격 통신 설정")]
    [Tooltip("원격 통신 메시지를 보낼 원격 네트워크 주소")]
    [SerializeField]
    private string remoteIP = "127.0.0.1";

    [Tooltip("원격 통신 메시지를 보낼 포트 번호")]
    [SerializeField]
    private int sendPort = 8000;

    [Tooltip("원격 통신 메시지를 받을 포트 번호")]
    [SerializeField]
    private int receivePort = 9000;

    // 실제 라이브러리 객체 (사용하는 에셋에 맞춰 주석 해제)
    // private OscSender oscSender;
    // private OscReceiver oscReceiver;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeOSC();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeOSC()
    {
        // 1. 라이브러리 초기화 (사용하는 에셋에 맞춰 코드 추가)
        // oscSender = new OscSender(remoteIP, sendPort);
        // oscReceiver = new OscReceiver(receivePort);

        // 2. 수신 리스너 등록
        // oscReceiver.OnMessageReceived += OnOscMessageReceived;

        Debug.Log($"OSC Manager 초기화 완료. 송신:{remoteIP}:{sendPort}, 수신:{receivePort}");
    }

    void Update()
    {
        // 사용하는 라이브러리가 Update에서 폴링이 필요하다면 여기에 작성
        // if (oscReceiver.hasMessages) { ... }
    }

    /// <summary>
    /// 다른 사용자에게 상호작용 상태 전송
    /// </summary>
    public void SendInteractionStatus(string organID, bool isViewing)
    {
        string address = $"/interaction/{organID}";
        string status = isViewing ? "start" : "stop";

        // 메시지 전송 로직 (주석 해제 후 사용)
        // oscSender.Send(address, status);

        Debug.Log($"[OSC Send] 주소: {address}, 상태: {status}");
    }

    /// <summary>
    /// 수신된 OSC 메시지 처리
    /// </summary>
    private void OnOscMessageReceived(/* OscMessage message */)
    {
        // 1. 메시지 파싱 (라이브러리에 맞춰 수정)
        string address = ""; // message.address;
        string status = "";  // message.data[0].ToString();

        // 2. 주소 검증 (/interaction/Pancreas 형태인지 확인)
        string[] addressParts = address.Split('/');
        if (addressParts.Length < 3 || addressParts[1] != "interaction") return;

        string remoteOrganID = addressParts[2];
        bool isRemoteViewing = (status == "start");

        // 3. 대상 객체 찾아서 상태 동기화 (최신 API 적용됨: FindObjectsByType)
        // FindObjectsSortMode.None을 사용하면 이전 FindObjectsOfType보다 훨씬 빠름
        DisplayInfo[] allInfos = FindObjectsByType<DisplayInfo>(FindObjectsSortMode.None);

        foreach (DisplayInfo info in allInfos)
        {
            // DisplayInfo에 OrganID라는 변수가 있다고 가정
            // if (info.OrganID == remoteOrganID)
            // {
            //     if (isRemoteViewing) info.ShowRemoteStatus();
            //     else info.HideRemoteStatus();
            //     break; 
            // }
        }

        Debug.Log($"[OSC Receive] {address} -> {status}");
    }
}