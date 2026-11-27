// Kính lỏng (Liquid Glass) cho mọi bảng của VERA Lab — viết lại 27/11/2026 theo vật liệu Liquid Glass của Apple (iOS 26 / macOS Tahoe).
// Tên shader giữ là "Khkt/KinhMo" để mọi màn hình đang gọi Shader.Find("Khkt/KinhMo") tự đổi theo.
//
// Lớp ánh sáng, từ dưới lên:
//   1. Khúc xạ mép (lensing): vành kính dày như thấu kính lồi, bẻ ảnh cảnh phía sau vào trong theo pháp tuyến mép.
//   2. Tán sắc ở mép: kênh đỏ và lam lệch nhau một chút, chỉ trên vành.
//   3. Làm mờ kiểu đĩa (2 vòng × 8 điểm) để chữ đọc được trên cảnh 3D.
//   4. Lớp phủ màu tự thích ứng: nền phía sau càng khác màu kính thì lớp phủ càng đặc, giữ tương phản chữ.
//   5. Ánh sáng: viền phản chiếu sáng phía hướng đèn, viền phụ mờ hơn ở phía đối diện, bóng trong mảnh cho độ dày,
//      ánh Fresnel dọc mép. Hướng đèn (_KL_Light) đi theo chuột trên Mac/Windows, theo độ nghiêng máy trên iPad (KinhLongAnhSang.cs).
//   6. Kênh alpha của màu đỉnh (Image.color.a, CanvasGroup.alpha) là mức "ngưng tụ": 0 = chưa hiện, 1 = kính đầy đủ.
//      Khi bảng hiện/ẩn, khúc xạ và vệt sáng lớn dần/tắt dần cùng độ đục, như giọt kính đọng lại.
//
// Ảnh sprite (GlassKit.Rounded, 9-slice): A = hình, R = độ sâu tính từ mép (0 ở mép → 1 sâu hơn 40 px),
// G, B = pháp tuyến mép hướng ra ngoài (mã hóa 0,5 + 0,5·n).
// Cần camera bật Opaque Texture (ModernUI tự bật) và Canvas ở chế độ Screen Space - Camera.
Shader "Khkt/KinhMo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Tint ("Lớp phủ", Color) = (0.92, 0.95, 1, 0.16)
        _Dark ("Làm tối nền", Range(0, 1)) = 0.35
        _BorderColor ("Viền", Color) = (1, 1, 1, 0.55)
        _Blur ("Độ mờ (pixel)", Float) = 12
        _AccentColor ("Màu ánh phản chiếu", Color) = (0.2, 0.84, 1, 0.9)
        _Grid ("Độ đậm lưới bản vẽ", Range(0, 2)) = 1
        _Refraction ("Khúc xạ mép (pixel)", Range(0, 80)) = 30
        _Chroma ("Tán sắc", Range(0, 2)) = 1
        _Specular ("Độ sáng viền phản chiếu", Range(0, 2)) = 1
        _Adaptive ("Tự thích ứng nền", Range(0, 1)) = 1
        _Vibrancy ("Độ rực màu nền", Range(0, 2.5)) = 1.45
        _EdgeClear ("Vành trong hơn lòng", Range(0, 1)) = 0.5
        _Reflect ("Phản chiếu cảnh ngoài mép", Range(0, 1)) = 0.35
        _VertTint ("Nhuộm theo màu Image (kính màu)", Range(0, 1)) = 0.72

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
            #pragma target 3.0
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
            float _Refraction;
            float _Chroma;
            float _Specular;
            float _Adaptive;
            float _Vibrancy;
            float _EdgeClear;
            float _Reflect;
            float _VertTint;
            // Màn ngoài lab không có cảnh 3D: KinhLongNen đặt hình nền toàn màn vào _KL_Nen và bật _KL_DungNen = 1,
            // kính khúc xạ hình nền đó thay cho ảnh camera.
            sampler2D _KL_Nen;
            float _KL_DungNen;
            float4 _KL_NenUV;   // xy: gốc, zw: cỡ vùng hình nền đang hiện (parallax)

            // Đặt toàn cục bởi KinhLongAnhSang: xy = hướng đèn (đơn vị, trục y lên), z = cường độ (1 = nghỉ, >1 khi đang rê/nghiêng), w = thời gian
            float4 _KL_Light;

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

            float3 Sharp(float2 uv)
            {
                float4 t = _KL_NenUV.z > 0.01 ? _KL_NenUV : float4(0, 0, 1, 1);
                return _KL_DungNen > 0.5 ? tex2Dlod(_KL_Nen, float4(t.xy + saturate(uv) * t.zw, 0, 0)).rgb
                                         : tex2Dlod(_CameraOpaqueTexture, float4(uv, 0, 0)).rgb;
            }

            float3 BlurAt(float2 suv, float2 px)
            {
                float3 sum = Sharp(suv);
                float w = 1;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785398 + 0.39;
                    float2 d = float2(cos(a), sin(a));
                    sum += Sharp(suv + d * px * 0.5) * 0.8;
                    sum += Sharp(suv + float2(-d.y, d.x) * px) * 0.5;
                    w += 1.3;
                }
                return sum / w;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 s = tex2D(_MainTex, i.uv);
                if (s.a <= 0.002) discard;

                float cond = saturate(i.color.a);                   // mức ngưng tụ
                float depth = s.r;                                   // 0 ở mép, 1 ở lòng kính
                float2 n = s.gb * 2.0 - 1.0;                         // pháp tuyến mép hướng ra ngoài
                float nl = length(n);
                n = nl > 1e-3 ? n / nl : float2(0, 0);

                float2 texel = 1.0 / max(_ScreenParams.xy, 1.0);
                float2 suv = i.screen.xy / i.screen.w;

                // 1. Khúc xạ: mặt cắt vành tròn h(x) = sqrt(1 - (1 - x)^2), độ lệch ~ độ dốc, chặn ở mép cho khỏi vô hạn
                float x = saturate(depth);
                float rim = 1.0 - x;
                float slope = rim * rsqrt(max(1.0 - rim * rim, 0.035));
                float bend = saturate(slope * 0.22) * cond;
                float2 shift = -n * bend * _Refraction * texel;
                float2 ruv = suv + shift;

                // 3. Mờ — vành kính ít mờ hơn lòng kính để nhìn rõ sự uốn cong
                float blurPx = _Blur * lerp(0.55, 1.0, smoothstep(0.0, 0.6, x));
                float3 bg = BlurAt(ruv, texel * blurPx);

                // 2. Tán sắc trên vành
                float ca = bend * _Chroma;
                if (ca > 0.01)
                {
                    float2 off = -n * ca * 6.0 * texel;
                    float r = Sharp(ruv + off).r;
                    float b = Sharp(ruv - off).b;
                    float wca = saturate(ca * 1.6) * 0.55;
                    bg.r = lerp(bg.r, r, wca);
                    bg.b = lerp(bg.b, b, wca);
                }

                // Độ rực (vibrancy): kính Apple làm màu phía sau đậm lên thay vì xám đi — đây là chỗ khác nhựa mờ
                const float3 LUM = float3(0.2126, 0.7152, 0.0722);
                float lumRaw = dot(bg, LUM);
                bg = max(lerp(lumRaw.xxx, bg, _Vibrancy), 0.0);
                bg *= (1.0 - _Dark * lerp(1.0 - _EdgeClear * 0.6, 1.0, smoothstep(0.0, 0.5, x)));

                // Phản chiếu: vành kính bắt màu của cảnh ngay ngoài mép (như kính dày soi lại xung quanh)
                float fres = pow(rim, 3.0);
                if (_Reflect > 0.001)
                {
                    float3 outside = Sharp(suv + n * 22.0 * texel) * 0.6 + Sharp(suv + n * 44.0 * texel) * 0.4;
                    outside = max(lerp(dot(outside, LUM).xxx, outside, _Vibrancy), 0.0);
                    bg = lerp(bg, outside, fres * _Reflect * cond);
                }

                // 4. Lớp phủ tự thích ứng — mỏng ở vành (kính trong), dày dần vào lòng (chữ dễ đọc)
                float lumBg = dot(bg, LUM);
                float lumTint = dot(_Tint.rgb, LUM);
                float clash = saturate(abs(lumBg - lumTint) * 1.6 - 0.2) * _Adaptive;
                float tintA = lerp(_Tint.a, min(_Tint.a + 0.16, 0.94), clash);
                tintA *= lerp(1.0 - _EdgeClear, 1.0, smoothstep(0.0, 0.55, x));
                float3 col = lerp(bg, _Tint.rgb, tintA);

                // Kính màu (tint của Apple): màu Image khác trắng nhuộm kính; trắng thì giữ nguyên
                float3 vc = i.color.rgb;
                float tintW = saturate(1.0 - min(min(vc.r, vc.g), vc.b));
                // nhuộm thẳng lên nền đã khúc xạ (không qua lớp phủ trắng/đen) để kính màu trong và rực, không xám
                float3 tinted = lerp(bg, vc * lerp(0.8, 1.2, saturate(dot(bg, LUM) * 1.3)), _VertTint);
                col = lerp(col, tinted, tintW);

                // lưới bản vẽ (chỉ ở chủ đề Xanh mực), cố định theo điểm ảnh màn hình
                if (_Grid > 0.001)
                {
                    float2 gp = i.pos.xy;
                    float2 minor = abs(frac(gp / 24.0) - 0.5) * 24.0;
                    float2 major = abs(frac(gp / 96.0) - 0.5) * 96.0;
                    float gMinor = 1.0 - saturate(min(minor.x, minor.y) - 11.2);
                    float gMajor = 1.0 - saturate(min(major.x, major.y) - 47.0);
                    col += float3(0.35, 0.62, 1.0) * (gMinor * 0.022 + gMajor * 0.035) * _Grid * smoothstep(0.1, 0.5, x);
                }

                // viền mảnh tách kính khỏi nền
                float border = smoothstep(0.045, 0.012, x);
                col = lerp(col, _BorderColor.rgb, border * _BorderColor.a * 0.55);

                // 5. Ánh sáng
                float2 L = _KL_Light.xy;
                float lenL = length(L);
                L = lenL > 1e-3 ? L / lenL : float2(-0.45, 0.89);
                float energy = _KL_Light.z > 0.01 ? _KL_Light.z : 1.0;
                float facing = dot(n, L);
                float3 specTint = lerp(float3(1, 1, 1), _AccentColor.rgb, _AccentColor.a * 0.35);
                float lightBg = smoothstep(0.35, 0.9, lumTint);        // kính sáng (chủ đề Trắng): vệt sáng dịu hơn

                // hai lớp phản chiếu: đường sắc mảnh sát mép (~1,5 px) + dải mềm (~4 px), và quầng rộng phía trong
                float rimSharp = smoothstep(0.05, 0.012, x) * smoothstep(0.0, 0.01, x + 0.004);
                float rimBand = smoothstep(0.12, 0.02, x);
                float rimSoft = smoothstep(0.5, 0.0, x);
                float key = pow(saturate(facing), 2.2);
                float back = pow(saturate(-facing), 3.0);
                float glint = pow(saturate(facing), 28.0);            // điểm lóe nơi mép quay thẳng về đèn
                float spec = rimSharp * (key * 0.75 + back * 0.32 + glint * 0.9)
                           + rimBand * (key * 0.30 + back * 0.12)
                           + rimSoft * key * 0.07;
                spec *= _Specular * energy * cond * lerp(1.0, 0.6, lightBg);
                // vệt sáng cộng kiểu "screen" để không cháy trắng thành mảng nhựa
                float3 sp = specTint * saturate(spec);
                col = 1.0 - (1.0 - col) * (1.0 - sp);

                // Fresnel đều quanh mép + bóng trong mảnh ở phía khuất đèn tạo độ dày
                col += specTint * fres * 0.05 * cond;
                col *= 1.0 - smoothstep(0.30, 0.04, x) * saturate(-facing * 0.9 + 0.15) * 0.14 * cond;

                // khúc xạ ánh sáng xuyên qua vành: dải hơi sáng nằm phía trong, đối diện đèn (caustic)
                float caustic = smoothstep(0.08, 0.2, x) * smoothstep(0.38, 0.2, x) * back;
                col += specTint * caustic * 0.035 * (1.0 - lightBg) * _Specular * cond;

                // nhiễu rất nhỏ chống dải màu trên vùng chuyển mượt (gradient) của kính
                float nz = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                col += nz / 255.0;

                return fixed4(col, s.a * cond);
            }
            ENDCG
        }
    }
}
