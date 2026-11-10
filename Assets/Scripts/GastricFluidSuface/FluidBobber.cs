using UnityEngine;

[AddComponentMenu("인체탐험/소화액 움직임") ]
public class FluidBobber : MonoBehaviour
{
    // [Inspector에서 조절할 수 있는 설정 변수들]

    [Header("움직임 설정")]
    [Tooltip("상하로 움직이는 속도입니다. 값이 낮을수록 느리고 끈적합니다.")]
    public float bobSpeed = 0.5f;

    [Tooltip("최대 움직이는 폭입니다. 위아래로 움직이는 거리입니다.")]
    public float bobHeight = 0.005f;

    [Header("흐름 설정")]
    [Tooltip("텍스처가 흐르는 속도 (가로, 세로 방향) 입니다.")]
    public Vector2 flowSpeed = new Vector2(0.01f, 0.01f);

    // [내부 변수]
    private float initialY;
    private Renderer rend; // Material을 가져오기 위한 변수
    private float currentXOffset;
    private float currentYOffset;

    // Start는 오브젝트가 시작될 때 한 번 호출됩니다.
    void Start()
    {
        // 오브젝트의 초기 Y 위치를 저장합니다.
        initialY = transform.localPosition.y;
        rend = GetComponent<Renderer>(); // 렌더러 컴포넌트를 가져옵니다.

        // Material이 있는지 확인
        if (rend == null || rend.material == null)
        {
            Debug.LogError("Renderer 또는 Material을 찾을 수 없습니다! 흐르는 효과를 적용할 수 없습니다.");
            enabled = false; // 스크립트 비활성화
        }
    }

    // Update는 매 프레임마다 호출됩니다.
    void Update()
    {
        // 1. 시간 기반 값 계산 (사인 곡선을 사용하여 부드러운 왕복 운동을 만듭니다.)
        // Time.time * bobSpeed를 통해 시간이 흐르는 속도를 조절합니다.
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        // 2. 오브젝트의 위치를 업데이트합니다.
        // 초기 위치에서 계산된 오프셋만큼 위아래로 움직입니다.
        Vector3 newPosition = new Vector3(
            transform.localPosition.x,
            initialY + offset,
            transform.localPosition.z
        );

        transform.localPosition = newPosition;

        if (rend != null && rend.material != null)
        {
            // 시간에 따라 Offset 값을 누적하여 계산합니다.
            currentXOffset += flowSpeed.x * Time.deltaTime;
            currentYOffset += flowSpeed.y * Time.deltaTime;

            // Material의 _MainTex_ST (노멀 맵에 주로 사용됨) Offset을 설정합니다.
            // URP Lit 셰이더 기준 _BaseMap 사용
            rend.material.SetTextureOffset("_BaseMap", new Vector2(currentXOffset, currentYOffset));
            // 만약 BaseMap으로 안되면 _BumpMap 또는 _NormalMap 텍스처에 대한 Offset을 사용해야 합니다.
            rend.material.SetTextureOffset("_BumpMap", new Vector2(currentXOffset, currentYOffset));
        }
    }
}