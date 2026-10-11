Shader "Hidden/MxFramework/SceneLightingResolve"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ MX_CAPSULE_TILES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "MxLightField.hlsl"
            float4 _MxLightingCapsules[32];
            float4 _MxLightingCapsuleEnds[32];
            float4 _MxLightingCapsuleRanges[32];
            #if defined(MX_CAPSULE_TILES)
            StructuredBuffer<uint> _MxLightingCapsuleTileMasks;
            float4 _MxLightingCapsuleTileGrid;
            #endif
            int _MxLightingCapsuleCount;
            float _MxLightingCapsuleStrength;
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(uint id:SV_VertexID) { V o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=GetFullScreenTriangleVertexPosition(id);o.uv=GetFullScreenTriangleTexCoord(id);return o; }
            float4 Frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float depth=SampleSceneDepth(input.uv);
                #if UNITY_REVERSED_Z
                if(depth<0.00001) return 0;
                #else
                if(depth>0.99999) return 0;
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 position=ComputeWorldSpacePosition(input.uv,depth,UNITY_MATRIX_I_VP);
                float eyeDepth=-TransformWorldToView(position).z;
                float3 grid=(position-_MxLightingGridOrigin.xyz)*_MxLightingGridOrigin.w;
                // Match the material's field bounds. Keep depth for edge-aware reconstruction.
                if(any(grid<0)||any(grid>_MxLightingGridSize.xyz-1))return float4(0,0,0,eyeDepth);
                float3 normal=normalize(SampleSceneNormals(input.uv));
                MxProbeStencil stencil=MxPrepareProbeStencil(grid);
                float supportBlendDistance=0.25/max(_MxLightingGridOrigin.w,0.001);
                float3 sum=0; float total=0;
                for(uint level=0;level<2;level++)
                {
                if((level==0 ? 1-stencil.blend : stencil.blend)<=0)continue;
                for(uint corner=0;corner<8;corner++)
                {
                    float spatialWeight;uint id=MxProbeStencilIndex(stencil,level,corner,spatialWeight);
                    MxLightProbe probe=_MxLightingProbes[id];
                    if(probe.positionValid.w<0.5) continue;
                    float3 delta=position+normal*0.08-probe.positionValid.xyz;
                    float distanceToProbe=length(delta);
                    float2 moments=_MxLightingVisibility[id*96+MxProbeVisibilityCell(delta)];
                    float variance=max(moments.y-moments.x*moments.x,0.0001);
                    float visible=MxProbeVisibilityForId(id,delta,distanceToProbe);
                    float weight=spatialWeight*visible;
                    float facingProbe=saturate(dot(normal,-delta/max(distanceToProbe,0.001))*0.5+0.5);
                    weight*=facingProbe*facingProbe+0.02;
                    // A receiver's tangent plane is not itself an occluder. Curved receivers
                    // may have all valid probes behind it, even in unobstructed space.
                    float tangentDistance=dot(normal,probe.positionValid.xyz-position);
                    float tangentWeight=smoothstep(0.0,supportBlendDistance,tangentDistance);
                    // Format v1 captures 16 distances per bin (16x16 face / 4x4 bins).
                    // min(sample) >= mean - sqrt((N-1)*variance). Only relax the tangent
                    // rejection before this conservative bound; distant sky in a mixed bin
                    // must not bypass a nearer sampled obstruction.
                    float clearDistance=_MxLightingGridSize.w>0.5 ? _MxLightingClearDistances[id*96+MxProbeVisibilityCell(delta)] : moments.x-sqrt(15.0*variance);
                    float clearPath=smoothstep(0.0,supportBlendDistance,clearDistance-distanceToProbe-0.03);
                    weight*=max(tangentWeight,clearPath);
                    MxLightCoefficients coeff=_MxLightingCoefficients[id]; float4 basis=float4(normal*2,1);
                    sum+=max(0,float3(dot(coeff.r,basis),dot(coeff.g,basis),dot(coeff.b,basis)))*weight;
                    total+=weight;
                }
                }
                float3 irradiance=total>0.000000000001?sum/total:0;
                if(!any(irradiance>0)||_MxLightingCapsuleStrength<=0||_MxLightingCapsuleCount==0)
                    return float4(irradiance,eyeDepth);
                float occlusion=1;
                #if defined(MX_CAPSULE_TILES)
                uint2 tile=min((uint2)(input.position.xy/16),(uint2)_MxLightingCapsuleTileGrid.xy-1);
                uint candidates=_MxLightingCapsuleTileMasks[tile.y*(uint)_MxLightingCapsuleTileGrid.x+tile.x];
                while(candidates!=0)
                {
                    uint i=firstbitlow(candidates);candidates&=candidates-1;
                    float3 start=_MxLightingCapsules[i].xyz, end=_MxLightingCapsuleEnds[i].xyz;
                    float3 axis=end-start;
                    float t=saturate(dot(position-start,axis)/max(dot(axis,axis),0.0001));
                    float3 direction=start+axis*t-position;
                    float distanceSquared=dot(direction,direction), radius=_MxLightingCapsules[i].w;
                    float4 range=_MxLightingCapsuleRanges[i];
                    if(distanceSquared>=range.z)continue;
                    float dist=sqrt(distanceSquared);
                    // Exclude the capsule's own surface; only obscure nearby receiving surfaces.
                    float outside=smoothstep(radius*1.02,radius*1.25,dist);
                    float solidAngle=saturate(radius*radius/max(dist*dist,0.01));
                    float facing=saturate(dot(normal,direction/max(dist,0.001)));
                    float fade=1-smoothstep(0,1,(distanceSquared-range.x)*range.y);
                    occlusion*=1-_MxLightingCapsuleStrength*solidAngle*facing*outside*fade;
                }
                #endif
                return float4(irradiance*occlusion,eyeDepth);
            }
            ENDHLSL
        }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DebugFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            int _MxSceneDebugMode;
            float4 DebugFrag(Varyings input):SV_Target
            {
                float4 value=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,input.texcoord,0);
                if(_MxSceneDebugMode==2)return float4(value.xyz*0.5+0.5,1);
                if(_MxSceneDebugMode==3)return float4(0.5+value.xy*16,0.5,1);
                return float4(value.rgb,1);
            }
            ENDHLSL
        }
    }
}
