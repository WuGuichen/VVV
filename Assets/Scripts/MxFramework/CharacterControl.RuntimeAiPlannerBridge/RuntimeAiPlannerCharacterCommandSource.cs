using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl.RuntimeAiPlannerBridge
{
    public enum RuntimeAiPlannerCharacterCommandSuppressedReason
    {
        None = 0,
        TargetFactsMissing = 1,
        PlanUnavailable = 2,
        EmptyPlan = 3,
        MissingActionProfile = 4,
        ReactionDelay = 5
    }

    public readonly struct RuntimeAiPlannerCharacterCommandDiagnostics
    {
        public RuntimeAiPlannerCharacterCommandDiagnostics(
            RuntimeAiPlannerCharacterCommandSuppressedReason suppressedReason,
            string lastGoal,
            int lastActionId,
            RuntimeFrame lastDecisionFrame,
            CharacterCommand lastCommand)
        {
            SuppressedReason = suppressedReason;
            LastGoal = lastGoal ?? string.Empty;
            LastActionId = lastActionId;
            LastDecisionFrame = lastDecisionFrame;
            LastCommand = lastCommand;
        }

        public RuntimeAiPlannerCharacterCommandSuppressedReason SuppressedReason { get; }

        public string LastGoal { get; }

        public int LastActionId { get; }

        public RuntimeFrame LastDecisionFrame { get; }

        public CharacterCommand LastCommand { get; }
    }

    public readonly struct RuntimeAiCharacterCommandProfile
    {
        public RuntimeAiCharacterCommandProfile(
            int actionId,
            FixVector3 moveDirection,
            CharacterFacingBasis facingBasis,
            bool jumpPressed = false,
            bool sprintHeld = false,
            CharacterActionKind actionKind = CharacterActionKind.None,
            int combatActionId = 0,
            int gameplayAbilityId = 0,
            bool forceStart = false,
            bool queueIfBusy = false,
            Fix64? moveSpeedScale = null,
            string traceTag = "")
        {
            if (actionId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionId), "Runtime AI Planner action id cannot be negative.");
            if (combatActionId < 0)
                throw new ArgumentOutOfRangeException(nameof(combatActionId), "Combat action id cannot be negative.");
            if (gameplayAbilityId < 0)
                throw new ArgumentOutOfRangeException(nameof(gameplayAbilityId), "Gameplay ability id cannot be negative.");

            ActionId = actionId;
            MoveDirection = moveDirection;
            FacingBasis = facingBasis;
            JumpPressed = jumpPressed;
            SprintHeld = sprintHeld;
            ActionKind = actionKind;
            CombatActionId = combatActionId;
            GameplayAbilityId = gameplayAbilityId;
            ForceStart = forceStart;
            QueueIfBusy = queueIfBusy;
            MoveSpeedScale = moveSpeedScale ?? Fix64.One;
            TraceTag = traceTag ?? string.Empty;
        }

        public int ActionId { get; }

        public FixVector3 MoveDirection { get; }

        public CharacterFacingBasis FacingBasis { get; }

        public bool JumpPressed { get; }

        public bool SprintHeld { get; }

        public CharacterActionKind ActionKind { get; }

        public int CombatActionId { get; }

        public int GameplayAbilityId { get; }

        public bool ForceStart { get; }

        public bool QueueIfBusy { get; }

        public Fix64 MoveSpeedScale { get; }

        public string TraceTag { get; }

        public bool HasActionRequest =>
            ActionKind == CharacterActionKind.Cancel || CombatActionId > 0 || GameplayAbilityId > 0;
    }

    public interface IRuntimeAiCharacterCommandProfileProvider
    {
        bool TryGetProfile(
            IAiAction action,
            IAiWorldState worldState,
            out RuntimeAiCharacterCommandProfile profile);
    }

    public sealed class RuntimeAiCharacterCommandProfileRegistry : IRuntimeAiCharacterCommandProfileProvider
    {
        private readonly Dictionary<int, RuntimeAiCharacterCommandProfile> _profiles =
            new Dictionary<int, RuntimeAiCharacterCommandProfile>();

        public void Register(RuntimeAiCharacterCommandProfile profile)
        {
            _profiles[profile.ActionId] = profile;
        }

        public bool TryGetProfile(
            IAiAction action,
            IAiWorldState worldState,
            out RuntimeAiCharacterCommandProfile profile)
        {
            profile = default;
            return action != null && _profiles.TryGetValue(action.Id, out profile);
        }
    }

    public sealed class RuntimeAiPlannerCharacterCommandSourceOptions
    {
        public int SourceId { get; set; }

        public int ReactionDelayFrames { get; set; }

        public int MinDecisionIntervalFrames { get; set; }

        public int CommandSmoothingFrames { get; set; }

        public bool RequireTargetFacts { get; set; }

        public AiFactKey TargetRequiredFactKey { get; set; } = RuntimeAiPressureFactKeys.TargetPostureBand;

        public Func<IAiWorldState, GameplayEntityId> TargetResolver { get; set; }

        public string TracePrefix { get; set; } = "runtime-ai-planner";
    }

    public sealed class RuntimeAiPlannerCharacterCommandSource : ICharacterCommandSource
    {
        private readonly IAiWorldState _worldState;
        private readonly IAiPlanner _planner;
        private readonly List<IAiGoal> _goals;
        private readonly List<IAiAction> _actions;
        private readonly IRuntimeAiCharacterCommandProfileProvider _profileProvider;
        private readonly RuntimeAiPlannerCharacterCommandSourceOptions _options;

        private RuntimeAiCharacterCommandProfile _lastProfile;
        private bool _hasLastProfile;
        private RuntimeAiCharacterCommandProfile _pendingProfile;
        private bool _hasPendingProfile;
        private RuntimeFrame _pendingEffectiveFrame;
        private RuntimeFrame _nextDecisionFrame;
        private RuntimeFrame _smoothUntilFrame;
        private int _lastActionId;
        private CharacterCommand _lastCommand;

        public RuntimeAiPlannerCharacterCommandSource(
            IAiWorldState worldState,
            IAiPlanner planner,
            IEnumerable<IAiGoal> goals,
            IEnumerable<IAiAction> actions,
            IRuntimeAiCharacterCommandProfileProvider profileProvider,
            RuntimeAiPlannerCharacterCommandSourceOptions options = null)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
            _options = options ?? new RuntimeAiPlannerCharacterCommandSourceOptions();
            ValidateOptions(_options);
            _goals = CopyGoals(goals);
            _actions = CopyActions(actions);
            Diagnostics = new RuntimeAiPlannerCharacterCommandDiagnostics(
                RuntimeAiPlannerCharacterCommandSuppressedReason.None,
                string.Empty,
                0,
                RuntimeFrame.Zero,
                default);
        }

        public RuntimeAiPlannerCharacterCommandDiagnostics Diagnostics { get; private set; }

        public bool TryGetCommand(RuntimeFrame frame, CharacterControlEntityRef entity, out CharacterCommand command)
        {
            command = default;
            if (TargetFactsMissing())
            {
                ClearCachedCommand(RuntimeAiPlannerCharacterCommandSuppressedReason.TargetFactsMissing, frame);
                return false;
            }

            if (_hasPendingProfile)
            {
                if (frame < _pendingEffectiveFrame)
                {
                    SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.ReactionDelay, frame, _lastActionId, default, string.Empty);
                    return false;
                }

                RuntimeAiCharacterCommandProfile profile = _pendingProfile;
                _pendingProfile = default;
                _hasPendingProfile = false;
                return TryBuildCommand(frame, entity, profile, emitActionRequest: true, out command);
            }

            if (_hasLastProfile && frame < _nextDecisionFrame)
            {
                return TryBuildCommand(frame, entity, _lastProfile, emitActionRequest: false, out command);
            }

            if (!_planner.TryPlan(_worldState, _goals, _actions, out AiPlan plan))
            {
                ClearCachedCommand(RuntimeAiPlannerCharacterCommandSuppressedReason.PlanUnavailable, frame);
                return false;
            }

            if (plan.Actions.Count == 0)
            {
                ClearCachedCommand(RuntimeAiPlannerCharacterCommandSuppressedReason.EmptyPlan, frame);
                SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.EmptyPlan, frame, 0, default, DescribeGoal(plan.Goal));
                return false;
            }

            IAiAction action = plan.Actions[0];
            if (!_profileProvider.TryGetProfile(action, _worldState, out RuntimeAiCharacterCommandProfile selectedProfile))
            {
                ClearCachedCommand(RuntimeAiPlannerCharacterCommandSuppressedReason.MissingActionProfile, frame);
                SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.MissingActionProfile, frame, action.Id, default, DescribeGoal(plan.Goal));
                return false;
            }

            RuntimeAiCharacterCommandProfile profileToUse = selectedProfile;
            bool actionChanged = !_hasLastProfile || selectedProfile.ActionId != _lastProfile.ActionId;
            if (_hasLastProfile
                && actionChanged
                && frame < _smoothUntilFrame)
            {
                profileToUse = _lastProfile;
            }
            else if (actionChanged && _options.ReactionDelayFrames > 0)
            {
                _pendingProfile = selectedProfile;
                _hasPendingProfile = true;
                _pendingEffectiveFrame = AddFrames(frame, _options.ReactionDelayFrames);
                _lastActionId = selectedProfile.ActionId;
                SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.ReactionDelay, frame, selectedProfile.ActionId, default, DescribeGoal(plan.Goal));
                return false;
            }

            _nextDecisionFrame = AddFrames(frame, _options.MinDecisionIntervalFrames);
            _smoothUntilFrame = AddFrames(frame, _options.CommandSmoothingFrames);
            _lastActionId = selectedProfile.ActionId;
            SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.None, frame, profileToUse.ActionId, default, DescribeGoal(plan.Goal));
            return TryBuildCommand(
                frame,
                entity,
                profileToUse,
                emitActionRequest: actionChanged && profileToUse.ActionId == selectedProfile.ActionId,
                out command);
        }

        private bool TryBuildCommand(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            RuntimeAiCharacterCommandProfile profile,
            bool emitActionRequest,
            out CharacterCommand command)
        {
            string traceId = BuildTraceId(frame, profile);
            CharacterActionRequest request = emitActionRequest
                ? CreateActionRequest(frame, entity, profile, traceId)
                : default;
            command = new CharacterCommand(
                frame,
                _options.SourceId,
                entity,
                profile.MoveDirection,
                profile.FacingBasis,
                profile.JumpPressed,
                profile.SprintHeld,
                GetButtons(profile, emitActionRequest),
                request,
                profile.MoveSpeedScale,
                traceId);
            _lastProfile = profile;
            _hasLastProfile = true;
            _lastCommand = command;
            SetDiagnostics(RuntimeAiPlannerCharacterCommandSuppressedReason.None, frame, profile.ActionId, command, Diagnostics.LastGoal);
            return true;
        }

        private CharacterActionRequest CreateActionRequest(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            RuntimeAiCharacterCommandProfile profile,
            string traceId)
        {
            if (!profile.HasActionRequest)
            {
                return default;
            }

            if (profile.ActionKind == CharacterActionKind.Cancel)
            {
                return CharacterActionRequest.Cancel(frame, entity, _options.SourceId, traceId);
            }

            if (profile.CombatActionId > 0)
            {
                return CharacterActionRequest.CombatAction(
                    frame,
                    entity,
                    profile.ActionKind,
                    profile.CombatActionId,
                    sourceId: _options.SourceId,
                    traceId: traceId,
                    forceStart: profile.ForceStart,
                    queueIfBusy: profile.QueueIfBusy);
            }

            if (profile.GameplayAbilityId > 0)
            {
                GameplayEntityId target = _options.TargetResolver != null
                    ? _options.TargetResolver(_worldState)
                    : default;
                return target.IsValid
                    ? CharacterActionRequest.GameplayAbility(frame, entity, profile.GameplayAbilityId, target, _options.SourceId, traceId, profile.QueueIfBusy)
                    : CharacterActionRequest.GameplayAbility(
                        frame,
                        entity,
                        profile.GameplayAbilityId,
                        sourceId: _options.SourceId,
                        traceId: traceId,
                        queueIfBusy: profile.QueueIfBusy);
            }

            return default;
        }

        private bool TargetFactsMissing()
        {
            return _options.RequireTargetFacts
                && _options.TargetRequiredFactKey.IsValid
                && !_worldState.Contains(_options.TargetRequiredFactKey);
        }

        private void ClearCachedCommand(
            RuntimeAiPlannerCharacterCommandSuppressedReason reason,
            RuntimeFrame frame)
        {
            _lastProfile = default;
            _hasLastProfile = false;
            _pendingProfile = default;
            _hasPendingProfile = false;
            _lastCommand = default;
            _lastActionId = 0;
            SetDiagnostics(reason, frame, 0, default, string.Empty);
        }

        private void SetDiagnostics(
            RuntimeAiPlannerCharacterCommandSuppressedReason reason,
            RuntimeFrame frame,
            int actionId,
            CharacterCommand command,
            string goal)
        {
            Diagnostics = new RuntimeAiPlannerCharacterCommandDiagnostics(
                reason,
                goal,
                actionId,
                frame,
                command);
        }

        private string BuildTraceId(RuntimeFrame frame, RuntimeAiCharacterCommandProfile profile)
        {
            string prefix = _options.TracePrefix ?? string.Empty;
            return string.IsNullOrEmpty(profile.TraceTag)
                ? $"{prefix}:{frame.Value}:{profile.ActionId}"
                : $"{prefix}:{frame.Value}:{profile.TraceTag}";
        }

        private static CharacterActionButtons GetButtons(RuntimeAiCharacterCommandProfile profile, bool emitActionRequest)
        {
            if (!emitActionRequest)
                return CharacterActionButtons.None;

            switch (profile.ActionKind)
            {
                case CharacterActionKind.Attack:
                    return CharacterActionButtons.Primary;
                case CharacterActionKind.Skill:
                case CharacterActionKind.GameplayAbility:
                    return CharacterActionButtons.Skill1;
                case CharacterActionKind.Interact:
                    return CharacterActionButtons.Interact;
                case CharacterActionKind.Dodge:
                    return CharacterActionButtons.Dodge;
                case CharacterActionKind.Cancel:
                    return CharacterActionButtons.Cancel;
                default:
                    return CharacterActionButtons.None;
            }
        }

        private static RuntimeFrame AddFrames(RuntimeFrame frame, int frames)
        {
            if (frames <= 0)
            {
                return frame;
            }

            return new RuntimeFrame(checked(frame.Value + frames));
        }

        private static string DescribeGoal(IAiGoal goal)
        {
            return goal == null ? string.Empty : goal.GetType().Name;
        }

        private static List<IAiGoal> CopyGoals(IEnumerable<IAiGoal> goals)
        {
            if (goals == null)
                throw new ArgumentNullException(nameof(goals));

            var list = new List<IAiGoal>();
            foreach (IAiGoal goal in goals)
            {
                if (goal != null)
                {
                    list.Add(goal);
                }
            }

            return list;
        }

        private static List<IAiAction> CopyActions(IEnumerable<IAiAction> actions)
        {
            if (actions == null)
                throw new ArgumentNullException(nameof(actions));

            var list = new List<IAiAction>();
            foreach (IAiAction action in actions)
            {
                if (action != null)
                {
                    list.Add(action);
                }
            }

            return list;
        }

        private static void ValidateOptions(RuntimeAiPlannerCharacterCommandSourceOptions options)
        {
            if (options.ReactionDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(options.ReactionDelayFrames));
            if (options.MinDecisionIntervalFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(options.MinDecisionIntervalFrames));
            if (options.CommandSmoothingFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(options.CommandSmoothingFrames));
        }
    }
}
