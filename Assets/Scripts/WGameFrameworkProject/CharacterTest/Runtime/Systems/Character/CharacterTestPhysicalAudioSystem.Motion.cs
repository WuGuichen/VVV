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
        private void FillBodyPartContactPointSamples(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame,
            int tickRate,
            int horizontalSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int contactIntensity,
            CharacterBodyPartContactRegistration registration,
            ref PhysicalAudioFrameState state)
        {
            _bodyPartContactSamples.Clear();
            CharacterBodyPartContactRuntimeBinding[] bindings = registration?.Bindings;
            if (bindings == null || bindings.Length == 0)
                return;

            CharacterTestCharacterTransformComponent transform = entity.Transform;
            FixVector3 bodyPosition = CreateFixPoint(
                transform.XMillimeters,
                transform.YMillimeters,
                transform.ZMillimeters);
            ResolveDeterministicAxes(
                transform,
                out FixVector3 forward,
                out FixVector3 right,
                out FixVector3 up);
            CombatEntityId sourceEntityId = physicsRuntime.GetEntityId(entity);
            CombatBodyId sourceBodyId = physicsRuntime.GetBodyId(entity);
            var sourceColliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId);
            for (int i = 0; i < bindings.Length && _bodyPartContactSamples.Count < PhysicalAudioSensorCapacity; i++)
            {
                CharacterBodyPartContactRuntimeBinding binding = bindings[i];
                CharacterBodyPartContactPointConfig config = binding.Config;
                BodyPartContactDerivedDefinition derived = binding.Derived;
                if (!derived.IsValid)
                    continue;
                if (!derived.PresentationAudioEligible)
                    continue;

                CharacterTestBodyPartContactMotionResolution motion =
                    ResolveBodyPartContactMotion(entity, binding, frame);
                CountBodyPartContactMotionResolution(motion, ref state);
                FixVector3 worldPosition = TransformContactPointLocalOffset(
                    bodyPosition,
                    forward,
                    right,
                    up,
                    motion.LocalOffset);
                registration.Kinematics[i].Capture(
                    worldPosition,
                    frame,
                    tickRate,
                    motion);
                CombatBodyPartContactPointSample sample = CreateBodyPartContactPointSample(
                    derived,
                    motion.LocalOffset,
                    sourceEntityId,
                    sourceBodyId,
                    sourceColliderId,
                    bodyPosition,
                    forward,
                    right,
                    up,
                    entity.SurfaceMaterial,
                    horizontalSpeedMillimetersPerSecond,
                    normalSpeedMillimetersPerSecond,
                    ResolveContactPressure(
                        normalSpeedMillimetersPerSecond,
                        horizontalSpeedMillimetersPerSecond,
                        entity.Physics.EffectiveMassGrams),
                    contactIntensity);
                _bodyPartContactSamples.Add(sample);
                AppendBodyPartContactDebugEntry(entity, sample, motion);
            }
        }

        private BodyPartContactDerivedDefinition ResolveBodyPartContactDerivedDefinition(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterBodyPartContactPointConfig config)
        {
            BodyPartContactDerivedKey key = BodyPartContactDerivedKey.From(physicsRuntime, config);
            if (_bodyPartContactDerivedDefinitions.TryGetValue(key, out BodyPartContactDerivedDefinition definition))
                return definition;
            return default;
        }

        /// <remarks>
        /// LowFreqAlloc: compiles package/config strings and baked-source lookups into fixed runtime arrays before
        /// Contact Tick. The returned bindings are immutable until the explicit registration refresh seam runs.
        /// </remarks>
        private CharacterBodyPartContactRuntimeBinding[] BuildContactRuntimeBindings(
            CharacterImportedPackage package,
            CharacterBodyPartContactPointConfig[] configs,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            string generatedPackageRoot)
        {
            if (configs == null || configs.Length == 0)
                return Array.Empty<CharacterBodyPartContactRuntimeBinding>();

            ICharacterTestBakedWeaponTraceSource source =
                _bakedMotionSource ?? CharacterTestDefaultBakedWeaponTraceSource.Instance;
            CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig =
                package?.Geometry?.AuthorityPoseRig;
            CharacterSocketBakeCatalog authorityPoseCatalog = null;
            bool hasAuthorityPoseContactAttachment = false;
            for (int i = 0; i < configs.Length; i++)
            {
                if (CharacterTestAuthorityPoseConsumerMath.HasAuthoredAttachment(
                    authorityPoseRig,
                    CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
                    configs[i]?.ContactPointId ?? 0))
                {
                    hasAuthorityPoseContactAttachment = true;
                    break;
                }
            }
            if (hasAuthorityPoseContactAttachment
                && source is CharacterTestDefaultBakedWeaponTraceSource packageSource)
            {
                packageSource.TryGetPackageSocketCatalog(
                    generatedPackageRoot,
                    out authorityPoseCatalog);
            }
            var bindings = new CharacterBodyPartContactRuntimeBinding[configs.Length];
            for (int i = 0; i < configs.Length; i++)
            {
                CharacterBodyPartContactPointConfig config = configs[i];
                BodyPartContactDerivedDefinition derived = config == null
                    ? default
                    : BodyPartContactDerivedDefinition.Create(
                        config,
                        ResolveContactPointLayerMask(physicsRuntime, config.LayerMaskProfileId));
                bindings[i] = new CharacterBodyPartContactRuntimeBinding(
                    config,
                    derived,
                    BuildLocomotionClipBindings(package, config, source),
                    BuildActionClipBindings(package, config, source),
                    source != null,
                    CharacterTestAuthorityPoseConsumerMath.HasAuthoredAttachment(
                        authorityPoseRig,
                        CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
                        config?.ContactPointId ?? 0),
                    authorityPoseRig,
                    authorityPoseCatalog);
            }
            return bindings;
        }

        /// <remarks>LowFreqAlloc.</remarks>
        private CharacterBodyPartContactClipBinding[] BuildLocomotionClipBindings(
            CharacterImportedPackage package,
            CharacterBodyPartContactPointConfig config,
            ICharacterTestBakedWeaponTraceSource source)
        {
            int count = (int)CharacterTestLocomotionAudioClipKind.Count;
            var bindings = new CharacterBodyPartContactClipBinding[count];
            if (config == null
                || config.MotionMode != CharacterBodyPartContactMotionMode.BakedSocket
                || (config.MotionSocketId <= 0))
            {
                return bindings;
            }

            for (int i = 1; i < count; i++)
            {
                CharacterTestLocomotionAudioClipKind kind = (CharacterTestLocomotionAudioClipKind)i;
                if (TryResolveLocomotionAnimationClipKey(
                        package,
                        ResolveLocomotionClipId(kind),
                        out string clipKey))
                {
                    bindings[i] = ResolveClipBinding(source, clipKey, config.MotionSocketId, package?.RootPath);
                }
            }
            return bindings;
        }

        /// <remarks>LowFreqAlloc.</remarks>
        private CharacterBodyPartContactActionBinding[] BuildActionClipBindings(
            CharacterImportedPackage package,
            CharacterBodyPartContactPointConfig config,
            ICharacterTestBakedWeaponTraceSource source)
        {
            CharacterActionConfig[] actions = _actionRuntime?.Registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            if (config == null
                || config.MotionMode != CharacterBodyPartContactMotionMode.BakedSocket
                || (config.MotionSocketId <= 0)
                || actions.Length == 0)
            {
                return Array.Empty<CharacterBodyPartContactActionBinding>();
            }

            var result = new List<CharacterBodyPartContactActionBinding>(actions.Length);
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                CharacterActionConfig action = actions[actionIndex];
                AnimationTrackEvent[] events = action?.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEvent>();
                if (events.Length == 0)
                    continue;

                var segments = new List<CharacterBodyPartContactActionSegment>(events.Length);
                for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                {
                    AnimationTrackEvent evt = events[eventIndex];
                    string clipKey = evt.EffectiveAnimationKey;
                    if (string.IsNullOrWhiteSpace(clipKey))
                        continue;
                    segments.Add(new CharacterBodyPartContactActionSegment(
                        evt.Frame,
                        evt.EndFrame,
                        ResolveClipBinding(source, clipKey, config.MotionSocketId, package?.RootPath)));
                }
                if (segments.Count == 0)
                    continue;

                int actionBindingId = CharacterTestCharacterActionComponent.ComputeStableAudioBindingId(action.ActionId);
                bool duplicate = false;
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i].ActionBindingId == actionBindingId)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    result.Add(new CharacterBodyPartContactActionBinding(
                        actionBindingId,
                        segments.ToArray()));
                }
            }
            return result.ToArray();
        }

        /// <remarks>LowFreqAlloc.</remarks>
        private static CharacterBodyPartContactClipBinding ResolveClipBinding(
            ICharacterTestBakedWeaponTraceSource source,
            string clipKey,
            int socketId,
            string packageRoot)
        {
            CharacterTestBakedSocketTrack track = null;
            if (source is ICharacterTestPackageContactMotionSource packages)
            {
                packages.TryResolveSocketTrack(
                    packageRoot,
                    clipKey,
                    socketId,
                    out track);
            }
            else
                source?.TryResolveSocketTrack(clipKey, socketId, out track);
            return new CharacterBodyPartContactClipBinding(clipKey, track);
        }

        private static CombatBodyPartContactPointSample CreateBodyPartContactPointSample(
            BodyPartContactDerivedDefinition derived,
            FixVector3 localOffset,
            CombatEntityId sourceEntityId,
            CombatBodyId sourceBodyId,
            CombatColliderId sourceColliderId,
            FixVector3 bodyPosition,
            FixVector3 forward,
            FixVector3 right,
            FixVector3 up,
            CombatSurfaceMaterial fallbackMaterial,
            int horizontalSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int contactPressure,
            int contactIntensity)
        {
            int relativeSpeed = EstimateContactRelativeSpeed(
                horizontalSpeedMillimetersPerSecond,
                normalSpeedMillimetersPerSecond);
            var definition = new CombatBodyPartContactPointDefinition(
                derived.ContactPointId,
                derived.BodyPartId,
                localOffset,
                derived.Radius,
                derived.LayerMask,
                derived.ActivationMode,
                derived.SourceOrder,
                derived.SourceSlotId);
            return new CombatBodyPartContactPointSample(
                definition,
                sourceEntityId,
                sourceBodyId,
                sourceColliderId,
                bodyPosition,
                forward,
                right,
                up,
                fallbackMaterial,
                relativeSpeed,
                normalSpeedMillimetersPerSecond,
                tangentialSpeedMillimetersPerSecond: horizontalSpeedMillimetersPerSecond,
                pressure: contactPressure,
                intensity: contactIntensity,
                BodyPartContactTraceId,
                actionWindowActive: false,
                explicitActive: false,
                motionStateActive: true);
        }

        private CharacterTestBodyPartContactMotionResolution ResolveBodyPartContactMotion(
            CharacterTestCharacterEntity entity,
            CharacterBodyPartContactRuntimeBinding binding,
            RuntimeFrame frame)
        {
            CharacterBodyPartContactPointConfig config = binding.Config;
            BodyPartContactDerivedDefinition derived = binding.Derived;
            FixVector3 authoredOffset = derived.StaticOffset;
            if (config.MotionMode != CharacterBodyPartContactMotionMode.BakedSocket)
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    authoredOffset,
                    config.MotionMode,
                    config.MotionSocketId,
                    CharacterTestBodyPartContactMotionState.Static,
                    string.Empty,
                    0);
            }

            int motionSocketId = binding.MotionSocketId;
            if (!binding.HasMotionSocket)
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    authoredOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.MissingSocket,
                    string.Empty,
                    0);
            }

            if (!binding.HasAuthorityPoseContactAttachment
                && _presentationPoseProvider != null
                && _presentationPoseProvider.TryResolveBodyPartContactPointEntityLocalOffset(
                    entity,
                    config,
                    out FixVector3 presentationOffset))
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    presentationOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.PresentationPose,
                    string.Empty,
                    0);
            }

            if (!binding.TryResolveClip(
                    entity.Action.ActiveActionAudioBindingId,
                    entity.Action.LocalFrame,
                    ResolveLocomotionClipKind(entity),
                    ClampFrame(frame.Value),
                    out CharacterBodyPartContactClipBinding clip,
                    out int localFrame))
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    authoredOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.MissingClip,
                    string.Empty,
                    0);
            }

            if (!binding.SourceAvailable)
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    authoredOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.MissingSource,
                    clip.ClipKey,
                    localFrame);
            }

            bool loop = entity.Action.ActiveActionAudioBindingId == 0;
            if (binding.HasAuthorityPoseContactAttachment
                && TryResolveAuthorityPoseContactPointBase(
                    binding,
                    clip.ClipKey,
                    motionSocketId,
                    localFrame,
                    loop,
                    authoredOffset,
                    out FixVector3 packageBaseOffset,
                    out int packageLocalFrame))
            {
                TryApplyAuthorityPoseContactPoint(
                    entity,
                    binding.AuthorityPoseRig,
                    binding.AuthorityPoseCatalog,
                    config,
                    clip.ClipKey,
                    localFrame,
                    loop,
                    frame,
                    packageBaseOffset,
                    out FixVector3 resolvedOffset);
                return CharacterTestBodyPartContactMotionResolution.Create(
                    resolvedOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.BakedSocket,
                    clip.ClipKey,
                    packageLocalFrame);
            }

            if (entity.Action.ActiveActionAudioBindingId == 0
                && clip.Track != null
                && clip.Track.FrameCount > 0)
            {
                localFrame %= clip.Track.FrameCount;
            }

            if (clip.Track == null
                || !clip.Track.TryGetFrame(localFrame, out MxAnimationBakedSocketFrame frameData))
            {
                return CharacterTestBodyPartContactMotionResolution.Create(
                    authoredOffset,
                    config.MotionMode,
                    motionSocketId,
                    CharacterTestBodyPartContactMotionState.MissingFrame,
                    clip.ClipKey,
                    localFrame);
            }

            return CharacterTestBodyPartContactMotionResolution.Create(
                ToFixVector3(frameData.Position) + authoredOffset,
                config.MotionMode,
                motionSocketId,
                CharacterTestBodyPartContactMotionState.BakedSocket,
                clip.ClipKey,
                localFrame);
        }

        private static bool TryResolveAuthorityPoseContactPointBase(
            CharacterBodyPartContactRuntimeBinding binding,
            string clipKey,
            int motionSocketId,
            int localFrame,
            bool loop,
            FixVector3 authoredOffset,
            out FixVector3 baseOffset,
            out int resolvedLocalFrame)
        {
            baseOffset = authoredOffset;
            resolvedLocalFrame = localFrame < 0 ? 0 : localFrame;
            CharacterSocketBakeCatalog catalog = binding.AuthorityPoseCatalog;
            if (catalog == null
                || !catalog.TryGetTrack(
                    clipKey,
                    motionSocketId,
                    out CharacterSocketBakeTrack track)
                || track == null
                || track.Frames.Count == 0)
            {
                return false;
            }

            int frameIndex = localFrame;
            if (loop)
                frameIndex %= track.Frames.Count;
            else if ((uint)frameIndex >= (uint)track.Frames.Count)
                return false;
            if (frameIndex < 0
                || !track.TryGetFrame(frameIndex, out MxAnimationBakedSocketFrame frameData))
            {
                return false;
            }

            baseOffset = ToFixVector3(frameData.Position, track.QuantizationScale) + authoredOffset;
            resolvedLocalFrame = frameIndex;
            return true;
        }

        internal static bool TryApplyAuthorityPoseContactPoint(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterSocketBakeCatalog catalog,
            CharacterBodyPartContactPointConfig config,
            string clipKey,
            int localFrame,
            bool loop,
            RuntimeFrame expectedRuntimeFrame,
            FixVector3 baseLocalOffset,
            out FixVector3 resolvedLocalOffset)
        {
            resolvedLocalOffset = baseLocalOffset;
            if (config == null
                || !CharacterTestAuthorityPoseConsumerMath.HasAuthoredAttachment(
                    rig,
                    CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
                    config.ContactPointId)
                || !CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                    entity,
                    rig,
                    catalog,
                    CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
                    config.ContactPointId,
                    clipKey,
                    localFrame,
                    loop,
                    expectedRuntimeFrame,
                    out CharacterTestAuthorityPoseAttachmentDelta delta))
            {
                return false;
            }

            resolvedLocalOffset = delta.TransformPoint(baseLocalOffset);
            return true;
        }

        private static void CountBodyPartContactMotionResolution(
            CharacterTestBodyPartContactMotionResolution motion,
            ref PhysicalAudioFrameState state)
        {
            switch (motion.State)
            {
                case CharacterTestBodyPartContactMotionState.BakedSocket:
                    state.BakedMotionResolvedCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.Static:
                    state.StaticMotionCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.MissingSocket:
                    state.MissingMotionSocketCount++;
                    state.MotionFallbackCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.MissingClip:
                    state.MissingMotionClipCount++;
                    state.MotionFallbackCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.MissingFrame:
                    state.MissingMotionFrameCount++;
                    state.MotionFallbackCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.MissingSource:
                    state.MissingMotionSourceCount++;
                    state.MotionFallbackCount++;
                    break;
                case CharacterTestBodyPartContactMotionState.PresentationPose:
                    state.PresentationMotionResolvedCount++;
                    break;
            }
        }

        private static bool TryResolveLocomotionAnimationClipKey(
            CharacterImportedPackage package,
            string targetClipId,
            out string clipKey)
        {
            clipKey = string.Empty;
            CharacterAnimationSetDefinition animationSet = package?.CompiledAnimationArtifacts?.AnimationSetDefinition;
            CharacterAnimationSet[] sets = animationSet?.Sets ?? Array.Empty<CharacterAnimationSet>();
            for (int i = 0; i < sets.Length; i++)
            {
                CharacterAnimationGroup[] groups = sets[i]?.Groups ?? Array.Empty<CharacterAnimationGroup>();
                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    CharacterAnimationGroup group = groups[groupIndex];
                    if (group == null || !IsLocomotionGroup(group))
                        continue;

                    CharacterAnimationClipReference[] clips = group.Clips ?? Array.Empty<CharacterAnimationClipReference>();
                    if (TryResolveLocomotionClipKey(clips, targetClipId, out clipKey))
                        return true;
                }
            }

            return false;
        }

        private static CharacterTestLocomotionAudioClipKind ResolveLocomotionClipKind(
            CharacterTestCharacterEntity entity)
        {
            if (entity == null || !entity.IsGrounded)
                return CharacterTestLocomotionAudioClipKind.None;

            CharacterMovementMode mode = entity.MovementMode;
            bool running = mode == CharacterMovementMode.Run;
            int velocityX = entity.HorizontalVelocityXMillimetersPerSecond;
            int velocityZ = entity.HorizontalVelocityYMillimetersPerSecond;
            if (velocityX == 0 && velocityZ == 0)
                return CharacterTestLocomotionAudioClipKind.Idle;

            int absX = Math.Abs(velocityX);
            int absZ = Math.Abs(velocityZ);
            if (absX > absZ)
            {
                if (running)
                    return velocityX >= 0
                        ? CharacterTestLocomotionAudioClipKind.RunRight
                        : CharacterTestLocomotionAudioClipKind.RunLeft;
                return velocityX >= 0
                    ? CharacterTestLocomotionAudioClipKind.WalkRight
                    : CharacterTestLocomotionAudioClipKind.WalkLeft;
            }

            if (running)
                return velocityZ >= 0
                    ? CharacterTestLocomotionAudioClipKind.RunForward
                    : CharacterTestLocomotionAudioClipKind.RunBack;
            return velocityZ >= 0
                ? CharacterTestLocomotionAudioClipKind.WalkForward
                : CharacterTestLocomotionAudioClipKind.WalkBack;
        }

        /// <remarks>LowFreq only.</remarks>
        private static string ResolveLocomotionClipId(CharacterTestLocomotionAudioClipKind kind)
        {
            switch (kind)
            {
                case CharacterTestLocomotionAudioClipKind.Idle: return LocomotionClipIdle;
                case CharacterTestLocomotionAudioClipKind.WalkForward: return LocomotionClipWalkForward;
                case CharacterTestLocomotionAudioClipKind.WalkBack: return LocomotionClipWalkBack;
                case CharacterTestLocomotionAudioClipKind.WalkLeft: return LocomotionClipWalkLeft;
                case CharacterTestLocomotionAudioClipKind.WalkRight: return LocomotionClipWalkRight;
                case CharacterTestLocomotionAudioClipKind.RunForward: return LocomotionClipRunForward;
                case CharacterTestLocomotionAudioClipKind.RunBack: return LocomotionClipRunBack;
                case CharacterTestLocomotionAudioClipKind.RunLeft: return LocomotionClipRunLeft;
                case CharacterTestLocomotionAudioClipKind.RunRight: return LocomotionClipRunRight;
                default: return string.Empty;
            }
        }

        private static bool IsLocomotionGroup(CharacterAnimationGroup group)
        {
            return group != null
                && (string.Equals(group.GroupId, "locomotion", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.Usage, "Locomotion", StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(group.DisplayName)
                        && group.DisplayName.IndexOf("Locomotion", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool TryResolveLocomotionClipKey(
            CharacterAnimationClipReference[] clips,
            string targetClipId,
            out string clipKey)
        {
            clipKey = string.Empty;
            string fallback = string.Empty;
            for (int i = 0; i < clips.Length; i++)
            {
                CharacterAnimationClipReference clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.RuntimeResourceKey))
                    continue;

                if (string.IsNullOrWhiteSpace(fallback))
                    fallback = clip.RuntimeResourceKey;
                if (string.Equals(clip.ClipId, targetClipId, StringComparison.Ordinal))
                {
                    clipKey = clip.RuntimeResourceKey;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                clipKey = fallback;
                return true;
            }

            return false;
        }

        private static FixVector3 CreateContactPointStaticOffset(CharacterBodyPartContactPointConfig config)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(config.LocalOffsetXMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(config.LocalOffsetYMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(config.LocalOffsetZMillimeters));
        }

        private static CharacterImportedPackage ResolveCharacterPackage(
            CharacterTestCharacterEntity entity)
        {
            return CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
        }

        private static CharacterBodyPartContactPointConfig[] ResolveBodyPartContactPointConfigs(
            CharacterImportedPackage package)
        {
            return package?.Configs?.BodyPartContactPoints ?? Array.Empty<CharacterBodyPartContactPointConfig>();
        }

        private static bool IsPresentationAudioContactPoint(CharacterBodyPartContactPointConfig config)
        {
            if (config == null)
                return false;

            string usage = config.Usage;
            if (!string.IsNullOrWhiteSpace(usage)
                && !string.Equals(usage, PresentationAudioUsage, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string authority = config.Authority;
            return string.IsNullOrWhiteSpace(authority)
                || string.Equals(authority, PresentationOnlyAuthority, StringComparison.OrdinalIgnoreCase);
        }

        private static CombatBodyPartContactActivationMode MapActivationMode(
            CharacterBodyPartContactActivationMode activationMode)
        {
            switch (activationMode)
            {
                case CharacterBodyPartContactActivationMode.Always:
                    return CombatBodyPartContactActivationMode.Always;
                case CharacterBodyPartContactActivationMode.ActionWindow:
                    return CombatBodyPartContactActivationMode.ActionWindow;
                case CharacterBodyPartContactActivationMode.ExplicitState:
                    return CombatBodyPartContactActivationMode.ExplicitState;
                case CharacterBodyPartContactActivationMode.MotionState:
                    return CombatBodyPartContactActivationMode.MotionState;
                default:
                    return CombatBodyPartContactActivationMode.Disabled;
            }
        }

        private static bool IsBodyPartContactSampleActive(in CombatBodyPartContactPointSample sample)
        {
            switch (sample.Definition.ActivationMode)
            {
                case CombatBodyPartContactActivationMode.Always:
                    return true;
                case CombatBodyPartContactActivationMode.ActionWindow:
                    return sample.ActionWindowActive;
                case CombatBodyPartContactActivationMode.ExplicitState:
                    return sample.ExplicitActive;
                case CombatBodyPartContactActivationMode.MotionState:
                    return sample.MotionStateActive;
                default:
                    return false;
            }
        }

        private static FixVector3 TransformContactPointLocalOffset(
            FixVector3 bodyPosition,
            FixVector3 forward,
            FixVector3 right,
            FixVector3 up,
            FixVector3 localOffset)
        {
            FixVector3 safeForward = forward.IsZero
                ? new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)
                : forward;
            FixVector3 safeRight = right.IsZero
                ? new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)
                : right;
            FixVector3 safeUp = up.IsZero
                ? new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)
                : up;
            return bodyPosition
                + (safeRight * localOffset.X)
                + (safeUp * localOffset.Y)
                + (safeForward * localOffset.Z);
        }

        private static void ResolveDeterministicAxes(
            CharacterTestCharacterTransformComponent transform,
            out FixVector3 forward,
            out FixVector3 right,
            out FixVector3 up)
        {
            CharacterTestDeterministicMath.SinCosMilliDegrees(
                transform.YawMilliDegrees,
                out int sin,
                out int cos);
            Fix64 forwardX = Fix64.FromRaw(((long)sin * Fix64.Scale) / CharacterTestDeterministicMath.TrigScale);
            Fix64 forwardZ = Fix64.FromRaw(((long)cos * Fix64.Scale) / CharacterTestDeterministicMath.TrigScale);
            Fix64 rightX = Fix64.FromRaw(((long)cos * Fix64.Scale) / CharacterTestDeterministicMath.TrigScale);
            Fix64 rightZ = Fix64.FromRaw(-((long)sin * Fix64.Scale) / CharacterTestDeterministicMath.TrigScale);
            forward = new FixVector3(forwardX, Fix64.Zero, forwardZ);
            right = new FixVector3(rightX, Fix64.Zero, rightZ);
            up = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        }
    }
}
