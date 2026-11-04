using TMPro;
using UnityEngine;

/// <summary>
/// 잠수정 탑승구 옆 조작대의 '출발' 입체 버튼. 진찰이 끝나 관문(MissionGate)이 열리기 전에는 회색으로 잠겨 있고 "진찰을 먼저 받아요",
/// 열리면 초록으로 빛나며 손으로 가리키고 방아쇠를 당기면 다음 단계(입)로 떠난다. 탑승구에 걸어 들어가기만 해서는 떠나지 않는다.
/// </summary>
[AddComponentMenu("인체탐험/탑승 출발 버튼")]
public class BoardingConsole : MonoBehaviour
{
    public StageExitPortal portal;
    public MissionGate gate;
    public DepthButton button;
    public BoxCollider buttonBox;
    public TextMeshProUGUI status;
    [Tooltip("이 거리 안에서 가리켜야 누를 수 있다 (m)")] public float reach = 4f;
    public Color readyTint = new Color(.18f, .58f, .4f, 1f), readyHover = new Color(.28f, .78f, .52f, 1f), lockedTint = new Color(.34f, .39f, .44f, 1f);
    [TextArea] public string lockedText = "진찰을 먼저 받아요", readyText = "준비 완료! 출발 버튼을 누르세요";

    bool departed;

    public bool Ready => gate == null || gate.IsOpen;
    public bool Departed => departed;

    void Update()
    {
        if (button == null || departed) return;
        bool ready = Ready;
        button.tint = ready ? readyTint : lockedTint;
        button.hoverTint = ready ? readyHover : lockedTint;
        if (status != null) status.text = ready ? readyText : lockedText;
        bool hover = buttonBox != null && Physics.Raycast(JourneyAim.Ray(), out var hit, reach, ~0, QueryTriggerInteraction.Collide) && hit.collider == buttonBox;
        button.SetHover(hover);
        if (hover && JourneyAim.TriggerDown()) Press();
    }

    /// <summary>출발 버튼을 누른다. 잠겨 있으면 낮은 소리만 나고 떠나지 않는다.</summary>
    public void Press()
    {
        if (departed) return;
        if (button != null) button.Press();
        if (!Ready) { Sfx.Play("incorrect", transform.position, .5f); return; }
        departed = true;
        Sfx.Play("success", transform.position, .8f);
        Haptics.Pulse(.6f, .15f);
        if (status != null) status.text = "출발!";
        if (portal != null) portal.Depart();
    }
}
