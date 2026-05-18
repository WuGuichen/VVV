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
using MxFramework.Core.Math;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxInput = MxFramework.Input;

namespace MxFramework.Demo
{
    internal sealed class RuntimeCombatCharacterControlSlice : IDisposable
    {
        private const int LocalInputSourceId = 20001;
        private const int RuntimeAiPlannerSourceId = 20002;
        private const int GameplayAbilityId = 300001;
        private const int RuntimeAiPlannerActionId = 920001;

        private readonly CharacterControlEntityRef _entity;
        private readonly int _combatActionId;
        private readonly CharacterControlStateMachine _stateMachine;
        private readonly CombatActionRunner _actionRunner;
        private readonly RuntimeCommandBuffer _gameplayCommandBuffer;
        private readonly GameplayComponentAbilityRequestStore _abilityRequestStore;
        private readonly CharacterActionController _actionController;
        private readonly CharacterPressureReactionController _pressureReactionController;
        private readonly RecordingAnimationBackend _animationBackend;
        private readonly CharacterAnimationPresentationController _animationController;
        private readonly CharacterControlDebugSource _debugSource;
        private readonly RuntimeHost _runtimeHost;
        private readonly SliceRuntimeModule _runtimeModule;
        private readonly RuntimeAiPlannerCharacterCommandSource _runtimeAiPlannerSource;

        private MxInput.IInputProvider _localInputProvider;
        private InputCharacterCommandSource _localInputSource;
        private CharacterCommand _lastCommand;
        private CharacterMotionResult _lastMotionResult;
        private CharacterActionResult _lastActionResult;
        private CharacterPressureReactionResult _lastPressureResult;
        private string _lastSourceName = "none";
        private bool _hasLastCommand;
        private bool _hasLastMotionResult;
        private bool _hasLastActionResult;
        private bool _hasLastPressureResult;
        private bool _disposed;

        public RuntimeCombatCharacterControlSlice(int combatActionId)
        {
            _combatActionId = combatActionId;
            _entity = CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(1),
                new CombatBodyId(1),
                stableId: 1);
            _stateMachine = new CharacterControlStateMachine(_entity);
            _actionRunner = new CombatActionRunner(CreateCombatActionRegistry(combatActionId));
            _gameplayCommandBuffer = new RuntimeCommandBuffer();
            _abilityRequestStore = new GameplayComponentAbilityRequestStore();
            _actionController = new CharacterActionController(
                _stateMachine,
                _actionRunner,
                _gameplayCommandBuffer,
                _abilityRequestStore);
            _pressureReactionController = new CharacterPressureReactionController(
                _stateMachine,
                _actionController,
                new CharacterPressureReactionPolicy
                {
                    PostureBreakReactionFrames = 12,
                    GuardBreakReactionFrames = 8
                });
            _animationBackend = new RecordingAnimationBackend();
            _animationController = new CharacterAnimationPresentationController(
                _animationBackend,
                CreateAnimationOptions());
            _debugSource = new CharacterControlDebugSource(
                _stateMachine,
                "CombatShowcaseCharacterControl",
                _actionController,
                _pressureReactionController);
            _runtimeAiPlannerSource = CreateRuntimeAiPlannerSource(combatActionId);

            _actionController.ActionEvent += OnActionEvent;
            _stateMachine.StateChanged += OnStateChanged;

            _runtimeModule = new SliceRuntimeModule(
                _actionRunner,
                _gameplayCommandBuffer,
                _pressureReactionController,
                OnPressureResult);
            _runtimeHost = new RuntimeHost();
            _runtimeHost.RegisterModule(_runtimeModule);
            _runtimeHost.Initialize();
            _runtimeHost.Start();
        }

        public CharacterControlEntityRef Entity => _entity;

        public CharacterControlStateMachine StateMachine => _stateMachine;

        public int TotalGameplayCommandsDrained => _runtimeModule.TotalGameplayCommandsDrained;

