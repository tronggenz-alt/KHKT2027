Shader "Khkt/Bio/MatCatGiaiPhau"
{
    Properties
    {
        _BaseColor ("Màu cơ bản", Color) = (0.88, 0.85, 0.78, 1.0)
        _CrossSectionColor ("Màu mặt cắt", Color) = (0.72, 0.24, 0.28, 1.0)
        _RimColor ("Màu viền chọn", Color) = (0.22, 0.85, 1.0, 1.0)
        _EmissionColor ("Màu tự phát sáng", Color) = (0, 0, 0, 1)
        _Highlight ("Cường độ chọn (0..1)", Range(0.0, 1.0)) = 0.0
        _Opacity ("Độ đục (0..1)", Range(0.0, 1.0)) = 1.0
        _Smoothness ("Độ bóng (0..1)", Range(0.0, 1.0)) = 0.35
        _WaveAmp ("Biên độ sóng nhu động", Float) = 0.0
        _WaveFreq ("Tần số sóng nhu động", Float) = 18.0
        _WaveSpeed ("Tốc độ sóng nhu động", Float) = 2.4
        _SliceEnabled ("Bật mặt cắt (0/1)", Float) = 0.0
        _SlicePlane ("Mặt phẳng cắt (nx, ny, nz, offset)", Vector) = (1, 0, 0, 0)
        _StageOrigin ("Tâm sân khấu giải phẫu", Vector) = (0, -520, 0, 0)
        _SrcBlend ("SrcBlend", Float) = 1.0
        _DstBlend ("DstBlend", Float) = 0.0
        _ZWrite ("ZWrite", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CrossSectionColor;
                half4 _RimColor;
                half4 _EmissionColor;
                float _Highlight;
                float _Opacity;
                float _Smoothness;
                float _WaveAmp;
                float _WaveFreq;
                float _WaveSpeed;
                float _SliceEnabled;
                float4 _SlicePlane;
                float4 _StageOrigin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posOS = input.positionOS.xyz;
                if (_WaveAmp > 0.0001)
                {
                    float phase = posOS.y * _WaveFreq + posOS.x * (_WaveFreq * 0.65) - _Time.y * _WaveSpeed;
                    float radial = sin(phase) * _WaveAmp;
                    posOS += input.normalOS * radial;
                }
                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 relPos = i.positionWS - _StageOrigin.xyz;
                if (_SliceEnabled > 0.5)
                {
                    float dist = dot(relPos, _SlicePlane.xyz) - _SlicePlane.w;
                    clip(dist);
                }

                bool isFront = IS_FRONT_VFACE(facing, true, false);
                float3 nWS = normalize(isFront ? i.normalWS : -i.normalWS);
                float3 vWS = normalize(GetCameraPositionWS() - i.positionWS);

                Light mainLight = GetMainLight();
                float ndl = dot(nWS, normalize(mainLight.direction));
                float wrap = saturate((ndl + 0.55) / 1.55);

                // Đèn phụ dịu từ dưới/trái để giữ khối giải phẫu rõ cả vùng khuất
                float3 fillDir = normalize(float3(-0.6, -0.35, -0.7));
                float fill = saturate(dot(nWS, fillDir) * 0.5 + 0.5) * 0.25;

                float3 hVec = normalize(normalize(mainLight.direction) + vWS);
                float specPow = lerp(12.0, 64.0, _Smoothness);
                float spec = pow(saturate(dot(nWS, hVec)), specPow) * (0.12 + 0.38 * _Smoothness);

                float fresnel = pow(1.0 - saturate(dot(nWS, vWS)), 2.6);

                half3 surfaceCol;
                if (!isFront && _SliceEnabled > 0.5)
                {
                    // Mặt cắt giải phẫu: hiện cấu trúc vân mô đậm nhạt theo tọa độ không gian
                    float ring = sin(length(relPos.xz) * 95.0) * 0.5 + 0.5;
                    float band = lerp(0.84, 1.08, ring);
                    float cutDist = abs(dot(relPos, _SlicePlane.xyz) - _SlicePlane.w);
                    float edgeBoost = saturate(1.0 - cutDist * 45.0) * 0.28;
                    surfaceCol = _CrossSectionColor.rgb * band * (0.68 + 0.32 * wrap) + _RimColor.rgb * edgeBoost;
                }
                else
                {
                    half3 lit = _BaseColor.rgb * (0.34 + 0.72 * wrap + fill) + mainLight.color * spec;
                    lit += _RimColor.rgb * (fresnel * (0.15 + 1.05 * _Highlight));
                    lit += _BaseColor.rgb * (_Highlight * 0.22) + _EmissionColor.rgb;
                    surfaceCol = lit;
                }

                return half4(surfaceCol, saturate(_Opacity * _BaseColor.a));
            }
            ENDHLSL
        }
    }
}
