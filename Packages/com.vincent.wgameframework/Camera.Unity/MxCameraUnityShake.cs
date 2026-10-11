using System;
using MxFramework.Camera;
using UnityEngine;

namespace MxFramework.Camera.Unity
{
    public enum MxCameraUnityShakeMode
    {
        LegacyCoreOffset,
        None,
        Procedural
    }

    [Serializable]
    public sealed class MxCameraUnityShakeSettings
    {
        public float positionAmplitude = 1f;
        public float rotationAmplitude = 3.5f;
        public float frequency = 22f;
        public float recoverySharpness = 14f;
        public float maxPositionOffset = 0.45f;
        public float maxRotationDegrees = 4f;
        [Range(0f, 1f)] public float directionalInfluence = 0.35f;
        public int seed = 1337;

        public MxCameraUnityShakeSettings Clone()
        {
            return new MxCameraUnityShakeSettings
            {
                positionAmplitude = positionAmplitude,
                rotationAmplitude = rotationAmplitude,
                frequency = frequency,
                recoverySharpness = recoverySharpness,
                maxPositionOffset = maxPositionOffset,
                maxRotationDegrees = maxRotationDegrees,
                directionalInfluence = directionalInfluence,
                seed = seed
            };
        }
    }

    public readonly struct MxCameraUnityShakeContext
    {
        public MxCameraUnityShakeContext(
            MxCameraUnityShakeMode mode,
            MxCameraState state,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 coreShakeOffset,
            float deltaTime,
            MxCameraUnityShakeSettings settings)
        {
            Mode = mode;
            State = state;
            BasePosition = basePosition;
            BaseRotation = baseRotation;
            CoreShakeOffset = coreShakeOffset;
            DeltaTime = Mathf.Max(0f, deltaTime);
            Settings = settings ?? new MxCameraUnityShakeSettings();
        }

        public MxCameraUnityShakeMode Mode { get; }
        public MxCameraState State { get; }
        public Vector3 BasePosition { get; }
        public Quaternion BaseRotation { get; }
        public Vector3 CoreShakeOffset { get; }
        public float DeltaTime { get; }
        public MxCameraUnityShakeSettings Settings { get; }
    }

