Shader "Khkt/Tang2/HoloBeam"
{
    Properties
    {
        _BeamColor ("Màu chùm sáng", Color) = (0.133, 0.827, 0.933, 1.0) // #22D3EE
        _HeightFade ("Độ mờ chiều cao", Float) = 2.2
        _WaveSpeed ("Tốc độ sóng cuộn", Float) = 3.0
        _WaveFreq ("Tần số sóng", Float) = 40.0
        _BaseIntensity ("Cường độ đáy", Float) = 0.22
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UnlitAdditiveBeam"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Lighting Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float4 _BeamColor;
            float _HeightFade;
            float _WaveSpeed;
            float _WaveFreq;
            float _BaseIntensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float v = saturate(input.uv.y);
                float t = _Time.y;

                float decay = pow(saturate(1.0 - v), _HeightFade) * _BaseIntensity;
                float wave = sin(v * _WaveFreq - t * _WaveSpeed) * 0.065;
                float waveGlow = saturate(decay + wave);

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float rim = pow(1.0 - abs(dot(input.normalWS, viewDir)), 2.5) * 0.35;

                float finalAlpha = saturate((waveGlow + rim) * _BeamColor.a);
                half3 col = _BeamColor.rgb * finalAlpha;

                return half4(col, finalAlpha);
            }
            ENDHLSL
        }
    }
}
