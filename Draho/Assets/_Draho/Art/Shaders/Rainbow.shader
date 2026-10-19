// A rainbow far away round the camera (like NightSky: no fog, additive). The mesh stores sky directions (unit
// vectors); uv.y runs across the band from the inside (0) to the outside (1), uv.x = 1 for the secondary bow.
Shader "Draho/Rainbow"
{
    Properties
    {
        _Distance ("Distance (m)", Float) = 150
        _Bright ("Brightness", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-195" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Rainbow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
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
                o.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + input.positionOS * _Distance);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Primary bow: violet inside, red outside; the secondary bow is the other way round and fainter.
                float t = i.uv.x > 0.5 ? 1.0 - i.uv.y : i.uv.y;
                float h = (1.0 - t) * 0.78;
                half3 col = saturate(half3(abs(h * 6.0 - 3.0) - 1.0, 2.0 - abs(h * 6.0 - 2.0), 2.0 - abs(h * 6.0 - 4.0)));
                half edge = sin(i.uv.y * 3.14159);
                half strength = i.uv.x > 0.5 ? 0.35 : 1.0;
                return half4(col * edge * strength * _Bright, 1.0);
            }
            ENDHLSL
        }
    }
}
