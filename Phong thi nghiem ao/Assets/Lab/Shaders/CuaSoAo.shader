// Cửa sổ ảo: tấm phẳng ngoài khung kính hiện cảnh ngoài trời đã chụp sẵn thành ảnh toàn cảnh (cubemap), thay cho việc vẽ
// thật hiên, sân, cây cối phía sau cửa sổ. Hướng lấy mẫu được hiệu chỉnh thị sai theo một khối hộp bao cảnh ngoài
// (giống đầu dò phản chiếu chiếu hộp): đi lại trong phòng thì cảnh ngoài vẫn dịch chuyển hợp lý chứ không như ảnh dán.
// Không đổ bóng (nắng vẫn lọt qua cửa sổ vào phòng), không cần ánh sáng (ảnh chụp đã có ánh sáng mặt trời thật).
Shader "Khkt/CuaSoAo"
{
    Properties
    {
        _Cube ("Cảnh ngoài (cubemap)", Cube) = "" {}
        _CapturePos ("Điểm chụp", Vector) = (0, 1.5, 0, 0)
        _BoxMin ("Khối hộp cảnh ngoài: góc nhỏ", Vector) = (-10, -0.2, 15, 0)
        _BoxMax ("Khối hộp cảnh ngoài: góc lớn", Vector) = (40, 12, 45, 0)
        _Tint ("Màu kính", Color) = (0.93, 0.97, 1, 1)
        _Exposure ("Độ sáng", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        ZWrite On

        Pass
        {
            Name "CuaSoAo"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Cube);
            SAMPLER(sampler_Cube);

            CBUFFER_START(UnityPerMaterial)
                float4 _CapturePos;
                float4 _BoxMin;
                float4 _BoxMax;
                half4 _Tint;
                float _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 v = normalize(i.positionWS - _WorldSpaceCameraPos);
                // tia nhìn đi tiếp ra khối hộp cảnh ngoài: lấy điểm chạm mặt hộp, hướng lấy mẫu tính từ điểm chụp
                float3 a = (_BoxMax.xyz - i.positionWS) / v;
                float3 b = (_BoxMin.xyz - i.positionWS) / v;
                float3 far3 = max(a, b);
                float t = min(min(far3.x, far3.y), far3.z);
                float3 dir = i.positionWS + v * t - _CapturePos.xyz;
                half3 c = SAMPLE_TEXTURECUBE_LOD(_Cube, sampler_Cube, dir, 0).rgb * _Exposure;
                c = lerp(c, c * _Tint.rgb, 0.6);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
