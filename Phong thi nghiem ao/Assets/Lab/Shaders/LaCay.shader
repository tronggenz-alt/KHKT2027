Shader "Khkt/Bio/LaCay"
{
    Properties
    {
        _BaseMap ("Texture lá cây", 2D) = "white" {}
        _BumpMap ("Bản đồ gân lá (Normal)", 2D) = "bump" {}
        _BumpScale ("Độ sâu gân lá", Range(0.0, 2.0)) = 1.0
        _BaseColor ("Màu lá", Color) = (0.20, 0.36, 0.18, 1.0)
        _Translucency ("Độ xuyên sáng lá (SSS)", Range(0.0, 1.0)) = 0.5
        _Cutoff ("Ngưỡng Alpha Clip", Range(0.0, 1.0)) = 0.5
        _WindSpeed ("Tốc độ gió", Float) = 2.4
        _WindStrength ("Biên độ rung gió (m)", Float) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        LOD 200
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _BumpScale;
            float _Translucency;
            float _Cutoff;
            float _WindSpeed;
            float _WindStrength;
        CBUFFER_END

        void ApplyLeafWind(inout float3 posOS, float weight, float time)
        {
            float t = time * _WindSpeed;
            float swayX = sin(t + posOS.x * 3.0 + posOS.z * 2.0) * _WindStrength * weight;
            float swayY = cos(t * 1.3 + posOS.z * 2.5) * (_WindStrength * 0.35) * weight;
            float swayZ = sin(t * 0.8 + posOS.y * 3.0) * (_WindStrength * 0.50) * weight;
            posOS += float3(swayX, swayY, swayZ);
        }
        ENDHLSL

        // 1. Pass UniversalForward (kết xuất chính nhận PBR, bóng đổ và xuyên sáng)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float3 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                ApplyLeafWind(posOS, input.color.r, _Time.y);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.color = input.color.rgb;
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(texColor.a - _Cutoff);

                // Pháp tuyến 2 mặt: khi vẽ mặt sau, đảo chiều pháp tuyến hình học
                float3 geomNormalWS = normalize(input.normalWS);
                #if !defined(SHADER_API_GLES)
                if (!isFrontFace) geomNormalWS = -geomNormalWS;
                #endif

                // Tính toán Normal Map gân lá
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangent = cross(geomNormalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, geomNormalWS));
                normalWS = normalize(normalWS);

                // Bóng đổ thời gian thực
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadowAtten = mainLight.shadowAttenuation;

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction));

                // 1. Ánh sáng xuyên lá (Subsurface Scattering giả lập tự nhiên khi nhìn ngược nắng)
                float backLight = saturate(dot(-normalWS, mainLight.direction));
                float forwardScatter = saturate(dot(-viewDirWS, mainLight.direction));
                float sssAmount = (backLight * 0.65 + pow(forwardScatter, 3.0) * 0.35) * _Translucency;
                half3 sssColor = half3(0.55, 0.88, 0.20) * sssAmount * mainLight.color * (0.35 + 0.65 * shadowAtten);

                // 2. Độ bóng nhẹ của lớp sáp biểu bì (cuticle sheen)
                float3 halfVec = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfVec));
                float spec = pow(NdotH, 28.0) * 0.25 * shadowAtten;
                half3 specColor = spec * mainLight.color;

                // 3. Đa sắc độ lá: xanh non (ngọn lá) vs xanh thẫm (gốc) từ vertex color G, B
                half3 leafAlbedo = texColor.rgb;
                half3 youngTint = half3(1.15, 1.25, 0.85);
                half3 matureTint = half3(0.85, 0.95, 0.80);
                leafAlbedo *= lerp(matureTint, youngTint, input.color.g);
                leafAlbedo *= (0.92 + input.color.b * 0.16);

                // 4. Kết hợp ánh sáng trực tiếp, xuyên sáng và môi trường
                float3 directLight = NdotL * mainLight.color * shadowAtten + sssColor;
                float3 ambient = SampleSH(normalWS) * 0.45 + half3(0.10, 0.14, 0.08);

                half3 finalRGB = leafAlbedo * (directLight + ambient) + specColor;
                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // 2. Pass ShadowCaster (đổ bóng lá cây)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                ApplyLeafWind(posOS, input.color.r, _Time.y);

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
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

        // 3. Pass DepthOnly (ghi chiều sâu cho SSAO và Depth Texture)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                ApplyLeafWind(posOS, input.color.r, _Time.y);

                output.positionCS = TransformObjectToHClip(posOS);
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

        // 4. Pass DepthNormals (phục vụ SSAO và hiệu ứng pháp tuyến bề mặt)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                ApplyLeafWind(posOS, input.color.r, _Time.y);

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(texColor.a - _Cutoff);

                float3 nWS = NormalizeNormalPerPixel(input.normalWS);
                #if !defined(SHADER_API_GLES)
                if (!isFrontFace) nWS = -nWS;
                #endif
                return half4(nWS, 0.0);
            }
            ENDHLSL
        }
    }
}
