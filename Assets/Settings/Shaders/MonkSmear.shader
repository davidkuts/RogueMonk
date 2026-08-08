// A ribbon of smeared time, for the Undertow's spin.
//
// Full-body afterimages are right for the dash, where the body travels, and wrong for a spin,
// where the torso stays put and every ghost stacks into a blob in the middle. The only thing
// actually sweeping is the leg, so that is what gets drawn: a trail traced by the feet.
//
// Alpha-blended rather than additive for the same reason the dash ghosts are — the arena floor
// is light, and additive vanished against it (M8). Soft across the ribbon's width so the edges
// feather instead of reading as a flat cutout.
Shader "Monk/Smear"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (0.29, 0.85, 0.92, 1)
        [HDR] _CoreColor ("Core Colour", Color) = (0.75, 0.98, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.55
        _CoreWidth ("Core Width", Range(0.0, 1)) = 0.35
        _HeadBoost ("Head Brightness", Range(1, 4)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Smear"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CoreColor;
                float  _EdgeSoftness;
                float  _CoreWidth;
                float  _HeadBoost;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // v runs across the ribbon: 0 and 1 are the edges, 0.5 the spine.
                float across = abs(IN.uv.y - 0.5) * 2.0;

                // Feather the edges, then lay a brighter core down the middle so the trail reads
                // as a lit streak rather than a flat band of colour.
                float body = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);
                float core = 1.0 - smoothstep(0.0, max(_CoreWidth, 0.001), across);

                // u runs along the trail, 0 at the newest point. The freshest part of the sweep is
                // the brightest, which is what gives the smear a direction to read.
                float head = lerp(1.0, _HeadBoost, saturate(1.0 - IN.uv.x));

                half3 rgb = lerp(_BaseColor.rgb, _CoreColor.rgb, core) * head;
                half alpha = _BaseColor.a * IN.color.a * body;

                return half4(rgb * IN.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
