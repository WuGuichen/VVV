using UnityEngine;

namespace MxFramework.Rendering
{
    public static class MxRenderingShaderIds
    {
        public const string MxTimeName = "_MxTime";
        public const string MxGameTimeName = "_MxGameTime";
        public const string MxDeltaTimeName = "_MxDeltaTime";
        public const string MxWindDirectionName = "_MxWindDirection";
        public const string MxWindStrengthName = "_MxWindStrength";
        public const string MxWindTurbulenceName = "_MxWindTurbulence";
        public const string MxWetnessName = "_MxWetness";
        public const string MxRainName = "_MxRain";
        public const string MxSnowCoverageName = "_MxSnowCoverage";
        public const string MxPrimarySubjectWorldPosName = "_MxPrimarySubjectWorldPos";
        public const string MxPrimarySubjectVelocityName = "_MxPrimarySubjectVelocity";
        public const string MxLocalSubjectWorldPosName = "_MxLocalSubjectWorldPos";
        public const string MxViewFocusWorldPosName = "_MxViewFocusWorldPos";

        public static readonly int MxTime = Shader.PropertyToID(MxTimeName);
        public static readonly int MxGameTime = Shader.PropertyToID(MxGameTimeName);
        public static readonly int MxDeltaTime = Shader.PropertyToID(MxDeltaTimeName);
        public static readonly int MxWindDirection = Shader.PropertyToID(MxWindDirectionName);
        public static readonly int MxWindStrength = Shader.PropertyToID(MxWindStrengthName);
        public static readonly int MxWindTurbulence = Shader.PropertyToID(MxWindTurbulenceName);
        public static readonly int MxWetness = Shader.PropertyToID(MxWetnessName);
        public static readonly int MxRain = Shader.PropertyToID(MxRainName);
        public static readonly int MxSnowCoverage = Shader.PropertyToID(MxSnowCoverageName);
        public static readonly int MxPrimarySubjectWorldPos = Shader.PropertyToID(MxPrimarySubjectWorldPosName);
        public static readonly int MxPrimarySubjectVelocity = Shader.PropertyToID(MxPrimarySubjectVelocityName);
        public static readonly int MxLocalSubjectWorldPos = Shader.PropertyToID(MxLocalSubjectWorldPosName);
        public static readonly int MxViewFocusWorldPos = Shader.PropertyToID(MxViewFocusWorldPosName);

        public static readonly int[] GlobalFramePropertyIds =
        {
            MxTime,
            MxGameTime,
            MxDeltaTime,
            MxWindDirection,
            MxWindStrength,
            MxWindTurbulence,
            MxWetness,
            MxRain,
            MxSnowCoverage,
            MxPrimarySubjectWorldPos,
            MxPrimarySubjectVelocity,
            MxLocalSubjectWorldPos
        };

        public static readonly int[] CameraFramePropertyIds =
        {
            MxViewFocusWorldPos
        };
    }
}
