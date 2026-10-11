using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeCommandModule : RuntimeModule
    {
        private static readonly ProfilerMarker DrainCommandsMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Drain");
        private static readonly ProfilerMarker ApplyCommandsMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Apply");
        private static readonly ProfilerMarker PruneTargetLockMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.PruneTargetLock");
        private static readonly ProfilerMarker EnqueueMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Enqueue");
        private static readonly ProfilerMarker EnqueueBufferMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Enqueue.Buffer");
        private static readonly ProfilerMarker EnqueueRecordAcceptedEventMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Enqueue.RecordAcceptedEvent");
        private static readonly ProfilerMarker EnqueueRecordRejectedEventMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.Enqueue.RecordRejectedEvent");
        private static readonly ProfilerMarker RecordEventQueueMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.RecordEvent.Queue");
        private static readonly ProfilerMarker RecordEventRecentMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.RecordEvent.Recent");
        private static readonly ProfilerMarker RecordEventPruneRecentMarker =
            new ProfilerMarker("CharacterTest.RuntimeCommands.RecordEvent.PruneRecent");

        private const int CommandBufferInitialCapacity = 16;
        private const int DrainedCommandInitialCapacity = 16;
        private const int CommandErrorInitialCapacity = 4;
        public const int DefaultRecentEventCapacity = 64;

        private readonly CharacterTestCharacterRuntimeModule _characterRuntimeModule;
        private readonly Func<RuntimeCommand, RuntimeCommandValidationResult> _applyBehaviorContinuation;
        private readonly List<RuntimeCommand> _lastBehaviorContinuations = new List<RuntimeCommand>(64);
        private readonly RuntimeCommandRegistryValidator _behaviorContinuationValidator;
        private RuntimeFrame _behaviorContinuationFrame;
        internal IReadOnlyList<RuntimeCommand> LastBehaviorContinuations => _lastBehaviorContinuations;
        private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private readonly IRuntimeLogger _logger;
        private readonly List<RuntimeCommand> _lastDrainedCommands =
            new List<RuntimeCommand>(DrainedCommandInitialCapacity);
        private readonly List<RuntimeCommandError> _lastCommandErrors =
            new List<RuntimeCommandError>(CommandErrorInitialCapacity);
        private readonly List<CharacterTestRuntimeEvent> _recentEvents =
            new List<CharacterTestRuntimeEvent>(DefaultRecentEventCapacity);
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(128);
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(256);
        private float _targetLockMaxDistance = 24f;

        public CharacterTestRuntimeCommandModule(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            IRuntimeLogger logger,
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled)
            : this(characterRuntimeModule, logger, initialCameraMode, null)
        {
        }

        internal CharacterTestRuntimeCommandModule(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            IRuntimeLogger logger,
            CharacterTestCameraMode initialCameraMode,
            CharacterTestPerceptionFrameCache perceptionFrameCache)
            : base("CharacterTest.RuntimeCommands", RuntimeTickStage.Simulation, priority: 0)
        {
            _characterRuntimeModule = characterRuntimeModule;
            _perceptionFrameCache = perceptionFrameCache ?? characterRuntimeModule?.PerceptionFrameCache;
            _logger = logger ?? NullRuntimeLogger.Instance;
            CommandRegistry = CharacterTestRuntimeCommandRegistry.Create();
            _applyBehaviorContinuation = ApplyBehaviorContinuation;
            _behaviorContinuationValidator = new RuntimeCommandRegistryValidator(CommandRegistry);
            CommandBuffer = new RuntimeCommandBuffer(
                new RuntimeCommandRegistryValidator(CommandRegistry),
                RuntimeFrame.Zero,
                CommandBufferInitialCapacity);
            Events = new RuntimeEventQueue<CharacterTestRuntimeEvent>(DefaultRecentEventCapacity);
            CameraMode = initialCameraMode;
            WarmUpControlIntentValidation();
            WarmUpAcceptedEventRecording();
        }

        public RuntimeCommandRegistry CommandRegistry { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public RuntimeEventQueue<CharacterTestRuntimeEvent> Events { get; }
        public IReadOnlyList<RuntimeCommand> LastDrainedCommands => _lastDrainedCommands;
        public IReadOnlyList<RuntimeCommandError> LastCommandErrors => _lastCommandErrors;
        public IReadOnlyList<CharacterTestRuntimeEvent> RecentEvents => _recentEvents;
        public CharacterTestCameraMode CameraMode { get; private set; }
        public bool TargetLockEnabled { get; private set; }
        public int LockedTargetEntityId { get; private set; }
        internal CharacterTestCombatTargetHandle LockedCombatTarget { get; private set; }
        public bool RuntimeAiBehaviorSelectionUnlocked { get; private set; }
        public int PendingCommandCount => CommandBuffer.PendingCount;
        /// <summary>Actual consumer receipt, after TryApplyControlIntent accepts an owned nonzero move. Enqueue alone never raises it.</summary>
        /// <remarks>Observers must not throw or submit commands. NoAlloc on the warmed execution path.</remarks>
        internal event Action<int, long, long> BehaviorMoveApplied;
        public RuntimeEventQueueSnapshot EventQueueSnapshot => Events.CreateSnapshot();

        public void SetRuntimeAiBehaviorSelectionUnlocked(bool unlocked)
        {
            RuntimeAiBehaviorSelectionUnlocked = unlocked;
        }

        public void SetTargetLockMaxDistance(float maxDistance)
        {
            _targetLockMaxDistance = maxDistance > 0f ? maxDistance : 0f;
        }

        public void ApplyNetworkSnapshotState(
            CharacterTestCameraMode cameraMode,
            bool targetLockEnabled,
            int lockedTargetEntityId,
            bool runtimeAiBehaviorSelectionUnlocked = false)
        {
            CameraMode = cameraMode;
            TargetLockEnabled = targetLockEnabled;
            LockedTargetEntityId = targetLockEnabled ? lockedTargetEntityId : 0;
            if (!targetLockEnabled)
                LockedCombatTarget = default;
            RuntimeAiBehaviorSelectionUnlocked = runtimeAiBehaviorSelectionUnlocked;
        }

        internal void ApplyLockedCombatTargetSnapshot(
            in CharacterTestCombatTargetHandle handle)
        {
            LockedCombatTarget = TargetLockEnabled ? handle : default;
        }

        public RuntimeCommandValidationResult Enqueue(RuntimeCommand command)
        {
            using (EnqueueMarker.Auto())
            {
                RuntimeCommandValidationResult result;
                using (EnqueueBufferMarker.Auto())
                {
                    result = CommandBuffer.Enqueue(command);
                }

                if (result.Success)
                {
                    if (ShouldRecordAcceptedEvent(result.Command))
                    {
                        using (EnqueueRecordAcceptedEventMarker.Auto())
                        {
                            RecordEvent(new CharacterTestRuntimeEvent(
                                result.Command.Frame,
                                CharacterTestRuntimeEventType.CommandAccepted,
                                result.Command.CommandId,
                                result.Command.SourceId,
                                result.Command.TargetId,
                                payload0: result.Command.Payload0,
                                payload1: result.Command.Payload1,
                                payload2: result.Command.Payload2));
                        }
                    }

                    return result;
                }

                using (EnqueueRecordRejectedEventMarker.Auto())
                {
                    _lastCommandErrors.Add(result.Error);
                    RecordEvent(new CharacterTestRuntimeEvent(
                        command.Frame,
                        CharacterTestRuntimeEventType.CommandRejected,
                        command.CommandId,
                        command.SourceId,
                        command.TargetId,
                        payload0: command.Payload0,
                        payload1: command.Payload1,
                        payload2: command.Payload2,
                        message: result.Error.Message));
                }

                return result;
            }
        }

        public int DrainEvents(RuntimeFrame frame, List<CharacterTestRuntimeEvent> output)
        {
            return Events.Drain(frame, output);
        }

        internal void ResetForRollbackReplay(RuntimeFrame frame)
        {
            CommandBuffer.Reset(frame);
            _characterRuntimeModule?.ResetBehaviorCommandReceipts(frame);
            Events.Clear();
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();
            _recentEvents.Clear();
        }

        internal void ClearWarmupSubmissionState()
        {
            RuntimeFrame frame = CommandBuffer.CurrentFrame;
            CommandBuffer.Reset(frame);
            _characterRuntimeModule?.ResetBehaviorCommandReceipts(frame);
            Events.Clear();
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();
            _recentEvents.Clear();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            var frame = new RuntimeFrame(context.FrameIndex);
            _perceptionFrameCache?.BeginCommittedConsumerPhase(
                frame,
                _characterRuntimeModule,
                _characterRuntimeModule?.PhysicsRuntime);
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();

            using (PruneTargetLockMarker.Auto())
            {
                PruneInvalidTargetLock(frame);
            }

            using (DrainCommandsMarker.Auto())
            {
                CommandBuffer.DrainForFrame(frame, _lastDrainedCommands);
            }

            using (ApplyCommandsMarker.Auto())
            {
                for (int i = 0; i < _lastDrainedCommands.Count; i++)
                {
                    RuntimeCommand command = _lastDrainedCommands[i];
                    ApplyCommand(command);
                }
            }

            _lastBehaviorContinuations.Clear();
            _behaviorContinuationFrame = frame;
            _characterRuntimeModule?.ConsumeBehaviorContinuations(frame, _applyBehaviorContinuation);

            using (PruneTargetLockMarker.Auto())
            {
                PruneInvalidTargetLock(frame);
            }
        }

        public override void Dispose()
        {
            CommandBuffer.Clear();
            Events.Clear();
            _recentEvents.Clear();
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();
        }

        private RuntimeCommandValidationResult ApplyBehaviorContinuation(RuntimeCommand command)
        {
            if (command.Frame.Value != _behaviorContinuationFrame.Value || command.SourceId != CharacterTestRuntimeCommandSources.RuntimeAiPlanner)
                throw new InvalidOperationException("Behavior continuation must remain in the current command-consumption frame.");
            var validation = _behaviorContinuationValidator.Validate(command);
            if (!validation.Success) return validation;
            // Derived output is observable but regenerated by replay, not another external input.
            _lastBehaviorContinuations.Add(command);
            ApplyCommand(command);
            return validation;
        }

        private void ApplyCommand(RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case CharacterTestRuntimeCommandIds.SpawnCharacter:
                    ApplySpawnCharacter(command);
                    break;
                case CharacterTestRuntimeCommandIds.SetCameraMode:
                    ApplySetCameraMode(command);
                    break;
                case CharacterTestRuntimeCommandIds.ToggleTargetLock:
                    ApplyToggleTargetLock(command);
                    break;
                case CharacterTestRuntimeCommandIds.SetTargetLock:
                    ApplySetTargetLock(command);
                    break;
                case CharacterTestRuntimeCommandIds.PositionRecall:
                    ApplyPositionRecall(command);
                    break;
                case CharacterTestRuntimeCommandIds.GroundPointTraction:
                    ApplyGroundPointTraction(command);
                    break;
                case CharacterTestRuntimeCommandIds.SetControlIntent:
                    ApplySetControlIntent(command);
                    break;
                case CharacterTestRuntimeCommandIds.CastAbilitySlot:
                    ApplyCastAbilitySlot(command);
                    break;
                case CharacterTestRuntimeCommandIds.RequestCharacterAction:
                    ApplyCharacterActionRequest(command);
                    break;
                case CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration:
                    ApplyBehaviorRecipeOrchestration(command);
                    break;
                case CharacterTestRuntimeCommandIds.ReviveCharacter:
                    ApplyReviveCharacter(command);
                    break;
                case CharacterTestRuntimeCommandIds.DestroyCharacter:
                    ApplyDestroyCharacter(command);
                    break;
                case CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection:
                    ApplyRuntimeAiBehaviorSelection(command);
                    break;
                case CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview:
                    ApplyAuthorityPosePreview(command);
                    break;
                case CharacterTestRuntimeCommandIds.SurfaceProbe:
                    ApplySurfaceProbe(command);
                    break;
                case CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe:
                    ApplyGenesisMovableObjectProbe(command);
                    break;
                case CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe:
                    ApplyGenesisPotencyMechanicsProbe(command);
                    break;
                case CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe:
                    ApplyGenesisLivingBeingProbe(command);
                    break;
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe:
                    ApplyGenesisLivingNaturalAttributesProbe(command);
                    break;
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState:
                    ApplyGenesisLivingNaturalAttributesRuntimeState(command);
                    break;
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectReset:
                    ApplyMovableNaturalObjectReset(command);
                    break;
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence:
                    ApplyMovableNaturalObjectPotencyInfluence(command);
                    break;
            }
        }

        private void ApplySpawnCharacter(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                AddCommandError(command, "Character runtime module is not available.");
                RecordEvent(new CharacterTestRuntimeEvent(
                    command.Frame,
                    CharacterTestRuntimeEventType.CharacterSpawnRejected,
                    command.CommandId,
                    command.SourceId,
                    command.TargetId,
                    message: "Character runtime module is not available."));
                return;
            }

            CharacterTestCharacterSpawnRequest request =
                CharacterTestRuntimeCommandFactory.DecodeSpawnCharacter(command);
            bool accepted = _characterRuntimeModule.EnqueueSpawn(request);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                accepted
                    ? CharacterTestRuntimeEventType.CharacterSpawnQueued
                    : CharacterTestRuntimeEventType.CharacterSpawnRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: request.TeamId,
                message: accepted ? "queued" : "spawn request was rejected"));
        }

        private void ApplySetCameraMode(RuntimeCommand command)
        {
            var nextMode = (CharacterTestCameraMode)command.Payload0;
            if (CameraMode == nextMode)
                return;

            CameraMode = nextMode;
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.CameraModeChanged,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                message: nextMode.ToString()));
        }

        private void ApplySetControlIntent(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectControlIntent(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestBehaviorControlInput behaviorControl = CharacterTestRuntimeCommandFactory.DecodeBehaviorControl(command.Envelope);
            if (behaviorControl.IsPresent && behaviorControl.IsOwnerOnlyRelease)
            {
                var owner = _characterRuntimeModule.FindEntityById(command.TargetId);
                if (owner != null && owner.Input.TryReleaseOwnedBehaviorControl(behaviorControl.OwnerToken, command.SourceId, out var released))
                    owner.SetInput(released);
                return;
            }
            if (behaviorControl.IsPresent && behaviorControl.IsRelease)
            {
                var owner = _characterRuntimeModule.FindEntityById(command.TargetId);
                if (owner != null && (owner.Input.BehaviorControl.IsPresent
                    ? owner.Input.BehaviorControl.OwnerToken != behaviorControl.OwnerToken
                    : owner.Input.ControlIntentSourceId != command.SourceId)) return;
            }
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            LogControlIntentApply("apply", command, intent);
            if (!_characterRuntimeModule.TryApplyControlIntent(
                command.Frame,
                intent,
                command.SourceId,
                out string message))
            {
                if (IsExpectedBlockedLocalControlIntent(command, intent))
                {
                    LogControlIntentApply("apply-blocked", command, intent, message);
                    return;
                }

                LogControlIntentApply("apply-rejected", command, intent, message);
                RejectControlIntent(command, message);
                return;
            }

            CharacterTestCharacterEntity controlled = _characterRuntimeModule.FindEntityById(command.TargetId);
            if (controlled != null)
                controlled.SetInput(controlled.Input.WithBehaviorControl(CharacterTestRuntimeCommandFactory.DecodeBehaviorControl(command.Envelope)));
            if (controlled != null && behaviorControl.IsPresent && !behaviorControl.IsRelease && controlled.Input.HasActiveMove)
                BehaviorMoveApplied?.Invoke(command.TargetId, behaviorControl.OwnerToken, command.Frame.Value);
            LogControlIntentApply("apply-accepted", command, intent);
            if (ShouldRecordControlIntentAppliedEvent(command))
            {
                RecordEvent(new CharacterTestRuntimeEvent(
                    command.Frame,
                    CharacterTestRuntimeEventType.ControlIntentApplied,
                    command.CommandId,
                    command.SourceId,
                    command.TargetId,
                    entityId: intent.TargetEntityId,
                    payload0: command.Payload0,
                    payload1: command.Payload1,
                    payload2: command.Payload2,
                    message: "applied"));
            }
        }

        private void ApplyCastAbilitySlot(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectAbilitySlotCast(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestAbilitySlotCastRequest request =
                CharacterTestRuntimeCommandFactory.DecodeAbilitySlotCast(command);
            if (!_characterRuntimeModule.TryApplyAbilitySlotCast(
                command.Frame,
                request.TargetEntityId,
                request.SlotNumber,
                request.AbilityId,
                out string message))
            {
                RejectAbilitySlotCast(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: request.SlotNumber,
                payload1: request.AbilityId,
                payload2: request.SchemaVersion,
                message: "ability slot cast applied"));
        }

        private void ApplyCharacterActionRequest(RuntimeCommand command)
        {
            CharacterTestCharacterActionRuntime actionRuntime = _characterRuntimeModule?.ActionRuntime;
            if (actionRuntime == null)
            {
                RejectCharacterActionRequest(command, "CharacterAction runtime is not available.");
                return;
            }

            var entityId = new CharacterTestCharacterEntityId(command.TargetId);
            bool accepted;
            string message;
            if (command.Payload0 != CharacterTestRuntimeCommandFactory.CharacterActionStartMarker
                || command.Payload1 <= 0)
            {
                accepted = false;
                message = "CharacterAction command schema v2 requires payload0=1 and a positive action id.";
            }
            else if (!actionRuntime.Registry.TryGetAction(
                         new CharacterActionId(command.Payload1),
                         out CharacterActionConfig action))
            {
                accepted = false;
                message = "CharacterAction numeric id is not registered: " + command.Payload1;
            }
            else if (action.Category == CharacterActionCategory.BasicAttack
                && IsRuntimeAiPrimaryAttackCommand(command.SourceId)
                && !_characterRuntimeModule.CanSubmitPrimaryAttack(
                    _characterRuntimeModule.FindEntityById(command.TargetId),
                    command.Frame))
            {
                accepted = false;
                message = "PrimaryAttack rejected by AttackOpportunity guard.";
            }
            else
            {
                accepted = actionRuntime.TryStartActionSlot(
                    entityId,
                    ResolveActionSlot(action.Category),
                    action.ActionId,
                    command.Frame,
                    ResolveCharacterActionSource(command.SourceId),
                    command.TraceId);
                message = accepted ? "CharacterAction start applied" : "CharacterAction start rejected";
            }

            if (!accepted)
            {
                RejectCharacterActionRequest(command, message);
                return;
            }
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void ApplyBehaviorRecipeOrchestration(RuntimeCommand command)
        {
            if (command.Payload2 == 2 && command.Envelope.TypeId == CharacterTestRuntimeCommandFactory.BehaviorStopEnvelopeTypeId)
            {
                CharacterTestCharacterActionRuntime actions = _characterRuntimeModule?.ActionRuntime;
                if (actions == null || !actions.TryGetActiveActionPresentationState(new CharacterTestCharacterEntityId(command.TargetId),
                    out long instanceId, out _, out _, out _) || instanceId != command.Envelope.Data0)
                    return; // A stale release cannot terminate a later owner's action instance.
            }
            if (_characterRuntimeModule == null)
            {
                RejectCharacterActionRequest(command, "Character runtime module is not available.");
                return;
            }
            var operation = (CharacterTestBehaviorRecipeOrchestrationOperation)command.Payload0;
            if (!_characterRuntimeModule.TryApplyBehaviorRecipeOrchestration(
                    command.Frame,
                    command.TargetId,
                    operation,
                    command.Payload1,
                    command.TraceId,
                    command.Sequence,command.SourceId,
                    out string message,command.Payload2,command.Envelope))
            {
                RejectCharacterActionRequest(command, message, !message.StartsWith("ActionUse.CommitFailed:", StringComparison.Ordinal));
                return;
            }
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private static int ResolveActionSlot(CharacterActionCategory category)
        {
            switch (category)
            {
                case CharacterActionCategory.Skill:
                    return CharacterTestActionSlotIds.Skill;
                case CharacterActionCategory.Guard:
                    return CharacterTestActionSlotIds.Guard;
                case CharacterActionCategory.Dodge:
                    return CharacterTestActionSlotIds.Dodge;
                case CharacterActionCategory.Jump:
                    return CharacterTestActionSlotIds.Jump;
                case CharacterActionCategory.Interaction:
                    return CharacterTestActionSlotIds.Interact;
                default:
                    return CharacterTestActionSlotIds.AttackPrimary;
            }
        }

        internal static CharacterActionSourceKind ResolveCharacterActionSource(int sourceId)
        {
            switch (sourceId)
            {
                case CharacterTestRuntimeCommandSources.PlayerInput:
                case CharacterTestRuntimeCommandSources.SkillPanel:
                    return CharacterActionSourceKind.LocalInput;
                case CharacterTestRuntimeCommandSources.RuntimeAiPlanner:
                    return CharacterActionSourceKind.RuntimeAiPlanner;
                case CharacterTestRuntimeCommandSources.StoryFlow:
                    return CharacterActionSourceKind.Scripted;
                case CharacterTestRuntimeCommandSources.DebugUi:
                case CharacterTestRuntimeCommandSources.SurfaceProbePanel:
                case CharacterTestRuntimeCommandSources.GenesisProbe:
                case CharacterTestRuntimeCommandSources.RuntimeAiBehaviorPanel:
                    return CharacterActionSourceKind.Debug;
                default:
                    return CharacterActionSourceKind.Command;
            }
        }

        private static bool IsRuntimeAiPrimaryAttackCommand(int sourceId)
        {
            return sourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                || sourceId == CharacterTestRuntimeCommandSources.RuntimeAiBehaviorPanel;
        }

        private void ApplyReviveCharacter(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectReviveCharacter(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestReviveRequest request =
                CharacterTestRuntimeCommandFactory.DecodeReviveCharacter(command);
            if (!_characterRuntimeModule.TryReviveCharacter(
                request,
                command.Frame,
                out string message))
            {
                RejectReviveCharacter(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.CharacterReviveApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: request.ReviveHp,
                payload1: request.SchemaVersion,
                payload2: 0,
                message: message));
        }

        private void ApplyDestroyCharacter(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectDestroyCharacter(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestDestroyCharacterRequest request =
                CharacterTestRuntimeCommandFactory.DecodeDestroyCharacter(command);
            if (!_characterRuntimeModule.TryDestroyCharacter(
                request,
                command.Frame,
                out string message))
            {
                RejectDestroyCharacter(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.CharacterDestroyApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: request.SourceEntityId,
                payload1: request.SchemaVersion,
                payload2: 0,
                message: message));
        }

        private void ApplyRuntimeAiBehaviorSelection(RuntimeCommand command)
        {
            if (!RuntimeAiBehaviorSelectionUnlocked)
            {
                RejectRuntimeAiBehaviorSelection(
                    command,
                    "Runtime AI Planner behavior selection phase is locked.");
                return;
            }

            if (_characterRuntimeModule == null)
            {
                RejectRuntimeAiBehaviorSelection(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestRuntimeAiBehaviorSelectionRequest request =
                CharacterTestRuntimeCommandFactory.DecodeRuntimeAiBehaviorSelection(command);
            if (request.SchemaVersion !=
                CharacterTestRuntimeCommandFactory.RuntimeAiBehaviorSelectionSchemaVersion)
            {
                RejectRuntimeAiBehaviorSelection(
                    command,
                    "Runtime AI Planner behavior selection schema is invalid.");
                return;
            }

            if (!_characterRuntimeModule.TryApplyRuntimeAiBehaviorSelection(
                    request.TargetEntityId,
                    request.LoadoutId,
                    request.VariantId,
                    out string message))
            {
                RejectRuntimeAiBehaviorSelection(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: request.LoadoutId,
                payload1: request.VariantId,
                payload2: request.SchemaVersion,
                message: "runtime ai behavior selection applied"));
        }

        private void ApplyAuthorityPosePreview(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectAuthorityPosePreview(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestAuthorityPosePreviewRequest request =
                CharacterTestRuntimeCommandFactory.DecodeAuthorityPosePreview(command);
            if (!_characterRuntimeModule.TryApplyAuthorityPosePreview(
                    request,
                    command.Frame,
                    out string message))
            {
                RejectAuthorityPosePreview(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "authority pose preview applied"
                    : message));
        }

        private void ApplySurfaceProbe(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectSurfaceProbe(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestSurfaceProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeSurfaceProbe(command);
            if (!_characterRuntimeModule.TryApplySurfaceProbe(request, command.Frame, out string message))
            {
                RejectSurfaceProbe(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.SurfaceProbeApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "surface probe applied"
                    : message));
        }

        private void ApplyMovableNaturalObjectReset(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectMovableNaturalObjectReset(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestMovableNaturalObjectResetRequest request =
                CharacterTestRuntimeCommandFactory.DecodeMovableNaturalObjectReset(command);
            if (request.SchemaVersion != CharacterTestRuntimeCommandFactory.MovableNaturalObjectResetSchemaVersion)
            {
                RejectMovableNaturalObjectReset(command, "movable natural object reset schema is invalid");
                return;
            }

            if (!_characterRuntimeModule.TryResetMovableNaturalObjects(
                    request.TargetNumericId,
                    out string message))
            {
                RejectMovableNaturalObjectReset(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.MovableNaturalObjectResetApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void ApplyMovableNaturalObjectPotencyInfluence(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectMovableNaturalObjectPotencyInfluence(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestMovableNaturalObjectPotencyInfluenceRequest request =
                CharacterTestRuntimeCommandFactory.DecodeMovableNaturalObjectPotencyInfluence(command);
            if (request.SchemaVersion !=
                CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluenceSchemaVersion)
            {
                RejectMovableNaturalObjectPotencyInfluence(
                    command,
                    "movable natural object potency influence schema is invalid");
                return;
            }

            if (!CharacterTestGenesisPotencyRuntime.IsCommandInfluenceKind(request.InfluenceKind))
            {
                RejectMovableNaturalObjectPotencyInfluence(
                    command,
                    "movable natural object potency influence is invalid");
                return;
            }

            if (!_characterRuntimeModule.TryApplyMovableNaturalObjectPotencyInfluence(
                    request,
                    command.Frame,
                    out string message))
            {
                RejectMovableNaturalObjectPotencyInfluence(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.MovableNaturalObjectPotencyInfluenceApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectControlIntent(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectAbilitySlotCast(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectCharacterActionRequest(RuntimeCommand command, string message, bool commandError = true)
        {
            if (commandError) AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectReviveCharacter(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.CharacterReviveRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectDestroyCharacter(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.CharacterDestroyRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectRuntimeAiBehaviorSelection(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectAuthorityPosePreview(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.ControlIntentRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectSurfaceProbe(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.SurfaceProbeRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectMovableNaturalObjectReset(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.MovableNaturalObjectResetRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectMovableNaturalObjectPotencyInfluence(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.MovableNaturalObjectPotencyInfluenceRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void AddCommandError(RuntimeCommand command, string message)
        {
            var error = new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                CommandBuffer.CurrentFrame,
                message);
            _lastCommandErrors.Add(error);
            _logBuffer.Clear()
                .Append("Command rejected. commandId=")
                .Append(command.CommandId)
                .Append(" targetId=")
                .Append(command.TargetId)
                .Append(" message=")
                .Append(message);
            _logger.Warning("CharacterTest.RuntimeCommands", _logBuffer);
        }

        private void LogControlIntentApply(
            string phase,
            RuntimeCommand command,
            CharacterTestControlIntent intent,
            string reason = "")
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (!CharacterTestInputFlowLog.HasActiveIntent(intent))
                return;

            _inputFlowBuffer.Clear()
                .Append(phase).Append(" frame=").Append(command.Frame.Value);
            CharacterTestInputFlowLog.AppendIntent(_inputFlowBuffer, intent);
            _inputFlowBuffer.Append(" source=").Append(command.SourceId)
                .Append(" trace=").Append(command.TraceId.KindId).Append(":").Append(command.TraceId.SourceId)
                .Append(":").Append(command.TraceId.ExecutionId).Append(":").Append(command.TraceId.Sequence);
            if (!string.IsNullOrWhiteSpace(reason))
                _inputFlowBuffer.Append(" reason=").Append(reason);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }

        private void RecordEvent(CharacterTestRuntimeEvent evt)
        {
            using (RecordEventQueueMarker.Auto())
            {
                Events.Enqueue(evt.Frame, evt);
            }

            using (RecordEventRecentMarker.Auto())
            {
                _recentEvents.Add(evt);
            }

            using (RecordEventPruneRecentMarker.Auto())
            {
                while (_recentEvents.Count > DefaultRecentEventCapacity)
                    _recentEvents.RemoveAt(0);
            }
        }

        private static bool ShouldRecordAcceptedEvent(RuntimeCommand command)
        {
            return command.CommandId != CharacterTestRuntimeCommandIds.SetControlIntent;
        }

        private static bool ShouldRecordControlIntentAppliedEvent(RuntimeCommand command)
        {
            return !(command.TraceId.KindId == CharacterTestRuntimeInputTraceKinds.LocalControlIntent);
        }

        private bool IsExpectedBlockedLocalControlIntent(
            RuntimeCommand command,
            CharacterTestControlIntent intent)
        {
            if (command.SourceId != CharacterTestRuntimeCommandSources.PlayerInput
                || !(command.TraceId.KindId == CharacterTestRuntimeInputTraceKinds.LocalControlIntent))
            {
                return false;
            }

            CharacterTestCharacterEntity entity =
                _characterRuntimeModule.FindEntityById(intent.TargetEntityId);
            if (entity == null
                || entity.Control.Owner
                    != CharacterTestCharacterControlOwner.LocalPlayer)
            {
                return false;
            }

            CharacterTestBattleStateComponent state = entity.BattleState;
            return !state.CanMoveByInput && !state.CanStartNormalAction;
        }

        private void WarmUpControlIntentValidation()
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.PlayerInput,
                CharacterTestControlIntent.FromDeterministic(
                    targetEntityId: 0,
                    moveXMilli: 0,
                    moveYMilli: 0,
                    worldMoveXMilli: 0,
                    worldMoveYMilli: 0,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    attackSecondaryPressed: false,
                    debugSecondaryPressed: false,
                    debugPrimaryPressed: false,
                    skillSlotPressedMask: 0),
                traceId: RuntimeTraceIdentity.None);
            if (CommandRegistry.TryGet(command.CommandId, out RuntimeCommandDefinition definition)
                && definition.PayloadSchema != null)
            {
                _ = definition.PayloadSchema.IsValid(command);
            }
        }

        private void WarmUpAcceptedEventRecording()
        {
            RecordEvent(new CharacterTestRuntimeEvent(
                RuntimeFrame.Zero,
                CharacterTestRuntimeEventType.CommandAccepted,
                CharacterTestRuntimeCommandIds.SetControlIntent,
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetId: 1));
            Events.Clear();
            _recentEvents.Clear();
        }
    }
}
