#ifndef MX_FRAME_INPUTS_INCLUDED
#define MX_FRAME_INPUTS_INCLUDED

float4 _MxTime;
float _MxTimeProvided;
float _MxGameTime, _MxDeltaTime;
float4 _MxWindDirection;
float _MxWindStrength, _MxWindTurbulence;
float _MxWetness, _MxRain, _MxSnowCoverage;
float4 _MxPrimarySubjectWorldPos, _MxPrimarySubjectVelocity;
float4 _MxLocalSubjectWorldPos, _MxViewFocusWorldPos;
float _MxPipelineHeightFogEnabled;

// An explicit clock may be paused at zero; otherwise use Unity's render clock.
float MxPresentationTime() { return _MxTimeProvided > 0.5 ? _MxTime.x : _Time.y; }
float MxGameTime() { return _MxTimeProvided > 0.5 ? _MxTime.y : _Time.y; }

#endif
