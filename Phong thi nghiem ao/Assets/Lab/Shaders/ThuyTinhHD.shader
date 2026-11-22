// Thủy tinh phòng thí nghiệm (cốc, bình, ống nghiệm, đĩa petri, lam kính) – bản dựng lại 2026-11-13.
//
// Vì sao viết lại:
//   • Bản cũ lấy điểm sáng GGX với độ nhám 0,02 và không chặn trần: ở vài góc nhìn một điểm ảnh sáng gấp trăm lần
//     xung quanh, hậu kỳ Bloom nhân nó thành quầng chớp tắt khi xoay góc nhìn ("nháy theo góc").
//   • Bản cũ không khai báo từ khóa Forward+ nên lấy phản chiếu từ ô phản chiếu mặc định thay vì ô của phòng:
//     ảnh phản chiếu đổi đột ngột khi Unity gộp lô vẽ lại.
//   • Khúc xạ lấy từ ảnh nền (Opaque Texture) rung theo độ phân giải động và làm mất chất lỏng trong bình.
//   • Bóng đổ khử điểm theo tọa độ màn hình trong bản đồ bóng: bóng lấm tấm bò theo camera.
//
// Bản này: thủy tinh thành mỏng thuần Fresnel, vẽ mặt trong rồi mặt ngoài, trộn kiểu nhân sẵn (phản chiếu cộng thêm,
// thân kính gần như trong suốt), điểm sáng có khử răng cưa theo độ cong và có trần, không đổ bóng.
Shader "Khkt/HD/ThuyTinhHD"
{
    Properties
    {
        _BaseColor ("Màu thân kính (A = độ đục nhìn thẳng)", Color) = (0.92, 0.97, 1.0, 0.045)
        _EdgeColor ("Màu mép và đáy dày (A = độ đục ở mép)", Color) = (0.62, 0.80, 0.88, 0.42)
        _F0 ("Phản xạ nhìn thẳng (F0)", Range(0.01, 0.15)) = 0.04
        _FresnelPower ("Độ dốc phản xạ mép", Range(1.0, 8.0)) = 5.0
        _Smoothness ("Độ bóng bề mặt", Range(0.5, 1.0)) = 0.93
        _SpecIntensity ("Cường độ điểm sáng đèn", Range(0.0, 3.0)) = 1.0
        _SpecMax ("Trần điểm sáng (chống chớp)", Range(0.5, 6.0)) = 2.2
        _Absorption ("Độ dày biểu kiến ở mép", Range(0.1, 4.0)) = 1.5
        _ReflectStrength ("Cường độ phản chiếu phòng", Range(0.0, 2.0)) = 1.0
        _BackFade ("Độ nhạt mặt trong", Range(0.0, 1.0)) = 0.55
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
        #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
        #pragma multi_compile _ _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
        #pragma multi_compile_fog
        #pragma multi_compile_instancing

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EdgeColor;
            half _F0;
            half _FresnelPower;
            half _Smoothness;
            half _SpecIntensity;
            half _SpecMax;
            half _Absorption;
            half _ReflectStrength;
            half _BackFade;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
            half   fogFactor  : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        // Điểm sáng GGX đã nới theo độ cong bề mặt và có trần: không còn điểm ảnh sáng đột biến
        half SpecularLobe(half3 N, half3 V, half3 L, half roughness2)
        {
            half3 H = SafeNormalize(L + V);
            half NdotH = saturate(dot(N, H));
            half NdotL = saturate(dot(N, L));
            half d = NdotH * NdotH * (roughness2 - 1.0h) + 1.0h;
            half D = roughness2 / (PI * d * d + 1e-4h);
            return min(D * NdotL * 0.25h, _SpecMax);
        }

        half4 FragGlass(Varyings input, half faceSign)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float3 nRaw = normalize(input.normalWS);
            half3 N = nRaw * faceSign;
            half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
            half NdotV = saturate(dot(N, V));

            // Khử răng cưa điểm sáng: bề mặt cong nhiều trong một điểm ảnh thì coi như nhám hơn (Kaplanyan 2016)
            float3 dndx = ddx(nRaw), dndy = ddy(nRaw);
            half variance = min(0.5h * (dot(dndx, dndx) + dot(dndy, dndy)), 0.18h);
            half perceptualRoughness = max(1.0h - _Smoothness, 0.07h);
            half roughness = perceptualRoughness * perceptualRoughness;
            half roughness2 = saturate(roughness * roughness + variance);
            half probeRoughness = saturate(perceptualRoughness + variance * 1.5h);

            half fresnel = _F0 + (1.0h - _F0) * pow(1.0h - NdotV, _FresnelPower);

            // Thành kính nhìn xiên thì dày hơn: đậm màu ở mép và đáy, giữa thân gần như trong suốt
            half thickness = saturate(pow(1.0h - NdotV, 2.2h) * _Absorption);
            half3 tint = lerp(_BaseColor.rgb, _EdgeColor.rgb, thickness);
            half bodyAlpha = lerp(_BaseColor.a, _EdgeColor.a, thickness);

            float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

            // Phản chiếu phòng từ ô phản chiếu (đúng cả Forward+ và chiếu hộp)
            half3 R = reflect(-V, N);
            half3 env = GlossyEnvironmentReflection(R, input.positionWS, probeRoughness, 1.0h, screenUV) * _ReflectStrength;

            // Ánh sáng khuếch tán môi trường cho thân kính (để kính không đen ở phòng tối, không chói ở phòng sáng)
            half3 ambient = SampleSH(N);

            // Điểm sáng từ đèn chính và đèn trần
            Light mainLight = GetMainLight();
            half3 spec = mainLight.color * SpecularLobe(N, V, mainLight.direction, roughness2);

        #if defined(_ADDITIONAL_LIGHTS)
            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = N;
            inputData.viewDirectionWS = V;
            inputData.normalizedScreenSpaceUV = screenUV;
            uint pixelLightCount = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(pixelLightCount)
                Light light = GetAdditionalLight(lightIndex, input.positionWS);
                spec += light.color * (light.distanceAttenuation * SpecularLobe(N, V, light.direction, roughness2));
            LIGHT_LOOP_END
        #endif
            spec = min(spec * _SpecIntensity, _SpecMax);

            // Trộn nhân sẵn: thân kính che nền theo bodyAlpha, phản chiếu và điểm sáng cộng thêm lên trên
            half3 body = tint * (ambient * 0.55h + 0.12h) * bodyAlpha;
            half3 color = body + env * fresnel + spec * fresnel * 6.0h;
            half alpha = saturate(bodyAlpha + fresnel * 0.85h);

            color = MixFog(color, input.fogFactor);
            return half4(color, alpha);
        }
        ENDHLSL

        // Lượt 1: mặt trong (nhìn xuyên qua thành trước thấy thành sau), nhạt hơn mặt ngoài
        Pass
        {
            Name "GlassBackFace"
            // URP vẽ các lượt theo thứ tự thẻ: UniversalForward trước, UniversalForwardOnly sau. Hai lượt phải khác thẻ,
            // cùng thẻ thì URP chỉ vẽ lượt đầu (bản cũ vì thế chỉ vẽ mặt trong).
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack

            half4 fragBack(Varyings input) : SV_Target
            {
                return FragGlass(input, -1.0h) * _BackFade;
            }
            ENDHLSL
        }

        // Lượt 2: mặt ngoài
        Pass
        {
            Name "GlassFrontFace"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront

            half4 fragFront(Varyings input) : SV_Target
            {
                return FragGlass(input, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
