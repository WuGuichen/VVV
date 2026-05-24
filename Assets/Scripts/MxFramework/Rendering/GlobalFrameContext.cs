using UnityEngine;

namespace MxFramework.Rendering
{
    public interface IGlobalFrameContext
    {
        void SetTime(float time, float gameTime, float deltaTime);
        void SetWind(Vector3 direction, float strength, float turbulence);
        void SetWeather(float wetness, float rain, float snowCoverage);
        void SetPrimarySubjectPose(Vector3 worldPosition, Vector3 velocity);
        void SetLocalSubjectPose(Vector3 worldPosition, Vector3 velocity);
        GlobalFrameSnapshot Snapshot();
    }

    public sealed class GlobalFrameContext : IGlobalFrameContext
    {
        private GlobalFrameSnapshot _snapshot;

        public void SetTime(float time, float gameTime, float deltaTime)
        {
            _snapshot = _snapshot.WithTime(time, gameTime, deltaTime);
            Shader.SetGlobalVector(MxRenderingShaderIds.MxTime, new Vector4(time, gameTime, deltaTime, 0f));
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxGameTime, gameTime);
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxDeltaTime, deltaTime);
        }

        public void SetWind(Vector3 direction, float strength, float turbulence)
        {
            _snapshot = _snapshot.WithWind(direction, strength, turbulence);
            Shader.SetGlobalVector(MxRenderingShaderIds.MxWindDirection, ToVector4(direction));
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxWindStrength, strength);
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxWindTurbulence, turbulence);
        }

        public void SetWeather(float wetness, float rain, float snowCoverage)
        {
            _snapshot = _snapshot.WithWeather(wetness, rain, snowCoverage);
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxWetness, wetness);
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxRain, rain);
            Shader.SetGlobalFloat(MxRenderingShaderIds.MxSnowCoverage, snowCoverage);
        }

        public void SetPrimarySubjectPose(Vector3 worldPosition, Vector3 velocity)
        {
            _snapshot = _snapshot.WithPrimarySubjectPose(worldPosition, velocity);
            Shader.SetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectWorldPos, ToVector4(worldPosition));
            Shader.SetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectVelocity, ToVector4(velocity));
        }

        public void SetLocalSubjectPose(Vector3 worldPosition, Vector3 velocity)
        {
            _snapshot = _snapshot.WithLocalSubjectPose(worldPosition, velocity);
            Shader.SetGlobalVector(MxRenderingShaderIds.MxLocalSubjectWorldPos, ToVector4(worldPosition));
        }

        public GlobalFrameSnapshot Snapshot()
        {
            return _snapshot;
        }

        private static Vector4 ToVector4(Vector3 value)
        {
            return new Vector4(value.x, value.y, value.z, 0f);
        }
    }

    public readonly struct GlobalFrameSnapshot
    {
        public GlobalFrameSnapshot(
            float time,
            float gameTime,
            float deltaTime,
            Vector3 windDirection,
            float windStrength,
            float windTurbulence,
            float wetness,
            float rain,
            float snowCoverage,
            Vector3 primarySubjectWorldPos,
            Vector3 primarySubjectVelocity,
            Vector3 localSubjectWorldPos,
            Vector3 localSubjectVelocity)
        {
            Time = time;
            GameTime = gameTime;
            DeltaTime = deltaTime;
            WindDirection = windDirection;
            WindStrength = windStrength;
            WindTurbulence = windTurbulence;
            Wetness = wetness;
            Rain = rain;
            SnowCoverage = snowCoverage;
            PrimarySubjectWorldPos = primarySubjectWorldPos;
            PrimarySubjectVelocity = primarySubjectVelocity;
            LocalSubjectWorldPos = localSubjectWorldPos;
            LocalSubjectVelocity = localSubjectVelocity;
        }

        public float Time { get; }
        public float GameTime { get; }
        public float DeltaTime { get; }
        public Vector3 WindDirection { get; }
        public float WindStrength { get; }
        public float WindTurbulence { get; }
        public float Wetness { get; }
        public float Rain { get; }
        public float SnowCoverage { get; }
        public Vector3 PrimarySubjectWorldPos { get; }
        public Vector3 PrimarySubjectVelocity { get; }
        public Vector3 LocalSubjectWorldPos { get; }
        public Vector3 LocalSubjectVelocity { get; }

        internal GlobalFrameSnapshot WithTime(float time, float gameTime, float deltaTime)
        {
            return new GlobalFrameSnapshot(
                time,
                gameTime,
                deltaTime,
                WindDirection,
                WindStrength,
                WindTurbulence,
                Wetness,
                Rain,
                SnowCoverage,
                PrimarySubjectWorldPos,
                PrimarySubjectVelocity,
                LocalSubjectWorldPos,
                LocalSubjectVelocity);
        }

        internal GlobalFrameSnapshot WithWind(Vector3 direction, float strength, float turbulence)
        {
            return new GlobalFrameSnapshot(
                Time,
                GameTime,
                DeltaTime,
                direction,
                strength,
                turbulence,
                Wetness,
                Rain,
                SnowCoverage,
                PrimarySubjectWorldPos,
                PrimarySubjectVelocity,
                LocalSubjectWorldPos,
                LocalSubjectVelocity);
        }

        internal GlobalFrameSnapshot WithWeather(float wetness, float rain, float snowCoverage)
        {
            return new GlobalFrameSnapshot(
                Time,
                GameTime,
                DeltaTime,
                WindDirection,
                WindStrength,
                WindTurbulence,
                wetness,
                rain,
                snowCoverage,
                PrimarySubjectWorldPos,
                PrimarySubjectVelocity,
                LocalSubjectWorldPos,
                LocalSubjectVelocity);
        }

        internal GlobalFrameSnapshot WithPrimarySubjectPose(Vector3 worldPosition, Vector3 velocity)
        {
            return new GlobalFrameSnapshot(
                Time,
                GameTime,
                DeltaTime,
                WindDirection,
                WindStrength,
                WindTurbulence,
                Wetness,
                Rain,
                SnowCoverage,
                worldPosition,
                velocity,
                LocalSubjectWorldPos,
                LocalSubjectVelocity);
        }

        internal GlobalFrameSnapshot WithLocalSubjectPose(Vector3 worldPosition, Vector3 velocity)
        {
            return new GlobalFrameSnapshot(
                Time,
                GameTime,
                DeltaTime,
                WindDirection,
                WindStrength,
                WindTurbulence,
                Wetness,
                Rain,
                SnowCoverage,
                PrimarySubjectWorldPos,
                PrimarySubjectVelocity,
                worldPosition,
                velocity);
        }
    }
}
