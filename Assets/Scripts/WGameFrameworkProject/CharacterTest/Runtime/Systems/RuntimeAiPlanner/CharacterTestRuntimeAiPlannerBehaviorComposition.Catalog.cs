using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
    {
        public const string FoundationApproachAndAttack = "bc_3qz33zjzk4";
        public const string FoundationIdle = "bc_g6a5fw319a";
        private const int FoundationApproachAndAttackNumericId = 3;
        private const int FoundationIdleNumericId = 4;

        internal const string ScoreKey = "score";
        internal const string CostKey = "cost";
        internal const string MinDistanceKey = "minDistance";
        internal const string MaxDistanceKey = "maxDistance";
        internal const string AbilitySlotKey = "abilitySlot";
        internal const string SpeedScaleKey = "speedScale";
        internal const string DirectionHoldFramesKey = "directionHoldFrames";
        internal const string ReactionMinImpactFramesKey = "reactionMinImpactFrames";
        internal const string ReactionMaxImpactFramesKey = "reactionMaxImpactFrames";
        internal const string ReactionCooldownFramesKey = "reactionCooldownFrames";

        private static readonly CharacterTestRuntimeAiPlannerBehaviorModuleDefinition[] BuiltInDefinitions =
        {
            new CharacterTestRuntimeAiPlannerBehaviorModuleDefinition(
                FoundationApproachAndAttack,
                "靠近并普攻",
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Combat,
                "Published foundation card for approaching the current target and performing the primary combo.",
                Array.Empty<CharacterTestRuntimeAiPlannerFactKind>(),
                new[]
                {
                    FloatParameter(ScoreKey, "Utility score", 10f, 0f, 500f),
                    FloatParameter(CostKey, "Cost", 0f, 0f, 100f)
                },
                CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait,
                "combat",
                primaryCapability: CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                secondaryCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Engage
                }),
            new CharacterTestRuntimeAiPlannerBehaviorModuleDefinition(
                FoundationIdle,
                "发呆",
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Fallback,
                "Published foundation fallback card with an empty timeline.",
                Array.Empty<CharacterTestRuntimeAiPlannerFactKind>(),
                new[]
                {
                    FloatParameter(ScoreKey, "Utility score", 10f, 0f, 500f),
                    FloatParameter(CostKey, "Cost", 0f, 0f, 100f)
                },
                CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait,
                "fallback",
                primaryCapability: CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback)
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> Definitions =>
            BuiltInDefinitions;

        public static CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog CreateDefinitionCatalog()
        {
            return new CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog(BuiltInDefinitions);
        }

        public static bool TryGetDefinition(
            string moduleId,
            out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            moduleId = CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog
                .NormalizeLegacyBehaviorId(moduleId);
            for (int i = 0; i < BuiltInDefinitions.Length; i++)
            {
                if (!string.Equals(BuiltInDefinitions[i].ModuleId, moduleId, StringComparison.Ordinal))
                    continue;

                definition = BuiltInDefinitions[i];
                return true;
            }

            definition = null;
            return false;
        }

        public static bool IsNoOpPlaceholder(string behaviorId) => false;

        public static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateDefaultProfile(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile = null)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                runtimeProfile ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                source.ProfileId + ".composition",
                source.TargetSelectionMode,
                source.AttackRange,
                source.PerceptionSettings,
                source.MovementSettings);
            AddDefaults(profile);
            return profile;
        }

        private static void AddDefaults(CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            Add(profile, FoundationApproachAndAttack, true, 10);
            Add(profile, FoundationIdle, true, 20);
        }

        private static void Add(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string moduleId,
            bool enabled,
            int order)
        {
            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(moduleId, enabled, order);
            int numericId = string.Equals(moduleId, FoundationApproachAndAttack, StringComparison.Ordinal)
                ? FoundationApproachAndAttackNumericId
                : string.Equals(moduleId, FoundationIdle, StringComparison.Ordinal)
                    ? FoundationIdleNumericId
                    : 0;
            entry.NumericId = numericId;
            entry.DefinitionNumericId = numericId;
            if (TryGetDefinition(moduleId, out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                entry.SetResponsibilities(new[] { InferDefaultResponsibility(definition.PrimaryCapability) });
                for (int i = 0; i < definition.Parameters.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                        definition.Parameters[i];
                    switch (parameter.Kind)
                    {
                        case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                            entry.SetBool(parameter.Key, parameter.DefaultBool);
                            break;
                        case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                            entry.SetInt(parameter.Key, parameter.DefaultInt);
                            break;
                        default:
                            entry.SetFloat(parameter.Key, parameter.DefaultFloat);
                            break;
                    }
                }
            }

            profile.Modules.Add(entry);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind InferDefaultResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (capability)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting;
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit:
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Control:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure;
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage;
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback;
                default:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorParameterDefinition FloatParameter(
            string key,
            string displayName,
            float defaultValue,
            float min,
            float max)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                key,
                displayName,
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float,
                defaultFloat: defaultValue,
                minFloat: min,
                maxFloat: max);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorParameterDefinition IntParameter(
            string key,
            string displayName,
            int defaultValue,
            int min,
            int max)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                key,
                displayName,
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int,
                defaultInt: defaultValue,
                minInt: min,
                maxInt: max);
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorModuleDefinition[] _definitions;

        public CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> definitions)
        {
            if (definitions == null)
            {
                _definitions = Array.Empty<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>();
                CatalogHash = ComputeHash(_definitions);
                return;
            }

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition in definitions)
            {
                if (definition != null)
                    list.Add(definition);
            }

            _definitions = list.ToArray();
            CatalogHash = ComputeHash(_definitions);
        }

        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> Definitions =>
            _definitions;

        public long CatalogHash { get; }

        public bool TryGetDefinition(
            string behaviorId,
            out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            behaviorId = NormalizeLegacyBehaviorId(behaviorId);
            for (int i = 0; i < _definitions.Length; i++)
            {
                if (!string.Equals(_definitions[i].BehaviorId, behaviorId, StringComparison.Ordinal))
                    continue;

                definition = _definitions[i];
                return true;
            }

            definition = null;
            return false;
        }

        internal static string NormalizeLegacyBehaviorId(string behaviorId)
        {
            switch (behaviorId)
            {
                case "Engage.NoOp":
                case "Damage.NoOp":
                case "Fallback.WaitOrHold":
                    return CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
                case "Targeting.NearestEnemy":
                case "Movement.ApproachTarget":
                case "Movement.KeepDistance":
                case "Movement.StrafeWhenBlocked":
                case "Survival.AvoidHazard":
                case "Survival.DodgeThreat":
                case "Combat.PrimaryAttack":
                case "Combat.CastAbilitySlot":
                    return CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                        .FoundationApproachAndAttack;
                default:
                    return behaviorId;
            }
        }

        public static CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog CreateBuiltIn()
        {
            return CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefinitionCatalog();
        }

        private static long ComputeHash(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> definitions)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddInt("behaviorDefinition.count", definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = definitions[i];
                accumulator.AddInt("behaviorDefinition.index", i);
                accumulator.AddStringStable("behaviorDefinition.id", definition.BehaviorId);
                accumulator.AddInt("behaviorDefinition.category", (int)definition.Category);
                accumulator.AddInt("behaviorDefinition.primitive", (int)definition.ActionPrimitive);
                accumulator.AddStringStable("behaviorDefinition.stateId", definition.StateId);
                accumulator.AddStringStable(
                    "behaviorDefinition.abilitySlotParameterKey",
                    definition.AbilitySlotParameterKey);
                accumulator.AddInt("behaviorDefinition.movementOverride", definition.UsesMovementOverride ? 1 : 0);
                accumulator.AddInt("behaviorDefinition.primaryCapability", (int)definition.PrimaryCapability);
                AddCapabilityHash(
                    accumulator,
                    "behaviorDefinition.secondaryCapability",
                    definition.SecondaryCapabilities);
                AddCapabilityHash(
                    accumulator,
                    "behaviorDefinition.requiredCapability",
                    definition.RequiredCapabilities);
                AddCapabilityHash(
                    accumulator,
                    "behaviorDefinition.counterCapability",
                    definition.CounterCapabilities);
                AddRiskTagHash(accumulator, "behaviorDefinition.riskTag", definition.RiskTags);
                accumulator.AddInt("behaviorDefinition.requiredFactCount", definition.RequiredFacts.Count);
                for (int j = 0; j < definition.RequiredFacts.Count; j++)
                    accumulator.AddInt("behaviorDefinition.requiredFact", (int)definition.RequiredFacts[j]);
                accumulator.AddInt("behaviorDefinition.parameterCount", definition.Parameters.Count);
                for (int j = 0; j < definition.Parameters.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                        definition.Parameters[j];
                    accumulator.AddStringStable("behaviorDefinition.parameterKey", parameter.Key);
                    accumulator.AddInt("behaviorDefinition.parameterKind", (int)parameter.Kind);
                    accumulator.AddInt("behaviorDefinition.parameterDefaultBool", parameter.DefaultBool ? 1 : 0);
                    accumulator.AddInt("behaviorDefinition.parameterDefaultInt", parameter.DefaultInt);
                    accumulator.AddDoubleQuantized(
                        "behaviorDefinition.parameterDefaultFloat",
                        CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(parameter.DefaultFloat)
                            ? parameter.DefaultFloat
                            : 0f,
                        1000d);
                    accumulator.AddDoubleQuantized(
                        "behaviorDefinition.parameterMinFloat",
                        CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(parameter.MinFloat)
                            ? parameter.MinFloat
                            : 0f,
                        1000d);
                    accumulator.AddDoubleQuantized(
                        "behaviorDefinition.parameterMaxFloat",
                        CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(parameter.MaxFloat)
                            ? parameter.MaxFloat
                            : 0f,
                        1000d);
                    accumulator.AddInt("behaviorDefinition.parameterMinInt", parameter.MinInt);
                    accumulator.AddInt("behaviorDefinition.parameterMaxInt", parameter.MaxInt);
                }
                accumulator.AddInt("behaviorDefinition.conditionCount", definition.Conditions.Count);
                for (int j = 0; j < definition.Conditions.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorConditionDefinition condition =
                        definition.Conditions[j];
                    accumulator.AddInt("behaviorDefinition.conditionFact", (int)condition.Fact);
                    accumulator.AddInt("behaviorDefinition.conditionOperator", (int)condition.Operator);
                    accumulator.AddInt("behaviorDefinition.conditionBool", condition.BoolValue ? 1 : 0);
                    accumulator.AddDoubleQuantized(
                        "behaviorDefinition.conditionFloat",
                        condition.FloatValue,
                        1000d);
                    accumulator.AddStringStable(
                        "behaviorDefinition.conditionFloatParameter",
                        condition.FloatParameterKey);
                }
            }

            return accumulator.ToHash();
        }

        private static void AddCapabilityHash(
            RuntimeContentHashAccumulator accumulator,
            string key,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        {
            accumulator.AddInt(key + ".count", capabilities != null ? capabilities.Count : 0);
            for (int i = 0; capabilities != null && i < capabilities.Count; i++)
                accumulator.AddInt(key, (int)capabilities[i]);
        }

        private static void AddRiskTagHash(
            RuntimeContentHashAccumulator accumulator,
            string key,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> riskTags)
        {
            accumulator.AddInt(key + ".count", riskTags != null ? riskTags.Count : 0);
            for (int i = 0; riskTags != null && i < riskTags.Count; i++)
                accumulator.AddInt(key, (int)riskTags[i]);
        }
    }

}
