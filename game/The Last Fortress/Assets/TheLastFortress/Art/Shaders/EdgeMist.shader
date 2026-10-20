// Plan 016 edge mist: one full-screen pass after opaques and the sky (both cameras), blended over the frame, so it
// needs no copy of the scene colour and works on the multisampled target. Each pixel's world position comes from the
// depth texture; mist thickens with distance beyond the board's edge and thins with height, and the height it reaches
// grows with distance, so the hero view's horizon disappears into it while the sky above stays clear. Every opaque
// object (ground, rivers, enemies still walking in) is covered by the same rule, whatever its material.
Shader "Hidden/TheLastFortress/EdgeMist"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "EdgeMist"
            HLSLPROGRAM
            #pragma vertex VertMist
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct MistVaryings { float4 positionCS : SV_POSITION; };

            MistVaryings VertMist(uint id : SV_VertexID)
            {
                MistVaryings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                return o;
            }

            float4 _TlfMistColor;  // rgb mist, a strength (0 = no mist in this scene)
            float4 _TlfMistShape;  // x start and y full distance from the board edge, z base height, w height gained per tile
            float4 _TlfMistBoard;  // xy board min, zw board max (world x, z)
            float _TlfWaterTime;   // presentation clock: drift freezes with the night's pause

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(MistVaryings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                if (_TlfMistColor.a <= 0.0) return 0;

                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(uv));
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // Distance outside the board rectangle (0 on the board).
                float2 q = max(0.0, max(_TlfMistBoard.xy - world.xz, world.xz - _TlfMistBoard.zw));
                float d = length(q);

                // Slow drift: two octaves of value noise moving in different directions bend the mist's edge.
                float t = _TlfWaterTime;
                float n = Noise(world.xz * 0.32 + float2(t * 0.045, t * 0.02)) * 0.65
                        + Noise(world.xz * 0.9 - float2(t * 0.03, -t * 0.05)) * 0.35;
                float edge = d + (n - 0.5) * 1.6;
                float amount = smoothstep(_TlfMistShape.x, _TlfMistShape.y, edge);

                // Height: low near the board, rising with distance (a soft horizon in the hero view, clear sky above).
                float top = _TlfMistShape.z + _TlfMistShape.w * d;
                amount *= 1.0 - smoothstep(top * 0.55, top, world.y);

                half3 mist = _TlfMistColor.rgb * (0.94 + 0.12 * n);
                return half4(mist, saturate(amount * _TlfMistColor.a));
            }
            ENDHLSL
        }
    }
}
