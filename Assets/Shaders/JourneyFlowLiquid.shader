Shader "인체탐험/흐르는 액체"
{
 // 위액 웅덩이, 소장의 미즙 물길, 대장의 묽은 내용물. 표면 물결이 흐르는 방향(UV 의 v)으로 움직이고
 // 탐험가 전등이 젖은 수면에 반짝이며, 비스듬히 볼수록 반사가 강해진다.
 Properties {
  _BaseColor ("액체 색 (알파 = 탁한 정도)", Color) = (.8,.72,.35,.7)
  _DeepColor ("깊은 곳 색", Color) = (.35,.22,.08,1)
  [NoScaleOffset] _DetailMap ("물결 자료 (점막 결 자료 재사용)", 2D) = "bump" {}
  _WaveScale ("물결 크기 (1m당)", Float) = .7
  _FlowSpeed ("흐름 속도 (m/초)", Float) = .25
  _WaveStrength ("물결 굴곡", Range(0,1)) = .35
  _Gloss ("반짝임", Range(0,4)) = 1.6
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
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
   half4 _BaseColor,_DeepColor;float _WaveScale,_FlowSpeed;half _WaveStrength,_Gloss;
   CBUFFER_END
   TEXTURE2D(_DetailMap);SAMPLER(sampler_DetailMap);
   float4 _JourneyLamp;
   struct A {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(v.position.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    // uv 는 미터 단위: x = 가로, y = 흐르는 방향 거리. 두 겹의 물결을 다른 속도로 흘려 반복이 보이지 않게 한다.
    float t=_Time.y*_FlowSpeed;
    half4 a=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,(i.uv+float2(0,-t))*_WaveScale);
    half4 b=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,(i.uv*1.7+float2(.31,-t*1.6))*_WaveScale);
    half2 slope=((a.rg*2-1)+(b.rg*2-1)*.6)*_WaveStrength;
    half3 n=normalize(half3(slope.x,1,slope.y));
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    half facing=saturate(abs(dot(n,view)));
    half fresnel=.03+.97*pow(1-facing,5);
    half churn=saturate(a.b*.6+b.b*.4);
    half3 body=lerp(_DeepColor.rgb,_BaseColor.rgb,.45+.4*churn);
    half3 lampVec=_JourneyLamp.xyz-i.world;half lampDist2=dot(lampVec,lampVec);half3 l=lampVec*rsqrt(max(lampDist2,1e-4));
    half lamp=_JourneyLamp.w/(1+lampDist2);
    half3 h=normalize(l+view);
    half spec=pow(saturate(dot(n,h)),180)*_Gloss*lamp*6;
    half3 ambient=SampleSH(half3(0,1,0));
    half3 color=body*(ambient+lamp*saturate(dot(n,l)*.5+.5))+spec+fresnel*ambient*.8;
    half alpha=saturate(_BaseColor.a+fresnel*.5+spec*.3);
    return half4(MixFog(color,i.fog),alpha);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
