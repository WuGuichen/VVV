Shader "WGame/ShallowWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Name "ThinFilm"
            ZWrite Off ZTest LEqual Cull Back
            Blend One Zero
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            TEXTURE2D_X(_WgWaterSceneColor);
            TEXTURE2D(_WgWaterReflection); SAMPLER(sampler_WgWaterReflection);
            CBUFFER_START(UnityPerMaterial)
            float4x4 _WgWaterReflectionVP;
            float4 _WgWaterSurface, _WgWaterResponse, _WgWaterWave, _WgWaterTint, _WgWaterSize, _WgWaterDetail;
            float _WgWaterTime; int _WgWaterRippleCount;
            float4 _WgWaterRipples[32];
            float4 _WgWaterRippleShape[32];
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 reflected:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=i.uv; o.reflected=mul(_WgWaterReflectionVP,float4(o.positionWS,1)); return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float3 floorColor=SAMPLE_TEXTURE2D_X(_WgWaterSceneColor,sampler_LinearClamp,uv).rgb;
                float depth=SampleSceneDepth(uv);
                float3 floorPosition=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float thickness=max(0,i.positionWS.y-floorPosition.y);
                float contact=saturate(thickness/max(.001,_WgWaterSurface.w));
                contact*=1-smoothstep(_WgWaterResponse.w*.75,_WgWaterResponse.w,thickness);
#if UNITY_REVERSED_Z
                contact*=step(.000001,depth);
#else
                contact*=1-step(.999999,depth);
#endif
                float2 toEdge=min(i.uv,1-i.uv)*_WgWaterSize.xy;
                float edge=smoothstep(0,_WgWaterResponse.y,min(toEdge.x,toEdge.y));
                float2 p=i.positionWS.xz;
                float2 slope=float2(cos(dot(p,float2(.43,.27))+_WgWaterTime*.37),
                    cos(dot(p,float2(-.23,.51))-_WgWaterTime*.29))*_WgWaterWave.x;
                float rippleCrest=0;
                [loop] for(int n=0;n<_WgWaterRippleCount;n++)
                {
                    float4 r=_WgWaterRipples[n], shape=_WgWaterRippleShape[n];
                    float age=_WgWaterTime-r.z;
                    float2 delta=p-r.xy; float distance=length(delta);
                    float wave=distance-age*_WgWaterWave.y;
                    float envelope=exp(-wave*wave/max(.01,shape.x*shape.x))*pow(saturate(1-age/_WgWaterWave.z),2);
                    // r.w is wave height in metres; its spatial derivative is a bounded normal slope.
                    float derivative=-sin(wave*shape.y)*shape.y
                        -2*wave/max(.01,shape.x*shape.x)*cos(wave*shape.y);
                    slope+=delta/max(.05,distance)*derivative*r.w*envelope;
                    rippleCrest+=cos(wave*shape.y)*envelope*saturate(r.w/.02);
                }
                slope=clamp(slope,-.25,.25);
                slope*=edge*contact;
                float2 refractedUv=clamp(uv+slope*_WgWaterResponse.x,.001,.999);
                // Do not drag foreground silhouettes across the water/solid contact boundary.
                float3 refractedPosition=ComputeWorldSpacePosition(refractedUv,SampleSceneDepth(refractedUv),UNITY_MATRIX_I_VP);
                refractedUv=lerp(uv,refractedUv,step(refractedPosition.y,i.positionWS.y+.005));
                float3 transmitted=SAMPLE_TEXTURE2D_X(_WgWaterSceneColor,sampler_LinearClamp,refractedUv).rgb;
                transmitted=lerp(transmitted,_WgWaterTint.rgb,_WgWaterSurface.z);
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fresnel=_WgWaterSurface.y+(1-_WgWaterSurface.y)*pow(1-saturate(dot(normal,view)),5);
                float2 reflectUv=i.reflected.xy/max(.0001,i.reflected.w)*.5+.5;
#if UNITY_UV_STARTS_AT_TOP
                // GPU projection for a render target flips Y; texture sampling uses the opposite origin.
                reflectUv.y=1-reflectUv.y;
#endif
                reflectUv+=slope*_WgWaterDetail.y;
                float valid=step(0,i.reflected.w)*step(0,reflectUv.x)*step(reflectUv.x,1)*step(0,reflectUv.y)*step(reflectUv.y,1);
                float3 reflected=SAMPLE_TEXTURE2D(_WgWaterReflection,sampler_WgWaterReflection,clamp(reflectUv,.001,.999)).rgb;
                // An authored visibility floor keeps a thin reflective film legible from the default overhead camera.
                float reflection=saturate(lerp(_WgWaterDetail.x,1,fresnel)*_WgWaterSurface.x)*valid;
                float3 color=lerp(transmitted,reflected,reflection);
                WgMainLightData sun=WgGetMainLightData(i.positionWS, float3(0,1,0));
                float3 halfVector=normalize(view+sun.directionWS);
                color+=sun.color*sun.shadowAttenuation*sun.distanceAttenuation
                    *pow(saturate(dot(normal,halfVector)),128)*_WgWaterResponse.z;
                // Actual contact packets add a bounded crest sheen, not an opaque foam decal.
                color+=saturate(sun.color)*sun.shadowAttenuation*sun.distanceAttenuation
                    *saturate(rippleCrest)*_WgWaterDetail.z;
                return float4(lerp(floorColor,color,edge*contact),1);
            }
            ENDHLSL
        }
    }
}
