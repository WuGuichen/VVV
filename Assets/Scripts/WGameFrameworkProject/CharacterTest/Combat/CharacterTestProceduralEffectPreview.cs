using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestProceduralEffectPreviewId
    {
        None = 0,
        LightSecondary = 1,
        HeavySecondary = 2,
        FullRigPulse = 3,
        SideSecondary = 4
    }

    public readonly struct CharacterTestProceduralEffectPreviewResult
    {
        public CharacterTestProceduralEffectPreviewResult(
            CharacterTestProceduralEffectPreviewId effectId,
            string triggerName,
            RuntimeFrame frame,
            int targetEntityId,
            bool accepted,
            string message,
            CharacterAuthorityPoseSnapshot snapshot)
        {
            EffectId = effectId;
            TriggerName = triggerName ?? string.Empty;
            Frame = frame;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            Accepted = accepted;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        public CharacterTestProceduralEffectPreviewId EffectId { get; }
        public string TriggerName { get; }
        public RuntimeFrame Frame { get; }
        public int TargetEntityId { get; }
        public bool Accepted { get; }
        public string Message { get; }
        public CharacterAuthorityPoseSnapshot Snapshot { get; }
        public bool HasResult => EffectId != CharacterTestProceduralEffectPreviewId.None
            || !string.IsNullOrEmpty(Message);

        public static CharacterTestProceduralEffectPreviewResult Empty =>
            new CharacterTestProceduralEffectPreviewResult(
                CharacterTestProceduralEffectPreviewId.None,
                string.Empty,
                RuntimeFrame.Zero,
                0,
                accepted: false,
                string.Empty,
                CharacterAuthorityPoseSnapshot.Empty());
    }

    internal static class CharacterTestProceduralEffectPreviewHotkeys
    {
        public static bool TryResolve(
            InputSnapshot snapshot,
            out CharacterTestProceduralEffectPreviewId effectId,
            out string triggerName)
        {
            if (snapshot.DebugPrimaryPressed)
            {
                effectId = CharacterTestProceduralEffectPreviewId.LightSecondary;
                triggerName = "F5";
                return true;
            }

            if (snapshot.DebugSecondaryPressed)
            {
                effectId = CharacterTestProceduralEffectPreviewId.HeavySecondary;
                triggerName = "F6";
                return true;
            }

            if (snapshot.AudioPrimaryPressed)
            {
                effectId = CharacterTestProceduralEffectPreviewId.FullRigPulse;
                triggerName = "F7";
                return true;
            }

            if (snapshot.AudioSecondaryPressed)
            {
                effectId = CharacterTestProceduralEffectPreviewId.SideSecondary;
                triggerName = "F8";
                return true;
            }

            effectId = CharacterTestProceduralEffectPreviewId.None;
            triggerName = string.Empty;
            return false;
        }
    }

    internal static class CharacterTestProceduralEffectPreviewRequests
    {
        private const float SourceHeight = 1.15f;
        private const float DirectionEpsilon = 0.0001f;

        public static bool TryCreateRequest(
            CharacterTestProceduralEffectPreviewId effectId,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            string triggerName,
            out CharacterProceduralBoneImpulseRequest request,
            out bool useFullRig)
        {
            request = default;
            useFullRig = false;
            if (target == null || effectId == CharacterTestProceduralEffectPreviewId.None)
                return false;

            Vector3 direction = ResolveDirection(effectId, source, target);
            Vector3 sourcePosition = ResolveSourcePosition(source, target, direction);
            float strength;
            float durationSeconds;
            int bodyRegionId;
            switch (effectId)
            {
                case CharacterTestProceduralEffectPreviewId.LightSecondary:
                    strength = 6.5f;
                    durationSeconds = 0.24f;
                    bodyRegionId = CharacterAuthorityPoseRegionIds.Torso;
                    break;
                case CharacterTestProceduralEffectPreviewId.HeavySecondary:
                    strength = 11f;
                    durationSeconds = 0.34f;
                    bodyRegionId = CharacterAuthorityPoseRegionIds.Torso;
                    break;
                case CharacterTestProceduralEffectPreviewId.FullRigPulse:
                    strength = 8f;
                    durationSeconds = 0.36f;
                    bodyRegionId = CharacterAuthorityPoseRegionIds.Broadcast;
                    useFullRig = true;
                    break;
                case CharacterTestProceduralEffectPreviewId.SideSecondary:
                    strength = 8.5f;
                    durationSeconds = 0.30f;
                    bodyRegionId = CharacterAuthorityPoseRegionIds.Torso;
                    break;
                default:
                    return false;
            }

            string correlationId = "character-test.effect-preview:"
                + EmptyAsNone(triggerName)
                + ":"
                + effectId
                + ":"
                + target.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            request = new CharacterProceduralBoneImpulseRequest(
                sourcePosition,
                direction,
                strength,
                durationSeconds,
                bodyRegionId,
                correlationId,
                useFullRig ? "preview.broadcast" : "preview",
                string.Empty);
            return request.HasEffect;
        }

        private static Vector3 ResolveDirection(
            CharacterTestProceduralEffectPreviewId effectId,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            if (effectId == CharacterTestProceduralEffectPreviewId.SideSecondary)
                return ResolveRight(target);

            if (source != null)
            {
                Vector3 delta = ToUnityPosition(target.Transform) - ToUnityPosition(source.Transform);
                delta.y = 0f;
                if (IsUsableDirection(delta))
                    return delta.normalized;
            }

            return ResolveForward(target);
        }

        private static Vector3 ResolveSourcePosition(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            Vector3 direction)
        {
            if (source != null)
                return ToUnityPosition(source.Transform) + Vector3.up * SourceHeight;

            return ToUnityPosition(target.Transform)
                + Vector3.up * SourceHeight
                - direction.normalized;
        }

        private static Vector3 ResolveForward(CharacterTestCharacterEntity entity)
        {
            float yawRadians = entity.Transform.YawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
        }

        private static Vector3 ResolveRight(CharacterTestCharacterEntity entity)
        {
            float yawRadians = entity.Transform.YawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(yawRadians), 0f, -Mathf.Sin(yawRadians));
        }

        private static Vector3 ToUnityPosition(CharacterTestCharacterTransformComponent transform)
        {
            return new Vector3(transform.X, transform.Y, transform.Z);
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > DirectionEpsilon;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
