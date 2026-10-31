Shader "Khkt/Bio/TieuBan"
{
    Properties
    {
        _MainTex ("Ảnh tiêu bản (H&E / Vi mô)", 2D) = "white" {}
        _FocusOffset ("Khoảng lệch tiêu cự (µm)", Float) = 0.0
        _DOF ("Độ sâu trường ảnh DOF (µm)", Float) = 4.0
        _DiaphragmBrightness ("Độ sáng màn chắn (0..1.5)", Float) = 1.0
        _CircleRadius ("Bán kính thị trường", Range(0.2, 0.5)) = 0.48
        _BorderSoftness ("Độ mềm viền đen", Range(0.001, 0.08)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Cull Back

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FocusOffset;
                float _DOF;
                float _DiaphragmBrightness;
                float _CircleRadius;
                float _BorderSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 center = input.uv - 0.5;
                float dist = length(center);

                // Viền tròn thị kính (vignette đen)
                float inCircle = 1.0 - smoothstep(_CircleRadius - _BorderSoftness, _CircleRadius, dist);
                if (inCircle <= 0.001) return half4(0.02, 0.02, 0.02, 1.0);

                // Tính toán độ mờ dựa trên tỷ số lệch tiêu cự / DOF
                float blurRatio = abs(_FocusOffset) / max(_DOF * 0.5, 0.1);
                float lodLevel = saturate(blurRatio * 0.25) * 5.0; // Sử dụng mipmap level 0..5

                // Lấy mẫu với mipmap bias / lod
                half4 col = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv, lodLevel);

                // Áp dụng độ sáng màn chắn sáng (tụ quang)
                col.rgb *= _DiaphragmBrightness;

                // Hòa trộn với viền tròn đen
                col.rgb = lerp(half3(0.02, 0.02, 0.02), col.rgb, inCircle);

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
