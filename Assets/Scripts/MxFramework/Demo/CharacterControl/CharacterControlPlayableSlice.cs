using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.Animation;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Animation;
using MxFramework.CharacterControl.Input;
using MxFramework.CharacterControl.RuntimeAiPlannerBridge;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;
using MxInput = MxFramework.Input;

namespace MxFramework.Demo.CharacterControl
{
    public sealed class CharacterControlPlayableSlice : IDisposable
    {
        public const int LightAttackId = 1001;
        public const int MoveCommandId = 71001;
        public const int JumpCommandId = 71002;
        public const int AttackCommandId = 71003;
        public const int PressureBreakCommandId = 71004;
        public const int RuntimeAiStepCommandId = 71005;

        public const int LocalInputSourceId = 1;
        public const int RuntimeAiSourceId = 2;
        public const int UiCommandSourceId = 10;

        private const double StepSeconds = 1d / 60d;
        private const int AxisScale = 1000;
        private const int ReactionFrames = 4;

        private static readonly CharacterControlEntityRef DemoEntity =
            CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1);

        private readonly RuntimeReplayHeader _replayHeader = new RuntimeReplayHeader(
            schemaVersion: 1,
            frameworkVersion: "character-control-playable-v1",
            configHash: "character-control-playable-demo",
            resourceCatalogHash: "demo-inline",
            startFrame: RuntimeFrame.Zero);

        private RuntimeHost _host;
        private RuntimeClock _clock;
        private RuntimeCommandBuffer _commands;
        private RuntimeReplayRecorder _replayRecorder;
        private CharacterControlStateMachine _stateMachine;
        private CombatActionRunner _combatActionRunner;
        private CharacterActionController _actionController;
        private CharacterPressureReactionController _pressureController;
        private CharacterMotionResolver _motionResolver;
        private CombatPhysicsWorld _physicsWorld;
        private CombatMotionState _motionState;
        private MxInput.FakeInputProvider _inputProvider;
        private InputCharacterCommandSource _inputSource;
        private RuntimeAiPlannerCharacterCommandSource _runtimeAiSource;
        private RecordingAnimationBackend _animationBackend;
        private CharacterAnimationPresentationController _animationController;
        private CharacterControlDebugSource _debugSource;
        private FrameworkDebugSourceRegistry _debugRegistry;
        private DebugUiSnapshotAggregator _debugAggregator;
        private CharacterControlPlayableSnapshot _snapshot;
        private CharacterCommand _lastCommand;
        private CharacterMotionResult _lastMotion;
        private CharacterActionResult _lastActionResult;
        private CharacterActionEvent _lastActionEvent;
        private CharacterPressureReactionResult _lastPressureResult;
        private RuntimeFrame _lastProcessedFrame;
        private long _elapsedFrames;
        private bool _disposed;

        public CharacterControlPlayableSlice()
        {
            ResetAll();
        }

        public CharacterControlEntityRef Entity => DemoEntity;

        public CharacterControlPlayableSnapshot CurrentSnapshot => _snapshot;

        public RuntimeReplaySnapshot ReplaySnapshot => _replayRecorder.CreateSnapshot();

        public RuntimeCommandValidationResult EnqueueMove(float x, float z, bool sprintHeld = false)
        {
            return Enqueue(new RuntimeCommand(
                _clock.CurrentFrame,
                UiCommandSourceId,
                MoveCommandId,
                DemoEntity.StableId,
                QuantizeAxis(x),
                QuantizeAxis(z),
                sprintHeld ? 1 : 0,
                "character-control-demo:move"));
        }

        public RuntimeCommandValidationResult EnqueueJump()
        {
            return EnqueueButton(JumpCommandId, "character-control-demo:jump");
        }

        public RuntimeCommandValidationResult EnqueueAttack()
        {
            return EnqueueButton(AttackCommandId, "character-control-demo:attack");
        }

        public RuntimeCommandValidationResult EnqueuePressureBreak()
        {
            return EnqueueButton(PressureBreakCommandId, "character-control-demo:pressure-break");
        }

        public RuntimeCommandValidationResult EnqueueRuntimeAiStep()
        {
            return EnqueueButton(RuntimeAiStepCommandId, "character-control-demo:runtime-ai-step");
        }

