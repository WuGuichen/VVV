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
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        private readonly struct ResolvedContactCooldownKey : IEquatable<ResolvedContactCooldownKey>
        {
            public ResolvedContactCooldownKey(
                int contactPairKey,
                int sourceSlotId,
                int contactPointId,
                int familyId,
                int basisId)
            {
                ContactPairKey = contactPairKey;
                SourceSlotId = sourceSlotId;
                ContactPointId = contactPointId;
                FamilyId = familyId;
                BasisId = basisId;
            }

            public int ContactPairKey { get; }
            public int SourceSlotId { get; }
            public int ContactPointId { get; }
            public int FamilyId { get; }
            public int BasisId { get; }

            public bool Equals(ResolvedContactCooldownKey other)
            {
                return ContactPairKey == other.ContactPairKey
                    && SourceSlotId == other.SourceSlotId
                    && ContactPointId == other.ContactPointId
                    && FamilyId == other.FamilyId
                    && BasisId == other.BasisId;
            }

            public override bool Equals(object obj)
            {
                return obj is ResolvedContactCooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ContactPairKey;
                    hash = (hash * 397) ^ SourceSlotId;
                    hash = (hash * 397) ^ ContactPointId;
                    hash = (hash * 397) ^ FamilyId;
                    hash = (hash * 397) ^ BasisId;
                    return hash;
                }
            }
        }

        private readonly struct BodyPartContactHit
        {
            public BodyPartContactHit(
                CombatBodyId bodyId,
                CombatColliderId colliderId,
                CombatPhysicsShapeKind shapeKind,
                FixVector3 point,
                FixVector3 normal,
                CombatSurfaceMaterial material,
                bool isStandable,
                int contactPointId,
                int bodyPartId,
                CombatBodyPartContactActivationMode activationMode)
            {
                HasHit = true;
                BodyId = bodyId;
                ColliderId = colliderId;
                ShapeKind = shapeKind;
                Point = point;
                Normal = normal;
                Material = material;
                IsStandable = isStandable;
                ContactPointId = contactPointId < 0 ? 0 : contactPointId;
                BodyPartId = bodyPartId < 0 ? 0 : bodyPartId;
                ActivationMode = activationMode;
            }

            public bool HasHit { get; }
            public CombatBodyId BodyId { get; }
            public CombatColliderId ColliderId { get; }
            public CombatPhysicsShapeKind ShapeKind { get; }
            public FixVector3 Point { get; }
            public FixVector3 Normal { get; }
            public CombatSurfaceMaterial Material { get; }
            public bool IsStandable { get; }
            public int ContactPointId { get; }
            public int BodyPartId { get; }
            public CombatBodyPartContactActivationMode ActivationMode { get; }
        }

        private readonly struct CharacterTestBodyPartContactMotionResolution
        {
            private CharacterTestBodyPartContactMotionResolution(
                FixVector3 localOffset,
                CharacterBodyPartContactMotionMode motionMode,
                int motionSocketId,
                CharacterTestBodyPartContactMotionState state,
                string clipKey,
                int localFrame)
            {
                LocalOffset = localOffset;
                MotionMode = motionMode;
                MotionSocketId = motionSocketId;
                State = state;
                ClipKey = clipKey ?? string.Empty;
                LocalFrame = localFrame < 0 ? 0 : localFrame;
            }

            public FixVector3 LocalOffset { get; }
            public CharacterBodyPartContactMotionMode MotionMode { get; }
            public int MotionSocketId { get; }
            public CharacterTestBodyPartContactMotionState State { get; }
            public string ClipKey { get; }
            public int LocalFrame { get; }

            public static CharacterTestBodyPartContactMotionResolution Create(
                FixVector3 localOffset,
                CharacterBodyPartContactMotionMode motionMode,
                int motionSocketId,
                CharacterTestBodyPartContactMotionState state,
                string clipKey,
                int localFrame)
            {
                return new CharacterTestBodyPartContactMotionResolution(
                    localOffset,
                    motionMode,
                    motionSocketId,
                    state,
                    clipKey,
                    localFrame);
            }
        }

        private enum CharacterTestLocomotionAudioClipKind
        {
            None = 0,
            Idle = 1,
            WalkForward = 2,
            WalkBack = 3,
            WalkLeft = 4,
            WalkRight = 5,
            RunForward = 6,
            RunBack = 7,
            RunLeft = 8,
            RunRight = 9,
            Count = 10,
        }

        private sealed class CharacterBodyPartContactRegistration
        {
            public CharacterBodyPartContactRegistration(CharacterBodyPartContactRuntimeBinding[] bindings,
                CharacterBodyPartConfig[] bodyParts, CharacterImportedGeometryBinding geometry)
            {
                Bindings = bindings ?? Array.Empty<CharacterBodyPartContactRuntimeBinding>();
                Kinematics = new CharacterBodyPartContactKinematicState[Bindings.Length];
                // Compile presentation semantics at registration, never from contact/render Tick.
                PartKinds = new CharacterBodyPartKind[Bindings.Length];
                if (bodyParts == null) return;
                for (int i = 0; i < Bindings.Length; i++)
                    PartKinds[i] = ResolveRegisteredContactPartKind(Bindings[i].Config, bodyParts, geometry);
            }

            public CharacterBodyPartContactRuntimeBinding[] Bindings { get; }

            public CharacterBodyPartKind[] PartKinds { get; }

            public CharacterBodyPartContactKinematicState[] Kinematics { get; }

            public void ResetKinematics()
            {
                Array.Clear(Kinematics, 0, Kinematics.Length);
            }
        }

        private struct CharacterBodyPartContactKinematicState
        {
            private const long TeleportAxisThresholdRaw = 2L * Fix64.Scale;

            public FixVector3 PreviousWorldPosition;
            public long PreviousFrame;
            public CharacterBodyPartContactKinematicMotionSource MotionSource;
            public float VelocityX;
            public float VelocityY;
            public float VelocityZ;
            public bool HasBaseline;
            public bool HasContinuousVelocity;

            public void Capture(
                FixVector3 worldPosition,
                RuntimeFrame frame,
                int tickRate,
                CharacterTestBodyPartContactMotionResolution motion)
            {
                FixVector3 delta = worldPosition - PreviousWorldPosition;
                CharacterBodyPartContactKinematicMotionSource motionSource =
                    ResolveMotionSource(motion.State);
                bool continuous = HasBaseline
                    && frame.Value == PreviousFrame + 1
                    && tickRate > 0
                    && MotionSource == motionSource
                    && !IsTeleportDelta(delta);
                if (continuous)
                {
                    float velocityScale = tickRate / (float)Fix64.Scale;
                    VelocityX = delta.X.RawValue * velocityScale;
                    VelocityY = delta.Y.RawValue * velocityScale;
                    VelocityZ = delta.Z.RawValue * velocityScale;
                }
                else
                {
                    VelocityX = 0f;
                    VelocityY = 0f;
                    VelocityZ = 0f;
                }

                PreviousWorldPosition = worldPosition;
                PreviousFrame = frame.Value;
                MotionSource = motionSource;
                HasBaseline = true;
                HasContinuousVelocity = continuous;
            }

            private static CharacterBodyPartContactKinematicMotionSource ResolveMotionSource(
                CharacterTestBodyPartContactMotionState state)
            {
                switch (state)
                {
                    case CharacterTestBodyPartContactMotionState.PresentationPose:
                        return CharacterBodyPartContactKinematicMotionSource.PresentationPose;
                    case CharacterTestBodyPartContactMotionState.BakedSocket:
                        return CharacterBodyPartContactKinematicMotionSource.BakedSocket;
                    default:
                        return CharacterBodyPartContactKinematicMotionSource.StaticFallback;
                }
            }

            private static bool IsTeleportDelta(FixVector3 delta)
            {
                return AbsRaw(delta.X.RawValue) > TeleportAxisThresholdRaw
                    || AbsRaw(delta.Y.RawValue) > TeleportAxisThresholdRaw
                    || AbsRaw(delta.Z.RawValue) > TeleportAxisThresholdRaw;
            }

            private static long AbsRaw(long value)
            {
                if (value >= 0)
                    return value;
                return value == long.MinValue ? long.MaxValue : -value;
            }
        }

        private enum CharacterBodyPartContactKinematicMotionSource : byte
        {
            StaticFallback = 0,
            BakedSocket = 1,
            PresentationPose = 2,
        }

        private readonly struct CharacterBodyPartContactRuntimeBinding
        {
            public CharacterBodyPartContactRuntimeBinding(
                CharacterBodyPartContactPointConfig config,
                BodyPartContactDerivedDefinition derived,
                CharacterBodyPartContactClipBinding[] locomotionClips,
                CharacterBodyPartContactActionBinding[] actionClips,
                bool sourceAvailable,
                bool hasAuthorityPoseContactAttachment,
                CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig,
                CharacterSocketBakeCatalog authorityPoseCatalog)
            {
                Config = config;
                Derived = derived;
                MotionSocketId = config?.MotionSocketId ?? 0;
                HasMotionSocket = config != null
                    && config.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && MotionSocketId > 0;
                LocomotionClips = locomotionClips ?? Array.Empty<CharacterBodyPartContactClipBinding>();
                ActionClips = actionClips ?? Array.Empty<CharacterBodyPartContactActionBinding>();
                SourceAvailable = sourceAvailable;
                HasAuthorityPoseContactAttachment = hasAuthorityPoseContactAttachment;
                AuthorityPoseRig = authorityPoseRig;
                AuthorityPoseCatalog = authorityPoseCatalog;
            }

            public CharacterBodyPartContactPointConfig Config { get; }
            public BodyPartContactDerivedDefinition Derived { get; }
            public int MotionSocketId { get; }
            public bool HasMotionSocket { get; }
            public CharacterBodyPartContactClipBinding[] LocomotionClips { get; }
            public CharacterBodyPartContactActionBinding[] ActionClips { get; }
            public bool SourceAvailable { get; }
            public bool HasAuthorityPoseContactAttachment { get; }
            public CharacterAuthorityPoseRigRuntimeBinding AuthorityPoseRig { get; }
            public CharacterSocketBakeCatalog AuthorityPoseCatalog { get; }

            /// <remarks>NoAlloc. Numeric-only selection; clip strings are carried as immutable diagnostics.</remarks>
            public bool TryResolveClip(
                int actionBindingId,
                int actionLocalFrame,
                CharacterTestLocomotionAudioClipKind locomotionKind,
                int locomotionLocalFrame,
                out CharacterBodyPartContactClipBinding clip,
                out int localFrame)
            {
                if (actionBindingId != 0)
                {
                    for (int i = 0; i < ActionClips.Length; i++)
                    {
                        CharacterBodyPartContactActionBinding action = ActionClips[i];
                        if (action.ActionBindingId == actionBindingId
                            && action.TryResolve(actionLocalFrame, out clip))
                        {
                            localFrame = actionLocalFrame < 0 ? 0 : actionLocalFrame;
                            return true;
                        }
                    }
                }

                int locomotionIndex = (int)locomotionKind;
                if ((uint)locomotionIndex < (uint)LocomotionClips.Length
                    && LocomotionClips[locomotionIndex].IsDefined)
                {
                    clip = LocomotionClips[locomotionIndex];
                    localFrame = locomotionLocalFrame < 0 ? 0 : locomotionLocalFrame;
                    return true;
                }

                clip = default;
                localFrame = 0;
                return false;
            }
        }

        private readonly struct CharacterBodyPartContactActionBinding
        {
            public CharacterBodyPartContactActionBinding(
                int actionBindingId,
                CharacterBodyPartContactActionSegment[] segments)
            {
                ActionBindingId = actionBindingId;
                Segments = segments ?? Array.Empty<CharacterBodyPartContactActionSegment>();
            }

            public int ActionBindingId { get; }
            public CharacterBodyPartContactActionSegment[] Segments { get; }

            /// <remarks>NoAlloc.</remarks>
            public bool TryResolve(int localFrame, out CharacterBodyPartContactClipBinding clip)
            {
                for (int i = 0; i < Segments.Length; i++)
                {
                    CharacterBodyPartContactActionSegment segment = Segments[i];
                    if (localFrame >= segment.StartFrame && localFrame <= segment.EndFrame)
                    {
                        clip = segment.Clip;
                        return clip.IsDefined;
                    }
                }
                clip = default;
                return false;
            }
        }

        private readonly struct CharacterBodyPartContactActionSegment
        {
            public CharacterBodyPartContactActionSegment(
                int startFrame,
                int endFrame,
                CharacterBodyPartContactClipBinding clip)
            {
                StartFrame = startFrame < 0 ? 0 : startFrame;
                EndFrame = endFrame < StartFrame ? StartFrame : endFrame;
                Clip = clip;
            }

            public int StartFrame { get; }
            public int EndFrame { get; }
            public CharacterBodyPartContactClipBinding Clip { get; }
        }

        private readonly struct CharacterBodyPartContactClipBinding
        {
            public CharacterBodyPartContactClipBinding(string clipKey, CharacterTestBakedSocketTrack track)
            {
                ClipKey = clipKey ?? string.Empty;
                Track = track;
                IsDefined = !string.IsNullOrEmpty(clipKey);
            }

            public string ClipKey { get; }
            public CharacterTestBakedSocketTrack Track { get; }
            public bool IsDefined { get; }
        }

        private struct CharacterBodyPartContactAudioState
        {
            public int LastHorizontalVelocityXMillimetersPerSecond;
            public int LastHorizontalVelocityZMillimetersPerSecond;
            public int LastVerticalVelocityMillimetersPerSecond;
            public long LastFrame;
            public bool HasMotionState;
            public bool WasGrounded;

            public static CharacterBodyPartContactAudioState CreateInitial()
            {
                return new CharacterBodyPartContactAudioState
                {
                    LastHorizontalVelocityXMillimetersPerSecond = 0,
                    LastHorizontalVelocityZMillimetersPerSecond = 0,
                    LastVerticalVelocityMillimetersPerSecond = 0,
                    LastFrame = 0,
                    HasMotionState = false,
                    WasGrounded = false
                };
            }

            public void CaptureMotion(
                RuntimeFrame frame,
                bool grounded,
                int horizontalVelocityXMillimetersPerSecond,
                int horizontalVelocityZMillimetersPerSecond,
                int verticalVelocityMillimetersPerSecond)
            {
                LastFrame = frame.Value;
                WasGrounded = grounded;
                LastHorizontalVelocityXMillimetersPerSecond = horizontalVelocityXMillimetersPerSecond;
                LastHorizontalVelocityZMillimetersPerSecond = horizontalVelocityZMillimetersPerSecond;
                LastVerticalVelocityMillimetersPerSecond = verticalVelocityMillimetersPerSecond;
                HasMotionState = true;
            }

            public void MarkNoContact()
            {
            }

        }

        private readonly struct BodyPartContactDerivedKey : IEquatable<BodyPartContactDerivedKey>
        {
            private BodyPartContactDerivedKey(
                CharacterBodyPartContactPointConfig config,
                int contactPointId,
                int bodyPartId,
                int radiusMillimeters,
                int localOffsetXMillimeters,
                int localOffsetYMillimeters,
                int localOffsetZMillimeters,
                int sourceOrder,
                CharacterBodyPartContactActivationMode activationMode,
                string usage,
                string authority,
                string layerMaskProfileId,
                int obstacleMask,
                int staticObstacleMask,
                int dynamicCharacterMask,
                int dynamicMovableObjectMask)
            {
                Config = config;
                ContactPointId = contactPointId;
                BodyPartId = bodyPartId;
                RadiusMillimeters = radiusMillimeters;
                LocalOffsetXMillimeters = localOffsetXMillimeters;
                LocalOffsetYMillimeters = localOffsetYMillimeters;
                LocalOffsetZMillimeters = localOffsetZMillimeters;
                SourceOrder = sourceOrder;
                ActivationMode = activationMode;
                Usage = usage ?? string.Empty;
                Authority = authority ?? string.Empty;
                LayerMaskProfileId = layerMaskProfileId ?? string.Empty;
                ObstacleMask = obstacleMask;
                StaticObstacleMask = staticObstacleMask;
                DynamicCharacterMask = dynamicCharacterMask;
                DynamicMovableObjectMask = dynamicMovableObjectMask;
            }

            public CharacterBodyPartContactPointConfig Config { get; }
            public int ContactPointId { get; }
            public int BodyPartId { get; }
            public int RadiusMillimeters { get; }
            public int LocalOffsetXMillimeters { get; }
            public int LocalOffsetYMillimeters { get; }
            public int LocalOffsetZMillimeters { get; }
            public int SourceOrder { get; }
            public CharacterBodyPartContactActivationMode ActivationMode { get; }
            public string Usage { get; }
            public string Authority { get; }
            public string LayerMaskProfileId { get; }
            public int ObstacleMask { get; }
            public int StaticObstacleMask { get; }
            public int DynamicCharacterMask { get; }
            public int DynamicMovableObjectMask { get; }

            public static BodyPartContactDerivedKey From(
                CharacterTestCharacterPhysicsRuntime physicsRuntime,
                CharacterBodyPartContactPointConfig config)
            {
                return new BodyPartContactDerivedKey(
                    config,
                    config == null ? 0 : config.ContactPointId,
                    config == null ? 0 : config.BodyPartId,
                    config == null ? 0 : config.RadiusMillimeters,
                    config == null ? 0 : config.LocalOffsetXMillimeters,
                    config == null ? 0 : config.LocalOffsetYMillimeters,
                    config == null ? 0 : config.LocalOffsetZMillimeters,
                    config == null ? 0 : config.SourceOrder,
                    config == null ? default : config.ActivationMode,
                    config == null ? string.Empty : config.Usage,
                    config == null ? string.Empty : config.Authority,
                    config == null ? string.Empty : config.LayerMaskProfileId,
                    physicsRuntime == null ? 0 : physicsRuntime.ObstacleLayerMask.Value,
                    physicsRuntime == null ? 0 : physicsRuntime.StaticObstacleLayerMask.Value,
                    physicsRuntime == null ? 0 : physicsRuntime.DynamicCharacterObstacleLayerMask.Value,
                    physicsRuntime == null ? 0 : physicsRuntime.DynamicMovableObjectObstacleLayerMask.Value);
            }

            public bool Equals(BodyPartContactDerivedKey other)
            {
                return ReferenceEquals(Config, other.Config)
                    && ContactPointId == other.ContactPointId
                    && BodyPartId == other.BodyPartId
                    && RadiusMillimeters == other.RadiusMillimeters
                    && LocalOffsetXMillimeters == other.LocalOffsetXMillimeters
                    && LocalOffsetYMillimeters == other.LocalOffsetYMillimeters
                    && LocalOffsetZMillimeters == other.LocalOffsetZMillimeters
                    && SourceOrder == other.SourceOrder
                    && ActivationMode == other.ActivationMode
                    && string.Equals(Usage, other.Usage, StringComparison.Ordinal)
                    && string.Equals(Authority, other.Authority, StringComparison.Ordinal)
                    && string.Equals(LayerMaskProfileId, other.LayerMaskProfileId, StringComparison.Ordinal)
                    && ObstacleMask == other.ObstacleMask
                    && StaticObstacleMask == other.StaticObstacleMask
                    && DynamicCharacterMask == other.DynamicCharacterMask
                    && DynamicMovableObjectMask == other.DynamicMovableObjectMask;
            }

            public override bool Equals(object obj)
            {
                return obj is BodyPartContactDerivedKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Config == null ? 0 : RuntimeHelpers.GetHashCode(Config);
                    hash = (hash * 397) ^ ContactPointId;
                    hash = (hash * 397) ^ BodyPartId;
                    hash = (hash * 397) ^ RadiusMillimeters;
                    hash = (hash * 397) ^ LocalOffsetXMillimeters;
                    hash = (hash * 397) ^ LocalOffsetYMillimeters;
                    hash = (hash * 397) ^ LocalOffsetZMillimeters;
                    hash = (hash * 397) ^ SourceOrder;
                    hash = (hash * 397) ^ (int)ActivationMode;
                    hash = (hash * 397) ^ (Usage == null ? 0 : StringComparer.Ordinal.GetHashCode(Usage));
                    hash = (hash * 397) ^ (Authority == null ? 0 : StringComparer.Ordinal.GetHashCode(Authority));
                    hash = (hash * 397) ^ (LayerMaskProfileId == null ? 0 : StringComparer.Ordinal.GetHashCode(LayerMaskProfileId));
                    hash = (hash * 397) ^ ObstacleMask;
                    hash = (hash * 397) ^ StaticObstacleMask;
                    hash = (hash * 397) ^ DynamicCharacterMask;
                    hash = (hash * 397) ^ DynamicMovableObjectMask;
                    return hash;
                }
            }
        }

        private readonly struct BodyPartContactDerivedDefinition
        {
            private BodyPartContactDerivedDefinition(
                bool isValid,
                bool presentationAudioEligible,
                int contactPointId,
                int bodyPartId,
                int sourceOrder,
                int sourceSlotId,
                FixVector3 staticOffset,
                Fix64 radius,
                CombatPhysicsLayerMask layerMask,
                CombatBodyPartContactActivationMode activationMode)
            {
                IsValid = isValid;
                PresentationAudioEligible = presentationAudioEligible;
                ContactPointId = contactPointId;
                BodyPartId = bodyPartId;
                SourceOrder = sourceOrder;
                SourceSlotId = sourceSlotId;
                StaticOffset = staticOffset;
                Radius = radius;
                LayerMask = layerMask;
                ActivationMode = activationMode;
            }

            public bool IsValid { get; }
            public bool PresentationAudioEligible { get; }
            public int ContactPointId { get; }
            public int BodyPartId { get; }
            public int SourceOrder { get; }
            public int SourceSlotId { get; }
            public FixVector3 StaticOffset { get; }
            public Fix64 Radius { get; }
            public CombatPhysicsLayerMask LayerMask { get; }
            public CombatBodyPartContactActivationMode ActivationMode { get; }

            public static BodyPartContactDerivedDefinition Create(
                CharacterBodyPartContactPointConfig config,
                CombatPhysicsLayerMask layerMask)
            {
                if (config == null || config.ContactPointId <= 0 || config.RadiusMillimeters <= 0)
                    return default;

                return new BodyPartContactDerivedDefinition(
                    isValid: true,
                    presentationAudioEligible: IsPresentationAudioContactPoint(config),
                    contactPointId: config.ContactPointId,
                    bodyPartId: config.BodyPartId,
                    sourceOrder: config.SourceOrder,
                    sourceSlotId: config.ContactPointId,
                    staticOffset: CreateContactPointStaticOffset(config),
                    radius: CharacterTestDeterministicUnits.MillimetersToFix64Meters(config.RadiusMillimeters),
                    layerMask: layerMask,
                    activationMode: MapActivationMode(config.ActivationMode));
            }
        }

        private struct PhysicalAudioFrameState
        {
            public PhysicalAudioFrameState(RuntimeFrame frame)
            {
                Frame = frame;
                CharacterCount = 0;
                MovingGroundedCharacterCount = 0;
                BodyPartContactCount = 0;
                BodyPartContactPlayedCount = 0;
                BodyPartProbeMissCount = 0;
                BodyPartFilteredContactCount = 0;
                LeftFootContactCount = 0;
                RightFootContactCount = 0;
                LandingPlayedCount = 0;
                ObjectContactCount = 0;
                ObjectCollisionPlayedCount = 0;
                FootProbeMissCount = 0;
                CooldownSkipCount = 0;
                CooldownCapacitySkipCount = 0;
                ContactPointEstimatedCount = 0;
                SolverVelocityDeltaCount = 0;
                ContactDynamicsBaselineSuppressCount = 0;
                DynamicTargetFallbackCount = 0;
                PresentationMotionResolvedCount = 0;
                BakedMotionResolvedCount = 0;
                StaticMotionCount = 0;
                MotionFallbackCount = 0;
                MissingMotionSocketCount = 0;
                MissingMotionClipCount = 0;
                MissingMotionFrameCount = 0;
                MissingMotionSourceCount = 0;
                MissingPhysicsRuntimeSkipCount = 0;
                MissingContactPointConfigSkipCount = 0;
                RegistrationCapacitySkipCount = 0;
                SkippedCount = 0;
                LastCueKey = string.Empty;
                LastEmitterId = 0;
            }

            public RuntimeFrame Frame { get; }
            public int CharacterCount;
            public int MovingGroundedCharacterCount;
            public int BodyPartContactCount;
            public int BodyPartContactPlayedCount;
            public int BodyPartProbeMissCount;
            public int BodyPartFilteredContactCount;
            public int LeftFootContactCount;
            public int RightFootContactCount;
            public int LandingPlayedCount;
            public int ObjectContactCount;
            public int ObjectCollisionPlayedCount;
            public int FootProbeMissCount;
            public int CooldownSkipCount;
            public int CooldownCapacitySkipCount;
            public int ContactPointEstimatedCount;
            public int SolverVelocityDeltaCount;
            public int ContactDynamicsBaselineSuppressCount;
            public int DynamicTargetFallbackCount;
            public int PresentationMotionResolvedCount;
            public int BakedMotionResolvedCount;
            public int StaticMotionCount;
            public int MotionFallbackCount;
            public int MissingMotionSocketCount;
            public int MissingMotionClipCount;
            public int MissingMotionFrameCount;
            public int MissingMotionSourceCount;
            public int MissingPhysicsRuntimeSkipCount;
            public int MissingContactPointConfigSkipCount;
            public int RegistrationCapacitySkipCount;
            public int SkippedCount;
            public string LastCueKey;
            public int LastEmitterId;

            public CharacterTestPhysicalAudioSummary ToSummary()
            {
                return new CharacterTestPhysicalAudioSummary(
                    Frame,
                    CharacterCount,
                    MovingGroundedCharacterCount,
                    BodyPartContactCount,
                    BodyPartContactPlayedCount,
                    BodyPartProbeMissCount,
                    BodyPartFilteredContactCount,
                    LeftFootContactCount,
                    RightFootContactCount,
                    LandingPlayedCount,
                    ObjectContactCount,
                    ObjectCollisionPlayedCount,
                    FootProbeMissCount,
                    CooldownSkipCount,
                    PresentationMotionResolvedCount,
                    BakedMotionResolvedCount,
                    StaticMotionCount,
                    MotionFallbackCount,
                    MissingMotionSocketCount,
                    MissingMotionClipCount,
                    MissingMotionFrameCount,
                    MissingMotionSourceCount,
                    MissingPhysicsRuntimeSkipCount,
                    MissingContactPointConfigSkipCount,
                    RegistrationCapacitySkipCount,
                    SkippedCount,
                    LastCueKey,
                    LastEmitterId,
                    CooldownCapacitySkipCount,
                    ContactPointEstimatedCount,
                    SolverVelocityDeltaCount,
                    ContactDynamicsBaselineSuppressCount,
                    DynamicTargetFallbackCount);
            }
        }
    }
}
