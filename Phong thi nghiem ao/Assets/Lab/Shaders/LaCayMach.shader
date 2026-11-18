Shader "Khkt/Bio/LaCayMach"
{
    Properties
    {
        _BaseColor ("Màu nền ống mạch", Color) = (0.14, 0.45, 0.85, 0.45)
        _PulseColor ("Màu xung dòng chảy", Color) = (0.55, 0.90, 1.0, 0.95)
        _FlowSpeed ("Tốc độ cuộn dòng (+ lên / - xuống)", Float) = 1.2
        _PulseDensity ("Mật độ xung dọc mạch", Float) = 6.0
        _FlowStrength ("Cường độ dòng (0..1)", Range(0.0, 1.5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PulseColor;
                float _FlowSpeed;
                float _PulseDensity;
                float _FlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = normalize(GetCameraPositionWS() - vpi.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Cuộn dọc trục V của trụ/ống mạch
                float wave = sin((i.uv.y * _PulseDensity - _Time.y * _FlowSpeed) * 6.2831853);
                float streak = sin((i.uv.x * 3.0 + i.uv.y * _PulseDensity * 0.5 - _Time.y * _FlowSpeed * 0.8) * 6.2831853);
                float pulse = saturate(wave * 0.5 + 0.5);
                pulse = pow(pulse, 3.0) * (0.65 + 0.35 * saturate(streak * 0.5 + 0.5));
                pulse *= saturate(_FlowStrength);

                float fresnel = pow(1.0 - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewDirWS)))), 1.6);
                half3 col = lerp(_BaseColor.rgb, _PulseColor.rgb, saturate(pulse + fresnel * 0.25));
                half alpha = saturate(_BaseColor.a + pulse * _PulseColor.a * 0.65 + fresnel * 0.12);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
