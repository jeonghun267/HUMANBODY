using UnityEngine;

/// <summary>이동 제어와 분리한 관절 동작. 실제 생체 운동을 재현하는 모형이 아닌 안내용 캐릭터 연출이다.</summary>
[AddComponentMenu("인체탐험/캐릭터 관절 동작")]
public class JourneyCharacterMotion : MonoBehaviour
{
    public enum CharacterRole { 안내자, 소화효소, 펩신, 유익균, 유해균, 바이러스, 적혈구 }
    public CharacterRole characterRole;
    [Tooltip("몸통, 머리, 부리, 왼팔, 오른팔, 왼날개, 오른날개, 하체 순서입니다.")]
    public Transform[] joints;
    [Range(0f, 1.5f)] public float motionStrength = 1f;
    [Tooltip("음성이나 자막 안내 중 부리와 머리를 움직입니다.")]
    public bool speaking;
    public float CutAmount { get; private set; }
    public Transform FocusTarget { get; private set; }
    Vector3[] restPositions, restScales;
    Quaternion[] restRotations;
    float phase, greetingUntil, focusUntil;
    AudioSource voice;
    readonly float[] samples = new float[64];
    float mouth;

    void Awake() { Cache(); phase = Random.value * 6.28f; voice = GetComponentInParent<AudioSource>(); }
    void OnEnable() { if (characterRole == CharacterRole.안내자) Greet(); }
    void Cache()
    {
        if (joints == null || joints.Length != 8 || restPositions != null) return;
        restPositions = new Vector3[8]; restScales = new Vector3[8]; restRotations = new Quaternion[8];
        for (int i=0;i<8;i++) if(joints[i]!=null) { restPositions[i]=joints[i].localPosition; restRotations[i]=joints[i].localRotation; restScales[i]=joints[i].localScale; }
    }
    public void Greet() { greetingUntil = Time.time + 2.2f; }
    public void Focus(Transform target) { FocusTarget=target; focusUntil=Time.time+6f; }
    public void SetCut(float amount) { CutAmount=Mathf.Clamp01(amount); }
    void LateUpdate()
    {
        float energy = speaking ? .2f + .8f * Mathf.Abs(Mathf.Sin(Time.time*10.5f)) : 0;
        if(speaking && voice != null && voice.isPlaying)
        {
            voice.GetOutputData(samples,0); float sum=0; foreach(float s in samples) sum+=s*s;
            energy=Mathf.Clamp01(Mathf.Sqrt(sum/samples.Length)*14f);
        }
        mouth=Mathf.MoveTowards(mouth, energy, Time.deltaTime*9f);
        if(FocusTarget != null && Time.time > focusUntil) FocusTarget=null;
        float turn=FocusTarget == null ? 0 : Mathf.Clamp(transform.InverseTransformPoint(FocusTarget.position).x*6,-16,16);
        Pose(Time.time+phase,mouth,Time.time < greetingUntil ? Mathf.Sin(Mathf.Clamp01((greetingUntil-Time.time)/2.2f)*Mathf.PI) : 0,CutAmount,turn);
    }
    /// <summary>자세 검증에도 사용하는 결정적 관절 계산. 모형의 이동 위치나 방향은 변경하지 않는다.</summary>
    public void Pose(float time,float talk,float greet,float cut,float look)
    {
        Cache(); if(restPositions==null) return;
        ResetPose();
        float s=motionStrength, wave=Mathf.Sin(time*2.3f), slow=Mathf.Sin(time*1.3f);
        if(characterRole==CharacterRole.안내자)
        {
            Rotate(1,new Vector3(slow*1.5f+talk*3f,look,slow*1.8f));
            Rotate(2,new Vector3(talk*13f,0,0));
            Rotate(3,new Vector3(greet*12f*Mathf.Sin(time*7f),0,-greet*17f-talk*3f));
            Rotate(4,new Vector3(talk*3f,0,slow*2f));
            Rotate(5,new Vector3(0,wave*9f+greet*12f,0));
            Rotate(6,new Vector3(0,-wave*9f-greet*12f,0));
            Scale(0,new Vector3(1,1+.005f*slow*s,1));
        }
        else if(characterRole==CharacterRole.바이러스)
        {
            Rotate(1,new Vector3(wave*4f,slow*5f,0));
            Scale(3,new Vector3(1+.045f*wave*s,1,1)); Scale(4,new Vector3(1-.045f*wave*s,1,1));
            Scale(5,new Vector3(1,1,1+.04f*slow*s)); Scale(6,new Vector3(1,1,1-.04f*slow*s));
        }
        else if(characterRole==CharacterRole.적혈구)
        {
            Rotate(3,new Vector3(0,0,slow*7f));Rotate(4,new Vector3(0,0,-slow*7f));
            Scale(0,new Vector3(1+.015f*wave*s,1-.025f*wave*s,1));
        }
        else
        {
            float pace=characterRole==CharacterRole.유해균 ? 1.5f : 2.5f;
            float stroke=Mathf.Sin(time*pace);
            Rotate(1,new Vector3(slow*4,0,stroke*3));
            Rotate(7,new Vector3(stroke*4,0,-stroke*3));
            float arm=characterRole==CharacterRole.펩신 ? 8 : characterRole==CharacterRole.유익균 ? 14 : 6;
            Rotate(3,new Vector3(stroke*arm,cut*16,-cut*25));
            Rotate(4,new Vector3(-stroke*arm,-cut*16,cut*25));
            Scale(0,new Vector3(1+.012f*wave*s,1-.018f*wave*s,1));
        }
    }
    void Rotate(int i,Vector3 degrees) { if(joints[i]!=null) joints[i].localRotation=restRotations[i]*Quaternion.Euler(degrees*motionStrength); }
    void Scale(int i,Vector3 scale) { if(joints[i]!=null) joints[i].localScale=Vector3.Scale(restScales[i],scale); }
    void ResetPose() { for(int i=0;i<8;i++) if(joints[i]!=null) { joints[i].localPosition=restPositions[i];joints[i].localRotation=restRotations[i];joints[i].localScale=restScales[i]; } }
    void OnDisable() { speaking=false;mouth=0;CutAmount=0;FocusTarget=null; if(restPositions!=null) ResetPose(); }
}
