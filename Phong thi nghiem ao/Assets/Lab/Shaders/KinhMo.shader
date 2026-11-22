// Nền bảng của bộ giao diện VERA Instrument: lấy ảnh cảnh phía sau (Opaque Texture của URP), làm mờ, phủ màu xanh mực,
// vẽ lưới bản vẽ kỹ thuật rất nhạt (ô nhỏ 24 px, ô lớn 96 px), viền mảnh, ke góc và vạch nhấn ở mép trên.
// Ảnh sprite: kênh A = tấm vát góc, R = đường viền, G = ke góc, B = vạch nhấn mép trên.
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
        _AccentColor ("Màu nhấn (ke góc, vạch trên)", Color) = (0.2, 0.84, 1, 0.9)
        _Grid ("Độ đậm lưới bản vẽ", Range(0, 2)) = 1

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
            fixed4 _AccentColor;
            float _Dark;
            float _Blur;
            float _Grid;

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
                // lưới bản vẽ theo điểm ảnh màn hình: đứng yên khi bảng trượt, không rung khi phóng to thu nhỏ
                float2 gp = i.pos.xy;
                float2 minor = abs(frac(gp / 24.0) - 0.5) * 24.0;
                float2 major = abs(frac(gp / 96.0) - 0.5) * 96.0;
                float gMinor = 1.0 - saturate(min(minor.x, minor.y) - 11.2);
                float gMajor = 1.0 - saturate(min(major.x, major.y) - 47.0);
                col += float3(0.35, 0.62, 1.0) * (gMinor * 0.022 + gMajor * 0.035) * _Grid;
                col = lerp(col, _BorderColor.rgb, s.r * _BorderColor.a);              // viền mảnh
                col = lerp(col, _AccentColor.rgb, saturate(s.g + s.b) * _AccentColor.a);  // ke góc và vạch nhấn
                col *= i.color.rgb;
                return fixed4(col, s.a * i.color.a);
            }
            ENDCG
        }
    }
}
