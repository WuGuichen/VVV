// Local card UVs are separate from atlas coordinates. This include adds only
// material shading; the owning FairyGUI shader retains clipping and blend rules.
float4 _CardUvRect;
float _FlowTime, _Quality, _FlowStrength, _PanelOpacity, _GrainStrength;
sampler2D _BackdropTex;
sampler2D _ShellDetailTex;
float4 _BackdropRect;

float2 CardLocalUv(float2 atlasUv)
{
    return (atlasUv - _CardUvRect.xy) / max(_CardUvRect.zw, .00001);
}

float CardHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float3 CardOpticalColor(float2 p)
{
    float t = _FlowTime;
    float bend = sin(p.y * 5.8 + p.x * 2.1 + t * .37) * .18
               + sin(p.x * 4.3 - p.y * 2.7 - t * .23) * .11;
    float phase = p.x * .76 + p.y * .47 + bend - t * .075;
    // Periodic smooth lobes have no sawtooth reset or faceted highlights.
    float ribbon = pow(.5 + .5 * sin(phase * 6.2831853), 7.0);
    float broad = .5 + .5 * sin((phase + .28) * 6.2831853);
    float3 color;
    if (_Quality < .5)
        color = lerp(float3(.43,.63,.78), float3(.87,.94,.98), broad * .65 + .25);
    else if (_Quality < 1.5)
        color = lerp(float3(.86,.42,.055), float3(1.0,.83,.23), broad * .66 + .25);
    else
    {
        float hue = p.x * .62 + p.y * .43 + bend * .52 - t * .027;
        color = .57 + .41 * cos(6.2831853 * (hue + float3(0,.3333,.6667)));
    }
    float amount = ribbon * _FlowStrength * (_Quality < .5 ? .20 : .34);
    return lerp(color, float3(1.0,.985,.94), amount);
}

float4 CardFlow(float2 atlasUv)
{
    float4 masks = tex2D(_MainTex, atlasUv);
    float2 p = CardLocalUv(atlasUv);
    float3 color = CardOpticalColor(p);
    float rawPhase = (p.x - p.y * .68) * 66.0;
    float phase = frac(rawPhase);
    float width = max(fwidth(rawPhase), .025);
    float stripe = smoothstep(.46-width,.46+width,phase)
                 - smoothstep(.80-width,.80+width,phase);
    // G is the central optical window. The fine hatch itself never moves.
    color = lerp(color, float3(.94,.97,1), stripe * masks.g * .57);
    color *= 1-masks.b*.23;
    color = lerp(color, float3(.98,.995,1), masks.r * .58);
    #if !defined(UNITY_COLORSPACE_GAMMA)
    color = GammaToLinearSpace(color);
    #endif
    return float4(color, masks.a);
}

float4 CardFrost(float2 atlasUv)
{
    // R coverage, G lit edge, B contact shadow, A underside edge.
    float4 masks = tex2D(_MainTex, atlasUv);
    float4 details = tex2D(_ShellDetailTex, CardLocalUv(atlasUv));
    float2 p = CardLocalUv(atlasUv);
    float3 back = tex2D(_BackdropTex, _BackdropRect.xy + p * _BackdropRect.zw).rgb;
    float diagonal = smoothstep(.35,.65,p.x*.67+p.y*.46);
    float3 frost = lerp(float3(.72,.83,.92), float3(.925,.96,.99), p.y*.25+.49);
    frost += diagonal*.01;
    #if !defined(UNITY_COLORSPACE_GAMMA)
    frost = GammaToLinearSpace(frost);
    #endif
    float grain = (CardHash(floor(p * float2(756,1056))) - .5) * _GrainStrength;
    float opacity = saturate(_PanelOpacity + (p.y < .46 ? .10 : 0));
    float3 face = lerp(back, frost, opacity) + grain;
    face += masks.g * .19;
    face -= masks.a * .18 + masks.b * .105;
    face = lerp(face,float3(.25,.38,.49),details.g*.45);
    float alpha = masks.r + (1-masks.r) * masks.b * .26;
    float3 col = lerp(float3(.10,.16,.23), face, masks.r / max(alpha,.001));
    return float4(saturate(col), saturate(alpha));
}
