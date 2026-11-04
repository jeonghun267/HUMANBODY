using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// STILL 키트처럼 판에서 떠 있는 입체 버튼: 앞판(Kenney 입체 버튼 그림)이 lift 만큼 앞으로 떠 있고,
/// 가리키면 밝아지며 둘레 고리가 켜지고, 누르면 판 쪽으로 쑥 들어갔다가 돌아온다. 겨냥 판정은 버튼의 BoxCollider 가 그대로 맡는다.
/// </summary>
[AddComponentMenu("인체탐험/입체 버튼")]
public class DepthButton : MonoBehaviour
{
    public Image front;
    public Graphic ring;
    public Color tint = new Color(.16f, .55f, .78f, 1f), hoverTint = new Color(.3f, .72f, .95f, 1f);
    [Tooltip("앞판이 판에서 떠 있는 거리 (캔버스 단위)")]
    public float lift = 12f;
    public float pressSeconds = .18f;

    bool hover;
    float press;
    Vector3 rest;

    void Awake() { if (front != null) rest = front.rectTransform.localPosition; }

    public void SetHover(bool on) => hover = on;
    public void Press() => press = 1f;

    void Update()
    {
        press = Mathf.MoveTowards(press, 0f, Time.deltaTime / Mathf.Max(.01f, pressSeconds));
        if (front != null)
        {
            front.color = Color.Lerp(front.color, hover || press > 0 ? hoverTint : tint, 1 - Mathf.Exp(-Time.deltaTime * 18f));
            front.rectTransform.localPosition = new Vector3(rest.x, rest.y, -lift * (1f - .85f * Mathf.Sin(press * Mathf.PI * .5f)));
        }
        if (ring != null) ring.enabled = hover;
    }
}
