// Stylized water for the sea and the rivers (plan 013). Opaque: the depth below the surface comes from the mesh (uv2.x =
// metres of water under this vertex, written by the view from the same height field as the ground), so shallows, shore
// foam and the deep tint need no depth texture and look the same from the strategic and the hero camera. Two scrolling
// ripple normals drive a sun glint and a sky-coloured fresnel; the view sets the colours for day and night.
Shader "TheLastFortress/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.36, 0.62, 0.62, 1)
        _DeepColor ("Deep", Color) = (0.12, 0.3, 0.42, 1)
        _FoamColor ("Foam", Color) = (0.92, 0.95, 0.93, 1)
        _SkyColor ("Sky reflection", Color) = (0.7, 0.8, 0.88, 1)
        [NoScaleOffset] _NormalMap ("Ripple normal", 2D) = "bump" {}
        _NormalTiles ("Ripple repeat (tiles)", Float) = 3.1
        _NormalScale ("Ripple strength", Range(0, 2)) = 0.55
        _Flow ("Flow (xy tiles/s, zw second layer)", Vector) = (0.035, 0.02, -0.025, 0.03)
        _DepthRange ("Depth to full deep colour", Float) = 0.6
        _FoamWidth ("Foam depth", Float) = 0.035
        _Gloss ("Glint sharpness", Float) = 180
        _Glint ("Glint strength", Range(0, 4)) = 1.2
        _Reflect ("Sky reflection strength", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor, _DeepColor, _FoamColor, _SkyColor;
            float _NormalTiles;
            half _NormalScale;
            float4 _Flow;
            float _DepthRange, _FoamWidth, _Gloss;
            half _Glint, _Reflect;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            // Presentation clock (seconds), advanced by the view so ripples freeze with pause and the menu.
            float _TlfWaterTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 depth : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float depth : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.depth = v.depth.x;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz / _NormalTiles;
                float t = _TlfWaterTime;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + _Flow.xy * t), _NormalScale);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * 1.73 + _Flow.zw * t), _NormalScale);
                half3 n = normalize(half3(a.x + b.x, 1.0, a.y + b.y));
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Depth tint with a little ripple breakup, and a foam band along the shore that wobbles with the ripples.
                half depth01 = saturate(i.depth / _DepthRange);
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, depth01));
                half foamEdge = i.depth + (a.x + b.y) * 0.02;
                half foam = (1.0 - smoothstep(_FoamWidth * 0.35, _FoamWidth, foamEdge)) * step(0.0005, i.depth + 0.02);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1));
                half shadow = sun.shadowAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half ndl = saturate(dot(n, sun.direction));
                half3 lit = water * (ambient + sun.color * (0.35 + 0.65 * ndl) * lerp(0.55, 1.0, shadow));

                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4.0);
                lit = lerp(lit, _SkyColor.rgb, fresnel * _Reflect);
                half3 h = normalize(sun.direction + viewDir);
                lit += sun.color * pow(saturate(dot(n, h)), _Gloss) * _Glint * shadow;
                lit = lerp(lit, _FoamColor.rgb * (ambient + sun.color * lerp(0.6, 1.0, shadow)), foam * _FoamColor.a);

                lit = MixFog(lit, i.fogFactor);
                return half4(lit, 1);
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

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag() : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