        public CharacterControlPlayableSnapshot Tick()
        {
            ThrowIfDisposed();
            RuntimeFrame frame = _clock.CurrentFrame;
            _host.Tick(frame.Value, StepSeconds, _elapsedFrames * StepSeconds);
            _elapsedFrames++;
            _clock.Step();
            return _snapshot;
        }

        public CharacterControlPlayableSnapshot Tick(int frameCount)
        {
            if (frameCount < 0)
                throw new ArgumentOutOfRangeException(nameof(frameCount));

            CharacterControlPlayableSnapshot latest = _snapshot;
            for (int i = 0; i < frameCount; i++)
                latest = Tick();

            return latest;
        }

        public void ResetAll()
        {
            ThrowIfDisposed();
            Cleanup();

            _clock = new RuntimeClock(RuntimeFrame.Zero);
            _commands = new RuntimeCommandBuffer();
            _replayRecorder = new RuntimeReplayRecorder(_replayHeader);
            _stateMachine = new CharacterControlStateMachine(DemoEntity);
            _combatActionRunner = CreateCombatActionRunner();
            _actionController = new CharacterActionController(_stateMachine, _combatActionRunner);
            _pressureController = new CharacterPressureReactionController(
                _stateMachine,
                _actionController,
                new CharacterPressureReactionPolicy
                {
                    PostureBreakReactionFrames = ReactionFrames,
                    PostureBreakLockMask =
                        CharacterControlLockMask.Move
                        | CharacterControlLockMask.Jump
                        | CharacterControlLockMask.Action
                });
            _motionResolver = new CharacterMotionResolver(new CombatKinematicMotor(CombatMotionConfig.Default));
            _physicsWorld = CreatePhysicsWorld();
            _motionState = CreateInitialMotionState();
            _inputProvider = new MxInput.FakeInputProvider();
            _inputProvider.SetContext(MxInput.InputContext.Gameplay);
            _inputSource = CreateInputSource(_inputProvider);
            _runtimeAiSource = CreateRuntimeAiSource();
            _animationBackend = new RecordingAnimationBackend();
            _animationController = CreateAnimationController(_animationBackend);
            _debugSource = new CharacterControlDebugSource(
                _stateMachine,
                actionController: _actionController,
                pressureReactionController: _pressureController,
                maxRecentEvents: 16);
            _debugRegistry = new FrameworkDebugSourceRegistry();
            _debugRegistry.Register(_debugSource);
            _debugAggregator = new DebugUiSnapshotAggregator();
            _lastCommand = default;
            _lastMotion = default;
            _lastActionResult = default;
            _lastActionEvent = default;
            _lastPressureResult = default;
            _lastProcessedFrame = RuntimeFrame.Zero;
            _elapsedFrames = 0;

            _stateMachine.StateChanged += OnStateChanged;
            _actionController.ActionEvent += OnActionEvent;

            _host = new RuntimeHost();
            _host.RegisterModule(new SliceSimulationModule(this));
            _host.Initialize();
            _host.Start();
            _snapshot = BuildSnapshot(RuntimeFrame.Zero, 0, 0);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Cleanup();
            _disposed = true;
        }

        private RuntimeCommandValidationResult EnqueueButton(int commandId, string traceId)
        {
            return Enqueue(new RuntimeCommand(
                _clock.CurrentFrame,
                UiCommandSourceId,
                commandId,
                DemoEntity.StableId,
                traceId: traceId));
        }

        private RuntimeCommandValidationResult Enqueue(RuntimeCommand command)
        {
            ThrowIfDisposed();
            return _commands.Enqueue(command);
        }

