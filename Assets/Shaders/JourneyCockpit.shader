Shader "인체탐험/잠수정 내부"
{
 // 잠수정 뼈대·바닥·조종대. 탐험가 전등이 바로 옆에 있어 장면 조명을 받으면 하얗게 타 버리므로,
 // 조종석 안쪽에 고정된 은은한 실내등만으로 칠한다. _Glow 로 계기판 줄빛을 낸다.
 Properties {
  _BaseColor ("금속 색", Color) = (.2,.25,.28,1)
  _Shine ("광택", Range(0,1)) = .5
  _GlowColor ("빛 띠 색", Color) = (.25,.85,1,1)
  _Glow ("빛 띠 세기", Range(0,4)) = 0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor,_GlowColor;half _Shine,_Glow;
   CBUFFER_END
   struct A {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(v.position.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normal);o.uv=v.uv;return o;}
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    half3 key=normalize(half3(.2,1,.35));
    half diffuse=.35+.65*saturate(dot(n,key)*.5+.5);
    half spec=pow(saturate(dot(n,normalize(key+view))),48)*_Shine;
    half rim=pow(1-saturate(abs(dot(n,view))),3)*.25;
    half3 color=_BaseColor.rgb*diffuse+spec*.6+rim*_GlowColor.rgb*.3+_GlowColor.rgb*_Glow;
    return half4(color,1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
