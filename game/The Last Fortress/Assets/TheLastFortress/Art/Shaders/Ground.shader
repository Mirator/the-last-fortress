// The island ground (plan 013): one mesh from the board out to the beach, lit by URP like Lit (main and additional lights,
// shadows, SSAO, fog). Materials are blended per vertex: colour r = soil, g = sand, b = rock, a = lush grass (0 = dry).
// Grass and soil tile in world space at two scales (macro patches and close-range detail) so the board reads as one meadow
// from the strategic camera and still has blades and grit at the hero's feet. Rock uses a side projection on steep faces.
Shader "TheLastFortress/Ground"
{
    Properties
    {
        [NoScaleOffset] _GrassMap ("Grass (macro)", 2D) = "white" {}
        [NoScaleOffset] _GrassDetail ("Grass (detail)", 2D) = "white" {}
        [NoScaleOffset] _SoilMap ("Soil", 2D) = "white" {}
        [NoScaleOffset] _SandMap ("Sand", 2D) = "white" {}
        [NoScaleOffset] _RockMap ("Rock", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Detail normal", 2D) = "bump" {}
        _LushTint ("Lush grass tint", Color) = (0.8, 0.9, 0.7, 1)
        _DryTint ("Dry grass tint", Color) = (1.0, 0.95, 0.72, 1)
        _SoilTint ("Soil tint", Color) = (0.82, 0.68, 0.52, 1)
        _SandTint ("Sand tint", Color) = (0.98, 0.9, 0.72, 1)
        _RockTint ("Rock tint", Color) = (0.74, 0.72, 0.68, 1)
        _MacroTiles ("Macro repeat (tiles)", Float) = 9.3
        _DetailTiles ("Detail repeat (tiles)", Float) = 2.37
        _RockTiles ("Rock repeat (tiles)", Float) = 1.6
        _DetailStrength ("Detail strength", Range(0, 1)) = 0.55
        _NormalScale ("Detail normal strength", Range(0, 2)) = 0.22
        _Smoothness ("Smoothness", Range(0, 1)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _LushTint, _DryTint, _SoilTint, _SandTint, _RockTint;
            float _MacroTiles, _DetailTiles, _RockTiles;
            half _DetailStrength, _NormalScale, _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_GrassDetail);
            TEXTURE2D(_SoilMap);
            TEXTURE2D(_SandMap);
            TEXTURE2D(_RockMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 weights : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.weights = v.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Luma(half3 c) { return dot(c, half3(0.3, 0.59, 0.11)); }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                half3 n = normalize(i.normalWS);
                half steep = saturate((1.0 - n.y) * 2.2);

                // Grass: macro patches times a close-range detail layer whose strength fades with distance (no shimmer).
                half3 macro = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, xz / _MacroTiles).rgb;
                half3 detail = SAMPLE_TEXTURE2D(_GrassDetail, sampler_GrassMap, xz / _DetailTiles).rgb;
                half3 grass = macro * lerp(half3(1, 1, 1), detail * 2.0, _DetailStrength);
                grass *= lerp(_DryTint.rgb, _LushTint.rgb, i.weights.a);

                half3 soil = SAMPLE_TEXTURE2D(_SoilMap, sampler_GrassMap, xz / 1.9).rgb * _SoilTint.rgb;
                half3 sand = SAMPLE_TEXTURE2D(_SandMap, sampler_GrassMap, xz / 2.6).rgb * _SandTint.rgb;
                float2 side = abs(n.x) > abs(n.z) ? i.positionWS.zy : i.positionWS.xy;
                half3 rockTop = SAMPLE_TEXTURE2D(_RockMap, sampler_GrassMap, xz / _RockTiles).rgb;
                half3 rockSide = SAMPLE_TEXTURE2D(_RockMap, sampler_GrassMap, side / _RockTiles * float2(1, 1.8)).rgb;
                half3 rock = lerp(rockTop, rockSide, steep) * _RockTint.rgb;

                // Height-aware blending: soil and sand show first in the low spots of the grass, so edges look worn, not faded.
                half grassHeight = Luma(detail);
                half wSoil = saturate((i.weights.r - (grassHeight - 0.45) * 0.8) * 2.2);
                half wSand = saturate((i.weights.g - (grassHeight - 0.45) * 0.6) * 2.4);
                half wRock = saturate(max(i.weights.b, steep * 1.15 - 0.15) * 1.6);
                half3 albedo = grass;
                albedo = lerp(albedo, soil, wSoil);
                albedo = lerp(albedo, sand, wSand);
                albedo = lerp(albedo, rock, wRock);

                half3 bump = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, xz / _DetailTiles), _NormalScale * (1.0 - wRock * 0.5));
                half3 normalWS = normalize(half3(n.x + bump.x, n.y, n.z + bump.y));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = _Smoothness + wSand * 0.08;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };

            Varyings Vert(float4 positionOS : POSITION, float3 normalOS : NORMAL)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
