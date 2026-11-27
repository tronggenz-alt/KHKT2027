Shader "Khkt/KinhLongApple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Hình học phần tử (đơn vị điểm ảnh UI)
        _KichThuoc ("Element Size (px)", Vector) = (260, 80, 0, 0)
        _BanKinhBo ("Corner Radius (px)", Float) = 22.0
        _DoMuotGoc ("Corner Curvature", Range(2.0, 4.0)) = 3.4
        _LoaiMatCat ("Profile (0=Squircle,1=Lip,2=Circle,3=Concave)", Float) = 0.0

        // Quang học Kính Lỏng Apple
        _CheDoKinh ("Mode (0=Regular, 1=Clear)", Float) = 0.0
        _LaDieuHuong ("Is Navigation Layer", Float) = 0.0
        _LaNenSang ("Is Light Glass", Float) = 0.0
        _MucMo ("Blur Level (0..4)", Range(0.0, 4.0)) = 2.35
        _VienKhucXa ("Refraction Bezel (px)", Float) = 18.0
        _CuongDoKhucXa ("Refraction Strength", Range(0.0, 2.5)) = 1.0
        _CuongDoNen ("Dynamic Range Compress", Range(0.0, 1.0)) = 0.45
        _DoRuc ("Vibrancy Saturation", Range(0.8, 1.5)) = 1.18
        _ToiCucBo ("Clear Local Dimming", Range(0.0, 0.7)) = 0.0
        _CuongDoVienSang ("Rim Specular Strength", Range(0.0, 2.0)) = 1.0
        _CuongDoVienToi ("Outer Hairline Strength", Range(0.0, 1.0)) = 0.24

        // Bóng đổ thích ứng bên ngoài thân kính
        _BongCuongDo ("Shadow Strength", Range(0.0, 0.6)) = 0.18
        _BongKichThuoc ("Shadow Blur (px)", Range(0.0, 32.0)) = 16.0
        _BongDichY ("Shadow Y Offset (px)", Float) = 5.0

        // Trạng thái tương tác động (P4-P6)
        _NgungTu ("Materialize (0..1)", Range(0.0, 1.0)) = 1.0
        _RoiTam ("Hover Glow (uvX, uvY, radiusPx, intensity)", Vector) = (0.5, 0.5, 90.0, 0.0)
        _ChamTam ("Touch Wave (uvX, uvY, radiusPx, intensity)", Vector) = (0.5, 0.5, 110.0, 0.0)
        _QuetVien ("Rim Sweep (-1=off, 0..1)", Float) = -1.0

        // UI Stencil & RectMask2D
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
            Name "KinhLongApple"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ _HOP_KINH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                float4 worldPos   : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);         SAMPLER(sampler_MainTex);
            TEXTURE2D(_KL_BangKhucXa);   SAMPLER(sampler_KL_BangKhucXa);

            TEXTURE2D(_KL_NoiDung);      SAMPLER(sampler_KL_NoiDung);
            TEXTURE2D(_KL_Mo1);          SAMPLER(sampler_KL_Mo1);
            TEXTURE2D(_KL_Mo2);          SAMPLER(sampler_KL_Mo2);
            TEXTURE2D(_KL_Mo3);          SAMPLER(sampler_KL_Mo3);
            TEXTURE2D(_KL_Mo4);          SAMPLER(sampler_KL_Mo4);

            TEXTURE2D(_KL_Nen);          SAMPLER(sampler_KL_Nen);
            TEXTURE2D(_KL_NenMo1);       SAMPLER(sampler_KL_NenMo1);
            TEXTURE2D(_KL_NenMo2);       SAMPLER(sampler_KL_NenMo2);
            TEXTURE2D(_KL_NenMo3);       SAMPLER(sampler_KL_NenMo3);
            TEXTURE2D(_KL_NenMo4);       SAMPLER(sampler_KL_NenMo4);

            float _KL_DungNoiDung;
            float _KL_DungNen;
            float4 _KL_NenUV;
            float4 _KL_Light;
            float _KL_ChieuSauKinh;
            float _KL_TanSac;
            float _KL_DoTrongKinh;
            float _KL_GiamTrongSuot;
            float _KL_TangTuongPhan;
            float _KL_MatTieuDiem;

            // Nhóm biến hình hợp nhất HopKinh (tối đa 16 phần tử trong không gian tâm quad)
            int _KL_HopSoLuong;
            float _KL_HopDoMuot;
            float4 _KL_HopTamKichThuoc[16];
            float _KL_HopBanKinh[16];

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ClipRect;
                float4 _KichThuoc;
                float _BanKinhBo;
                float _DoMuotGoc;
                float _LoaiMatCat;
                float _CheDoKinh;
                float _LaDieuHuong;
                float _LaNenSang;
                float _MucMo;
                float _VienKhucXa;
                float _CuongDoKhucXa;
                float _CuongDoNen;
                float _DoRuc;
                float _ToiCucBo;
                float _CuongDoVienSang;
                float _CuongDoVienToi;
                float _BongCuongDo;
                float _BongKichThuoc;
                float _BongDichY;
                float _NgungTu;
                float4 _RoiTam;
                float4 _ChamTam;
                float _QuetVien;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPos = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.uv1 = input.uv1;
                output.color = input.color * _Color;
                return output;
            }

            float UI2DClip(float2 pos, float4 rect)
            {
                float2 inside = step(rect.xy, pos) * step(pos, rect.zw);
                return inside.x * inside.y;
            }

            // SDF Squircle / Pill ổn định số (không sinh NaN trên Metal/HLSL)
            float SdSquircle(float2 p, float2 halfSize, float radius, float expPow)
            {
                float r = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - (halfSize - r);
                float2 qp = max(q, 0.0);
                float m = max(qp.x, qp.y);
                float outer = 0.0;
                if (m > 1e-5)
                {
                    float2 s = max(qp / m, 0.0);
                    outer = m * pow(pow(abs(s.x), expPow) + pow(abs(s.y), expPow), 1.0 / expPow);
                }
                return outer + min(max(q.x, q.y), 0.0) - r;
            }

            float SminPoly(float a, float b, float k)
            {
                float kk = max(k, 1e-4);
                float h = max(kk - abs(a - b), 0.0) / kk;
                return min(a, b) - h * h * kk * 0.25;
            }

            float EvalSdf(float2 p, float2 halfSize, float radius, float expPow)
            {
                float d = SdSquircle(p, halfSize, radius, expPow);
                #ifdef _HOP_KINH
                int n = min(_KL_HopSoLuong, 16);
                for (int i = 0; i < n; i++)
                {
                    float4 c = _KL_HopTamKichThuoc[i];
                    float r = _KL_HopBanKinh[i];
                    float expI = (r >= min(c.z, c.w) * 0.94) ? 2.0 : expPow;
                    float di = SdSquircle(p - c.xy, c.zw, r, expI);
                    d = SminPoly(d, di, _KL_HopDoMuot);
                }
                #endif
                return d;
            }

            float SampleLutProfile(float uLut)
            {
                float4 lut = SAMPLE_TEXTURE2D(_KL_BangKhucXa, sampler_KL_BangKhucXa, float2(saturate(uLut), 0.5));
                if (_LoaiMatCat < 0.5) return lut.r;
                if (_LoaiMatCat < 1.5) return lut.g;
                if (_LoaiMatCat < 2.5) return lut.b;
                return lut.a;
            }

            float3 SamplePyramid(float2 uv, float blurLvl, bool useLive)
            {
                float b = clamp(blurLvl, 0.0, 4.0);
                float3 cA, cB;
                float t;

                if (useLive)
                {
                    if (b < 1.0)
                    {
                        t = b;
                        cA = SAMPLE_TEXTURE2D(_KL_NoiDung, sampler_KL_NoiDung, uv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_Mo1, sampler_KL_Mo1, uv).rgb;
                    }
                    else if (b < 2.0)
                    {
                        t = b - 1.0;
                        cA = SAMPLE_TEXTURE2D(_KL_Mo1, sampler_KL_Mo1, uv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_Mo2, sampler_KL_Mo2, uv).rgb;
                    }
                    else if (b < 3.0)
                    {
                        t = b - 2.0;
                        cA = SAMPLE_TEXTURE2D(_KL_Mo2, sampler_KL_Mo2, uv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_Mo3, sampler_KL_Mo3, uv).rgb;
                    }
                    else
                    {
                        t = b - 3.0;
                        cA = SAMPLE_TEXTURE2D(_KL_Mo3, sampler_KL_Mo3, uv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_Mo4, sampler_KL_Mo4, uv).rgb;
                    }
                }
                else
                {
                    float2 nenUv = saturate(uv * _KL_NenUV.xy + _KL_NenUV.zw);
                    if (b < 1.0)
                    {
                        t = b;
                        cA = SAMPLE_TEXTURE2D(_KL_Nen, sampler_KL_Nen, nenUv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_NenMo1, sampler_KL_NenMo1, nenUv).rgb;
                    }
                    else if (b < 2.0)
                    {
                        t = b - 1.0;
                        cA = SAMPLE_TEXTURE2D(_KL_NenMo1, sampler_KL_NenMo1, nenUv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_NenMo2, sampler_KL_NenMo2, nenUv).rgb;
                    }
                    else if (b < 3.0)
                    {
                        t = b - 2.0;
                        cA = SAMPLE_TEXTURE2D(_KL_NenMo2, sampler_KL_NenMo2, nenUv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_NenMo3, sampler_KL_NenMo3, nenUv).rgb;
                    }
                    else
                    {
                        t = b - 3.0;
                        cA = SAMPLE_TEXTURE2D(_KL_NenMo3, sampler_KL_NenMo3, nenUv).rgb;
                        cB = SAMPLE_TEXTURE2D(_KL_NenMo4, sampler_KL_NenMo4, nenUv).rgb;
                    }
                }

                return lerp(cA, cB, t);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float clipFactor = 1.0;
                #ifdef UNITY_UI_CLIP_RECT
                clipFactor = UI2DClip(input.worldPos.xy, _ClipRect);
                if (clipFactor <= 0.001) discard;
                #endif

                float2 elemSize = (input.uv1.x > 1.0 && input.uv1.y > 1.0) ? input.uv1.xy : _KichThuoc.xy;
                elemSize = max(elemSize, float2(2.0, 2.0));
                float2 halfSize = elemSize * 0.5;
                float2 p = (input.uv - 0.5) * elemSize;

                float radius = clamp(_BanKinhBo, 1.0, min(halfSize.x, halfSize.y));
                float expPow = (radius >= min(halfSize.x, halfSize.y) * 0.94) ? 2.0 : _DoMuotGoc;

                float sd = EvalSdf(p, halfSize, radius, expPow);
                float aa = max(0.75, length(float2(ddx(sd), ddy(sd))));
                float bodyMask = smoothstep(aa * 0.5, -aa * 0.5, sd);

                // Pháp tuyến hướng ra ngoài dựa trên đạo hàm SDF (hỗ trợ cả hình hợp nhất HopKinh)
                float2 grad = float2(ddx(sd), ddy(sd));
                float gLen = length(grad);
                float2 n2 = (gLen > 1e-4) ? (grad / gLen) : normalize(p + 1e-4);

                // Bóng đổ mềm thích ứng ở phía ngoài viền kính (đặc biệt hướng xuống dưới)
                float shadowAlpha = 0.0;
                if (_BongCuongDo > 0.001 && _BongKichThuoc > 0.5 && bodyMask < 0.999)
                {
                    float2 pShadow = p + float2(0.0, _BongDichY);
                    float sdShadow = EvalSdf(pShadow, halfSize, radius, expPow);
                    float sh = 1.0 - smoothstep(-1.0, max(_BongKichThuoc, 2.0), sdShadow);
                    shadowAlpha = sh * sh * _BongCuongDo * (1.0 - bodyMask) * saturate(1.0 - _KL_MatTieuDiem * 0.65);
                }

                if (bodyMask <= 0.001 && shadowAlpha <= 0.001)
                    discard;

                float2 screenUV = saturate(input.screenPos.xy / max(input.screenPos.w, 1e-5));
                bool useLive = (_LaDieuHuong > 0.5 && _KL_DungNoiDung > 0.5);
                bool hasBackdrop = useLive || (_KL_DungNen > 0.5);

                // Đường cong ngưng tụ quang học P5 (0..1): 0.40s không fade alpha thô mà vặn chiết suất + mờ
                float mCurve = saturate(_NgungTu);
                float refractPulse = (mCurve < 0.999) ? lerp(0.0, 1.35, sin(mCurve * 3.14159 * 0.68)) : 1.0;
                refractPulse = lerp(refractPulse, 1.0, smoothstep(0.65, 1.0, mCurve));
                float blurScale = (mCurve < 0.999) ? lerp(0.25, 1.25, smoothstep(0.0, 0.45, mCurve)) : 1.0;
                blurScale = lerp(blurScale, 1.0, smoothstep(0.45, 1.0, mCurve));

                // Trợ năng
                float effRefractScale = _CuongDoKhucXa * _KL_ChieuSauKinh * refractPulse * (1.0 - _KL_GiamTrongSuot * 0.7) * (1.0 - _KL_MatTieuDiem * 0.5);
                float effBlur = _MucMo * blurScale * lerp(1.0, 1.8, _KL_GiamTrongSuot) * lerp(1.0, 0.65, _KL_MatTieuDiem);
                if (_CheDoKinh > 0.5)
                    effBlur = min(effBlur, lerp(1.15, 2.2, _KL_GiamTrongSuot));

                float bezelPx = clamp(_VienKhucXa, 4.0, min(halfSize.x, halfSize.y) * 0.9);
                float uLut = clamp(-sd / bezelPx, 0.0, 1.0);
                float lutDisp = SampleLutProfile(uLut);

                float2 pixToUv = 1.0 / max(_ScreenParams.xy, float2(64.0, 64.0));
                float2 refOffset = -n2 * (lutDisp * effRefractScale) * pixToUv;

                float caAmt = _KL_TanSac * abs(lutDisp) * (1.0 - smoothstep(0.0, 0.6, uLut)) * (1.0 - _KL_TangTuongPhan) * smoothstep(0.0, 1.0, _CheDoKinh);
                float2 tangent = float2(-n2.y, n2.x);
                float2 caVec = (n2 * 0.7 + tangent * 0.3) * caAmt * pixToUv;

                float3 bgLin;
                if (hasBackdrop)
                {
                    if (caAmt > 1e-4)
                    {
                        float rC = SamplePyramid(saturate(screenUV + refOffset + caVec), effBlur, useLive).r;
                        float gC = SamplePyramid(saturate(screenUV + refOffset), effBlur, useLive).g;
                        float bC = SamplePyramid(saturate(screenUV + refOffset - caVec), effBlur, useLive).b;
                        bgLin = float3(rC, gC, bC);
                    }
                    else
                    {
                        bgLin = SamplePyramid(saturate(screenUV + refOffset), effBlur, useLive);
                    }
                }
                else
                {
                    float s = saturate(_LaNenSang);
                    bgLin = lerp(float3(0.025, 0.032, 0.045), float3(0.82, 0.84, 0.88), s);
                }

                // Toàn bộ nén dải sáng, độ rực và cộng loé thực hiện trong không gian sRGB nhận thức
                float3 c = LinearToSRGB(max(bgLin, 0.0));
                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                float nenSang = saturate(_LaNenSang);

                float clearness = clamp(_KL_DoTrongKinh, 0.0, 1.0);
                float baseNen = (_CheDoKinh > 0.5)
                    ? (_CuongDoNen * 0.08)
                    : (_CuongDoNen * lerp(0.92, 0.30, clearness));

                // Kính Tối Regular đặt trên vùng nền rực/sáng tự tăng nén dải sáng để chữ trắng luôn rõ >= 4.5:1
                float brightOverDark = saturate((luma - 0.12) * 1.85) * (1.0 - nenSang) * (1.0 - _CheDoKinh);
                baseNen = saturate(baseNen + brightOverDark * 0.78 + _KL_GiamTrongSuot * 0.35);
                if (_KL_TangTuongPhan > 0.5) baseNen = 0.85;

                float ldich = lerp(0.10, 0.90, nenSang);
                float lt = lerp(luma, ldich, baseNen);

                if (luma > 1e-4)
                {
                    float ratio = lt / luma;
                    // Giữ sắc trung tính tuyệt đối trên nền xám (R=G=B giữ nguyên R=G=B)
                    c = lerp(float3(lt, lt, lt), c * ratio, 0.92);
                }
                else
                {
                    c = float3(lt, lt, lt);
                }

                // Vibrancy: tăng bão hòa các mảng màu xuyên qua kính, hoàn toàn trung tính (không tác động) trên xám
                float lumaPost = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(lumaPost, lumaPost, lumaPost), c, _DoRuc);

                // Tối dịu cục bộ cho nút Clear đặt trên nội dung rực / khung 3D sáng (0 tint)
                if (_ToiCucBo > 0.001)
                    c *= (1.0 - _ToiCucBo);

                // Ánh sáng vành định hướng (-0.45, 0.89) mượt kiểu smoothstep + phản xạ phía đối diện
                float2 lightDir = normalize(_KL_Light.xy + float2(1e-4, 1e-4));
                float align = dot(n2, lightDir);
                float primaryArc = pow(saturate(align * 0.5 + 0.5), 3.6) * 0.65 + pow(saturate(align), 2.8) * 0.55;
                float oppArc = pow(saturate(-align), 3.2) * 0.28;

                float rimSharp = smoothstep(2.6, 0.4, -sd) * smoothstep(-0.35, 0.6, -sd);
                float rimSoft = smoothstep(6.0, 0.2, -sd) * 0.16;
                float rimFactor = (rimSharp * 0.85 + rimSoft) * (primaryArc + oppArc);

                // Điểm loé rọi con trỏ (_RoiTam) và sóng chạm nhấn (_ChamTam)
                float interactiveSpec = 0.0;
                if (_RoiTam.w > 0.001)
                {
                    float2 hoverP = (_RoiTam.xy - 0.5) * elemSize;
                    float hd = length(p - hoverP);
                    interactiveSpec += exp(-hd * hd / max(_RoiTam.z * _RoiTam.z, 16.0)) * _RoiTam.w * 0.35;
                }
                if (_ChamTam.w > 0.001)
                {
                    float2 touchP = (_ChamTam.xy - 0.5) * elemSize;
                    float td = length(p - touchP);
                    interactiveSpec += exp(-td * td / max(_ChamTam.z * _ChamTam.z, 16.0)) * _ChamTam.w * 0.55;
                }
                if (_QuetVien >= 0.0)
                {
                    float ang = atan2(n2.y, n2.x) / 6.283185 + 0.5;
                    float delta = abs(frac(ang - _QuetVien + 0.5) - 0.5);
                    interactiveSpec += exp(-delta * delta * 55.0) * rimSharp * 0.75;
                }

                float specBloom = (mCurve < 0.999) ? (1.0 + sin(mCurve * 3.14159) * 0.65) : 1.0;
                float specVal = saturate((rimFactor + interactiveSpec) * _CuongDoVienSang * specBloom * (1.0 - _KL_MatTieuDiem * 0.45));
                float specCap = lerp(0.78, 0.85, nenSang);
                float sp = saturate(specVal) * specCap;

                // Screen blend đơn sắc (giữ độ tinh khiết không màu)
                c = 1.0 - (1.0 - saturate(c)) * (1.0 - sp.xxx);

                // Viền tối sub-pixel iOS 27 (0.75px) tách nét thân kính khỏi nền (lọc mượt ở đầu mũi viên nang)
                float darkRim = smoothstep(-1.1, 0.05, sd) * smoothstep(1.15, 0.2, sd);
                float darkRimStr = (_KL_TangTuongPhan > 0.5)
                    ? (smoothstep(-1.5, 0.0, sd) * 0.55)
                    : (darkRim * _CuongDoVienToi);
                c = lerp(c, float3(0.02, 0.02, 0.02), saturate(darkRimStr));

                // Dither đơn sắc chống vệt bậc trên GPU Metal/D3D11
                float dither = (frac(sin(dot(input.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
                c = saturate(c + dither.xxx);

                float3 bodyLin = SRGBToLinear(c);

                // Phối bóng đổ phía ngoài với thân kính
                float3 finalLin = lerp(float3(0.0, 0.0, 0.0), bodyLin, bodyMask);
                float finalAlpha = saturate(max(bodyMask, shadowAlpha)) * clipFactor * input.color.a;

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalAlpha - 0.001);
                #endif

                return half4(finalLin, finalAlpha);
            }
            ENDHLSL
        }
    }
}
