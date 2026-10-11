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
    internal interface ICharacterTestBodyPartContactPresentationPoseProvider
    {
        bool TryResolveBodyPartContactPointEntityLocalOffset(
            CharacterTestCharacterEntity entity,
            CharacterBodyPartContactPointConfig config,
            out FixVector3 localOffset);
    }

    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        internal event Action<CombatPhysicalContactEvent> ContactObserved;
        // Compatibility-only values retained for existing callers. Body-part contact triggering no longer
        // uses speed or traveled distance; the configured physical contact probes are authoritative.
        public const int MinimumBodyPartContactSpeedMillimetersPerSecond = 600;
        public const int MinimumSoleContactSpeedMillimetersPerSecond =
            MinimumBodyPartContactSpeedMillimetersPerSecond;
        public const int SoleContactDistanceMillimeters = 1200;
        public const int BodyPartContactCooldownFrames = 8;
        public const int FootContactCooldownFrames = BodyPartContactCooldownFrames;
        public const int SoleContactCooldownFrames = BodyPartContactCooldownFrames;
        public const int CharacterBodyPartContactGlobalCooldownFrames = 8;
        public const int CharacterSoleContactGlobalCooldownFrames =
            CharacterBodyPartContactGlobalCooldownFrames;
        public const int MinimumBodyPartImpactSpeedMillimetersPerSecond = 900;
        public const int MinimumLandingSpeedMillimetersPerSecond =
            MinimumBodyPartImpactSpeedMillimetersPerSecond;
        public const int MaximumSoleContactFrameDistanceMillimeters = 1500;
        public const int MinimumObjectContactSpeedMillimetersPerSecond = 350;
        public const int MinimumObjectContactPressure = 15000;
        public const int ObjectContactCooldownFrames = 12;
        public const int MovableNaturalObjectEmitterIdOffset = 700000;
        private const string PresentationAudioUsage = "PresentationAudio";
        private const string PresentationOnlyAuthority = "PresentationOnly";
        public const int FootProbeLateralOffsetMillimeters = 160;
        public const int FootProbeForwardOffsetMillimeters = 120;
        public const int FootProbeRadiusMillimeters = 90;
        public const int FootProbeDownMillimeters = 140;
        private const int BodyPartContactTraceId = 500260;
        private const int PhysicalAudioContactEventCapacity = 32;
        private const int PhysicalAudioSensorCapacity = 64;
        private const int PhysicalAudioQueryHitCapacity = 8;
        private const int PhysicalAudioCooldownCapacity = 96;
        private const int StandaloneAudioIntentCapacity = 64;
        private const int ObjectImpactSlotId = 4;
        private static readonly AudioTraceIdentity ResolvedFootstepTraceId = new AudioTraceIdentity(AudioTraceKindIds.PhysicalFootstep);
        private static readonly AudioTraceIdentity ResolvedLandingTraceId = new AudioTraceIdentity(AudioTraceKindIds.PhysicalLanding);
        private static readonly AudioTraceIdentity ResolvedObjectImpactTraceId = new AudioTraceIdentity(AudioTraceKindIds.PhysicalObjectImpact);
        private const string LocomotionClipIdle = "idle";
        private const string LocomotionClipWalkForward = "walk.f";
        private const string LocomotionClipWalkBack = "walk.b";
        private const string LocomotionClipWalkLeft = "walk.l";
        private const string LocomotionClipWalkRight = "walk.r";
        private const string LocomotionClipRunForward = "run.f";
        private const string LocomotionClipRunBack = "run.b";
        private const string LocomotionClipRunLeft = "run.l";
        private const string LocomotionClipRunRight = "run.r";
        private static readonly Fix64 MinimumStandableNormalY = Fix64.Half;

        private readonly Dictionary<int, CharacterBodyPartContactAudioState> _characterContactAudioStates =
            new Dictionary<int, CharacterBodyPartContactAudioState>(PhysicalAudioSensorCapacity);
        private readonly CombatPhysicalContactEventBuffer _contactEvents =
            new CombatPhysicalContactEventBuffer(PhysicalAudioContactEventCapacity);
        private readonly PhysicalAudioContactDynamics[] _contactDynamics =
            new PhysicalAudioContactDynamics[PhysicalAudioContactEventCapacity];
        private readonly int[] _startedBodyPartContactEventIndices =
            new int[PhysicalAudioContactEventCapacity];
        private readonly PhysicalAudioContactDynamics[] _startedBodyPartContactDynamics =
            new PhysicalAudioContactDynamics[PhysicalAudioContactEventCapacity];
        private readonly CombatPhysicalContactEventBuffer _bodyPartContactEvents =
            new CombatPhysicalContactEventBuffer(PhysicalAudioContactEventCapacity);
        private readonly Dictionary<int, CombatBodyPartContactPointRuntime> _bodyPartContactRuntimes =
            new Dictionary<int, CombatBodyPartContactPointRuntime>(PhysicalAudioSensorCapacity);
        private readonly Dictionary<int, CharacterBodyPartContactRegistration> _contactRegistrations =
            new Dictionary<int, CharacterBodyPartContactRegistration>(PhysicalAudioSensorCapacity);
        private readonly Dictionary<BodyPartContactDerivedKey, BodyPartContactDerivedDefinition> _bodyPartContactDerivedDefinitions =
            new Dictionary<BodyPartContactDerivedKey, BodyPartContactDerivedDefinition>(PhysicalAudioSensorCapacity);
        private readonly List<CombatBodyPartContactPointSample> _bodyPartContactSamples =
            new List<CombatBodyPartContactPointSample>(PhysicalAudioSensorCapacity);
        private readonly CharacterTestBodyPartContactDebugEntry[] _bodyPartContactDebugEntries =
            new CharacterTestBodyPartContactDebugEntry[PhysicalAudioSensorCapacity];
        private readonly Dictionary<BodyPartContactDebugKey, int> _bodyPartContactDebugEntryIndices =
            new Dictionary<BodyPartContactDebugKey, int>(PhysicalAudioSensorCapacity);
        private readonly PhysicalAudioPlaybackSystem _physicalAudioPlaybackSystem;
        private readonly PhysicalAudioResolver _physicalAudioResolver;
        private readonly ContinuousPhysicalAudioSystem _continuousAudioSystem;
        private readonly AudioParameterValue[] _physicalResolveParameters;
        private readonly CharacterTestFixedCooldownCache<ResolvedContactCooldownKey> _resolvedContactCooldowns =
            new CharacterTestFixedCooldownCache<ResolvedContactCooldownKey>(PhysicalAudioCooldownCapacity);
        private readonly ICharacterTestAudioCueCatalog _audioCueCatalog;
        private readonly IAcousticBodyPropertyProvider _propertyProvider;
        private readonly CharacterTestAudioIntentQueue _audioIntentQueue;
        private readonly CharacterTestAudioIntentDrainSystem _standaloneAudioIntentDrainSystem;
        private readonly bool _useStandaloneAudioIntentQueue;
        private readonly Dictionary<int, string> _cueKeyByEventId;
        private ICharacterTestBakedWeaponTraceSource _bakedMotionSource;
        private ICharacterTestBodyPartContactPresentationPoseProvider _presentationPoseProvider;
        private CharacterTestCharacterActionRuntime _actionRuntime;
        private int _bodyPartContactDebugCount;
        private RuntimeFrame _bodyPartContactDebugFrame;

        public CharacterTestPhysicalAudioSystem(CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem)
            : this(audioCuePlaybackSystem, null, null)
        {
        }

        internal CharacterTestPhysicalAudioSystem(
            CharacterTestAudioCuePlaybackSystem audioCuePlaybackSystem,
            CharacterTestAudioIntentQueue audioIntentQueue,
            IAcousticBodyPropertyProvider propertyProvider = null)
        {
            ICharacterTestAudioCueCatalog catalog = audioCuePlaybackSystem == null ? null : audioCuePlaybackSystem.Catalog;
            _audioCueCatalog = catalog;
            _propertyProvider = propertyProvider;
            _audioIntentQueue = audioIntentQueue ?? new CharacterTestAudioIntentQueue(StandaloneAudioIntentCapacity);
            _useStandaloneAudioIntentQueue = audioIntentQueue == null;
            _standaloneAudioIntentDrainSystem = _useStandaloneAudioIntentQueue
                ? new CharacterTestAudioIntentDrainSystem(audioCuePlaybackSystem)
                : null;
            _cueKeyByEventId = BuildPhysicalAudioEventCueMap(catalog);
            IPhysicalAudioDefinitionProvider physicalDefinitions = catalog?.PhysicalAudioDefinitions;
            if (physicalDefinitions != null)
            {
                _physicalAudioResolver = new PhysicalAudioResolver(physicalDefinitions);
                _physicalResolveParameters = new AudioParameterValue[CharacterTestAudioIntent.MaxParameterCount];
                if (audioCuePlaybackSystem?.AudioService != null && catalog != null)
                {
                    _continuousAudioSystem = new ContinuousPhysicalAudioSystem(
                        audioCuePlaybackSystem.AudioService,
                        catalog,
                        _physicalAudioResolver,
                        voiceCapacity: 24,
                        parameterCapacity: CharacterTestAudioIntent.MaxParameterCount);
                }
            }
            else
            {
                _physicalResolveParameters = AudioPlayRequest.EmptyParameters;
                _physicalAudioPlaybackSystem = new PhysicalAudioPlaybackSystem(
                    new CharacterTestAudioIntentAudioService(
                        _audioIntentQueue,
                        CharacterTestAudioIntentSource.PhysicalAudio),
                    CreatePhysicalAudioRuleTable(catalog),
                    PhysicalAudioCooldownCapacity,
                    CreatePhysicalAudioParameterMapping(catalog));
            }
        }

        public CharacterTestPhysicalAudioSummary LastSummary { get; private set; } =
            CharacterTestPhysicalAudioSummary.Empty;
        public CharacterTestAudioIntentDrainSummary LastIntentDrainSummary =>
            _standaloneAudioIntentDrainSystem == null
                ? CharacterTestAudioIntentDrainSummary.Empty
                : _standaloneAudioIntentDrainSystem.LastSummary;
        internal CharacterTestBodyPartContactDebugSnapshot BodyPartContactDebugSnapshot =>
            new CharacterTestBodyPartContactDebugSnapshot(
                _bodyPartContactDebugFrame,
                _bodyPartContactDebugEntries,
                _bodyPartContactDebugCount);
        public ContinuousPhysicalAudioSnapshot ContinuousAudioSnapshot =>
            _continuousAudioSystem?.Snapshot ?? default;

        /// <summary>Consumes deterministic Started/Stayed/Ended scrape, roll or compression facts.</summary>
        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult ProcessContinuousInteraction(in PhysicalAudioInteractionFact fact)
        {
            if (_continuousAudioSystem == null)
            {
                return AudioResult.Fail(
                    AudioErrorCode.BackendUnavailable,
                    "Continuous physical audio is unavailable because the linked runtime catalog is missing.");
            }
            return _continuousAudioSystem.Process(fact);
        }

        public int StopContinuousInteractions(AudioStopMode stopMode = AudioStopMode.AllowFadeout)
        {
            return _continuousAudioSystem?.StopAll(stopMode) ?? 0;
        }

        /// <remarks>
        /// NoAlloc after entity-spawn registration (or an explicit low-frequency bulk preparation); capacity
        /// overflow is counted and safely skipped.
        /// </remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            int tickRate = CharacterTestDeterministicUnits.DefaultTickRate,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null)
        {
            if (tickRate <= 0)
                tickRate = CharacterTestDeterministicUnits.DefaultTickRate;

            var state = new PhysicalAudioFrameState(frame);
            BeginBodyPartContactDebug(frame);
            if (world == null)
            {
                LastSummary = state.ToSummary();
                return;
            }

            _contactEvents.Clear();
            if (_useStandaloneAudioIntentQueue)
                _audioIntentQueue.Clear();
            TickCharacters(world, frame, tickRate, physicsRuntime, ref state);
            TickMovableObjects(world.MovableNaturalObjects, frame, ref state);
            ApplyPhysicalAudioPlayback(frame, ref state);
            if (_useStandaloneAudioIntentQueue)
            {
                CharacterTestAudioIntentDrainSummary drainSummary =
                    _standaloneAudioIntentDrainSystem.Tick(_audioIntentQueue, frame);
                state.SkippedCount += drainSummary.FailedCount + drainSummary.DroppedCount;
            }
            LastSummary = state.ToSummary();
        }

        private void TickCharacters(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            int tickRate,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ref PhysicalAudioFrameState state)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world?.Entities;
            if (entities == null || entities.Count == 0)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                state.CharacterCount++;
                TickCharacter(entity, world, frame, tickRate, physicsRuntime, ref state);
            }
        }

        private void TickCharacter(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            int tickRate,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ref PhysicalAudioFrameState state)
        {
            int entityId = entity.Id.Value;
            if (!_characterContactAudioStates.TryGetValue(entityId, out CharacterBodyPartContactAudioState contactAudioState))
            {
                state.SkippedCount++;
                state.RegistrationCapacitySkipCount++;
                return;
            }

            if (physicsRuntime == null || !physicsRuntime.IsReady)
            {
                state.SkippedCount++;
                state.MissingPhysicsRuntimeSkipCount++;
                ResetBodyPartContactRuntimeIfPresent(entityId);
                ResetBodyPartContactKinematicsIfPresent(entityId);
                contactAudioState.MarkNoContact();
                contactAudioState.CaptureMotion(
                    frame,
                    entity.IsGrounded,
                    entity.HorizontalVelocityXMillimetersPerSecond,
                    entity.HorizontalVelocityYMillimetersPerSecond,
                    entity.VerticalVelocityMillimetersPerSecond);
                _characterContactAudioStates[entityId] = contactAudioState;
                return;
            }

            int horizontalSpeed = EstimateHorizontalSpeedMillimetersPerSecond(
                entity.HorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond);
            if (entity.IsGrounded && horizontalSpeed > 0)
                state.MovingGroundedCharacterCount++;

            if (!_contactRegistrations.TryGetValue(
                    entityId,
                    out CharacterBodyPartContactRegistration registration)
                || registration.Bindings.Length == 0)
            {
                state.SkippedCount++;
                state.MissingContactPointConfigSkipCount++;
                _bodyPartContactSamples.Clear();
                ResetBodyPartContactRuntimeIfPresent(entityId);
                registration?.ResetKinematics();
                contactAudioState.MarkNoContact();
                contactAudioState.CaptureMotion(
                    frame,
                    entity.IsGrounded,
                    entity.HorizontalVelocityXMillimetersPerSecond,
                    entity.HorizontalVelocityYMillimetersPerSecond,
                    entity.VerticalVelocityMillimetersPerSecond);
                _characterContactAudioStates[entityId] = contactAudioState;
                return;
            }

            int normalContactSpeed = ResolveBodyPartContactNormalSpeed(entity, contactAudioState);
            int contactIntensity = ResolveBodyPartContactIntensity(horizontalSpeed, normalContactSpeed);
            _bodyPartContactEvents.Clear();
            FillBodyPartContactPointSamples(
                entity,
                physicsRuntime,
                frame,
                tickRate,
                horizontalSpeed,
                normalContactSpeed,
                contactIntensity,
                registration,
                ref state);
            if (_bodyPartContactSamples.Count == 0)
            {
                state.SkippedCount++;
                state.MissingContactPointConfigSkipCount++;
                ResetBodyPartContactRuntimeIfPresent(entityId);
                registration.ResetKinematics();
                contactAudioState.MarkNoContact();
                contactAudioState.CaptureMotion(
                    frame,
                    entity.IsGrounded,
                    entity.HorizontalVelocityXMillimetersPerSecond,
                    entity.HorizontalVelocityYMillimetersPerSecond,
                    entity.VerticalVelocityMillimetersPerSecond);
                _characterContactAudioStates[entityId] = contactAudioState;
                return;
            }

            if (!_bodyPartContactRuntimes.TryGetValue(entityId, out CombatBodyPartContactPointRuntime runtime))
            {
                state.SkippedCount++;
                state.RegistrationCapacitySkipCount++;
                return;
            }

            runtime.Tick(
                physicsRuntime.World,
                _bodyPartContactSamples,
                new CombatFrame(ClampFrame(frame.Value)),
                _bodyPartContactEvents);

            if (ContactObserved != null)
                for (int i = 0; i < _bodyPartContactEvents.Count; i++) ContactObserved(_bodyPartContactEvents[i]);

            AddBodyPartContactAudioEvents(
                _bodyPartContactEvents,
                entity,
                world,
                physicsRuntime,
                registration,
                contactAudioState,
                frame,
                tickRate,
                ref state);
            CountBodyPartContactResults(ref state);

            contactAudioState.CaptureMotion(
                frame,
                entity.IsGrounded,
                entity.HorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond,
                entity.VerticalVelocityMillimetersPerSecond);
            _characterContactAudioStates[entityId] = contactAudioState;
        }

        private void ResetBodyPartContactRuntimeIfPresent(int entityId)
        {
            if (_bodyPartContactRuntimes.TryGetValue(entityId, out CombatBodyPartContactPointRuntime runtime))
                runtime.Reset();
        }

        private void ResetBodyPartContactKinematicsIfPresent(int entityId)
        {
            if (_contactRegistrations.TryGetValue(
                    entityId,
                    out CharacterBodyPartContactRegistration registration))
            {
                registration.ResetKinematics();
            }
        }

        private void TickMovableObjects(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects,
            RuntimeFrame frame,
            ref PhysicalAudioFrameState state)
        {
            if (objects == null || objects.Count == 0)
                return;

            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState movableObject = objects[i];
                if (movableObject == null)
                    continue;

                TickMovableObject(movableObject, frame, ref state);
            }
        }

        private void TickMovableObject(
            CharacterTestMovableNaturalObjectState movableObject,
            RuntimeFrame frame,
            ref PhysicalAudioFrameState state)
        {
            if (movableObject.ContactCount <= 0)
                return;

            if (!IsSpeedAtLeast(
                    movableObject.ContactVelocityXMillimetersPerSecond,
                    movableObject.ContactVelocityZMillimetersPerSecond,
                    MinimumObjectContactSpeedMillimetersPerSecond)
                && movableObject.ContactPressure < MinimumObjectContactPressure)
            {
                return;
            }

            int objectId = movableObject.NumericId;
            if (objectId <= 0)
                return;

            state.ObjectContactCount++;
            AddObjectImpactContactEvent(movableObject, frame);
        }

        private void CountBodyPartContactResults(ref PhysicalAudioFrameState state)
        {
            for (int i = 0; i < _bodyPartContactDebugCount; i++)
            {
                CharacterTestBodyPartContactDebugEntry entry = _bodyPartContactDebugEntries[i];
                if (!entry.Active)
                    continue;

                if (!entry.HasHit)
                {
                    state.BodyPartProbeMissCount++;
                    state.FootProbeMissCount++;
                    continue;
                }

                if (!entry.ContactAllowed)
                {
                    state.BodyPartFilteredContactCount++;
                    continue;
                }

                state.BodyPartContactCount++;
                if (entry.ContactPointId == 1)
                    state.LeftFootContactCount++;
                else if (entry.ContactPointId == 2)
                    state.RightFootContactCount++;
            }
        }

        private void AddObjectImpactContactEvent(
            CharacterTestMovableNaturalObjectState movableObject,
            RuntimeFrame frame)
        {
            int objectId = movableObject.NumericId;
            int sourceId = MovableNaturalObjectEmitterIdOffset + objectId;
            var sourceEntityId = new CombatEntityId(sourceId);
            var sourceBodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart + objectId);
            var sourceColliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId);
            int relativeSpeed = EstimateHorizontalSpeedMillimetersPerSecond(
                movableObject.ContactVelocityXMillimetersPerSecond,
                movableObject.ContactVelocityZMillimetersPerSecond);
            int massPressure = ResolvePressure(relativeSpeed, movableObject.MassGrams);
            int contactPressure = Math.Max(movableObject.ContactPressure, massPressure);
            int intensity = Math.Max(relativeSpeed, contactPressure / 100);
            int pairKey = StableContactPairKey(
                CombatPhysicalContactKind.ObjectImpact,
                movableObject.ContactPair,
                objectId);
            var contactEvent = new CombatPhysicalContactEvent(
                new CombatFrame(ClampFrame(frame.Value)),
                _contactEvents.Count,
                CombatPhysicalContactKind.ObjectImpact,
                CombatPhysicalContactPhase.Started,
                sourceEntityId,
                sourceBodyId,
                sourceColliderId,
                CombatBodyId.None,
                CombatColliderId.None,
                CombatPhysicsShapeKind.Aabb,
                ObjectImpactSlotId,
                pairKey,
                CreateFixPoint(
                    movableObject.XMillimeters,
                    movableObject.YMillimeters,
                    movableObject.ZMillimeters),
                FixVector3.Zero,
                movableObject.Material,
                relativeSpeed,
                0,
                relativeSpeed,
                contactPressure,
                intensity);
            int dynamicsIndex = _contactEvents.Count;
            if (_contactEvents.Add(contactEvent))
                _contactDynamics[dynamicsIndex] = default;
        }

        private bool AddAudioContactEvent(
            in CombatPhysicalContactEvent source,
            CombatFrame frame,
            in PhysicalAudioContactDynamics dynamics)
        {
            var contactEvent = new CombatPhysicalContactEvent(
                frame,
                _contactEvents.Count,
                source.Kind,
                source.Phase,
                source.SourceEntityId,
                source.SourceBodyId,
                source.SourceColliderId,
                source.TargetBodyId,
                source.TargetColliderId,
                source.TargetShapeKind,
                source.SourceSlotId,
                source.ContactPairKey,
                source.Point,
                source.Normal,
                source.SurfaceMaterial,
                source.RelativeSpeedMillimetersPerSecond,
                source.NormalSpeedMillimetersPerSecond,
                source.TangentialSpeedMillimetersPerSecond,
                source.Pressure,
                source.Intensity,
                source.ContactPointId,
                source.BodyPartId,
                source.ActivationMode);
            int dynamicsIndex = _contactEvents.Count;
            if (!_contactEvents.Add(contactEvent))
                return false;
            _contactDynamics[dynamicsIndex] = dynamics;
            return true;
        }

        private void ApplyPhysicalAudioPlayback(RuntimeFrame frame, ref PhysicalAudioFrameState state)
        {
            if (_physicalAudioResolver != null)
            {
                ApplyResolvedPhysicalAudioPlayback(frame, ref state);
                return;
            }

            PhysicalAudioPlaybackSummary playbackSummary =
                _physicalAudioPlaybackSystem.Tick(_contactEvents, frame.Value);
            state.BodyPartContactPlayedCount += playbackSummary.BodyPartContactPlayedCount;
            state.LandingPlayedCount += playbackSummary.LandingPlayedCount;
            state.ObjectCollisionPlayedCount += playbackSummary.ObjectImpactPlayedCount;
            state.CooldownSkipCount += playbackSummary.CooldownSkipCount;
            state.SkippedCount += playbackSummary.MissingRuleSkipCount
                + playbackSummary.MissingAudioServiceSkipCount
                + playbackSummary.PlaybackFailedSkipCount
                + playbackSummary.DroppedContactEventCount;
            if (playbackSummary.LastEventId > 0)
            {
                state.LastCueKey = ResolveCueKey(playbackSummary.LastEventId);
                state.LastEmitterId = playbackSummary.LastEmitterId;
            }
        }

        /// <remarks>NoAlloc after initialization while cooldown storage and the intent queue remain within capacity.</remarks>
        private void ApplyResolvedPhysicalAudioPlayback(
            RuntimeFrame frame,
            ref PhysicalAudioFrameState state)
        {
            state.SkippedCount += _contactEvents.DroppedCount;
            for (int i = 0; i < _contactEvents.Count; i++)
            {
                CombatPhysicalContactEvent contactEvent = _contactEvents[i];
                if (!CharacterTestPhysicalAudioFactBuilder.TryCreate(
                        contactEvent,
                        _propertyProvider,
                        _contactDynamics[i],
                        out PhysicalAudioInteractionFact fact)
                    || !_physicalAudioResolver.TryResolve(
                        fact,
                        _physicalResolveParameters.AsSpan(),
                        out PhysicalAudioResolvedPlayback resolved))
                {
                    state.SkippedCount++;
                    continue;
                }

                if (_audioCueCatalog == null
                    || !_audioCueCatalog.TryGetPhysicalAudioBinding(
                        resolved.CueId,
                        out CharacterTestPhysicalAudioPlaybackBinding playbackBinding))
                {
                    state.SkippedCount++;
                    continue;
                }

                var cooldownKey = new ResolvedContactCooldownKey(
                    contactEvent.ContactPairKey,
                    contactEvent.SourceSlotId,
                    contactEvent.ContactPointId,
                    resolved.FamilyId.Value,
                    resolved.BasisId.Value);
                CharacterTestCooldownAdmission admission = _resolvedContactCooldowns.Evaluate(
                    cooldownKey,
                    frame.Value,
                    resolved.CooldownFrames);
                if (admission == CharacterTestCooldownAdmission.Cooldown)
                {
                    state.CooldownSkipCount++;
                    continue;
                }
                if (admission == CharacterTestCooldownAdmission.CapacityFull)
                {
                    state.CooldownCapacitySkipCount++;
                    state.SkippedCount++;
                    continue;
                }
                if (!_audioIntentQueue.Enqueue(CharacterTestAudioIntent.CreateEvent(
                        CharacterTestAudioIntentSource.PhysicalAudio,
                        playbackBinding.EventId,
                        fact.Transform,
                        fact.EmitterId,
                        frame,
                        ResolveTraceId(fact.Kind),
                        _physicalResolveParameters,
                        resolved.Priority,
                        playbackBinding.Is3D,
                        resolved.ParameterCount)))
                {
                    state.SkippedCount++;
                    continue;
                }

                PhysicalAudioLatestFactCapture.Publish(
                    fact,
                    resolved,
                    _physicalResolveParameters.AsSpan(0, resolved.ParameterCount),
                    _contactDynamics[i]);

                _resolvedContactCooldowns.Record(
                    cooldownKey,
                    frame.Value,
                    resolved.CooldownFrames);
                state.LastCueKey = playbackBinding.CueName;
                state.LastEmitterId = fact.EmitterId;
                switch (fact.Kind)
                {
                    case PhysicalAudioInteractionKind.Footstep:
                        state.BodyPartContactPlayedCount++;
                        break;
                    case PhysicalAudioInteractionKind.Landing:
                        state.LandingPlayedCount++;
                        break;
                    case PhysicalAudioInteractionKind.ObjectImpact:
                        state.ObjectCollisionPlayedCount++;
                        break;
                }
            }
        }
    }
}
