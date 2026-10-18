// Stars and the moon for the night sky. Hand-written URP shader (no Shader Graph: the scenes are built in code).
// Each quad is a billboard far away in a fixed direction from the camera: the mesh stores the direction (vertex
// position), the corner (uv), a twinkle seed (colour alpha) and the size in metres (uv2.x). No fog, additive.
// SkyMood sets _DrahoNight (0 by day, 1 at night) and _DrahoMoonDir (towards the moon).
Shader "Draho/NightSky"
{
    Properties
    {
        _Color ("Colour", Color) = (0.85, 0.9, 1, 1)
        _Distance ("Distance (m)", Float) = 140
        _Moon ("Moon (1) or stars (0)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-200" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "NightSky"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Distance;
                float _Moon;
            CBUFFER_END

            float _DrahoNight;
            float3 _DrahoMoonDir;

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
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 dir = _Moon > 0.5 ? normalize(_DrahoMoonDir) : normalize(input.positionOS);
                float3 right = normalize(cross(float3(0, 1, 0), dir) + float3(1e-4, 0, 0));
                float3 up = cross(dir, right);
                float2 corner = input.uv * 2.0 - 1.0;
                float3 positionWS = _WorldSpaceCameraPos + dir * _Distance + (right * corner.x + up * corner.y) * (input.uv2.x * 0.5);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);
                half3 col;
                if (_Moon > 0.5)
                {
                    // A pale disc with a soft halo.
                    half disc = smoothstep(0.36, 0.32, d);
                    half halo = exp(-d * 5.0) * 0.45 * (1.0 - disc);
                    col = _Color.rgb * (disc + halo);
                }
                else
                {
                    // A small soft point that twinkles at its own speed.
                    half core = saturate(1.0 - d);
                    core = core * core * core;
                    float seed = i.color.a;
                    half twinkle = 0.6 + 0.4 * sin(_Time.y * (1.3 + seed * 2.7) + seed * 40.0);
                    col = _Color.rgb * i.color.rgb * core * twinkle;
                }
                return half4(col * _DrahoNight, 1.0);
            }
            ENDHLSL
        }
    }
}
