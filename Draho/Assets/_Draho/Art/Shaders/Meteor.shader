// A shooting star: one quad far away round the camera, stretched between two sky directions (like NightSky: no
// fog, additive). The script sets the start and end directions and the progress; uv.x runs from the tail (0) to the
// head (1), uv.y across the streak.
Shader "Draho/Meteor"
{
    Properties
    {
        _Color ("Colour", Color) = (0.9, 0.95, 1, 1)
        _Distance ("Distance (m)", Float) = 150
        _Width ("Width (m)", Float) = 2.5
        _Start ("Start direction", Vector) = (0, 1, 1, 0)
        _End ("End direction", Vector) = (1, 0.8, 1, 0)
        _T ("Progress", Float) = 0
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
            Name "Meteor"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Distance;
                float _Width;
                float4 _Start;
                float4 _End;
                float _T;
                float _Bright;
            CBUFFER_END

            float _DrahoNight;

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
                float3 head = normalize(lerp(_Start.xyz, _End.xyz, _T));
                float3 tail = normalize(lerp(_Start.xyz, _End.xyz, max(0.0, _T - 0.2)));
                float3 along = lerp(tail, head, input.uv.x);
                float3 side = normalize(cross(head - tail + float3(1e-5, 0, 0), head));
                float3 positionWS = _WorldSpaceCameraPos + along * _Distance + side * ((input.uv.y * 2.0 - 1.0) * _Width * 0.5);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                half glow = i.uv.x * i.uv.x * across * across; // bright at the head, fading along the tail
                return half4(_Color.rgb * glow * _Bright * _DrahoNight, 1.0);
            }
            ENDHLSL
        }
    }
}
