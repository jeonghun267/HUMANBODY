using System.Collections.Generic;
using UnityEngine;
using OscJack;

public class OSCBodyManager : MonoBehaviour
{
    public static OSCBodyManager Instance;

    // 메인 스레드에서 실행할 행동을 담는 큐
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
    private readonly object queueLock = new object();

    [Header("Network Settings")]
    [Tooltip("상대방(B유저)의 IP 주소를 입력하세요. 내 IP(127.0.0.1)를 적으면 무한 루프에 빠질 수 있습니다.")]
    public string targetIP = "192.168.0.218";
    public int sendPort = 7600;
    public int receivePort = 7500;

    [Header("Button Events (Receiver Only)")]
    // 상대방이 버튼을 눌렀을 때 실행될 이벤트
    public UnityEngine.Events.UnityEvent onRemotePressA;
    public UnityEngine.Events.UnityEvent onRemotePressB;

    private OscClient client;
    private OscServer server;

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // 1. OSC 통신 초기화
        client = new OscClient(targetIP, sendPort);
        server = new OscServer(receivePort);

        // 2. 메시지 수신 대기
        server.MessageDispatcher.AddCallback("/input/a", OnReceiveButtonA);
        server.MessageDispatcher.AddCallback("/input/b", OnReceiveButtonB);
    }

    void Update()
    {
        // =================================================================
        // [수정된 부분] 안전장치 추가: 한 프레임에 최대 50개까지만 처리
        // 메시지가 폭주해도 에디터가 멈추지 않게 방어합니다.
        // =================================================================
        lock (queueLock)
        {
            int processLimit = 50; // 한 프레임당 처리 한도

            while (mainThreadActions.Count > 0 && processLimit > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
                processLimit--;
            }
        }

        // =================================================================
        // 2. 내 입력 감지 및 전송 (보내는 로직)
        // =================================================================

        // A 버튼
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            client.Send("/input/a", 1);
            Debug.Log("[OSC] 내(A)가 A버튼을 눌러 전송함");
        }

        // B 버튼
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            client.Send("/input/b", 1);
            Debug.Log("[OSC] 내(A)가 B버튼을 눌러 전송함");
        }
    }

    // ==========================================
    // [수신부] 백그라운드 스레드에서 실행됨
    // ==========================================
    private void OnReceiveButtonA(string address, OscDataHandle data)
    {
        if (data.GetElementAsInt(0) == 1)
        {
            lock (queueLock)
            {
                mainThreadActions.Enqueue(() =>
                {
                    Debug.Log("[OSC] 상대방이 A 버튼을 눌렀음");
                    onRemotePressA.Invoke();
                });
            }
        }
    }

    private void OnReceiveButtonB(string address, OscDataHandle data)
    {
        if (data.GetElementAsInt(0) == 1)
        {
            lock (queueLock)
            {
                mainThreadActions.Enqueue(() =>
                {
                    Debug.Log("[OSC] 상대방이 B 버튼을 눌렀음");
                    onRemotePressB.Invoke();
                });
            }
        }
    }

    void OnDestroy()
    {
        client?.Dispose();
        server?.Dispose();
    }
}