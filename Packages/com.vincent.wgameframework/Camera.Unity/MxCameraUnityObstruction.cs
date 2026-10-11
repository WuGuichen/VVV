using System;
using MxFramework.Camera;
using UnityEngine;

namespace MxFramework.Camera.Unity
{
    public enum MxCameraUnityObstructionMode
    {
        None,
        FrameworkPhysicsAabb,
        UnityPhysicsSphereCast
    }

    [Serializable]
    public sealed class MxCameraUnityObstructionSettings
    {
        public float radius = 0.22f;
        public float padding = 0.12f;
        public float minDistance = 0.75f;
        public float focusClearance = 0.25f;
        public float recoverySharpness = 12f;

        public MxCameraUnityObstructionSettings Clone()
        {
            return new MxCameraUnityObstructionSettings
            {
                radius = radius,
                padding = padding,
                minDistance = minDistance,
                focusClearance = focusClearance,
                recoverySharpness = recoverySharpness
            };
        }
    }

    public readonly struct MxCameraUnityObstructionContext
    {
        public MxCameraUnityObstructionContext(
            MxCameraUnityObstructionMode mode,
            MxCameraState state,
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float deltaTime,
            MxCameraUnityObstructionSettings settings)
        {
            Mode = mode;
            State = state;
            DesiredPosition = desiredPosition;
            DesiredRotation = desiredRotation;
            DeltaTime = Mathf.Max(0f, deltaTime);
            Settings = settings ?? new MxCameraUnityObstructionSettings();
        }

        public MxCameraUnityObstructionMode Mode { get; }
        public MxCameraState State { get; }
        public Vector3 DesiredPosition { get; }
        public Quaternion DesiredRotation { get; }
        public float DeltaTime { get; }
        public MxCameraUnityObstructionSettings Settings { get; }
    }

    public readonly struct MxCameraUnityObstructionContextValue
    {
        public MxCameraUnityObstructionContextValue(
            MxCameraUnityObstructionMode mode,
            MxCameraStateValue state,
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float deltaTime,
            MxCameraUnityObstructionSettings settings)
        {
            Mode = mode;
            State = state;
            DesiredPosition = desiredPosition;
            DesiredRotation = desiredRotation;
            DeltaTime = Mathf.Max(0f, deltaTime);
            Settings = settings ?? new MxCameraUnityObstructionSettings();
        }

        public MxCameraUnityObstructionMode Mode { get; }
        public MxCameraStateValue State { get; }
        public Vector3 DesiredPosition { get; }
        public Quaternion DesiredRotation { get; }
        public float DeltaTime { get; }
        public MxCameraUnityObstructionSettings Settings { get; }
    }

    public readonly struct MxCameraUnityObstructionResult
    {
        private MxCameraUnityObstructionResult(
            bool hasResult,
            Vector3 position,
            bool obstructed,
            float idealDistance,
            float resolvedDistance,
            float hitDistance,
            Vector3 hitNormal,
            string code,
            string message)
        {
            HasResult = hasResult;
            Position = position;
            IsObstructed = obstructed;
            IdealDistance = Mathf.Max(0f, idealDistance);
            ResolvedDistance = Mathf.Max(0f, resolvedDistance);
            HitDistance = Mathf.Max(0f, hitDistance);
            HitNormal = hitNormal;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool HasResult { get; }
        public Vector3 Position { get; }
        public bool IsObstructed { get; }
        public float IdealDistance { get; }
        public float ResolvedDistance { get; }
        public float HitDistance { get; }
        public Vector3 HitNormal { get; }
        public string Code { get; }
        public string Message { get; }

        public static MxCameraUnityObstructionResult Unobstructed(
            Vector3 position,
            float idealDistance = 0f,
            float resolvedDistance = 0f,
            string message = "")
        {
            float distance = resolvedDistance > 0f ? resolvedDistance : idealDistance;
            return new MxCameraUnityObstructionResult(
                true,
                position,
                false,
                idealDistance,
                distance,
                0f,
                Vector3.zero,
                string.Empty,
                message);
        }

        public static MxCameraUnityObstructionResult Obstructed(
            Vector3 position,
            float idealDistance,
            float resolvedDistance,
            float hitDistance,
            Vector3 hitNormal,
            string code = "",
            string message = "")
        {
            return new MxCameraUnityObstructionResult(
                true,
                position,
                true,
                idealDistance,
                resolvedDistance,
                hitDistance,
                hitNormal,
                code,
                message);
        }
    }

    public interface IMxCameraUnityObstructionResolver
    {
        MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContext context);
    }

    public interface IMxCameraUnityObstructionResolverNoAlloc
    {
        MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContextValue context);
    }
}
