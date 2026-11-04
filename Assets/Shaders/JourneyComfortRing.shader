// 멀미 완화 테두리: 눈 앞에 붙은 고리 메시. 꼭짓점 색 알파(안쪽 0 → 바깥 1) × _Color 로 시야 가장자리를 어둡게 한다.
// 후처리(전체 화면 한 번 더 그리기) 없이 그리므로 Quest 에서 가볍고, 맨 마지막에 깊이 검사 없이 덮는다.
Shader "인체탐험/멀미 완화 테두리"
{
 Properties { _Color ("색(알파 = 세기)", Color) = (0,0,0,0) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) half4 _Color; CBUFFER_END
   struct A {float4 position:POSITION;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.position.xyz);o.color=v.color;return o;}
   half4 frag(V i):SV_Target{return half4(_Color.rgb,_Color.a*i.color.a);}
   ENDHLSL
  }
 }
}
