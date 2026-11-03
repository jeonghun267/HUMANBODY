Shader "인체탐험/실사 점막"
{
 // 젖은 점막: 점액막 반사(맑은 겉막) + 조직 속 산란 + 혈관망 + 미세 굴곡.
 // 결 자료(_DetailMap): R·G 굴곡 방향, B 높이, A 혈관. 세계 좌표 삼면 투영이라 모형마다 UV 가 달라도 이음매가 없다.
 Properties {
  _BaseColor ("점막 기본 색", Color) = (.78,.36,.36,1)
  _DeepColor ("주름 안쪽 색", Color) = (.38,.09,.13,1)
  _ScatterColor ("조직 속 산란 색", Color) = (.95,.25,.2,1)
  _VesselColor ("혈관 색", Color) = (.52,.06,.1,1)
  [NoScaleOffset] _DetailMap ("점막 결 자료", 2D) = "bump" {}
  _DetailScale ("잔결 반복 (1m당)", Float) = 1.6
  _MacroScale ("큰 결 반복 (1m당)", Float) = .28
  _NormalStrength ("잔결 굴곡", Range(0,2)) = .7
  _MacroNormalStrength ("큰 굴곡", Range(0,2)) = .6
  _VesselStrength ("잔혈관 진하기", Range(0,1)) = .35
  _LargeVesselStrength ("굵은 혈관 진하기", Range(0,1)) = .3
  _Mottle ("색 얼룩", Range(0,1)) = .25
  _Cavity ("주름 그늘", Range(0,1)) = .5
  _Smoothness ("조직 매끄러움", Range(0,1)) = .55
  _CoatSmoothness ("점액막 매끄러움", Range(0,1)) = .9
  _CoatStrength ("점액막 반사", Range(0,2)) = 1
  _Wrap ("빛 스며듦", Range(0,1)) = .45
  _Velvet ("융털 벨벳 광택", Range(0,1)) = 0
  _EnvReflection ("주변 반사", Range(0,1)) = .25
  [Header(Flow)]
  _FlowDir ("벽을 타고 흐르는 방향 (세계)", Vector) = (0,-1,0,0)
  _FlowSpeed ("흐름 속도 (m/초)", Float) = .2
  _FlowStrength ("흐르는 액체 막", Range(0,1)) = 0
  [Header(Teeth)]
  [Toggle(_TEETH)] _Teeth ("치아와 잇몸 구분 (원본 그림 사용)", Float) = 0
  _MainTex ("치아 원본 그림", 2D) = "white" {}
  _EnamelColor ("법랑질 색", Color) = (.93,.9,.83,1)
  _GumThreshold ("잇몸 판정 기준 (빨강-파랑)", Range(0,1)) = .3
  [HideInInspector] _DebugView ("검사용 보기", Float) = 0
  _NearLampLimit ("가까운 벽 눈부심 한도", Range(.05,2)) = .45
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  CBUFFER_START(UnityPerMaterial)
  half4 _BaseColor,_DeepColor,_ScatterColor,_VesselColor;
  float _DetailScale,_MacroScale;
  half _NormalStrength,_MacroNormalStrength,_VesselStrength,_LargeVesselStrength,_Mottle,_Cavity;
  half _Smoothness,_CoatSmoothness,_CoatStrength,_Wrap,_Velvet,_EnvReflection;
  float4 _MainTex_ST;half4 _EnamelColor;half _GumThreshold;
  float4 _FlowDir;float _FlowSpeed;half _FlowStrength;
  half _DebugView,_NearLampLimit;
  CBUFFER_END
  TEXTURE2D(_DetailMap);SAMPLER(sampler_DetailMap);
  TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);

  struct Detail {half3 normal;half height;half vessel;};
  // 세 방향 투영을 한 번에 섞는다. 굴곡은 표면 법선에 더하는 방식(UDN)이라 접선 자료가 필요 없다.
  Detail SampleDetail(float3 p,half3 n,half3 blend,float scale,half strength)
  {
   half4 x=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.zy*scale);
   half4 y=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.xz*scale);
   half4 z=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.xy*scale);
   half2 dx=(x.rg*2-1)*strength,dy=(y.rg*2-1)*strength,dz=(z.rg*2-1)*strength;
   Detail d;
   d.normal=half3(0,dx.y,dx.x)*blend.x+half3(dy.x,0,dy.y)*blend.y+half3(dz.x,dz.y,0)*blend.z;
   d.height=dot(half3(x.b,y.b,z.b),blend);
   d.vessel=dot(half3(x.a,y.a,z.a),blend);
   return d;
  }
  half3 TriplanarBlend(half3 n){half3 b=pow(abs(n),4);return b/max(dot(b,1),1e-4);}
  ENDHLSL

  Pass
  {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForward"}
   Cull Off ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #pragma shader_feature_local _TEETH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct A {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;float2 uv:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v)
   {
    V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=TransformObjectToWorld(v.position.xyz);o.positionCS=TransformWorldToHClip(o.world);
    o.normal=TransformObjectToWorldNormal(v.normal);o.fog=ComputeFogFactor(o.positionCS.z);
    o.uv=TRANSFORM_TEX(v.uv,_MainTex);
    return o;
   }
   // 조직 한 점에 들어오는 빛 하나: 감싸는 확산(빛이 조직 속으로 번지는 테두리) + 조직 반사 + 점액막 반사.
   half3 Shade(Light light,half3 n,half3 nCoat,half3 view,half3 albedo,half3 scatter,BRDFData tissue,BRDFData coat,half coatStrength)
   {
    // 전등이 눈 바로 옆이라 가까운 벽은 거리 감쇠가 폭주해 하얗게 탄다. 눈이 편하도록 한도를 둔다.
    half3 l=light.direction;half atten=min(light.distanceAttenuation,_NearLampLimit)*light.shadowAttenuation;
    half ndl=dot(n,l),direct=saturate(ndl),wrapped=saturate((ndl+_Wrap)/(1+_Wrap));
    half3 diffuse=albedo*(direct+(wrapped-direct)*scatter*1.6);
    half3 spec=tissue.specular*DirectBRDFSpecular(tissue,n,l,view)*direct;
    half coatNdl=saturate(dot(nCoat,l));
    half3 film=coat.specular*DirectBRDFSpecular(coat,nCoat,l,view)*coatNdl*coatStrength;
    half rim=pow(1-saturate(dot(n,view)),3)*_Velvet;
    return light.color*atten*(diffuse+spec+film+albedo*rim*wrapped);
   }
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 p=i.world;
    half3 view=GetWorldSpaceNormalizeViewDir(p);
    half3 ng=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
    half3 blend=TriplanarBlend(ng);
    Detail fine=SampleDetail(p,ng,blend,_DetailScale,_NormalStrength);
    Detail macro=SampleDetail(p+17.3,ng,blend,_MacroScale,_MacroNormalStrength);
    // gum: 1 = 점막(잇몸), 0 = 법랑질. 치아 모드가 아니면 전부 점막이다.
    half gum=1;half3 enamel=0;
    #if defined(_TEETH)
    half3 source=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
    gum=saturate((source.r-source.b-_GumThreshold)*6);
    enamel=_EnamelColor.rgb*(.8+.3*saturate(dot(source,half3(.3,.59,.11))/.8));
    #endif
    half3 n=normalize(ng+(fine.normal+macro.normal)*lerp(.15,1,gum));
    // 점액막은 잔결을 메우고 큰 굴곡만 따라간다.
    half3 nCoat=normalize(ng+macro.normal*.7*gum+fine.normal*.15*gum);
    // 벽을 타고 흐르는 얇은 액체 막: 결 자료를 흐름 방향으로 밀어 점액막 반사만 움직인다.
    if(_FlowStrength>0)
    {
     Detail film=SampleDetail(p-_FlowDir.xyz*(_Time.y*_FlowSpeed),ng,blend,_DetailScale*.45,_FlowStrength*gum);
     nCoat=normalize(nCoat+film.normal);
    }

    half3 albedo=_BaseColor.rgb*lerp(1-_Mottle,1+_Mottle*.6,macro.height);
    albedo=lerp(albedo,_DeepColor.rgb,saturate((1-fine.height)*_Cavity));
    albedo=lerp(albedo,_DeepColor.rgb,saturate((.55-macro.height)*_Cavity*1.4));
    half vessel=saturate(fine.vessel*_VesselStrength+macro.vessel*_LargeVesselStrength);
    albedo=lerp(albedo,_VesselColor.rgb,vessel);
    half occlusion=lerp(1,saturate(fine.height*1.4),_Cavity*.6)*lerp(1,saturate(macro.height*1.6),_Cavity*.5);
    albedo=lerp(enamel,albedo,gum);occlusion=lerp(1,occlusion,gum);
    // 법랑질은 빛이 노르스름하게 스며든다.
    half3 scatter=lerp(half3(.5,.44,.34),_ScatterColor.rgb,gum);

    half alpha=1;BRDFData tissue,coat;
    InitializeBRDFData(albedo,0,0,lerp(.82,_Smoothness,gum),alpha,tissue);
    InitializeBRDFData(0,0,0,_CoatSmoothness,alpha,coat);
    half coatStrength=_CoatStrength*occlusion;

    InputData inputData=(InputData)0;
    inputData.positionWS=p;inputData.normalWS=n;inputData.viewDirectionWS=view;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);

    half3 color=SampleSH(n)*albedo*occlusion;
    half fresnel=.04+.96*pow(1-saturate(dot(nCoat,view)),5);
    color+=GlossyEnvironmentReflection(reflect(-view,nCoat),p,PerceptualSmoothnessToPerceptualRoughness(_CoatSmoothness),occlusion,inputData.normalizedScreenSpaceUV)*fresnel*_EnvReflection*coatStrength;
    color+=Shade(GetMainLight(),n,nCoat,view,albedo,scatter,tissue,coat,coatStrength)*occlusion;
    #if defined(_ADDITIONAL_LIGHTS)
    uint count=GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
     color+=Shade(GetAdditionalLight(lightIndex,p),n,nCoat,view,albedo,scatter,tissue,coat,coatStrength)*occlusion;
    #endif
    LIGHT_LOOP_BEGIN(count)
     color+=Shade(GetAdditionalLight(lightIndex,p),n,nCoat,view,albedo,scatter,tissue,coat,coatStrength)*occlusion;
    LIGHT_LOOP_END
    #endif
    #if defined(_TEETH)
    if(_DebugView>.5)
    {
     if(_DebugView<1.5) return half4(albedo,1);
     if(_DebugView<2.5) return half4(gum.xxx,1);
     if(_DebugView<3.5) return half4(source,1);
     return half4(n*.5+.5,1);
    }
    #endif
    return half4(MixFog(max(color,0),i.fog),1);
   }
   ENDHLSL
  }

  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   Cull Off ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   struct A {float4 position:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.position.xyz);return o;}
   half frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return 0;}
   ENDHLSL
  }

  Pass
  {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}
   Cull Off ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   struct A {float4 position:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;half3 normal:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.position.xyz);o.normal=TransformObjectToWorldNormal(v.normal);return o;}
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
    #else
    return half4(n,0);
    #endif
   }
   ENDHLSL
  }
 }
 Fallback Off
}
