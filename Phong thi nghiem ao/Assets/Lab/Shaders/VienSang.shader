// Viền sáng quanh dụng cụ đang được trỏ chuột (kiểu "inverted hull"): vẽ lại lưới, đẩy các đỉnh ra ngoài
// theo pháp tuyến một số pixel cố định trên màn hình, chỉ vẽ mặt sau nên chỉ lộ ra phần viền.
Shader "Khkt/VienSang"
{
    Properties
    {
        _Color ("Màu viền", Color) = (0.45, 0.9, 1, 1)
        _Width ("Độ dày (pixel)", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Width;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 clip = UnityObjectToClipPos(v.vertex);
                float3 nView = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 dir = TransformViewToProjection(nView.xy);
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                clip.xy += dir * _Width * 2.0 * clip.w / _ScreenParams.xy;
                o.pos = clip;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