        private void ProcessFrame(RuntimeFrame frame)
        {
            IReadOnlyList<RuntimeCommand> drained = _commands.DrainForFrame(frame);
            DemoFrameInput input = DemoFrameInput.FromCommands(drained);
            _inputProvider.SetSnapshot(input.ToSnapshot());

            if (_pressureController.TryFinishExpiredReaction(frame, out CharacterPressureReactionResult expired))
                _lastPressureResult = expired;

            CharacterCommand command;
            bool hasCommand = input.RuntimeAiStep
                ? _runtimeAiSource.TryGetCommand(frame, DemoEntity, out command)
                : _inputSource.TryGetCommand(frame, DemoEntity, out command);

            if (hasCommand)
                ProcessCharacterCommand(command);

            if (input.PressureBreak)
            {
                _lastPressureResult = _pressureController.Apply(new PostureBreakEvent(
                    frame,
                    DemoEntity.GameplayEntityId,
                    PressureBand.Critical,
                    previousValue: 80,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 20,
                    sourceId: UiCommandSourceId,
                    reason: "playable-demo",
                    traceId: "character-control-demo:pressure-break:" + frame.Value));
            }

            _combatActionRunner.TickActions(new CombatFrame(ToCombatFrame(frame)));
            _animationBackend.Tick((float)StepSeconds);

            long hash = ComputeRuntimeHash(frame);
            string diagnostics = BuildDiagnosticsSummary(frame);
            _replayRecorder.RecordFrame(frame, drained, hash, diagnostics);
            _lastProcessedFrame = frame;
            _snapshot = BuildSnapshot(frame, hash, drained.Count);
        }

        private void ProcessCharacterCommand(CharacterCommand command)
        {
            _lastCommand = command;
            _debugSource.RecordCommand(command);

            if (command.HasActionRequest)
                _lastActionResult = _actionController.Submit(command.ActionRequest);

            CharacterMotionResult motion = _motionResolver.Step(
                command,
                _stateMachine,
                _motionState,
                _physicsWorld);
            _motionState = motion.StepResult.State;
            _lastMotion = motion;
            _debugSource.RecordMotionResult(motion);
            _animationController.ApplyLocomotion(motion);
        }

        private CharacterControlPlayableSnapshot BuildSnapshot(
            RuntimeFrame frame,
            long runtimeHash,
            int drainedCommandCount)
        {
            DebugUiDashboardViewModel dashboard = _debugAggregator.Refresh(_debugRegistry);
            FrameworkDebugSnapshot debugSnapshot = _debugSource.CreateSnapshot();
            string report = FrameworkDebugReportExporter.ExportText(debugSnapshot);
            CharacterAnimationPresentationDiagnosticSnapshot animationSnapshot =
                _animationController.CreateSnapshot();
            return new CharacterControlPlayableSnapshot(
                frame,
                _stateMachine.CurrentState,
                _stateMachine.ControlLockMask,
                _motionState.Position,
                _motionState.Velocity,
                _motionState.Grounded,
                _motionState.CollisionFlags,
                _lastCommand,
                _lastMotion,
                _lastActionResult,
                _lastActionEvent,
                _lastPressureResult,
                animationSnapshot.LastEntry,
                runtimeHash,
                _replayRecorder.Count,
                drainedCommandCount,
                dashboard,
                report);
        }

