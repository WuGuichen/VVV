using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Vfx;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule : RuntimeModule
    {
        private const int ReviveResetTraceKind = 90841;
        private const int RuntimeShutdownTraceKind = 90842;
        private static readonly ProfilerMarker SpawnMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.Spawn");
        private static readonly ProfilerMarker StatusControlMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.StatusControl");
        private static readonly ProfilerMarker BattleStateMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.BattleState");
        private static readonly ProfilerMarker ActionMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.Action");
        private static readonly ProfilerMarker AudioCueMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AudioCue");
        private static readonly ProfilerMarker AirDisplacementAudioMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AirDisplacementAudio");
        private static readonly ProfilerMarker RootMotionMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.RootMotion");
        private static readonly ProfilerMarker CollisionProfileMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.CollisionProfile");
        private static readonly ProfilerMarker MechanicMotionMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.MechanicMotion");
        private static readonly ProfilerMarker MotionMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.Motion");
        private static readonly ProfilerMarker PhysicalAudioMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.PhysicalAudio");
        private static readonly ProfilerMarker PostMotionInputFlowMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.PostMotionInputFlow");
        private static readonly ProfilerMarker CombatTraceMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.CombatTrace");
        private static readonly ProfilerMarker WeaponHitAudioMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.WeaponHitAudio");
        private static readonly ProfilerMarker InteractionConsumersMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.InteractionConsumers");
        private static readonly ProfilerMarker AudioIntentDrainMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AudioIntentDrain");
        private static readonly ProfilerMarker CompleteActionsMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.CompleteActions");
        private static readonly ProfilerMarker AuthorityPoseMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AuthorityPose");
        private static readonly ProfilerMarker AnimationMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.Animation");
        private static readonly ProfilerMarker ViewSyncMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.ViewSync");
        private static readonly ProfilerMarker AnimationSyncMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AnimationSync");
        private static readonly ProfilerMarker AuthorityPoseSyncMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.AuthorityPoseSync");
        private static readonly ProfilerMarker DestructionMarker =
            new ProfilerMarker("CharacterTest.CharacterRuntime.Destruction");

        private readonly CharacterTestCharacterRuntimeWorld _world;
        private readonly CharacterTestCharacterUnityViewBridge _viewBridge;
        private readonly CharacterTestCharacterSpawnSystem _spawnSystem;
        private readonly CharacterTestCharacterDestructionSystem _destructionSystem;
        private readonly CharacterTestBattleStateSystem _battleStateSystem;
        private readonly AbilityRuntimeModule _abilityRuntimeModule;
        private readonly CharacterTestActionUseAuthority _actionUseAuthority;
        internal CharacterTestActionUseAuthority ActionUseAuthority => _actionUseAuthority;
        private readonly CharacterTestCharacterActionSystem _actionSystem;
        private readonly CharacterTestAuthorityPoseTerrainSystem _authorityPoseTerrainSystem;
        private readonly CharacterTestAudioCuePlaybackSystem _audioCuePlaybackSystem;
        private readonly CharacterTestAudioIntentQueue _audioIntentQueue =
            new CharacterTestAudioIntentQueue(256);
        private readonly CharacterTestAcousticBodyPropertyProvider _acousticBodyPropertyProvider =
            new CharacterTestAcousticBodyPropertyProvider();
        private readonly CharacterTestAudioIntentDrainSystem _audioIntentDrainSystem;
        private readonly CharacterTestAirDisplacementAudioSystem _airDisplacementAudioSystem;
        private readonly CharacterTestCharacterRootMotionSystem _rootMotionSystem;
        private readonly CharacterTestCollisionProfileResolverSystem _collisionProfileResolverSystem;
        private readonly CharacterTestStatusControlEffectBridge _statusControlEffectBridge;
        private readonly CharacterTestMechanicMotionEffectBridge _mechanicMotionEffectBridge;
        private readonly CharacterTestCharacterMotionSystem _motionSystem;
        private readonly CharacterTestPhysicalAudioSystem _physicalAudioSystem;
        private readonly CharacterTestCharacterCombatTraceSystem _combatTraceSystem;
        private readonly CharacterTestWeaponHitAudioSystem _weaponHitAudioSystem;
        private readonly CharacterTestInteractionPhysicsSystem _interactionPhysicsSystem;
        private readonly CharacterTestInteractionVfxSystem _interactionVfxSystem;
        private readonly CharacterTestInteractionAudioSystem _interactionAudioSystem;
        private readonly CharacterTestInteractionRenderingSystem _interactionRenderingSystem;
        private readonly CharacterTestInteractionConsumerDispatcher _interactionConsumerDispatcher;
        private readonly CharacterTestInteractionPerformanceCounterSource _interactionPerformanceCounterSource;
        private readonly CharacterTestRuntimePerformanceCounterSource _runtimePerformanceCounterSource;
        private readonly CharacterTestCharacterAnimationSystem _animationSystem;
        private readonly CharacterTestCharacterViewSyncSystem _viewSyncSystem;
        private readonly CharacterTestCharacterAnimationSyncSystem _animationSyncSystem;
        private readonly CharacterTestCharacterActionConfigSourceOptions _actionConfigOptions;
        private readonly CharacterTestTraceRecorder _traceRecorder;
        private readonly CharacterTestCharacterDefinitionRegistry _definitions;
        private readonly GameplayBuffDefinitionRegistry _buffDefinitions;
        private readonly GameplayBuffPresentationDefinitionRegistry _buffPresentationDefinitions;
        private readonly IRuntimeLogger _logger;
        private readonly int _simulationTickRate;
        internal int SimulationTickRate => _simulationTickRate;
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(320);
        private readonly bool _createViews;
        private readonly bool _smoothNetworkSnapshotViews;
        private CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime _runtimeAiBehaviorLoadouts;
        private Func<int> _lockedTargetEntityIdProvider;
        private int _presentationAllyTeamId;
        private bool _forceHeadPadsVisible;
        private CharacterTestHeadPadDisplayMode _headPadDisplayMode =
            CharacterTestHeadPadDisplayMode.CombatReadout;
        private CharacterTestGenesisMovableNaturalObjectValidation _lastGenesisMovableObjectValidation =
            CharacterTestGenesisMovableNaturalObjectValidation.Empty;
        private CharacterTestGenesisPotencyMechanicsValidation _lastGenesisPotencyMechanicsValidation =
            CharacterTestGenesisPotencyMechanicsValidation.Empty;
        private CharacterTestGenesisLivingBeingValidation _lastGenesisLivingBeingValidation =
            CharacterTestGenesisLivingBeingValidation.Empty;
        private CharacterTestGenesisLivingNaturalAttributesValidation _lastGenesisLivingNaturalAttributesValidation =
            CharacterTestGenesisLivingNaturalAttributesValidation.Empty;
        private long _lastPostMotionInputFlowFrame = long.MinValue;
        private RuntimeFrame _lastRuntimeFrame;
        private bool _hasCompletedRuntimeFrame;
        private bool _lastPostMotionHadInputOrMotion;
        private Func<CharacterTestCharacterEntity, RuntimeFrame, bool> _primaryAttackOpportunityGuard;

        public CharacterTestCharacterRuntimeModule(
            GlobalResourceRuntimeServices resources,
            IRuntimeLogger logger,
            CharacterTestCharacterDefinitionRegistry definitions = null,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            CharacterTestTraceRecorder traceRecorder = null,
            bool createViews = true,
            bool smoothNetworkSnapshotViews = false,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            AbilityRuntimeModule abilityRuntimeModule = null,
            GameplayBuffDefinitionRegistry buffDefinitions = null,
            GameplayBuffPresentationDefinitionRegistry buffPresentationDefinitions = null,
            int simulationTickRate = CharacterTestDeterministicUnits.DefaultTickRate,
            CharacterTestProceduralHitFeedbackOptions proceduralHitFeedbackOptions = null,
            CharacterTestAiBehaviorProfile aiBehaviorProfile = null,
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> movableNaturalObjectSpawnSpecs = null,
            VfxRuntimeModule interactionVfxRuntime = null,
            CharacterTestCharacterActionRuntime actionRuntime = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
            : base("CharacterTest.CharacterRuntime", RuntimeTickStage.Simulation, priority: 100)
        {
            using var startupProfile = CharacterTestStartupProfiler.CharacterRuntime.Auto();
            IRuntimeLogger safeLogger = logger ?? NullRuntimeLogger.Instance;
            CharacterTestCharacterDefinitionRegistry safeDefinitions =
                definitions ?? new CharacterTestCharacterDefinitionRegistry();
            CharacterTestProceduralHitFeedbackOptions safeProceduralHitFeedbackOptions =
                proceduralHitFeedbackOptions ?? CharacterTestProceduralHitFeedbackOptions.Default;
            _definitions = safeDefinitions;
            _buffDefinitions = buffDefinitions ?? CharacterTestGuardAuraRuntime.CreateBuffDefinitions();
            _buffPresentationDefinitions =
                buffPresentationDefinitions ?? CharacterTestGuardAuraRuntime.CreateBuffPresentationDefinitions();
            _logger = safeLogger;
            _actionConfigOptions = actionConfigOptions ?? CharacterTestCharacterActionConfigSourceOptions.CreateDefault();
            _traceRecorder = traceRecorder;
            _createViews = createViews;
            _smoothNetworkSnapshotViews = smoothNetworkSnapshotViews;
            _simulationTickRate = simulationTickRate > 0
                ? simulationTickRate
                : CharacterTestDeterministicUnits.DefaultTickRate;
            _abilityRuntimeModule = abilityRuntimeModule;
            _actionSystem = new CharacterTestCharacterActionSystem(
                safeLogger,
                _actionConfigOptions,
                _abilityRuntimeModule,
                runtime: actionRuntime,
                actionCatalog: actionCatalog);
            _actionSystem.PrepareAnimationDefinitions(safeDefinitions);
            _world = new CharacterTestCharacterRuntimeWorld();
            _world.SetMovableNaturalObjectSpawnSpecs(movableNaturalObjectSpawnSpecs);


            _viewBridge = new CharacterTestCharacterUnityViewBridge(
                resources,
                safeLogger,
                createViews
                && safeProceduralHitFeedbackOptions.Enabled
                && safeProceduralHitFeedbackOptions.InstallDefaultSecondaryChain);
            _viewBridge.SetTraceRecorder(_traceRecorder);
            _spawnSystem = new CharacterTestCharacterSpawnSystem(
                _world,
                safeDefinitions,
                _viewBridge,
                safeLogger,
                createViews);
            _destructionSystem = new CharacterTestCharacterDestructionSystem();
            _battleStateSystem = new CharacterTestBattleStateSystem();
            _abilityRuntimeModule?.SetCostAuthority(new CharacterTestAbilityCostAuthority(_world));
            _actionUseAuthority = new CharacterTestActionUseAuthority(this, _abilityRuntimeModule);
            _actionSystem.Runtime.BindRuntimeWorld(_world);
            _authorityPoseTerrainSystem =
                new CharacterTestAuthorityPoseTerrainSystem(_actionSystem.Runtime);
            _audioCuePlaybackSystem = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                audioCueCatalog,
                safeLogger,
                logMissingAudioServiceWarnings: createViews);
            _audioIntentDrainSystem = new CharacterTestAudioIntentDrainSystem(_audioCuePlaybackSystem);
            _airDisplacementAudioSystem = new CharacterTestAirDisplacementAudioSystem(
                _audioCuePlaybackSystem,
                _audioIntentQueue,
                _acousticBodyPropertyProvider);
            _rootMotionSystem = new CharacterTestCharacterRootMotionSystem(safeLogger);
            _collisionProfileResolverSystem = new CharacterTestCollisionProfileResolverSystem();
            _statusControlEffectBridge = new CharacterTestStatusControlEffectBridge();
            _mechanicMotionEffectBridge = new CharacterTestMechanicMotionEffectBridge();
            _motionSystem = new CharacterTestCharacterMotionSystem();
            _motionSystem.ConfigurePositionRecall(entity => new TacticalCard.CharacterTestTacticalPointNavigation(this, entity, null));
            _physicalAudioSystem = new CharacterTestPhysicalAudioSystem(
                _audioCuePlaybackSystem,
                _audioIntentQueue,
                _acousticBodyPropertyProvider);
            _physicalAudioSystem.SetActionRuntime(_actionSystem.Runtime);
            _physicalAudioSystem.SetPresentationPoseProvider(
                new CharacterTestBodyPartContactUnityPoseProvider(_viewBridge));
            _world.EntitySpawned += PreparePhysicalAudioForSpawnedEntity;
            _world.EntitySpawned += CharacterTestDefaultBakedWeaponTraceSource.PrepareSpawnedEntity;
            _world.EntitySpawned += _authorityPoseTerrainSystem.Register;
            _world.EntityRemoved += RemovePhysicalAudioForRemovedEntity;
            _world.EntityRemoved += _authorityPoseTerrainSystem.Remove;
            _world.EntityRemoved += RemoveHurtboxForRemovedEntity;
            _world.EntitiesCleared += ClearPhysicalAudioRegistrations;
            _world.EntitiesCleared += _authorityPoseTerrainSystem.Clear;
            _world.EntitiesCleared += ClearHurtboxesForClearedWorld;
            _combatTraceSystem = new CharacterTestCharacterCombatTraceSystem(safeLogger);
            _weaponHitAudioSystem = new CharacterTestWeaponHitAudioSystem(
                _audioCuePlaybackSystem,
                _audioIntentQueue,
                _acousticBodyPropertyProvider);
            PerceptionFrameCache = new CharacterTestPerceptionFrameCache(this);
            _combatTraceSystem.SetPerceptionFrameCache(PerceptionFrameCache);
            _interactionPhysicsSystem = new CharacterTestInteractionPhysicsSystem();
            _interactionVfxSystem = new CharacterTestInteractionVfxSystem(interactionVfxRuntime);
            _interactionAudioSystem = new CharacterTestInteractionAudioSystem(_audioCuePlaybackSystem, _audioIntentQueue);
            _interactionRenderingSystem = new CharacterTestInteractionRenderingSystem();
            _interactionConsumerDispatcher = new CharacterTestInteractionConsumerDispatcher(
                _interactionPhysicsSystem,
                _interactionVfxSystem,
                _interactionAudioSystem,
                _interactionRenderingSystem);
            _interactionPerformanceCounterSource =
                new CharacterTestInteractionPerformanceCounterSource(this);
            _runtimePerformanceCounterSource =
                new CharacterTestRuntimePerformanceCounterSource(this);
            _animationSystem = new CharacterTestCharacterAnimationSystem();
            _actionSystem.SetTraceRecorder(_traceRecorder);
            _rootMotionSystem.SetTraceRecorder(_traceRecorder);
            _motionSystem.SetTraceRecorder(_traceRecorder);
            _combatTraceSystem.SetTraceRecorder(_traceRecorder);
            _combatTraceSystem.SetAbilityDefinitions(_abilityRuntimeModule?.Definitions);
            _animationSystem.SetTraceRecorder(_traceRecorder);
            RefreshMovementProfile();
            _motionSystem.SetPhysicsRuntime(physicsRuntime);
            _combatTraceSystem.SetPhysicsRuntime(physicsRuntime);
            _viewSyncSystem = new CharacterTestCharacterViewSyncSystem(
                _viewBridge,
                _buffDefinitions,
                _buffPresentationDefinitions);
            _animationSyncSystem = new CharacterTestCharacterAnimationSyncSystem(_viewBridge);
        }

        public CharacterTestCharacterRuntimeWorld World => _world;
        public IReadOnlyList<CharacterTestCharacterEntity> Entities => _world.Entities;
        public IReadOnlyList<CharacterTestMovableNaturalObjectState> MovableNaturalObjects =>
            _world.MovableNaturalObjects;

        /// <summary>
        /// Resolves the conservative capsule half-extents supported by the session navigation
        /// graph. The graph is shared by every playable definition, so its build probe must fit
        /// the largest registered character rather than only the scene physics fallback shape.
        /// </summary>
        /// <remarks>LowFreqNoAlloc. Call during slice composition before runtime ticks begin.</remarks>
        internal FixVector3 ResolveMaximumNavigationAgentHalfExtents()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime = PhysicsRuntime;
            if (physicsRuntime == null)
                return FixVector3.Zero;

            FixVector3 maximum = physicsRuntime.CharacterHalfExtents;
            IncludeNavigationHalfExtents(
                physicsRuntime.GetCharacterHalfExtents(CharacterTestCharacterPhysicsComponent.Default),
                ref maximum);
            IReadOnlyList<CharacterTestCharacterDefinition> definitions = _definitions.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                IncludeNavigationHalfExtents(
                    physicsRuntime.GetCharacterHalfExtents(definitions[i].PhysicsProfile),
                    ref maximum);
            }

            return maximum;
        }

        private static void IncludeNavigationHalfExtents(
            FixVector3 candidate,
            ref FixVector3 maximum)
        {
            maximum = new FixVector3(
                candidate.X > maximum.X ? candidate.X : maximum.X,
                candidate.Y > maximum.Y ? candidate.Y : maximum.Y,
                candidate.Z > maximum.Z ? candidate.Z : maximum.Z);
        }

        public GameplayBuffDefinitionRegistry BuffDefinitions => _buffDefinitions;
        public GameplayBuffPresentationDefinitionRegistry BuffPresentationDefinitions =>
            _buffPresentationDefinitions;
        public CharacterTestCharacterActionRuntime ActionRuntime => _actionSystem.Runtime;

        /// <remarks>LowFreqAlloc. Measurement bootstrap only; never changes live gameplay or completed-frame state.</remarks>
        internal void ConfigureReferenceDummyForMeasurement(int entityId, CharacterActionReferenceDummy dummy)
        {
            if (_createViews || _hasCompletedRuntimeFrame)
                throw new InvalidOperationException("Reference dummy geometry is restricted to headless session bootstrap.");
            _motionSystem.ConfigureReferenceDummy(entityId, dummy);
        }

        /// <summary>Returns the last frame whose complete Simulation tick has finished.</summary>
        /// <remarks>NoAlloc. PreSimulation callers must use this authority boundary, never the current frame.</remarks>
        internal bool TryGetLastCompletedRuntimeFrame(out RuntimeFrame frame)
        {
            frame = _lastRuntimeFrame;
            return _hasCompletedRuntimeFrame;
        }

        internal bool TryGetDamageGeometrySnapshot(
            int entityId,
            out CharacterTestDamageGeometrySnapshot snapshot)
        {
            return _motionSystem.TryGetDamageGeometrySnapshot(entityId, out snapshot);
        }

        internal int GetHurtboxRevision(int entityId)
        {
            return _motionSystem.GetHurtboxRevision(entityId);
        }

        internal bool TryGetDamageZoneSnapshot(
            int entityId,
            out CharacterTestDamageZoneSnapshot snapshot)
        {
            return _motionSystem.TryGetDamageZoneSnapshot(entityId, out snapshot);
        }

        internal bool TryGetRuntimeGeometryBinding(
            int entityId,
            out CharacterImportedGeometryBinding binding)
        {
            return _motionSystem.TryGetRuntimeGeometryBinding(entityId, out binding);
        }

        /// <remarks>NoAlloc. Read-only Presence contact counters and typed status.</remarks>
        internal CharacterTestBodyPresenceContactDiagnostics CaptureBodyPresenceContactDiagnostics()
        {
            return _motionSystem.CaptureBodyPresenceContactDiagnostics();
        }

        /// <remarks>NoAlloc. Reads the existing stable-sorted Presence constraint buffer.</remarks>
        internal bool TryGetBodyPresenceConstraintDiagnostic(
            int index,
            out CharacterTestBodyPresenceConstraintDiagnostic diagnostic)
        {
            return _motionSystem.TryGetBodyPresenceConstraintDiagnostic(index, out diagnostic);
        }

        /// <remarks>NoAlloc. Read-only Presence compound projection counters.</remarks>
        internal CharacterTestBodyPresenceSyncDiagnostics CaptureBodyPresenceSyncDiagnostics()
        {
            return _motionSystem.CaptureBodyPresenceSyncDiagnostics();
        }

        internal void SetRuntimeAiPlannerDodgeOutcomeSink(
            ICharacterTestRuntimeAiPlannerDodgeOutcomeSink sink)
        {
            _actionSystem.SetRuntimeAiPlannerDodgeOutcomeSink(sink);
        }

        /// <summary>
        /// Installs the authoritative PrimaryAttack eligibility guard. The
        /// callback is deliberately injected at the runtime boundary so the
        /// action gateway remains the final submit point while the M1 service
        /// stays independent of CharacterTest orchestration.
        /// </summary>
        internal void SetPrimaryAttackOpportunityGuard(
            Func<CharacterTestCharacterEntity, RuntimeFrame, bool> guard)
        {
            _primaryAttackOpportunityGuard = guard;
            _actionSystem.SetPrimaryAttackOpportunityGuard(guard);
        }

        internal bool CanSubmitPrimaryAttack(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame)
        {
            // TurnInPlace is an explicit no-attack control semantic. Keep this
            // check at the action submit boundary as well as the input-driven
            // action path, so a same-frame CharacterAction BasicAttack request
            // cannot bypass the turn command.
            if (entity != null && entity.Input.TurnInPlace)
                return false;
            if (_primaryAttackOpportunityGuard == null)
                return true;
            if (entity == null)
                return false;
            if (entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled)
                return true;
            return _primaryAttackOpportunityGuard(entity, frame);
        }
        public CharacterTestCharacterActionReloadResult? LastActionReloadResult =>
            _actionSystem.LastReloadResult;
        public CharacterTestCharacterMovementProfileRuntime MovementProfile => _motionSystem.MovementProfile;
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime => _motionSystem.PhysicsRuntime;

        /// <summary>
        /// Reads the latest same-frame motion diagnostic for one character.
        /// </summary>
        /// <remarks>NoAlloc. The result is transient and is not authority snapshot state.</remarks>
        public bool TryGetCharacterMotionDiagnostic(
            int entityId,
            out CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            return _motionSystem.TryGetCharacterMotionDiagnostic(entityId, out diagnostic);
        }

        public CharacterTestCharacterCombatTraceSystem CombatTraceSystem => _combatTraceSystem;
        public CharacterTestInteractionHistory InteractionHistory => _combatTraceSystem.InteractionHistory;
        public CharacterTestInteractionDebugSnapshot InteractionDebugSnapshot =>
            _combatTraceSystem.InteractionDebugSnapshot;
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameInteractionEvents =>
            _combatTraceSystem.LastFrameInteractionEvents;
        public CharacterTestInteractionPhysicsSummary InteractionPhysicsSummary =>
            _interactionPhysicsSystem.LastSummary;
        public CharacterTestInteractionVfxSummary InteractionVfxSummary =>
            _interactionVfxSystem.LastSummary;
        public CharacterTestInteractionAudioSummary InteractionAudioSummary =>
            _interactionAudioSystem.LastSummary;
        public CharacterTestPhysicalAudioSummary PhysicalAudioSummary =>
            _physicalAudioSystem.LastSummary;
        public ContinuousPhysicalAudioSnapshot ContinuousPhysicalAudioSnapshot =>
            _physicalAudioSystem.ContinuousAudioSnapshot;
        public CharacterTestWeaponHitAudioSummary WeaponHitAudioSummary =>
            _weaponHitAudioSystem.LastSummary;
        public CharacterTestAudioIntentDrainSummary AudioIntentDrainSummary =>
            _audioIntentDrainSystem.LastSummary;
        internal AudioPresentationDrain AudioPresentationDrain =>
            _audioIntentDrainSystem.PresentationDrain;
        public CharacterTestAirDisplacementAudioSummary AirDisplacementAudioSummary =>
            _airDisplacementAudioSystem.LastSummary;
        internal CharacterTestBodyPartContactDebugSnapshot BodyPartContactDebugSnapshot =>
            _physicalAudioSystem.BodyPartContactDebugSnapshot;
        public CharacterTestInteractionRenderingSummary InteractionRenderingSummary =>
            _interactionRenderingSystem.LastSummary;
        public CharacterTestInteractionConsumerDispatchSummary InteractionConsumerSummary =>
            _interactionConsumerDispatcher.LastSummary;
        public CharacterTestInteractionPerformanceCounterSource InteractionPerformanceCounterSource =>
            _interactionPerformanceCounterSource;
        internal CharacterTestRuntimePerformanceCounterSource RuntimePerformanceCounterSource =>
            _runtimePerformanceCounterSource;
        internal CharacterTestPerceptionFrameCache PerceptionFrameCache { get; }
        internal CharacterTestCharacterSpawnPerformanceSummary SpawnPerformanceSummary =>
            new CharacterTestCharacterSpawnPerformanceSummary(
                _spawnSystem.PendingRequestCount,
                _spawnSystem.LastProcessedRequestCount,
                _spawnSystem.LastSpawnedCount,
                _spawnSystem.LastFailedCount,
                _spawnSystem.TotalSpawnedCount,
                _spawnSystem.TotalFailedCount);
        internal CharacterTestCharacterViewSyncPerformanceSummary ViewSyncPerformanceSummary =>
            new CharacterTestCharacterViewSyncPerformanceSummary(
                _viewBridge.ActiveViewCount,
                _viewBridge.LastSyncRequestCount,
                _viewBridge.LastSyncAppliedCount,
                _viewBridge.LastSyncMissCount,
                _viewBridge.LastTransformApplyCount,
                _viewBridge.TotalSyncRequestCount,
                _viewBridge.TotalSyncAppliedCount,
                _viewBridge.TotalSyncMissCount,
                _viewBridge.TotalTransformApplyCount);
        public CharacterTestCharacterUnityViewBridge ViewBridge => _viewBridge;
        internal bool TryGetCharacterDefinition(int characterId, out CharacterTestCharacterDefinition definition) =>
            _definitions.TryGet(characterId, out definition);
        public CharacterTestTraceRecorder TraceRecorder => _traceRecorder;
        public CharacterTestHeadPadDisplayMode HeadPadDisplayMode => _headPadDisplayMode;
        internal int LastAreaConfinementMotionLockCount =>
            _mechanicMotionEffectBridge.LastLockedCount;
        internal int LastStasisActionFrozenCount =>
            _statusControlEffectBridge.LastActionFrozenCount;
        internal string LastGenesisMovableObjectSummary { get; private set; } = string.Empty;
        internal CharacterTestGenesisMovableNaturalObjectValidation LastGenesisMovableObjectValidation =>
            _lastGenesisMovableObjectValidation;
        internal string LastGenesisPotencyMechanicsSummary { get; private set; } = string.Empty;
        internal CharacterTestGenesisPotencyMechanicsValidation LastGenesisPotencyMechanicsValidation =>
            _lastGenesisPotencyMechanicsValidation;
        internal string LastGenesisLivingBeingSummary { get; private set; } = string.Empty;
        internal CharacterTestGenesisLivingBeingValidation LastGenesisLivingBeingValidation =>
            _lastGenesisLivingBeingValidation;
        internal string LastGenesisLivingNaturalAttributesSummary { get; private set; } = string.Empty;
        internal CharacterTestGenesisLivingNaturalAttributesValidation LastGenesisLivingNaturalAttributesValidation =>
            _lastGenesisLivingNaturalAttributesValidation;
        internal string LastGenesisLivingNaturalAttributesRuntimeStateSummary { get; private set; } = string.Empty;
        public CharacterTestMovableNaturalObjectSummary MovableNaturalObjectSummary =>
            CharacterTestMovableNaturalObjectSystem.CaptureSummary(_world.MovableNaturalObjects);

        public CharacterTestCharacterActionReloadResult ReloadActionConfig(RuntimeFrame frame)
        {
            return ReloadActionConfig(frame, null);
        }

        public CharacterTestCharacterActionReloadResult ReloadActionConfig(
            RuntimeFrame frame,
            CharacterTestCharacterActionConfigSourceOptions options)
        {
            CharacterTestCharacterActionReloadResult result = options == null
                ? _actionSystem.ReloadActionConfig(_world, frame)
                : _actionSystem.ReloadActionConfig(_world, frame, options);
            if (result.Success)
            {
                RefreshMovementProfile();
                _motionSystem.SuppressLandingEdgesForNextTick();
            }

            return result;
        }

        public void SetMovementBasisProvider(ICharacterTestCharacterMovementBasisProvider provider)
        {
            _motionSystem.SetMovementBasisProvider(provider);
        }

        public void SetLockedTargetEntityIdProvider(Func<int> provider)
        {
            _lockedTargetEntityIdProvider = provider;
            _combatTraceSystem.SetLockedTargetEntityIdProvider(provider);
        }

        internal void SetLockedCombatTargetProvider(
            Func<CharacterTestCombatTargetHandle> provider)
        {
            _combatTraceSystem.SetLockedCombatTargetProvider(provider);
        }

        public void SetInteractionVfxRuntime(VfxRuntimeModule vfxRuntime)
        {
            _interactionVfxSystem.SetVfxRuntime(vfxRuntime);
        }

        public void SetInteractionRenderingPublisher(
            IRenderDataPublisher publisher,
            IRenderSubjectMap<int> sourceSubjects)
        {
            _interactionRenderingSystem.SetPublisher(publisher, sourceSubjects);
        }

        public FrameworkPerformanceCounterSnapshot CreateInteractionPerformanceCounterSnapshot()
        {
            return _interactionPerformanceCounterSource.Capture();
        }

        internal FrameworkPerformanceCounterSnapshot CreateRuntimePerformanceCounterSnapshot()
        {
            return _runtimePerformanceCounterSource.Capture();
        }

        public void SetHeadPadDisplayMode(CharacterTestHeadPadDisplayMode displayMode)
        {
            _headPadDisplayMode = CharacterTestHeadPadDisplayModes.Normalize(displayMode);
        }

        internal void SetPresentationAllyTeamId(int teamId)
        {
            _presentationAllyTeamId = teamId > 0 ? teamId : 0;
        }

        internal void SetForceHeadPadsVisible(bool forceVisible)
        {
            _forceHeadPadsVisible = forceVisible;
        }

        public bool EnqueueSpawn(CharacterTestCharacterSpawnRequest request)
        {
            return _spawnSystem.Enqueue(request);
        }

        public bool TryApplyControlIntent(CharacterTestControlIntent intent, out string message)
        {
            return TryApplyControlIntent(RuntimeFrame.Zero, intent, 0, out message);
        }

        public bool TryApplyControlIntent(RuntimeFrame frame, CharacterTestControlIntent intent, out string message)
        {
            return TryApplyControlIntent(frame, intent, 0, out message);
        }

        internal bool TryApplyControlIntent(
            RuntimeFrame frame,
            CharacterTestControlIntent intent,
            int controlIntentSourceId,
            out string message)
        {
            CharacterTestCharacterEntity entity = FindEntity(intent.TargetEntityId);
            if (entity == null)
            {
                message = "controlled entity was not found";
                return false;
            }

            if (!CanApplyControlIntent(entity.Control.Owner))
            {
                message = "controlled entity is not command-driven";
                return false;
            }

            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            CharacterTestBattleStateComponent battleState = entity.BattleState;
            if (!battleState.CanMoveByInput && !battleState.CanStartNormalAction)
            {
                message = "controlled entity cannot receive control intent";
                return false;
            }

            CharacterTestCharacterInputComponent existingInput = entity.Input;
            entity.SetInput(CharacterTestCharacterInputComponent.FromDeterministicWithSkillSlotMask(
                intent.MoveXMilli,
                intent.MoveYMilli,
                intent.WorldMoveXMilli,
                intent.WorldMoveYMilli,
                hasWorldMove: true,
                intent.JumpPressed,
                intent.JumpHeld,
                intent.JumpReleased,
                intent.AttackPrimaryPressed,
                intent.AttackSecondaryPressed,
                intent.DebugSecondaryPressed,
                intent.DebugPrimaryPressed,
                intent.SkillSlotPressedMask,
                intent.DodgePressed,
                intent.MovementSpeedScaleMilli,
                intent.TurnInPlace,
                intent.TurnInPlaceYawMilliDegrees,
                controlIntentSourceId)
                .WithAbilitySlotCast(
                    existingInput.AbilitySlotPressed,
                    existingInput.AbilityIdPressed));
            AddGameplayIntentBufferEntries(entity, frame, intent);
            LogControlIntentWrite(intent, entity);
            message = string.Empty;
            return true;
        }

        public bool TryApplyAbilitySlotCast(
            int targetEntityId,
            int slotNumber,
            int abilityId,
            out string message)
        {
            return TryApplyAbilitySlotCast(RuntimeFrame.Zero, targetEntityId, slotNumber, abilityId, out message);
        }

        public bool TryApplyAbilitySlotCast(
            RuntimeFrame frame,
            int targetEntityId,
            int slotNumber,
            int abilityId,
            out string message)
        {
            CharacterTestCharacterEntity entity = FindEntity(targetEntityId);
            if (entity == null)
            {
                message = "controlled entity was not found";
                return false;
            }

            if (!CanApplyControlIntent(entity.Control.Owner))
            {
                message = "controlled entity is not command-driven";
                return false;
            }

            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            CharacterTestBattleStateComponent battleState = entity.BattleState;
            if (!battleState.CanStartNormalAction)
            {
                message = "controlled entity cannot start normal action";
                return false;
            }

            if (!CharacterTestAbilityHotbarRuntime.IsValidSlot(slotNumber))
            {
                message = "ability slot is invalid";
                return false;
            }

            if (abilityId <= 0)
            {
                message = "ability id is invalid";
                return false;
            }

            entity.SetInput(entity.Input.WithAbilitySlotCast(slotNumber, abilityId));
            entity.GameplayIntentBuffer.AddOrRefresh(
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                targetEntityId,
                frame.Value,
                abilitySlotPressed: slotNumber,
                abilityIdPressed: abilityId);
            message = string.Empty;
            return true;
        }

        private bool TryApplyBehaviorRecipeOrchestrationCore(
            RuntimeFrame frame,
            int targetEntityId,
            CharacterTestBehaviorRecipeOrchestrationOperation operation,
            int resourceId,
            RuntimeTraceIdentity traceId,
            out string message,
            out CharacterTestBehaviorOrchestrationReason reasonCode,
            out CharacterTestActionUseFailure failureDetails)
        {
            reasonCode = CharacterTestBehaviorOrchestrationReason.None;
            failureDetails = default;
            CharacterTestCharacterEntity entity = FindEntity(targetEntityId);
            if (entity == null)
            {
                reasonCode = CharacterTestBehaviorOrchestrationReason.EntityMissing;
                message = "BehaviorRecipe.EntityMissing";
                return false;
            }

            CharacterTestCharacterActionRuntime actionRuntime = _actionSystem?.Runtime;
            if (actionRuntime == null)
            {
                reasonCode = CharacterTestBehaviorOrchestrationReason.CharacterActionRuntimeMissing;
                message = "BehaviorRecipe.CharacterActionRuntimeMissing";
                return false;
            }
            var entityId = new CharacterTestCharacterEntityId(targetEntityId);
            switch (operation)
            {
                case CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction:
                    if (resourceId <= 0
                        || !actionRuntime.Registry.TryGetAction(new CharacterActionId(resourceId), out CharacterActionConfig action))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.CharacterActionUnknown;
                        message = "BehaviorRecipe.CharacterActionUnknown";
                        return false;
                    }
                    if (!CanCommitBehaviorAction(frame, targetEntityId, resourceId))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.CharacterActionCommitRejected;
                        failureDetails = new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.Qualification,
                            CharacterTestActionUseStatus.TargetUnavailable);
                        message = "BehaviorRecipe.ActionConditionChanged"; return false;
                    }
                    var usageCommit = _actionUseAuthority.TryCommit(entity, action, frame, ResolveBehaviorActionFacts(frame, targetEntityId, resourceId));
                    reasonCode = usageCommit.Committed ? CharacterTestBehaviorOrchestrationReason.CharacterActionReplaced : CharacterTestBehaviorOrchestrationReason.CharacterActionCommitRejected;
                    failureDetails = usageCommit.Failure;
                    message = usageCommit.Committed ? "BehaviorRecipe.CharacterActionReplaced" : usageCommit.Reason;
                    return usageCommit.Committed;

                case CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility:
                    if (_abilityRuntimeModule == null)
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.AbilityRuntimeMissing;
                        message = "BehaviorRecipe.AbilityRuntimeMissing";
                        return false;
                    }
                    if (!_abilityRuntimeModule.Definitions.TryGet(resourceId, out AbilityDefinitionConfig ability))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.AbilityUnknown;
                        message = "BehaviorRecipe.AbilityUnknown";
                        return false;
                    }
                    CharacterActionId boundActionId = ability.ActionBinding?.ActionId ?? CharacterActionId.None;
                    if (!boundActionId.IsValid
                        || !actionRuntime.Registry.TryGetAction(boundActionId, out var boundAction))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.AbilityActionUnknown;
                        message = "BehaviorRecipe.AbilityActionUnknown";
                        return false;
                    }
                    var abilityCommit = _actionUseAuthority.TryCommit(entity, boundAction, frame,
                        ResolveBehaviorActionFacts(frame, targetEntityId, resourceId), ability);
                    reasonCode = abilityCommit.Committed ? CharacterTestBehaviorOrchestrationReason.AbilityReplaced : CharacterTestBehaviorOrchestrationReason.AbilityCommitRejected;
                    failureDetails = abilityCommit.Failure;
                    message = abilityCommit.Committed ? "BehaviorRecipe.AbilityReplaced" : abilityCommit.Reason;
                    return abilityCommit.Committed;

                case CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForTimelineJump:
                    if (!actionRuntime.TerminateForOrchestration(
                            entityId,
                            frame,
                            CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump,
                            traceId))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.TimelineJumpTerminateRejected;
                        message = "BehaviorRecipe.TimelineJumpTerminateRejected";
                        return false;
                    }
                    reasonCode = CharacterTestBehaviorOrchestrationReason.TimelineJumpTerminated;
                    message = "BehaviorRecipe.TimelineJumpTerminated";
                    return true;

                case CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner:
                    if (!actionRuntime.TerminateForOrchestration(
                            entityId,
                            frame,
                            CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner,
                            traceId))
                    {
                        reasonCode = CharacterTestBehaviorOrchestrationReason.ReturnToPlannerTerminateRejected;
                        message = "BehaviorRecipe.ReturnToPlannerTerminateRejected";
                        return false;
                    }
                    reasonCode = CharacterTestBehaviorOrchestrationReason.ReturnToPlannerTerminated;
                    message = "BehaviorRecipe.ReturnToPlannerTerminated";
                    return true;

                default:
                    reasonCode = CharacterTestBehaviorOrchestrationReason.OrchestrationOperationInvalid;
                    message = "BehaviorRecipe.OrchestrationOperationInvalid";
                    return false;
            }
        }

        public bool TryReviveCharacter(
            CharacterTestReviveRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion)
            {
                message = "revive request schema is invalid";
                return false;
            }

            CharacterTestCharacterEntity entity = FindEntity(request.TargetEntityId);
            if (entity == null)
            {
                message = "revive target was not found";
                return false;
            }

            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            if (!entity.Presence.IsActive)
            {
                message = "revive target is pending destroy";
                return false;
            }

            if (entity.BattleState.LifeState != CharacterTestCharacterLifeState.Dead)
            {
                message = "revive target is not dead";
                return false;
            }

            var recall = entity.PositionRecall; recall.ClearRecord(); entity.SetPositionRecall(recall);
            entity.InteractionEffects.Clear();
            int reviveHp = ResolveReviveHealth(entity, request.ReviveHp);
            _world.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, reviveHp);
            _world.ClearScatterPressure(entity, frame.Value);
            _world.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);
            entity.BeginRevive(frame.Value, reviveHp);
            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);

            CharacterReactionContextBuildResult reactionContext =
                CharacterReactionContextBuilder.FromLifecycle(
                    frame,
                    entity.GameplayEntityId,
                    "Revive",
                    isDeath: false,
                    reason: "revive",
                    traceId: RuntimeTraceIdentity.None);
            bool startedReaction = _actionSystem.Runtime != null
                && _actionSystem.Runtime.TryReact(
                    entity.Id,
                    reactionContext.Context,
                    allowFallback: false);
            if (!startedReaction)
            {
                _actionSystem.Runtime?.TerminateAndReleaseEntityAction(
                    entity,
                    frame,
                    CharacterActionLifecycleReason.ReviveReset,
                    new RuntimeTraceIdentity(ReviveResetTraceKind, entity.Id.Value, frame.Value));
                entity.CompleteRevive(frame.Value);
                _world.SetGameplayLifecycle(entity, GameplayLifecycleState.Alive);
                CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
                message = "revived without lifecycle action";
                return true;
            }

            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            message = "revive action started";
            return true;
        }

        public bool TryDestroyCharacter(
            CharacterTestDestroyCharacterRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion)
            {
                message = "destroy request schema is invalid";
                return false;
            }

            CharacterTestCharacterEntity entity = FindEntity(request.TargetEntityId);
            if (entity == null)
            {
                message = "destroy target was not found";
                _traceRecorder?.Lifecycle(
                    frame,
                    request.TargetEntityId,
                    "destroy rejected reason=TargetNotFound trace=" + request.TraceId,
                    warning: true);
                return false;
            }

            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            CharacterTestEntityPresenceComponent presence = entity.Presence;
            if (!presence.IsActive)
            {
                message = "destroy target is already pending destroy";
                _traceRecorder?.Lifecycle(
                    frame,
                    entity.Id.Value,
                    "destroy rejected reason=PresenceBlocked presence=" + presence.State
                    + " trace=" + request.TraceId,
                    warning: true);
                return false;
            }

            if (entity.BattleState.LifeState != CharacterTestCharacterLifeState.Dead)
            {
                message = "destroy target must be dead";
                _traceRecorder?.Lifecycle(
                    frame,
                    entity.Id.Value,
                    "destroy rejected reason=LifeStateBlocked lifeState="
                    + entity.BattleState.LifeState
                    + " trace=" + request.TraceId,
                    warning: true);
                return false;
            }

            entity.SetPresence(presence.WithPendingDestroy(
                frame.Value,
                request.SourceEntityId,
                request.TraceId));
            CharacterTestBattleStateSystem.RefreshEntity(_world, entity, frame, _statusControlEffectBridge);
            _traceRecorder?.Lifecycle(
                frame,
                entity.Id.Value,
                "destroy requested sourceEntity="
                + request.SourceEntityId
                + " trace=" + request.TraceId);
            message = "destroy queued";
            return true;
        }

        private static int ResolveReviveHealth(CharacterTestCharacterEntity entity, int requestedReviveHp)
        {
            int maxHealth = entity != null && entity.CombatState.MaxHealth > 0
                ? entity.CombatState.MaxHealth
                : CharacterTestCharacterAttributeIds.DefaultMaxHp;
            if (requestedReviveHp > 0)
                return requestedReviveHp > maxHealth ? maxHealth : requestedReviveHp;

            int defaultHp = (int)Math.Ceiling(maxHealth * 0.3d);
            if (defaultHp < 1)
                return 1;
            return defaultHp > maxHealth ? maxHealth : defaultHp;
        }

        public bool TrySetControlOwner(int entityId, CharacterTestCharacterControlOwner owner)
        {
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            if (entity == null)
                return false;

            entity.SetControl(entity.Control.WithOwner(owner));
            return true;
        }

        internal void SetRuntimeAiBehaviorLoadouts(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime)
        {
            _runtimeAiBehaviorLoadouts = runtime;
        }

        internal bool TryApplyRuntimeAiBehaviorSelection(
            int entityId,
            int loadoutId,
            int variantId,
            out string message)
        {
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            if (entity == null)
            {
                message = "controlled entity was not found";
                return false;
            }

            if (!CanApplyControlIntent(entity.Control.Owner))
            {
                message = "controlled entity is not command-driven";
                return false;
            }

            if (_runtimeAiBehaviorLoadouts == null)
            {
                message = "Runtime AI Planner behavior loadout runtime is unavailable.";
                return false;
            }

            if (!_runtimeAiBehaviorLoadouts.TryResolveSelection(
                    loadoutId,
                    variantId,
                    out CharacterTestRuntimeAiBehaviorSelection selection,
                    out message))
            {
                return false;
            }

            entity.SetControl(entity.Control.WithRuntimeAiBehaviorSelection(selection));
            message = string.Empty;
            return true;
        }

        public bool TryApplyAuthorityPosePreview(
            CharacterTestAuthorityPosePreviewRequest request,
            RuntimeFrame frame,
            out string message)
        {
            message = string.Empty;
            if (request.SchemaVersionValue != CharacterTestAuthorityPosePreviewRequest.SchemaVersion)
            {
                message = "authority pose preview schema is invalid";
                return false;
            }

            CharacterTestCharacterEntity target = FindEntity(request.TargetEntityId);
            if (target == null)
            {
                message = "authority pose preview target entity was not found";
                return false;
            }

            if (!CharacterTestAuthorityPosePreviewMapper.TryCreateRequest(
                    request.EffectId,
                    target,
                    request.EffectId.ToString(),
                    out CharacterAuthorityPoseImpulseRequest poseRequest))
            {
                message = "authority pose preview request was invalid";
                return false;
            }

            return TryApplyAuthorityPoseImpulse(
                target.Id.Value,
                poseRequest,
                frame.Next(),
                out message);
        }

        public bool TryApplyAuthorityPoseImpulse(
            int targetEntityId,
            CharacterAuthorityPoseImpulseRequest request,
            RuntimeFrame startFrame,
            out string issue)
        {
            issue = string.Empty;
            CharacterTestCharacterEntity entity = FindEntity(targetEntityId);
            if (entity == null)
            {
                issue = "authority pose target entity was not found";
                return false;
            }

            if (entity.AuthorityPose == null)
                entity.SetAuthorityPose(new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));

            return entity.ApplyAuthorityPoseImpulse(request, startFrame, out issue);
        }

        public bool TryApplyAuthorityPoseImpulse(
            int targetEntityId,
            CharacterAuthorityPoseImpulseRequest request,
            RuntimeFrame startFrame,
            out CharacterAuthorityPoseSnapshot snapshot,
            out string issue)
        {
            snapshot = CharacterAuthorityPoseSnapshot.Empty();
            issue = string.Empty;
            CharacterTestCharacterEntity entity = FindEntity(targetEntityId);
            if (entity == null)
            {
                issue = "authority pose target entity was not found";
                snapshot = CharacterAuthorityPoseSnapshot.Empty(CharacterAuthorityPoseIssue.TargetEntityNotFound);
                return false;
            }

            if (entity.AuthorityPose == null)
                entity.SetAuthorityPose(new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));

            bool accepted = entity.ApplyAuthorityPoseImpulse(request, startFrame, out issue);
            snapshot = entity.AuthorityPose.CreateSnapshot();
            return accepted;
        }

        public bool TryGetAuthorityPoseSnapshot(
            int entityId,
            out CharacterAuthorityPoseSnapshot snapshot)
        {
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            if (entity != null && entity.AuthorityPose != null)
            {
                snapshot = entity.AuthorityPose.CreateSnapshot();
                return true;
            }

            snapshot = CharacterAuthorityPoseSnapshot.Empty(CharacterAuthorityPoseIssue.StateUnavailable);
            return false;
        }

        public bool TryGetAuthorityPoseSocketOffset(
            int entityId,
            int socketId,
            out FixVector3 offset)
        {
            offset = FixVector3.Zero;
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            return entity?.AuthorityPose != null
                && entity.AuthorityPose.TryGetSocketOffset(socketId, out offset);
        }

        public bool TryApplySurfaceProbe(
            CharacterTestSurfaceProbeRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.SurfaceProbeSchemaVersion)
            {
                message = "surface probe schema is invalid";
                return false;
            }

            CharacterTestCharacterEntity target = FindEntity(request.TargetEntityId);
            if (target == null)
            {
                message = "surface probe target entity was not found";
                return false;
            }

            switch (request.Operation)
            {
                case CharacterTestSurfaceProbeOperation.ApplyImpulse:
                    ApplySurfaceProbeMotion(
                        target,
                        ResolveForward(target) * 8.5f,
                        CharacterMovementMode.Run,
                        CharacterTestAuthoredContentIds.D401_Id131);
                    message = "surface probe impulse applied. target=" + target.Id.Value;
                    return true;
                case CharacterTestSurfaceProbeOperation.ApplyHit:
                    ApplySurfaceProbeMotion(
                        target,
                        -ResolveForward(target) * 5.5f,
                        CharacterMovementMode.ControlLocked,
                        CharacterTestAuthoredContentIds.D401_Id132);
                    target.SetCombatState(target.CombatState.ApplyHitReaction(
                        damage: 0,
                        staggerFrames: 18,
                        frame: frame.Value,
                        sourceEntityId: 0,
                        traceId: CharacterTestAuthoredContentIds.D401_Id132,
                        maxHealth: target.CombatState.MaxHealth,
                        health: target.CombatState.Health));
                    message = "surface probe hit applied. target=" + target.Id.Value;
                    return true;
                case CharacterTestSurfaceProbeOperation.ApplyPush:
                    ApplySurfaceProbeMotion(
                        target,
                        ResolveRight(target) * 6.5f,
                        CharacterMovementMode.Run,
                        CharacterTestAuthoredContentIds.D401_Id133);
                    message = "surface probe push applied. target=" + target.Id.Value;
                    return true;
                case CharacterTestSurfaceProbeOperation.Reset:
                    ResetSurfaceProbeTarget(target);
                    message = "surface probe reset applied. target=" + target.Id.Value;
                    return true;
                default:
                    message = "surface probe operation is invalid";
                    return false;
            }
        }

        internal bool TryRunGenesisMovableObjectProbe(
            CharacterTestGenesisMovableObjectProbeRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbeSchemaVersion)
            {
                message = "genesis movable object probe schema is invalid";
                return false;
            }

            if (request.Scenario == CharacterTestGenesisMovableObjectProbeKind.None)
            {
                message = "genesis movable object probe scenario is invalid";
                return false;
            }

            _lastGenesisMovableObjectValidation =
                CharacterTestGenesisMovableNaturalObjectHarness.RunProbe(
                    request.Scenario,
                    request.ForceOrigin,
                    frame);
            LastGenesisMovableObjectSummary = _lastGenesisMovableObjectValidation.ToSummaryLine();
            message = "genesis movable object probe applied. stateModel=harness-only scenario="
                + request.Scenario
                + " forceOrigin="
                + request.ForceOrigin;
            return true;
        }

        internal bool TryRunGenesisPotencyMechanicsProbe(
            CharacterTestGenesisPotencyMechanicsProbeRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbeSchemaVersion)
            {
                message = "genesis potency mechanics probe schema is invalid";
                return false;
            }

            if (request.Scenario == CharacterTestGenesisPotencyMechanicsProbeKind.None)
            {
                message = "genesis potency mechanics probe scenario is invalid";
                return false;
            }

            _lastGenesisPotencyMechanicsValidation =
                CharacterTestGenesisPotencyMechanicsHarness.RunProbe(request.Scenario, frame);
            LastGenesisPotencyMechanicsSummary = _lastGenesisPotencyMechanicsValidation.ToSummaryLine();
            message = "genesis potency mechanics probe applied. stateModel=harness-only scenario="
                + request.Scenario;
            return true;
        }

        internal bool TryRunGenesisLivingBeingProbe(
            CharacterTestGenesisLivingBeingProbeRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbeSchemaVersion)
            {
                message = "genesis living being probe schema is invalid";
                return false;
            }

            if (request.Scenario == CharacterTestGenesisLivingBeingProbeKind.None)
            {
                message = "genesis living being probe scenario is invalid";
                return false;
            }

            _lastGenesisLivingBeingValidation =
                CharacterTestGenesisLivingBeingHarness.RunProbe(request.Scenario, frame);
            LastGenesisLivingBeingSummary = _lastGenesisLivingBeingValidation.ToSummaryLine();
            message = "genesis living being probe applied. stateModel=harness-only scenario="
                + request.Scenario;
            return true;
        }

        internal bool TryRunGenesisLivingNaturalAttributesProbe(
            CharacterTestGenesisLivingNaturalAttributesProbeRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion !=
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbeSchemaVersion)
            {
                message = "genesis living natural attributes probe schema is invalid";
                return false;
            }

            if (request.Scenario == CharacterTestGenesisLivingNaturalAttributesProbeKind.None)
            {
                message = "genesis living natural attributes probe scenario is invalid";
                return false;
            }

            _lastGenesisLivingNaturalAttributesValidation =
                CharacterTestGenesisLivingNaturalAttributesHarness.RunProbe(request.Scenario, frame);
            LastGenesisLivingNaturalAttributesSummary =
                _lastGenesisLivingNaturalAttributesValidation.ToSummaryLine();
            message = "genesis living natural attributes probe applied. stateModel=harness-only scenario="
                + request.Scenario;
            return true;
        }

        internal bool TryApplyGenesisLivingNaturalAttributesRuntimeState(
            CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion !=
                CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeStateSchemaVersion)
            {
                message = "genesis living natural attributes runtime state schema is invalid";
                return false;
            }

            if (!CharacterTestLivingNaturalAttributesRuntimeState.IsValidOperation(request.Operation))
            {
                message = "genesis living natural attributes runtime state operation is invalid";
                return false;
            }

            if (!CharacterTestLivingNaturalAttributesRuntimeState.IsValidCommandMagnitude(request.Magnitude))
            {
                message = "genesis living natural attributes runtime state magnitude is invalid";
                return false;
            }

            CharacterTestCharacterEntity target = FindEntity(request.TargetEntityId);
            if (target == null)
            {
                message = "genesis living natural attributes runtime state target was not found: "
                    + request.TargetEntityId;
                return false;
            }

            CharacterTestLivingNaturalAttributesRuntimeState state =
                target.LivingNaturalAttributes.ApplyOperation(
                    request.Operation,
                    request.Magnitude,
                    frame);
            target.SetLivingNaturalAttributes(state);
            LastGenesisLivingNaturalAttributesRuntimeStateSummary = state.ToSummaryLine(target.Id.Value);
            message = "genesis living natural attributes runtime state applied. "
                + LastGenesisLivingNaturalAttributesRuntimeStateSummary;
            return true;
        }

        public bool TryResetMovableNaturalObjects(int targetNumericId, out string message)
        {
            int resetCount = _world.ResetMovableNaturalObjects(targetNumericId);
            if (resetCount <= 0)
            {
                message = targetNumericId <= 0
                    ? "no movable natural objects are registered"
                    : "movable natural object target was not found: " + targetNumericId;
                return false;
            }

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                _world.MovableNaturalObjects,
                PhysicsRuntime);
            message = targetNumericId <= 0
                ? "movable natural object reset applied. count=" + resetCount
                : "movable natural object reset applied. target=" + targetNumericId;
            return true;
        }

        public bool TryApplyMovableNaturalObjectPotencyInfluence(
            CharacterTestMovableNaturalObjectPotencyInfluenceRequest request,
            RuntimeFrame frame,
            out string message)
        {
            if (request.SchemaVersion !=
                CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluenceSchemaVersion)
            {
                message = "movable natural object potency influence schema is invalid";
                return false;
            }

            if (!_world.ApplyMovableNaturalObjectPotencyInfluence(
                    request.TargetNumericId,
                    request.InfluenceKind,
                    frame,
                    out _,
                    out message))
            {
                return false;
            }

            return true;
        }

        private static void ApplySurfaceProbeMotion(
            CharacterTestCharacterEntity target,
            Vector3 impulse,
            CharacterMovementMode movementMode,
            int stableEventId)
        {
            target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.Impulse(
                impulse,
                movementMode,
                CharacterActionId.None,
                stableEventId));
        }

        private static void ResetSurfaceProbeTarget(CharacterTestCharacterEntity target)
        {
            target.ClearMotionRequests();
            target.SetInput(CharacterTestCharacterInputComponent.None);
            target.SetTransform(new CharacterTestCharacterTransformComponent(
                -8f,
                0.04f,
                0f,
                0f));
            target.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Idle,
                physicsEnabled: target.PhysicsEnabled,
                collisionFlags: target.PhysicsEnabled
                    ? MxFramework.Combat.Motion.CombatMotionCollisionFlags.Grounded
                    : MxFramework.Combat.Motion.CombatMotionCollisionFlags.None,
                surfaceMaterial: CombatSurfaceMaterial.Default);
            target.SetCombatState(new CharacterTestCharacterCombatStateComponent(
                maxHealth: target.CombatState.MaxHealth,
                health: target.CombatState.Health,
                hitStunFramesRemaining: 0,
                lastDamage: 0,
                lastHitFrame: -1L,
                lastHitSourceEntityId: 0,
                lastHitTraceId: 0,
                scatterPressure: target.CombatState.ScatterPressure,
                maxScatterPressure: target.CombatState.MaxScatterPressure,
                scatterPressureBand: target.CombatState.ScatterPressureBand,
                lastCohesionBreakFrame: target.CombatState.LastCohesionBreakFrame));
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

        private static bool CanApplyControlIntent(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.LocalPlayer
                || owner == CharacterTestCharacterControlOwner.RemotePlayer
                || owner == CharacterTestCharacterControlOwner.AiControlled;
        }

        public CharacterTestCharacterEntity FindEntityById(int entityId)
        {
            return FindEntity(entityId);
        }

        public Vector2 ResolveControlWorldMove(CharacterTestCharacterEntity entity, Vector2 move)
        {
            return _motionSystem.ResolveWorldMove(entity, move);
        }

        public CharacterTestNetworkCharacterSnapshot[] CaptureNetworkCharacters()
        {
            if (_world.Entities.Count == 0)
                return new CharacterTestNetworkCharacterSnapshot[0];

            var snapshots = new CharacterTestNetworkCharacterSnapshot[_world.Entities.Count];
            for (int i = 0; i < _world.Entities.Count; i++)
                snapshots[i] = CharacterTestNetworkCharacterSnapshot.FromEntity(
                    _world.Entities[i],
                    _actionSystem.Runtime,
                    _actionSystem.CaptureCombatEvadeState(_world.Entities[i].Id.Value),
                    _world.GetCurrentAttribute(
                        _world.Entities[i],
                        CharacterTestCombatEvadeRuntime.StaminaAttributeId));

            return snapshots;
        }

        internal CharacterTestCombatEvadeStateSnapshot CaptureCombatEvadeState(int entityId)
        {
            return _actionSystem.CaptureCombatEvadeState(entityId);
        }

        internal bool CanUseAuthoredForwardStep(CharacterTestCharacterEntity entity) => _actionSystem.CanUseAuthoredForwardStep(entity);
        internal void RecordAuthoredForwardStep(int entityId, RuntimeFrame frame) => _actionSystem.RecordAuthoredForwardStep(entityId, frame);

        internal int GetCurrentAttribute(int entityId, int attributeId)
        {
            CharacterTestCharacterEntity entity = FindEntity(entityId);
            return entity == null ? 0 : _world.GetCurrentAttribute(entity, attributeId);
        }

        public CharacterTestMovableNaturalObjectSnapshot[] CaptureNetworkMovableNaturalObjects()
        {
            return _world.CaptureMovableNaturalObjectSnapshots();
        }

        public NetworkSnapshotApplyResult ApplyNetworkMovableNaturalObjects(
            IReadOnlyList<CharacterTestMovableNaturalObjectSnapshot> snapshots)
        {
            if (!_world.ApplyMovableNaturalObjectSnapshots(snapshots, out string message))
                return NetworkSnapshotApplyResult.Failed(message);

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                _world.MovableNaturalObjects,
                PhysicsRuntime);
            return NetworkSnapshotApplyResult.Succeeded();
        }

        public NetworkSnapshotApplyResult ApplyNetworkCharacters(
            IReadOnlyList<CharacterTestNetworkCharacterSnapshot> snapshots,
            RuntimeFrame frame,
            int localControlledEntityId = 0,
            bool preserveLocalPredictedEntity = true)
        {
            // Direct callers have no aggregate deck/ledger/journal restore owner. Reject before any empty/rebuild write.
            foreach (var currentActor in _world.Entities) {
                if (currentActor.InteractionEffects.HasFixedAssemblyState)
                    return NetworkSnapshotApplyResult.Failed("network:FixedAssemblyAggregateRestoreUnavailable");
                if (_world.TryGetTacticalMotion(currentActor.Id.Value, out var ledger) && ledger is TacticalCard.MomentumLedger)
                    return NetworkSnapshotApplyResult.Failed(CharacterTestNetworkSnapshotParticipant.MomentumLedgerAggregateRestoreUnavailableMessage);
            }
            if (snapshots == null || snapshots.Count == 0)
            {
                if (_world.Entities.Count > 0)
                    ReleaseAll();
                else
                    _spawnSystem.Clear();
                return NetworkSnapshotApplyResult.Succeeded();
            }

            NetworkSnapshotApplyResult validation = ValidateNetworkCharacterSnapshots(snapshots);
            if (!validation.Success)
                return validation;

            if (_pausedProjectionRuntimeIds != null && !CanApplyNetworkCharactersInPlace(snapshots))
                return NetworkSnapshotApplyResult.Failed("Exact paused identity projection cannot be rebuilt through network spawn.");

            _motionSystem.SuppressLandingEdgesForNextTick();
            _spawnSystem.Clear();
            if (CanApplyNetworkCharactersInPlace(snapshots))
            {
                for (int i = 0; i < snapshots.Count; i++)
                {
                    CharacterTestCharacterEntity entity = _world.Entities[i];
                    CharacterTestNetworkCharacterSnapshot snapshot = snapshots[i];
                    if (preserveLocalPredictedEntity
                        && ShouldPreserveLocalPredictedEntity(entity, snapshot, localControlledEntityId))
                    {
                        continue;
                    }

                    ApplyNetworkCharacterSnapshot(entity, snapshot, frame);
                }

                SyncAuthorityCombatColliders();
                SyncNetworkSnapshotViews(frame);
                return NetworkSnapshotApplyResult.Succeeded();
            }

            NetworkSnapshotApplyResult rebuild = RebuildNetworkCharacters(snapshots, frame);
            if (rebuild.Success)
            {
                SyncAuthorityCombatColliders();
                SyncNetworkSnapshotViews(frame);
            }
            return rebuild;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            var frame = new RuntimeFrame(context.FrameIndex);
            _lastRuntimeFrame = frame;
            _world.CharacterComponents.ClearAnimationRequestsBefore(frame.Value);
            using (SpawnMarker.Auto())
            {
                _spawnSystem.Tick();
            }

            // Capture restored authority before action/root-motion/facing/pose producers mutate this frame.
            _combatTraceSystem.CaptureIncomingSourceMotionStart(_world, frame);

            using (StatusControlMarker.Auto())
            {
                _statusControlEffectBridge.Tick(_world);
            }

            using (BattleStateMarker.Auto())
            {
                _battleStateSystem.Tick(_world, frame, _statusControlEffectBridge);
            }

            using (ActionMarker.Auto())
            {
                _actionSystem.Tick(
                    _world,
                    frame,
                    _statusControlEffectBridge);
            }

            using (BattleStateMarker.Auto())
            {
                _battleStateSystem.Tick(_world, frame, _statusControlEffectBridge);
            }

            using (AudioCueMarker.Auto())
            {
                _audioCuePlaybackSystem.BeginFrame();
                _audioIntentQueue.Clear();
            }

            using (AirDisplacementAudioMarker.Auto())
            {
                _airDisplacementAudioSystem.Tick(_world, frame);
            }

            using (AudioCueMarker.Auto())
            {
                _audioCuePlaybackSystem.CollectIntents(_world, frame, _audioIntentQueue);
            }

            using (RootMotionMarker.Auto())
            {
                _rootMotionSystem.Tick(_world, _actionSystem.Runtime, frame, _statusControlEffectBridge);
            }

            using (CollisionProfileMarker.Auto())
            {
                _collisionProfileResolverSystem.Tick(
                    _world,
                    _motionSystem.PhysicsRuntime,
                    frame,
                    _traceRecorder);
            }

            using (MechanicMotionMarker.Auto())
            {
                _mechanicMotionEffectBridge.Tick(_world);
            }

            using (MotionMarker.Auto())
            {
                // All action/root-motion/mechanic producers have now published this frame's requests.
                // Return admission must see them before Motion consumes and clears them.
                ProcessInteractionReturns(frame);
                _motionSystem.SetActionPoseRegistry(_actionSystem.Runtime.Registry);
                _motionSystem.Tick(_world, _simulationTickRate, frame);
                PublishPresentationMotion(frame);
                for (int i = 0; i < _world.Entities.Count; i++)
                    if (_world.Entities[i].PositionRecall.Result == CharacterTestPositionRecallResult.Returned && _world.Entities[i].PositionRecall.CompletedTick == frame.Value)
                        _combatTraceSystem.ResetPositionRecallWitnesses(_world.Entities[i].Id.Value);
            }

            using (AuthorityPoseMarker.Auto())
            {
                TickAuthorityPose(frame);
                _motionSystem.SyncBodyPartDerivedTopology(_world.Entities, frame);
                _motionSystem.ResolveAnimatedAppendageSoftContact(
                    _world.Entities,
                    _simulationTickRate,
                    frame);
            }

            PerceptionFrameCache.BeginPostMotionGeometryFrame(
                frame,
                this,
                _motionSystem.PhysicsRuntime);

            using (PostMotionInputFlowMarker.Auto())
            {
                LogPostMotionInputFlow(frame);
            }

            using (CombatTraceMarker.Auto())
            {
                _combatTraceSystem.Tick(_world, _actionSystem.Runtime, frame, _statusControlEffectBridge);
            }

            using (BattleStateMarker.Auto())
            {
                _battleStateSystem.Tick(_world, frame, _statusControlEffectBridge);
            }

            using (WeaponHitAudioMarker.Auto())
            {
                _weaponHitAudioSystem.Tick(_combatTraceSystem.WeaponHitAudioEvents, frame);
            }

            using (InteractionConsumersMarker.Auto())
            {
                _interactionConsumerDispatcher.Consume(new CharacterTestInteractionConsumerContext(
                    _world,
                    _motionSystem.PhysicsRuntime,
                    _combatTraceSystem.LastFrameInteractionEvents,
                    frame,
                    _simulationTickRate));
            }

            using (AudioIntentDrainMarker.Auto())
            {
                _audioIntentDrainSystem.Tick(_audioIntentQueue, frame);
                _audioIntentQueue.Clear();
            }

            using (CompleteActionsMarker.Auto())
            {
                _actionSystem.ProcessLandingActions(
                    _motionSystem,
                    frame,
                    _statusControlEffectBridge);
                _actionSystem.CompleteGroundedJumpActions(_world, frame, _statusControlEffectBridge);
            }

            using (BattleStateMarker.Auto())
            {
                _battleStateSystem.Tick(_world, frame, _statusControlEffectBridge);
            }

            using (AnimationMarker.Auto())
            {
                _animationSystem.Tick(_world, frame);
            }

            using (DestructionMarker.Auto())
            {
                _destructionSystem.Tick(
                    _world,
                    _viewBridge,
                    _actionSystem.Runtime,
                    frame,
                    _traceRecorder);
            }

            using (ViewSyncMarker.Auto())
            {
                _viewSyncSystem.Tick(
                    _world,
                    ResolveLockedTargetEntityId(),
                    _headPadDisplayMode,
                    smoothNetworkSnapshots: false,
                    frame: frame,
                    presentationAllyTeamId: _presentationAllyTeamId,
                    forceHeadPadsVisible: _forceHeadPadsVisible);
            }

            using (AnimationSyncMarker.Auto())
            {
                _animationSyncSystem.Tick(_world, frame, _statusControlEffectBridge, _actionSystem.Runtime);
            }

            _world.CharacterComponents.ClearAnimationRequestsAtOrBefore(frame.Value);

            using (AuthorityPoseSyncMarker.Auto())
            {
                _viewBridge.SyncAuthorityPose(_world.Entities, frame);
            }

            PerceptionFrameCache.CommitCompletedGeometryFrame(frame);

            // The planner runs before the next Simulation tick. Mark this
            // frame complete only after all authority pose/hurtbox/trace work
            // has finished, so it can never observe a partially produced tick.
            _hasCompletedRuntimeFrame = true;
        }

        public override void Dispose()
        {
            _physicalAudioSystem.StopContinuousInteractions(AudioStopMode.Immediate);
            _actionSystem.Runtime?.TerminateAll(
                _world,
                _lastRuntimeFrame,
                CharacterActionLifecycleReason.RuntimeShutdown,
                new RuntimeTraceIdentity(RuntimeShutdownTraceKind, 0, _lastRuntimeFrame.Value));
            ReleaseAll();
            _world.EntitySpawned -= PreparePhysicalAudioForSpawnedEntity;
            _world.EntitySpawned -= CharacterTestDefaultBakedWeaponTraceSource.PrepareSpawnedEntity;
            _world.EntitySpawned -= _authorityPoseTerrainSystem.Register;
            _world.EntityRemoved -= RemovePhysicalAudioForRemovedEntity;
            _world.EntityRemoved -= _authorityPoseTerrainSystem.Remove;
            _world.EntityRemoved -= RemoveHurtboxForRemovedEntity;
            _world.EntitiesCleared -= ClearPhysicalAudioRegistrations;
            _world.EntitiesCleared -= _authorityPoseTerrainSystem.Clear;
            _world.EntitiesCleared -= ClearHurtboxesForClearedWorld;
        }

        public void ReleaseAll()
        {
            _combatTraceSystem.ClearIncomingSourceMotion();
            _viewBridge.ReleaseAll();
            _world.Clear();
            _spawnSystem.Clear();
            PerceptionFrameCache.ResetCompletedGeometry();
            _motionSystem.ClearCharacterMotionDiagnostics();
            _hasCompletedRuntimeFrame = false;
        }

        public void TickNetworkPresentation(
            double deltaTime,
            int tickRate,
            int interpolationDelayFrames)
        {
            _viewBridge.TickNetworkPresentation(deltaTime, tickRate, interpolationDelayFrames);
        }

        public void TickPresentation(double deltaTime, RuntimeFrame frame = default)
        {
            _viewBridge.TickPresentation(deltaTime, frame);
            using (PhysicalAudioMarker.Auto())
            {
                _physicalAudioSystem.Tick(_world, frame, _simulationTickRate, _motionSystem.PhysicsRuntime);
            }
            using (AudioIntentDrainMarker.Auto())
            {
                _audioIntentDrainSystem.Tick(_audioIntentQueue, frame);
                _audioIntentQueue.Clear();
            }
        }

        /// <remarks>
        /// LowFreqAlloc: replaces the presentation pose source and explicitly rebuilds active contact registrations.
        /// The next Contact Tick establishes a fresh motion baseline and remains silent.
        /// </remarks>
        internal void SetBodyPartContactPresentationPoseProvider(
            ICharacterTestBodyPartContactPresentationPoseProvider poseProvider)
        {
            _physicalAudioSystem.SetPresentationPoseProvider(poseProvider);
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                _physicalAudioSystem.RefreshContactRegistration(
                    entities[i],
                    _motionSystem.PhysicsRuntime);
            }
        }

        /// <remarks>
        /// LowFreqAlloc: invoked by the world spawn seam, before the entity reaches the PhysicalAudio Tick.
        /// </remarks>
        private void PreparePhysicalAudioForSpawnedEntity(CharacterTestCharacterEntity entity)
        {
            _motionSystem.PrepareHurtboxSource(entity);
            _acousticBodyPropertyProvider.RegisterCharacter(entity);
            _physicalAudioSystem.PrepareContactRegistration(
                entity,
                _motionSystem.PhysicsRuntime);
        }

        /// <remarks>
        /// LowFreqAlloc: invoked after a character leaves the world. Remaining shared probe definitions are rebuilt
        /// before the next PhysicalAudio Tick so the fixed registration capacity can be reused indefinitely.
        /// </remarks>
        private void RemovePhysicalAudioForRemovedEntity(CharacterTestCharacterEntityId entityId)
        {
            _acousticBodyPropertyProvider.RemoveCharacter(entityId);
            _physicalAudioSystem.RemoveContactRegistration(
                entityId,
                _world,
                _motionSystem.PhysicsRuntime);
        }

        /// <remarks>NoAlloc: removes the authoritative hurtbox before the next combat trace.</remarks>
        private void RemoveHurtboxForRemovedEntity(CharacterTestCharacterEntityId entityId)
        {
            _motionSystem.RemoveHurtbox(entityId);
        }

        /// <remarks>NoAlloc: invoked after the runtime world clears all entities.</remarks>
        private void ClearPhysicalAudioRegistrations()
        {
            _acousticBodyPropertyProvider.Clear();
            _physicalAudioSystem.ClearContactRegistrations();
        }

        /// <remarks>NoAlloc: removes all character-owned hurtbox bodies while preserving shared world state.</remarks>
        private void ClearHurtboxesForClearedWorld()
        {
            _motionSystem.ClearHurtboxes();
        }

        internal void TickRuntimeAiLabReplayAnimationState(
            double deltaTime,
            RuntimeFrame frame)
        {
            _viewBridge.TickRuntimeAiLabReplayAnimationState(deltaTime, frame);
        }

        private NetworkSnapshotApplyResult ValidateNetworkCharacterSnapshots(
            IReadOnlyList<CharacterTestNetworkCharacterSnapshot> snapshots)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestNetworkCharacterSnapshot snapshot = snapshots[i];
                if (!_definitions.TryGet(snapshot.CharacterId, out _))
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest snapshot references missing character definition: " + snapshot.CharacterId);
                }

                // Normal network imports keep their existing sequential contract.
                // A detached paused import owns an explicit, validated runtime-ID projection.
                int expectedEntityId = _pausedProjectionRuntimeIds == null ? i + 1
                    : i < _pausedProjectionRuntimeIds.Length ? _pausedProjectionRuntimeIds[i] : -1;
                if (snapshot.EntityId != expectedEntityId)
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest snapshot entity id order mismatch. expected="
                        + expectedEntityId
                        + " actual="
                        + snapshot.EntityId);
                }

                if (!_actionSystem.Runtime.ValidateAnimationSnapshot(snapshot))
                    return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot animation identity does not belong to its prepared character profile.");
                NetworkSnapshotApplyResult behaviorSelectionValidation =
                    ValidateRuntimeAiBehaviorSelection(snapshot);
                if (!behaviorSelectionValidation.Success)
                    return behaviorSelectionValidation;
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        /// <summary>
        /// Validates schema-30 body-part source identity against the registered character packages
        /// before late-join rebuild mutates the live world.
        /// </summary>
        /// <remarks>LowFreqAlloc: network snapshot apply only; package loading uses the existing source cache.</remarks>
        internal NetworkSnapshotApplyResult PreflightNetworkBodyPartStateSnapshots(
            IReadOnlyList<CharacterTestNetworkCharacterSnapshot> characters,
            IReadOnlyList<CharacterTestNetworkBodyPartStateSnapshot> bodyPartStates)
        {
            int characterCount = characters?.Count ?? 0;
            if ((bodyPartStates?.Count ?? 0) != characterCount)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest body-part snapshot does not cover the character set.");
            }

            for (int i = 0; i < characterCount; i++)
            {
                CharacterTestNetworkCharacterSnapshot character = characters[i];
                CharacterTestNetworkBodyPartStateSnapshot bodyPartState = bodyPartStates[i];
                if (character == null
                    || bodyPartState == null
                    || character.EntityId != bodyPartState.EntityId
                    || !_definitions.TryGet(
                        character.CharacterId,
                        out CharacterTestCharacterDefinition definition))
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest body-part snapshot references an invalid character definition.");
                }

                CharacterImportedPackage package;
                try
                {
                    package = CharacterTestGeneratedPackageSource.LoadPackage(
                        definition.GeneratedPackageRoot);
                }
                catch (Exception exception)
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest body-part definition preflight failed: " + exception.Message);
                }

                var component = new CharacterTestBodyPartStateComponent(bodyPartState.EntityId);
                component.Reinitialize(package?.Geometry);
                if (!component.TryPrepareNetworkRestore(bodyPartState, out _, out string failure))
                    return NetworkSnapshotApplyResult.Failed(failure);
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        private NetworkSnapshotApplyResult ValidateRuntimeAiBehaviorSelection(
            CharacterTestNetworkCharacterSnapshot snapshot)
        {
            CharacterTestRuntimeAiBehaviorSelection selection =
                snapshot.CreateRuntimeAiBehaviorSelection();
            if (!selection.HasSelection)
                return NetworkSnapshotApplyResult.Succeeded();

            if (_runtimeAiBehaviorLoadouts == null)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "Runtime AI Planner behavior loadout runtime is unavailable.");
            }

            if (!_runtimeAiBehaviorLoadouts.TryValidateSelection(selection, out string message))
                return NetworkSnapshotApplyResult.Failed(message);

            return NetworkSnapshotApplyResult.Succeeded();
        }

        private bool CanApplyNetworkCharactersInPlace(
            IReadOnlyList<CharacterTestNetworkCharacterSnapshot> snapshots)
        {
            if (_world.Entities.Count != snapshots.Count)
                return false;

            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestCharacterEntity entity = _world.Entities[i];
                CharacterTestNetworkCharacterSnapshot snapshot = snapshots[i];
                if (entity.Id.Value != snapshot.EntityId
                    || entity.Identity.CharacterId != snapshot.CharacterId
                    || entity.Team.TeamId != snapshot.TeamId
                    || entity.Team.DefaultTeamId != snapshot.DefaultTeamId)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldPreserveLocalPredictedEntity(
            CharacterTestCharacterEntity entity,
            CharacterTestNetworkCharacterSnapshot snapshot,
            int localControlledEntityId)
        {
            return localControlledEntityId > 0
                && entity.Id.Value == localControlledEntityId
                && snapshot.EntityId == localControlledEntityId
                && entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
                && snapshot.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer;
        }

        private NetworkSnapshotApplyResult RebuildNetworkCharacters(
            IReadOnlyList<CharacterTestNetworkCharacterSnapshot> snapshots,
            RuntimeFrame frame)
        {
            ReleaseAll();
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestNetworkCharacterSnapshot snapshot = snapshots[i];
                _definitions.TryGet(snapshot.CharacterId, out CharacterTestCharacterDefinition definition);
                var request = CharacterTestCharacterSpawnRequest.FromDeterministic(
                    snapshot.CharacterId,
                    snapshot.TeamId,
                    snapshot.XMillimeters,
                    snapshot.YMillimeters,
                    snapshot.ZMillimeters,
                    snapshot.ControlOwner,
                    snapshot.RuntimeAiPlannerProfileSlot,
                    snapshot.CreateRuntimeAiBehaviorSelection());
                CharacterTestCharacterEntity entity = _world.Spawn(request, definition);
                ApplyNetworkCharacterSnapshot(entity, snapshot, frame);

                if (_createViews && !_viewBridge.TryCreateView(entity))
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest snapshot view creation failed for entity: " + entity.Id.Value);
                }
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        private void ApplyNetworkCharacterSnapshot(
            CharacterTestCharacterEntity entity,
            CharacterTestNetworkCharacterSnapshot snapshot,
            RuntimeFrame frame)
        {
            entity.SetControl(new CharacterTestCharacterControlComponent(
                snapshot.ControlOwner,
                snapshot.RuntimeAiPlannerProfileSlot,
                snapshot.CreateRuntimeAiBehaviorSelection()));
            entity.SetTransform(snapshot.CreateTransformComponent());
            entity.SetInput(snapshot.CreateInputComponent());
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                snapshot.ActiveActionId,
                snapshot.ActionState,
                snapshot.ActionPhase,
                snapshot.ActionLocalFrame,
                snapshot.LastRejection,
                snapshot.LastAnimationActionKey,
                snapshot.AnimationActionKeyThisFrame,
                snapshot.AnimationTransitionFrames,
                snapshot.AnimationHoldFrames,
                snapshot.JumpImpulseYMillimetersPerSecond,
                snapshot.ActionMovementMode,
                snapshot.ActionMovementLocked,
                snapshot.ActionMovementControlScaleMilli,
                snapshot.ActionRotationControlScaleMilli,
                snapshot.AnimationPlaybackSpeedMilli,
                lastAnimationBindingId: snapshot.LastAnimationBindingId));
            CharacterTestCharacterActionRuntime actionRuntime = _actionSystem.Runtime;
            if (actionRuntime != null)
            {
                CharacterActionExecutionState restoreState = snapshot.CreateActionExecutionState();
                if (!actionRuntime.TryRestoreRunner(
                        entity.ComponentWorld,
                        entity.Id,
                        restoreState,
                        out string actionRestoreDiagnostic)
                    && !string.IsNullOrEmpty(actionRestoreDiagnostic))
                {
                    throw new InvalidOperationException("ActionRunnerRestoreFailed: " + actionRestoreDiagnostic);
                }
            }

            entity.SetMotionStateDeterministic(
                snapshot.VerticalVelocityMillimetersPerSecond,
                snapshot.LastMoveInputXMilli,
                snapshot.LastMoveInputYMilli,
                snapshot.IsGrounded,
                snapshot.HorizontalVelocityXMillimetersPerSecond,
                snapshot.HorizontalVelocityYMillimetersPerSecond,
                snapshot.DesiredVelocityXMillimetersPerSecond,
                snapshot.DesiredVelocityYMillimetersPerSecond,
                snapshot.JumpBufferFramesRemaining,
                snapshot.CoyoteTimeFramesRemaining,
                snapshot.MovementMode,
                snapshot.MovementLocked,
                snapshot.MovementControlScaleMilli,
                snapshot.RotationControlScaleMilli,
                snapshot.MovementProfileId,
                snapshot.MovementProfileFallback,
                snapshot.PhysicsEnabled,
                snapshot.CollisionFlags,
                snapshot.SurfaceFrictionMilli,
                snapshot.CreateSurfaceMaterial(),
                snapshot.SurfaceBounceSpeedCapYMillimetersPerSecond);
            entity.SetGroundPointTraction(snapshot.GroundPointTraction);
            entity.SetPositionRecall(snapshot.PositionRecall);
            entity.GameplayIntentBuffer.Clear();
            CharacterTestCharacterPhysicsComponent physics = CreateSnapshotPhysicsComponent(snapshot);
            entity.SetPhysics(physics);
            entity.SetColliderPosture(snapshot.CreateColliderPostureComponent(physics));
            entity.SetCombatState(new CharacterTestCharacterCombatStateComponent(
                snapshot.MaxHealth,
                snapshot.Health,
                snapshot.HitStunFramesRemaining,
                snapshot.LastDamage,
                snapshot.LastHitFrame,
                snapshot.LastHitSourceEntityId,
                snapshot.LastHitTraceId));
            entity.SetLivingNaturalAttributes(snapshot.CreateLivingNaturalAttributesRuntimeState());
            if (entity.AuthorityPose == null)
                entity.SetAuthorityPose(new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));
            entity.RestoreAuthorityPoseSnapshot(snapshot.AuthorityPose);
            _world.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, snapshot.Health);
            _world.SetCurrentAttribute(
                entity.GameplayEntityId,
                CharacterTestCombatEvadeRuntime.StaminaAttributeId,
                snapshot.CombatEvadeStamina);
            if (snapshot.HasExactBattleState)
                _world.SetBattleState(entity.Id, snapshot.CreateBattleStateComponent());
            else
                CharacterTestBattleStateSystem.RefreshEntity(
                    _world,
                    entity,
                    frame,
                    _statusControlEffectBridge);
            entity.RestoreSourceRevisions(
                snapshot.SourcePoseRevision,
                snapshot.SourceFacingRevision);
            _actionSystem.RestoreCombatEvadeState(
                entity.Id.Value,
                snapshot.CreateCombatEvadeStateSnapshot());
            snapshot.RestoreAnimationState(entity, actionRuntime);
        }

        private CharacterTestCharacterPhysicsComponent CreateSnapshotPhysicsComponent(
            CharacterTestNetworkCharacterSnapshot snapshot)
        {
            CharacterTestCharacterPhysicsComponent fallback = CharacterTestCharacterPhysicsComponent.Default;
            if (_definitions.TryGet(snapshot.CharacterId, out CharacterTestCharacterDefinition definition))
                fallback = definition.PhysicsProfile;

            return snapshot.CreatePhysicsComponent(fallback);
        }

        private void SyncNetworkSnapshotViews(RuntimeFrame frame)
        {
            _animationSystem.Tick(_world, frame);
            _viewSyncSystem.Tick(
                _world,
                ResolveLockedTargetEntityId(),
                _headPadDisplayMode,
                _smoothNetworkSnapshotViews,
                frame,
                _presentationAllyTeamId,
                _forceHeadPadsVisible);
            _statusControlEffectBridge.Tick(_world);
            _animationSyncSystem.Tick(_world, frame, _statusControlEffectBridge, _actionSystem.Runtime);
            _viewBridge.SyncAuthorityPose(_world.Entities, frame);
        }

        internal void ForceSyncRuntimeAiLabPresentation(RuntimeFrame frame)
        {
            SyncAuthorityCombatColliders();
            _statusControlEffectBridge.Tick(_world);
            _animationSyncSystem.Tick(_world, frame, _statusControlEffectBridge, _actionSystem.Runtime);
            _viewSyncSystem.Tick(
                _world,
                ResolveLockedTargetEntityId(),
                _headPadDisplayMode,
                smoothNetworkSnapshots: false,
                frame: frame,
                presentationAllyTeamId: _presentationAllyTeamId,
                forceHeadPadsVisible: _forceHeadPadsVisible);
            _viewBridge.SyncAuthorityPose(_world.Entities, frame);
        }

        internal void ResetRuntimeAiLabReplayTransientState()
        {
            _motionSystem.ResetRuntimeAiLabReplayTransientState();
            _rootMotionSystem.ClearCache();
        }

        internal void ForceSyncRuntimeAiLabReplayPresentation(RuntimeFrame frame)
        {
            SyncAuthorityCombatColliders();
            _statusControlEffectBridge.Tick(_world);
            _viewSyncSystem.Tick(
                _world,
                ResolveLockedTargetEntityId(),
                _headPadDisplayMode,
                smoothNetworkSnapshots: false,
                frame: frame,
                presentationAllyTeamId: _presentationAllyTeamId,
                forceHeadPadsVisible: _forceHeadPadsVisible);
            _viewBridge.SyncAuthorityPose(_world.Entities, frame);
        }

        internal CharacterTestAnimationPresentationCheckpoint CaptureAnimationPresentationCheckpoint(
            RuntimeFrame frame)
        {
            return _viewBridge.CaptureAnimationPresentationCheckpoint(frame, _world.Entities);
        }

        internal void CopyRuntimeAiLabAnimationInputsTo(CharacterTestAnimationInputPresentationState[] inputs)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                inputs[i] = new CharacterTestAnimationInputPresentationState(
                    entity.Id.Value,
                    entity.Animation);
            }
        }

        internal CharacterTestAnimationInputPresentationState[] CaptureRuntimeAiLabAnimationInputs()
        {
            var inputs = new CharacterTestAnimationInputPresentationState[_world.Entities.Count];
            CopyRuntimeAiLabAnimationInputsTo(inputs);
            return inputs;
        }

        internal bool ApplyRuntimeAiLabAnimationInputs(
            CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState> inputs,
            RuntimeFrame frame)
        {
            if (inputs.Count != _world.Entities.Count)
                return false;
            for (int i = 0; i < inputs.Count; i++)
            {
                CharacterTestCharacterEntity entity = FindEntityById(inputs[i].EntityId);
                if (entity == null)
                    return false;
                entity.SetAnimation(inputs[i].Animation);
            }
            _animationSyncSystem.Tick(
                _world,
                frame,
                _statusControlEffectBridge,
                _actionSystem.Runtime);
            return true;
        }

        internal bool RestoreAnimationPresentationCheckpoint(
            CharacterTestAnimationPresentationCheckpoint checkpoint,
            RuntimeFrame frame,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (checkpoint == null || checkpoint.Frame != frame || !checkpoint.FrameAligned)
            {
                diagnostic = checkpoint?.Diagnostic ?? "Animation presentation checkpoint is missing.";
                return false;
            }

            IReadOnlyList<CharacterTestAnimationInputPresentationState> inputs = checkpoint.AnimationInputs;
            if (inputs.Count != _world.Entities.Count)
            {
                diagnostic = "Animation presentation checkpoint entity count does not match runtime world.";
                return false;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                CharacterTestCharacterEntity entity = FindEntityById(inputs[i].EntityId);
                if (entity == null)
                {
                    diagnostic = "Animation presentation checkpoint references a missing runtime entity.";
                    return false;
                }
                entity.SetAnimation(inputs[i].Animation);
            }

            return _viewBridge.RestoreAnimationPresentationCheckpoint(checkpoint, frame, out diagnostic);
        }

        internal bool RestoreRuntimeAiLabViewRootPoses(
            CharacterTestRuntimeAiLabPresentationDiagnosticSample capture,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> characters = capture?.Characters;
            int count = characters?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabPresentationCharacterSample character = characters[i];
                if (!character.HasUnityView)
                    continue;

                var position = new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(character.UnityViewXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(character.UnityViewYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(character.UnityViewZMillimeters));
                Quaternion rotation = Quaternion.Euler(
                    0f,
                    CharacterTestDeterministicUnits.MilliDegreesToDegrees(character.UnityViewYawMilliDegrees),
                    0f);
                if (_viewBridge.TryRestoreRuntimeAiLabViewPose(
                        new CharacterTestCharacterEntityId(character.EntityId),
                        position,
                        rotation))
                {
                    continue;
                }

                diagnostic = "Committed Runtime AI Lab character view is unavailable for entity "
                    + character.EntityId
                    + ".";
                return false;
            }

            return true;
        }

        internal bool RestoreCanonicalAnimationPresentation(out string diagnostic)
        {
            return _viewBridge.RestoreCanonicalAnimation(
                _world.Entities,
                _simulationTickRate,
                out diagnostic);
        }

        private void SyncAuthorityCombatColliders()
        {
            _motionSystem.SetActionPoseRegistry(_actionSystem.Runtime.Registry);
            if (_motionSystem.PhysicsRuntime == null)
                return;

            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                _world.MovableNaturalObjects,
                _motionSystem.PhysicsRuntime);
            ((ICharacterTestCharacterColliderSync)_motionSystem)
                .SyncAllCharacterCombatColliders(_world.Entities);
            _motionSystem.SyncBodyPartDerivedTopology(_world.Entities, _lastRuntimeFrame);
        }

        internal void SyncBodyPartDerivedTopologyAfterAuthorityRestore(RuntimeFrame frame)
        {
            _motionSystem.SyncBodyPartDerivedTopology(_world.Entities, frame);
        }

        private void TickAuthorityPose(RuntimeFrame frame)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
                entities[i].TickAuthorityPose(frame);
            _authorityPoseTerrainSystem.Tick(entities, _motionSystem.PhysicsRuntime, frame, _actionSystem.Runtime.Registry);
        }

        private int ResolveLockedTargetEntityId()
        {
            return _lockedTargetEntityIdProvider != null
                ? Math.Max(0, _lockedTargetEntityIdProvider())
                : 0;
        }

        private void RefreshMovementProfile()
        {
            CharacterTestCharacterMovementProfileRuntime profile =
                CharacterTestCharacterMovementProfileSource.Load(
                    _actionConfigOptions,
                    _actionSystem.Runtime != null ? _actionSystem.Runtime.ActionSet : null);
            _motionSystem.SetMovementProfile(profile);
            _animationSystem.SetMovementProfile(profile);
        }

        private CharacterTestCharacterEntity FindEntity(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        private static void AddGameplayIntentBufferEntries(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterTestControlIntent intent)
        {
            if (intent.JumpPressed)
            {
                entity.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.Jump,
                    intent.TargetEntityId,
                    frame.Value);
            }

            if (intent.AttackPrimaryPressed)
            {
                entity.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.AttackPrimary,
                    intent.TargetEntityId,
                    frame.Value);
            }

            if (intent.AttackSecondaryPressed)
            {
                entity.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.AttackSecondary,
                    intent.TargetEntityId,
                    frame.Value);
            }

            if (intent.DodgePressed)
            {
                entity.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.Dodge,
                    intent.TargetEntityId,
                    frame.Value,
                    bufferFrames: 18);
            }

            if (intent.SkillSlotPressedMask != 0)
            {
                entity.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.SkillSlot,
                    intent.TargetEntityId,
                    frame.Value,
                    skillSlotPressedMask: intent.SkillSlotPressedMask);
            }
        }

        private void LogControlIntentWrite(
            CharacterTestControlIntent intent,
            CharacterTestCharacterEntity entity)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (!CharacterTestInputFlowLog.HasActiveIntent(intent))
                return;

            _inputFlowBuffer.Clear()
                .Append("write-input");
            CharacterTestInputFlowLog.AppendIntent(_inputFlowBuffer, intent);
            _inputFlowBuffer.Append(" owner=").Append(entity.Control.Owner.ToString());
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }

        private void LogPostMotionInputFlow(RuntimeFrame frame)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;

                bool hasInputOrMotion = CharacterTestInputFlowLog.HasActiveInput(entity.Input)
                    || CharacterTestInputFlowLog.HasMotionEffect(entity);
                int repeatFrames = hasInputOrMotion
                    ? CharacterTestInputFlowLog.ActiveRepeatFrames
                    : CharacterTestInputFlowLog.IdleRepeatFrames;
                if (_lastPostMotionInputFlowFrame != long.MinValue
                    && _lastPostMotionHadInputOrMotion == hasInputOrMotion
                    && frame.Value - _lastPostMotionInputFlowFrame < repeatFrames)
                {
                    return;
                }

                _lastPostMotionInputFlowFrame = frame.Value;
                _lastPostMotionHadInputOrMotion = hasInputOrMotion;
                _inputFlowBuffer.Clear()
                    .Append("post-motion frame=").Append(frame.Value);
                CharacterTestInputFlowLog.AppendPostMotion(_inputFlowBuffer, entity);
                _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
                return;
            }
        }

        private sealed class CharacterTestBodyPartContactUnityPoseProvider :
            ICharacterTestBodyPartContactPresentationPoseProvider
        {
            private readonly CharacterTestCharacterUnityViewBridge _viewBridge;

            public CharacterTestBodyPartContactUnityPoseProvider(
                CharacterTestCharacterUnityViewBridge viewBridge)
            {
                _viewBridge = viewBridge;
            }

            public bool TryResolveBodyPartContactPointEntityLocalOffset(
                CharacterTestCharacterEntity entity,
                CharacterBodyPartContactPointConfig config,
                out FixVector3 localOffset)
            {
                localOffset = FixVector3.Zero;
                return _viewBridge != null
                    && _viewBridge.TryResolveBodyPartContactPointEntityLocalOffset(
                        entity,
                        config,
                        out localOffset);
            }
        }
    }
}
