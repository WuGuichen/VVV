using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler
    {
        private const int RootStateId = 1;
        private const int TargetingStateId = 2;
        private const int EngageStateId = 3;
        private const int SurvivalStateId = 4;
        private const int MovementStateId = 5;
        private const int CombatStateId = 6;
        private const int FallbackStateId = 7;

        public static CharacterTestRuntimeAiPlannerBehaviorCompileResult Compile(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            return Compile(
                profile,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCompileResult Compile(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile source =
                profile ?? CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                definitionCatalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            var actions = new List<CharacterTestRuntimeAiPlannerActionDefinition>();
            var transitions = new List<CharacterTestRuntimeAiPlannerTransitionDefinition>();
            var summaries = new List<CharacterTestRuntimeAiPlannerBehaviorModuleSummary>();
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] modules = CopySortedModules(source.Modules);
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < modules.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = modules[i];
                if (module.NumericId <= 0 || module.DefinitionNumericId <= 0)
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "composition.identity.missing", "Behavior entries and definitions must have authoring-assigned numeric identities before compilation."));
                    continue;
                }
                string entryId = module != null ? module.EntryId : string.Empty;
                if (!string.IsNullOrWhiteSpace(entryId) && entryIds.Add(entryId))
                    continue;

                diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "composition.entry.duplicate",
                    "Behavior composition entry ids must be non-empty and unique: " + entryId));
            }
            bool hasSurvival = HasEnabledModule(
                modules,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack)
                || HasEnabledModule(
                    modules,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            bool hasCombat = HasEnabledModule(
                modules,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack)
                || HasEnabledModule(
                    modules,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            bool hasOnlyFallback = HasOnlyEnabledFallbackModules(modules);

            if (hasSurvival && hasCombat)
            {
                diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning,
                    "composition.priority.survivalCombat",
                    "Survival modules compile with higher transition priority than combat modules."));
            }

            if (hasOnlyFallback)
            {
                transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    0,
                    FallbackStateId,
                    100,
                    CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true),
                    "target exists -> fallback"));
            }
            else
            {
                transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    0,
                    SurvivalStateId,
                    100,
                    CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                        CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard,
                        true),
                    "hazard -> survival"));
                transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    0,
                    FallbackStateId,
                    90,
                    CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                        CharacterTestRuntimeAiPlannerTargetLifeState.Alive,
                        CharacterTestRuntimeAiPlannerConditionOperator.NotEquals),
                    "inactive target -> fallback"));
                transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    0,
                    CombatStateId,
                    70,
                    CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange,
                        true),
                    "target in range -> combat"));
                transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    0,
                    MovementStateId,
                    40,
                    CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                        CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                        true),
                    "target exists -> movement"));
            }
            transitions.Add(new CharacterTestRuntimeAiPlannerTransitionDefinition(
                0,
                FallbackStateId,
                10,
                CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                    CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                    false),
                "no target -> fallback"));

            for (int i = 0; i < modules.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = modules[i];
                if (!catalog.TryGetDefinition(
                        module.BehaviorId,
                        out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "composition.module.unknown",
                        "Unknown behavior definition: " + module.BehaviorId));
                    summaries.Add(CreateSummary(module, null, 0, string.Empty, "Unknown behavior definition."));
                    continue;
                }

                if (module.Enabled
                    && string.Equals(
                        module.BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal)
                    && module.GetInt(
                        definition,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey)
                        > module.GetInt(
                            definition,
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "composition.reactionWindow.invalid",
                        "Reactive dodge minimum impact frame must be less than or equal to its maximum impact frame: " + module.EntryId));
                }

                int before = actions.Count;
                string generated = module.Enabled
                    ? AppendModuleActions(definition, module, i, actions)
                    : string.Empty;
                int generatedCount = actions.Count - before;
                summaries.Add(CreateSummary(
                    module,
                    definition,
                    generatedCount,
                    generated,
                    module.Disabled ? "disabled condition blocked" : string.Empty));
            }

            ValidateGeneratedActions(actions, diagnostics);

            CharacterTestRuntimeAiPlannerProfile generatedProfile = diagnosticsHasError(diagnostics)
                ? null
                : new CharacterTestRuntimeAiPlannerProfile(
                    string.IsNullOrWhiteSpace(source.CompositionId)
                        ? CharacterTestRuntimeAiPlannerProfileSource.DefaultProfileId
                        : source.CompositionId + ".compiled",
                    MovementStateId,
                    source.TargetSelectionMode,
                    source.AttackRange,
                    CreateStates(),
                    transitions,
                    actions,
                    source.PerceptionSettings,
                    source.MovementSettings);

            CharacterTestRuntimeAiPlannerValidationReport compilerReport =
                new CharacterTestRuntimeAiPlannerValidationReport(diagnostics);
            CharacterTestRuntimeAiPlannerValidationReport validationReport = generatedProfile != null
                ? CharacterTestRuntimeAiPlannerProfileValidator.Validate(generatedProfile)
                : null;
            CharacterTestRuntimeAiPlannerValidationReport combined =
                CharacterTestRuntimeAiPlannerProfileJson.CombineReports(compilerReport, validationReport);
            if (combined != null && combined.HasErrors)
                generatedProfile = null;
            return new CharacterTestRuntimeAiPlannerBehaviorCompileResult(
                generatedProfile,
                combined,
                summaries.ToArray());
        }

        private static CharacterTestRuntimeAiPlannerStateDefinition[] CreateStates()
        {
            return new[]
            {
                new CharacterTestRuntimeAiPlannerStateDefinition(RootStateId, displayName: "Root"),
                new CharacterTestRuntimeAiPlannerStateDefinition(TargetingStateId, RootStateId, "Targeting"),
                new CharacterTestRuntimeAiPlannerStateDefinition(EngageStateId, RootStateId, "Engage"),
                new CharacterTestRuntimeAiPlannerStateDefinition(SurvivalStateId, EngageStateId, "Survival"),
                new CharacterTestRuntimeAiPlannerStateDefinition(MovementStateId, EngageStateId, "Movement"),
                new CharacterTestRuntimeAiPlannerStateDefinition(CombatStateId, EngageStateId, "Combat"),
                new CharacterTestRuntimeAiPlannerStateDefinition(FallbackStateId, RootStateId, "Fallback")
            };
        }

        private static string AppendModuleActions(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            int index,
            List<CharacterTestRuntimeAiPlannerActionDefinition> actions)
        {
            if (!module.Enabled)
                return string.Empty;

            int actionId = module.NumericId;
            return AppendDefinitionAction(definition, module, actionId, actions);
        }

        private static string AppendDefinitionAction(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            int actionId,
            List<CharacterTestRuntimeAiPlannerActionDefinition> actions)
        {
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
                return string.Empty;

            CharacterTestRuntimeAiPlannerActionKind kind = ToActionKind(definition.ActionPrimitive);
            int stateId = ResolveDefinitionStateId(definition.StateId, kind);
            CharacterTestRuntimeAiPlannerConditionDefinition[] conditions =
                BuildRuntimeConditions(definition, module);
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings =
                definition.UsesMovementOverride
                    ? CreateMovementOverride(definition, module)
                    : null;
            int abilitySlot = kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                ? module.GetInt(
                    definition,
                    !string.IsNullOrWhiteSpace(definition.AbilitySlotParameterKey)
                        ? definition.AbilitySlotParameterKey
                        : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.AbilitySlotKey)
                : 0;
            actions.Add(new CharacterTestRuntimeAiPlannerActionDefinition(
                actionId,
                stateId,
                kind,
                module.GetFloat(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey),
                module.GetFloat(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey),
                FormatBehaviorActionName(definition, module, definition.DisplayName),
                abilitySlot: abilitySlot,
                conditions: conditions,
                movementSettings: movementSettings,
                reactionCooldownFrames: kind == CharacterTestRuntimeAiPlannerActionKind.Dodge
                    ? module.GetInt(
                        definition,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionCooldownFramesKey)
                    : 0));
            return kind.ToString() + "@" + stateId;
        }

        private static int ResolveDefinitionStateId(string stateId, CharacterTestRuntimeAiPlannerActionKind kind)
        {
            if (!string.IsNullOrWhiteSpace(stateId))
            {
                switch (stateId.Trim().ToLowerInvariant())
                {
                    case "root": return RootStateId;
                    case "targeting": return TargetingStateId;
                    case "engage": return EngageStateId;
                    case "survival": return SurvivalStateId;
                    case "movement": return MovementStateId;
                    case "combat": return CombatStateId;
                    case "fallback": return FallbackStateId;
                    default:
                        if (int.TryParse(stateId, out int numeric) && numeric > 0)
                            return numeric;
                        break;
                }
            }
            return ResolveDefaultStateId(kind);
        }

        private static string FormatBehaviorActionName(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            string fallback)
        {
            string name = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : fallback;
            if (module == null || string.IsNullOrWhiteSpace(module.EntryId))
                return name ?? string.Empty;

            return (name ?? string.Empty) + " [" + module.EntryId + "]";
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition[] BuildRuntimeConditions(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module)
        {
            if (definition.Conditions.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerConditionDefinition>();

            var conditions = new CharacterTestRuntimeAiPlannerConditionDefinition[definition.Conditions.Count];
            for (int i = 0; i < definition.Conditions.Count; i++)
                conditions[i] = definition.Conditions[i].ToRuntimeCondition(definition, module);
            return conditions;
        }

        private static CharacterTestRuntimeAiPlannerActionKind ToActionKind(
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive primitive)
        {
            switch (primitive)
            {
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.MoveToTarget:
                    return CharacterTestRuntimeAiPlannerActionKind.MoveToTarget;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Strafe:
                    return CharacterTestRuntimeAiPlannerActionKind.Strafe;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Retreat:
                    return CharacterTestRuntimeAiPlannerActionKind.Retreat;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.PrimaryAttack:
                    return CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.CastAbilitySlot:
                    return CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Dodge:
                    return CharacterTestRuntimeAiPlannerActionKind.Dodge;
                default:
                    return CharacterTestRuntimeAiPlannerActionKind.Wait;
            }
        }

        private static int ResolveDefaultStateId(CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return MovementStateId;
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return CombatStateId;
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return RootStateId;
                default:
                    return FallbackStateId;
            }
        }

        private static CharacterTestRuntimeAiPlannerMovementSettings CreateMovementOverride(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module)
        {
            bool hasMin = definition.TryGetParameter(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MinDistanceKey,
                out _);
            bool hasMax = definition.TryGetParameter(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MaxDistanceKey,
                out _);
            float min = hasMin
                ? module.GetFloat(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MinDistanceKey)
                : CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMin;
            float max = hasMax
                ? module.GetFloat(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MaxDistanceKey)
                : hasMin
                    ? min
                    : CharacterTestRuntimeAiPlannerMovementSettings.DefaultPreferredDistanceMax;
            int hold = definition.TryGetParameter(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.DirectionHoldFramesKey,
                out _)
                ? module.GetInt(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.DirectionHoldFramesKey)
                : CharacterTestRuntimeAiPlannerMovementSettings.DefaultDirectionHoldFrames;
            float speedScale = definition.TryGetParameter(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey,
                out _)
                ? module.GetFloat(definition, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey)
                : CharacterTestRuntimeAiPlannerMovementSettings.DefaultSpeedScale;
            return new CharacterTestRuntimeAiPlannerMovementSettings(
                speedScale,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultSteeringResponsiveness,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultApproachLateralBlend,
                min,
                max,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultArrivalSlowdownDistance,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMinFrames,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMaxFrames,
                hold,
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultReactionDelayFrames);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleSummary CreateSummary(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            int generatedActionCount,
            string generatedActionsText,
            string diagnosticsText)
        {
            if (definition == null)
            {
                return new CharacterTestRuntimeAiPlannerBehaviorModuleSummary(
                    module != null ? module.ModuleId : string.Empty,
                    module != null ? module.ModuleId : string.Empty,
                    CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Fallback,
                    module != null && module.Enabled,
                    string.Empty,
                    string.Empty,
                    generatedActionsText,
                    generatedActionCount,
                    diagnosticsText,
                    module != null ? module.EntryId : string.Empty);
            }

            return new CharacterTestRuntimeAiPlannerBehaviorModuleSummary(
                definition.ModuleId,
                definition.DisplayName,
                definition.Category,
                module.Enabled,
                definition.Intent,
                FormatRequiredFacts(definition.RequiredFacts),
                generatedActionsText,
                generatedActionCount,
                diagnosticsText,
                module != null ? module.EntryId : string.Empty);
        }

        private static void ValidateGeneratedActions(
            List<CharacterTestRuntimeAiPlannerActionDefinition> actions,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerActionDefinition action = actions[i];
                if (!ids.Add(action.ActionId))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "composition.action.duplicate",
                        "Behavior composition generated duplicate action id: " + action.ActionId));
                }

                if (HasContradictoryConditions(action))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "composition.action.unreachable",
                        "Behavior composition generated an unreachable action: " + action.ActionId));
                }
            }
        }

        private static bool HasContradictoryConditions(
            CharacterTestRuntimeAiPlannerActionDefinition action)
        {
            for (int i = 0; i < action.Conditions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerConditionDefinition a = action.Conditions[i];
                for (int j = i + 1; j < action.Conditions.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerConditionDefinition b = action.Conditions[j];
                    if (a.Fact != b.Fact)
                        continue;

                    if (a.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                        && b.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                        && a.BoolValue != b.BoolValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool RequiresTargetFacts(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            for (int i = 0; i < definition.RequiredFacts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerFactKind fact = definition.RequiredFacts[i];
                if (fact == CharacterTestRuntimeAiPlannerFactKind.TargetExists
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDistance
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetVisible
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetDead
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetLifeState
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames
                    || fact == CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] CopySortedModules(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> modules)
        {
            if (modules == null || modules.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>();

            var copy = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry[modules.Count];
            for (int i = 0; i < modules.Count; i++)
                copy[i] = modules[i];
            Array.Sort(copy, CompareModuleOrder);
            return copy;
        }

        private static int CompareModuleOrder(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry left,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;
            return string.CompareOrdinal(left.ModuleId, right.ModuleId);
        }

        private static bool HasEnabledModule(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] modules,
            string moduleId)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = modules[i];
                if (module != null
                    && module.Enabled
                    && string.Equals(module.ModuleId, moduleId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOnlyEnabledFallbackModules(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] modules)
        {
            bool hasEnabledFallback = false;
            for (int i = 0; i < modules.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = modules[i];
                if (module == null || !module.Enabled)
                    continue;

                if (!string.Equals(
                        module.BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                hasEnabledFallback = true;
            }

            return hasEnabledFallback;
        }

        private static string FormatRequiredFacts(
            IReadOnlyList<CharacterTestRuntimeAiPlannerFactKind> facts)
        {
            if (facts == null || facts.Count == 0)
                return "none";

            var names = new string[facts.Count];
            for (int i = 0; i < facts.Count; i++)
                names[i] = facts[i].ToString();
            return string.Join(", ", names);
        }

        private static bool diagnosticsHasError(
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition Bool(
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool value)
        {
            return CharacterTestRuntimeAiPlannerConditionDefinition.Bool(fact, value);
        }

        private static CharacterTestRuntimeAiPlannerConditionDefinition Float(
            CharacterTestRuntimeAiPlannerFactKind fact,
            CharacterTestRuntimeAiPlannerConditionOperator op,
            float value)
        {
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                fact,
                op,
                false,
                value);
        }
    }
}
