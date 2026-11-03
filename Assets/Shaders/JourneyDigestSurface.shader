Shader "인체탐험/소화되는 음식 표면"
{
 Properties
 {
  _BaseMap ("음식 표면 그림", 2D) = "white" {}
  _BaseColor ("음식 색상", Color) = (1,1,1,1)
  _BumpMap ("표면 요철", 2D) = "bump" {}
  _Dissolve ("분해 진행도", Range(0,1)) = 0
  _EdgeColor ("부드러워진 경계 색상", Color) = (.45,.25,.09,1)
  _Wetness ("촉촉한 반사", Range(0,1)) = .22
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor, _EdgeColor; float _Dissolve,_Wetness;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float2 uv:TEXCOORD3;float4 tangent:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v) {V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.local=v.positionOS.xyz;o.world=TransformObjectToWorld(v.positionOS.xyz);o.normal=TransformObjectToWorldNormal(v.normalOS);o.tangent=float4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
   float hash(float3 p) {return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   float noise(float3 p) {float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float pattern=noise(i.local*6)*.7+noise(i.local*15)*.3;
    float d=pattern-lerp(-.06,1.06,_Dissolve);clip(d);
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    base=lerp(_EdgeColor.rgb,base,smoothstep(0,.065,d));
    float3 n=normalize(i.normal),t=normalize(i.tangent.xyz),b=cross(n,t)*i.tangent.w;
    float3 map=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
    n=normalize(t*map.x*.45+b*map.y*.45+n*map.z);
    Light light=GetMainLight(); float3 v=GetWorldSpaceNormalizeViewDir(i.world);
    float diffuse=saturate(dot(n,light.direction))*.55+.45;
    float spec=pow(saturate(dot(n,normalize(v+light.direction))),48)*_Wetness;
    return half4(base*(diffuse*max(light.color,float3(.45,.45,.45)))+spec,1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
