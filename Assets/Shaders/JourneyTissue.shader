Shader "인체탐험/차분한 기관 조직"
{
 Properties {
  _BaseColor ("조직 기본 색상", Color) = (.55,.22,.26,1)
  _LightColor ("조직 밝은 색상", Color) = (.72,.34,.37,1)
  _Wetness ("점막 반사", Range(0,1)) = .18
  _Detail ("표면 결", Range(0,1)) = .25
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Cull Off ZWrite On
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_LightColor;float _Wetness,_Detail;
   CBUFFER_END
   struct A {float4 position:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=TransformObjectToHClip(v.position.xyz);o.world=TransformObjectToWorld(v.position.xyz);o.normal=TransformObjectToWorldNormal(v.normal);return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.world;
    // 실제 공간에 고정된 미세한 결. 구면 좌표, 방사형 주름, 화면 전체 움직임을 사용하지 않는다.
    float tissue=sin(p.x*3.7+sin(p.z*2.1))*sin(p.y*3.1+sin(p.x*1.8));
    float fine=sin(dot(p,float3(24,17,11)))*sin(dot(p,float3(-9,23,16)));
    float3 color=lerp(_BaseColor.rgb,_LightColor.rgb,.42+tissue*_Detail*.35+fine*_Detail*.06);
    float3 n=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.world);n*=dot(n,view)>=0?1:-1;
    float3 light=normalize(float3(.3,.75,-.45));
    float shade=.60+.40*saturate(dot(n,light));
    float sheen=pow(saturate(dot(n,normalize(view+light))),36)*_Wetness*.22;
    return half4(color*shade+sheen,1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
