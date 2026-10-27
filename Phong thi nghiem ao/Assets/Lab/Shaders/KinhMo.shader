// Nền card kiểu kính mờ (glassmorphism) cho giao diện: lấy ảnh cảnh phía sau (Opaque Texture của URP),
// làm mờ, phủ một lớp màu trắng trong, thêm viền sáng mảnh và ánh sáng nhẹ ở mép trên.
// Ảnh sprite: kênh A = hình chữ nhật bo góc, kênh R = đường viền.
// Cần camera bật Opaque Texture (ModernUI tự bật) và Canvas ở chế độ Screen Space - Camera.
Shader "Khkt/KinhMo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Tint ("Lớp phủ", Color) = (0.92, 0.95, 1, 0.16)
        _Dark ("Làm tối nền", Range(0, 1)) = 0.35
        _BorderColor ("Viền", Color) = (1, 1, 1, 0.55)
        _Blur ("Độ mờ (pixel)", Float) = 14

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize;
            fixed4 _Tint;
            fixed4 _BorderColor;
            float _Dark;
            float _Blur;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 s = tex2D(_MainTex, i.uv);
                float2 suv = i.screen.xy / i.screen.w;
                float2 px = _CameraOpaqueTexture_TexelSize.xy * _Blur;
                // Làm mờ kiểu đĩa: 1 tâm + 2 vòng x 8 điểm
                float3 sum = tex2D(_CameraOpaqueTexture, suv).rgb;
                float w = 1;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785398 + 0.39;
                    float2 d = float2(cos(a), sin(a));
                    sum += tex2D(_CameraOpaqueTexture, suv + d * px * 0.5).rgb * 0.8;
                    sum += tex2D(_CameraOpaqueTexture, suv + d * px).rgb * 0.5;
                    w += 1.3;
                }
                float3 bg = sum / w;
                bg *= (1 - _Dark);
                float3 col = lerp(bg, _Tint.rgb, _Tint.a);
                col += smoothstep(0.55, 1.0, i.uv.y) * 0.035;             // ánh sáng nhẹ mép trên
                col = lerp(col, _BorderColor.rgb, s.r * _BorderColor.a);  // viền mảnh
                col *= i.color.rgb;
                return fixed4(col, s.a * i.color.a);
            }
            ENDCG
        }
    }
}
