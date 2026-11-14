// Short fur for the cats: shell texturing. Hand-written URP shader (no Shader Graph: the scenes are built in code).
// The mesh (DrahomilFur, made by Tools/Blender/convert_lowpoly_cat.py) holds several copies of the fur surface,
// all at the skin. Each copy knows its layer (uv0.x: 0 = skin, 1 = hair tips), the skin's rest position in
// metres (uv1.xy and uv0.y) and the fur length there (uv2.x: 0 round the eyes and nose, 1 on the body).
// The vertex shader pushes every layer out along the normal; the fragment shader cuts each layer into
// strand dots that get thinner towards the tip, so the layers stack into hairs.
// The strand pattern comes from the rest position, so it needs no texture and no UV unwrap, and it does not
// swim when the cat moves. It is a 2D grid seen along the rest normal's main axis: a 3D grid drew contour
// rings where the curved skin cut through its cells.
// Lighting: main light with shadows, extra lights (with rendering layers: the cat's rim light), ambient
// probe (SH), fog. No geometry shader: it runs on WebGL.
Shader "Draho/ShellFur"
{
    Properties
    {
        _BaseColor ("Fur colour", Color) = (0.16, 0.17, 0.2, 1)
        _TipColor ("Tip tint (multiplied at the hair tips)", Color) = (1.25, 1.25, 1.32, 1)
        _FurLength ("Fur length (m)", Float) = 0.009
        _Density ("Strands per metre", Float) = 420
        _Thickness ("Strand thickness at the root (0..1 of a cell)", Range(0.2, 1.2)) = 0.95
        _MinLength ("Shortest strand (0..1 of the length)", Range(0, 1)) = 0.45
        _RootShade ("Shade at the roots", Range(0, 1)) = 0.65
        _Droop ("Droop (0..1 of the length, at the tip)", Range(0, 1)) = 0.25
        _Rim ("Soft rim sheen", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                float _FurLength;
                float _Density;
                half _Thickness;
                half _MinLength;
                half _RootShade;
                half _Droop;
                half _Rim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0; // x: layer 0..1, y: rest z
                float2 uv1 : TEXCOORD1; // rest x, y
                float2 uv2 : TEXCOORD2; // x: fur length 0..1
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 restAndLayer : TEXCOORD0; // xyz: rest position (m), w: layer
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half2 fogAndMask : TEXCOORD3; // x: fog factor, y: fur length 0..1
            };

            float Hash(float2 cell, float seed)
            {
                float3 p = frac(float3(cell.xyx) * 0.1031 + seed);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float layer = v.uv0.x;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                // Out along the normal; the tips hang down a little (more for the outer layers).
                float furLength = _FurLength * v.uv2.x;
                positionWS += normalWS * (furLength * layer);
                positionWS.y -= furLength * _Droop * layer * layer;
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                o.restAndLayer = float4(v.uv1.x, v.uv1.y, v.uv0.y, layer);
                o.fogAndMask = half2(ComputeFogFactor(o.positionCS.z), v.uv2.x);
                return o;
            }

            half3 Diffuse(Light light, half3 n)
            {
                half wrap = saturate(dot(n, light.direction) * 0.6 + 0.4); // fur scatters: a soft terminator
                return light.color * (wrap * light.distanceAttenuation * light.shadowAttenuation);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float layer = i.restAndLayer.w;
                clip(i.fogAndMask.y - 0.12); // (almost) no fur here: the skin shows, and the face parts stay clear
                // One strand per cell of a 2D grid in rest space, seen along the main axis of the rest normal.
                // All layers have the same rest position, so the dots stack along the normal.
                // A strand stands a little off the middle of its cell, ends at its own length and tapers to a point.
                float3 rest = i.restAndLayer.xyz;
                float3 axis = abs(cross(ddx(rest), ddy(rest)));
                float3 p = rest * _Density;
                float2 q = p.xy;
                float seed = 0.0;
                if (axis.x > axis.y && axis.x > axis.z) { q = p.yz; seed = 0.37; }
                else if (axis.y > axis.z) { q = p.xz; seed = 0.71; }
                float2 cell = floor(q);
                float2 offset = frac(q) - 0.5;
                offset -= (float2(Hash(cell, seed + 0.11), Hash(cell, seed + 0.23)) - 0.5) * 0.45;
                float strandLength = lerp(_MinLength, 1.0, Hash(cell, seed));
                float along = layer / strandLength;              // 0 at the root, 1 at this strand's tip
                float radius = length(offset) * 2.0;             // 0 at the strand's middle
                clip(_Thickness * (1.0 - along) - radius);

                half3 n = normalize(i.normalWS);
                half3 albedo = _BaseColor.rgb * lerp(_RootShade, 1.0, layer) * lerp(half3(1, 1, 1), _TipColor.rgb, layer * layer);

                half3 light = Diffuse(GetMainLight(TransformWorldToShadowCoord(i.positionWS)), n);
                #ifdef _ADDITIONAL_LIGHTS
                uint meshLayers = GetMeshRenderingLayer();
                uint count = GetAdditionalLightsCount();
                for (uint k = 0u; k < count; ++k)
                {
                    Light extra = GetAdditionalLight(k, i.positionWS);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(extra.layerMask, meshLayers))
                    #endif
                    {
                        light += Diffuse(extra, n);
                    }
                }
                #endif
                half3 ambient = SampleSH(n);
                // Hair tips at a glancing angle catch the sky: a soft edge, so the dark cat reads on dark floors.
                half3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0 - saturate(dot(n, view)), 3.0) * _Rim * layer;
                half3 colour = albedo * (light + ambient) + rim * ambient;
                return half4(MixFog(colour, i.fogAndMask.x), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
