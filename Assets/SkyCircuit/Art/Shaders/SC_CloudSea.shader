Shader "SkyCircuit/Cloud Sea"
{
    Properties
    {
        _CloudTex ("Cloud Texture", 2D) = "white" {}
        _VoidColor ("Open Sky Color", Color) = (0.33, 0.58, 0.82, 1)
        _ThinCloudColor ("Thin Cloud Color", Color) = (0.72, 0.83, 0.9, 1)
        _BaseColor ("Cloud Color", Color) = (0.93, 0.96, 0.96, 1)
        _HighlightColor ("Cloud Highlight", Color) = (1, 1, 0.98, 1)
        _FogColor ("Fog Color", Color) = (0.58, 0.72, 0.86, 1)
        _WorldTiling ("World Tiling", Float) = 0.00016
        _BandStretch ("Band Stretch", Float) = 1
        _BandSlant ("Band Slant", Float) = 0.18
        _DetailScale ("Detail Scale", Float) = 0.85
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudFeather ("Cloud Feather", Range(0.01, 0.5)) = 0.24
        _CenterThinRadius ("Center Thin Radius", Float) = 950
        _EdgeThickStart ("Edge Thick Start", Float) = 1250
        _EdgeThickEnd ("Edge Thick End", Float) = 3500
        _CenterThinness ("Center Thinness", Range(0, 1)) = 0.38
        _EdgeDensityBoost ("Edge Density Boost", Range(0, 1)) = 0.3
        _EdgeOpacityBoost ("Edge Opacity Boost", Range(0, 1)) = 0.35
        _SurfaceLift ("Cloud Surface Lift", Float) = 24
        _EdgeLift ("Edge Cloud Lift", Float) = 72
        _CenterLowering ("Center Cloud Lowering", Float) = 20
        _DistanceFogStart ("Distance Fog Start", Float) = 1600
        _DistanceFogEnd ("Distance Fog End", Float) = 5200
        _RadialFadeStart ("Radial Fade Start", Float) = 2400
        _RadialFadeEnd ("Radial Fade End", Float) = 3450
        _ScrollOffset ("Scroll Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "CloudSea"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
                half4 _VoidColor;
                half4 _ThinCloudColor;
                half4 _BaseColor;
                half4 _HighlightColor;
                half4 _FogColor;
                float _WorldTiling;
                float _BandStretch;
                float _BandSlant;
                float _DetailScale;
                float _CloudCoverage;
                float _CloudFeather;
                float _CenterThinRadius;
                float _EdgeThickStart;
                float _EdgeThickEnd;
                float _CenterThinness;
                float _EdgeDensityBoost;
                float _EdgeOpacityBoost;
                float _SurfaceLift;
                float _EdgeLift;
                float _CenterLowering;
                float _DistanceFogStart;
                float _DistanceFogEnd;
                float _RadialFadeStart;
                float _RadialFadeEnd;
                float4 _ScrollOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float radius = length(positionWS.xz);
                float edgeMass = smoothstep(_EdgeThickStart, _EdgeThickEnd, radius);
                float centerThin = 1.0 - smoothstep(_CenterThinRadius, _EdgeThickStart, radius);

                float2 shapeUv = positionWS.xz * (_WorldTiling * 0.72) + _ScrollOffset.xy * 0.35;
                half3 shapeSample = SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, shapeUv, 0).rgb;
                half shape = smoothstep(0.28, 0.86, dot(shapeSample, half3(0.36, 0.48, 0.16)));
                positionWS.y += shape * _SurfaceLift + edgeMass * _EdgeLift - centerThin * _CenterLowering;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 worldUv = input.positionWS.xz * _WorldTiling;
                float radius = length(input.positionWS.xz);
                half edgeMass = smoothstep(_EdgeThickStart, _EdgeThickEnd, radius);
                half centerThin = 1.0 - smoothstep(_CenterThinRadius, _EdgeThickStart, radius);

                float2 paintedUv = float2(
                    worldUv.x * max(0.05, _BandStretch) + worldUv.y * _BandSlant,
                    worldUv.y * max(0.05, _DetailScale)) + _ScrollOffset.xy;
                float2 detailUv = paintedUv * 2.15 + _ScrollOffset.zw;

                half3 painted = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, paintedUv).rgb;
                half3 detail = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, detailUv).rgb;
                half luminance = dot(painted, half3(0.299, 0.587, 0.114));
                half detailShape = dot(detail, half3(0.2, 0.62, 0.18));
                half whiteness = min(max(painted.r, detail.r), min(max(painted.g, detail.g), max(painted.b, detail.b)));
                half rawCloud = saturate(luminance * 0.68 + detailShape * 0.22 + whiteness * 0.22);
                rawCloud = saturate(rawCloud - centerThin * (_CenterThinness * 0.34) + edgeMass * (_EdgeDensityBoost * 0.55));

                half radialCoverage = saturate(_CloudCoverage + centerThin * _CenterThinness - edgeMass * _EdgeDensityBoost);
                half thinCloud = smoothstep(radialCoverage - _CloudFeather * 1.15, radialCoverage, rawCloud);
                half cloudCore = smoothstep(radialCoverage, radialCoverage + _CloudFeather, rawCloud);
                half highlight = smoothstep(radialCoverage + _CloudFeather * 0.3, 1.0, rawCloud);

                half3 skyColor = lerp(_VoidColor.rgb, painted, 0.72);
                half3 thinColor = lerp(_ThinCloudColor.rgb, painted, 0.58);
                half3 coreColor = lerp(_BaseColor.rgb, _HighlightColor.rgb, highlight * 0.5);
                half3 color = lerp(skyColor, thinColor, thinCloud * lerp(0.46, 0.82, edgeMass));
                color = lerp(color, coreColor, cloudCore * lerp(0.52, 0.95, edgeMass));
                color = lerp(color, _HighlightColor.rgb, highlight * edgeMass * 0.28);

                half thickness = saturate(thinCloud * 0.26 + cloudCore * 0.72 + edgeMass * _EdgeOpacityBoost - centerThin * 0.22);
                float cameraDistance = distance(GetCameraPositionWS(), input.positionWS);
                half fog = smoothstep(0.0, 1.0, saturate((cameraDistance - _DistanceFogStart) / max(1.0, _DistanceFogEnd - _DistanceFogStart)));
                half radialFog = smoothstep(_RadialFadeStart, _RadialFadeEnd, radius);
                half outerFade = 1.0 - radialFog;
                half alpha = saturate((0.18 + thickness * 0.82) * lerp(0.56, 1.0, edgeMass) * outerFade);

                color = lerp(color, _FogColor.rgb, saturate(max(fog * 0.35, radialFog * 0.55)));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
