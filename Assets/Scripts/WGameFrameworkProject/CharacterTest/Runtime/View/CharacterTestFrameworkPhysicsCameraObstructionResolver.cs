using System;
using System.Collections.Generic;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFrameworkPhysicsCameraObstructionResolver
        : IMxCameraUnityObstructionResolver, IMxCameraUnityObstructionResolverNoAlloc
    {
        private const int TraceId = 9101;
        private const int ActionId = 1401;

        private readonly Func<CharacterTestCharacterPhysicsRuntime> _runtimeProvider;
        private readonly Func<Vector3?> _focusOverrideProvider;
        private readonly List<CombatQueryResult> _hits = new List<CombatQueryResult>(8);
        private int _nextQueryId = 1;
        private bool _hasResolvedDistance;
        private float _resolvedDistance;

        public CharacterTestFrameworkPhysicsCameraObstructionResolver(
            Func<CharacterTestCharacterPhysicsRuntime> runtimeProvider,
            Func<Vector3?> focusOverrideProvider = null)
        {
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _focusOverrideProvider = focusOverrideProvider;
        }

        public void Reset()
        {
            _hits.Clear();
            _hasResolvedDistance = false;
            _resolvedDistance = 0f;
        }

        public MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContext context)
        {
            if (context.Mode != MxCameraUnityObstructionMode.FrameworkPhysicsAabb)
                return MxCameraUnityObstructionResult.Unobstructed(context.DesiredPosition);

            CharacterTestCharacterPhysicsRuntime runtime = _runtimeProvider();
            if (runtime == null || !runtime.IsReady || runtime.World == null)
                return MxCameraUnityObstructionResult.Unobstructed(context.DesiredPosition);

            Vector3 focus = ResolveFocus(in context);
            Vector3 toDesired = context.DesiredPosition - focus;
            float idealDistance = toDesired.magnitude;
            if (idealDistance <= 0.001f)
            {
                _hasResolvedDistance = false;
                _resolvedDistance = 0f;
                return MxCameraUnityObstructionResult.Unobstructed(
                    context.DesiredPosition,
                    idealDistance,
                    idealDistance);
            }

            Vector3 direction = toDesired / idealDistance;
            MxCameraUnityObstructionSettings settings = context.Settings;
            float radius = Mathf.Max(0.001f, settings.radius);
            float padding = Mathf.Max(0f, settings.padding);
            float minDistance = Mathf.Clamp(Mathf.Max(0f, settings.minDistance), 0f, idealDistance);
            float focusClearance = Mathf.Clamp(Mathf.Max(0f, settings.focusClearance), 0f, idealDistance);
            Vector3 queryStart = focus + direction * focusClearance;
            float queryLength = Mathf.Max(0f, idealDistance - focusClearance);
            if (queryLength <= 0.001f)
                return ResolveUnobstructedPosition(focus, direction, idealDistance, context.DeltaTime, settings);

            _hits.Clear();
            var query = new CombatCapsuleQuery(
                new CombatQueryHeader(
                    NextQueryId(),
                    CombatQueryKind.Capsule,
                    CombatEntityId.None,
                    TraceId,
                    ActionId,
                    0,
                    runtime.ObstacleLayerMask),
                ToFixVector3(queryStart),
                ToFixVector3(context.DesiredPosition),
                ToFix64(radius));
            runtime.World.QueryCapsule(query, _hits, includeSourceEntity: false);

            if (_hits.Count == 0)
                return ResolveUnobstructedPosition(focus, direction, idealDistance, context.DeltaTime, settings);

            CombatQueryResult hit = _hits[0];
            float hitDistance = ToFloat(hit.Distance);
            float targetDistance = Mathf.Clamp(
                focusClearance + Mathf.Max(0f, hitDistance - padding),
                minDistance,
                idealDistance);
            _resolvedDistance = targetDistance;
            _hasResolvedDistance = true;
            Vector3 resolvedPosition = focus + direction * targetDistance;
            return MxCameraUnityObstructionResult.Obstructed(
                resolvedPosition,
                idealDistance,
                targetDistance,
                focusClearance + hitDistance,
                ToVector3(hit.Normal),
                "CAM_OBSTRUCTION_FRAMEWORK_PHYSICS_AABB",
                "Camera position was moved in front of a framework physics AABB.");
        }

        public MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContextValue context)
        {
            if (context.Mode != MxCameraUnityObstructionMode.FrameworkPhysicsAabb)
                return MxCameraUnityObstructionResult.Unobstructed(context.DesiredPosition);

            CharacterTestCharacterPhysicsRuntime runtime = _runtimeProvider();
            if (runtime == null || !runtime.IsReady || runtime.World == null)
                return MxCameraUnityObstructionResult.Unobstructed(context.DesiredPosition);

            Vector3 focus = ResolveFocus(in context);
            Vector3 toDesired = context.DesiredPosition - focus;
            float idealDistance = toDesired.magnitude;
            if (idealDistance <= 0.001f)
            {
                _hasResolvedDistance = false;
                _resolvedDistance = 0f;
                return MxCameraUnityObstructionResult.Unobstructed(
                    context.DesiredPosition,
                    idealDistance,
                    idealDistance);
            }

            Vector3 direction = toDesired / idealDistance;
            MxCameraUnityObstructionSettings settings = context.Settings;
            float radius = Mathf.Max(0.001f, settings.radius);
            float padding = Mathf.Max(0f, settings.padding);
            float minDistance = Mathf.Clamp(Mathf.Max(0f, settings.minDistance), 0f, idealDistance);
            float focusClearance = Mathf.Clamp(Mathf.Max(0f, settings.focusClearance), 0f, idealDistance);
            Vector3 queryStart = focus + direction * focusClearance;
            float queryLength = Mathf.Max(0f, idealDistance - focusClearance);
            if (queryLength <= 0.001f)
                return ResolveUnobstructedPosition(focus, direction, idealDistance, context.DeltaTime, settings);

            _hits.Clear();
            var query = new CombatCapsuleQuery(
                new CombatQueryHeader(
                    NextQueryId(),
                    CombatQueryKind.Capsule,
                    CombatEntityId.None,
                    TraceId,
                    ActionId,
                    0,
                    runtime.ObstacleLayerMask),
                ToFixVector3(queryStart),
                ToFixVector3(context.DesiredPosition),
                ToFix64(radius));
            runtime.World.QueryCapsule(query, _hits, includeSourceEntity: false);

            if (_hits.Count == 0)
                return ResolveUnobstructedPosition(focus, direction, idealDistance, context.DeltaTime, settings);

            CombatQueryResult hit = _hits[0];
            float hitDistance = ToFloat(hit.Distance);
            float targetDistance = Mathf.Clamp(
                focusClearance + Mathf.Max(0f, hitDistance - padding),
                minDistance,
                idealDistance);
            _resolvedDistance = targetDistance;
            _hasResolvedDistance = true;
            Vector3 resolvedPosition = focus + direction * targetDistance;
            return MxCameraUnityObstructionResult.Obstructed(
                resolvedPosition,
                idealDistance,
                targetDistance,
                focusClearance + hitDistance,
                ToVector3(hit.Normal),
                "CAM_OBSTRUCTION_FRAMEWORK_PHYSICS_AABB",
                "Camera position was moved in front of a framework physics AABB.");
        }

        private Vector3 ResolveFocus(in MxCameraUnityObstructionContext context)
        {
            Vector3? focusOverride = _focusOverrideProvider != null
                ? _focusOverrideProvider()
                : null;
            if (focusOverride.HasValue && IsFinite(focusOverride.Value))
                return focusOverride.Value;

            return context.State != null
                ? MxCameraUnityConversions.ToUnityVector(context.State.FocusCenter)
                : context.DesiredPosition + context.DesiredRotation * Vector3.forward;
        }

        private Vector3 ResolveFocus(in MxCameraUnityObstructionContextValue context)
        {
            Vector3? focusOverride = _focusOverrideProvider != null
                ? _focusOverrideProvider()
                : null;
            if (focusOverride.HasValue && IsFinite(focusOverride.Value))
                return focusOverride.Value;

            return MxCameraUnityConversions.ToUnityVector(context.State.FocusCenter);
        }

        private MxCameraUnityObstructionResult ResolveUnobstructedPosition(
            Vector3 focus,
            Vector3 direction,
            float idealDistance,
            float deltaTime,
            MxCameraUnityObstructionSettings settings)
        {
            float nextDistance = idealDistance;
            if (_hasResolvedDistance && _resolvedDistance < idealDistance - 0.001f)
                nextDistance = SmoothDistance(_resolvedDistance, idealDistance, settings.recoverySharpness, deltaTime);

            _resolvedDistance = nextDistance;
            _hasResolvedDistance = nextDistance < idealDistance - 0.001f;
            return MxCameraUnityObstructionResult.Unobstructed(
                focus + direction * nextDistance,
                idealDistance,
                nextDistance);
        }

        private int NextQueryId()
        {
            if (_nextQueryId == int.MaxValue)
                _nextQueryId = 1;

            return _nextQueryId++;
        }

        private static float SmoothDistance(float current, float target, float sharpness, float deltaTime)
        {
            float safeSharpness = Mathf.Max(0f, sharpness);
            if (safeSharpness <= 0f || deltaTime <= 0f)
                return target;

            float t = 1f - Mathf.Exp(-safeSharpness * deltaTime);
            float next = Mathf.Lerp(current, target, t);
            return Mathf.Abs(next - target) <= 0.001f ? target : next;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(ToFix64(value.x), ToFix64(value.y), ToFix64(value.z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }
    }
}
