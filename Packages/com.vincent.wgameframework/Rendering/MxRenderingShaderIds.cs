using UnityEngine;

namespace MxFramework.Rendering
{
    public static class MxRenderingShaderIds
    {
        public const string MxTimeName = "_MxTime";
        public const string MxTimeProvidedName = "_MxTimeProvided";
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
        public const string MxVolumetricFogParamsName = "_MxVolumetricFogParams";
        public const string MxVolumetricFogHeightParamsName = "_MxVolumetricFogHeightParams";
        public const string MxVolumetricFogScatteringParamsName = "_MxVolumetricFogScatteringParams";
        public const string MxVolumetricFogFocusParamsName = "_MxVolumetricFogFocusParams";
        public const string MxVolumetricFogColorName = "_MxVolumetricFogColor";
        public const string MxVolumetricFogCompositeTempName = "_MxVolumetricFogCompositeTemp";

        public static readonly int MxTime = Shader.PropertyToID(MxTimeName);
        public static readonly int MxTimeProvided = Shader.PropertyToID(MxTimeProvidedName);
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
        public static readonly int MxVolumetricFogParams = Shader.PropertyToID(MxVolumetricFogParamsName);
        public static readonly int MxVolumetricFogHeightParams = Shader.PropertyToID(MxVolumetricFogHeightParamsName);
        public static readonly int MxVolumetricFogScatteringParams = Shader.PropertyToID(MxVolumetricFogScatteringParamsName);
        public static readonly int MxVolumetricFogFocusParams = Shader.PropertyToID(MxVolumetricFogFocusParamsName);
        public static readonly int MxVolumetricFogColor = Shader.PropertyToID(MxVolumetricFogColorName);
        public static readonly int MxVolumetricFogCompositeTemp = Shader.PropertyToID(MxVolumetricFogCompositeTempName);

        public static readonly int[] GlobalFramePropertyIds =
        {
            MxTime,
            MxTimeProvided,
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
