// Ground telegraph: the footprint an attack is about to cover, drawn flat on the floor.
//
// On a rigless capsule, colour alone cannot tell a narrow cleave from a wide sweep from a
// gap-closing slam — every wind-up looks identical. This draws the actual hitbox on the floor and,
// more importantly, fills it from the centre outward as the wind-up runs: when the fill reaches the
// outline, the hit lands. That communicates *timing*, not merely presence.
Shader "Monk/Telegraph"
{
    Properties
    {
        _Color("Colour", Color) = (1, 0.24, 0.18, 1)
        _Fill("Fill 0-1", Range(0, 1)) = 0
        _Alpha("Master Alpha", Range(0, 1)) = 1
        _EdgeWidth("Outline Width", Range(0.01, 0.4)) = 0.09
        _Rect("Rectangular", Range(0, 1)) = 0
        _ArcDegrees("Arc Width (deg, 360 = full)", Range(10, 360)) = 360
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
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha  // alpha, not additive: it must read on a light floor too
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex TelegraphVertex
            #pragma fragment TelegraphFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill;
                half _Alpha;
                half _EdgeWidth;
                half _Rect;
                half _ArcDegrees;
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

            Varyings TelegraphVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 TelegraphFragment(Varyings input) : SV_Target
            {
                float2 centred = (input.uv - 0.5) * 2.0;

                // Two distance metrics over the same quad: euclidean gives a disc, chebyshev gives
                // a rectangle. The quad is scaled to the hitbox, so both land on the real footprint.
                float radial = length(centred);
                float boxed = max(abs(centred.x), abs(centred.y));
                float d = lerp(radial, boxed, _Rect);

                if (d > 1.0)
                    discard;

                // Wedge clip. The quad is oriented so +v runs along the attacker's facing, so the
                // bearing of a fragment from the centre is measured against that axis. This is what
                // lets a swing read as a slice you can step out of instead of a bubble around the
                // attacker — and it matches the hitbox exactly, so the warning stays honest.
                if (_ArcDegrees < 359.0)
                {
                    float bearing = degrees(atan2(centred.x, centred.y));   // 0 = straight ahead
                    if (abs(bearing) > _ArcDegrees * 0.5)
                        discard;

                    // Soften the two straight edges so the slice does not alias into a hard fan.
                    float edgeFade = saturate((_ArcDegrees * 0.5 - abs(bearing)) / 4.0);
                    d = max(d, 1.0 - edgeFade * (1.0 - d));
                }

                // The outline is present for the whole wind-up: it is the promise of where the hit
                // will be, and it must be readable from the first frame.
                half edge = smoothstep(1.0 - _EdgeWidth, 1.0, d);

                // The fill is the clock. It reaches the outline exactly as the attack goes active.
                half fill = 1.0 - smoothstep(_Fill - 0.06, _Fill, d);

                half alpha = saturate(max(edge, fill * 0.42)) * _Alpha * _Color.a;

                // Brighten the leading edge of the fill so the sweep itself is trackable.
                half3 rgb = _Color.rgb * (1.0 + edge * 0.6);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
