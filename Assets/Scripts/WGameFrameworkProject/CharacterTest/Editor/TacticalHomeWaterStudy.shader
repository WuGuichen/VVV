Shader "Hidden/MxFramework/TacticalHomeWaterStudy"
{
    Properties
    {
        _MainTex("Approved home reference",2D)="white"{}
        _WaterTime("UI time",Float)=0
        _WaterStrength("Water strength",Range(0,1.5))=1
        _WaterOnly("Inspect generated water layer",Range(0,1))=0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _WaterTime,_WaterStrength,_WaterOnly;
            float4 _StableRects[6]; // top-origin screen UV: xmin,ymin,xmax,ymax

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

            float4 frag(v2f_img i):SV_Target
            {
                float2 p=float2(i.uv.x,1-i.uv.y);
                float fluid=1;
                [unroll] for(int n=0;n<6;n++)
                {
                    float4 r=_StableRects[n];
                    float2 outside=max(r.xy-p,p-r.zw);
                    float distance=max(outside.x*1.7777778,outside.y);
                    fluid=min(fluid,smoothstep(0,.025,distance));
                }
                // All phases close at twelve seconds. This is a screen-space UI
                // study; neither the scene camera nor its world materials change.
                float phase=_WaterTime*6.2831853/12;
                float3 surface=WaterSurface(p*float2(1.7777778,1),phase);
                float edgeDistance=min(min(p.x,1-p.x)*1.7777778,min(p.y,1-p.y));
                float edge=1-smoothstep(.015,.235,edgeDistance+.018*sin(p.x*7+p.y*9+phase));
                float amount=fluid*_WaterStrength*lerp(.45,1.45,edge);
                float2 drift=surface.yz*float2(.0009,-.0013);
                float2 uv=i.uv+drift*amount;
                uv=clamp(uv,float2(.0005,.0005),float2(.9995,.9995));
                float thickness=pow(saturate(.5+surface.x*.5),1.6);
                float blur=(.0002+thickness*.0012)*amount;
                float4 color=tex2D(_MainTex,uv)*.6;
                color+=tex2D(_MainTex,uv+float2(blur,0))*.1;
                color+=tex2D(_MainTex,uv-float2(blur,0))*.1;
                color+=tex2D(_MainTex,uv+float2(0,blur))*.1;
                color+=tex2D(_MainTex,uv-float2(0,blur))*.1;
                // Water-shadow is generated independently of the source picture.
                // Optical depth and refraction are driven by the same moving surface.
                // The perimeter carries readable moving water-shadows, with a
                // softer centre. The drifting edge profile avoids a fixed vignette.
                float sky=smoothstep(-.45,.75,surface.x);
                float3 waterTint=lerp(float3(.055,.15,.23),float3(.34,.51,.59),sky);
                float veil=lerp(.035+thickness*.10,.18+thickness*.32,edge)*fluid*_WaterStrength;
                color.rgb=lerp(color.rgb,waterTint,saturate(veil));
                color.rgb*=1-saturate(-surface.x)*.035*amount;
                // Broad soft reflected folds are confined to the perimeter;
                // they move with the surface instead of tracing a fixed border.
                float fold=exp(-pow((surface.x-.18)/.18,2));
                float foldOpacity=fold*edge*fluid*_WaterStrength*.24;
                color.rgb=lerp(color.rgb,float3(.40,.60,.70),saturate(foldOpacity));
                if(_WaterOnly>.5)
                    return float4(lerp(float3(.5,.5,.5),waterTint,saturate(veil)),1);
                // The solid UI interior is sampled unchanged: no typography swim.
                if(fluid<=0)color=tex2D(_MainTex,i.uv);
                color.a=1;return color;
            }
            ENDCG
        }
    }
}
