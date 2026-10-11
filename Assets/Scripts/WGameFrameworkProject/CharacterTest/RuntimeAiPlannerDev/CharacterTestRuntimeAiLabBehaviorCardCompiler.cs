using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabCompiledCondition
    {
        public CharacterTestRuntimeAiLabCompiledCondition(
            string conditionId,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiLabCardConditionOperator op,
            bool boolValue,
            float value,
            float maxValue,
            int abilityId,
            CharacterActionId characterActionId,
            uint allowedTargetPhases = 0u,
            uint requiredTargetFlags = 0u,
            uint forbiddenTargetFlags = 0u)
        {
            ConditionId = conditionId ?? string.Empty;
            Fact = fact;
            Operator = op;
            BoolValue = boolValue;
            Value = value;
            MaxValue = maxValue;
            AbilityId = abilityId;
            CharacterActionId = characterActionId;
            AllowedTargetPhases = allowedTargetPhases;
            RequiredTargetFlags = requiredTargetFlags;
            ForbiddenTargetFlags = forbiddenTargetFlags;
        }

        public string ConditionId { get; }
        public CharacterTestRuntimeAiLabCardConditionFact Fact { get; }
        public CharacterTestRuntimeAiLabCardConditionOperator Operator { get; }
        public bool BoolValue { get; }
        public float Value { get; }
        public float MaxValue { get; }
        public int AbilityId { get; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; }
        public uint AllowedTargetPhases { get; }
        public uint RequiredTargetFlags { get; }
        public uint ForbiddenTargetFlags { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledBranch
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _conditions;

        public CharacterTestRuntimeAiLabCompiledBranch(
            string branchId,
            CharacterTestRuntimeAiLabCardBranchTrigger trigger,
            int priority,
            int targetStepIndex,
            string targetStepId,
            string label,
            int windowStartFrame,
            int windowEndFrame,
            string endReason,
            CharacterTestRuntimeAiLabCompiledCondition[] conditions)
        {
            BranchId = branchId ?? string.Empty;
            Trigger = trigger;
            Priority = priority;
            TargetStepIndex = targetStepIndex;
            TargetStepId = targetStepId ?? string.Empty;
            Label = label ?? string.Empty;
            WindowStartFrame = windowStartFrame;
            WindowEndFrame = windowEndFrame;
            EndReason = endReason ?? string.Empty;
            _conditions = Array.AsReadOnly(
                conditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
        }

        public string BranchId { get; }
        public CharacterTestRuntimeAiLabCardBranchTrigger Trigger { get; }
        public int Priority { get; }
        public int TargetStepIndex { get; }
        public string TargetStepId { get; }
        public bool EndsCard => TargetStepIndex < 0;
        public string Label { get; }
        public int WindowStartFrame { get; }
        public int WindowEndFrame { get; }
        public string EndReason { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> Conditions => _conditions;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledStep
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _entryConditions;
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _monitorConditions;
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledBranch> _branches;

        public CharacterTestRuntimeAiLabCompiledStep(
            string stepId,
            int order,
            CharacterTestRuntimeAiLabCardStepKind kind,
            string title,
            int abilityId,
            CharacterActionId characterActionId,
            CharacterTestRuntimeAiPlannerActionKind legacyActionKind,
            float moveMinDistance,
            float moveMaxDistance,
            int waitFrames,
            int estimatedDurationFrames,
            string note,
            CharacterTestRuntimeAiLabCompiledCondition[] entryConditions,
            CharacterTestRuntimeAiLabCompiledCondition[] monitorConditions,
            CharacterTestRuntimeAiLabCompiledBranch[] branches)
        {
            StepId = stepId ?? string.Empty;
            Order = order;
            Kind = kind;
            Title = title ?? string.Empty;
            AbilityId = abilityId;
            CharacterActionId = characterActionId;
            LegacyActionKind = legacyActionKind;
            MoveMinDistance = moveMinDistance;
            MoveMaxDistance = moveMaxDistance;
            WaitFrames = waitFrames;
            EstimatedDurationFrames = estimatedDurationFrames;
            Note = note ?? string.Empty;
            _entryConditions = Array.AsReadOnly(
                entryConditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
            _monitorConditions = Array.AsReadOnly(
                monitorConditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
            _branches = Array.AsReadOnly(branches ?? Array.Empty<CharacterTestRuntimeAiLabCompiledBranch>());
        }

        public string StepId { get; }
        public int Order { get; }
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; }
        public string Title { get; }
        public int AbilityId { get; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; }
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; }
        public float MoveMinDistance { get; }
        public float MoveMaxDistance { get; }
        public int WaitFrames { get; }
        public int EstimatedDurationFrames { get; }
        public string Note { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> EntryConditions => _entryConditions;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> MonitorConditions => _monitorConditions;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledBranch> Branches => _branches;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledProgram
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _triggerConditions;
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledStep> _steps;

        public CharacterTestRuntimeAiLabCompiledProgram(
            string entryStepId,
            int entryStepIndex,
            string intent,
            string riskSummary,
            long contentHash,
            CharacterTestRuntimeAiLabCompiledCondition[] triggerConditions,
            CharacterTestRuntimeAiLabCompiledStep[] steps)
        {
            EntryStepId = entryStepId ?? string.Empty;
            EntryStepIndex = entryStepIndex;
            Intent = intent ?? string.Empty;
            RiskSummary = riskSummary ?? string.Empty;
            ContentHash = contentHash;
            _triggerConditions = Array.AsReadOnly(
                triggerConditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
            _steps = Array.AsReadOnly(steps ?? Array.Empty<CharacterTestRuntimeAiLabCompiledStep>());
        }

        public string EntryStepId { get; }
        public int EntryStepIndex { get; }
        public string Intent { get; }
        public string RiskSummary { get; }
        public long ContentHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> TriggerConditions => _triggerConditions;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledStep> Steps => _steps;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardCompileResult
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCardDiagnostic> _diagnostics;

        public CharacterTestRuntimeAiLabBehaviorCardCompileResult(
            CharacterTestRuntimeAiLabCompiledProgram program,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            Program = program;
            _diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiLabCompiledProgram Program { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics => _diagnostics;
        public bool Success => Program != null
            && !CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(_diagnostics);
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog
    {
        private readonly AbilityDefinitionRegistry _abilities;
        private readonly CharacterActionConfigRegistry _actions;

        public CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
            AbilityDefinitionRegistry abilities,
            CharacterActionConfigRegistry actions)
        {
            _abilities = abilities;
            _actions = actions;
            StableHash = ComputeStableHash(abilities, actions);
        }

        public long StableHash { get; }

        public IReadOnlyList<CharacterActionConfig> CharacterActions =>
            _actions?.Actions ?? Array.Empty<CharacterActionConfig>();

        public bool Matches(
            AbilityDefinitionRegistry abilities,
            CharacterActionConfigRegistry actions)
        {
            return ReferenceEquals(_abilities, abilities) && ReferenceEquals(_actions, actions);
        }

        public bool HasAbility(int abilityId)
        {
            return _abilities != null && _abilities.TryGet(abilityId, out _);
        }
        internal bool TryGetExecutionAbilityDependency(int abilityId, out AbilityDefinitionConfig definition)
        { definition = null; return _abilities != null && _abilities.TryGet(abilityId, out definition); }

        public bool HasCharacterAction(CharacterActionId characterActionId)
        {
            return _actions != null && _actions.TryGetAction(characterActionId, out _);
        }

        public bool TryResolveCharacterActionDurationFrames(
            CharacterActionId characterActionId,
            out int durationFrames)
        {
            durationFrames = 0;
            if (_actions == null
                || !_actions.TryGetAction(characterActionId, out CharacterActionConfig action)
                || action == null
                || !action.DurationFrames.HasValue
                || action.DurationFrames.Value <= 0)
            {
                return false;
            }
            durationFrames = action.DurationFrames.Value;
            return true;
        }

        public bool TryResolveAbilityAction(
            int abilityId,
            out CharacterActionId characterActionId,
            out int durationFrames)
        {
            characterActionId = CharacterActionId.None;
            durationFrames = 0;
            if (_abilities == null
                || !_abilities.TryGet(abilityId, out AbilityDefinitionConfig ability)
                || ability?.ActionBinding == null
                || !ability.ActionBinding.ActionId.IsValid)
            {
                return false;
            }
            characterActionId = ability.ActionBinding.ActionId;
            return TryResolveCharacterActionDurationFrames(characterActionId, out durationFrames);
        }

        private static long ComputeStableHash(
            AbilityDefinitionRegistry abilities,
            CharacterActionConfigRegistry actions)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddLong(
                "runtimeAiLab.references.abilities",
                abilities != null ? abilities.ComputeStableHash() : 0L);
            CharacterActionConfig[] actionDefinitions = actions != null
                ? actions.Actions
                : Array.Empty<CharacterActionConfig>();
            accumulator.AddInt("runtimeAiLab.references.actionCount", actionDefinitions.Length);
            for (int i = 0; i < actionDefinitions.Length; i++)
            {
                CharacterActionConfig action = actionDefinitions[i];
                accumulator.AddInt(
                    "runtimeAiLab.references.action.id",
                    action != null ? action.ActionId.Value : 0);
                accumulator.AddInt(
                    "runtimeAiLab.references.action.durationFrames",
                    action?.DurationFrames ?? 0);
            }
            return accumulator.ToHash();
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardCompiler
    {
        private const int EndStepIndex = -1;
        private const double FloatHashScale = 1000d;

        public static CharacterTestRuntimeAiLabBehaviorCardCompileResult Compile(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload snapshot = payload?.Clone();
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> validation =
                CharacterTestRuntimeAiLabBehaviorCardValidator.Validate(snapshot);
            for (int i = 0; i < validation.Count; i++)
                diagnostics.Add(validation[i]);
            if (CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            ValidateFiniteNumbers(snapshot, diagnostics);
            SourceStep[] sources = CreateSortedSteps(snapshot, diagnostics);
            ValidateReferences(snapshot, references, diagnostics);
            if (CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            var stepIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Length; i++)
                stepIndices.Add(sources[i].Step.StepId, i);

            CharacterTestRuntimeAiLabCompiledCondition[] triggerConditions =
                CompileConditions(snapshot.TriggerConditions);
            var steps = new CharacterTestRuntimeAiLabCompiledStep[sources.Length];
            for (int i = 0; i < sources.Length; i++)
                steps[i] = CompileStep(sources[i].Step, stepIndices);

            int entryStepIndex = stepIndices[snapshot.EntryStepId];
            long contentHash = ComputeContentHash(
                snapshot.EntryStepId,
                entryStepIndex,
                snapshot.Intent,
                snapshot.RiskSummary,
                triggerConditions,
                steps);
            SortDiagnostics(diagnostics);
            return new CharacterTestRuntimeAiLabBehaviorCardCompileResult(
                new CharacterTestRuntimeAiLabCompiledProgram(
                    snapshot.EntryStepId,
                    entryStepIndex,
                    snapshot.Intent,
                    snapshot.RiskSummary,
                    contentHash,
                    triggerConditions,
                    steps),
                diagnostics.ToArray());
        }

        private static SourceStep[] CreateSortedSteps(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var sources = new SourceStep[payload.Steps.Count];
            for (int i = 0; i < payload.Steps.Count; i++)
                sources[i] = new SourceStep(payload.Steps[i], i);
            Array.Sort(sources, CompareSourceSteps);
            for (int i = 1; i < sources.Length; i++)
            {
                if (sources[i - 1].Step.Order != sources[i].Step.Order)
                    continue;
                AddError(
                    diagnostics,
                    "CARD_COMPILE_STEP_ORDER_DUPLICATE",
                    "$.steps[" + sources[i].SourceIndex + "].order",
                    "Behavior card step order must be unique: " + sources[i].Step.Order + ".");
            }
            return sources;
        }

        private static void ValidateFiniteNumbers(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            ValidateConditionNumbers(payload.TriggerConditions, "$.triggerConditions", diagnostics);
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                string path = "$.steps[" + i + "]";
                if (!IsFinite(step.MoveMinDistance) || !IsFinite(step.MoveMaxDistance))
                {
                    AddError(
                        diagnostics,
                        "CARD_COMPILE_MOVE_DISTANCE_NON_FINITE",
                        path,
                        "Move distance values must be finite.");
                }
                ValidateConditionNumbers(step.EntryConditions, path + ".entryConditions", diagnostics);
                ValidateConditionNumbers(step.MonitorConditions, path + ".monitorConditions", diagnostics);
                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    ValidateConditionNumbers(
                        step.Branches[branchIndex].Conditions,
                        path + ".branches[" + branchIndex + "].conditions",
                        diagnostics);
                }
            }
        }

        private static void ValidateConditionNumbers(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (IsFinite(condition.Value) && IsFinite(condition.MaxValue))
                    continue;
                AddError(
                    diagnostics,
                    "CARD_COMPILE_CONDITION_VALUE_NON_FINITE",
                    path + "[" + i + "]",
                    "Condition numeric values must be finite.");
            }
        }

        private static void ValidateReferences(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            ValidateConditionReferences(payload.TriggerConditions, "$.triggerConditions", references, diagnostics);
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                string path = "$.steps[" + i + "]";
                if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                    && (references == null || !references.HasAbility(step.AbilityId)))
                {
                    AddError(
                        diagnostics,
                        "CARD_COMPILE_ABILITY_UNKNOWN",
                        path + ".abilityId",
                        "Ability reference is not available in the current GameSlice: " + step.AbilityId + ".");
                }
                if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    && (references == null || !references.HasCharacterAction(step.CharacterActionId)))
                {
                    AddError(
                        diagnostics,
                        "CARD_COMPILE_ACTION_UNKNOWN",
                        path + ".characterActionId",
                        "CharacterAction reference is not available in the current GameSlice: "
                            + step.CharacterActionId + ".");
                }
                ValidateConditionReferences(step.EntryConditions, path + ".entryConditions", references, diagnostics);
                ValidateConditionReferences(step.MonitorConditions, path + ".monitorConditions", references, diagnostics);
                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    ValidateConditionReferences(
                        step.Branches[branchIndex].Conditions,
                        path + ".branches[" + branchIndex + "].conditions",
                        references,
                        diagnostics);
                }
            }
        }

        private static void ValidateConditionReferences(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                    && (references == null || !references.HasAbility(condition.AbilityId)))
                {
                    AddError(
                        diagnostics,
                        "CARD_COMPILE_CONDITION_ABILITY_UNKNOWN",
                        path + "[" + i + "].abilityId",
                        "Condition Ability reference is not available in the current GameSlice: "
                            + condition.AbilityId + ".");
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && (references == null || !references.HasCharacterAction(condition.CharacterActionId)))
                {
                    AddError(
                        diagnostics,
                        "CARD_COMPILE_CONDITION_ACTION_UNKNOWN",
                        path + "[" + i + "].characterActionId",
                        "Condition CharacterAction reference is not available in the current GameSlice: "
                            + condition.CharacterActionId + ".");
                }
            }
        }

        private static CharacterTestRuntimeAiLabCompiledStep CompileStep(
            CharacterTestRuntimeAiLabCardStep step,
            IReadOnlyDictionary<string, int> stepIndices)
        {
            CharacterTestRuntimeAiLabCardBranch[] sourceBranches = step.Branches.ToArray();
            Array.Sort(sourceBranches, CompareBranches);
            var branches = new CharacterTestRuntimeAiLabCompiledBranch[sourceBranches.Length];
            for (int i = 0; i < sourceBranches.Length; i++)
            {
                CharacterTestRuntimeAiLabCardBranch branch = sourceBranches[i];
                int targetStepIndex = string.Equals(
                    branch.TargetStepId,
                    CharacterTestRuntimeAiLabCardBranch.EndTarget,
                    StringComparison.Ordinal)
                    ? EndStepIndex
                    : stepIndices[branch.TargetStepId];
                branches[i] = new CharacterTestRuntimeAiLabCompiledBranch(
                    branch.BranchId,
                    branch.Trigger,
                    branch.Priority,
                    targetStepIndex,
                    branch.TargetStepId,
                    branch.Label,
                    branch.WindowStartFrame,
                    branch.WindowEndFrame,
                    branch.EndReason,
                    CompileConditions(branch.Conditions));
            }

            return new CharacterTestRuntimeAiLabCompiledStep(
                step.StepId,
                step.Order,
                step.Kind,
                step.Title,
                step.AbilityId,
                step.CharacterActionId,
                step.LegacyActionKind,
                step.MoveMinDistance,
                step.MoveMaxDistance,
                step.WaitFrames,
                step.EstimatedDurationFrames,
                step.Note,
                CompileConditions(step.EntryConditions),
                CompileConditions(step.MonitorConditions),
                branches);
        }

        private static CharacterTestRuntimeAiLabCompiledCondition[] CompileConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> source)
        {
            var conditions = new CharacterTestRuntimeAiLabCardCondition[source != null ? source.Count : 0];
            for (int i = 0; i < conditions.Length; i++)
                conditions[i] = source[i];
            Array.Sort(conditions, CompareConditions);
            var compiled = new CharacterTestRuntimeAiLabCompiledCondition[conditions.Length];
            for (int i = 0; i < conditions.Length; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                compiled[i] = new CharacterTestRuntimeAiLabCompiledCondition(
                    condition.ConditionId,
                    condition.Fact,
                    condition.Operator,
                    condition.BoolValue,
                    condition.Value,
                    condition.MaxValue,
                    condition.AbilityId,
                    condition.CharacterActionId,
                    condition.AllowedTargetPhases,
                    condition.RequiredTargetFlags,
                    condition.ForbiddenTargetFlags);
            }
            return compiled;
        }

        private static long ComputeContentHash(
            string entryStepId,
            int entryStepIndex,
            string intent,
            string riskSummary,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> triggerConditions,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledStep> steps)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddStringStable("runtimeAiLab.card.schema", CharacterTestRuntimeAiLabBehaviorCardPayload.Schema);
            accumulator.AddStringStable("runtimeAiLab.card.entryStepId", entryStepId);
            accumulator.AddInt("runtimeAiLab.card.entryStepIndex", entryStepIndex);
            accumulator.AddStringStable("runtimeAiLab.card.intent", intent);
            accumulator.AddStringStable("runtimeAiLab.card.riskSummary", riskSummary);
            AddConditionsHash(accumulator, "runtimeAiLab.card.trigger", triggerConditions);
            accumulator.AddInt("runtimeAiLab.card.stepCount", steps.Count);
            for (int i = 0; i < steps.Count; i++)
                AddStepHash(accumulator, "runtimeAiLab.card.step." + i, steps[i]);
            return accumulator.ToHash();
        }

        private static void AddStepHash(
            RuntimeContentHashAccumulator accumulator,
            string prefix,
            CharacterTestRuntimeAiLabCompiledStep step)
        {
            accumulator.AddStringStable(prefix + ".id", step.StepId);
            accumulator.AddInt(prefix + ".order", step.Order);
            accumulator.AddInt(prefix + ".kind", (int)step.Kind);
            accumulator.AddStringStable(prefix + ".title", step.Title);
            accumulator.AddInt(prefix + ".abilityId", step.AbilityId);
            accumulator.AddInt(prefix + ".characterActionId", step.CharacterActionId.Value);
            accumulator.AddInt(prefix + ".legacyActionKind", (int)step.LegacyActionKind);
            accumulator.AddDoubleQuantized(prefix + ".moveMinDistance", step.MoveMinDistance, FloatHashScale);
            accumulator.AddDoubleQuantized(prefix + ".moveMaxDistance", step.MoveMaxDistance, FloatHashScale);
            accumulator.AddInt(prefix + ".waitFrames", step.WaitFrames);
            accumulator.AddInt(prefix + ".estimatedDurationFrames", step.EstimatedDurationFrames);
            accumulator.AddStringStable(prefix + ".note", step.Note);
            AddConditionsHash(accumulator, prefix + ".entry", step.EntryConditions);
            AddConditionsHash(accumulator, prefix + ".monitor", step.MonitorConditions);
            accumulator.AddInt(prefix + ".branchCount", step.Branches.Count);
            for (int i = 0; i < step.Branches.Count; i++)
                AddBranchHash(accumulator, prefix + ".branch." + i, step.Branches[i]);
        }

        private static void AddBranchHash(
            RuntimeContentHashAccumulator accumulator,
            string prefix,
            CharacterTestRuntimeAiLabCompiledBranch branch)
        {
            accumulator.AddStringStable(prefix + ".id", branch.BranchId);
            accumulator.AddInt(prefix + ".trigger", (int)branch.Trigger);
            accumulator.AddInt(prefix + ".priority", branch.Priority);
            accumulator.AddInt(prefix + ".targetStepIndex", branch.TargetStepIndex);
            accumulator.AddStringStable(prefix + ".targetStepId", branch.TargetStepId);
            accumulator.AddStringStable(prefix + ".label", branch.Label);
            accumulator.AddInt(prefix + ".windowStartFrame", branch.WindowStartFrame);
            accumulator.AddInt(prefix + ".windowEndFrame", branch.WindowEndFrame);
            accumulator.AddStringStable(prefix + ".endReason", branch.EndReason);
            AddConditionsHash(accumulator, prefix + ".condition", branch.Conditions);
        }

        private static void AddConditionsHash(
            RuntimeContentHashAccumulator accumulator,
            string prefix,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> conditions)
        {
            accumulator.AddInt(prefix + ".count", conditions.Count);
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledCondition condition = conditions[i];
                string conditionPrefix = prefix + "." + i;
                accumulator.AddStringStable(conditionPrefix + ".id", condition.ConditionId);
                accumulator.AddInt(conditionPrefix + ".fact", (int)condition.Fact);
                accumulator.AddInt(conditionPrefix + ".operator", (int)condition.Operator);
                accumulator.AddInt(conditionPrefix + ".boolValue", condition.BoolValue ? 1 : 0);
                accumulator.AddDoubleQuantized(conditionPrefix + ".value", condition.Value, FloatHashScale);
                accumulator.AddDoubleQuantized(conditionPrefix + ".maxValue", condition.MaxValue, FloatHashScale);
                accumulator.AddInt(conditionPrefix + ".abilityId", condition.AbilityId);
                accumulator.AddInt(conditionPrefix + ".characterActionId", condition.CharacterActionId.Value);
            }
        }

        private static CharacterTestRuntimeAiLabBehaviorCardCompileResult Failed(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            SortDiagnostics(diagnostics);
            return new CharacterTestRuntimeAiLabBehaviorCardCompileResult(null, diagnostics.ToArray());
        }

        private static void AddError(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static void SortDiagnostics(List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            diagnostics.Sort(CompareDiagnostics);
        }

        private static int CompareDiagnostics(
            CharacterTestRuntimeAiLabCardDiagnostic left,
            CharacterTestRuntimeAiLabCardDiagnostic right)
        {
            int path = string.CompareOrdinal(left.Path, right.Path);
            return path != 0 ? path : string.CompareOrdinal(left.Code, right.Code);
        }

        private static int CompareSourceSteps(SourceStep left, SourceStep right)
        {
            int order = left.Step.Order.CompareTo(right.Step.Order);
            return order != 0 ? order : string.CompareOrdinal(left.Step.StepId, right.Step.StepId);
        }

        private static int CompareConditions(
            CharacterTestRuntimeAiLabCardCondition left,
            CharacterTestRuntimeAiLabCardCondition right)
        {
            return string.CompareOrdinal(left.ConditionId, right.ConditionId);
        }

        private static int CompareBranches(
            CharacterTestRuntimeAiLabCardBranch left,
            CharacterTestRuntimeAiLabCardBranch right)
        {
            int trigger = GetTriggerRank(left.Trigger).CompareTo(GetTriggerRank(right.Trigger));
            if (trigger != 0)
                return trigger;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.BranchId, right.BranchId);
        }

        private static int GetTriggerRank(CharacterTestRuntimeAiLabCardBranchTrigger trigger)
        {
            switch (trigger)
            {
                case CharacterTestRuntimeAiLabCardBranchTrigger.Condition:
                    return 0;
                case CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess:
                    return 1;
                default:
                    return 2;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct SourceStep
        {
            public SourceStep(CharacterTestRuntimeAiLabCardStep step, int sourceIndex)
            {
                Step = step;
                SourceIndex = sourceIndex;
            }

            public CharacterTestRuntimeAiLabCardStep Step { get; }
            public int SourceIndex { get; }
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardCompileCache
    {
        private string _workspaceId = string.Empty;
        private int _workspaceRevision = -1;
        private long _referenceHash;
        private readonly Dictionary<string, CacheEntry> _entries =
            new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, LogicCacheEntry> _logicEntries =
            new Dictionary<string, LogicCacheEntry>(StringComparer.Ordinal);

        public CharacterTestRuntimeAiLabBehaviorCardCompileResult GetOrCompile(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            string cardDraftId = card != null ? card.CardDraftId : string.Empty;
            int cardRevision = card != null ? card.CurrentRevision : -1;
            EnsureContext(workspace, references);

            if (_entries.TryGetValue(cardDraftId, out CacheEntry entry)
                && entry.CardRevision == cardRevision)
            {
                return entry.Result;
            }

            CharacterTestRuntimeAiLabBehaviorCardCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(card?.Payload, references);
            _entries[cardDraftId] = new CacheEntry(cardRevision, result);
            return result;
        }

        public CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult GetOrCompileLogic(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            string cardDraftId = card != null ? card.CardDraftId : string.Empty;
            int cardRevision = card != null ? card.CurrentRevision : -1;
            EnsureContext(workspace, references);
            if (_logicEntries.TryGetValue(cardDraftId, out LogicCacheEntry entry)
                && entry.CardRevision == cardRevision)
            {
                return entry.Result;
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result = card?.HasAuthoredSource == true
                ? new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(null, new[] {
                    new CharacterTestRuntimeAiLabCardDiagnostic(CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        "TACTICAL_RUNTIME_BINDING_REQUIRED", "$", "战术源已保留；当前角色编译预览不等于可启动的冻结版本。") })
                : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card?.LogicPayload, references);
            if (card != null && result.Success && result.Program != null)
                result = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                    result.Program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromCard(card)), new List<CharacterTestRuntimeAiLabCardDiagnostic>(result.Diagnostics).ToArray());
            _logicEntries[cardDraftId] = new LogicCacheEntry(cardRevision, result);
            return result;
        }

        private void EnsureContext(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            string workspaceId = workspace != null ? workspace.WorkspaceId : string.Empty;
            int workspaceRevision = workspace != null ? workspace.CurrentRevision : -1;
            long referenceHash = references != null ? references.StableHash : 0L;
            if (_workspaceRevision == workspaceRevision
                && _referenceHash == referenceHash
                && string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal))
            {
                return;
            }

            _workspaceId = workspaceId;
            _workspaceRevision = workspaceRevision;
            _referenceHash = referenceHash;
            _entries.Clear();
            _logicEntries.Clear();
        }

        public void Clear()
        {
            _workspaceId = string.Empty;
            _workspaceRevision = -1;
            _referenceHash = 0L;
            _entries.Clear();
            _logicEntries.Clear();
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(
                int cardRevision,
                CharacterTestRuntimeAiLabBehaviorCardCompileResult result)
            {
                CardRevision = cardRevision;
                Result = result;
            }

            public int CardRevision { get; }
            public CharacterTestRuntimeAiLabBehaviorCardCompileResult Result { get; }
        }

        private readonly struct LogicCacheEntry
        {
            public LogicCacheEntry(
                int cardRevision,
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult result)
            {
                CardRevision = cardRevision;
                Result = result;
            }

            public int CardRevision { get; }
            public CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Result { get; }
        }
    }
}
