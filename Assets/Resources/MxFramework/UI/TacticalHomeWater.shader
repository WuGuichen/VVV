Shader "Hidden/WGame/UI/TacticalHomeWater"
{
 Properties
 {
  _WgUiWaterStrength("Water strength",Range(0,1.5))=1
  _WgUiWaterTime("Unscaled water phase",Float)=0
  _WgUiPointer("Pointer in top-left viewport UV",Vector)=(-2,-2,0,0)
  _WgUiBattleMode("Battle time filter mode",Float)=0
  _WgUiBattleStillness("Battle stillness",Range(0,1))=0
  _WgUiBattleMotion("Direction progress drive reserved",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   float _WgUiWaterTime, _WgUiWaterStrength, _WgUiWaterOnly;
   float _WgUiBattleMode, _WgUiBattleStillness;
   float4 _WgUiPointer;
   float4 _WgUiBattleMotion;
   float4 _WgUiStableRects[6];
            float3 WaterSurface(float2 q,float phase)
            {
                float a=dot(q,float2(5.1,9.3))+phase;
                float b=dot(q,float2(-8.4,6.1))-phase*2;
                float c=dot(q,float2(13,3))+phase*3;
                float height=.55*sin(a)+.30*sin(b)+.15*sin(c);
                float dx=.55*5.1*cos(a)-.30*8.4*cos(b)+.15*13*cos(c);
                float dy=.55*9.3*cos(a)+.30*6.1*cos(b)+.15*3*cos(c);
                return float3(height,dx,dy);
            }

            float4 ReadBattleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,clamp(uv,.0005,.9995));
            }

            float4 RenderRippleBattle(Varyings i,float2 p,float fluid,float phase)
            {
                float aspect=_ScaledScreenParams.x/_ScaledScreenParams.y;
                float2 q=(p-.5)*float2(aspect,1);
                float radius=length(q);
                float2 radial=q/max(radius,.0001);
                float peripheral=smoothstep(.12,.75,radius);
                float pointerDistance=length((p-_WgUiPointer.xy)*float2(aspect,1));
                // Enlarge the clear core and feather by 50%; match slope and curvature at both edges.
                float pointerBlend=saturate((pointerDistance-.105)/.12);
                pointerBlend=pointerBlend*pointerBlend*pointerBlend*(pointerBlend*(pointerBlend*6-15)+10);
                fluid*=pointerBlend;
                float still=saturate(_WgUiBattleStillness);
                float direction=_WgUiBattleMotion.x;
                float travel=saturate(_WgUiBattleMotion.y);

                // A broad packet of concentric ripples passes once, outside-to-inside on stopping.
                // Both ends start beyond the visible radius, so no completion flash is needed.
                float outerRadius=length(float2(aspect,1)*.5);
                float front=direction>0?lerp(-.40,outerRadius+.40,travel)
                    :lerp(outerRadius+.40,-.40,travel);
                float distance=radius-front;
                float envelope=exp(-pow(distance/.17,2));
                float ripple=sin(distance*42)*envelope;
                ripple*=abs(direction)*smoothstep(0,.12,radius);
                float2 lens=radial/float2(aspect,1)*ripple*.007;

                // The persistent viscous film is separate from the transient ripple packet.
                // Restore broad refraction and directional drag without emissive highlights.
                float3 surface=WaterSurface(p*float2(aspect,1)*.80,phase*.55);
                float3 secondary=WaterSurface(p*float2(aspect,1)*1.35+2,phase*.31);
                float2 flow=clamp((surface.yz+secondary.yz*.35)*.09,-1,1);
                float2 filmLens=flow*(.003+.006*peripheral)*still*fluid;
                lens*=fluid;
                lens+=filmLens;
                #if UNITY_UV_STARTS_AT_TOP
                lens.y=-lens.y;
                filmLens.y=-filmLens.y;
                #endif
                float2 uv=i.texcoord+lens;
                float2 pull=filmLens*.75+float2(.00045,-.0002)*still*fluid;
                // All channels share the same displacement: no color split, glare or contrast pulse.
                float4 color=ReadBattleColor(uv)*.55;
                color+=ReadBattleColor(uv-pull)*.24;
                color+=ReadBattleColor(uv-pull*2)*.14;
                color+=ReadBattleColor(uv-pull*3)*.07;
                color.a=1;
                return color;
            }
            float4 Frag(Varyings i):SV_Target
            {
                // Blit source UV follows the displayed image, independent of render-target scaling.
                // FairyGUI and the supplied pointer rectangles use a top-left origin.
                float2 p=float2(i.texcoord.x,1-i.texcoord.y);
                float fluid=1;
                [unroll] for(int n=0;n<6;n++)
                {
                    float4 r=_WgUiStableRects[n];
                    float2 outside=max(r.xy-p,p-r.zw);
                    float distance=max(outside.x*1.7777778,outside.y);
                    fluid=min(fluid,smoothstep(0,.025,distance));
                }
                // All phases close at twelve seconds. This is a screen-space UI
                // study; neither the scene camera nor its world materials change.
                float phase=_WgUiWaterTime*6.2831853/12;
                if(_WgUiBattleMode>.5)return RenderRippleBattle(i,p,fluid,phase);
                float3 surface=WaterSurface(p*float2(1.7777778,1),phase);
                float edgeDistance=min(min(p.x,1-p.x)*1.7777778,min(p.y,1-p.y));
                float edge=1-smoothstep(.015,.235,edgeDistance+.018*sin(p.x*7+p.y*9+phase));
                float amount=fluid*_WgUiWaterStrength*lerp(.45,1.45,edge);
                float2 drift=surface.yz*float2(.0009,-.0013);
                #if UNITY_UV_STARTS_AT_TOP
                drift.y=-drift.y;
                #endif
                float2 uv=i.texcoord+drift*amount;
                uv=clamp(uv,float2(.0005,.0005),float2(.9995,.9995));
                float thickness=pow(saturate(.5+surface.x*.5),1.6);
                float blur=(.0002+thickness*.0012)*amount;
                float4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv)*.6;
                color+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(blur,0))*.1;
                color+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(blur,0))*.1;
                color+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(0,blur))*.1;
                color+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(0,blur))*.1;
                // Water-shadow is generated independently of the source picture.
                // Optical depth and refraction are driven by the same moving surface.
                // The perimeter carries readable moving water-shadows, with a
                // softer centre. The drifting edge profile avoids a fixed vignette.
                float sky=smoothstep(-.45,.75,surface.x);
                float3 waterTint=lerp(float3(.055,.15,.23),float3(.34,.51,.59),sky);
                float veil=lerp(.035+thickness*.10,.18+thickness*.32,edge)*fluid*_WgUiWaterStrength;
                color.rgb=lerp(color.rgb,waterTint,saturate(veil));
                color.rgb*=1-saturate(-surface.x)*.035*amount;
                // Broad soft reflected folds are confined to the perimeter;
                // they move with the surface instead of tracing a fixed border.
                float fold=exp(-pow((surface.x-.18)/.18,2));
                float foldOpacity=fold*edge*fluid*_WgUiWaterStrength*.24;
                color.rgb=lerp(color.rgb,float3(.40,.60,.70),saturate(foldOpacity));
                if(_WgUiWaterOnly>.5)
                    return float4(lerp(float3(.5,.5,.5),waterTint,saturate(veil)),1);
                // The solid UI interior is sampled unchanged: no typography swim.
                if(fluid<=0)color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,i.texcoord);
                color.a=1;return color;
            }

 ENDHLSL
 }
 }
}