        public bool TryReadLocalCommand(MxInput.IInputProvider inputProvider, RuntimeFrame frame, out CharacterCommand command)
        {
            command = default;
            if (inputProvider == null)
            {
                return false;
            }

            _lastSourceName = "Local Input";
            return EnsureLocalInputSource(inputProvider).TryGetCommand(frame, _entity, out command);
        }

        public bool TryReadRuntimeAiPlannerCommand(RuntimeFrame frame, out CharacterCommand command)
        {
            _lastSourceName = "Runtime AI Planner";
            return _runtimeAiPlannerSource.TryGetCommand(frame, _entity, out command);
        }

        public void RecordCommand(CharacterCommand command)
        {
            ThrowIfDisposed();
            _lastCommand = command;
            _hasLastCommand = true;
            _debugSource.RecordCommand(command);
        }

        public CharacterActionResult SubmitAction(CharacterActionRequest request)
        {
            ThrowIfDisposed();
            _lastActionResult = request.Kind == CharacterActionKind.None
                ? CharacterActionResult.Rejected(CharacterActionRejectedReason.InvalidRequest, "No character action request.")
                : _actionController.Submit(request);
            _hasLastActionResult = true;
            return _lastActionResult;
        }

        public void RecordMotion(CharacterMotionResult result)
        {
            ThrowIfDisposed();
            _lastMotionResult = result;
            _hasLastMotionResult = true;
            _debugSource.RecordMotionResult(result);
            _animationController.ApplyLocomotion(result);
        }

        public CharacterPressureReactionResult ApplyPostureBreak(RuntimeFrame frame, string traceId)
        {
            ThrowIfDisposed();
            CharacterPressureReactionResult result = _pressureReactionController.Apply(new PostureBreakEvent(
                frame,
                _entity.GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentPressure: 100,
                maxPressure: 100,
                delta: 20,
                sourceId: RuntimeAiPlannerSourceId,
                reason: "playable-vertical-slice",
                traceId: traceId));
            OnPressureResult(result);
            return result;
        }

        public void TickRuntime(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            _runtimeHost.Tick(frame.Value, 1d / 60d, frame.Value / 60d);
            _animationBackend.Tick(1f / 60f);
        }

        public FrameworkDebugSnapshot CreateDebugSnapshot()
        {
            ThrowIfDisposed();
            return _debugSource.CreateSnapshot();
        }

        public string BuildDebugReport()
        {
            return FrameworkDebugReportExporter.ExportText(CreateDebugSnapshot());
        }

        public string BuildSummary()
        {
            string command = _hasLastCommand
                ? $"command source={_lastSourceName} frame={_lastCommand.Frame} move={_lastCommand.MoveDirection} jump={_lastCommand.JumpPressed} action={_lastCommand.ActionRequest.Kind}"
                : "command=none";
            string motion = _hasLastMotionResult
                ? $"motion pos={_lastMotionResult.Position} grounded={_lastMotionResult.Grounded} flags={_lastMotionResult.CollisionFlags}"
                : "motion=none";
            string action = _hasLastActionResult
                ? $"action success={_lastActionResult.Success} queued={_lastActionResult.Queued} rejected={_lastActionResult.RejectedReason}"
                : "action=none";
            string pressure = _hasLastPressureResult
                ? $"pressure kind={_lastPressureResult.Kind} started={_lastPressureResult.ReactionStarted} active={_pressureReactionController.HasActiveReaction}"
                : "pressure=none";

            return $"CharacterControl state={_stateMachine.CurrentState} locks={_stateMachine.ControlLockMask} {command} | {motion} | {action} | {pressure} | {BuildAnimationSummary()} | runtimeTicks={_runtimeHost.TickCount} gameplayCommands={_runtimeModule.TotalGameplayCommandsDrained}";
        }

        public string BuildAnimationSummary()
        {
            CharacterAnimationPresentationDiagnosticSnapshot snapshot = _animationController.CreateSnapshot();
            return "animation event=" + snapshot.LastEntry.EventKind
                + " blend2d=" + _animationBackend.Blend2DCount
                + " crossFade=" + _animationBackend.CrossFadeCount
                + " play=" + _animationBackend.PlayCount;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _actionController.ActionEvent -= OnActionEvent;
            _stateMachine.StateChanged -= OnStateChanged;
            _runtimeHost.Dispose();
            _debugSource.Dispose();
            _actionController.Dispose();
            _animationBackend.Dispose();
            _disposed = true;
        }

