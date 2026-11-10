Shader "인체탐험/환경 배경"
{
    Properties
    {
        _Mode ("환경 유형", Float) = 1
        _BaseColor ("기본 색상", Color) = (0.44, 0.12, 0.13, 1)
        _LightColor ("밝은 부분 색상", Color) = (0.88, 0.47, 0.42, 1)
        _DeepColor ("깊은 부분 색상", Color) = (0.15, 0.03, 0.06, 1)
        _AccentColor ("강조 색상", Color) = (0.48, 0.85, 0.86, 1)
        _FoldCount ("큰 주름 개수", Range(2, 24)) = 10
        _FoldStrength ("주름 깊이", Range(0, 1)) = 0.55
        _RingStrength ("가로 주름 강도", Range(0, 1)) = 0.15
        _Detail ("표면 결 강도", Range(0, 1)) = 0.2
        _Tunnel ("관 모양 깊이", Range(0, 1)) = 0.5
        _Exposure ("밝기", Range(0.1, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            #include "JourneyEnvironmentShared.hlsl"
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return JourneyEnvironmentColor(i.direction);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
