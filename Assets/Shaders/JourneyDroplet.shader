Shader "인체탐험/점액 방울"
{
 // 침·위액·담즙 같은 끈적한 분비물 입자. 가운데는 투명하고 가장자리가 굴절빛으로 밝으며 작은 반사점이 있다.
 // 몸속 장면은 어두워서 스스로 빛나면 안 되므로, 탐험가 전등(_JourneyLamp)과 주변광으로만 밝기를 정한다.
 // _Strand 를 켜면 입자 꼬리(가닥)용: 길이 방향으로는 이어지고 폭 방향으로만 둥글다.
 Properties {
  _BaseColor ("액체 색 (알파 = 진하기)", Color) = (.86,.93,1,.55)
  _Rim ("가장자리 굴절빛", Range(0,2)) = 1
  _Highlight ("반사점", Range(0,3)) = 1.3
  [Toggle] _Strand ("가닥(꼬리)용", Float) = 0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;half _Rim,_Highlight,_Strand;
   CBUFFER_END
   float4 _JourneyLamp; // xyz 전등 위치, w 세기. BodyHeadlamp 가 넣는다.
   struct A {float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;half3 light:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v)
   {
    V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 world=TransformObjectToWorld(v.position.xyz);
    o.positionCS=TransformWorldToHClip(world);
    o.color=v.color*_BaseColor;o.uv=v.uv;
    float3 d=world-_JourneyLamp.xyz;
    o.light=SampleSH(half3(0,1,0))+_JourneyLamp.w/(1+dot(d,d));
    o.fog=ComputeFogFactor(o.positionCS.z);
    return o;
   }
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1;
    bool strand=_Strand>.5;
    float r=strand?abs(p.y):length(p);
    clip(.99-r);
    float body=sqrt(saturate(1-r*r));
    float rim=pow(saturate(r),4)*_Rim;
    float2 h=strand?float2(0,p.y+.35):p-float2(-.3,.35);
    float shine=exp(-dot(h,h)*30)*_Highlight;
    half3 lit=min(i.light,1.5);
    half3 color=i.color.rgb*(.3+.45*body)*lit+(rim*.55+shine)*lit;
    half alpha=i.color.a*(.35+.65*saturate(rim+.2*(1-body)))+shine*.5;
    alpha*=1-smoothstep(.86,.99,r);
    return half4(MixFog(color,i.fog),saturate(alpha));
   }
   ENDHLSL
  }
 }
 Fallback Off
}
