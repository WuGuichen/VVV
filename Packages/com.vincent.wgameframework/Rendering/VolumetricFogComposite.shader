Shader "Hidden/MxFramework/Rendering/VolumetricFogComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "MxVolumetricFogComposite"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "MxMedium.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _BlitScaleBias;
            float4 _BlitTexture_TexelSize;
            float4 _MxVolumetricFogParams;
            float4 _MxVolumetricFogHeightParams;
            float4 _MxVolumetricFogScatteringParams;
            float4 _MxVolumetricFogFocusParams;
            float4 _MxVolumetricFogColor;
            float4 _MxViewFocusWorldPos;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                output.texcoord = output.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            float Hash21(float2 value)
            {
                float3 p = frac(float3(value.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float enabled = _MxVolumetricFogParams.w;
                if (enabled <= 0.001)
                    return source;

                float rawDepth = SampleSceneDepth(input.texcoord);
                #if UNITY_REVERSED_Z
                bool skyPixel = rawDepth <= 0.000001;
                #else
                bool skyPixel = rawDepth >= 0.999999;
                #endif

                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraToSurface = worldPos - GetCameraPositionWS();
                float surfaceDistance = length(cameraToSurface);
                float maxDistance = max(_MxVolumetricFogParams.y, 1.0);
                float distance01 = skyPixel ? 1.0 : saturate(surfaceDistance / maxDistance);

                float3 viewDirection = cameraToSurface / max(surfaceDistance, 0.0001);
                float geometryHeightDensity = MxHeightDensity(worldPos.y, _MxVolumetricFogHeightParams.x, _MxVolumetricFogHeightParams.y);
                float horizonDensity = saturate(1.0 - abs(viewDirection.y) * 2.6);
                horizonDensity *= horizonDensity;
                float heightDensity = skyPixel ? horizonDensity : geometryHeightDensity;
                float density = max(_MxVolumetricFogParams.x, 0.0);
                float opticalDepth = density * heightDensity * distance01;

                if (_MxVolumetricFogParams.z > 0.0)
                {
                    float noise = Hash21(input.texcoord * _MxVolumetricFogScatteringParams.z + _MxVolumetricFogScatteringParams.w);
                    opticalDepth *= lerp(1.0, lerp(0.72, 1.28, noise), saturate(_MxVolumetricFogParams.z));
                }

                float fogAmount = MxFogAmountFromOpticalDepth(opticalDepth, worldPos,
                    _MxViewFocusWorldPos.xyz, maxDistance, _MxVolumetricFogScatteringParams,
                    _MxVolumetricFogFocusParams, skyPixel);

                half3 fogColor = (half3)_MxVolumetricFogColor.rgb;
                source.rgb = lerp(source.rgb, fogColor, (half)fogAmount);
                return source;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