        private InputCharacterCommandSource EnsureLocalInputSource(MxInput.IInputProvider inputProvider)
        {
            if (_localInputSource != null && ReferenceEquals(_localInputProvider, inputProvider))
            {
                return _localInputSource;
            }

            _localInputProvider = inputProvider;
            _localInputSource = new InputCharacterCommandSource(inputProvider, new InputCharacterCommandSourceOptions
            {
                SourceId = LocalInputSourceId,
                CanReadGameplayInput = provider => provider != null && provider.CurrentContext == MxInput.InputContext.Gameplay,
                ActionBindings = new[]
                {
                    CharacterInputActionBinding.CombatAction(
                        MxInput.InputIntent.DebugPrimary,
                        CharacterActionKind.Attack,
                        combatActionId: _combatActionId,
                        forceStart: true),
                    CharacterInputActionBinding.CombatAction(
                        MxInput.InputIntent.AttackPrimary,
                        CharacterActionKind.Attack,
                        combatActionId: _combatActionId,
                        forceStart: true),
                    CharacterInputActionBinding.GameplayAbility(
                        MxInput.InputIntent.AttackSecondary,
                        GameplayAbilityId)
                },
                TracePrefix = "combat-showcase-input"
            });
            return _localInputSource;
        }

        private static CombatActionRegistry CreateCombatActionRegistry(int combatActionId)
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(combatActionId, new CombatActionTimeline(
                combatActionId,
                totalFrames: 4,
                startup: new CombatFrameRange(0, 0),
                active: new CombatFrameRange(1, 1),
                recovery: new CombatFrameRange(2, 3),
                windows: null,
                events: null));
            return registry;
        }

        private static CharacterAnimationPresentationOptions CreateAnimationOptions()
        {
            var options = new CharacterAnimationPresentationOptions
            {
                TargetActorId = "combat-showcase-player",
                LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D,
                LocomotionBlend2DId = "combat-showcase.locomotion"
            };
            options.ReactionBindings.Add(new CharacterAnimationReactionBinding
            {
                Reason = CharacterControlTransitionReason.PressureBreak,
                RequestKind = CharacterAnimationReactionRequestKind.CrossFade,
                BindingId = "combat-showcase.pressure-break",
                ClipKey = new ResourceKey("demo.character.pressure-break", ResourceTypeIds.AnimationClip),
                FadeDurationSeconds = 0.16f
            });
            return options;
        }

        private static RuntimeAiPlannerCharacterCommandSource CreateRuntimeAiPlannerSource(int combatActionId)
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, ExploitPostureWeaknessGoal.DefaultActivationBand);
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureWeaknessExploited, false);
            var action = new AiAction(
                RuntimeAiPlannerActionId,
                cost: 1f,
                preconditions: null,
                effects: new IAiEffect[]
                {
                    new AiSetFactEffect<bool>(RuntimeAiPressureFactKeys.TargetPostureWeaknessExploited, true)
                });
            var registry = new RuntimeAiCharacterCommandProfileRegistry();
            registry.Register(new RuntimeAiCharacterCommandProfile(
                RuntimeAiPlannerActionId,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                CharacterFacingBasis.Identity,
                actionKind: CharacterActionKind.Attack,
                combatActionId: combatActionId,
                forceStart: true,
                traceTag: "showcase-attack"));
            return new RuntimeAiPlannerCharacterCommandSource(
                world,
                new SequentialPlanner(maxDepth: 2),
                new IAiGoal[] { new ExploitPostureWeaknessGoal() },
                new IAiAction[] { action },
                registry,
                new RuntimeAiPlannerCharacterCommandSourceOptions
                {
                    SourceId = RuntimeAiPlannerSourceId,
                    RequireTargetFacts = true,
                    MinDecisionIntervalFrames = 2,
                    CommandSmoothingFrames = 2,
                    TracePrefix = "runtime-ai-planner-showcase"
                });
        }

        private void OnActionEvent(CharacterActionEvent evt)
        {
            _animationController.RecordActionEvent(evt);
        }

        private void OnStateChanged(CharacterStateChangedEvent evt)
        {
            _animationController.ApplyStateChanged(evt);
        }

        private void OnPressureResult(CharacterPressureReactionResult result)
        {
            if (!result.Recorded && result.Kind == CharacterPressureReactionKind.None)
            {
                return;
            }

            _lastPressureResult = result;
            _hasLastPressureResult = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RuntimeCombatCharacterControlSlice));
            }
        }

        private sealed class SliceRuntimeModule : RuntimeModule
        {
            private readonly CombatActionRunner _actionRunner;
            private readonly RuntimeCommandBuffer _commandBuffer;
            private readonly CharacterPressureReactionController _pressureReactionController;
            private readonly Action<CharacterPressureReactionResult> _pressureResultSink;

            public SliceRuntimeModule(
                CombatActionRunner actionRunner,
                RuntimeCommandBuffer commandBuffer,
                CharacterPressureReactionController pressureReactionController,
                Action<CharacterPressureReactionResult> pressureResultSink)
                : base("demo.character-control.playable-slice")
            {
                _actionRunner = actionRunner;
                _commandBuffer = commandBuffer;
                _pressureReactionController = pressureReactionController;
                _pressureResultSink = pressureResultSink;
            }

            public int LastGameplayCommandsDrained { get; private set; }

            public int TotalGameplayCommandsDrained { get; private set; }

            public override void Tick(RuntimeTickContext context)
            {
                var frame = new RuntimeFrame(context.FrameIndex);
                _actionRunner.TickActions(new CombatFrame(ToCombatFrame(context.FrameIndex)));
                IReadOnlyList<RuntimeCommand> commands = _commandBuffer.DrainForFrame(frame);
                LastGameplayCommandsDrained = commands.Count;
                TotalGameplayCommandsDrained += commands.Count;
                if (_pressureReactionController.TryFinishExpiredReaction(frame, out CharacterPressureReactionResult result))
                {
                    _pressureResultSink?.Invoke(result);
                }
            }

            private static int ToCombatFrame(long frame)
            {
                return frame > int.MaxValue ? int.MaxValue : (int)frame;
            }
        }

        private sealed class RecordingAnimationBackend : IMxAnimationBackend
        {
            public int PlayCount { get; private set; }
            public int CrossFadeCount { get; private set; }
            public int Blend1DCount { get; private set; }
            public int Blend2DCount { get; private set; }
            public int StopCount { get; private set; }
            public int LayerWeightCount { get; private set; }
            public bool IsReleased { get; private set; }

            public string BackendName => "CombatShowcaseRecording";

            public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
            {
                PlayCount++;
                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded demo play.");
            }

            public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
            {
                StopCount++;
                return MxAnimationBackendResult.Succeeded(default, "Recorded demo stop.");
            }

            public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
            {
                CrossFadeCount++;
                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded demo crossfade.");
            }

            public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
            {
                LayerWeightCount++;
                return MxAnimationBackendResult.Succeeded(default, "Recorded demo layer weight.");
            }

            public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
            {
                Blend1DCount++;
                return MxAnimationBackendResult.Succeeded(default, "Recorded demo 1D blend.");
            }

            public MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request)
            {
                Blend2DCount++;
                return MxAnimationBackendResult.Succeeded(default, "Recorded demo 2D blend.");
            }

            public void Tick(float deltaTime)
            {
            }

            public MxAnimationDiagnosticSnapshot CreateSnapshot()
            {
                return new MxAnimationDiagnosticSnapshot(
                    BackendName,
                    string.Empty,
                    string.Empty,
                    Blend2DCount + Blend1DCount + CrossFadeCount + PlayCount,
                    graphIsValid: false,
                    isReleased: IsReleased,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            public void Release()
            {
                IsReleased = true;
            }

            public void Dispose()
            {
                Release();
            }
        }
    }
}
