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
                float _DistanceFogStart;
                float _DistanceFogEnd;
                float _RadialFadeStart;
                float _RadialFadeEnd;
                float4 _ScrollOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 worldUv = input.positionWS.xz * _WorldTiling;
                float2 paintedUv = float2(
                    worldUv.x * max(0.05, _BandStretch) + worldUv.y * _BandSlant,
                    worldUv.y * max(0.05, _DetailScale)) + _ScrollOffset.xy;

                half3 painted = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, paintedUv).rgb;
                half luminance = dot(painted, half3(0.299, 0.587, 0.114));
                half whiteness = min(painted.r, min(painted.g, painted.b));
                half rawCloud = saturate(luminance * 0.78 + whiteness * 0.3);
                half thinCloud = smoothstep(_CloudCoverage - _CloudFeather * 1.1, _CloudCoverage, rawCloud);
                half cloudCore = smoothstep(_CloudCoverage, _CloudCoverage + _CloudFeather, rawCloud);
                half highlight = smoothstep(_CloudCoverage + _CloudFeather * 0.35, 1.0, rawCloud);

                half3 skyColor = lerp(_VoidColor.rgb, painted, 0.72);
                half3 thinColor = lerp(_ThinCloudColor.rgb, painted, 0.58);
                half3 coreColor = lerp(_BaseColor.rgb, _HighlightColor.rgb, highlight * 0.5);
                half3 color = lerp(skyColor, thinColor, thinCloud * 0.68);
                color = lerp(color, coreColor, cloudCore * 0.78);
                float cameraDistance = distance(GetCameraPositionWS(), input.positionWS);
                half fog = smoothstep(0.0, 1.0, saturate((cameraDistance - _DistanceFogStart) / max(1.0, _DistanceFogEnd - _DistanceFogStart)));
                half radialFog = smoothstep(_RadialFadeStart, _RadialFadeEnd, length(input.positionWS.xz));
                half edgeAlpha = 1.0 - radialFog;

                color = lerp(color, _FogColor.rgb, saturate(max(fog * 0.35, radialFog * 0.55)));
                return half4(color, edgeAlpha);
            }
            ENDHLSL
        }
    }
}
