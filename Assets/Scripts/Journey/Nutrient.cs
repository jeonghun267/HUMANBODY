using UnityEngine;

/// <summary>영양소 오브젝트 하나. 손(콜라이더)이 닿아도 흡수된다.</summary>
[AddComponentMenu("인체탐험/영양소") ]
public class Nutrient : MonoBehaviour
{
    [HideInInspector] public NutrientAbsorption.NutrientKind kind;
    [HideInInspector] public NutrientAbsorption owner;
    [HideInInspector] public bool absorbed;

    Renderer[] renderers;
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    bool highlighted;

    bool applied; // 현재 이미션이 켜져 있는지 (매 프레임 다시 쓰지 않도록)

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(); block = new MaterialPropertyBlock();
        foreach (var r in renderers) foreach (var m in r.materials) if (m != null && m.HasProperty(EmissionId)) m.EnableKeyword("_EMISSION");
    }

    void LateUpdate()
    {
        // 응시가 끝나면 다음 프레임에 하이라이트 해제 (owner 가 매 프레임 true 로 갱신)
        if (highlighted) { highlighted = false; return; }
        if (applied) { ApplyEmission(0f); applied = false; }
    }

    public void SetHighlight(bool on) { highlighted = on; if (on && !applied) { ApplyEmission(1.2f); applied = true; } }

    void ApplyEmission(float strength)
    {
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, Color.white * strength);
            r.SetPropertyBlock(block);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner != null && (other.CompareTag("Hand") || other.CompareTag("Player"))) owner.Absorb(this);
    }
}
