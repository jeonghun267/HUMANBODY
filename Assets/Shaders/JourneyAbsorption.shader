Shader "인체탐험/흡수 입자"
{
    Properties
    {
        _BaseColor ("기본 색상", Color) = (1,1,1,1)
        _Ring ("파문 모양", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float4 _BaseColor;
            float _Ring;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _BaseColor;
                o.uv = v.uv;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv * 2.0 - 1.0;
                float r = length(uv);
                float disk = pow(saturate(1.0-r), 1.6);
                float ring = exp(-pow((r-0.67)*16.0, 2.0));
                float alpha = lerp(disk, ring, _Ring) * i.color.a;
                clip(alpha - 0.002);
                return half4(i.color.rgb * 1.15, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

