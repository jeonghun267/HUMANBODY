using UnityEngine;
using UnityEngine.UI;

/// <summary>모니터의 심전도 줄: 왼쪽에서 오른쪽으로 그려지며 지나간 자리는 흐려진다 (판 안에 그리는 UI 선).</summary>
[AddComponentMenu("인체탐험/심전도 줄")]
[RequireComponent(typeof(CanvasRenderer))]
public class EcgLine : MaskableGraphic
{
    public float beatsPerMinute = 78f;
    public float sweepSeconds = 3f;
    public float thickness = 4f;
    [Range(16, 240)] public int samples = 160;

    float head;

    void Update()
    {
        head = (head + Time.deltaTime / Mathf.Max(.1f, sweepSeconds)) % 1f;
        SetVerticesDirty();
    }

    /// <summary>한 박동 안 위치(0~1)에서의 높이(-1~1): 작은 P, 뾰족한 QRS, 둥근 T.</summary>
    static float Beat(float p)
    {
        float Bump(float c, float w, float h) => h * Mathf.Exp(-(p - c) * (p - c) / (w * w));
        return Bump(.18f, .03f, .12f) - Bump(.30f, .008f, .15f) + Bump(.32f, .01f, 1f) - Bump(.345f, .01f, .3f) + Bump(.55f, .05f, .25f);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float beatsInSweep = beatsPerMinute / 60f * sweepSeconds;
        Vector2 Point(int i)
        {
            float u = (float)i / samples;
            return new Vector2(r.xMin + u * r.width, r.center.y + Beat(u * beatsInSweep % 1f) * r.height * .45f);
        }
        for (int i = 0; i < samples; i++)
        {
            Vector2 a = Point(i), b = Point(i + 1);
            float u = (float)i / samples, age = (head - u + 1f) % 1f; // 방금 그린 곳이 가장 밝다
            if (age > .92f) continue; // 지우개 틈
            var c = color; c.a *= Mathf.Lerp(1f, .15f, age);
            var n = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f;
            int k = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero); vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
    }
}
