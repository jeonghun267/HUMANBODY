Shader "URP/Water_Shader" {
    Properties {
        _DeepColor ("Deep Water Color", Color) = (0.0, 0.2, 0.4, 1)
        _ShallowColor ("Shallow Water Color", Color) = (0.2, 0.5, 0.7, 1)
        _FresnelPower ("Fresnel Power", Range(0.1, 8.0)) = 3.0
        _NormalMap ("Normal Map (Water)", 2D) = "bump" {}
        _NormalSpeed ("Normal Scroll Speed", Vector) = (0.1, 0.1, 0, 0)
        _NormalStrength ("Normal Strength", Range(0, 1)) = 1.0
        _WaterAlpha ("Water Transparency", Range(0.0, 1.0)) = 0.8
    }

    SubShader {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles
            #pragma target 3.5
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _DeepColor;
            float4 _ShallowColor;
            float _FresnelPower;
            float _NormalStrength;
            float4 _NormalMap_ST;
            float4 _NormalSpeed;
            float _WaterAlpha;
            CBUFFER_END

            TEXTURE2D(_NormalMap); 
            SAMPLER(sampler_NormalMap);

            Varyings UnlitPassVertex (Attributes input) {
                Varyings output = (Varyings)0;
                
                // Unity 렌더링 함수를 사용하여 정점 변환
                VertexPositionInputs vertexInput = Get=>VertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                // UV 및 노멀/탄젠트 공간 설정
                output.uv = TRANSFORM_TEX(input.uv, _NormalMap);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                
                return output;
            }

            float4 UnlitPassFragment (Varyings input) : SV_Target {
                
                // 1. 노멀 맵 애니메이션 및 적용
                float2 uv0 = input.uv + _Time.y * _NormalSpeed.xy;
                float3 normalSample = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv0), _NormalStrength);
                
                // 탄젠트 공간에서 월드 공간 노멀로 변환
                float3x3 TBN = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(normalSample, TBN));

                // 2. 뷰 방향 및 프레넬 계산
                float3 viewDirWS = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // 3. 색상 보간 (프레넬을 사용하여 깊은 색상과 얕은 색상 보간)
                // 0: 깊은 곳 (_DeepColor), 1: 얕은 곳 (_ShallowColor)
                float3 finalColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(fresnel));

                // 4. 최종 색상 출력
                return float4(finalColor, _WaterAlpha);
            }
            ENDHLSL
        }
    }
}