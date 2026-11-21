Shader "Khkt/TayNguoi"
{
    // Vật liệu tay người góc nhìn thứ nhất (VERA Lab, 20/11/2026):
    //  _Mode = 0: da người (bọc sáng + tán xạ dưới da + hai lớp phản xạ dầu da, lỗ chân lông, nếp gấp khớp)
    //  _Mode = 1: găng nitrile (mờ nhẹ, bóng mềm, sáng xuyên ở đầu ngón mỏng, hạt nhám mịn)
    //  _Mode = 2: vải áo blouse (khuếch tán mềm, ánh vải ở rìa, sợi dệt chéo, AO đậm trong nếp gấp)
    // Mẫu chi tiết tính theo tọa độ tư thế gốc (TEXCOORD1) nên không trôi khi khớp gập.
    // Nổi khối chi tiết dùng "surface gradient" theo đạo hàm màn hình, không cần tangent hay texture.
    Properties
    {
        _BaseColor ("Màu chính", Color) = (0.87, 0.69, 0.59, 1)
        _Mode ("Chế độ (0 da, 1 găng, 2 vải)", Float) = 0
        _SubsurfaceColor ("Màu tán xạ dưới da", Color) = (0.90, 0.32, 0.22, 1)
        _Wrap ("Bọc sáng", Range(0, 1)) = 0.45
        _SssStrength ("Cường độ tán xạ", Range(0, 1)) = 0.40
        _Smoothness ("Độ mịn", Range(0, 1)) = 0.35
        _SpecStrength ("Cường độ phản xạ", Range(0, 1)) = 0.35
        _SheenColor ("Màu ánh rìa", Color) = (1, 1, 1, 1)
        _SheenStrength ("Ánh rìa", Range(0, 1)) = 0.2
        _DetailScale ("Tỉ lệ chi tiết (1/m)", Float) = 900
        _DetailStrength ("Độ nổi chi tiết", Range(0, 2)) = 0.6
        _CreaseStrength ("Độ đậm nếp gấp", Range(0, 2)) = 1
        _AoStrength ("AO đỉnh", Range(0, 1)) = 0.85
        _Inflate ("Phồng theo pháp tuyến (m)", Float) = 0
        _Translucency ("Sáng xuyên", Range(0, 1)) = 0
        _ClothSag ("Vải rủ theo trọng lực (m)", Float) = 0.0035
        _ClothLag ("Vải trễ quán tính (m, thế giới)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _SubsurfaceColor;
            float4 _SheenColor;
            float _Mode;
            float _Wrap;
            float _SssStrength;
            float _Smoothness;
            float _SpecStrength;
            float _SheenStrength;
            float _DetailScale;
            float _DetailStrength;
            float _CreaseStrength;
            float _AoStrength;
            float _Inflate;
            float _Translucency;
            float _ClothSag;
            float4 _ClothLag;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv0        : TEXCOORD0;
                float3 bindPos    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : TEXCOORD2;
                float3 bindPos    : TEXCOORD3;
                float2 uv0        : TEXCOORD4;
                float  fog        : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 p = v.positionOS.xyz + v.normalOS * _Inflate;
                float3 posWS = TransformObjectToWorld(p);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                if (_Mode > 1.5)
                {
                    // Vải: chùng xuống theo trọng lực (mặt dưới ống tay rủ nhiều hơn) + trễ theo quán tính, theo độ chùng (G)
                    float loose = v.color.g;
                    float under = saturate(-nWS.y);
                    posWS.y -= _ClothSag * loose * (0.35 + 0.65 * under);
                    posWS += _ClothLag.xyz * loose * (0.5 + 0.5 * under);
                }
                o.positionCS = TransformWorldToHClip(posWS);
                o.positionWS = posWS;
                o.normalWS = nWS;
                o.color = v.color;
                o.bindPos = v.bindPos;
                o.uv0 = v.uv0;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // ---------- Nhiễu giá trị 3D (không texture) ----------
            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Chiều cao chi tiết vi mô (đơn vị tùy ý), mờ dần khi mẫu nhỏ hơn điểm ảnh để không bị nhiễu răng cưa
            float DetailHeight(float3 bp, float2 uv, float4 col, float pixelFootprint)
            {
                float h = 0;
                if (_Mode < 0.5)
                {
                    // Da: lỗ chân lông + vân da + nếp gấp ngang ở khớp (R = vùng nếp, uv0.x = tọa độ dọc đốt)
                    float sc = _DetailScale;
                    float fade = saturate(1.0 - pixelFootprint * sc * 0.35);
                    h += (Noise3(bp * sc) - 0.5) * 0.55 * fade;
                    h += (Noise3(bp * sc * 0.27 + 7.1) - 0.5) * 0.45;
                    float creaseFreq = 6.2831 / 0.0016;
                    float stripes = pow(abs(sin(uv.x * creaseFreq + Noise3(bp * 420.0) * 2.4)), 6.0);
                    float creaseFade = saturate(1.0 - pixelFootprint * 900.0);
                    h -= stripes * col.r * 1.4 * _CreaseStrength * creaseFade;
                }
                else if (_Mode < 1.5)
                {
                    // Găng nitrile: hạt nhám rất mịn + nếp nhăn mềm ở khớp
                    float sc = _DetailScale;
                    float fade = saturate(1.0 - pixelFootprint * sc * 0.35);
                    h += (Noise3(bp * sc) - 0.5) * 0.35 * fade;
                    h -= col.r * (Noise3(bp * 260.0 + 3.3) * 0.9) * _CreaseStrength;
                }
                else
                {
                    // Vải blouse: sợi dệt chéo (twill) + xơ vải; uv0 = (chu vi, dọc tay áo) theo mét
                    float k = 6.2831 / 0.0011;
                    float fade = saturate(1.0 - pixelFootprint * 2600.0);
                    float twill = sin((uv.x + uv.y) * k) * 0.5 + sin((uv.x - uv.y * 0.35) * k * 0.5) * 0.25;
                    h += twill * 0.35 * fade;
                    h += (Noise3(bp * 380.0) - 0.5) * 0.5;
                }
                return h;
            }

            // Bump theo surface gradient (Mikkelsen 2010): không cần tangent
            float3 PerturbNormal(float3 n, float3 posWS, float h)
            {
                float3 dpdx = ddx(posWS);
                float3 dpdy = ddy(posWS);
                float dhdx = ddx(h);
                float dhdy = ddy(h);
                float3 r1 = cross(dpdy, n);
                float3 r2 = cross(n, dpdx);
                float det = dot(dpdx, r1);
                float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
                return normalize(abs(det) * n - grad * 0.0009 * _DetailStrength);
            }

            float3 ShadeLight(Light light, float3 n, float3 v, float3 albedo, float ao, float thin, float specMask)
            {
                float ndl = dot(n, light.direction);
                float atten = light.distanceAttenuation * light.shadowAttenuation;
                float wrapD = saturate((ndl + _Wrap) / ((1.0 + _Wrap) * (1.0 + _Wrap)));
                float3 diffuse = albedo * wrapD;

                float3 h = normalize(light.direction + v);
                float ndh = saturate(dot(n, h));
                float ndv = saturate(dot(n, v));
                float rough = max(0.04, 1.0 - _Smoothness);
                float a2 = rough * rough * rough * rough;
                float d = ndh * ndh * (a2 - 1.0) + 1.0;
                float ggx = a2 / (PI * d * d + 1e-5);
                float fres = 0.04 + 0.96 * pow(1.0 - saturate(dot(h, v)), 5.0);
                float spec = ggx * fres * saturate(ndl) * _SpecStrength * specMask;

                float3 extra = 0;
                if (_Mode < 0.5)
                {
                    // Dải tán xạ dưới da vùng chuyển sáng-tối + lớp phản xạ dầu da thứ hai rộng hơn
                    float band = exp(-ndl * ndl * 4.0) * (0.35 + 0.65 * wrapD);
                    float back = pow(saturate(dot(v, -light.direction)), 3.0) * thin * 0.6;
                    extra += _SubsurfaceColor.rgb * _SssStrength * (band + back);
                    float rough2 = 0.55;
                    float a22 = rough2 * rough2 * rough2 * rough2;
                    float d2 = ndh * ndh * (a22 - 1.0) + 1.0;
                    spec += a22 / (PI * d2 * d2 + 1e-5) * fres * saturate(ndl) * 0.5 * _SpecStrength;
                }
                else if (_Mode < 1.5)
                {
                    // Nitrile mỏng: ánh sáng xuyên ở đầu ngón / mép
                    float back = pow(saturate(dot(v, -light.direction)), 2.0) * thin;
                    extra += albedo * _Translucency * back * 0.8;
                }
                else
                {
                    // Vải: ánh rìa (sheen) mềm
                    float sheen = pow(1.0 - ndv, 4.0) * saturate(ndl + 0.3);
                    extra += _SheenColor.rgb * _SheenStrength * sheen;
                }

                float3 radiance = light.color * atten;
                return (diffuse + extra) * radiance * lerp(1.0, ao, 0.5) + radiance * spec * ao;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n0 = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float footprint = length(fwidth(i.bindPos));
                float h = DetailHeight(i.bindPos, i.uv0, i.color, footprint);
                float3 n = PerturbNormal(n0, i.positionWS, h);

                float ao = lerp(1.0, i.color.b, _AoStrength);
                float thin = i.color.a;
                float3 albedo = _BaseColor.rgb;
                float specMask = 1.0;
                if (_Mode < 0.5)
                {
                    // Lòng bàn tay sáng/hồng hơn, khớp và đầu ngón đỏ hơn, vân màu nhẹ
                    albedo = lerp(albedo, albedo * float3(1.06, 0.93, 0.90) + float3(0.03, 0.0, 0.0), i.color.g * 0.6);
                    albedo = lerp(albedo, albedo * float3(1.04, 0.86, 0.84), saturate(i.color.r * 0.7 + thin * 0.5));
                    albedo *= 0.94 + 0.12 * Noise3(i.bindPos * 140.0);
                    specMask = lerp(1.0, 0.55, i.color.g);
                }
                else if (_Mode < 1.5)
                {
                    albedo *= 0.96 + 0.06 * Noise3(i.bindPos * 90.0);
                    albedo = lerp(albedo, albedo * 1.12, thin * 0.5);
                }
                else
                {
                    albedo *= 0.97 + 0.05 * Noise3(i.bindPos * 60.0);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);

                float directAo = 1.0;
                float indirectAo = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aof = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    directAo = aof.directAmbientOcclusion;
                    indirectAo = aof.indirectAmbientOcclusion;
                #endif

                Light mainLight = GetMainLight(inputData.shadowCoord);
                float3 color = ShadeLight(mainLight, n, v, albedo, ao, thin, specMask) * directAo;

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light dl = GetAdditionalLight(li, i.positionWS);
                    color += ShadeLight(dl, n, v, albedo, ao, thin, specMask);
                }
                #endif
                LIGHT_LOOP_BEGIN(count)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    color += ShadeLight(al, n, v, albedo, ao, thin, specMask);
                LIGHT_LOOP_END
                #endif

                // Ánh sáng môi trường (SH) + phản xạ môi trường mờ
                float3 sh = SampleSH(n);
                float3 ambient = albedo * sh * ao * indirectAo;
                if (_Mode < 0.5) ambient += _SubsurfaceColor.rgb * _SssStrength * 0.10 * sh * ao;
                float3 refl = reflect(-v, n);
                float ndv = saturate(dot(n, v));
                float envF = (0.04 + 0.96 * pow(1.0 - ndv, 5.0)) * _SpecStrength * specMask;
                float3 env = GlossyEnvironmentReflection(refl, 1.0 - _Smoothness, ao * indirectAo) * envF;

                float3 sheenAmb = 0;
                if (_Mode > 1.5) sheenAmb = _SheenColor.rgb * _SheenStrength * pow(1.0 - ndv, 4.0) * sh * 0.6;

                color += ambient + env + sheenAmb;
                color = MixFog(color, i.fog);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pw);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 ShadowFrag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DVert
            #pragma fragment DFrag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V DVert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half DFrag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NVert
            #pragma fragment NFrag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V NVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NFrag(V i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
