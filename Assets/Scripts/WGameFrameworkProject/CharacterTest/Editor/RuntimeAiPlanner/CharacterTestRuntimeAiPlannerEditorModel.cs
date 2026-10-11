using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestRuntimeAiPlannerEditorModel
    {
        private const int GeneratedActionIdBase = 910100;
        public const string PerceptionAwareMeleeProfileId =
            CharacterTestRuntimeAiPlannerTemplateLibrary.PerceptionAwareMeleeProfileId;
        public const string PerceptionAwareMeleeProjectAssetPath =
            CharacterTestRuntimeAiPlannerTemplateLibrary.PerceptionAwareMeleeProjectAssetPath;

        public string ProfileId { get; set; } =
            CharacterTestRuntimeAiPlannerProfileSource.DefaultProfileId;
        public string InitialStateId { get; set; } =
            CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString();
        public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; set; } =
            CharacterTestAiTargetSelectionMode.NearestOpposingTeam;
        public float AttackRange { get; set; } =
            CharacterTestAiBehaviorProfileSource.DefaultAttackRange;
        public float VisionRange { get; set; } =
            CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultVisionRange;
        public int TargetMemoryFrames { get; set; } =
            CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultTargetMemoryFrames;
        public int LineOfSightObstacleLayerMask { get; set; } =
            CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultLineOfSightObstacleLayerMask;
        public float EnvironmentSampleRadius { get; set; } =
            CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultEnvironmentSampleRadius;
        public float ThreatDistanceWeight { get; set; } =
            CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultThreatDistanceWeight;
        public float MovementSpeedScale { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultSpeedScale;
        public float MovementSteeringResponsiveness { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultSteeringResponsiveness;
        public float MovementApproachLateralBlend { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultApproachLateralBlend;
        public float MovementPreferredDistanceMin { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMin;
        public float MovementPreferredDistanceMax { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMax;
        public float MovementArrivalSlowdownDistance { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultArrivalSlowdownDistance;
        public int MovementStrafeSwitchMinFrames { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMinFrames;
        public int MovementStrafeSwitchMaxFrames { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMaxFrames;
        public int MovementDirectionHoldFrames { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultDirectionHoldFrames;
        public int MovementReactionDelayFrames { get; set; } =
            CharacterTestRuntimeAiPlannerMovementSettings.DefaultReactionDelayFrames;
        public List<StateDraft> States { get; } = new List<StateDraft>();
        public List<TransitionDraft> Transitions { get; } = new List<TransitionDraft>();
        public List<ActionDraft> Actions { get; } = new List<ActionDraft>();

        public void Load(CharacterTestRuntimeAiPlannerProfile profile)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                profile ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            ProfileId = source.ProfileId;
            InitialStateId = source.InitialStateId.ToString();
            TargetSelectionMode = source.TargetSelectionMode;
            AttackRange = source.AttackRange;
            CharacterTestRuntimeAiPlannerPerceptionSettings perception =
                source.PerceptionSettings;
            VisionRange = perception.VisionRange;
            TargetMemoryFrames = perception.TargetMemoryFrames;
            LineOfSightObstacleLayerMask = perception.LineOfSightObstacleLayerMask;
            EnvironmentSampleRadius = perception.EnvironmentSampleRadius;
            ThreatDistanceWeight = perception.ThreatDistanceWeight;
            CharacterTestRuntimeAiPlannerMovementSettings movement =
                source.MovementSettings;
            MovementSpeedScale = movement.SpeedScale;
            MovementSteeringResponsiveness = movement.SteeringResponsiveness;
            MovementApproachLateralBlend = movement.ApproachLateralBlend;
            MovementPreferredDistanceMin = movement.PreferredDistanceMin;
            MovementPreferredDistanceMax = movement.PreferredDistanceMax;
            MovementArrivalSlowdownDistance = movement.ArrivalSlowdownDistance;
            MovementStrafeSwitchMinFrames = movement.StrafeSwitchMinFrames;
            MovementStrafeSwitchMaxFrames = movement.StrafeSwitchMaxFrames;
            MovementDirectionHoldFrames = movement.DirectionHoldFrames;
            MovementReactionDelayFrames = movement.ReactionDelayFrames;

            States.Clear();
            Transitions.Clear();
            Actions.Clear();

            for (int i = 0; i < source.States.Count; i++)
                States.Add(new StateDraft(source.States[i]));
            for (int i = 0; i < source.Transitions.Count; i++)
                Transitions.Add(new TransitionDraft(source.Transitions[i]));
            for (int i = 0; i < source.Actions.Count; i++)
                Actions.Add(new ActionDraft(source.Actions[i]));
        }

        public CharacterTestRuntimeAiPlannerProfile ToRuntimeProfile()
        {
            return new CharacterTestRuntimeAiPlannerProfile(
                ProfileId,
                ResolveStateNumericId(InitialStateId),
                TargetSelectionMode,
                AttackRange,
                CopyStates(),
                CopyTransitions(),
                CopyActions(),
                new CharacterTestRuntimeAiPlannerPerceptionSettings(
                    VisionRange,
                    TargetMemoryFrames,
                    LineOfSightObstacleLayerMask,
                    EnvironmentSampleRadius,
                    ThreatDistanceWeight),
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    MovementSpeedScale,
                    MovementSteeringResponsiveness,
                    MovementApproachLateralBlend,
                    MovementPreferredDistanceMin,
                    MovementPreferredDistanceMax,
                    MovementArrivalSlowdownDistance,
                    MovementStrafeSwitchMinFrames,
                    MovementStrafeSwitchMaxFrames,
                    MovementDirectionHoldFrames,
                    MovementReactionDelayFrames));
        }

        public CharacterTestRuntimeAiPlannerValidationReport Validate()
        {
            return CharacterTestRuntimeAiPlannerProfileValidator.Validate(ToRuntimeProfile());
        }

        public static CharacterTestRuntimeAiPlannerProfile CreatePerceptionAwareMeleeTemplate()
        {
            const string observeStateId = "observe";
            const string chaseStateId = "chase";
            const string attackStateId = "attack";
            const string recoverStateId = "recover";

            CharacterTestRuntimeAiPlannerPerceptionSettings perception =
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault();
            var movement = new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale: 1f,
                steeringResponsiveness: 0.4f,
                approachLateralBlend: 0.12f,
                preferredDistanceMin: 0f,
                preferredDistanceMax: 0f,
                arrivalSlowdownDistance: 1.1f,
                strafeSwitchMinFrames: 20,
                strafeSwitchMaxFrames: 56,
                directionHoldFrames: 6,
                reactionDelayFrames: 2);

            return new CharacterTestRuntimeAiPlannerProfile(
                PerceptionAwareMeleeProfileId,
                ResolveStateNumericId(observeStateId),
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
                CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
                new[]
                {
                    StateDefinition(
                        "root",
                        displayName: "根"),
                    StateDefinition(observeStateId, "root", "观察"),
                    StateDefinition(chaseStateId, "root", "追击"),
                    StateDefinition(attackStateId, "root", "攻击"),
                    StateDefinition(recoverStateId, "root", "脱险")
                },
                new[]
                {
                    TransitionDefinition(
                        string.Empty,
                        observeStateId,
                        10,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            false),
                        "无目标时观察"),
                    TransitionDefinition(
                        string.Empty,
                        recoverStateId,
                        20,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                            true),
                        "靠近危险时脱险"),
                    TransitionDefinition(
                        string.Empty,
                        attackStateId,
                        30,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                            true),
                        "进入攻击范围"),
                    TransitionDefinition(
                        string.Empty,
                        chaseStateId,
                        40,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            true),
                        "发现目标后追击")
                },
                new[]
                {
                    ActionDefinition(
                        920001,
                        observeStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 5f,
                        cost: 0f,
                        displayName: "观察等待",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                false)
                        }),
                    ActionDefinition(
                        920010,
                        chaseStateId,
                        CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        score: 60f,
                        cost: 1f,
                        displayName: "可见目标追击",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetVisible,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                                false),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable,
                                true)
                        }),
                    ActionDefinition(
                        920011,
                        chaseStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Strafe,
                        score: 45f,
                        cost: 1.2f,
                        displayName: "遮挡时绕行",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked,
                                true),
                            new CharacterTestRuntimeAiPlannerConditionDefinition(
                                CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames,
                                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual,
                                true,
                                perception.TargetMemoryFrames)
                        }),
                    ActionDefinition(
                        920012,
                        chaseStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 15f,
                        cost: 0.2f,
                        displayName: "记忆中等待",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetVisible,
                                false),
                            new CharacterTestRuntimeAiPlannerConditionDefinition(
                                CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames,
                                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual,
                                true,
                                perception.TargetMemoryFrames)
                        }),
                    ActionDefinition(
                        920020,
                        attackStateId,
                        CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                        score: 100f,
                        cost: 1f,
                        displayName: "可见目标轻攻击",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetVisible,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                                true),
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning,
                                false)
                        }),
                    ActionDefinition(
                        920021,
                        attackStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 10f,
                        cost: 0f,
                        displayName: "动作中等待",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning,
                                true)
                        }),
                    ActionDefinition(
                        920030,
                        recoverStateId,
                        CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        score: 90f,
                        cost: 1f,
                        displayName: "远离危险",
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                                true)
                        })
                },
                perception,
                movement);
        }

        public StateDraft AddRootState()
        {
            return AddState(string.Empty);
        }

        public StateDraft AddChildState(string parentStateId)
        {
            return AddState(parentStateId);
        }

        public StateDraft DuplicateState(string stateId)
        {
            StateDraft source = FindState(stateId);
            if (source == null)
                return null;

            var copy = new StateDraft(
                MakeUniqueStateId(source.StateId + "_copy"),
                source.ParentStateId,
                AppendCopySuffix(source.DisplayName));
            int index = States.IndexOf(source);
            States.Insert(index + 1, copy);
            return copy;
        }

        public bool DeleteState(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId))
                return false;

            StateDraft state = FindState(stateId);
            if (state == null)
                return false;

            if (!CharacterTestRuntimeAiPlannerEditorStateIdentity.IsBuiltin(state.NumericStateId))
                CharacterTestRuntimeAiPlannerEditorStateIdentity.Retire(state.NumericStateId);

            States.Remove(state);
            for (int i = Transitions.Count - 1; i >= 0; i--)
            {
                TransitionDraft transition = Transitions[i];
                if (string.Equals(transition.SourceStateId, stateId, StringComparison.Ordinal)
                    || string.Equals(transition.TargetStateId, stateId, StringComparison.Ordinal))
                {
                    Transitions.RemoveAt(i);
                }
            }

            return true;
        }

        public bool RenameState(string oldStateId, string newStateId)
        {
            string cleanNewId = CleanId(newStateId);
            if (string.IsNullOrWhiteSpace(oldStateId) || string.IsNullOrWhiteSpace(cleanNewId))
                return false;

            if (string.Equals(oldStateId, cleanNewId, StringComparison.Ordinal))
                return true;
            if (HasState(cleanNewId))
                return false;

            StateDraft state = FindState(oldStateId);
            if (state == null)
                return false;

            state.StateId = cleanNewId;
            if (string.Equals(InitialStateId, oldStateId, StringComparison.Ordinal))
                InitialStateId = cleanNewId;

            for (int i = 0; i < States.Count; i++)
            {
                if (string.Equals(States[i].ParentStateId, oldStateId, StringComparison.Ordinal))
                    States[i].ParentStateId = cleanNewId;
            }

            for (int i = 0; i < Transitions.Count; i++)
            {
                if (string.Equals(Transitions[i].SourceStateId, oldStateId, StringComparison.Ordinal))
                    Transitions[i].SourceStateId = cleanNewId;
                if (string.Equals(Transitions[i].TargetStateId, oldStateId, StringComparison.Ordinal))
                    Transitions[i].TargetStateId = cleanNewId;
            }

            for (int i = 0; i < Actions.Count; i++)
            {
                if (string.Equals(Actions[i].StateId, oldStateId, StringComparison.Ordinal))
                    Actions[i].StateId = cleanNewId;
            }

            return true;
        }

        public TransitionDraft AddTransition(string sourceStateId)
        {
            var transition = new TransitionDraft
            {
                SourceStateId = sourceStateId ?? string.Empty,
                TargetStateId = GetFirstStateId(),
                Priority = NextTransitionPriority(),
                DisplayName = "新切换",
                Condition = new ConditionDraft(
                    CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                    CharacterTestRuntimeAiPlannerConditionOperator.Equals,
                    true,
                    0f)
            };
            Transitions.Add(transition);
            return transition;
        }

        public TransitionDraft DuplicateTransition(int index)
        {
            if (index < 0 || index >= Transitions.Count)
                return null;

            TransitionDraft copy = Transitions[index].Clone();
            copy.DisplayName = AppendCopySuffix(copy.DisplayName);
            Transitions.Insert(index + 1, copy);
            return copy;
        }

        public bool DeleteTransition(int index)
        {
            if (index < 0 || index >= Transitions.Count)
                return false;

            Transitions.RemoveAt(index);
            return true;
        }

        public bool MoveTransition(int index, int delta)
        {
            return MoveItem(Transitions, index, delta);
        }

        public ActionDraft AddAction(string stateId)
        {
            var action = new ActionDraft
            {
                ActionId = NextActionId(),
                StateId = string.IsNullOrWhiteSpace(stateId) ? GetFirstStateId() : stateId,
                Kind = CharacterTestRuntimeAiPlannerActionKind.Wait,
                Score = 0f,
                Cost = 0f,
                DisplayName = "新动作",
                AbilitySlot = 0,
                AbilityId = 0
            };
            Actions.Add(action);
            return action;
        }

        public ActionDraft DuplicateAction(int index)
        {
            if (index < 0 || index >= Actions.Count)
                return null;

            ActionDraft copy = Actions[index].Clone();
            copy.ActionId = NextActionId();
            copy.DisplayName = AppendCopySuffix(copy.DisplayName);
            Actions.Insert(index + 1, copy);
            return copy;
        }

        public bool DeleteAction(int index)
        {
            if (index < 0 || index >= Actions.Count)
                return false;

            Actions.RemoveAt(index);
            return true;
        }

        public bool MoveAction(int index, int delta)
        {
            return MoveItem(Actions, index, delta);
        }

        public StateDraft FindState(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId))
                return null;

            for (int i = 0; i < States.Count; i++)
            {
                if (string.Equals(States[i].StateId, stateId, StringComparison.Ordinal))
                    return States[i];
            }

            return null;
        }

        public bool HasState(string stateId)
        {
            return FindState(stateId) != null;
        }

        public string GetFirstStateId()
        {
            return States.Count > 0 ? States[0].StateId : string.Empty;
        }

        public int GetStateDepth(string stateId)
        {
            int depth = 0;
            string current = stateId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                StateDraft state = FindState(current);
                if (state == null || string.IsNullOrWhiteSpace(state.ParentStateId))
                    break;

                depth++;
                current = state.ParentStateId;
                if (depth > 32)
                    break;
            }

            return depth;
        }

        public string MakeUniqueStateId(string baseId)
        {
            string cleanBaseId = CleanId(baseId);
            if (string.IsNullOrWhiteSpace(cleanBaseId))
                cleanBaseId = "state";
            if (!HasState(cleanBaseId))
                return cleanBaseId;

            int suffix = 1;
            while (HasState(cleanBaseId + "_" + suffix))
                suffix++;
            return cleanBaseId + "_" + suffix;
        }

        private StateDraft AddState(string parentStateId)
        {
            var state = new StateDraft(
                MakeUniqueStateId("state"),
                parentStateId ?? string.Empty,
                "新状态");
            States.Add(state);
            if (string.IsNullOrWhiteSpace(InitialStateId))
                InitialStateId = state.StateId;
            return state;
        }

        private CharacterTestRuntimeAiPlannerStateDefinition[] CopyStates()
        {
            var copy = new CharacterTestRuntimeAiPlannerStateDefinition[States.Count];
            for (int i = 0; i < States.Count; i++)
                copy[i] = States[i].ToDefinition();
            return copy;
        }

        private CharacterTestRuntimeAiPlannerTransitionDefinition[] CopyTransitions()
        {
            var copy = new CharacterTestRuntimeAiPlannerTransitionDefinition[Transitions.Count];
            for (int i = 0; i < Transitions.Count; i++)
                copy[i] = Transitions[i].ToDefinition();
            return copy;
        }

        private CharacterTestRuntimeAiPlannerActionDefinition[] CopyActions()
        {
            var copy = new CharacterTestRuntimeAiPlannerActionDefinition[Actions.Count];
            for (int i = 0; i < Actions.Count; i++)
                copy[i] = Actions[i].ToDefinition();
            return copy;
        }

        private int NextTransitionPriority()
        {
            int priority = 0;
            for (int i = 0; i < Transitions.Count; i++)
            {
                if (Transitions[i].Priority > priority)
                    priority = Transitions[i].Priority;
            }

            return priority + 10;
        }

        private int NextActionId()
        {
            int id = GeneratedActionIdBase;
            for (int i = 0; i < Actions.Count; i++)
            {
                if (Actions[i].ActionId >= id)
                    id = Actions[i].ActionId + 1;
            }

            return id;
        }

        private static bool MoveItem<T>(List<T> list, int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= list.Count || target < 0 || target >= list.Count)
                return false;

            T item = list[index];
            list.RemoveAt(index);
            list.Insert(target, item);
            return true;
        }

        private static string AppendCopySuffix(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Copy" : value + " Copy";
        }

        private static string CleanId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            var chars = new char[trimmed.Length];
            int count = 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if ((c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-'
                    || c == '.')
                {
                    chars[count++] = char.ToLowerInvariant(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    chars[count++] = '_';
                }
            }

            return count > 0 ? new string(chars, 0, count) : string.Empty;
        }

        private static CharacterTestRuntimeAiPlannerStateDefinition StateDefinition(
            string stateId,
            string parentStateId = "",
            string displayName = "")
        {
            return new CharacterTestRuntimeAiPlannerStateDefinition(
                ResolveStateNumericId(stateId),
                ResolveStateNumericId(parentStateId),
                displayName);
        }

        private static CharacterTestRuntimeAiPlannerTransitionDefinition TransitionDefinition(
            string sourceStateId,
            string targetStateId,
            int priority,
            CharacterTestRuntimeAiPlannerConditionDefinition condition,
            string displayName = "")
        {
            return new CharacterTestRuntimeAiPlannerTransitionDefinition(
                ResolveStateNumericId(sourceStateId),
                ResolveStateNumericId(targetStateId),
                priority,
                condition,
                displayName);
        }

        private static CharacterTestRuntimeAiPlannerActionDefinition ActionDefinition(
            int actionId,
            string stateId,
            CharacterTestRuntimeAiPlannerActionKind kind,
            float score,
            float cost,
            string displayName = "",
            int abilitySlot = 0,
            int abilityId = 0,
            IEnumerable<CharacterTestRuntimeAiPlannerConditionDefinition> conditions = null,
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings = null)
        {
            return new CharacterTestRuntimeAiPlannerActionDefinition(
                actionId,
                ResolveStateNumericId(stateId),
                kind,
                score,
                cost,
                displayName,
                abilitySlot,
                abilityId,
                conditions,
                movementSettings);
        }

        private static int ResolveStateNumericId(string value)
        {
            return CharacterTestRuntimeAiPlannerEditorStateIdentity.ResolveOrAllocate(value);
        }

        internal sealed class StateDraft
        {
            private string _stateId;
            private string _parentStateId;

            public StateDraft(CharacterTestRuntimeAiPlannerStateDefinition definition)
                : this(
                    definition != null
                        ? CharacterTestRuntimeAiPlannerEditorStateIdentity.LabelForNumeric(definition.StateId)
                        : string.Empty,
                    definition != null && definition.ParentStateId > 0
                        ? CharacterTestRuntimeAiPlannerEditorStateIdentity.LabelForNumeric(definition.ParentStateId)
                        : string.Empty,
                    definition != null ? definition.DisplayName : string.Empty)
            {
                NumericStateId = definition != null
                    ? CharacterTestRuntimeAiPlannerEditorStateIdentity.ValidateNumeric(definition.StateId)
                    : 0;
                NumericParentStateId = definition != null && definition.ParentStateId > 0
                    ? CharacterTestRuntimeAiPlannerEditorStateIdentity.ValidateNumeric(definition.ParentStateId)
                    : 0;
            }

            public StateDraft(string stateId, string parentStateId, string displayName)
            {
                StateId = stateId ?? string.Empty;
                ParentStateId = parentStateId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                NumericStateId = CharacterTestRuntimeAiPlannerEditorStateIdentity.ResolveOrAllocate(StateId);
                NumericParentStateId = CharacterTestRuntimeAiPlannerEditorStateIdentity.ResolveOrAllocate(ParentStateId);
            }

            public string StateId
            {
                get => _stateId;
                set => _stateId = value ?? string.Empty;
            }

            public string ParentStateId
            {
                get => _parentStateId;
                set
                {
                    _parentStateId = value ?? string.Empty;
                    NumericParentStateId = CharacterTestRuntimeAiPlannerEditorStateIdentity.ResolveOrAllocate(_parentStateId);
                }
            }
            public string DisplayName { get; set; }
            public int NumericStateId { get; private set; }
            public int NumericParentStateId { get; private set; }

            public CharacterTestRuntimeAiPlannerStateDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerStateDefinition(
                    NumericStateId,
                    NumericParentStateId,
                    DisplayName);
            }
        }

        internal sealed class TransitionDraft
        {
            public TransitionDraft()
            {
                Condition = new ConditionDraft();
            }

            public TransitionDraft(CharacterTestRuntimeAiPlannerTransitionDefinition definition)
            {
                SourceStateId = definition != null && definition.SourceStateId > 0
                    ? CharacterTestRuntimeAiPlannerEditorStateIdentity.LabelForNumeric(definition.SourceStateId)
                    : string.Empty;
                TargetStateId = definition != null && definition.TargetStateId > 0
                    ? CharacterTestRuntimeAiPlannerEditorStateIdentity.LabelForNumeric(definition.TargetStateId)
                    : string.Empty;
                Priority = definition != null ? definition.Priority : 0;
                DisplayName = definition != null ? definition.DisplayName : string.Empty;
                Condition = new ConditionDraft(definition != null ? definition.Condition : default);
            }

            public string SourceStateId { get; set; }
            public string TargetStateId { get; set; }
            public int Priority { get; set; }
            public string DisplayName { get; set; }
            public ConditionDraft Condition { get; set; }

            public TransitionDraft Clone()
            {
                return new TransitionDraft
                {
                    SourceStateId = SourceStateId,
                    TargetStateId = TargetStateId,
                    Priority = Priority,
                    DisplayName = DisplayName,
                    Condition = Condition != null ? Condition.Clone() : new ConditionDraft()
                };
            }

            public CharacterTestRuntimeAiPlannerTransitionDefinition ToDefinition()
            {
                return TransitionDefinition(
                    SourceStateId,
                    TargetStateId,
                    Priority,
                    Condition != null ? Condition.ToDefinition() : default,
                    DisplayName);
            }
        }

        internal sealed class ActionDraft
        {
            public ActionDraft()
            {
            }

            public ActionDraft(CharacterTestRuntimeAiPlannerActionDefinition definition)
            {
                ActionId = definition != null ? definition.ActionId : 0;
                StateId = definition != null && definition.StateId > 0
                    ? CharacterTestRuntimeAiPlannerEditorStateIdentity.LabelForNumeric(definition.StateId)
                    : string.Empty;
                Kind = definition != null ? definition.Kind : CharacterTestRuntimeAiPlannerActionKind.Wait;
                Score = definition != null ? definition.Score : 0f;
                Cost = definition != null ? definition.Cost : 0f;
                DisplayName = definition != null ? definition.DisplayName : string.Empty;
                AbilitySlot = definition != null ? definition.AbilitySlot : 0;
                AbilityId = definition != null ? definition.AbilityId : 0;
                if (definition == null)
                    return;

                CharacterTestRuntimeAiPlannerMovementSettings movement =
                    definition.MovementSettings;
                if (movement != null)
                {
                    HasMovementOverride = true;
                    MovementSpeedScale = movement.SpeedScale;
                    MovementSteeringResponsiveness = movement.SteeringResponsiveness;
                    MovementApproachLateralBlend = movement.ApproachLateralBlend;
                    MovementPreferredDistanceMin = movement.PreferredDistanceMin;
                    MovementPreferredDistanceMax = movement.PreferredDistanceMax;
                    MovementArrivalSlowdownDistance = movement.ArrivalSlowdownDistance;
                    MovementStrafeSwitchMinFrames = movement.StrafeSwitchMinFrames;
                    MovementStrafeSwitchMaxFrames = movement.StrafeSwitchMaxFrames;
                    MovementDirectionHoldFrames = movement.DirectionHoldFrames;
                    MovementReactionDelayFrames = movement.ReactionDelayFrames;
                }

                for (int i = 0; i < definition.Conditions.Count; i++)
                    Conditions.Add(new ConditionDraft(definition.Conditions[i]));
            }

            public int ActionId { get; set; }
            public string StateId { get; set; }
            public CharacterTestRuntimeAiPlannerActionKind Kind { get; set; }
            public float Score { get; set; }
            public float Cost { get; set; }
            public string DisplayName { get; set; }
            public int AbilitySlot { get; set; }
            public int AbilityId { get; set; }
            public bool HasMovementOverride { get; set; }
            public float MovementSpeedScale { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultSpeedScale;
            public float MovementSteeringResponsiveness { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultSteeringResponsiveness;
            public float MovementApproachLateralBlend { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultApproachLateralBlend;
            public float MovementPreferredDistanceMin { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMin;
            public float MovementPreferredDistanceMax { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMax;
            public float MovementArrivalSlowdownDistance { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultArrivalSlowdownDistance;
            public int MovementStrafeSwitchMinFrames { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMinFrames;
            public int MovementStrafeSwitchMaxFrames { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMaxFrames;
            public int MovementDirectionHoldFrames { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultDirectionHoldFrames;
            public int MovementReactionDelayFrames { get; set; } =
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultReactionDelayFrames;
            public List<ConditionDraft> Conditions { get; } = new List<ConditionDraft>();

            public ActionDraft Clone()
            {
                var copy = new ActionDraft
                {
                    ActionId = ActionId,
                    StateId = StateId,
                    Kind = Kind,
                    Score = Score,
                    Cost = Cost,
                    DisplayName = DisplayName,
                    AbilitySlot = AbilitySlot,
                    AbilityId = AbilityId,
                    HasMovementOverride = HasMovementOverride,
                    MovementSpeedScale = MovementSpeedScale,
                    MovementSteeringResponsiveness = MovementSteeringResponsiveness,
                    MovementApproachLateralBlend = MovementApproachLateralBlend,
                    MovementPreferredDistanceMin = MovementPreferredDistanceMin,
                    MovementPreferredDistanceMax = MovementPreferredDistanceMax,
                    MovementArrivalSlowdownDistance = MovementArrivalSlowdownDistance,
                    MovementStrafeSwitchMinFrames = MovementStrafeSwitchMinFrames,
                    MovementStrafeSwitchMaxFrames = MovementStrafeSwitchMaxFrames,
                    MovementDirectionHoldFrames = MovementDirectionHoldFrames,
                    MovementReactionDelayFrames = MovementReactionDelayFrames
                };
                for (int i = 0; i < Conditions.Count; i++)
                    copy.Conditions.Add(Conditions[i].Clone());
                return copy;
            }

            public CharacterTestRuntimeAiPlannerActionDefinition ToDefinition()
            {
                var conditions = new CharacterTestRuntimeAiPlannerConditionDefinition[Conditions.Count];
                for (int i = 0; i < Conditions.Count; i++)
                    conditions[i] = Conditions[i].ToDefinition();

                return ActionDefinition(
                    ActionId,
                    StateId,
                    Kind,
                    Score,
                    Cost,
                    DisplayName,
                    AbilitySlot,
                    AbilityId,
                    conditions,
                    HasMovementOverride
                        ? new CharacterTestRuntimeAiPlannerMovementSettings(
                            MovementSpeedScale,
                            MovementSteeringResponsiveness,
                            MovementApproachLateralBlend,
                            MovementPreferredDistanceMin,
                            MovementPreferredDistanceMax,
                            MovementArrivalSlowdownDistance,
                            MovementStrafeSwitchMinFrames,
                            MovementStrafeSwitchMaxFrames,
                            MovementDirectionHoldFrames,
                            MovementReactionDelayFrames)
                        : null);
            }
        }

        internal sealed class ConditionDraft
        {
            public ConditionDraft()
                : this(
                    CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                    CharacterTestRuntimeAiPlannerConditionOperator.Equals,
                    true,
                    0f)
            {
            }

            public ConditionDraft(CharacterTestRuntimeAiPlannerConditionDefinition definition)
                : this(
                    definition.Fact,
                    definition.Operator,
                    definition.BoolValue,
                    definition.FloatValue)
            {
            }

            public ConditionDraft(
                CharacterTestRuntimeAiPlannerFactKind fact,
                CharacterTestRuntimeAiPlannerConditionOperator op,
                bool boolValue,
                float floatValue)
            {
                Fact = fact;
                Operator = op;
                BoolValue = boolValue;
                FloatValue = floatValue;
            }

            public CharacterTestRuntimeAiPlannerFactKind Fact { get; set; }
            public CharacterTestRuntimeAiPlannerConditionOperator Operator { get; set; }
            public bool BoolValue { get; set; }
            public float FloatValue { get; set; }

            public ConditionDraft Clone()
            {
                return new ConditionDraft(Fact, Operator, BoolValue, FloatValue);
            }

            public CharacterTestRuntimeAiPlannerConditionDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerConditionDefinition(
                    Fact,
                    Operator,
                    BoolValue,
                    FloatValue);
            }
        }
    }
}
