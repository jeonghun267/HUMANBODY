using UnityEngine;
[AddComponentMenu("인체탐험/음식 표면 분해")]
public class JourneyDissolve : MonoBehaviour
{
    public Renderer[] dissolveRenderers;
    public float Progress { get; private set; }
    MaterialPropertyBlock block;
    static readonly int Id=Shader.PropertyToID("_Dissolve");
    public void SetProgress(float progress)
    {
        Progress=Mathf.Clamp01(progress);block??=new MaterialPropertyBlock();
        if(dissolveRenderers==null) return;
        foreach(var r in dissolveRenderers) if(r!=null) { r.GetPropertyBlock(block);block.SetFloat(Id,Progress);r.SetPropertyBlock(block); }
    }
    void OnEnable() { SetProgress(0); }
}
