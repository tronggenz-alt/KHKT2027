Shader "Khkt/Bio/LaCay"
{
    Properties
    {
        _BaseMap ("Texture lá cây", 2D) = "white" {}
        _BaseColor ("Màu lá", Color) = (0.28, 0.55, 0.22, 1.0)
        _Cutoff ("Ngưỡng Alpha Clip", Range(0.0, 1.0)) = 0.5
        _WindSpeed ("Tốc độ gió", Float) = 2.4
        _WindStrength ("Biên độ rung gió (m)", Float) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        LOD 200
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _WindSpeed;
                float _WindStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                // Trọng số uốn theo màu đỉnh (vertex color r = 1 ở ngọn lá, 0 ở cuống)
                float weight = input.color.r;
                float time = _Time.y * _WindSpeed;
                float swayX = sin(time + posOS.x * 3.0 + posOS.z * 2.0) * _WindStrength * weight;
                float swayY = cos(time * 1.3 + posOS.z * 2.5) * (_WindStrength * 0.4) * weight;
                float swayZ = sin(time * 0.8 + posOS.y * 3.0) * (_WindStrength * 0.6) * weight;

                posOS += float3(swayX, swayY, swayZ);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.color = input.color.rgb;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(texColor.a - _Cutoff);

                // Ánh sáng diffuse đơn giản và ánh sáng môi trường
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                // Hai mặt lá: cho phép ánh sáng xuyên qua (subsurface scattering giả lập)
                float backLight = saturate(dot(-normalWS, mainLight.direction)) * 0.35;
                float3 lighting = (NdotL + backLight) * mainLight.color + half3(0.25, 0.28, 0.24);

                return half4(texColor.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _WindSpeed;
                float _WindStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float weight = input.color.r;
                float time = _Time.y * _WindSpeed;
                posOS.x += sin(time + posOS.x * 3.0) * _WindStrength * weight;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
