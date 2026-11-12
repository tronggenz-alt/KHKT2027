Shader "Khkt/Tang2/HexWipe"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _TienDo ("Tiến độ (0-1)", Range(0, 1)) = 0.0
        _Tam ("Tâm quét (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Chieu ("Chiều quét (+1 mở, -1 đóng)", Float) = 1.0
        _TiLeManHinh ("Tỉ lệ màn hình (W/H)", Float) = 1.77778
        _HexScale ("Tỉ lệ lưới lục giác", Float) = 28.0
        _BgColor ("Màu nền tối", Color) = (0.039, 0.071, 0.133, 1.0) // #0A1222
        _BorderColor ("Màu viền sáng", Color) = (0.133, 0.827, 0.933, 1.0) // #22D3EE
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float _TienDo;
            float4 _Tam;
            float _Chieu;
            float _TiLeManHinh;
            float _HexScale;
            fixed4 _BgColor;
            fixed4 _BorderColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 HexGrid(float2 uv, out float2 hexCenter)
            {
                float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = fmod(uv, r) - h;
                float2 b = fmod(uv - h, r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                hexCenter = uv - gv;
                return gv;
            }

            float HexDist(float2 p)
            {
                p = abs(p);
                float c = dot(p, normalize(float2(1.0, 1.7320508)));
                c = max(c, p.x);
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_TienDo <= 0.0001) return fixed4(0, 0, 0, 0);
                if (_TienDo >= 0.9999) return _BgColor;

                float2 aspectUV = float2(i.uv.x * _TiLeManHinh, i.uv.y);
                float2 centerAspect = float2(_Tam.x * _TiLeManHinh, _Tam.y);

                float2 hexCenter;
                float2 localHex = HexGrid(aspectUV * _HexScale, hexCenter);
                float distToHexEdge = HexDist(localHex);

                float distFromOrigin = length((hexCenter / _HexScale) - centerAspect);
                float maxR = max(1.0, _TiLeManHinh) * 0.85;
                float normDist = saturate(distFromOrigin / maxR);

                float cellProgress;
                if (_Chieu > 0.0)
                {
                    cellProgress = (_TienDo * 1.35) - normDist;
                }
                else
                {
                    cellProgress = (_TienDo * 1.35) - (1.0 - normDist);
                }

                if (cellProgress <= 0.0)
                {
                    return fixed4(0, 0, 0, 0);
                }

                float edgeWidth = 0.07;
                float isBorder = smoothstep(0.5 - edgeWidth, 0.5, distToHexEdge);

                float waveGlow = saturate(1.0 - (cellProgress / 0.35));

                fixed4 col;
                if (cellProgress < 0.30)
                {
                    col = lerp(_BgColor, _BorderColor, waveGlow * 0.85 + isBorder * 0.45);
                    col.a = saturate(cellProgress / 0.15 + isBorder * 0.8);
                }
                else
                {
                    col = lerp(_BgColor, _BorderColor, isBorder * 0.18);
                    col.a = 1.0;
                }

                return col;
            }
            ENDCG
        }
    }
}
