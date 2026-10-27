// Chữ 3D (TextMesh) có kiểm tra độ sâu: bị tường che thì không hiện, nhìn từ phía sau thì không thấy.
// Shader chữ mặc định của Unity (GUI/Text Shader) vẽ đè lên mọi thứ, nên chữ bên kia tường vẫn lộ ra.
// Viết theo URP (SRP Batcher gộp được ~1.800 nhãn), vẽ trong hàng đợi riêng ngay trước vật trong suốt để không xen kẽ
// với ~700 lọ thủy tinh (trước đây mỗi lần xen là một lần đổi shader, phòng hóa chất mất hơn 1.300 lần mỗi khung hình).
Shader "Khkt/ChuTrongCanh"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Cull Back
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Chu"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = i.color;
                c.a *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                return c;
            }
            ENDHLSL
        }
    }
}
