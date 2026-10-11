using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBodyPartContactMotionState
    {
        Static = 0,
        BakedSocket = 1,
        MissingSocket = 2,
        MissingClip = 3,
        MissingFrame = 4,
        MissingSource = 5,
        PresentationPose = 6
    }

    public readonly struct CharacterTestPhysicalAudioSummary
    {
        public static readonly CharacterTestPhysicalAudioSummary Empty =
            new CharacterTestPhysicalAudioSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                0);

        public CharacterTestPhysicalAudioSummary(
            RuntimeFrame frame,
            int characterCount,
            int movingGroundedCharacterCount,
            int bodyPartContactCount,
            int bodyPartContactPlayedCount,
            int bodyPartProbeMissCount,
            int bodyPartFilteredContactCount,
            int leftFootContactCount,
            int rightFootContactCount,
            int landingPlayedCount,
            int objectContactCount,
            int objectCollisionPlayedCount,
            int footProbeMissCount,
            int cooldownSkipCount,
            int presentationMotionResolvedCount,
            int bakedMotionResolvedCount,
            int staticMotionCount,
            int motionFallbackCount,
            int missingMotionSocketCount,
            int missingMotionClipCount,
            int missingMotionFrameCount,
            int missingMotionSourceCount,
            int missingPhysicsRuntimeSkipCount,
            int missingContactPointConfigSkipCount,
            int registrationCapacitySkipCount,
            int skippedCount,
            string lastCueKey,
            int lastEmitterId,
            int cooldownCapacitySkipCount = 0,
            int contactPointEstimatedCount = 0,
            int solverVelocityDeltaCount = 0,
            int contactDynamicsBaselineSuppressCount = 0,
            int dynamicTargetFallbackCount = 0)
        {
            Frame = frame;
            CharacterCount = characterCount < 0 ? 0 : characterCount;
            MovingGroundedCharacterCount = movingGroundedCharacterCount < 0 ? 0 : movingGroundedCharacterCount;
            BodyPartContactCount = bodyPartContactCount < 0 ? 0 : bodyPartContactCount;
            BodyPartContactPlayedCount = bodyPartContactPlayedCount < 0 ? 0 : bodyPartContactPlayedCount;
            BodyPartProbeMissCount = bodyPartProbeMissCount < 0 ? 0 : bodyPartProbeMissCount;
            BodyPartFilteredContactCount = bodyPartFilteredContactCount < 0 ? 0 : bodyPartFilteredContactCount;
            LeftFootContactCount = leftFootContactCount < 0 ? 0 : leftFootContactCount;
            RightFootContactCount = rightFootContactCount < 0 ? 0 : rightFootContactCount;
            LandingPlayedCount = landingPlayedCount < 0 ? 0 : landingPlayedCount;
            ObjectContactCount = objectContactCount < 0 ? 0 : objectContactCount;
            ObjectCollisionPlayedCount = objectCollisionPlayedCount < 0 ? 0 : objectCollisionPlayedCount;
            FootProbeMissCount = footProbeMissCount < 0 ? 0 : footProbeMissCount;
            CooldownSkipCount = cooldownSkipCount < 0 ? 0 : cooldownSkipCount;
            CooldownCapacitySkipCount = cooldownCapacitySkipCount < 0 ? 0 : cooldownCapacitySkipCount;
            ContactPointEstimatedCount = contactPointEstimatedCount < 0 ? 0 : contactPointEstimatedCount;
            SolverVelocityDeltaCount = solverVelocityDeltaCount < 0 ? 0 : solverVelocityDeltaCount;
            ContactDynamicsBaselineSuppressCount = contactDynamicsBaselineSuppressCount < 0
                ? 0
                : contactDynamicsBaselineSuppressCount;
            DynamicTargetFallbackCount = dynamicTargetFallbackCount < 0 ? 0 : dynamicTargetFallbackCount;
            PresentationMotionResolvedCount = presentationMotionResolvedCount < 0
                ? 0
                : presentationMotionResolvedCount;
            BakedMotionResolvedCount = bakedMotionResolvedCount < 0 ? 0 : bakedMotionResolvedCount;
            StaticMotionCount = staticMotionCount < 0 ? 0 : staticMotionCount;
            MotionFallbackCount = motionFallbackCount < 0 ? 0 : motionFallbackCount;
            MissingMotionSocketCount = missingMotionSocketCount < 0 ? 0 : missingMotionSocketCount;
            MissingMotionClipCount = missingMotionClipCount < 0 ? 0 : missingMotionClipCount;
            MissingMotionFrameCount = missingMotionFrameCount < 0 ? 0 : missingMotionFrameCount;
            MissingMotionSourceCount = missingMotionSourceCount < 0 ? 0 : missingMotionSourceCount;
            MissingPhysicsRuntimeSkipCount = missingPhysicsRuntimeSkipCount < 0
                ? 0
                : missingPhysicsRuntimeSkipCount;
            MissingContactPointConfigSkipCount = missingContactPointConfigSkipCount < 0
                ? 0
                : missingContactPointConfigSkipCount;
            RegistrationCapacitySkipCount = registrationCapacitySkipCount < 0
                ? 0
                : registrationCapacitySkipCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            LastCueKey = lastCueKey ?? string.Empty;
            LastEmitterId = lastEmitterId;
        }

        public RuntimeFrame Frame { get; }
        public int CharacterCount { get; }
        public int MovingGroundedCharacterCount { get; }
        public int BodyPartContactCount { get; }
        public int BodyPartContactPlayedCount { get; }
        public int BodyPartProbeMissCount { get; }
        public int BodyPartFilteredContactCount { get; }
        public int LeftFootContactCount { get; }
        public int RightFootContactCount { get; }
        public int LandingPlayedCount { get; }
        public int ObjectContactCount { get; }
        public int ObjectCollisionPlayedCount { get; }
        public int FootProbeMissCount { get; }
        public int CooldownSkipCount { get; }
        public int CooldownCapacitySkipCount { get; }
        public int ContactPointEstimatedCount { get; }
        public int SolverVelocityDeltaCount { get; }
        public int ContactDynamicsBaselineSuppressCount { get; }
        public int DynamicTargetFallbackCount { get; }
        public int PresentationMotionResolvedCount { get; }
        public int BakedMotionResolvedCount { get; }
        public int StaticMotionCount { get; }
        public int MotionFallbackCount { get; }
        public int MissingMotionSocketCount { get; }
        public int MissingMotionClipCount { get; }
        public int MissingMotionFrameCount { get; }
        public int MissingMotionSourceCount { get; }
        public int MissingPhysicsRuntimeSkipCount { get; }
        public int MissingContactPointConfigSkipCount { get; }
        public int RegistrationCapacitySkipCount { get; }
        public int SkippedCount { get; }
        public string LastCueKey { get; }
        public int LastEmitterId { get; }

        public string ToSummaryLine()
        {
            return "frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " characters=" + CharacterCount.ToString(CultureInfo.InvariantCulture)
                + " movingGrounded=" + MovingGroundedCharacterCount.ToString(CultureInfo.InvariantCulture)
                + " bodyPartContacts=" + BodyPartContactCount.ToString(CultureInfo.InvariantCulture)
                + " bodyPartAudio=" + BodyPartContactPlayedCount.ToString(CultureInfo.InvariantCulture)
                + " bodyPartMiss=" + BodyPartProbeMissCount.ToString(CultureInfo.InvariantCulture)
                + " bodyPartFiltered=" + BodyPartFilteredContactCount.ToString(CultureInfo.InvariantCulture)
                + " leftFootContacts=" + LeftFootContactCount.ToString(CultureInfo.InvariantCulture)
                + " rightFootContacts=" + RightFootContactCount.ToString(CultureInfo.InvariantCulture)
                + " landings=" + LandingPlayedCount.ToString(CultureInfo.InvariantCulture)
                + " objectContacts=" + ObjectContactCount.ToString(CultureInfo.InvariantCulture)
                + " objectAudio=" + ObjectCollisionPlayedCount.ToString(CultureInfo.InvariantCulture)
                + " probeMiss=" + FootProbeMissCount.ToString(CultureInfo.InvariantCulture)
                + " cooldownSkip=" + CooldownSkipCount.ToString(CultureInfo.InvariantCulture)
                + " cooldownCapacitySkip=" + CooldownCapacitySkipCount.ToString(CultureInfo.InvariantCulture)
                + " contactEstimated=" + ContactPointEstimatedCount.ToString(CultureInfo.InvariantCulture)
                + " solverDelta=" + SolverVelocityDeltaCount.ToString(CultureInfo.InvariantCulture)
                + " dynamicsBaselineSuppress="
                + ContactDynamicsBaselineSuppressCount.ToString(CultureInfo.InvariantCulture)
                + " dynamicTargetFallback=" + DynamicTargetFallbackCount.ToString(CultureInfo.InvariantCulture)
                + " presentationMotion=" + PresentationMotionResolvedCount.ToString(CultureInfo.InvariantCulture)
                + " bakedMotion=" + BakedMotionResolvedCount.ToString(CultureInfo.InvariantCulture)
                + " staticMotion=" + StaticMotionCount.ToString(CultureInfo.InvariantCulture)
                + " motionFallback=" + MotionFallbackCount.ToString(CultureInfo.InvariantCulture)
                + " missingMotionSocket=" + MissingMotionSocketCount.ToString(CultureInfo.InvariantCulture)
                + " missingMotionClip=" + MissingMotionClipCount.ToString(CultureInfo.InvariantCulture)
                + " missingMotionFrame=" + MissingMotionFrameCount.ToString(CultureInfo.InvariantCulture)
                + " missingMotionSource=" + MissingMotionSourceCount.ToString(CultureInfo.InvariantCulture)
                + " missingPhysics=" + MissingPhysicsRuntimeSkipCount.ToString(CultureInfo.InvariantCulture)
                + " missingContactPointConfig=" + MissingContactPointConfigSkipCount.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + SkippedCount.ToString(CultureInfo.InvariantCulture)
                + " lastAudio=" + (string.IsNullOrWhiteSpace(LastCueKey) ? "none" : LastCueKey)
                + " emitter=" + LastEmitterId.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal readonly struct CharacterTestBodyPartContactDebugSnapshot
    {
        public static readonly CharacterTestBodyPartContactDebugSnapshot Empty =
            new CharacterTestBodyPartContactDebugSnapshot(
                RuntimeFrame.Zero,
                Array.Empty<CharacterTestBodyPartContactDebugEntry>(),
                0);

        internal CharacterTestBodyPartContactDebugSnapshot(
            RuntimeFrame frame,
            CharacterTestBodyPartContactDebugEntry[] entries,
            int count)
        {
            Frame = frame;
            _entries = entries ?? Array.Empty<CharacterTestBodyPartContactDebugEntry>();
            Count = count < 0
                ? 0
                : count > _entries.Length
                    ? _entries.Length
                    : count;
        }

        private readonly CharacterTestBodyPartContactDebugEntry[] _entries;

        public RuntimeFrame Frame { get; }
        public int Count { get; }

        public CharacterTestBodyPartContactDebugEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _entries[index];
            }
        }
    }

    internal readonly struct BodyPartContactDebugKey : IEquatable<BodyPartContactDebugKey>
    {
        public BodyPartContactDebugKey(int sourceEntityId, int contactPointId, int bodyPartId)
        {
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            ContactPointId = contactPointId < 0 ? 0 : contactPointId;
            BodyPartId = bodyPartId < 0 ? 0 : bodyPartId;
        }

        public int SourceEntityId { get; }
        public int ContactPointId { get; }
        public int BodyPartId { get; }

        public bool Equals(BodyPartContactDebugKey other)
        {
            return SourceEntityId == other.SourceEntityId
                && ContactPointId == other.ContactPointId
                && BodyPartId == other.BodyPartId;
        }

        public override bool Equals(object obj)
        {
            return obj is BodyPartContactDebugKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceEntityId;
                hash = (hash * 397) ^ ContactPointId;
                hash = (hash * 397) ^ BodyPartId;
                return hash;
            }
        }
    }

    internal readonly struct CharacterTestBodyPartContactDebugEntry
    {
        private CharacterTestBodyPartContactDebugEntry(
            RuntimeFrame frame,
            int entityId,
            int sourceEntityId,
            int contactPointId,
            int bodyPartId,
            int sourceSlotId,
            int sourceOrder,
            CombatBodyPartContactActivationMode activationMode,
            CharacterBodyPartContactMotionMode motionMode,
            int motionSocketId,
            CharacterTestBodyPartContactMotionState motionState,
            string motionClipKey,
            int motionLocalFrame,
            CombatPhysicsLayerMask layerMask,
            FixVector3 center,
            Fix64 radius,
            bool active,
            bool hasHit,
            bool contactAllowed,
            bool audioQueued,
            CombatPhysicalContactPhase phase,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShapeKind targetShapeKind,
            FixVector3 hitPoint,
            FixVector3 hitNormal,
            CombatSurfaceMaterial surfaceMaterial,
            int relativeSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int pressure,
            int intensity,
            PhysicalAudioContactDynamicsSource dynamicsSource = PhysicalAudioContactDynamicsSource.Unknown,
            float dynamicsNormalSpeedMetersPerSecond = 0f,
            float dynamicsTangentialSpeedMetersPerSecond = 0f,
            float normalImpulseNewtonSeconds = 0f,
            float averagePressurePascals = 0f)
        {
            Frame = frame;
            EntityId = entityId < 0 ? 0 : entityId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            ContactPointId = contactPointId < 0 ? 0 : contactPointId;
            BodyPartId = bodyPartId < 0 ? 0 : bodyPartId;
            SourceSlotId = sourceSlotId < 0 ? 0 : sourceSlotId;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            ActivationMode = activationMode;
            MotionMode = motionMode;
            MotionSocketId = motionSocketId;
            MotionState = motionState;
            MotionClipKey = motionClipKey ?? string.Empty;
            MotionLocalFrame = motionLocalFrame < 0 ? 0 : motionLocalFrame;
            LayerMask = layerMask;
            Center = center;
            Radius = radius < Fix64.Zero ? Fix64.Zero : radius;
            Active = active;
            HasHit = hasHit;
            ContactAllowed = contactAllowed;
            AudioQueued = audioQueued;
            Phase = phase;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetShapeKind = targetShapeKind;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            SurfaceMaterial = surfaceMaterial;
            RelativeSpeedMillimetersPerSecond = ClampNonNegative(relativeSpeedMillimetersPerSecond);
            NormalSpeedMillimetersPerSecond = ClampNonNegative(normalSpeedMillimetersPerSecond);
            TangentialSpeedMillimetersPerSecond = ClampNonNegative(tangentialSpeedMillimetersPerSecond);
            Pressure = ClampNonNegative(pressure);
            Intensity = ClampNonNegative(intensity);
            DynamicsSource = dynamicsSource;
            DynamicsNormalSpeedMetersPerSecond = ClampNonNegative(dynamicsNormalSpeedMetersPerSecond);
            DynamicsTangentialSpeedMetersPerSecond = ClampNonNegative(dynamicsTangentialSpeedMetersPerSecond);
            NormalImpulseNewtonSeconds = ClampNonNegative(normalImpulseNewtonSeconds);
            AveragePressurePascals = ClampNonNegative(averagePressurePascals);
        }

        public RuntimeFrame Frame { get; }
        public int EntityId { get; }
        public int SourceEntityId { get; }
        public int ContactPointId { get; }
        public int BodyPartId { get; }
        public int SourceSlotId { get; }
        public int SourceOrder { get; }
        public CombatBodyPartContactActivationMode ActivationMode { get; }
        public CharacterBodyPartContactMotionMode MotionMode { get; }
        public int MotionSocketId { get; }
        public CharacterTestBodyPartContactMotionState MotionState { get; }
        public string MotionClipKey { get; }
        public int MotionLocalFrame { get; }
        public CombatPhysicsLayerMask LayerMask { get; }
        public FixVector3 Center { get; }
        public Fix64 Radius { get; }
        public bool Active { get; }
        public bool HasHit { get; }
        public bool ContactAllowed { get; }
        public bool AudioQueued { get; }
        public CombatPhysicalContactPhase Phase { get; }
        public CombatBodyId TargetBodyId { get; }
        public CombatColliderId TargetColliderId { get; }
        public CombatPhysicsShapeKind TargetShapeKind { get; }
        public FixVector3 HitPoint { get; }
        public FixVector3 HitNormal { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int RelativeSpeedMillimetersPerSecond { get; }
        public int NormalSpeedMillimetersPerSecond { get; }
        public int TangentialSpeedMillimetersPerSecond { get; }
        public int Pressure { get; }
        public int Intensity { get; }
        public PhysicalAudioContactDynamicsSource DynamicsSource { get; }
        public float DynamicsNormalSpeedMetersPerSecond { get; }
        public float DynamicsTangentialSpeedMetersPerSecond { get; }
        public float NormalImpulseNewtonSeconds { get; }
        public float AveragePressurePascals { get; }

        public static CharacterTestBodyPartContactDebugEntry CreateProbe(
            RuntimeFrame frame,
            int entityId,
            int sourceEntityId,
            int contactPointId,
            int bodyPartId,
            int sourceSlotId,
            int sourceOrder,
            CombatBodyPartContactActivationMode activationMode,
            CharacterBodyPartContactMotionMode motionMode,
            int motionSocketId,
            CharacterTestBodyPartContactMotionState motionState,
            string motionClipKey,
            int motionLocalFrame,
            CombatPhysicsLayerMask layerMask,
            FixVector3 center,
            Fix64 radius,
            bool active,
            int relativeSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int pressure,
            int intensity)
        {
            return new CharacterTestBodyPartContactDebugEntry(
                frame,
                entityId,
                sourceEntityId,
                contactPointId,
                bodyPartId,
                sourceSlotId,
                sourceOrder,
                activationMode,
                motionMode,
                motionSocketId,
                motionState,
                motionClipKey,
                motionLocalFrame,
                layerMask,
                center,
                radius,
                active,
                hasHit: false,
                contactAllowed: false,
                audioQueued: false,
                CombatPhysicalContactPhase.None,
                CombatBodyId.None,
                CombatColliderId.None,
                CombatPhysicsShapeKind.Aabb,
                FixVector3.Zero,
                FixVector3.Zero,
                CombatSurfaceMaterial.Default,
                relativeSpeedMillimetersPerSecond,
                normalSpeedMillimetersPerSecond,
                tangentialSpeedMillimetersPerSecond,
                pressure,
                intensity);
        }

        public CharacterTestBodyPartContactDebugEntry WithContact(
            in CombatPhysicalContactEvent contactEvent,
            bool contactAllowed)
        {
            bool hasHit = contactEvent.Phase != CombatPhysicalContactPhase.Ended;
            return new CharacterTestBodyPartContactDebugEntry(
                Frame,
                EntityId,
                SourceEntityId,
                ContactPointId,
                BodyPartId,
                SourceSlotId,
                SourceOrder,
                contactEvent.ActivationMode,
                MotionMode,
                MotionSocketId,
                MotionState,
                MotionClipKey,
                MotionLocalFrame,
                LayerMask,
                Center,
                Radius,
                Active,
                hasHit,
                hasHit && contactAllowed,
                AudioQueued,
                contactEvent.Phase,
                contactEvent.TargetBodyId,
                contactEvent.TargetColliderId,
                contactEvent.TargetShapeKind,
                contactEvent.Point,
                contactEvent.Normal,
                contactEvent.SurfaceMaterial,
                contactEvent.RelativeSpeedMillimetersPerSecond,
                contactEvent.NormalSpeedMillimetersPerSecond,
                contactEvent.TangentialSpeedMillimetersPerSecond,
                contactEvent.Pressure,
                contactEvent.Intensity);
        }

        public CharacterTestBodyPartContactDebugEntry WithAudioQueued()
        {
            return new CharacterTestBodyPartContactDebugEntry(
                Frame,
                EntityId,
                SourceEntityId,
                ContactPointId,
                BodyPartId,
                SourceSlotId,
                SourceOrder,
                ActivationMode,
                MotionMode,
                MotionSocketId,
                MotionState,
                MotionClipKey,
                MotionLocalFrame,
                LayerMask,
                Center,
                Radius,
                Active,
                HasHit,
                ContactAllowed,
                audioQueued: true,
                Phase,
                TargetBodyId,
                TargetColliderId,
                TargetShapeKind,
                HitPoint,
                HitNormal,
                SurfaceMaterial,
                RelativeSpeedMillimetersPerSecond,
                NormalSpeedMillimetersPerSecond,
                TangentialSpeedMillimetersPerSecond,
                Pressure,
                Intensity,
                DynamicsSource,
                DynamicsNormalSpeedMetersPerSecond,
                DynamicsTangentialSpeedMetersPerSecond,
                NormalImpulseNewtonSeconds,
                AveragePressurePascals);
        }

        public CharacterTestBodyPartContactDebugEntry WithDynamics(
            in PhysicalAudioContactDynamics dynamics)
        {
            return new CharacterTestBodyPartContactDebugEntry(
                Frame,
                EntityId,
                SourceEntityId,
                ContactPointId,
                BodyPartId,
                SourceSlotId,
                SourceOrder,
                ActivationMode,
                MotionMode,
                MotionSocketId,
                MotionState,
                MotionClipKey,
                MotionLocalFrame,
                LayerMask,
                Center,
                Radius,
                Active,
                HasHit,
                ContactAllowed,
                AudioQueued,
                Phase,
                TargetBodyId,
                TargetColliderId,
                TargetShapeKind,
                HitPoint,
                HitNormal,
                SurfaceMaterial,
                RelativeSpeedMillimetersPerSecond,
                NormalSpeedMillimetersPerSecond,
                TangentialSpeedMillimetersPerSecond,
                Pressure,
                Intensity,
                dynamics.Source,
                dynamics.NormalSpeedMetersPerSecond,
                dynamics.TangentialSpeedMetersPerSecond,
                dynamics.NormalImpulseNewtonSeconds,
                dynamics.AveragePressurePascals);
        }

        private static int ClampNonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }

        private static float ClampNonNegative(float value)
        {
            return value < 0f || float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
