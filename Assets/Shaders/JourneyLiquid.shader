Shader "인체탐험/둥근 액체 입자"
{
 Properties { _BaseColor ("물방울 색상", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct A {float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
   float4 _BaseColor;
   V vert(A v) {V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(V,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_BaseColor; o.uv=v.uv; return o;}
   half4 frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1; float r=length(p); clip(.98-r);
    float edge=1-smoothstep(.80,.98,r);
    float shine=exp(-dot(p-float2(-.28,.32),p-float2(-.28,.32))*38);
    float rim=pow(saturate(r),5)*.4;
    return half4(i.color.rgb*(.65+.3*sqrt(saturate(1-r*r)))+shine*.8+rim*.24, i.color.a*edge*(.65+rim));
   }
   ENDHLSL
  }
 }
 Fallback Off
}
