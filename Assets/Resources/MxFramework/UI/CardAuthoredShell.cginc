sampler2D _RegionTex, _BackdropTex;
sampler2D _CardIllustration;
float4 _CardIllustrationRect;
float _CardIllustrationEnabled;
float4 _CardUvRect, _BackdropRect;
float _Quality, _FlowTime, _FlowStrength, _RippleStrength, _RippleDensity, _RippleBandWidth, _RippleDrift, _FrostStrength, _PanelOpacity, _BaseOnly;
float _BattleReadiness;
float _UtilityCard;
float _TouchClock;
float4 _TouchEvents[12]; // local UV, birth time, impulse strength

float ShellGrain(float2 p)
{
    return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
}

float3 ShellHsv(float3 c)
{
    float4 k=float4(0,-1.0/3.0,2.0/3.0,-1);
    float4 p=lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
    float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
    float d=q.x-min(q.w,q.y), e=1e-7;
    return float3(abs(q.z+(q.w-q.y)/(6*d+e)),d/(q.x+e),q.x);
}

float3 ShellRgb(float3 c)
{
    float3 p=abs(frac(c.xxx+float3(0,2.0/3.0,1.0/3.0))*6-3);
    return c.z*lerp(float3(1,1,1),saturate(p-1),c.y);
}

