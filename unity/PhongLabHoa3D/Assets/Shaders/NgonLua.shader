// Ngọn lửa thật (URP, không sáng tạo thêm đèn): lưới giọt nước + nhiễu cuộn theo thời gian.
//  • lõi xanh nhạt → vỏ cam (hai màu chỉnh được: màu vỏ đổi theo ion khi thử ngọn lửa)
//  • đỉnh ngọn lửa chao đảo, bị "xé" theo nhiễu, mờ dần; mép mờ theo góc nhìn (fresnel) nên không thấy viền cứng
//  • cộng màu (additive) → kết hợp với Bloom của hậu kỳ cho vầng sáng
// Phải được một vật liệu trong Resources tham chiếu (Resources/PTN/NgonLua.mat) để shader có mặt trong bản build.
Shader "PhongLab/NgonLua"
{
    Properties
    {
        _MauTrong ("Mau loi", Color) = (0.45, 0.65, 1, 1)
        _MauNgoai ("Mau vo", Color) = (1, 0.52, 0.1, 1)
        _CuongDo ("Cuong do", Float) = 2.6
        _DoCao ("Do cao (m)", Float) = 0.06
        _LacLu ("Do chao dao", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "NgonLua"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MauTrong;
                half4 _MauNgoai;
                float _CuongDo;
                float _DoCao;
                float _LacLu;
            CBUFFER_END

            struct A { float4 p : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float3 nW : TEXCOORD1; float3 vW : TEXCOORD2; };

            float Bam(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Nhieu(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Bam(i), Bam(i + float2(1, 0)), f.x), lerp(Bam(i + float2(0, 1)), Bam(i + float2(1, 1)), f.x), f.y);
            }

            V vert(A i)
            {
                V o;
                float t = _Time.y;
                float h = i.uv.y;                                      // 0 = chân ngọn lửa, 1 = chóp
                float3 pos = i.p.xyz;
                float luc = h * h * _LacLu * _DoCao * 0.35;
                pos.x += (Nhieu(float2(t * 3.1, h * 2.3)) - 0.5) * 2.0 * luc;
                pos.z += (Nhieu(float2(t * 2.7 + 9.0, h * 2.0)) - 0.5) * 2.0 * luc;
                float3 pW = TransformObjectToWorld(pos);
                o.p = TransformWorldToHClip(pW);
                o.uv = i.uv;
                o.nW = TransformObjectToWorldNormal(i.n);
                o.vW = GetWorldSpaceViewDir(pW);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float h = i.uv.y;
                float n1 = Nhieu(float2(i.uv.x * 8.0, h * 6.0 - t * 4.5));
                float n2 = Nhieu(float2(i.uv.x * 17.0 + 3.0, h * 11.0 - t * 7.5));
                float nhieu = n1 * 0.65 + n2 * 0.35;
                float fres = pow(saturate(abs(dot(normalize(i.nW), normalize(i.vW)))), 1.25);
                float tron = smoothstep(0.0, 0.78, h + (nhieu - 0.5) * 0.38);
                half3 mau = lerp(_MauTrong.rgb, _MauNgoai.rgb, tron);
                float alpha = fres * (1.0 - pow(h, 1.7));
                alpha *= smoothstep(0.0, 0.10, h);                     // mờ ở chân
                alpha *= saturate(nhieu * 1.7 - h * 0.6 + 0.3);        // ngọn bị xé theo nhiễu
                return half4(mau * _CuongDo, saturate(alpha) * 0.85);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