        private long ComputeRuntimeHash(RuntimeFrame frame)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddLong("frame", frame.Value);
            accumulator.AddInt("state", (int)_stateMachine.CurrentState);
            accumulator.AddInt("lock", (int)_stateMachine.ControlLockMask);
            accumulator.AddLong("pos.x", _motionState.Position.X.RawValue);
            accumulator.AddLong("pos.y", _motionState.Position.Y.RawValue);
            accumulator.AddLong("pos.z", _motionState.Position.Z.RawValue);
            accumulator.AddLong("vel.x", _motionState.Velocity.X.RawValue);
            accumulator.AddLong("vel.y", _motionState.Velocity.Y.RawValue);
            accumulator.AddLong("vel.z", _motionState.Velocity.Z.RawValue);
            accumulator.AddInt("grounded", _motionState.Grounded ? 1 : 0);
            accumulator.AddInt("lastSource", _lastCommand.SourceId);
            accumulator.AddInt("lastActionKind", (int)_lastCommand.ActionRequest.Kind);
            accumulator.AddInt("lastActionId", _lastCommand.ActionRequest.CombatActionId);
            accumulator.AddInt("pressureActive", _pressureController.HasActiveReaction ? 1 : 0);
            accumulator.AddInt("actionPhase", (int)_combatActionRunner.GetCurrentPhase(DemoEntity.CombatEntityId));
            return accumulator.ToHash();
        }

        private string BuildDiagnosticsSummary(RuntimeFrame frame)
        {
            return "state=" + _stateMachine.CurrentState
                + " lock=" + _stateMachine.ControlLockMask
                + " source=" + DescribeSource(_lastCommand.SourceId)
                + " action=" + _lastCommand.ActionRequest.Kind
                + " pressure=" + _lastPressureResult.Kind
                + " hashFrame=" + frame.Value;
        }

        private void OnStateChanged(CharacterStateChangedEvent evt)
        {
            _animationController.ApplyStateChanged(evt);
        }

        private void OnActionEvent(CharacterActionEvent evt)
        {
            _lastActionEvent = evt;
            _animationController.RecordActionEvent(evt);
        }

        private void Cleanup()
        {
            if (_stateMachine != null)
                _stateMachine.StateChanged -= OnStateChanged;
            if (_actionController != null)
                _actionController.ActionEvent -= OnActionEvent;

            _debugSource?.Dispose();
            _actionController?.Dispose();
            _animationBackend?.Dispose();
            _host?.Dispose();

            _host = null;
            _debugSource = null;
            _actionController = null;
            _animationBackend = null;
        }

        private static InputCharacterCommandSource CreateInputSource(MxInput.IInputProvider provider)
        {
            return new InputCharacterCommandSource(provider, new InputCharacterCommandSourceOptions
            {
                SourceId = LocalInputSourceId,
                UseLookAsFacing = false,
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.CombatAction(
                        MxInput.InputIntent.AttackPrimary,
                        CharacterActionKind.Attack,
                        LightAttackId,
                        queueIfBusy: true),
                    CharacterInputActionBinding.Cancel(MxInput.InputIntent.Cancel)
                },
                TracePrefix = "character-control-local-input"
            });
        }

        private static RuntimeAiPlannerCharacterCommandSource CreateRuntimeAiSource()
        {
            var world = new AiWorldState();
            var firstAction = new DemoAiAction(1);
            var secondAction = new DemoAiAction(2);
            var registry = new RuntimeAiCharacterCommandProfileRegistry();
            RegisterRuntimeAiAttackProfile(registry, firstAction.Id);
            RegisterRuntimeAiAttackProfile(registry, secondAction.Id);

            return new RuntimeAiPlannerCharacterCommandSource(
                world,
                new DemoAiPlanner(firstAction, secondAction),
                new[] { new DemoAiGoal() },
                new IAiAction[] { firstAction, secondAction },
                registry,
                new RuntimeAiPlannerCharacterCommandSourceOptions
                {
                    SourceId = RuntimeAiSourceId,
                    RequireTargetFacts = false,
                    MinDecisionIntervalFrames = 1,
                    TracePrefix = "character-control-runtime-ai-planner"
                });
        }

        private static void RegisterRuntimeAiAttackProfile(
            RuntimeAiCharacterCommandProfileRegistry registry,
            int actionId)
        {
            registry.Register(new RuntimeAiCharacterCommandProfile(
                actionId: actionId,
                moveDirection: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                facingBasis: CharacterFacingBasis.Identity,
                actionKind: CharacterActionKind.Attack,
                combatActionId: LightAttackId,
                forceStart: false,
                queueIfBusy: true,
                traceTag: "attack"));
        }

        private static CharacterAnimationPresentationController CreateAnimationController(
            IMxAnimationBackend backend)
        {
            var options = new CharacterAnimationPresentationOptions
            {
                TargetActorId = "character-control-demo",
                LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D,
                LocomotionBlend2DId = "character-control.locomotion"
            };
            options.ReactionBindings.Add(new CharacterAnimationReactionBinding
            {
                Reason = CharacterControlTransitionReason.PressureBreak,
                RequestKind = CharacterAnimationReactionRequestKind.CrossFade,
                BindingId = "character-control.pressure-break",
                ClipKey = new ResourceKey("character_control.pressure_break", ResourceTypeIds.AnimationClip),
                FadeDurationSeconds = 0.08f
            });
            return new CharacterAnimationPresentationController(backend, options);
        }

        private static CombatActionRunner CreateCombatActionRunner()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(LightAttackId, new CombatActionTimeline(
                LightAttackId,
                totalFrames: 4,
                startup: new CombatFrameRange(0, 0),
                active: new CombatFrameRange(1, 1),
                recovery: new CombatFrameRange(2, 3),
                windows: null,
                events: null));
            return new CombatActionRunner(registry);
        }

        private static CombatPhysicsWorld CreatePhysicsWorld()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(
                DemoEntity.CombatEntityId,
                DemoEntity.CombatBodyId,
                CreateInitialPosition()));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(99), new CombatBodyId(99), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(99),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20))));
            return world;
        }

        private static CombatMotionState CreateInitialMotionState()
        {
            return new CombatMotionState(
                CombatFrame.Zero,
                CreateInitialPosition(),
                FixVector3.Zero,
                grounded: true,
                lastCollisionNormal: FixVector3.Zero,
                collisionFlags: CombatMotionCollisionFlags.Grounded);
        }

        private static FixVector3 CreateInitialPosition()
        {
            return new FixVector3(Fix64.Zero, Fix64.FromRatio(9, 10), Fix64.Zero);
        }

        private static int ToCombatFrame(RuntimeFrame frame)
        {
            if (frame.Value > int.MaxValue)
                throw new InvalidOperationException("Runtime frame is too large to convert to CombatFrame.");

            return (int)frame.Value;
        }

        private static int QuantizeAxis(float value)
        {
            float clamped = Math.Max(-1f, Math.Min(1f, value));
            return (int)Math.Round(clamped * AxisScale, MidpointRounding.AwayFromZero);
        }

        private static Fix64 ToFix(int quantized)
        {
            return Fix64.FromRaw((long)quantized * Fix64.Scale / AxisScale);
        }

        private static string DescribeSource(int sourceId)
        {
            switch (sourceId)
            {
                case LocalInputSourceId:
                    return "Local Input";
                case RuntimeAiSourceId:
                    return "Runtime AI Planner";
                case UiCommandSourceId:
                    return "UI Command";
                default:
                    return sourceId == 0 ? "None" : "Source " + sourceId;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterControlPlayableSlice));
        }

        private readonly struct DemoFrameInput
        {
            private DemoFrameInput(
                int moveX,
                int moveZ,
                bool sprintHeld,
                bool jumpPressed,
                bool attackPressed,
                bool pressureBreak,
                bool runtimeAiStep)
            {
                MoveX = moveX;
                MoveZ = moveZ;
                SprintHeld = sprintHeld;
                JumpPressed = jumpPressed;
                AttackPressed = attackPressed;
                PressureBreak = pressureBreak;
                RuntimeAiStep = runtimeAiStep;
            }

            public int MoveX { get; }
            public int MoveZ { get; }
            public bool SprintHeld { get; }
            public bool JumpPressed { get; }
            public bool AttackPressed { get; }
            public bool PressureBreak { get; }
            public bool RuntimeAiStep { get; }

            public MxInput.InputSnapshot ToSnapshot()
            {
                var move = new Vector2(MoveX / (float)AxisScale, MoveZ / (float)AxisScale);
                Vector2 look = move.sqrMagnitude > 0f ? move.normalized : Vector2.up;
                return new MxInput.InputSnapshot(
                    move,
                    look,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    0f,
                    JumpPressed,
                    jumpHeld: JumpPressed,
                    jumpReleased: false,
                    AttackPressed,
                    attackPrimaryHeld: AttackPressed,
                    attackSecondaryPressed: false,
                    interactPressed: false,
                    dodgePressed: false,
                    SprintHeld,
                    submitPressed: false,
                    cancelPressed: false,
                    pausePressed: false,
                    debugTogglePressed: false);
            }

            public static DemoFrameInput FromCommands(IReadOnlyList<RuntimeCommand> commands)
            {
                int moveX = 0;
                int moveZ = 0;
                bool sprintHeld = false;
                bool jumpPressed = false;
                bool attackPressed = false;
                bool pressureBreak = false;
                bool runtimeAiStep = false;

                for (int i = 0; i < commands.Count; i++)
                {
                    RuntimeCommand command = commands[i];
                    switch (command.CommandId)
                    {
                        case MoveCommandId:
                            moveX = command.Payload0;
                            moveZ = command.Payload1;
                            sprintHeld = command.Payload2 != 0;
                            break;
                        case JumpCommandId:
                            jumpPressed = true;
                            break;
                        case AttackCommandId:
                            attackPressed = true;
                            break;
                        case PressureBreakCommandId:
                            pressureBreak = true;
                            break;
                        case RuntimeAiStepCommandId:
                            runtimeAiStep = true;
                            break;
                    }
                }

                return new DemoFrameInput(
                    moveX,
                    moveZ,
                    sprintHeld,
                    jumpPressed,
                    attackPressed,
                    pressureBreak,
                    runtimeAiStep);
            }
        }

        private sealed class SliceSimulationModule : IRuntimeModule
        {
            private readonly CharacterControlPlayableSlice _owner;

            public SliceSimulationModule(CharacterControlPlayableSlice owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public string ModuleId => "character-control.playable-slice";
            public int Priority => 0;
            public RuntimeTickStage TickStage => RuntimeTickStage.Simulation;

            public void Initialize(RuntimeHostContext context)
            {
            }

            public void Start(RuntimeHostContext context)
            {
            }

            public void Tick(RuntimeTickContext context)
            {
                _owner.ProcessFrame(new RuntimeFrame(context.FrameIndex));
            }

            public void Stop(RuntimeHostContext context)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class DemoAiGoal : IAiGoal
        {
            public int Id => 1;
            public float Priority => 1f;

            public bool IsRelevant(IAiWorldState worldState)
            {
                return true;
            }

            public bool IsSatisfied(IAiWorldState worldState)
            {
                return false;
            }
        }

        private sealed class DemoAiAction : IAiAction
        {
            public DemoAiAction(int id)
            {
                Id = id;
            }

            public int Id { get; }
            public float Cost => 1f;
            public IReadOnlyList<IAiCondition> Preconditions => Array.Empty<IAiCondition>();
            public IReadOnlyList<IAiEffect> Effects => Array.Empty<IAiEffect>();

            public bool CanExecute(IAiWorldState worldState)
            {
                return true;
            }

            public void Apply(IAiWorldState worldState)
            {
            }
        }

        private sealed class DemoAiPlanner : IAiPlanner
        {
            private readonly IAiAction[] _actions;
            private int _nextActionIndex;

            public DemoAiPlanner(params IAiAction[] actions)
            {
                if (actions == null || actions.Length == 0)
                    throw new ArgumentException("At least one demo action is required.", nameof(actions));

                _actions = new IAiAction[actions.Length];
                for (int i = 0; i < actions.Length; i++)
                    _actions[i] = actions[i] ?? throw new ArgumentNullException(nameof(actions));
            }

            public bool TryPlan(
                IAiWorldState worldState,
                IEnumerable<IAiGoal> goals,
                IEnumerable<IAiAction> actions,
                out AiPlan plan)
            {
                IAiAction action = _actions[_nextActionIndex];
                _nextActionIndex = (_nextActionIndex + 1) % _actions.Length;
                plan = new AiPlan(new DemoAiGoal(), new[] { action }, action.Cost);
                return true;
            }
        }

        private sealed class RecordingAnimationBackend : IMxAnimationBackend
        {
            private readonly List<string> _recentRequests = new List<string>();
            private bool _released;

            public string BackendName => "CharacterControlPlayableRecordingBackend";
            public string LastRequest { get; private set; } = string.Empty;

            public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
            {
                return Record("Play", request != null ? request.ClipKey : default);
            }

            public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
            {
                return Record("Stop", default);
            }

            public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
            {
                return Record("CrossFade", request != null ? request.ClipKey : default);
            }

            public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
            {
                return Record("SetLayerWeight", default);
            }

            public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
            {
                return Record("SetBlend1D", default);
            }

            public MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request)
            {
                return Record("SetBlend2D", default);
            }

            public void Tick(float deltaTime)
            {
            }

            public MxAnimationDiagnosticSnapshot CreateSnapshot()
            {
                return new MxAnimationDiagnosticSnapshot(
                    BackendName,
                    "character-control-demo",
                    "character-control-playable",
                    actorCount: 1,
                    graphIsValid: !_released,
                    isReleased: _released,
                    defaultClip: null,
                    fallbackClip: null,
                    layerStates: Array.Empty<MxAnimationLayerDiagnostic>(),
                    activeFades: Array.Empty<MxAnimationFadeDiagnostic>(),
                    recentRequests: Array.Empty<MxAnimationRequestDiagnostic>(),
                    recentResourceErrors: Array.Empty<ResourceError>());
            }

            public void Release()
            {
                _released = true;
            }

            public void Dispose()
            {
                Release();
            }

            private MxAnimationBackendResult Record(string requestKind, ResourceKey clipKey)
            {
                if (_released)
                    return MxAnimationBackendResult.Failed(MxAnimationBackendResultCode.BackendReleased, clipKey, "Backend released.");

                LastRequest = requestKind;
                _recentRequests.Add(requestKind);
                while (_recentRequests.Count > 8)
                    _recentRequests.RemoveAt(0);

                return MxAnimationBackendResult.Succeeded(clipKey, requestKind);
            }
        }
    }

    public readonly struct CharacterControlPlayableSnapshot
    {
        public CharacterControlPlayableSnapshot(
            RuntimeFrame frame,
            CharacterControlState state,
            CharacterControlLockMask lockMask,
            FixVector3 position,
            FixVector3 velocity,
            bool grounded,
            CombatMotionCollisionFlags collisionFlags,
            CharacterCommand lastCommand,
            CharacterMotionResult lastMotion,
            CharacterActionResult lastActionResult,
            CharacterActionEvent lastActionEvent,
            CharacterPressureReactionResult lastPressureResult,
            CharacterAnimationPresentationDiagnosticEntry lastAnimation,
            long runtimeHash,
            int replayFrameCount,
            int drainedCommandCount,
            DebugUiDashboardViewModel debugDashboard,
            string debugReport)
        {
            Frame = frame;
            State = state;
            LockMask = lockMask;
            Position = position;
            Velocity = velocity;
            Grounded = grounded;
            CollisionFlags = collisionFlags;
            LastCommand = lastCommand;
            LastMotion = lastMotion;
            LastActionResult = lastActionResult;
            LastActionEvent = lastActionEvent;
            LastPressureResult = lastPressureResult;
            LastAnimation = lastAnimation;
            RuntimeHash = runtimeHash;
            ReplayFrameCount = replayFrameCount;
            DrainedCommandCount = drainedCommandCount;
            DebugDashboard = debugDashboard;
            DebugReport = debugReport ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public CharacterControlState State { get; }
        public CharacterControlLockMask LockMask { get; }
        public FixVector3 Position { get; }
        public FixVector3 Velocity { get; }
        public bool Grounded { get; }
        public CombatMotionCollisionFlags CollisionFlags { get; }
        public CharacterCommand LastCommand { get; }
        public CharacterMotionResult LastMotion { get; }
        public CharacterActionResult LastActionResult { get; }
        public CharacterActionEvent LastActionEvent { get; }
        public CharacterPressureReactionResult LastPressureResult { get; }
        public CharacterAnimationPresentationDiagnosticEntry LastAnimation { get; }
        public long RuntimeHash { get; }
        public int ReplayFrameCount { get; }
        public int DrainedCommandCount { get; }
        public DebugUiDashboardViewModel DebugDashboard { get; }
        public string DebugReport { get; }

        public string LastCommandSource
        {
            get
            {
                switch (LastCommand.SourceId)
                {
                    case CharacterControlPlayableSlice.LocalInputSourceId:
                        return "Local Input";
                    case CharacterControlPlayableSlice.RuntimeAiSourceId:
                        return "Runtime AI Planner";
                    case CharacterControlPlayableSlice.UiCommandSourceId:
                        return "UI Command";
                    default:
                        return LastCommand.SourceId == 0 ? "None" : "Source " + LastCommand.SourceId;
                }
            }
        }
    }
}
