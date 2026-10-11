using MxFramework.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.AI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Prepared text content identity for authoring/import boundaries. Runtime state hashing
    /// consumes the retained digest and never computes SHA-256 in a tick or contribution call.
    /// </summary>
    internal static class RuntimeTextContentDigest
    {
        public static RuntimeHashDigest Prepare(string value) =>
            RuntimeHashDigest.ComputeContentDigest(value);
    }

    public enum CharacterTestRuntimeAiPlannerActionKind
    {
        Wait = 0,
        MoveToTarget = 1,
        Strafe = 2,
        Retreat = 3,
        PrimaryAttack = 4,
        CastAbilitySlot = 5,
        Dodge = 6
    }

    /// <summary>
    /// Describes the selected target lifecycle without conflating an absent target with an alive one.
    /// </summary>
    public enum CharacterTestRuntimeAiPlannerTargetLifeState
    {
        None = 0,
        Alive = 1,
        Dying = 2,
        Dead = 3,
        Reviving = 4
    }

    [Flags]
    internal enum CharacterTestRuntimeAiPlannerTargetStateMask : uint
    {
        None = 0,
        NoTarget = 1u << 0,
        Alive = 1u << 1,
        Dying = 1u << 2,
        Dead = 1u << 3,
        Reviving = 1u << 4,
        PhaseMask = NoTarget | Alive | Dying | Dead | Reviving,
        ExistingPhaseMask = Alive | Dying | Dead | Reviving,
        Perceivable = 1u << 8,
        LineOfSightBlocked = 1u << 9,
        InAttackRange = 1u << 10,
        MovePathAvailable = 1u << 11,
        AttackThreatToSelf = 1u << 12,
        AttackInstanceResponded = 1u << 13,
        FlagMask = Perceivable | LineOfSightBlocked | InAttackRange | MovePathAvailable
            | AttackThreatToSelf | AttackInstanceResponded
    }

    internal static class CharacterTestRuntimeAiPlannerTargetStatePolicy
    {
        internal static bool HasSinglePhase(CharacterTestRuntimeAiPlannerTargetStateMask state)
        {
            uint phases = (uint)(state & CharacterTestRuntimeAiPlannerTargetStateMask.PhaseMask);
            return phases != 0u && (phases & (phases - 1u)) == 0u;
        }

        internal static bool Matches(
            CharacterTestRuntimeAiPlannerTargetStateMask state,
            uint allowedPhases,
            uint requiredFlags,
            uint forbiddenFlags) =>
            ((uint)state & allowedPhases) != 0u
            && ((uint)state & requiredFlags) == requiredFlags
            && ((uint)state & forbiddenFlags) == 0u;
    }

    public enum CharacterTestRuntimeAiPlannerFactKind
    {
        TargetExists = 0,
        TargetInAttackRange = 1,
        TargetDistance = 2,
        SelfActionRunning = 3,
        MovePathAvailable = 4,
        TargetVisible = 5,
        TargetLineOfSightBlocked = 6,
        TargetLastSeenAgeFrames = 7,
        TargetThreatScore = 8,
        SelfSurfaceKind = 9,
        SelfOnHardSlick = 10,
        SelfNearMovableObject = 11,
        MoveDirectPathClear = 12,
        MoveCandidateIndex = 13,
        EnvironmentNearObstacle = 14,
        EnvironmentNearHazard = 15,
        EnvironmentNearUsefulObject = 16,
        TargetAttackWillHitSelf = 17,
        TargetAttackImpactFrames = 18,
        TargetAttackInstanceResponded = 19,
        SelfDodgeCooldownReady = 20,
        TargetDead = 21,
        TargetLifeState = 22,
        TargetDistanceAvailable = 23,
        TargetRootDistance = 24
    }

    public enum CharacterTestRuntimeAiPlannerConditionOperator
    {
        Equals = 0,
        NotEquals = 1,
        GreaterOrEqual = 2,
        LessOrEqual = 3
    }

    public enum CharacterTestRuntimeAiPlannerDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct CharacterTestRuntimeAiPlannerConditionDefinition
    {
        public CharacterTestRuntimeAiPlannerConditionDefinition(
            CharacterTestRuntimeAiPlannerFactKind fact,
            CharacterTestRuntimeAiPlannerConditionOperator op,
            bool boolValue,
            float floatValue = 0f)
        {
            Fact = fact;
            Operator = op;
            BoolValue = boolValue;
            FloatValue = floatValue;
        }

        public CharacterTestRuntimeAiPlannerFactKind Fact { get; }
        public CharacterTestRuntimeAiPlannerConditionOperator Operator { get; }
        public bool BoolValue { get; }
        public float FloatValue { get; }

        public static CharacterTestRuntimeAiPlannerConditionDefinition Bool(
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool value)
        {
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                fact,
                CharacterTestRuntimeAiPlannerConditionOperator.Equals,
                value);
        }

        /// <summary>
        /// Creates a condition for the selected target lifecycle state.
        /// </summary>
        /// <remarks>LowFreqAlloc: creates one immutable condition definition.</remarks>
        public static CharacterTestRuntimeAiPlannerConditionDefinition LifeState(
            CharacterTestRuntimeAiPlannerTargetLifeState value,
            CharacterTestRuntimeAiPlannerConditionOperator op =
                CharacterTestRuntimeAiPlannerConditionOperator.Equals)
        {
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                CharacterTestRuntimeAiPlannerFactKind.TargetLifeState,
                op,
                boolValue: false,
                floatValue: (int)value);
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerStateDefinition
    {
        public CharacterTestRuntimeAiPlannerStateDefinition(
            int stateId,
            int parentStateId = 0,
            string displayName = "")
        {
            StateId = stateId;
            ParentStateId = parentStateId;
            DisplayName = displayName ?? string.Empty;
        }

        public int StateId { get; }
        public int ParentStateId { get; }
        public string DisplayName { get; }
    }

    public sealed class CharacterTestRuntimeAiPlannerTransitionDefinition
    {
        public CharacterTestRuntimeAiPlannerTransitionDefinition(
            int sourceStateId,
            int targetStateId,
            int priority,
            CharacterTestRuntimeAiPlannerConditionDefinition condition,
            string displayName = "")
        {
            SourceStateId = sourceStateId;
            TargetStateId = targetStateId;
            Priority = priority;
            Condition = condition;
            DisplayName = displayName ?? string.Empty;
        }

        public int SourceStateId { get; }
        public int TargetStateId { get; }
        public int Priority { get; }
        public CharacterTestRuntimeAiPlannerConditionDefinition Condition { get; }
        public string DisplayName { get; }
    }

    public sealed class CharacterTestRuntimeAiPlannerActionDefinition
    {
        private readonly CharacterTestRuntimeAiPlannerConditionDefinition[] _conditions;

        public CharacterTestRuntimeAiPlannerActionDefinition(
            int actionId,
            int stateId,
            CharacterTestRuntimeAiPlannerActionKind kind,
            float score,
            float cost,
            string displayName = "",
            int abilitySlot = 0,
            int abilityId = 0,
            IEnumerable<CharacterTestRuntimeAiPlannerConditionDefinition> conditions = null,
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings = null,
            int reactionCooldownFrames = 0)
        {
            ActionId = actionId;
            StateId = stateId;
            Kind = kind;
            Score = score;
            Cost = cost;
            DisplayName = displayName ?? string.Empty;
            AbilitySlot = abilitySlot;
            AbilityId = abilityId;
            _conditions = CopyConditions(conditions);
            MovementSettings = movementSettings;
            ReactionCooldownFrames = reactionCooldownFrames > 0 ? reactionCooldownFrames : 0;
        }

        public int ActionId { get; }
        public int StateId { get; }
        public CharacterTestRuntimeAiPlannerActionKind Kind { get; }
        public float Score { get; }
        public float Cost { get; }
        public string DisplayName { get; }
        public int AbilitySlot { get; }
        public int AbilityId { get; }
        public CharacterTestRuntimeAiPlannerMovementSettings MovementSettings { get; }
        public int ReactionCooldownFrames { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerConditionDefinition> Conditions => _conditions;

        internal CharacterTestRuntimeAiPlannerConditionDefinition[] CopyConditions()
        {
            var copy = new CharacterTestRuntimeAiPlannerConditionDefinition[_conditions.Length];
            Array.Copy(_conditions, copy, _conditions.Length);
            return copy;
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition[] CopyConditions(
            IEnumerable<CharacterTestRuntimeAiPlannerConditionDefinition> conditions)
        {
            if (conditions == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerConditionDefinition>();

            var copy = new List<CharacterTestRuntimeAiPlannerConditionDefinition>();
            foreach (CharacterTestRuntimeAiPlannerConditionDefinition condition in conditions)
                copy.Add(condition);
            return copy.ToArray();
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerPerceptionSettings
    {
        public const float DefaultVisionRange = 12f;
        public const int DefaultTargetMemoryFrames = 30;
        public const int DefaultLineOfSightObstacleLayerMask = 0;
        public const float DefaultEnvironmentSampleRadius = 2.5f;
        public const float DefaultThreatDistanceWeight = 1f;

        public CharacterTestRuntimeAiPlannerPerceptionSettings(
            float visionRange = DefaultVisionRange,
            int targetMemoryFrames = DefaultTargetMemoryFrames,
            int lineOfSightObstacleLayerMask = DefaultLineOfSightObstacleLayerMask,
            float environmentSampleRadius = DefaultEnvironmentSampleRadius,
            float threatDistanceWeight = DefaultThreatDistanceWeight)
        {
            VisionRange = visionRange;
            TargetMemoryFrames = targetMemoryFrames;
            LineOfSightObstacleLayerMask = lineOfSightObstacleLayerMask;
            EnvironmentSampleRadius = environmentSampleRadius;
            ThreatDistanceWeight = threatDistanceWeight;
        }

        public float VisionRange { get; }
        public int TargetMemoryFrames { get; }
        public int LineOfSightObstacleLayerMask { get; }
        public float EnvironmentSampleRadius { get; }
        public float ThreatDistanceWeight { get; }

        public static CharacterTestRuntimeAiPlannerPerceptionSettings CreateDefault()
        {
            return new CharacterTestRuntimeAiPlannerPerceptionSettings();
        }

        internal static CharacterTestRuntimeAiPlannerPerceptionSettings Normalize(
            CharacterTestRuntimeAiPlannerPerceptionSettings settings)
        {
            return settings ?? CreateDefault();
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerMovementSettings
    {
        public const float DefaultSpeedScale = 1f;
        public const float DefaultSteeringResponsiveness = 0.35f;
        public const float DefaultApproachLateralBlend = 0.08f;
        public const float DefaultPreferredDistanceMin = 0f;
        public const float DefaultPreferredDistanceMax = 0f;
        public const float DefaultArrivalSlowdownDistance = 1.2f;
        public const int DefaultStrafeSwitchMinFrames = 24;
        public const int DefaultStrafeSwitchMaxFrames = 72;
        public const int DefaultDirectionHoldFrames = 6;
        public const int DefaultReactionDelayFrames = 0;

        public CharacterTestRuntimeAiPlannerMovementSettings(
            float speedScale = DefaultSpeedScale,
            float steeringResponsiveness = DefaultSteeringResponsiveness,
            float approachLateralBlend = DefaultApproachLateralBlend,
            float preferredDistanceMin = DefaultPreferredDistanceMin,
            float preferredDistanceMax = DefaultPreferredDistanceMax,
            float arrivalSlowdownDistance = DefaultArrivalSlowdownDistance,
            int strafeSwitchMinFrames = DefaultStrafeSwitchMinFrames,
            int strafeSwitchMaxFrames = DefaultStrafeSwitchMaxFrames,
            int directionHoldFrames = DefaultDirectionHoldFrames,
            int reactionDelayFrames = DefaultReactionDelayFrames)
        {
            SpeedScale = speedScale;
            SteeringResponsiveness = steeringResponsiveness;
            ApproachLateralBlend = approachLateralBlend;
            PreferredDistanceMin = preferredDistanceMin;
            PreferredDistanceMax = preferredDistanceMax;
            ArrivalSlowdownDistance = arrivalSlowdownDistance;
            StrafeSwitchMinFrames = strafeSwitchMinFrames;
            StrafeSwitchMaxFrames = strafeSwitchMaxFrames;
            DirectionHoldFrames = directionHoldFrames;
            ReactionDelayFrames = reactionDelayFrames;
        }

        public float SpeedScale { get; }
        public float SteeringResponsiveness { get; }
        public float ApproachLateralBlend { get; }
        public float PreferredDistanceMin { get; }
        public float PreferredDistanceMax { get; }
        public float ArrivalSlowdownDistance { get; }
        public int StrafeSwitchMinFrames { get; }
        public int StrafeSwitchMaxFrames { get; }
        public int DirectionHoldFrames { get; }
        public int ReactionDelayFrames { get; }

        public static CharacterTestRuntimeAiPlannerMovementSettings CreateDefault()
        {
            return new CharacterTestRuntimeAiPlannerMovementSettings();
        }

        internal static CharacterTestRuntimeAiPlannerMovementSettings Normalize(
            CharacterTestRuntimeAiPlannerMovementSettings settings)
        {
            return settings ?? CreateDefault();
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerProfile
    {
        public const int FirstAuthoredStateId = 1000;
        public const int FirstAuthoredActionId = 1000000;
        private readonly CharacterTestRuntimeAiPlannerStateDefinition[] _states;
        private readonly CharacterTestRuntimeAiPlannerTransitionDefinition[] _transitions;
        private readonly CharacterTestRuntimeAiPlannerActionDefinition[] _actions;

        public CharacterTestRuntimeAiPlannerProfile(
            string profileId = "",
            int initialStateId = 0,
            CharacterTestAiTargetSelectionMode targetSelectionMode =
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
            float attackRange = CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
            IEnumerable<CharacterTestRuntimeAiPlannerStateDefinition> states = null,
            IEnumerable<CharacterTestRuntimeAiPlannerTransitionDefinition> transitions = null,
            IEnumerable<CharacterTestRuntimeAiPlannerActionDefinition> actions = null,
            CharacterTestRuntimeAiPlannerPerceptionSettings perceptionSettings = null,
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings = null,
            int nextStateId = FirstAuthoredStateId,
            IEnumerable<int> retiredStateIds = null,
            int nextActionId = FirstAuthoredActionId,
            IEnumerable<int> retiredActionIds = null)
        {
            ProfileId = string.IsNullOrWhiteSpace(profileId)
                ? CharacterTestRuntimeAiPlannerProfileSource.DefaultProfileId
                : profileId;
            InitialStateId = initialStateId;
            TargetSelectionMode = targetSelectionMode;
            AttackRange = attackRange;
            PerceptionSettings = CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(perceptionSettings);
            MovementSettings = CharacterTestRuntimeAiPlannerMovementSettings.Normalize(movementSettings);
            _states = Copy(states);
            _transitions = Copy(transitions);
            _actions = Copy(actions);
            if (nextStateId <= 0 || nextActionId <= 0) throw new ArgumentOutOfRangeException("Authoring identity counters must be positive.");
            NextStateId = nextStateId;
            NextActionId = nextActionId;
            RetiredStateIds = ValidateRetired(retiredStateIds, nextStateId);
            RetiredActionIds = ValidateRetired(retiredActionIds, nextActionId);
            for (int i = 0; i < _states.Length; i++)
                if (_states[i].StateId >= nextStateId || Contains(RetiredStateIds, _states[i].StateId))
                    throw new ArgumentException("State identity conflicts with its persisted authoring ledger.");
            for (int i = 0; i < _actions.Length; i++)
                if (_actions[i].ActionId >= nextActionId || Contains(RetiredActionIds, _actions[i].ActionId))
                    throw new ArgumentException("Action identity conflicts with its persisted authoring ledger.");
        }

        public int NextStateId { get; }
        public int NextActionId { get; }
        public IReadOnlyList<int> RetiredStateIds { get; }
        public IReadOnlyList<int> RetiredActionIds { get; }
        public string ProfileId { get; }
        public int InitialStateId { get; }
        public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; }
        public float AttackRange { get; }
        public CharacterTestRuntimeAiPlannerPerceptionSettings PerceptionSettings { get; }
        public CharacterTestRuntimeAiPlannerMovementSettings MovementSettings { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerStateDefinition> States => _states;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerTransitionDefinition> Transitions => _transitions;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerActionDefinition> Actions => _actions;

        internal CharacterTestRuntimeAiPlannerStateDefinition[] CopyStates()
        {
            return Copy(_states);
        }

        internal CharacterTestRuntimeAiPlannerTransitionDefinition[] CopyTransitions()
        {
            return Copy(_transitions);
        }

        internal CharacterTestRuntimeAiPlannerActionDefinition[] CopyActions()
        {
            return Copy(_actions);
        }

        private static IReadOnlyList<int> ValidateRetired(IEnumerable<int> source, int next)
        {
            int[] ids = Copy(source);
            Array.Sort(ids);
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] <= 0 || ids[i] >= next || (i > 0 && ids[i - 1] == ids[i]))
                    throw new ArgumentException("Retired identity ledger is invalid.");
            return Array.AsReadOnly(ids);
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true;
            return false;
        }

        private static T[] Copy<T>(IEnumerable<T> source)
        {
            if (source == null)
                return Array.Empty<T>();

            if (source is T[] array)
            {
                var copy = new T[array.Length];
                Array.Copy(array, copy, array.Length);
                return copy;
            }

            var list = new List<T>();
            foreach (T item in source)
            {
                if (item != null)
                    list.Add(item);
            }

            return list.ToArray();
        }
    }

    public static class CharacterTestRuntimeAiPlannerProfileSource
    {
        public const string DefaultProfileId = "character-test.runtime-ai-planner.default";
        public const int RootStateId = 1;
        public const int EngageStateId = 3;
        public const int ChaseStateId = 8;
        public const int AttackStateId = 9;
        public const int DefaultWaitActionId = 910000;
        private const int InactiveTargetStateId = 10;
        private const int InactiveTargetWaitActionId = DefaultWaitActionId + 3;
        public const int DefaultMoveToTargetActionId =
            CharacterTestAiBehaviorProfileSource.DefaultMoveToTargetActionId;
        public const int DefaultPrimaryAttackActionId =
            CharacterTestAiBehaviorProfileSource.DefaultLightAttackActionId;

        /// <summary>
        /// LowFreqAlloc: creates the default authoring DTO and its arrays.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerProfile CreateDefault()
        {
            return CreateDefault(CharacterTestAiBehaviorProfileSource.CreateDefault());
        }

        /// <summary>
        /// LowFreqAlloc: maps the legacy code profile into the v1 HFSM + Utility profile.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerProfile CreateDefault(CharacterTestAiBehaviorProfile legacyProfile)
        {
            CharacterTestAiBehaviorProfile profile = CharacterTestAiBehaviorProfileSource.Normalize(legacyProfile);
            return new CharacterTestRuntimeAiPlannerProfile(
                DefaultProfileId,
                ChaseStateId,
                profile.TargetSelectionMode,
                profile.AttackRange,
                new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(RootStateId, displayName: "根"),
                    new CharacterTestRuntimeAiPlannerStateDefinition(EngageStateId, RootStateId, "战斗"),
                    new CharacterTestRuntimeAiPlannerStateDefinition(ChaseStateId, EngageStateId, "追击"),
                    new CharacterTestRuntimeAiPlannerStateDefinition(AttackStateId, EngageStateId, "攻击"),
                    new CharacterTestRuntimeAiPlannerStateDefinition(
                        InactiveTargetStateId,
                        RootStateId,
                        "目标失活")
                },
                new[]
                {
                    new CharacterTestRuntimeAiPlannerTransitionDefinition(
                        0,
                        InactiveTargetStateId,
                        30,
                        CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                            CharacterTestRuntimeAiPlannerTargetLifeState.Alive,
                            CharacterTestRuntimeAiPlannerConditionOperator.NotEquals),
                        "目标失活"),
                    new CharacterTestRuntimeAiPlannerTransitionDefinition(
                        0,
                        ChaseStateId,
                        10,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                            false),
                        "目标离开攻击范围"),
                    new CharacterTestRuntimeAiPlannerTransitionDefinition(
                        0,
                        AttackStateId,
                        20,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                            true),
                        "目标进入攻击范围")
                },
                new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        profile.MoveToTargetActionId,
                        ChaseStateId,
                        CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        score: 50f,
                        cost: 1f,
                        displayName: "移动到目标",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                                CharacterTestRuntimeAiPlannerTargetLifeState.Alive),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                                false)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        profile.LightAttackActionId,
                        AttackStateId,
                        CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        score: 100f,
                        cost: 1f,
                        displayName: "轻攻击",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                                CharacterTestRuntimeAiPlannerTargetLifeState.Alive),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning,
                                false)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        DefaultWaitActionId,
                        AttackStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 0f,
                        cost: 0f,
                        displayName: "等待",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        InactiveTargetWaitActionId,
                        InactiveTargetStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 0f,
                        cost: 0f,
                        displayName: "等待",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true)
                        })
                });
        }

        public static CharacterTestRuntimeAiPlannerProfile Normalize(
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestAiBehaviorProfile fallbackLegacyProfile = null)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                profile ?? CreateDefault(fallbackLegacyProfile);
            // A deliberately blank behavior group still owns a valid state machine,
            // but has no Utility actions until the author adds the first card.
            if (source.States.Count > 0)
                return source;

            return CreateDefault(fallbackLegacyProfile);
        }
    }

    public readonly struct CharacterTestRuntimeAiPlannerDiagnostic
    {
        public CharacterTestRuntimeAiPlannerDiagnostic(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }

        public override string ToString()
        {
            return Severity + " " + Code + ": " + Message;
        }
    }

    public sealed class CharacterTestRuntimeAiPlannerValidationReport
    {
        private readonly CharacterTestRuntimeAiPlannerDiagnostic[] _diagnostics;

        public CharacterTestRuntimeAiPlannerValidationReport(
            IEnumerable<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            _diagnostics = Copy(diagnostics);
            HasErrors = HasSeverity(_diagnostics, CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error);
        }

        public IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> Diagnostics => _diagnostics;
        public bool HasErrors { get; }
        public bool Success => !HasErrors;

        public string ToText()
        {
            if (_diagnostics.Length == 0)
                return "Runtime AI Planner profile validation passed.";

            var builder = new StringBuilder();
            for (int i = 0; i < _diagnostics.Length; i++)
            {
                if (i > 0)
                    builder.AppendLine();
                builder.Append(_diagnostics[i].ToString());
            }

            return builder.ToString();
        }

        private static bool HasSeverity(
            CharacterTestRuntimeAiPlannerDiagnostic[] diagnostics,
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == severity)
                    return true;
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerDiagnostic[] Copy(
            IEnumerable<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerDiagnostic>();

            var copy = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            foreach (CharacterTestRuntimeAiPlannerDiagnostic diagnostic in diagnostics)
                copy.Add(diagnostic);
            return copy.ToArray();
        }
    }

    public static class CharacterTestRuntimeAiPlannerProfileValidator
    {
        /// <summary>
        /// AllocByDesign: editor/build-time validation report.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerValidationReport Validate(
            CharacterTestRuntimeAiPlannerProfile profile)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            CharacterTestRuntimeAiPlannerProfile source =
                profile ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            ValidateCore(source, diagnostics);
            return new CharacterTestRuntimeAiPlannerValidationReport(diagnostics);
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ValidateCore(
            CharacterTestRuntimeAiPlannerProfile profile,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (!IsFinite(profile.AttackRange) || profile.AttackRange <= 0f)
                Error(diagnostics, "profile.attackRange", "Attack range must be a positive finite value.");
            ValidatePerceptionSettings(profile.PerceptionSettings, diagnostics);
            ValidateMovementSettings(profile.MovementSettings, diagnostics, "movement");

            var states = profile.CopyStates();
            var actions = profile.CopyActions();
            var transitions = profile.CopyTransitions();
            if (states.Length == 0)
                Error(diagnostics, "state.missing", "At least one state is required.");
            var stateIds = new HashSet<int>();
            var actionIds = new HashSet<int>();
            for (int i = 0; i < states.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStateDefinition state = states[i];
                if (state == null || (state.StateId <= 0))
                {
                    Error(diagnostics, "state.id.empty", "State id cannot be empty.");
                    continue;
                }

                if (!stateIds.Add(state.StateId))
                    Error(diagnostics, "state.id.duplicate", "Duplicate state id: " + state.StateId);
            }

            if (profile.InitialStateId <= 0 || !stateIds.Contains(profile.InitialStateId))
                Error(diagnostics, "state.initial.missing", "Initial state is missing: " + profile.InitialStateId);

            for (int i = 0; i < states.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStateDefinition state = states[i];
                if (state == null || (state.ParentStateId <= 0))
                    continue;

                if (!stateIds.Contains(state.ParentStateId))
                    Error(diagnostics, "state.parent.missing", "State parent is missing: " + state.ParentStateId);
            }

            DetectParentCycles(states, diagnostics);

            for (int i = 0; i < transitions.Length; i++)
            {
                CharacterTestRuntimeAiPlannerTransitionDefinition transition = transitions[i];
                if (transition == null)
                    continue;

                if (!(transition.SourceStateId <= 0)
                    && !stateIds.Contains(transition.SourceStateId))
                {
                    Error(
                        diagnostics,
                        "transition.source.missing",
                        "Transition source state is missing: " + transition.SourceStateId);
                }

                if ((transition.TargetStateId <= 0)
                    || !stateIds.Contains(transition.TargetStateId))
                {
                    Error(
                        diagnostics,
                        "transition.target.missing",
                        "Transition target state is missing: " + transition.TargetStateId);
                }

                ValidateCondition(transition.Condition, diagnostics, "transition.condition");
            }

            for (int i = 0; i < actions.Length; i++)
            {
                CharacterTestRuntimeAiPlannerActionDefinition action = actions[i];
                if (action == null)
                    continue;

                if (action.ActionId <= 0)
                    Error(diagnostics, "action.id.invalid", "Action id must be positive.");
                else if (!actionIds.Add(action.ActionId))
                    Error(diagnostics, "action.id.duplicate", "Duplicate action id: " + action.ActionId);

                if ((action.StateId <= 0) || !stateIds.Contains(action.StateId))
                    Error(diagnostics, "action.state.missing", "Action state is missing: " + action.StateId);

                if (!IsFinite(action.Score))
                    Error(diagnostics, "action.score.invalid", "Action score must be finite. id=" + action.ActionId);
                if (!IsFinite(action.Cost) || action.Cost < 0f)
                    Error(diagnostics, "action.cost.invalid", "Action cost must be non-negative and finite. id=" + action.ActionId);
                if (!IsValidActionKind(action.Kind))
                    Error(diagnostics, "action.output.invalid", "Action output mapping is invalid. id=" + action.ActionId);
                if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                    && (action.AbilitySlot < 1 || action.AbilitySlot > CharacterTestControlIntent.SkillSlotCount))
                {
                    Error(
                        diagnostics,
                        "action.output.slot.missing",
                        "CastAbilitySlot action requires an ability slot from 1 to 9. id=" + action.ActionId);
                }

                ValidateMovementSettings(
                    action.MovementSettings,
                    diagnostics,
                    "action.movement");

                for (int c = 0; c < action.Conditions.Count; c++)
                    ValidateCondition(action.Conditions[c], diagnostics, "action.condition");
            }
        }

        private static void ValidatePerceptionSettings(
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            CharacterTestRuntimeAiPlannerPerceptionSettings source =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
            if (!IsFinite(source.VisionRange) || source.VisionRange <= 0f)
                Error(diagnostics, "perception.visionRange", "Vision range must be a positive finite value.");
            if (source.TargetMemoryFrames < 0)
                Error(diagnostics, "perception.targetMemoryFrames", "Target memory frames cannot be negative.");
            if (source.LineOfSightObstacleLayerMask < 0)
                Error(diagnostics, "perception.lineOfSightObstacleLayerMask", "Line-of-sight obstacle layer mask cannot be negative.");
            if (!IsFinite(source.EnvironmentSampleRadius) || source.EnvironmentSampleRadius < 0f)
                Error(diagnostics, "perception.environmentSampleRadius", "Environment sample radius must be non-negative and finite.");
            if (!IsFinite(source.ThreatDistanceWeight) || source.ThreatDistanceWeight < 0f)
                Error(diagnostics, "perception.threatDistanceWeight", "Threat distance weight must be non-negative and finite.");
        }

        private static void ValidateMovementSettings(
            CharacterTestRuntimeAiPlannerMovementSettings settings,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics,
            string codePrefix)
        {
            if (settings == null)
                return;

            if (!IsFinite(settings.SpeedScale) || settings.SpeedScale < 0f || settings.SpeedScale > 1f)
                Error(diagnostics, codePrefix + ".speedScale", "Movement speedScale must be finite and between 0 and 1.");
            if (!IsFinite(settings.SteeringResponsiveness)
                || settings.SteeringResponsiveness < 0f
                || settings.SteeringResponsiveness > 1f)
            {
                Error(diagnostics, codePrefix + ".steeringResponsiveness", "Movement steeringResponsiveness must be finite and between 0 and 1.");
            }

            if (!IsFinite(settings.ApproachLateralBlend)
                || settings.ApproachLateralBlend < 0f
                || settings.ApproachLateralBlend > 1f)
            {
                Error(diagnostics, codePrefix + ".approachLateralBlend", "Movement approachLateralBlend must be finite and between 0 and 1.");
            }

            if (!IsFinite(settings.PreferredDistanceMin) || settings.PreferredDistanceMin < 0f)
                Error(diagnostics, codePrefix + ".preferredDistanceMin", "Movement preferredDistanceMin must be non-negative and finite.");
            if (!IsFinite(settings.PreferredDistanceMax) || settings.PreferredDistanceMax < 0f)
                Error(diagnostics, codePrefix + ".preferredDistanceMax", "Movement preferredDistanceMax must be non-negative and finite.");
            if (IsFinite(settings.PreferredDistanceMin)
                && IsFinite(settings.PreferredDistanceMax)
                && settings.PreferredDistanceMin > settings.PreferredDistanceMax)
            {
                Error(diagnostics, codePrefix + ".preferredDistanceRange", "Movement preferredDistanceMin cannot exceed preferredDistanceMax.");
            }

            if (!IsFinite(settings.ArrivalSlowdownDistance) || settings.ArrivalSlowdownDistance < 0f)
                Error(diagnostics, codePrefix + ".arrivalSlowdownDistance", "Movement arrivalSlowdownDistance must be non-negative and finite.");
            if (settings.StrafeSwitchMinFrames < 0 || settings.StrafeSwitchMaxFrames < 0)
                Error(diagnostics, codePrefix + ".strafeSwitchFrames", "Movement strafe switch frame range cannot be negative.");
            if (settings.StrafeSwitchMinFrames > settings.StrafeSwitchMaxFrames)
                Error(diagnostics, codePrefix + ".strafeSwitchFrames", "Movement strafeSwitchMinFrames cannot exceed strafeSwitchMaxFrames.");
            if (settings.DirectionHoldFrames < 0)
                Error(diagnostics, codePrefix + ".directionHoldFrames", "Movement directionHoldFrames cannot be negative.");
            if (settings.ReactionDelayFrames < 0)
                Error(diagnostics, codePrefix + ".reactionDelayFrames", "Movement reactionDelayFrames cannot be negative.");
        }

        private static void DetectParentCycles(
            CharacterTestRuntimeAiPlannerStateDefinition[] states,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            var parents = new Dictionary<int, int>();
            for (int i = 0; i < states.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStateDefinition state = states[i];
                if (state == null || (state.StateId <= 0))
                    continue;

                parents[state.StateId] = state.ParentStateId;
            }

            foreach (KeyValuePair<int, int> entry in parents)
            {
                var visited = new HashSet<int>();
                int current = entry.Key;
                while (!(current <= 0))
                {
                    if (!visited.Add(current))
                    {
                        Error(diagnostics, "state.parent.cycle", "State parent cycle starts at: " + entry.Key);
                        break;
                    }

                    if (!parents.TryGetValue(current, out current))
                        break;
                }
            }
        }

        private static void ValidateCondition(
            CharacterTestRuntimeAiPlannerConditionDefinition condition,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics,
            string code)
        {
            if (!IsValidFactKind(condition.Fact))
                Error(diagnostics, code + ".fact.invalid", "Condition fact is invalid: " + condition.Fact);

            if ((condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual
                    || condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual)
                && !IsFinite(condition.FloatValue))
            {
                Error(diagnostics, code + ".float.invalid", "Condition float value must be finite.");
            }

            if (condition.Fact == CharacterTestRuntimeAiPlannerFactKind.TargetLifeState)
            {
                if (condition.Operator != CharacterTestRuntimeAiPlannerConditionOperator.Equals
                    && condition.Operator != CharacterTestRuntimeAiPlannerConditionOperator.NotEquals)
                {
                    Error(
                        diagnostics,
                        code + ".operator.invalid",
                        "Target life state only supports Equals and NotEquals.");
                }

                float value = condition.FloatValue;
                if (!IsFinite(value)
                    || value < (int)CharacterTestRuntimeAiPlannerTargetLifeState.None
                    || value > (int)CharacterTestRuntimeAiPlannerTargetLifeState.Reviving
                    || value != (int)value)
                {
                    Error(
                        diagnostics,
                        code + ".value.invalid",
                        "Target life state value is invalid.");
                }
            }
        }

        private static bool IsValidActionKind(CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.Wait:
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidFactKind(CharacterTestRuntimeAiPlannerFactKind fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistance:
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable:
                case CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance:
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                case CharacterTestRuntimeAiPlannerFactKind.TargetDead:
                case CharacterTestRuntimeAiPlannerFactKind.TargetLifeState:
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames:
                case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore:
                case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind:
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex:
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackWillHitSelf:
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackImpactFrames:
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackInstanceResponded:
                case CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady:
                    return true;
                default:
                    return false;
            }
        }

        private static void Error(
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics,
            string code,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                code,
                message));
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerCompiledProfile
    {
        private readonly int[] _stateIds;
        private readonly int[] _stateParentIds;
        private readonly string[] _stateDisplayNames;
        private readonly string[] _statePathText;
        private readonly RuntimeHashDigest[] _statePathDigests;
        private readonly RuntimeHashDigest[] _stateDisplayNameDigests;
        private static readonly RuntimeHashDigest EmptyStateTextDigest = RuntimeTextContentDigest.Prepare(string.Empty);
        private readonly CharacterTestRuntimeAiPlannerCompiledAction[] _actions;

        private CharacterTestRuntimeAiPlannerCompiledProfile(
            CharacterTestRuntimeAiPlannerProfile source,
            AiHfsmState[] hfsmStates,
            AiHfsmTransition[] hfsmTransitions,
            int[] stateIds,
            string[] stateDisplayNames,
            string[] statePathText,
            CharacterTestRuntimeAiPlannerCompiledAction[] actions)
        {
            Source = source;
            HfsmStates = hfsmStates;
            HfsmTransitions = hfsmTransitions;
            _stateIds = stateIds;
            _stateParentIds = new int[stateIds.Length];
            for (int i = 0; i < stateIds.Length; i++)
                _stateParentIds[i] = source.States[i].ParentStateId;
            _stateDisplayNames = stateDisplayNames;
            _statePathText = statePathText;
            _statePathDigests = new RuntimeHashDigest[statePathText.Length];
            _stateDisplayNameDigests = new RuntimeHashDigest[stateDisplayNames.Length];
            for (int i = 0; i < statePathText.Length; i++)
            {
                _statePathDigests[i] = RuntimeTextContentDigest.Prepare(statePathText[i]);
                _stateDisplayNameDigests[i] = RuntimeTextContentDigest.Prepare(stateDisplayNames[i]);
            }
            _actions = actions;
            InitialStateId = source.InitialStateId;
        }

        public CharacterTestRuntimeAiPlannerProfile Source { get; }
        public AiHfsmState[] HfsmStates { get; }
        public AiHfsmTransition[] HfsmTransitions { get; }
        public int InitialStateId { get; }
        public float AttackRange => Source.AttackRange;
        public CharacterTestAiTargetSelectionMode TargetSelectionMode => Source.TargetSelectionMode;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> Actions => _actions;
        public CharacterTestRuntimeAiPlannerPerceptionSettings PerceptionSettings =>
            Source.PerceptionSettings;
        public CharacterTestRuntimeAiPlannerMovementSettings MovementSettings =>
            Source.MovementSettings;

        public RuntimeHashDigest GetStatePathDigest(int stateId)
        {
            if (stateId == 0) return EmptyStateTextDigest;
            for (int i = 0; i < _stateIds.Length; i++) if (_stateIds[i] == stateId) return _statePathDigests[i];
            throw new ArgumentOutOfRangeException(nameof(stateId), "State does not belong to the prepared profile.");
        }

        public RuntimeHashDigest GetStateDisplayNameDigest(int stateId)
        {
            if (stateId == 0) return EmptyStateTextDigest;
            for (int i = 0; i < _stateIds.Length; i++) if (_stateIds[i] == stateId) return _stateDisplayNameDigests[i];
            throw new ArgumentOutOfRangeException(nameof(stateId), "State does not belong to the prepared profile.");
        }

        public static bool TryCompile(
            CharacterTestRuntimeAiPlannerProfile profile,
            out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
            out CharacterTestRuntimeAiPlannerValidationReport report)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                CharacterTestRuntimeAiPlannerProfileSource.Normalize(profile);
            report = CharacterTestRuntimeAiPlannerProfileValidator.Validate(source);
            if (!report.Success)
            {
                compiled = null;
                return false;
            }

            CharacterTestRuntimeAiPlannerStateDefinition[] states = source.CopyStates();
            CharacterTestRuntimeAiPlannerTransitionDefinition[] transitions = source.CopyTransitions();
            CharacterTestRuntimeAiPlannerActionDefinition[] actions = source.CopyActions();
            var hfsmStates = new AiHfsmState[states.Length];
            var stateIds = new int[states.Length];
            var stateDisplayNames = new string[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStateDefinition state = states[i];
                hfsmStates[i] = new AiHfsmState(state.StateId, state.ParentStateId);
                stateIds[i] = state.StateId;
                stateDisplayNames[i] = string.IsNullOrWhiteSpace(state.DisplayName)
                    ? state.StateId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : state.DisplayName;
            }

            var hfsmTransitions = new AiHfsmTransition[transitions.Length];
            for (int i = 0; i < transitions.Length; i++)
            {
                CharacterTestRuntimeAiPlannerTransitionDefinition transition = transitions[i];
                CharacterTestRuntimeAiPlannerConditionDefinition condition = transition.Condition;
                hfsmTransitions[i] = new AiHfsmTransition(
                    transition.SourceStateId,
                    transition.TargetStateId,
                    transition.Priority,
                    context => CharacterTestRuntimeAiPlannerConditionEvaluator.IsSatisfied(
                        (CharacterTestRuntimeAiPlannerFactState)context.WorldState,
                        condition));
            }

            var compiledActions = new CharacterTestRuntimeAiPlannerCompiledAction[actions.Length];
            var defaultMovement =
                CharacterTestRuntimeAiPlannerCompiledMovementSettings.FromSettings(source.MovementSettings);
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterTestRuntimeAiPlannerActionDefinition action = actions[i];
                compiledActions[i] = new CharacterTestRuntimeAiPlannerCompiledAction(
                    action.ActionId,
                    action.StateId,
                    action.Kind,
                    action.Score,
                    action.Cost,
                    string.IsNullOrWhiteSpace(action.DisplayName)
                        ? action.Kind.ToString()
                        : action.DisplayName,
                    action.AbilitySlot,
                    action.AbilityId,
                    action.CopyConditions(),
                    action.MovementSettings != null
                        ? CharacterTestRuntimeAiPlannerCompiledMovementSettings.FromSettings(action.MovementSettings)
                        : defaultMovement,
                    action.ReactionCooldownFrames);
            }

            compiled = new CharacterTestRuntimeAiPlannerCompiledProfile(
                source,
                hfsmStates,
                hfsmTransitions,
                stateIds,
                stateDisplayNames,
                BuildPathText(states),
                compiledActions);
            return true;
        }

        public string GetStateDisplayName(int stateId)
        {
            for (int i = 0; i < _stateIds.Length; i++)
            {
                if ((_stateIds[i] == stateId))
                    return _stateDisplayNames[i];
            }

            return stateId > 0 ? stateId.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        internal bool ContainsState(int stateId)
        {
            if (stateId <= 0) return false;
            for (int i = 0; i < _stateIds.Length; i++) if (_stateIds[i] == stateId) return true;
            return false;
        }

        public string GetStatePathText(int stateId)
        {
            for (int i = 0; i < _stateIds.Length; i++)
            {
                if ((_stateIds[i] == stateId))
                    return _statePathText[i];
            }

            return stateId > 0 ? stateId.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        public bool IsActionInActivePath(
            CharacterTestRuntimeAiPlannerCompiledAction action,
            IReadOnlyList<int> activePath)
        {
            if (activePath == null)
                return false;
            for (int i = 0; i < activePath.Count; i++)
            {
                if (activePath[i] == action.StateId)
                    return true;
            }
            return false;
        }

        private static string GetStateLabel(CharacterTestRuntimeAiPlannerStateDefinition[] states, int id)
        {
            for (int i = 0; i < states.Length; i++)
                if (states[i].StateId == id && !string.IsNullOrEmpty(states[i].DisplayName)) return states[i].DisplayName;
            return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string[] BuildPathText(CharacterTestRuntimeAiPlannerStateDefinition[] states)
        {
            var parents = new Dictionary<int, int>();
            for (int i = 0; i < states.Length; i++)
                parents[states[i].StateId] = states[i].ParentStateId;

            var paths = new string[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                var reversed = new List<string>();
                int current = states[i].StateId;
                while (!(current <= 0))
                {
                    reversed.Add(GetStateLabel(states, current));
                    if (!parents.TryGetValue(current, out current))
                        break;
                }

                reversed.Reverse();
                paths[i] = string.Join("/", reversed.ToArray());
            }

            return paths;
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerCompiledMovementSettings
    {
        public CharacterTestRuntimeAiPlannerCompiledMovementSettings(
            float speedScale,
            float steeringResponsiveness,
            float approachLateralBlend,
            float preferredDistanceMin,
            float preferredDistanceMax,
            float arrivalSlowdownDistance,
            int strafeSwitchMinFrames,
            int strafeSwitchMaxFrames,
            int directionHoldFrames,
            int reactionDelayFrames)
        {
            SpeedScale = Clamp01(speedScale);
            SteeringResponsiveness = Clamp01(steeringResponsiveness);
            ApproachLateralBlend = Clamp01(approachLateralBlend);
            PreferredDistanceMin = preferredDistanceMin > 0f ? preferredDistanceMin : 0f;
            PreferredDistanceMax = preferredDistanceMax > 0f ? preferredDistanceMax : 0f;
            ArrivalSlowdownDistance = arrivalSlowdownDistance > 0f ? arrivalSlowdownDistance : 0f;
            int safeStrafeMin = strafeSwitchMinFrames > 0 ? strafeSwitchMinFrames : 0;
            int safeStrafeMax = strafeSwitchMaxFrames > 0 ? strafeSwitchMaxFrames : 0;
            StrafeSwitchMinFrames = safeStrafeMin;
            StrafeSwitchMaxFrames = safeStrafeMax < safeStrafeMin ? safeStrafeMin : safeStrafeMax;
            DirectionHoldFrames = directionHoldFrames > 0 ? directionHoldFrames : 0;
            ReactionDelayFrames = reactionDelayFrames > 0 ? reactionDelayFrames : 0;
        }

        public float SpeedScale { get; }
        public float SteeringResponsiveness { get; }
        public float ApproachLateralBlend { get; }
        public float PreferredDistanceMin { get; }
        public float PreferredDistanceMax { get; }
        public float ArrivalSlowdownDistance { get; }
        public int StrafeSwitchMinFrames { get; }
        public int StrafeSwitchMaxFrames { get; }
        public int DirectionHoldFrames { get; }
        public int ReactionDelayFrames { get; }

        public static CharacterTestRuntimeAiPlannerCompiledMovementSettings FromSettings(
            CharacterTestRuntimeAiPlannerMovementSettings settings)
        {
            CharacterTestRuntimeAiPlannerMovementSettings source =
                CharacterTestRuntimeAiPlannerMovementSettings.Normalize(settings);
            return new CharacterTestRuntimeAiPlannerCompiledMovementSettings(
                source.SpeedScale,
                source.SteeringResponsiveness,
                source.ApproachLateralBlend,
                source.PreferredDistanceMin,
                source.PreferredDistanceMax,
                source.ArrivalSlowdownDistance,
                source.StrafeSwitchMinFrames,
                source.StrafeSwitchMaxFrames,
                source.DirectionHoldFrames,
                source.ReactionDelayFrames);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerCompiledAction
    {
        private readonly CharacterTestRuntimeAiPlannerConditionDefinition[] _conditions;

        public CharacterTestRuntimeAiPlannerCompiledAction(
            int actionId,
            int stateId,
            CharacterTestRuntimeAiPlannerActionKind kind,
            float score,
            float cost,
            string displayName,
            int abilitySlot,
            int abilityId,
            CharacterTestRuntimeAiPlannerConditionDefinition[] conditions,
            CharacterTestRuntimeAiPlannerCompiledMovementSettings movementSettings,
            int reactionCooldownFrames)
        {
            ActionId = actionId;
            StateId = stateId;
            Kind = kind;
            Score = score;
            Cost = cost;
            DisplayName = displayName ?? string.Empty;
            AbilitySlot = abilitySlot;
            AbilityId = abilityId;
            _conditions = conditions ?? Array.Empty<CharacterTestRuntimeAiPlannerConditionDefinition>();
            MovementSettings = movementSettings;
            ReactionCooldownFrames = reactionCooldownFrames > 0 ? reactionCooldownFrames : 0;
        }

        public int ActionId { get; }
        public int StateId { get; }
        public CharacterTestRuntimeAiPlannerActionKind Kind { get; }
        public float Score { get; }
        public float Cost { get; }
        public string DisplayName { get; }
        public int AbilitySlot { get; }
        public int AbilityId { get; }
        public CharacterTestRuntimeAiPlannerCompiledMovementSettings MovementSettings { get; }
        public int ReactionCooldownFrames { get; }
        public int ConditionCount => _conditions.Length;

        internal CharacterTestRuntimeAiPlannerConditionDefinition GetCondition(int index)
        {
            return index >= 0 && index < _conditions.Length ? _conditions[index] : default;
        }

        public bool CanExecute(CharacterTestRuntimeAiPlannerFactState facts)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                if (!CharacterTestRuntimeAiPlannerConditionEvaluator.IsSatisfied(facts, _conditions[i]))
                    return false;
            }

            return true;
        }
    }

    internal static class CharacterTestRuntimeAiPlannerConditionEvaluator
    {
        public static bool IsSatisfied(
            CharacterTestRuntimeAiPlannerFactState facts,
            CharacterTestRuntimeAiPlannerConditionDefinition condition)
        {
            if (facts == null)
                return false;

            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                    return CompareBool(facts.TargetExists, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                    return CompareBool(facts.TargetInAttackRange, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                    return CompareBool(facts.TargetVisible, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetDead:
                    return CompareBool(facts.TargetDead, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetLifeState:
                    return CompareFloat((int)facts.TargetLifeState, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                    return CompareBool(facts.TargetLineOfSightBlocked, condition);
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                    return CompareBool(facts.SelfActionRunning, condition);
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                    return CompareBool(facts.SelfOnHardSlick, condition);
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                    return CompareBool(facts.SelfNearMovableObject, condition);
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                    return CompareBool(facts.MovePathAvailable, condition);
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                    return CompareBool(facts.DirectPathClear, condition);
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                    return CompareBool(facts.EnvironmentNearObstacle, condition);
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                    return CompareBool(facts.EnvironmentNearHazard, condition);
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                    return CompareBool(facts.EnvironmentNearUsefulObject, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackWillHitSelf:
                    return CompareBool(facts.TargetAttackWillHitSelf, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackInstanceResponded:
                    return CompareBool(facts.TargetAttackInstanceResponded, condition);
                case CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady:
                    return CompareBool(facts.SelfDodgeCooldownReady, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistance:
                    return facts.TargetDistanceAvailable
                        && CompareFloat(facts.TargetDistance, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable:
                    return CompareBool(facts.TargetDistanceAvailable, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance:
                    return CompareFloat(facts.TargetRootDistance, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames:
                    return CompareFloat(facts.TargetLastSeenAgeFrames, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore:
                    return CompareFloat(facts.TargetThreatScore, condition);
                case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind:
                    return CompareFloat(facts.SelfSurfaceKind, condition);
                case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex:
                    return CompareFloat(facts.NavigationCandidateIndex, condition);
                case CharacterTestRuntimeAiPlannerFactKind.TargetAttackImpactFrames:
                    return CompareFloat(facts.TargetAttackImpactFrames, condition);
                default:
                    return false;
            }
        }

        private static bool CompareBool(
            bool currentValue,
            CharacterTestRuntimeAiPlannerConditionDefinition condition)
        {
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiPlannerConditionOperator.Equals:
                    return currentValue == condition.BoolValue;
                case CharacterTestRuntimeAiPlannerConditionOperator.NotEquals:
                    return currentValue != condition.BoolValue;
                default:
                    return false;
            }
        }

        private static bool CompareFloat(
            float currentValue,
            CharacterTestRuntimeAiPlannerConditionDefinition condition)
        {
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiPlannerConditionOperator.Equals:
                    return currentValue == condition.FloatValue;
                case CharacterTestRuntimeAiPlannerConditionOperator.NotEquals:
                    return currentValue != condition.FloatValue;
                case CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual:
                    return currentValue >= condition.FloatValue;
                case CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual:
                    return currentValue <= condition.FloatValue;
                default:
                    return false;
            }
        }
    }
}
