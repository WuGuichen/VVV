using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Replaceable pure C# authority composition owned by the public Demo session.</summary>
    internal sealed class GameplayTriggerPlayableComposition : IDisposable
    {
        public const double FixedDeltaTime = 1d / 60d;

        private readonly RuntimeHost _host;
        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly GameplayEntityId _playerEntity;
        private readonly GameplayTriggerRuntime _triggerRuntime;
        private readonly GameplayTriggerCombatSensorSystem _combatSensor;
        private readonly GameplayTriggerRuntimeEventObserverBuffer _eventObserver;
        private readonly StoryDirector _storyDirector;
        private readonly GameplayTriggerPlayableSimulationModule _simulation;
        private readonly RuntimeCommandSubmissionGate _inputGate;
        private readonly StoryRuntimeCommandValidator _storyCommandValidator;
        private readonly GameplayTriggerPlayableCommandValidator _demoCommandValidator;
        private readonly RuntimeCommandBuffer _storyCommands;
        private readonly RuntimeCommandBuffer _gameplayCommands;
        private readonly RuntimeCommandBuffer _demoCommands;
        private readonly RuntimeSaveStateCoordinator _saveCoordinator;
        private readonly RuntimeCheckpointBarrier _checkpointBarrier;
        private readonly Func<bool> _isCandidateSwapInProgress;
        private readonly RuntimeHashCombiner _hashCombiner;
        private readonly RuntimeCommand[] _inputBatch = new RuntimeCommand[2];
        private long _nextFrame;
        private long _inputSequence;
        private long _lastAuthorityHash;
        private GameplayTriggerOccurrenceKind _lastOccurrenceKind;
        private int _lastOccurrenceSourceBindingId;
        private int _lastActionKindId;
        private GameplayTriggerRuntimeEventKind _lastResultKind;
        private GameplayTriggerRuntimeEventStatus _lastResultStatus;
        private GameplayTriggerOccurrenceKind _firstOccurrenceKindThisTick;
        private int _occurrenceCountThisTick;
        private int _actionResultCountThisTick;
        private bool _disposed;

        /// <remarks>AllocByDesign. Builds and starts the fixed-capacity playable composition.</remarks>
        internal GameplayTriggerPlayableComposition(
            RuntimeSaveStateRestoreProfile restoreProfile,
            Func<bool> isCandidateSwapInProgress)
        {
            _isCandidateSwapInProgress = isCandidateSwapInProgress ?? NeverSwapping;
            _buffRequests = new GameplayBuffRequestBuffer(
                reservationCapacity: 8,
                legacyPrewarmCapacity: 8);
            var buffSink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                _buffRequests);
            var storySink = new GameplayTriggerStoryReliableCommandActionSink(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
            GameplayTriggerPlayableCatalogComposition trigger =
                GameplayTriggerPlayableCatalog.Create(buffSink, storySink);
            var triggerEvents = new GameplayTriggerRuntimeEventSource(32);
            _triggerRuntime = new GameplayTriggerRuntime(
                trigger.Catalog,
                trigger.RuntimeOptions,
                triggerEvents);

            _host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.FailFast
            });

            _storyDirector = CreateStoryDirector();
            _storyCommandValidator = new StoryRuntimeCommandValidator(_storyDirector);
            _storyCommands = RuntimeCommandBuffer.CreateBounded(
                _storyCommandValidator,
                RuntimeFrame.Zero,
                pendingCapacityLimit: 8);
            var storySources = new RuntimeCommandSourceRegistry(initialCapacity: 1);
            if (!storySources.TryRegister(
                    StoryRuntimeCommandSources.GameplayBridge,
                    RuntimeCommandSourceKind.Derived,
                    new[] { StoryRuntimeCommandIds.RaiseTrigger },
                    out RuntimeCommandSourceRegistrationResult storyRegistration))
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger Story source registration failed: " +
                    storyRegistration.ErrorCode);
            }
            var storyGate = new RuntimeCommandSubmissionGate(
                storySources,
                _storyCommands,
                RuntimeCommandIngressKind.TrustedDerived);
            var storyRelay = new GameplayTriggerStoryCommandResultRelay(triggerEvents);
            var storyModule = new StoryRuntimeModule(
                _storyDirector,
                _storyCommands,
                events: null,
                _storyCommandValidator,
                storyRelay);
            var storyTransfer = new GameplayTriggerStoryReliableCommandTransfer(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                StoryRuntimeCommandSources.GameplayBridge,
                _host,
                storyModule,
                storyGate);
            var outbox = new GameplayTriggerReliableOutboxModule(
                _triggerRuntime,
                new IGameplayTriggerReliableTargetTransfer[] { storyTransfer });

            _componentWorld = new GameplayComponentWorld();
            RegisterGameplaySchemas(_componentWorld.Schemas);
            _playerEntity = _componentWorld.CreateEntity();
            _componentWorld.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                _playerEntity,
                new GameplayAttributeSetComponent(
                    new GameplayAttributeValue(
                        GameplayTriggerPlayableIds.MoveSpeedAttribute,
                        baseValue: 6,
                        currentValue: 6)));
            var physicsWorld = CreatePhysicsWorld();
            var entityMap = new CombatEntityGameplayMap();
            entityMap.Register(
                new CombatEntityId(GameplayTriggerPlayableIds.PlayerCombatEntity),
                _playerEntity);
            _combatSensor = CreateCombatSensor(
                trigger.Plan,
                _triggerRuntime,
                physicsWorld,
                entityMap,
                _componentWorld);
            GameplayBuffDefinitionRegistry buffs = CreateBuffDefinitions();
            var gameplayPipeline = new GameplaySystemPipeline();
            gameplayPipeline.Add(new GameplayTriggerDeferredSystem(_triggerRuntime));
            gameplayPipeline.Add(new GameplayBuffCommandSystem(buffs, _buffRequests));
            gameplayPipeline.Add(new GameplayTriggerCommandSystem(
                _triggerRuntime,
                maxSignalCommandItems: 3));
            gameplayPipeline.Add(_combatSensor);
            gameplayPipeline.Add(new GameplayTriggerEvaluationSystem(_triggerRuntime));
            gameplayPipeline.Add(new GameplayComponentBuffCleanupSystem());
            gameplayPipeline.Add(new GameplayBuffApplicationSystem(
                buffs,
                _buffRequests,
                resultSink: new GameplayTriggerGameplayBuffResultRelay(triggerEvents)));
            gameplayPipeline.Add(new GameplayBuffEffectProjectionSystem(buffs));
            _eventObserver = new GameplayTriggerRuntimeEventObserverBuffer(32);
            gameplayPipeline.Add(new GameplayTriggerEventRouterSystem(
                triggerEvents,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration(
                        "mxframework.demo.gameplay-trigger-playable",
                        _eventObserver)
                }));
            _gameplayCommands = RuntimeCommandBuffer.CreateBounded(
                validator: null,
                RuntimeFrame.Zero,
                pendingCapacityLimit: 8);
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                _gameplayCommands,
                tickWorldAutomatically: false,
                priority: 100,
                systemPipeline: gameplayPipeline,
                componentWorld: _componentWorld);
            _demoCommandValidator = new GameplayTriggerPlayableCommandValidator();
            _demoCommands = RuntimeCommandBuffer.CreateBounded(
                _demoCommandValidator,
                RuntimeFrame.Zero,
                pendingCapacityLimit: 16);
            var inputSources = new RuntimeCommandSourceRegistry(initialCapacity: 1);
            if (!inputSources.TryRegister(
                    GameplayTriggerPlayableIds.CommandSource,
                    RuntimeCommandSourceKind.Exogenous,
                    new[]
                    {
                        GameplayTriggerPlayableIds.MoveCommand,
                        GameplayTriggerPlayableIds.CaptureCommand,
                        GameplayTriggerPlayableIds.RestoreCommand,
                        GameplayTriggerPlayableIds.ResetCommand
                    },
                    out RuntimeCommandSourceRegistrationResult inputRegistration))
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable input source registration failed: " +
                    inputRegistration.ErrorCode);
            }
            _inputGate = new RuntimeCommandSubmissionGate(
                inputSources,
                _demoCommands,
                RuntimeCommandIngressKind.External);
            _simulation = new GameplayTriggerPlayableSimulationModule(
                _storyDirector,
                _componentWorld,
                _playerEntity,
                _demoCommands,
                physicsWorld);

            _host.RegisterModule(outbox);
            _host.RegisterModule(storyModule);
            _host.RegisterModule(_simulation);
            _host.RegisterModule(gameplayModule);
            if (!GameplayTriggerCompositionVerifier.TryVerify(
                    trigger.Plan,
                    _host,
                    _triggerRuntime,
                    outbox,
                    out GameplayTriggerCompositionVerificationResult verification))
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable composition verification failed with " +
                    verification.DiagnosticCount + " diagnostic(s).");
            }
            _host.Initialize();
            _host.Start();

            _hashCombiner = new RuntimeHashCombiner(
                new IRuntimeHashContributor[]
                {
                    new GameplayComponentWorldHashContributor(_componentWorld),
                    new GameplayTriggerRuntimeHashContributor(_triggerRuntime),
                    new StoryRuntimeHashContributor(_storyDirector),
                    new GameplayTriggerPlayableHashContributor(this)
                });
            _lastAuthorityHash = ComputeAuthorityHash();

            var triggerSave = new GameplayTriggerRuntimeSaveStateParticipant(
                _triggerRuntime,
                new RestoredGameplayEntityValidator(_componentWorld));
            var demoSave = new GameplayTriggerPlayableSaveStateParticipant(this);
            _saveCoordinator = CreateSaveStateCoordinator(
                restoreProfile,
                triggerSave,
                demoSave);
            var quiescence = new RuntimeCheckpointQuiescenceRegistry();
            RequireQuiescence(quiescence.Register(
                GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultQuiescenceOrder,
                triggerSave));
            RequireQuiescence(quiescence.Register(
                GameplayTriggerPlayableSaveStateParticipant.ParticipantId,
                GameplayTriggerPlayableSaveStateParticipant.ParticipantOrder,
                demoSave));
            _checkpointBarrier = new RuntimeCheckpointBarrier(
                _host,
                _saveCoordinator,
                quiescence);
        }

        internal GameplayTriggerPlayableComposition()
            : this(null, null)
        {
        }

        /// <remarks>NoAlloc.</remarks>
        internal GameplayTriggerPlayableSnapshot Snapshot => CreateSnapshot(0);

        internal RuntimeSaveStateRestoreProfile RestoreProfile =>
            _saveCoordinator.RestoreProfile;

        internal RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureCheckpoint() =>
            _checkpointBarrier.CaptureCheckpoint();

        internal RuntimeSaveStateCoordinatorResult<bool> RestorePrepared(
            RuntimeSaveStateRestorePlan plan) =>
            _saveCoordinator.RestorePrepared(plan);

        internal long NextFrame => _nextFrame;
        internal long InputSequence => _inputSequence;
        internal long LastConsumedCommandSequence =>
            _simulation.LastConsumedCommandSequence;
        internal bool CheckpointAccepted => IsCheckpointAccepted();
        internal RuntimeCommandBuffer StoryCommands => _storyCommands;
        internal RuntimeCommandBuffer GameplayCommands => _gameplayCommands;
        internal RuntimeCommandBuffer DemoCommands => _demoCommands;
        internal GameplayBuffRequestBuffer BuffRequests => _buffRequests;
        internal GameplayTriggerPlayableSimulationModule Simulation => _simulation;
        internal StoryRuntimeCommandValidator StoryCommandValidator =>
            _storyCommandValidator;
        internal GameplayTriggerPlayableCommandValidator DemoCommandValidator =>
            _demoCommandValidator;
        internal bool IsCandidateSwapInProgress => _isCandidateSwapInProgress();

        /// <remarks>LowFreqAlloc. Hash contributors may copy canonical snapshots.</remarks>
        internal long ComputeAuthorityHash()
        {
            long completedFrame = _nextFrame == 0L ? 0L : _nextFrame - 1L;
            _lastAuthorityHash = _hashCombiner.ComputeHash(
                new RuntimeFrame(completedFrame));
            return _lastAuthorityHash;
        }

        /// <summary>Advances one logical Tick with an already latched Move plus optional control.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        internal GameplayTriggerPlayableTickResult Tick(
            int moveX,
            int moveZ,
            int controlCommandId)
        {
            ThrowIfDisposed();
            int commandCount = BuildInputBatch(moveX, moveZ, controlCommandId);
            RuntimeCommandBatchSubmissionResult submission =
                _inputGate.SubmitBatch(
                    new ReadOnlySpan<RuntimeCommand>(
                        _inputBatch,
                        start: 0,
                        commandCount));
            if (!submission.Success)
                return new GameplayTriggerPlayableTickResult(
                    false,
                    CreateSnapshot(0));
            _inputSequence++;
            _firstOccurrenceKindThisTick = GameplayTriggerOccurrenceKind.None;
            _occurrenceCountThisTick = 0;
            _actionResultCountThisTick = 0;
            _host.Tick(_nextFrame, FixedDeltaTime, _nextFrame * FixedDeltaTime);
            ProjectPresentationEvents();
            _nextFrame++;
            return new GameplayTriggerPlayableTickResult(
                true,
                CreateSnapshot(controlCommandId));
        }

        /// <remarks>LowFreqAlloc only on lifecycle error paths.</remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _host.Dispose();
        }

        internal GameplayTriggerPlayableSnapshot CreateSnapshot(
            int lastControlCommandId) =>
            new GameplayTriggerPlayableSnapshot(
                _nextFrame,
                _simulation.MotionState.Position.X,
                _simulation.MotionState.Position.Z,
                checkpointAccepted: IsCheckpointAccepted(),
                doorUnlocked: IsSourceEnabled(
                    GameplayTriggerPlayableIds.DoorSourceBinding),
                doorOpen: IsDoorOpen(),
                trapActivations: GetTrapActivationCount(),
                slowFramesRemaining: GetSlowFramesRemaining(),
                lastOccurrenceKind: _lastOccurrenceKind,
                lastOccurrenceSourceBindingId: _lastOccurrenceSourceBindingId,
                lastActionKindId: _lastActionKindId,
                lastResultKind: _lastResultKind,
                lastResultStatus: _lastResultStatus,
                firstOccurrenceKindThisTick: _firstOccurrenceKindThisTick,
                occurrenceCountThisTick: _occurrenceCountThisTick,
                actionResultCountThisTick: _actionResultCountThisTick,
                combatBodyCount: _simulation.PhysicsWorld.BodyCount,
                combatColliderCount: _simulation.PhysicsWorld.ColliderCount,
                lastControlCommandId,
                hasCapture: false,
                capturedFrame: -1L,
                lastControlSucceeded: true,
                authorityHash: _lastAuthorityHash);

        private void ProjectPresentationEvents()
        {
            while (_eventObserver.TryDequeue(out GameplayTriggerRuntimeEvent evt))
            {
                if (evt.Kind == GameplayTriggerRuntimeEventKind.Occurrence)
                {
                    if (_occurrenceCountThisTick == 0)
                        _firstOccurrenceKindThisTick = evt.OccurrenceKind;
                    _occurrenceCountThisTick++;
                    _lastOccurrenceKind = evt.OccurrenceKind;
                    _lastOccurrenceSourceBindingId = evt.SourceBindingId.Value;
                }
                if (evt.ActionKindId.IsValid)
                    _lastActionKindId = evt.ActionKindId.Value;

                switch (evt.Kind)
                {
                    case GameplayTriggerRuntimeEventKind.CommandResult:
                    case GameplayTriggerRuntimeEventKind.InternalDeferredResult:
                    case GameplayTriggerRuntimeEventKind.ReliableTransferResult:
                    case GameplayTriggerRuntimeEventKind.ActionResult:
                        _lastResultKind = evt.Kind;
                        _lastResultStatus = evt.Status;
                        if (evt.Kind == GameplayTriggerRuntimeEventKind.ActionResult)
                            _actionResultCountThisTick++;
                        break;
                }
            }
        }

        private int BuildInputBatch(int moveX, int moveZ, int controlCommand)
        {
            RuntimeFrame frame = new RuntimeFrame(_nextFrame);
            RuntimeCommandEnvelope envelope = new RuntimeCommandEnvelope(
                GameplayTriggerPlayableIds.CommandEnvelopeType,
                GameplayTriggerPlayableIds.CommandEnvelopeSchema,
                RuntimeCommandProvenanceKind.Exogenous,
                data0: _inputSequence);
            _inputBatch[0] = new RuntimeCommand(
                frame,
                GameplayTriggerPlayableIds.CommandSource,
                GameplayTriggerPlayableIds.MoveCommand,
                targetId: GameplayTriggerPlayableIds.PlayerCombatEntity,
                payload0: moveX,
                payload1: moveZ,
                envelope: envelope);

            if (controlCommand == 0)
                return 1;
            _inputBatch[1] = new RuntimeCommand(
                frame,
                GameplayTriggerPlayableIds.CommandSource,
                controlCommand,
                targetId: 0,
                envelope: envelope);
            return 2;
        }

        private static CombatPhysicsWorld CreatePhysicsWorld()
        {
            var world = new CombatPhysicsWorld();
            AddAabb(
                world,
                GameplayTriggerPlayableIds.PlayerCombatEntity,
                GameplayTriggerPlayableIds.PlayerBody,
                GameplayTriggerPlayableIds.PlayerCollider,
                GameplayTriggerPlayableCombatLayers.Actor,
                new FixVector3(Fix64.FromInt(-4), Fix64.Zero, Fix64.Zero),
                new FixVector3(
                    Fix64.FromRatio(-3, 10),
                    -Fix64.Half,
                    Fix64.FromRatio(-3, 10)),
                new FixVector3(
                    Fix64.FromRatio(3, 10),
                    Fix64.Half,
                    Fix64.FromRatio(3, 10)));
            AddAabb(
                world,
                GameplayTriggerPlayableIds.BoundsCombatEntityStart,
                GameplayTriggerPlayableIds.BoundsBodyStart,
                GameplayTriggerPlayableIds.BoundsColliderStart,
                GameplayTriggerPlayableCombatLayers.World,
                FixVector3.Zero,
                V(-6, -1, -4),
                V(-5, 1, 4));
            AddAabb(
                world,
                GameplayTriggerPlayableIds.BoundsCombatEntityStart + 1,
                GameplayTriggerPlayableIds.BoundsBodyStart + 1,
                GameplayTriggerPlayableIds.BoundsColliderStart + 1,
                GameplayTriggerPlayableCombatLayers.World,
                FixVector3.Zero,
                V(5, -1, -4),
                V(6, 1, 4));
            AddAabb(
                world,
                GameplayTriggerPlayableIds.BoundsCombatEntityStart + 2,
                GameplayTriggerPlayableIds.BoundsBodyStart + 2,
                GameplayTriggerPlayableIds.BoundsColliderStart + 2,
                GameplayTriggerPlayableCombatLayers.World,
                FixVector3.Zero,
                V(-6, -1, -4),
                V(6, 1, -3));
            AddAabb(
                world,
                GameplayTriggerPlayableIds.BoundsCombatEntityStart + 3,
                GameplayTriggerPlayableIds.BoundsBodyStart + 3,
                GameplayTriggerPlayableIds.BoundsColliderStart + 3,
                GameplayTriggerPlayableCombatLayers.World,
                FixVector3.Zero,
                V(-6, -1, 3),
                V(6, 1, 4));
            AddAabb(
                world,
                GameplayTriggerPlayableIds.DoorCombatEntity,
                GameplayTriggerPlayableIds.DoorBody,
                GameplayTriggerPlayableIds.DoorCollider,
                GameplayTriggerPlayableCombatLayers.Door,
                FixVector3.Zero,
                new FixVector3(
                    Fix64.FromRatio(-2, 10),
                    -Fix64.One,
                    Fix64.FromInt(-3)),
                new FixVector3(
                    Fix64.FromRatio(2, 10),
                    Fix64.One,
                    Fix64.FromInt(3)));
            return world;
        }

        private static GameplayTriggerCombatSensorSystem CreateCombatSensor(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerRuntime runtime,
            CombatPhysicsWorld physicsWorld,
            ICombatEntityGameplayMapReader entityMap,
            GameplayComponentWorld componentWorld)
        {
            var descriptors = new[]
            {
                SensorDescriptor(
                    GameplayTriggerPlayableIds.CheckpointSourceBinding,
                    GameplayTriggerPlayableIds.CheckpointQuery,
                    new FixVector3(
                        Fix64.FromRatio(-25, 10),
                        -Fix64.One,
                        -Fix64.One),
                    new FixVector3(
                        Fix64.FromRatio(-15, 10),
                        Fix64.One,
                        Fix64.One)),
                SensorDescriptor(
                    GameplayTriggerPlayableIds.DoorSourceBinding,
                    GameplayTriggerPlayableIds.DoorQuery,
                    new FixVector3(
                        Fix64.FromRatio(-9, 10),
                        -Fix64.One,
                        -Fix64.One),
                    new FixVector3(
                        Fix64.FromRatio(-2, 10),
                        Fix64.One,
                        Fix64.One)),
                SensorDescriptor(
                    GameplayTriggerPlayableIds.TrapSourceBinding,
                    GameplayTriggerPlayableIds.TrapQuery,
                    new FixVector3(
                        Fix64.FromRatio(25, 10),
                        -Fix64.One,
                        -Fix64.One),
                    new FixVector3(
                        Fix64.FromRatio(35, 10),
                        Fix64.One,
                        Fix64.One))
            };
            if (!GameplayTriggerCombatSensorCatalog.TryCreate(
                    descriptors,
                    plan,
                    out GameplayTriggerCombatSensorCatalog sensorCatalog,
                    out GameplayTriggerCombatSensorCatalogResult catalogResult))
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable Combat sensor catalog failed: " +
                    catalogResult.ErrorCode);
            }
            if (!GameplayTriggerCombatSensorSystem.TryCreate(
                    sensorCatalog,
                    runtime,
                    physicsWorld,
                    entityMap,
                    componentWorld,
                    out GameplayTriggerCombatSensorSystem sensor,
                    out GameplayTriggerCombatSensorInitializationResult result))
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable Combat sensor composition failed: " +
                    result.ErrorCode);
            }
            return sensor;
        }

        private static GameplayTriggerCombatSensorDescriptor SensorDescriptor(
            int sourceBindingId,
            int queryId,
            FixVector3 min,
            FixVector3 max)
        {
            var header = new CombatQueryHeader(
                queryId,
                CombatQueryKind.Aabb,
                CombatEntityId.None,
                traceId: queryId,
                actionId: queryId,
                sourceOrder: queryId,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    GameplayTriggerPlayableCombatLayers.Actor));
            return new GameplayTriggerCombatSensorDescriptor(
                new GameplayTriggerSourceBindingId(sourceBindingId),
                GameplayTriggerPlayableIds.CombatSourceProvider,
                CombatPhysicsQuery.From(new CombatAabbQuery(header, min, max)),
                maxQueryHits: 8,
                maxMappedSubjects: 1);
        }

        private static void AddAabb(
            CombatPhysicsWorld world,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax)
        {
            var combatBodyId = new CombatBodyId(bodyId);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entityId),
                combatBodyId,
                position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                combatBodyId,
                new CombatColliderId(colliderId),
                layer,
                localMin,
                localMax));
        }

        private static FixVector3 V(int x, int y, int z) =>
            new FixVector3(
                Fix64.FromInt(x),
                Fix64.FromInt(y),
                Fix64.FromInt(z));

        private static StoryDirector CreateStoryDirector()
        {
            var director = new StoryDirector();
            StoryLoadGraphResult load = director.TryLoadGraph(
                new StoryGraphDefinition(
                    GameplayTriggerPlayableIds.StoryGraph,
                    version: 1,
                    GameplayTriggerPlayableIds.StoryWaitingBeat,
                    new[]
                    {
                        new StoryBeatDefinition(
                            GameplayTriggerPlayableIds.StoryWaitingBeat,
                            Array.Empty<StoryStepDefinition>()),
                        new StoryBeatDefinition(
                            GameplayTriggerPlayableIds.StoryOpenBeat,
                            new[]
                            {
                                new StoryStepDefinition(
                                    GameplayTriggerPlayableIds.StoryOpenStep,
                                    StoryStepKind.SetFact,
                                    factKey: DoorOpenFact,
                                    factValue: StoryValue.FromBool(true))
                            },
                            triggerIds: new[]
                            {
                                GameplayTriggerPlayableIds.StoryOpenTrigger
                            })
                    }));
            if (!load.Success)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable Story graph failed to load: " + load.Code);
            return director;
        }

        private static GameplayBuffDefinitionRegistry CreateBuffDefinitions()
        {
            const int maxEffectId = 2;
            ValidateEffectBearingBuffId(
                GameplayTriggerPlayableIds.TrapSlowBuff,
                maxEffectId);
            return new GameplayBuffDefinitionRegistry(
                new GameplayBuffDefinition(
                    GameplayTriggerPlayableIds.TrapCounterBuff,
                    "demo.gameplay-trigger.trap-counter",
                    defaultDurationFrames: 0L,
                    maxStackCount: 8,
                    stackPolicy: GameplayBuffStackPolicy.Stack,
                    effects: Array.Empty<GameplayBuffEffectDefinition>(),
                    isPermanent: true),
                new GameplayBuffDefinition(
                    GameplayTriggerPlayableIds.TrapSlowBuff,
                    "demo.gameplay-trigger.trap-slow",
                    defaultDurationFrames: 30L,
                    maxStackCount: 1,
                    stackPolicy: GameplayBuffStackPolicy.RefreshDuration,
                    effects: new[]
                    {
                        GameplayBuffEffectDefinition.AttributeModifier(
                            effectId: 1,
                            GameplayTriggerPlayableIds.MoveSpeedAttribute,
                            addValue: -2),
                        GameplayBuffEffectDefinition.Status(
                            effectId: 2,
                            new GameplayStatusId(
                                GameplayTriggerPlayableIds.SlowedStatus))
                    }));
        }

        private static void ValidateEffectBearingBuffId(int buffId, int maxEffectId)
        {
            int maximumBuffId = (int.MaxValue - maxEffectId) / 1000;
            if (buffId <= 0 || maxEffectId <= 0 || buffId > maximumBuffId)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable effect-bearing Buff id exceeds the " +
                    "Gameplay projection modifier-id range.");
            }
        }

        private static StoryFactKey DoorOpenFact => new StoryFactKey(
            GameplayTriggerPlayableIds.StoryFactNamespace,
            GameplayTriggerPlayableIds.StoryDoorOpenFact);

        private bool IsDoorOpen()
        {
            return _storyDirector.Blackboard.TryGet(DoorOpenFact, out StoryValue value) &&
                   value.Kind == StoryValueKind.Bool &&
                   value.AsBool();
        }

        private int GetTrapActivationCount()
        {
            if (!_componentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayComponentBuffSetComponent> store) ||
                !store.TryGet(_playerEntity, out GameplayComponentBuffSetComponent buffs) ||
                !buffs.TryGet(
                    GameplayTriggerPlayableIds.TrapCounterBuff,
                    out GameplayComponentBuffEntry entry))
            {
                return 0;
            }
            return entry.StackCount;
        }

        private int GetSlowFramesRemaining()
        {
            if (!_componentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayComponentBuffSetComponent> store) ||
                !store.TryGet(_playerEntity, out GameplayComponentBuffSetComponent buffs) ||
                !buffs.TryGet(
                    GameplayTriggerPlayableIds.TrapSlowBuff,
                    out GameplayComponentBuffEntry entry) ||
                entry.IsPermanent)
            {
                return 0;
            }
            long remaining = entry.EndFrame - _nextFrame;
            return remaining <= 0L ? 0 : remaining > int.MaxValue
                ? int.MaxValue
                : (int)remaining;
        }

        private bool IsSourceEnabled(int sourceBindingId)
        {
            return _triggerRuntime.TryGetSourceEnabled(
                       new GameplayTriggerSourceBindingId(sourceBindingId),
                       out bool enabled) &&
                   enabled;
        }

        private bool IsCheckpointAccepted()
        {
            if (IsSourceEnabled(GameplayTriggerPlayableIds.DoorSourceBinding))
                return true;

            for (int i = 0; i < _triggerRuntime.DeferredRecordCount; i++)
            {
                GameplayTriggerDeferredRecord record =
                    _triggerRuntime.GetDeferredRecord(i);
                if (record.OperationKind ==
                        GameplayTriggerInternalDeferredOperationKind.SetEnabled &&
                    record.Enabled &&
                    record.ActionKindId == GameplayTriggerPlayableIds.SetEnabledAction &&
                    record.OriginBindingId.Value ==
                        GameplayTriggerPlayableIds.CheckpointBinding &&
                    record.OriginRouteId.Value ==
                        GameplayTriggerPlayableIds.CheckpointRoute &&
                    record.TargetSourceBindingId.Value ==
                        GameplayTriggerPlayableIds.DoorSourceBinding)
                {
                    return true;
                }
            }
            return false;
        }

        internal void RestoreDemoState(
            long nextFrame,
            long inputSequence,
            long lastConsumedCommandSequence,
            in MxFramework.Combat.Motion.CombatMotionState motionState,
            RuntimeCommandBufferSnapshot storyCommands,
            RuntimeCommandBufferSnapshot gameplayCommands,
            RuntimeCommandBufferSnapshot demoCommands)
        {
            _storyCommands.RestoreSnapshot(storyCommands);
            _gameplayCommands.RestoreSnapshot(gameplayCommands);
            _demoCommands.RestoreSnapshot(demoCommands);
            _nextFrame = nextFrame;
            _inputSequence = inputSequence;
            _simulation.RestoreAuthority(
                in motionState,
                lastConsumedCommandSequence);
            _combatSensor.ResetSchedulingState();
        }

        private RuntimeSaveStateCoordinator CreateSaveStateCoordinator(
            RuntimeSaveStateRestoreProfile restoreProfile,
            GameplayTriggerRuntimeSaveStateParticipant triggerSave,
            GameplayTriggerPlayableSaveStateParticipant demoSave)
        {
            var descriptor = new RuntimeSaveStateRootHeaderDescriptor(
                RuntimeSaveState.CurrentSchemaVersion,
                "mxframework-19.4a",
                "gameplay-trigger-playable-v1",
                "embedded-demo-assets-v1");
            var registry = new RuntimeSaveStateRegistry();
            RequireSaveRegistration(registry.Register(
                new RuntimeSaveStateRootHeaderProvider(descriptor).CreateParticipant()));

            var gameplaySave = new GameplayComponentWorldSaveStateProvider(
                _componentWorld,
                GetCompletedFrame);
            RequireSaveRegistration(registry.Register(
                GameplayComponentWorldSaveStateProvider.ModuleId,
                order: 100,
                gameplaySave,
                gameplaySave));
            RequireSaveRegistration(registry.Register(
                GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
                triggerSave,
                triggerSave));

            var storySave = new StoryRuntimeSaveStateProvider(
                _storyDirector,
                GetCompletedFrame);
            RequireSaveRegistration(registry.Register(
                StoryRuntimeSaveStateProvider.ModuleId,
                order: 300,
                storySave,
                storySave));
            RequireSaveRegistration(registry.Register(
                GameplayTriggerPlayableSaveStateParticipant.ParticipantId,
                GameplayTriggerPlayableSaveStateParticipant.ParticipantOrder,
                demoSave,
                demoSave));

            RuntimeSaveStateCoordinatorFactoryResult factory = restoreProfile == null
                ? RuntimeSaveStateCoordinator.CreateAuthoritative(registry)
                : RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(
                    registry,
                    restoreProfile);
            if (!factory.Success)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable SaveState composition failed: " +
                    (factory.Error != null ? factory.Error.ToString() : "unknown"));
            }
            return factory.Coordinator;
        }

        private long GetCompletedFrame()
        {
            return _nextFrame == 0L ? 0L : _nextFrame - 1L;
        }

        private static void RegisterGameplaySchemas(
            GameplayComponentSchemaRegistry schemas)
        {
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterRuntimeHash(schemas);
            GameplayComponentBuffSchemaDescriptors.RegisterSaveState(schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterRuntimeHash(schemas);
            GameplayComponentModifierSchemaDescriptors.RegisterSaveState(schemas);
        }

        private static void RequireSaveRegistration(
            RuntimeSaveStateRegistryResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable SaveState participant registration failed: " +
                    result.Error);
            }
        }

        private static void RequireQuiescence(
            RuntimeCheckpointQuiescenceRegistryResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable quiescence participant registration failed: " +
                    result.Error);
            }
        }

        private static bool NeverSwapping()
        {
            return false;
        }

        private sealed class RestoredGameplayEntityValidator :
            IGameplayTriggerRestoreEntityValidator
        {
            private readonly GameplayComponentWorld _world;

            internal RestoredGameplayEntityValidator(GameplayComponentWorld world)
            {
                _world = world;
            }

            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                return _world.IsAlive(entityId);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayTriggerPlayableComposition));
        }
    }
}
