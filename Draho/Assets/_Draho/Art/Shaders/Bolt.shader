// A lightning bolt far away round the camera (like NightSky: no fog, additive). The mesh stores sky directions
// (positions on a unit sphere, the ribbon width already added on the CPU); uv.y runs across the ribbon.
Shader "Draho/Bolt"
{
    Properties
    {
        _Color ("Colour", Color) = (0.8, 0.85, 1, 1)
        _Distance ("Distance (m)", Float) = 140
        _Bright ("Brightness", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-190" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Bolt"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Distance;
                float _Bright;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = _WorldSpaceCameraPos + input.positionOS * _Distance;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                half glow = across * across * 0.6 + pow(across, 8.0) * 2.0; // a soft glow round a white-hot core
                return half4(_Color.rgb * glow * _Bright, 1.0);
            }
            ENDHLSL
        }
    }
}
