Shader "Hidden/Khkt/KinhLongThapMo"
{
    Properties
    {
        _KL_BlurStep ("Blur Step", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _KL_BlurStep;

        half4 SampleSrc(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv));
        }

        // Pass 0: Copy full-resolution (không làm mờ)
        half4 FragCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleSrc(input.texcoord);
        }

        // Pass 1: Dual Kawase Downsample 5-tap (lọc chuẩn nửa texel, sạch răng cưa)
        half4 FragDownsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 hp = _BlitTexture_TexelSize.xy * _KL_BlurStep;

            half4 c = SampleSrc(uv) * 4.0h;
            c += SampleSrc(uv + float2(-hp.x, -hp.y));
            c += SampleSrc(uv + float2( hp.x, -hp.y));
            c += SampleSrc(uv + float2(-hp.x,  hp.y));
            c += SampleSrc(uv + float2( hp.x,  hp.y));
            return c * (1.0h / 8.0h);
        }

        // Pass 2: Dual Kawase Upsample 8-tap Tent (mịn đều như Gaussian, không sọc kim cương)
        half4 FragUpsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 hp = _BlitTexture_TexelSize.xy * _KL_BlurStep;

            half4 c = SampleSrc(uv + float2(-hp.x * 2.0, 0.0));
            c += SampleSrc(uv + float2( hp.x * 2.0, 0.0));
            c += SampleSrc(uv + float2(0.0, -hp.y * 2.0));
            c += SampleSrc(uv + float2(0.0,  hp.y * 2.0));

            c += SampleSrc(uv + float2(-hp.x, -hp.y)) * 2.0h;
            c += SampleSrc(uv + float2( hp.x, -hp.y)) * 2.0h;
            c += SampleSrc(uv + float2(-hp.x,  hp.y)) * 2.0h;
            c += SampleSrc(uv + float2( hp.x,  hp.y)) * 2.0h;

            return c * (1.0h / 12.0h);
        }
        ENDHLSL

        Pass
        {
            Name "KinhLongCopy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            ENDHLSL
        }

        Pass
        {
            Name "KinhLongDownsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            ENDHLSL
        }

        Pass
        {
            Name "KinhLongUpsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }
    }
}
