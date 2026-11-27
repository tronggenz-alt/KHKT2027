Shader "Khkt/BoGocAnh"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KichThuoc ("Element Size (px)", Vector) = (260, 150, 0, 0)
        _BanKinhBo ("Corner Radius (px)", Float) = 14.0
        _DoMuotGoc ("Corner Curvature", Range(2.0, 4.0)) = 3.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "BoGocAnh"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                float4 worldPos   : TEXCOORD2;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ClipRect;
                float4 _KichThuoc;
                float _BanKinhBo;
                float _DoMuotGoc;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.worldPos = input.positionOS;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.uv1 = input.uv1;
                o.color = input.color * _Color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.worldPos.xy) * step(input.worldPos.xy, _ClipRect.zw);
                if (inside.x * inside.y <= 0.001) discard;
                #endif

                float2 size = (input.uv1.x > 1.0 && input.uv1.y > 1.0) ? input.uv1.xy : _KichThuoc.xy;
                size = max(size, float2(2.0, 2.0));
                float2 halfSize = size * 0.5;
                float2 p = (input.uv - 0.5) * size;
                float r = clamp(_BanKinhBo, 1.0, min(halfSize.x, halfSize.y));

                float2 q = abs(p) - (halfSize - r);
                float2 qp = max(q, 0.0);
                float m = max(qp.x, qp.y);
                float outer = 0.0;
                if (m > 1e-5)
                {
                    float2 s = max(qp / m, 0.0);
                    outer = m * pow(pow(abs(s.x), _DoMuotGoc) + pow(abs(s.y), _DoMuotGoc), 1.0 / _DoMuotGoc);
                }
                float sd = outer + min(max(q.x, q.y), 0.0) - r;
                float aa = max(0.75, length(float2(ddx(sd), ddy(sd))));
                float mask = smoothstep(aa * 0.5, -aa * 0.5, sd);
                if (mask <= 0.001) discard;

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                col.a *= mask;
                return col;
            }
            ENDHLSL
        }
    }
}
