Shader "Hidden/MxFramework/Rendering/SceneDust"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "SceneDust"
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ MX_DUST_LIGHT_FIELD
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "MxWind.hlsl"
            #include "MxMedium.hlsl"
            #if defined(MX_DUST_LIGHT_FIELD)
            #include "MxLightField.hlsl"
            #include "MxSunVisibility.hlsl"
            Texture2D<float> _MxDustSunVisibility;
            float4x4 _MxDustSunVP, _MxDustSunView;
            float4 _MxDustSunRange, _MxDustSunDirection, _MxDustSunColor;
            float _MxDustIndirectStrength;
            #endif
            float4 _MxDustVolume, _MxDustAppearance, _MxDustColor, _MxDustAmbient, _MxDustCenter;
            float4 _MxVolumetricFogParams, _MxVolumetricFogHeightParams;
            float4 _MxVolumetricFogScatteringParams, _MxVolumetricFogFocusParams, _MxVolumetricFogColor;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 quad : TEXCOORD0;
                float3 centerWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float2 depthSize : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 DustRandom(uint id)
            {
                uint3 hash = uint3(id, id + 1013u, id + 7919u);
                hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
                hash *= 3266489917u; hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777216.0;
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings o = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                const float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float3 seed = DustRandom(instanceID + (uint)_MxDustVolume.w);
                float radius = _MxDustVolume.x, diameter = radius * 2;
                float time = MxPresentationTime();
                float3 basePosition = float3(seed.x * diameter, _MxDustVolume.y, seed.y * diameter);
                float3 wind = MxWindDirection() * max(_MxWindStrength, 0) * time * _MxDustAppearance.z;
                wind += MxWindDisplacement(basePosition, time, 0.035, seed.z) * 0.3;
                // Slow local suspension remains visible in still air.
                wind += float3(sin(time * 0.17 + seed.z * 30), 0, cos(time * 0.13 + seed.x * 30)) * _MxDustAppearance.z;
                float2 lower = _MxDustCenter.xz - radius;
                float2 horizontal = lower + frac((basePosition.xz + wind.xz - lower) / diameter) * diameter;
                float heightFraction = frac(seed.z + time * _MxDustAppearance.z / max(_MxDustVolume.z, 0.1) * 0.12);
                float3 center = float3(horizontal.x, _MxDustVolume.y + heightFraction * _MxDustVolume.z, horizontal.y);
                float fade = 1 - smoothstep(radius * 0.7, radius, distance(center.xz, _MxDustCenter.xz));
                fade *= smoothstep(0, 0.06, heightFraction) * (1 - smoothstep(0.94, 1, heightFraction));
                fade *= 1 - saturate(_MxRain); // Same weather source as the rest of the scene.
                float size = _MxDustAppearance.x * lerp(0.45, 1.4, seed.x);
                float eyeDepth = -TransformWorldToView(center).z;
                fade *= smoothstep(0.8, 2.5, eyeDepth);
                float4 centerCS = TransformWorldToHClip(center);
                float pixelRadius = size * abs(UNITY_MATRIX_P._m11) * _ScaledScreenParams.y * 0.5 / max(abs(centerCS.w), 0.001);
                float rasterRadius = clamp(pixelRadius, 0.65, 2.5);
                fade *= saturate(pixelRadius * pixelRadius / (rasterRadius * rasterRadius));
                centerCS.xy += corners[vertexID] * rasterRadius * 2 / _ScaledScreenParams.xy * centerCS.w;
                if (eyeDepth <= _ProjectionParams.y) fade = 0;
                float3 toEye = normalize(GetCameraPositionWS() - center + float3(0, 0.00001, 0));
                float3 ambient = _MxDustAmbient.rgb;
                #if defined(MX_DUST_LIGHT_FIELD)
                float3 grid = (center - _MxLightingGridOrigin.xyz) * _MxLightingGridOrigin.w;
                if (all(grid >= 0) && all(grid <= _MxLightingGridSize.xyz - 1))
                    ambient = MxSampleMediumAmbient(center, toEye, _MxDustAppearance.w) * _MxDustIndirectStrength;
                #endif
                o.positionCS = centerCS;
                o.quad = corners[vertexID];
                o.centerWS = center;
                o.color = float4(ambient, fade * _MxDustAppearance.y);
                o.depthSize = float2(eyeDepth, size);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float mask = saturate(1 - dot(input.quad, input.quad));
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float sceneDepth = -TransformWorldToView(scenePosition).z;
                float softIntersection = saturate((sceneDepth - input.depthSize.x) / max(input.depthSize.y * 3, 0.05));
                float alpha = mask * mask * input.color.a * softIntersection;
                clip(alpha - 0.0001);
                float3 toEye = normalize(GetCameraPositionWS() - input.centerWS);
                Light main = GetMainLight(TransformWorldToShadowCoord(input.centerWS));
                float3 lightDirection = main.direction;
                float3 lightColor = main.color;
                float visibility = main.shadowAttenuation * main.distanceAttenuation;
                #if defined(MX_DUST_LIGHT_FIELD)
                lightDirection = _MxDustSunDirection.xyz;
                lightColor = _MxDustSunColor.rgb;
                visibility *= MxSampleSunVisibility(input.centerWS, _MxDustSunVP, _MxDustSunView,
                    _MxDustSunRange, _MxDustSunVisibility, sampler_PointClamp);
                #endif
                float3 radiance = input.color.rgb + lightColor * visibility * MxHenyeyGreenstein(lightDirection, toEye, _MxDustAppearance.w);
                radiance *= _MxDustColor.rgb * _MxDustColor.a;
                float fog = MxHeightFogAmount(input.centerWS, distance(GetCameraPositionWS(), input.centerWS),
                    _MxViewFocusWorldPos.xyz, _MxVolumetricFogParams, _MxVolumetricFogHeightParams,
                    _MxVolumetricFogScatteringParams, _MxVolumetricFogFocusParams);
                radiance = lerp(radiance, _MxVolumetricFogColor.rgb, fog);
                return half4(radiance * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
