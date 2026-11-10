Shader "인체탐험/관찰창 유리"
{
 // 잠수정의 둥근 관찰창. 탐험가 눈이 거의 공 중심에 있어 유리가 정면으로 보이므로 반사만으로는 유리가 느껴지지 않는다.
 // 바깥 면에 맺혀 천천히 흘러내리는 물방울과 아주 옅은 얼룩으로 '유리 너머를 본다'는 느낌을 준다.
 Properties {
  _Tint ("유리 색", Color) = (.8,.95,1,.04)
  [NoScaleOffset] _DropMap ("물방울 자료", 2D) = "black" {}
  _DropScale ("물방울 반복", Float) = 7
  _DropSpeed ("흘러내림 속도", Float) = .012
  _DropStrength ("물방울 진하기", Range(0,1)) = .3
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;float _DropScale,_DropSpeed;half _DropStrength;
   CBUFFER_END
   TEXTURE2D(_DropMap);SAMPLER(sampler_DropMap);
   float4 _JourneyLamp;
   struct A {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(v.position.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normal);o.uv=v.uv;return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    half3 n=normalize(i.normal);
    half fresnel=pow(1-abs(dot(n,view)),4);
    // 물방울: R = 방울 모양, G = 방울 안의 밝은 반사점. 위도 방향(v)으로 흘러내린다.
    float2 uv=float2(i.uv.x*2,i.uv.y)*_DropScale+float2(0,_Time.y*_DropSpeed*_DropScale);
    half2 drop=SAMPLE_TEXTURE2D(_DropMap,sampler_DropMap,uv).rg;
    half2 fine=SAMPLE_TEXTURE2D(_DropMap,sampler_DropMap,uv*2.7+.37).rg*.5;
    // 눈높이 정면에는 방울을 두지 않는다(가까운 유리와 먼 벽을 번갈아 초점 맞추면 눈이 피로하다). 위·아래 가장자리에만 맺힌다.
    half edge=smoothstep(.35,.75,abs(n.y));
    half shape=max(drop.r,fine.r)*_DropStrength*edge,glint=max(drop.g,fine.g)*_DropStrength*edge;
    half3 lit=SampleSH(n)+_JourneyLamp.w*.15;
    half3 color=_Tint.rgb*(.3+fresnel)*lit+glint*min(lit,1.2)*1.4;
    half alpha=_Tint.a+fresnel*.35+shape*.22+glint*.6;
    return half4(color,saturate(alpha));
   }
   ENDHLSL
  }
 }
 Fallback Off
}
