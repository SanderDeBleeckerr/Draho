// Grass and ivy that move in the wind. Hand-written URP shader (no Shader Graph: the scenes are built in code).
// The sway weight comes from uv.y: 0 at the root (does not move), 1 at the tip.
// The colour comes from _BaseMap at uv.x (a small strip of leaf tones).
// Lighting: main light with shadows, ambient probe (SH), fog. Both faces are lit; light that comes
// through a thin leaf from behind gives a soft glow (cheap translucency).
Shader "Draho/WindFoliage"
{
    Properties
    {
        _BaseMap ("Colours", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind strength (m at the tip)", Float) = 0.05
        _WindSpeed ("Wind speed", Float) = 1.2
        _WindScale ("Gust size (1/m)", Float) = 0.35
        _Flutter ("Flutter (m)", Float) = 0.008
        _Translucency ("Translucency", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float _Flutter;
            half _Translucency;
        CBUFFER_END

        // A slow gust that rolls across the garden, plus a quick flutter per leaf.
        float3 Wind(float3 positionWS, float weight)
        {
            float t = _Time.y * _WindSpeed;
            float2 p = positionWS.xz * _WindScale;
            float gust = sin(t + p.x * 1.3 + p.y * 0.7) * 0.6 + sin(t * 2.3 + p.x * 2.1 - p.y * 1.7) * 0.4;
            gust = gust * 0.5 + 0.5; // the wind pushes one way, more or less
            float3 dir = normalize(float3(0.8, 0, 0.6));
            float flutter = sin(t * 7.0 + dot(positionWS, float3(5.1, 3.3, 4.7)));
            return (dir * gust * _WindStrength + float3(0.6, 0.3, -0.5) * flutter * _Flutter) * weight;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float weight = v.uv.y * v.uv.y;
                positionWS += Wind(positionWS, weight);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 n = normalize(i.normalWS) * (IS_FRONT_VFACE(face, 1, -1));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half ndl = dot(n, light.direction);
                half front = saturate(ndl * 0.75 + 0.25); // soft wrap: thin leaves are never fully dark
                half back = saturate(-ndl) * _Translucency;
                half3 lit = albedo * light.color * (front + back) * shadow;
                half3 ambient = albedo * SampleSH(n);
                half3 colour = MixFog(lit + ambient, i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                positionWS += Wind(positionWS, v.uv.y * v.uv.y);
                return TransformWorldToHClip(positionWS);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
