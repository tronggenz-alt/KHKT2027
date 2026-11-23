Shader "Khkt/DaNguoi"
{
    Properties
    {
        _BaseColor ("Màu da chính", Color) = (0.87, 0.69, 0.59, 1.0)
        _SubsurfaceColor ("Màu tán xạ dưới da (SSS)", Color) = (0.92, 0.30, 0.20, 1.0)
        _RimColor ("Màu viền sáng ấm", Color) = (0.98, 0.75, 0.62, 1.0)
        _Wrap ("Độ bọc sáng (Wrap)", Range(0.0, 1.0)) = 0.45
        _SssStrength ("Cường độ tán xạ dưới da", Range(0.0, 1.0)) = 0.42
        _RimStrength ("Cường độ viền ấm", Range(0.0, 1.0)) = 0.22
        _Smoothness ("Độ mịn da", Range(0.0, 1.0)) = 0.26
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SubsurfaceColor;
                float4 _RimColor;
                float _Wrap;
                float _SssStrength;
                float _RimStrength;
                float _Smoothness;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = nrmInputs.normalWS;
                return output;
            }

            half3 EvalSkinLight(Light light, half3 normalWS, half3 viewDirWS)
            {
                half ndotl = dot(normalWS, light.direction);
                half wrapDiffuse = saturate((ndotl + _Wrap) / (1.0h + _Wrap));
                half atten = light.distanceAttenuation * light.shadowAttenuation;

                // Dải tán xạ dưới da tại vùng chuyển sáng-tối để da không bị xám chết
                half termBand = exp(-ndotl * ndotl * 4.5h) * (0.45h + 0.55h * wrapDiffuse);
                half backGlow = pow(saturate(dot(viewDirWS, -light.direction)), 2.5h) * 0.28h;
                half3 sss = _SubsurfaceColor.rgb * (_SssStrength * (termBand + backGlow));

                // Phản xạ dầu/da rất nhẹ thay cho bóng nhựa
                half3 halfVec = normalize(light.direction + viewDirWS);
                half specExp = lerp(8.0h, 48.0h, _Smoothness);
                half spec = pow(saturate(dot(normalWS, halfVec)), specExp) * (0.04h + 0.08h * _Smoothness);

                half3 radiance = light.color * atten;
                return (_BaseColor.rgb * wrapDiffuse + sss) * radiance + radiance * spec;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 color = EvalSkinLight(mainLight, normalWS, viewDirWS);

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0; i < count; i++)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    color += EvalSkinLight(addLight, normalWS, viewDirWS);
                }
                #endif

                // Ánh sáng môi trường + tán xạ ấm nhẹ mặt khuất
                half3 sh = SampleSH(normalWS);
                half3 ambient = _BaseColor.rgb * sh + _SubsurfaceColor.rgb * (_SssStrength * 0.12h);

                // Viền sáng ấm (Fresnel rim) làm nổi khối ngón tay và gò má
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.2h);
                half3 rim = _RimColor.rgb * (fresnel * _RimStrength);

                return half4(color + ambient + rim, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
