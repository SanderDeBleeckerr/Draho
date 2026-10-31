// Clouds far away round the camera (like NightSky: positions are sky directions, no fog). Each quad is a soft cloud
// from a noise texture, alpha blended; the colour comes from the material (set by SkyClouds from the time of day)
// and is lit a little more at the top than at the base. uv2.x = size (m), the vertex colour alpha = the cloud's own
// opacity. The quads drift round the sky with _Drift (degrees of azimuth). Drawn after the stars (they hide them).
Shader "Draho/Clouds"
{
    Properties
    {
        _MainTex ("Cloud", 2D) = "white" {}
        _Lit ("Lit colour", Color) = (1, 1, 1, 1)
        _Shade ("Shade colour", Color) = (0.7, 0.72, 0.78, 1)
        _Distance ("Distance (m)", Float) = 150
        _Drift ("Drift (degrees)", Float) = 0
        _Opacity ("Opacity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-198" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Lit;
                half4 _Shade;
                float _Distance;
                float _Drift;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float a = radians(_Drift);
                float s = sin(a), c = cos(a);
                float3 d = normalize(input.positionOS);
                d = float3(d.x * c - d.z * s, d.y, d.x * s + d.z * c);
                float3 right = normalize(cross(float3(0, 1, 0), d) + float3(1e-4, 0, 0));
                float3 up = cross(d, right);
                float2 corner = input.uv * 2.0 - 1.0;
                float w = input.uv2.x;
                float3 positionWS = _WorldSpaceCameraPos + d * _Distance + (right * corner.x * w + up * corner.y * w * 0.45);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.alpha = input.color.a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                half3 col = lerp(_Shade.rgb, _Lit.rgb, saturate(i.uv.y * 1.3));
                return half4(col, t * i.alpha * _Opacity);
            }
            ENDHLSL
        }
    }
}
