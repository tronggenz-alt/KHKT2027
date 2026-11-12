Shader "Khkt/Tang2/HoloReveal"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _DoCaoCat ("Độ cao cắt (Thế giới)", Float) = 1000.0
        _EdgeColor ("Màu mép phát sáng", Color) = (0.133, 0.827, 0.933, 1.0) // #22D3EE
        _EdgeWidth ("Độ dày mép sáng (m)", Float) = 0.03
        _Smoothness ("Độ bóng", Range(0, 1)) = 0.5
        _Metallic ("Độ kim loại", Range(0, 1)) = 0.0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _DoCaoCat)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EdgeColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _EdgeWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float doCaoCat = UNITY_ACCESS_INSTANCED_PROP(Props, _DoCaoCat);
                float edgeWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _EdgeWidth);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 edgeColor = UNITY_ACCESS_INSTANCED_PROP(Props, _EdgeColor);

                float diff = doCaoCat - input.positionWS.y;
                if (diff < 0.0)
                {
                    discard;
                }

                float noise = Hash21(input.positionWS.xz * 45.0) * 0.008;
                float dynamicEdge = edgeWidth + noise;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * baseColor;

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(input.normalWS, mainLight.direction));
                half3 litColor = texColor.rgb * (mainLight.color * NdotL + 0.35);

                if (diff < dynamicEdge)
                {
                    float edgeFactor = 1.0 - (diff / dynamicEdge);
                    half3 glow = edgeColor.rgb * (edgeFactor * 2.2 + 0.8);
                    return half4(glow, 1.0);
                }

                return half4(litColor, texColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
