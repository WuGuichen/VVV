Shader "WGame/Skybox/TrainingYardDusk"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.11, 0.13, 0.29, 1)
        _SkyColor ("Upper Sky", Color) = (0.30, 0.29, 0.48, 1)
        _HorizonColor ("Horizon", Color) = (0.80, 0.47, 0.36, 1)
        _GroundColor ("Below Horizon", Color) = (0.31, 0.27, 0.36, 1)
        _CloudLightColor ("Cloud Light", Color) = (0.96, 0.57, 0.38, 1)
        _CloudShadowColor ("Cloud Shadow", Color) = (0.42, 0.34, 0.52, 1)
        _SunGlowColor ("Sun Glow", Color) = (1, 0.45, 0.18, 1)
        _SunDirection ("Sun Direction", Vector) = (0.35, 0.12, 0.93, 0)
        _SunGlowStrength ("Sun Glow Strength", Range(0, 1)) = 0.28
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudContrast ("Cloud Contrast", Range(1, 8)) = 4
        _CloudScale ("Cloud Scale", Range(1, 16)) = 5
        _Exposure ("Exposure", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _SkyColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _CloudLightColor;
                half4 _CloudShadowColor;
                half4 _SunGlowColor;
                float4 _SunDirection;
                half _SunGlowStrength;
                half _CloudCoverage;
                half _CloudContrast;
                half _CloudScale;
                half _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 blend = frac(p);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float a = lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), blend.x);
                float b = lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), blend.x);
                float c = lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), blend.x);
                float d = lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + float3(1, 1, 1)), blend.x);
                return lerp(lerp(a, b, blend.y), lerp(c, d, blend.y), blend.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionWS);
                float height = direction.y;
                half3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, 0.44, height));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(0.36, 1.0, height));
                sky = lerp(_GroundColor.rgb, sky, smoothstep(-0.18, 0.04, height));

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = saturate(dot(direction, sunDirection));
                sky += _SunGlowColor.rgb * (pow(sunAlignment, 16.0) * 0.5
                    + pow(sunAlignment, 256.0) * 0.5) * _SunGlowStrength;

                // Sampling noise on the sphere direction keeps every azimuth continuous.
                float3 cloudPoint = direction * _CloudScale;
                float cloudNoise = Noise(cloudPoint) * 0.58
                    + Noise(cloudPoint * 2.03 + 13.7) * 0.28
                    + Noise(cloudPoint * 4.07 + 37.1) * 0.14;
                float cloud = saturate((cloudNoise - (0.62 - _CloudCoverage * 0.27)) * _CloudContrast);
                cloud *= smoothstep(0.01, 0.13, height);
                cloud *= 1.0 - smoothstep(0.75, 1.0, height) * 0.35;
                half lightAmount = saturate(0.45 + 0.55 * dot(direction, sunDirection));
                half3 cloudColor = lerp(_CloudShadowColor.rgb, _CloudLightColor.rgb, lightAmount);
                sky = lerp(sky, cloudColor, cloud * 0.62);

                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
