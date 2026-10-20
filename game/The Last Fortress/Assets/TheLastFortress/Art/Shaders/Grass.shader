// Hero-view grass (plan 013): GPU-instanced clumps of blades drawn only around the hero camera, so the ground holds up
// at the hero's feet without costing anything in the strategic view. Vertex colour is the blade gradient (root to tip),
// uv.y the height along the blade for the sway. Lit like the ground (main light with shadows, ambient, fog); normals
// point up so a clump shades like the ground it grows from instead of flickering blade by blade.
Shader "TheLastFortress/Grass"
{
    Properties
    {
        _TintA ("Tint A", Color) = (0.9, 0.95, 0.82, 1)
        _TintB ("Tint B", Color) = (1.04, 1.0, 0.8, 1)
        _Sway ("Sway (tiles)", Float) = 0.006
        _SwaySpeed ("Sway speed", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _TintA, _TintB;
            float _Sway, _SwaySpeed;
        CBUFFER_END

        // Presentation clock shared with the water: the grass holds still while Night is paused or the menu is open.
        float _TlfWaterTime;

        float3 Sway(float3 positionWS, float along)
        {
            float t = _TlfWaterTime * _SwaySpeed;
            float phase = positionWS.x * 2.3 + positionWS.z * 1.7;
            float2 wind = float2(sin(t + phase), cos(t * 0.8 + phase * 1.3)) * _Sway * along * along;
            return positionWS + float3(wind.x, 0, wind.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 positionWS : TEXCOORD0;
                half3 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 world = Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.y);
                o.positionWS = world;
                o.positionCS = TransformWorldToHClip(world);
                // A stable tint per clump from where it stands (no per-instance data needed).
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                half hue = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.color = v.color.rgb * lerp(_TintA.rgb, _TintB.rgb, hue);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1));
                half3 lit = i.color * (SampleSH(half3(0, 1, 0)) + sun.color * saturate(sun.direction.y * 0.8 + 0.2) * sun.shadowAttenuation);
                #if defined(_ADDITIONAL_LIGHTS)
                // Torches and the Keep light the blades like the ground under them (otherwise moon-cool tufts on warm ground).
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light extra = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    lit += i.color * extra.color * extra.distanceAttenuation * 0.8;
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(lit, i.fogFactor), 1);
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
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.y));
            }

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
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz), v.uv.y));
            }

            half4 Frag() : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
