// Cel shading for URP: hard light bands plus a thin dark outline (DESIGN.md § Art direction).
//
// Hand-written rather than Flat Kit / Toony Colors Pro 2, both of which are paid Asset Store
// packages. This covers the MVP's needs — banded lighting, tinted shadows, rim light, inverted
// hull outline — and can be swapped for a bought shader later without touching gameplay code.
Shader "Monk/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Colour", Color) = (0.8, 0.8, 0.82, 1)

        [Header(Cel Shading)]
        _ShadowTint("Shadow Tint", Color) = (0.42, 0.45, 0.58, 1)
        _Bands("Light Bands", Range(2, 4)) = 3
        _BandSoftness("Band Softness", Range(0.001, 0.2)) = 0.015
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.75

        [Header(Rim)]
        _RimColor("Rim Colour", Color) = (1, 1, 1, 1)
        _RimStrength("Rim Strength", Range(0, 1)) = 0.18
        _RimPower("Rim Power", Range(0.5, 8)) = 3

        [Header(Outline)]
        _OutlineColor("Outline Colour", Color) = (0.05, 0.05, 0.08, 1)
        // Width is multiplied by view depth to keep constant screen thickness, so the useful
        // range is small: at a 13 m camera distance, 0.002 is already a ~26 mm shell.
        _OutlineWidth("Outline Width", Range(0, 0.008)) = 0.0022
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ---------------------------------------------------------------------------------
        // Outline: inverted hull. Rendered first with front faces culled and the shell pushed
        // out along the normal, so only the back-facing shell survives around the silhouette.
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half _Bands;
                half _BandSoftness;
                half _ShadowStrength;
                half4 _RimColor;
                half _RimStrength;
                half _RimPower;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct OutlineVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output;

                // Widen in view space so the outline keeps a roughly constant screen thickness
                // instead of ballooning on large meshes.
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), true);
                positionVS += normalVS * _OutlineWidth * -positionVS.z;

                output.positionHCS = TransformWViewToHClip(positionVS);
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        // Lit pass: quantise the main light into hard bands.
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half _Bands;
                half _BandSoftness;
                half _ShadowStrength;
                half4 _RimColor;
                half _RimStrength;
                half _RimPower;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Quantises 0..1 into hard steps with a narrow soft edge, which is what separates
            // cel shading from a plain lambert term.
            half Posterise(half value, half bands, half softness)
            {
                half scaled = saturate(value) * bands;
                half stepIndex = floor(scaled);
                half fraction = scaled - stepIndex;
                half edge = smoothstep(0.5 - softness, 0.5 + softness, fraction);
                return saturate((stepIndex + edge) / bands);
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = dot(normalWS, mainLight.direction) * 0.5 + 0.5;
                half banded = Posterise(ndotl * mainLight.shadowAttenuation, _Bands, _BandSoftness);

                // Shadowed areas take a tint rather than going black, which keeps the limited
                // palette readable instead of muddy.
                half3 shaded = lerp(albedo.rgb * _ShadowTint.rgb, albedo.rgb, lerp(1.0h, banded, _ShadowStrength));
                shaded *= mainLight.color;

                // Ambient, so unlit faces still read as their palette colour.
                shaded += albedo.rgb * SampleSH(normalWS) * 0.35;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0 - saturate(dot(normalWS, viewDir))), _RimPower);
                shaded += _RimColor.rgb * rim * _RimStrength;

                shaded = MixFog(shaded, input.fogFactor);
                return half4(shaded, albedo.a);
            }
            ENDHLSL
        }

        // Shadow casting, so characters and geometry still drop shadows.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half _Bands;
                half _BandSoftness;
                half _ShadowStrength;
                half4 _RimColor;
                half _RimStrength;
                half _RimPower;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth prepass / depth-normals, so URP features that need them keep working.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half _Bands;
                half _BandSoftness;
                half _ShadowStrength;
                half4 _RimColor;
                half _RimStrength;
                half _RimPower;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
