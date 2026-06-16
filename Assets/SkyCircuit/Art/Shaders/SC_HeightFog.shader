Shader "SkyCircuit/Height Fog"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _CloudColor ("Cloud Sea Color", Color) = (0.78, 0.88, 0.92, 1)
        _HorizonColor ("Horizon Mist Color", Color) = (0.62, 0.78, 0.9, 1)
        _SkyColor ("Sky Color", Color) = (0.47, 0.66, 0.86, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.42
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.22
        _HorizonBottom ("Horizon Bottom", Float) = -70
        _HorizonTop ("Horizon Top", Float) = 330
        _DistanceFadeStart ("Distance Fade Start", Float) = 700
        _DistanceFadeEnd ("Distance Fade End", Float) = 3300
        _WorldTiling ("World Tiling", Float) = 0.0016
        _ScrollOffset ("Scroll Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HeightFog"

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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColor;
                half4 _HorizonColor;
                half4 _SkyColor;
                float _Alpha;
                float _NoiseStrength;
                float _HorizonBottom;
                float _HorizonTop;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                float _WorldTiling;
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
                float cameraDistance = distance(GetCameraPositionWS(), input.positionWS);
                half distanceFade = smoothstep(0.0, 1.0, saturate((cameraDistance - _DistanceFadeStart) / max(1.0, _DistanceFadeEnd - _DistanceFadeStart)));
                half height01 = saturate((input.positionWS.y - _HorizonBottom) / max(1.0, _HorizonTop - _HorizonBottom));
                half lowerFade = smoothstep(0.0, 0.18, height01);
                half upperFade = 1.0 - smoothstep(0.62, 1.0, height01);
                half horizonBand = lowerFade * upperFade;

                float2 uv = input.positionWS.xz * _WorldTiling + _ScrollOffset.xy;
                float2 highUv = input.positionWS.xz * (_WorldTiling * 2.35) + _ScrollOffset.zw;
                half3 noiseSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).rgb;
                half3 highNoiseSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, highUv).rgb;
                half broadNoise = smoothstep(0.16, 0.88, dot(noiseSample, half3(0.299, 0.587, 0.114)));
                half highNoise = smoothstep(0.28, 0.92, dot(highNoiseSample, half3(0.3, 0.46, 0.24)));
                half noiseAlpha = lerp(1.0, broadNoise * 0.75 + highNoise * 0.25, _NoiseStrength);
                half alpha = saturate(_Alpha * distanceFade * horizonBand * noiseAlpha);

                half3 lowerColor = lerp(_CloudColor.rgb, _HorizonColor.rgb, smoothstep(0.0, 0.48, height01));
                half3 color = lerp(lowerColor, _SkyColor.rgb, smoothstep(0.38, 1.0, height01));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