    public readonly struct MxCameraUnityShakeResult
    {
        private MxCameraUnityShakeResult(
            bool hasResult,
            Vector3 position,
            Quaternion rotation,
            bool active,
            float intensity,
            Vector3 positionOffset,
            Vector3 rotationEulerOffset,
            Vector3 coreShakeOffset,
            string code,
            string message)
        {
            HasResult = hasResult;
            Position = position;
            Rotation = rotation;
            IsActive = active;
            Intensity = Mathf.Max(0f, intensity);
            PositionOffset = positionOffset;
            RotationEulerOffset = rotationEulerOffset;
            CoreShakeOffset = coreShakeOffset;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool HasResult { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsActive { get; }
        public float Intensity { get; }
        public Vector3 PositionOffset { get; }
        public Vector3 RotationEulerOffset { get; }
        public Vector3 CoreShakeOffset { get; }
        public string Code { get; }
        public string Message { get; }

        public static MxCameraUnityShakeResult Inactive(
            Vector3 position,
            Quaternion rotation,
            Vector3 coreShakeOffset,
            string message = "")
        {
            return new MxCameraUnityShakeResult(
                true,
                position,
                rotation,
                false,
                0f,
                Vector3.zero,
                Vector3.zero,
                coreShakeOffset,
                string.Empty,
                message);
        }

        public static MxCameraUnityShakeResult Applied(
            Vector3 position,
            Quaternion rotation,
            float intensity,
            Vector3 positionOffset,
            Vector3 rotationEulerOffset,
            Vector3 coreShakeOffset,
            string code = "",
            string message = "")
        {
            return new MxCameraUnityShakeResult(
                true,
                position,
                rotation,
                true,
                intensity,
                positionOffset,
                rotationEulerOffset,
                coreShakeOffset,
                code,
                message);
        }
    }

    public interface IMxCameraUnityShakePresenter
    {
        void Reset();
        MxCameraUnityShakeResult Resolve(in MxCameraUnityShakeContext context);
    }

    public sealed class MxCameraUnityProceduralShakePresenter : IMxCameraUnityShakePresenter
    {
        private const float Epsilon = 0.0001f;

        private float _time;
        private float _intensity;

        public void Reset()
        {
            _time = 0f;
            _intensity = 0f;
        }

        public MxCameraUnityShakeResult Resolve(in MxCameraUnityShakeContext context)
        {
            if (context.Mode != MxCameraUnityShakeMode.Procedural)
                return MxCameraUnityShakeResult.Inactive(
                    context.BasePosition,
                    context.BaseRotation,
                    context.CoreShakeOffset);

            MxCameraUnityShakeSettings settings = context.Settings;
            float incoming = context.CoreShakeOffset.magnitude;
            if (incoming > _intensity)
                _intensity = incoming;
            else
                _intensity = SmoothToZero(_intensity, settings.recoverySharpness, context.DeltaTime);

            if (_intensity <= Epsilon)
            {
                _intensity = 0f;
                return MxCameraUnityShakeResult.Inactive(
                    context.BasePosition,
                    context.BaseRotation,
                    context.CoreShakeOffset);
            }

            _time += Mathf.Max(0.001f, settings.frequency) * Mathf.Max(context.DeltaTime, 1f / 120f);
            int seed = settings.seed;
            Vector3 localNoise = new Vector3(
                Noise(seed, 0, _time),
                Noise(seed, 1, _time),
                Noise(seed, 2, _time) * 0.35f);
            Vector3 worldNoise = context.BaseRotation * localNoise;
            Vector3 directional = context.CoreShakeOffset.sqrMagnitude > Epsilon * Epsilon
                ? context.CoreShakeOffset.normalized
                : worldNoise.normalized;
            Vector3 mixedDirection = Vector3.Lerp(
                worldNoise,
                directional,
                Mathf.Clamp01(settings.directionalInfluence));
            if (mixedDirection.sqrMagnitude <= Epsilon * Epsilon)
                mixedDirection = context.BaseRotation * Vector3.right;

            float positionScale = _intensity * Mathf.Max(0f, settings.positionAmplitude);
            Vector3 positionOffset = Vector3.ClampMagnitude(
                mixedDirection.normalized * positionScale,
                Mathf.Max(0f, settings.maxPositionOffset));
            Vector3 rotationEulerOffset = new Vector3(
                Noise(seed, 3, _time),
                Noise(seed, 4, _time),
                Noise(seed, 5, _time) * 0.5f)
                * (_intensity * Mathf.Max(0f, settings.rotationAmplitude));
            rotationEulerOffset = Vector3.ClampMagnitude(
                rotationEulerOffset,
                Mathf.Max(0f, settings.maxRotationDegrees));

            return MxCameraUnityShakeResult.Applied(
                context.BasePosition + positionOffset,
                context.BaseRotation * Quaternion.Euler(rotationEulerOffset),
                _intensity,
                positionOffset,
                rotationEulerOffset,
                context.CoreShakeOffset,
                "CAM_SHAKE_PROCEDURAL",
                "Unity camera procedural shake was applied.");
        }

        private static float SmoothToZero(float value, float sharpness, float deltaTime)
        {
            if (value <= 0f)
                return 0f;

            float safeSharpness = Mathf.Max(0f, sharpness);
            if (safeSharpness <= 0f || deltaTime <= 0f)
                return 0f;

            float t = 1f - Mathf.Exp(-safeSharpness * deltaTime);
            float next = Mathf.Lerp(value, 0f, t);
            return next <= Epsilon ? 0f : next;
        }

        private static float Noise(int seed, int channel, float time)
        {
            float x = seed * 0.0137f + channel * 11.17f;
            float y = time + channel * 7.31f;
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}
