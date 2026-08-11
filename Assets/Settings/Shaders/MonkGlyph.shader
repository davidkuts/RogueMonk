// Flat unlit glyph geometry: damage numbers, and anything else built out of blocks that has to
// be READ rather than looked at.
//
// Three deliberate choices.
//
// Unlit, because a number is UI that happens to live in the world — a digit that dims when the
// player walks into shadow is a digit that stops being legible for reasons the player cannot act
// on.
//
// ZTest Always, so numbers draw over the bodies they describe. A damage number occluded by the
// enemy it belongs to is worse than no number: it flickers in and out as the body moves, which
// reads as a rendering fault rather than as information.
//
// _BaseColor as a plain material property, set per-instance through a MaterialPropertyBlock — the
// same route the enemy tints and the reward icons take. Monk/Ghost records what happens if you
// reach for the GPU instancing buffer instead: it returns zero alpha and every glyph vanishes.
Shader "Monk/Glyph"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (1, 1, 1, 1)
        _Boost ("Brightness", Range(1, 3)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "Glyph"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex GlyphVertex
            #pragma fragment GlyphFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Boost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings GlyphVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 GlyphFragment(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb * _Boost, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