float4 AuthoredShell(float2 atlasUv)
{
    float2 p=(atlasUv-_CardUvRect.xy)/max(_CardUvRect.zw,.00001);
    // R faceted outer layer/accents, G inset ripple, B cost medallion, A silhouette.
    float4 region=tex2D(_RegionTex,p);
    float t=_FlowTime;
    float touchHeight=0;
    float2 touchOffset=0;
    if(region.g>.001)
    {
        [unroll] for(int touch=0;touch<12;touch++)
        {
            float4 impulse=_TouchEvents[touch];
            float age=_TouchClock-impulse.z;
            if(impulse.w>0 && age>=0 && age<2.5)
            {
                float2 delta=(p-impulse.xy)*float2(1,1.3968254);
                float distance=length(delta);
                float front=distance-age*.24;
                float envelope=exp(-front*front/.007)*exp(-age*2.6)*impulse.w*smoothstep(0,.045,age);
                float oscillation=distance*65-age*17;
                touchHeight+=sin(oscillation)*envelope;
                touchOffset+=delta/max(distance,.002)/float2(1,1.3968254)*cos(oscillation)*envelope*.006;
            }
        }
    }
    // Nearly parallel diagonal ripple crests, matching the supplied close-up.
    // Low-frequency phase bending keeps adjacent crests coherent, not radial.
    float bend=.30*sin(p.y*8+p.x*2-t*.38)+.10*sin(p.x*9+p.y*5+t*.23);
    float phase=(p.x-p.y*.68)*_RippleDensity+bend-t*_RippleDrift+touchHeight*.85;
    float secondary=(p.x-p.y*.63)*_RippleDensity*.81+.18*sin(p.y*10-t*.19)-t*_RippleDrift*.72;
    float wave=sin(phase*6.2831853)*.86+sin(secondary*6.2831853)*.14;
    float slope=cos(phase*6.2831853)*.86+cos(secondary*6.2831853)*.14;
    float bandDistance=abs(frac(phase+.035*sin(secondary*6.2831853))-.5);
    float feather=max(fwidth(phase)*.5,.07);
    float band=1-smoothstep(_RippleBandWidth*.5-feather,_RippleBandWidth*.5+feather,bandDistance);
    float2 offset=(float2(1,-.48)*slope*.0023*_RippleStrength+clamp(touchOffset,-.012,.012))*region.g;
    // Keep coverage all the way to the inner edge; attenuate only displacement
    // when its proposed sample would leave the aperture.
    float guard=min(tex2D(_RegionTex,p+float2(.004,0)).g,tex2D(_RegionTex,p-float2(.004,0)).g);
    guard=min(guard,min(tex2D(_RegionTex,p+float2(0,.003)).g,tex2D(_RegionTex,p-float2(0,.003)).g));
    offset*=guard;
    float2 sourceUv=atlasUv+offset*_CardUvRect.zw;
    float3 raw=tex2D(_MainTex,sourceUv).rgb;
    float3 srgb=raw;
    #if !defined(UNITY_COLORSPACE_GAMMA)
    srgb=LinearToGammaSpace(raw);
    #endif
    float3 hsv=ShellHsv(srgb);
    float zone=saturate(region.r+region.g+region.b);
    float3 foundation=srgb;
    if(_Quality<1.5)
    {
        // The shared artwork is holographic. Never expose its original hues
        // through unmasked seams or anti-aliased edges on silver/gold cards.
        foundation=ShellRgb(float3(.58,min(hsv.y,.14),hsv.z));
        // Strong authored accent colors are quality ink even where the geometric
        // mask missed a narrow bevel. Keep low-chroma frost out of this recovery.
        zone=max(zone,smoothstep(.28,.48,hsv.y));
    }
    float3 graded=hsv;
    if(_Quality<.5){graded.x=.58;graded.y=hsv.y*.20;}
    else if(_Quality<1.5){graded.x=.117;graded.y=saturate(hsv.y*1.06);}
    else
    {
        // Continuous hue travel across the authored spectrum (8 second cycle).
        // Apply the optical mask when compositing, not to the hue phase: masking
        // the phase would make anti-aliased edges cycle at different speeds.
        graded.x+=(t*.125+.045*sin(p.x*5+p.y*4-t*.8))*_FlowStrength;
    }
    // Keep the authored edge cuts and their brightness; animate only their optical response.
    float travel=p.x*.7+p.y*.57-t*(_Quality>=1.5 ? .16 : .045);
    float glint=pow(.5+.5*sin(travel*6.2831853),18)*region.r;
    graded.z=saturate(graded.z+glint*.15*_FlowStrength);
    graded.z=saturate(graded.z+region.r*_BattleReadiness*.20);
    graded.y*=1-glint*.12*_FlowStrength;
    // The face is matte: no white ridge, caustic emission, specular lobe or
    // pulsing medallion. Broad bands modulate tint, not specular intensity.
    graded.z*=1-(1-band)*.035*_RippleStrength*region.g;
    graded.y*=1-band*.38*_RippleStrength*region.g;
    graded.x+=wave*.006*_RippleStrength*region.g;
    float opticalZone=_Quality>=1.5 ? saturate(region.r+region.g) : zone;
    float3 color=lerp(foundation,ShellRgb(graded),opticalZone);
    // Frost is a stationary surface treatment on neutral raised plates only.
    // Preserve their narrow bevels and contact shadows instead of blurring the card.
    float boundary=saturate((length(ddx(srgb))+length(ddy(srgb)))*22);
    float plate=(1-zone)*(1-boundary)*saturate(_FrostStrength);
    float2 grainUv=floor(p*float2(756,1056));
    float grain=(ShellGrain(grainUv)-.5)*.013+(ShellGrain(floor(grainUv*.57))-.5)*.004;
    float3 matte=min(color,float3(.932,.96,.982))+grain;
    color=lerp(color,matte,plate);
    // Multiply the approved neutral painting into the optical inset. White paper
    // disappears; pen guides and oil masses inherit the underlying quality tint.
    // Local card UVs keep the drawing stable while the water moves beneath it.
    if (_CardIllustrationEnabled > .5)
    {
        float2 artUv = (p - _CardIllustrationRect.xy) / max(_CardIllustrationRect.zw, .00001);
        if (all(artUv >= 0) && all(artUv <= 1))
        {
            float4 art = tex2D(_CardIllustration, artUv);
            #if !defined(UNITY_COLORSPACE_GAMMA)
            art.rgb = LinearToGammaSpace(art.rgb);
            #endif
            if (_UtilityCard > .5) art.rgb = dot(art.rgb, float3(.299,.587,.114)).xxx;
            color *= lerp(float3(1,1,1), saturate(art.rgb * 1.10), art.a * region.g);
        }
    }
    if (_UtilityCard > .5)
    {
        // A fine interrupted inner bevel follows the same icon aperture; the card shell stays shared.
        float nearInset = min(tex2D(_RegionTex,p+float2(.006,0)).g,tex2D(_RegionTex,p-float2(.006,0)).g);
        nearInset = min(nearInset,min(tex2D(_RegionTex,p+float2(0,.004)).g,tex2D(_RegionTex,p-float2(0,.004)).g));
        float innerBevel = region.g * (1-nearInset);
        float engraving = smoothstep(.24,.44,frac((p.x+p.y*.68)*11));
        color = lerp(color, float3(.22,.32,.40), innerBevel*engraving*.45);
    }
    #if !defined(UNITY_COLORSPACE_GAMMA)
    color=GammaToLinearSpace(color);
    #endif
    float3 backdrop=tex2D(_BackdropTex,_BackdropRect.xy+p*_BackdropRect.zw).rgb;
    float frostMix=(1-zone)*(1-_PanelOpacity)*.22;
    color=lerp(color,backdrop,frostMix);
    float alpha=region.a*lerp(1,zone,_BaseOnly);
    return float4(color,alpha);
}
