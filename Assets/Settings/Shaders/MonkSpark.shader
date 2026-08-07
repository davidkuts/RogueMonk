// Impact flash: a camera-facing quad with a soft radial falloff.
//
// Separate from Monk/Ghost because that shader shades by silhouette rim, which on a flat quad
// produced a hard-edged square. A spark needs the opposite: brightest in the middle, fading to
// nothing at the edge, so the quad's own shape never reads.
Shader "Monk/Spark"
{
    Properties
    {
        _GhostColor("Colour", Color) = (1, 0.85, 0.45, 0.9)
        _CoreSize("Core Size", Range(0.01, 0.5)) = 0.14
        _Falloff("Falloff", Range(0.5, 6)) = 2.2
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
            Name "Spark"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One      // additive: a flash adds light rather than tinting
            ZWrite Off
            Cull Off                // visible whichever way the quad happens to face

            HLSLPROGRAM
            #pragma vertex SparkVertex
            #pragma fragment SparkFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GhostColor;
                half _CoreSize;
                half _Falloff;
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

            Varyings SparkVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 SparkFragment(Varyings input) : SV_Target
            {
                // Distance from the quad centre, 0 at the middle and 1 at the edge midpoints.
                float dist = length(input.uv - 0.5) * 2.0;
                half falloff = saturate(1.0 - dist);
                half intensity = pow(falloff, _Falloff);

                // A small solid core keeps the flash from looking like a soft blob.
                intensity += smoothstep(_CoreSize, 0.0, dist) * 0.6;

                half alpha = saturate(intensity) * _GhostColor.a;
                return half4(_GhostColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
