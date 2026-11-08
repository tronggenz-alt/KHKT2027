Shader "Khkt/HD/ThuyTinhHD"
{
    Properties
    {
        _BaseColor ("Màu thủy tinh", Color) = (0.95, 0.98, 1.0, 0.12)
        _EdgeColor ("Màu mép và đáy", Color) = (0.55, 0.72, 0.85, 0.65)
        _F0 ("Hệ số phản xạ nhìn thẳng (F0)", Range(0.01, 0.15)) = 0.04
        _FresnelPower ("Độ dốc phản xạ mép", Range(1.0, 8.0)) = 4.2
        _RefractDistort ("Độ lệch khúc xạ", Range(0.0, 0.10)) = 0.028
        _Dispersion ("Tán sắc quang sai mép", Range(0.0, 0.02)) = 0.006
        _Smoothness ("Độ bóng bề mặt", Range(0.8, 1.0)) = 0.97
        _SpecIntensity ("Độ sắc điểm sáng", Range(0.5, 4.0)) = 2.2
        _Absorption ("Hấp thụ độ dày mép", Range(0.1, 4.0)) = 1.6
        _ShadowStrength ("Độ đậm bóng thủy tinh", Range(0.0, 0.6)) = 0.18
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+10" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EdgeColor;
            float _F0;
            float _FresnelPower;
            float _RefractDistort;
            float _Dispersion;
            float _Smoothness;
            float _SpecIntensity;
            float _Absorption;
            float _ShadowStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
            float4 screenPos  : TEXCOORD2;
            float2 uv         : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.screenPos = ComputeScreenPos(output.positionCS);
            output.uv = input.uv;
            return output;
        }

        float4 FragGlass(Varyings input, float faceSign)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            float3 N = normalize(input.normalWS) * faceSign;
            float3 V = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
            float NdotV = saturate(dot(N, V));

            // 1. Phản xạ Fresnel Schlick (F0 = 0.04 với IOR thủy tinh ~1.47)
            float fresnel = _F0 + (1.0 - _F0) * pow(1.0 - NdotV, _FresnelPower);

            // 2. Độ dày biểu kiến tại mép và đáy
            float edgeFactor = pow(1.0 - NdotV, 2.5);
            float thickness = saturate(edgeFactor * _Absorption);
            float4 glassTint = lerp(_BaseColor, _EdgeColor, thickness);

            // 3. Khúc xạ xuyên qua nền bằng _CameraOpaqueTexture kèm tán sắc nhẹ
            float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
            float3 normalVS = TransformWorldToViewDir(N, true);
            float2 distort = normalVS.xy * _RefractDistort * (0.4 + 0.6 * edgeFactor);

            float3 refractedScene;
            refractedScene.r = SampleSceneColor(screenUV - distort * (1.0 + _Dispersion)).r;
            refractedScene.g = SampleSceneColor(screenUV - distort).g;
            refractedScene.b = SampleSceneColor(screenUV - distort * (1.0 - _Dispersion)).b;

            // 4. Ánh sáng chính và Điểm sáng phản xạ Specular (GGX)
            Light mainLight = GetMainLight();
            float3 L = normalize(mainLight.direction);
            float3 H = normalize(L + V);
            float NdotH = saturate(dot(N, H));
            float NdotL = saturate(dot(N, L));

            // Specular lobe cực hẹp và sắc cho thủy tinh nhẵn bóng
            float roughness = max(1.0 - _Smoothness, 0.02);
            float m2 = roughness * roughness;
            float d = (NdotH * m2 - NdotH) * NdotH + 1.0;
            float D = m2 / (3.14159265 * d * d + 0.00001);
            float specTerm = D * _SpecIntensity * NdotL;
            float3 specColor = mainLight.color * specTerm;

            // 5. Phản chiếu môi trường từ Reflection Probe (Box projection)
            float3 R = reflect(-V, N);
            float3 envReflect = GlossyEnvironmentReflection(R, input.positionWS, roughness, 1.0);

            // 6. Tổng hợp màu kính: khúc xạ nền hấp thụ + phản chiếu môi trường Fresnel + điểm sáng đèn
            float3 transmitted = refractedScene * lerp(float3(1, 1, 1), glassTint.rgb, thickness * 0.85);
            float3 reflected = envReflect * fresnel + specColor;

            float alpha = saturate(fresnel + thickness * glassTint.a + 0.05);
            float3 finalRgb = lerp(transmitted, reflected, fresnel) + specColor;

            return float4(finalRgb, alpha);
        }
        ENDHLSL

        // Pass 1: Mặt trong của dụng cụ thủy tinh (Backface)
        Pass
        {
            Name "GlassBackFace"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack
            #pragma multi_compile_instancing

            float4 fragBack(Varyings input) : SV_Target
            {
                return FragGlass(input, -1.0) * 0.85;
            }
            ENDHLSL
        }

        // Pass 2: Mặt ngoài của dụng cụ thủy tinh (Frontface)
        Pass
        {
            Name "GlassFrontFace"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            #pragma multi_compile_instancing

            float4 fragFront(Varyings input) : SV_Target
            {
                return FragGlass(input, 1.0);
            }
            ENDHLSL
        }

        // Pass 3: Bóng đổ nhạt (Faint Translucent Shadow)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings vertShadow(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _MainLightPosition.xyz));
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 fragShadow(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Dithered pseudo-translucent shadow matrix (4x4 Bayer pattern)
                float2 pixel = input.screenPos.xy / max(input.screenPos.w, 0.0001) * _ScreenParams.xy;
                int2 p = int2(fmod(pixel, 4.0));
                float dither = (
                    p.y == 0 ? (p.x == 0 ? 0.0f : p.x == 1 ? 8.0f : p.x == 2 ? 2.0f : 10.0f) :
                    p.y == 1 ? (p.x == 0 ? 12.0f : p.x == 1 ? 4.0f : p.x == 2 ? 14.0f : 6.0f) :
                    p.y == 2 ? (p.x == 0 ? 3.0f : p.x == 1 ? 11.0f : p.x == 2 ? 1.0f : 9.0f) :
                               (p.x == 0 ? 15.0f : p.x == 1 ? 7.0f : p.x == 2 ? 13.0f : 5.0f)
                ) / 16.0f;

                // Chỉ đổ bóng khi vượt ngưỡng dither _ShadowStrength (~18% bóng nhạt)
                clip(_ShadowStrength - dither);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

