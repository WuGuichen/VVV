Shader "WGame/ShallowWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/WGameMainLight.hlsl"
            TEXTURE2D_X(_WgWaterSceneColor);
            TEXTURE2D(_WgWaterRippleField); SAMPLER(sampler_WgWaterRippleField);
            TEXTURE2D(_WgWaterReflection); SAMPLER(sampler_WgWaterReflection);
            CBUFFER_START(UnityPerMaterial)
            float4x4 _WgWaterReflectionVP;
            float4 _WgWaterSurface, _WgWaterResponse, _WgWaterWave, _WgWaterTint, _WgWaterSize, _WgWaterDetail;
            float _WgWaterTime;
            float4 _WgWaterRippleRect;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 reflected:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=i.uv; o.reflected=mul(_WgWaterReflectionVP,float4(o.positionWS,1)); return o;
            }
            float4 ShadeWater(Varyings i, bool refract)
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
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
                // Dry/hidden pixels do not evaluate waves, reflection, refraction or lighting.
                float coverage=edge*contact;
                clip(coverage-.00001);
                float2 p=i.positionWS.xz;
                float2 slope=float2(cos(dot(p,float2(.43,.27))+_WgWaterTime*.37),
                    cos(dot(p,float2(-.23,.51))-_WgWaterTime*.29))*_WgWaterWave.x;
                float rippleCrest=0;
                if (_WgWaterRippleRect.z>0)
                {
                    float2 fieldUv=(p-_WgWaterRippleRect.xy)*_WgWaterRippleRect.z;
                    if(all(fieldUv>=0) && all(fieldUv<=1))
                    {
                        float3 field=SAMPLE_TEXTURE2D(_WgWaterRippleField,sampler_WgWaterRippleField,fieldUv).rgb;
                        float fade=saturate(min(min(fieldUv.x,fieldUv.y),min(1-fieldUv.x,1-fieldUv.y))*_WgWaterRippleRect.w);
                        slope+=field.rg*fade; rippleCrest=field.b*fade;
                    }
                }
                slope=clamp(slope,-.25,.25);
                slope*=edge*contact;
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
                float3 reflected=0;
                if(_WgWaterSurface.x>0)
                {
                    if(_WgWaterDetail.w>.5)
                    {
                        reflected=max(0,SampleSH(reflect(-view,normal))); valid=1;
                        if(_WgWaterDetail.w>1.5)
                        {
                            float2 ssprUv=uv+slope*_WgWaterDetail.y;
                            float4 screenReflection=SAMPLE_TEXTURE2D(_WgWaterReflection,sampler_WgWaterReflection,saturate(ssprUv));
                            float border=saturate(min(min(ssprUv.x,ssprUv.y),min(1-ssprUv.x,1-ssprUv.y))*20);
                            // RGB is confidence-premultiplied, preventing black fringes when filtering holes.
                            reflected=reflected*(1-screenReflection.a*border)+screenReflection.rgb*border;
                        }
                    }
                    else reflected=SAMPLE_TEXTURE2D(_WgWaterReflection,sampler_WgWaterReflection,clamp(reflectUv,.001,.999)).rgb;
                }
                // An authored visibility floor keeps a thin reflective film legible from the default overhead camera.
                float reflection=saturate(lerp(_WgWaterDetail.x,1,fresnel)*_WgWaterSurface.x)*valid;
                float opacity=1-(1-_WgWaterSurface.z)*(1-reflection);
                float3 color=lerp(_WgWaterTint.rgb*_WgWaterSurface.z,reflected,reflection);
                WgMainLightData sun=WgGetMainLightData(i.positionWS, float3(0,1,0));
                float3 halfVector=normalize(view+sun.directionWS);
                color+=sun.color*sun.shadowAttenuation*sun.distanceAttenuation
                    *pow(saturate(dot(normal,halfVector)),128)*_WgWaterResponse.z;
                // Actual contact packets add a bounded crest sheen, not an opaque foam decal.
                color+=saturate(sun.color)*sun.shadowAttenuation*sun.distanceAttenuation
                    *saturate(rippleCrest)*_WgWaterDetail.z;
                if(refract)
                {
                    float3 floorColor=SAMPLE_TEXTURE2D_X(_WgWaterSceneColor,sampler_LinearClamp,uv).rgb;
                    float2 refractedUv=clamp(uv+slope*_WgWaterResponse.x,.001,.999);
                    float3 refractedPosition=ComputeWorldSpacePosition(refractedUv,SampleSceneDepth(refractedUv),UNITY_MATRIX_I_VP);
                    refractedUv=lerp(uv,refractedUv,step(refractedPosition.y,i.positionWS.y+.005));
                    float3 transmitted=SAMPLE_TEXTURE2D_X(_WgWaterSceneColor,sampler_LinearClamp,refractedUv).rgb;
                    color+=transmitted*(1-opacity);
                    return float4(lerp(floorColor,color,coverage),1);
                }
                // Premultiplied blending reads the destination in fixed-function hardware, with no color-copy pass.
                return float4(color*coverage,opacity*coverage);
            }
            float4 Frag(Varyings i):SV_Target { return ShadeWater(i,true); }
            float4 FragSimple(Varyings i):SV_Target { return ShadeWater(i,false); }
        ENDHLSL
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
            ENDHLSL
        }
        Pass
        {
            Name "Lightweight"
            ZWrite Off ZTest LEqual Cull Back
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragSimple
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
    }
}
