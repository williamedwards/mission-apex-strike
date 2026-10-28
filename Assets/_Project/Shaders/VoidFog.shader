// Darkness that thickens downward inside a box: drawn on a unit cube over a lethal floor, it
// reads the scene's depth, finds the surface behind each pixel, and darkens it by how far down
// the box it is (clear at the top, black at the bottom). Pit walls, the player falling and
// anything passing through all sink into the void; nothing outside the box is touched.
// Back faces with ZTest Always, so it covers its area whether the camera is outside or inside.
Shader "GravBoots/VoidFog"
{
    Properties
    {
        _Color ("Void colour", Color) = (0, 0, 0, 1)
        _Fade ("Fade (share of the height that goes from clear to dark)", Range(0.05, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "VoidFog"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                // The box is a unit cube: inside is -0.5..0.5 on each axis.
                float3 local = TransformWorldToObject(world);
                if (abs(local.x) > 0.5 || abs(local.z) > 0.5 || local.y > 0.5)
                    discard;
                // 0 at the top of the box, 1 a fade's height below it; below the box stays black.
                float down = saturate((0.5 - local.y) / _Fade);
                half alpha = smoothstep(0, 1, down) * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
