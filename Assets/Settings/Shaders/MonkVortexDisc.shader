// The Undertow's inner vortex: a spiral drain drawn flat on the floor inside the pull radius.
//
// The foot-traced smear says "this is how far the pull reaches". This says "this is what it is
// doing" — a much smaller disc, deliberately well inside the smear so the two never compete for
// the same read. It is subtle at rest and flares when the spin feeds on a hit.
//
// Alpha-blended, NOT additive. The spec for this effect asked for additive, but this project has
// already learned that lesson the expensive way: both Monk/Smear and Monk/Telegraph carry a comment
// saying additive was tried and vanished against the light arena floor (M8). A drain the player
// cannot see on the ground they are standing on is worse than no drain.
//
// The scroll phase is pushed in from C# rather than read off _Time. That is what lets hitstop and
// the pause menu actually freeze it — an effect that keeps spinning through a frozen frame reads as
// a bug — and it is what lets a hit pulse kick the rotation speed.
Shader "Monk/VortexDisc"
{
    Properties
    {
        _Color("Colour", Color) = (0.29, 0.85, 0.92, 1)
        [HDR] _CoreColor("Core Colour", Color) = (0.75, 0.98, 1, 1)
        _Alpha("Base Alpha", Range(0, 1)) = 0.22
        _Pulse("Hit Pulse 0-1", Range(0, 1)) = 0
        _Phase("Scroll Phase", Float) = 0
        _Arms("Spiral Arms", Range(1, 6)) = 3
        _Tightness("Spiral Tightness", Range(0, 6)) = 1.6
        _InnerFade("Inner Fade", Range(0, 0.9)) = 0.18
        _RimFade("Rim Fade", Range(0.01, 0.9)) = 0.35
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
            Name "VortexDisc"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex VortexDiscVertex
            #pragma fragment VortexDiscFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _Alpha;
                half _Pulse;
                float _Phase;
                half _Arms;
                half _Tightness;
                half _InnerFade;
                half _RimFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings VortexDiscVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 VortexDiscFragment(Varyings input) : SV_Target
            {
                // Centred so the quad becomes a unit disc; the mesh is scaled to the real radius.
                float2 centred = (input.uv - 0.5) * 2.0;
                float r = length(centred);

                if (r > 1.0)
                    discard;

                float theta = atan2(centred.y, centred.x);
                const float TAU = 6.28318530718;

                // A spiral is just "angle plus a multiple of radius", wrapped. Adding _Phase moves
                // the stripes INWARD over time: the radius satisfying the constant falls as the
                // phase rises, which is the direction the pull is dragging things.
                float spiral = frac(_Arms * (theta / TAU) + _Tightness * r + _Phase);

                // Soft double-sided band so the arms read as smeared light rather than hard spokes.
                float band = smoothstep(0.5, 0.0, abs(spiral - 0.5));
                band = band * band;

                // Both ends need to let go: at the rim so the disc has no cut edge, and at the very
                // centre so the arms do not converge into a solid dot at the singularity.
                float rim = 1.0 - smoothstep(1.0 - _RimFade, 1.0, r);
                float inner = smoothstep(0.0, max(_InnerFade, 0.001), r);

                // The pulse is the whole point of the layer: it lifts alpha and pushes the colour
                // toward the hot core, so a spin eating a crowd is unmistakably brighter than one
                // spinning in empty air.
                half pulse = saturate(_Pulse);
                half alpha = _Alpha * (1.0 + pulse * 2.2) * band * rim * inner * _Color.a;

                half3 rgb = lerp(_Color.rgb, _CoreColor.rgb, pulse * 0.75);

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
