// Unlit transparent shell for dash afterimages.
//
// Separate from Monk/Toon because a ghost must not be lit, outlined or shadowed — it is a
// motion cue, not an object in the world.
//
// _GhostColor is a plain material property set per-renderer through a MaterialPropertyBlock,
// the same way the enemy telegraph tint works. An earlier version read it from the GPU
// instancing buffer instead, which returned zero alpha when driven by a property block and
// made every ghost invisible.
Shader "Monk/Ghost"
{
    Properties
    {
        _GhostColor("Ghost Colour", Color) = (0.29, 0.85, 0.92, 0.55)
        _RimPower("Edge Power", Range(0.5, 8)) = 2.5
        _RimBoost("Edge Boost", Range(0, 3)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }

            // Alpha blended, not additive: the arena floor is light, and additive ghosts
            // washed out against it almost completely.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex GhostVertex
            #pragma fragment GhostFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GhostColor;
                half _RimPower;
                half _RimBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings GhostVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 GhostFragment(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - saturate(dot(input.normalWS, viewDir))), _RimPower);

                // Brighter at the silhouette so a ghost reads as an outline of the body rather
                // than a solid blob.
                half3 colour = _GhostColor.rgb * (1.0 + rim * _RimBoost);
                return half4(colour, _GhostColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
