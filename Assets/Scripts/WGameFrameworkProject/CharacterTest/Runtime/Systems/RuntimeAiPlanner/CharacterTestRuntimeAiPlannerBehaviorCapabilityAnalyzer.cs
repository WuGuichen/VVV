using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCapabilityReport
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _providedCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _disabledOnlyCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _missingCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorRiskTag[] _riskTags;

        public CharacterTestRuntimeAiPlannerBehaviorCapabilityReport(
            CharacterTestRuntimeAiPlannerBehaviorCapability[] providedCapabilities,
            CharacterTestRuntimeAiPlannerBehaviorCapability[] disabledOnlyCapabilities,
            CharacterTestRuntimeAiPlannerBehaviorCapability[] missingCapabilities,
            CharacterTestRuntimeAiPlannerBehaviorRiskTag[] riskTags,
            bool meleeBasicReady,
            bool arenaCombatReady,
            bool hasOnlyStallLoopRisk,
            string summaryText,
            string coverageText,
            string riskText)
        {
            _providedCapabilities = providedCapabilities ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            _disabledOnlyCapabilities = disabledOnlyCapabilities ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            _missingCapabilities = missingCapabilities ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            _riskTags = riskTags ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorRiskTag>();
            MeleeBasicReady = meleeBasicReady;
            ArenaCombatReady = arenaCombatReady;
            HasOnlyStallLoopRisk = hasOnlyStallLoopRisk;
            SummaryText = summaryText ?? string.Empty;
            CoverageText = coverageText ?? string.Empty;
            RiskText = riskText ?? string.Empty;
        }

        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> ProvidedCapabilities =>
            _providedCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> DisabledOnlyCapabilities =>
            _disabledOnlyCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> MissingCapabilities =>
            _missingCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> RiskTags => _riskTags;
        public bool MeleeBasicReady { get; }
        public bool ArenaCombatReady { get; }
        public bool HasOnlyStallLoopRisk { get; }
        public string SummaryText { get; }
        public string CoverageText { get; }
        public string RiskText { get; }

        public bool HasProvided(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            return Contains(_providedCapabilities, capability);
        }

        public bool HasDisabledOnly(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            return Contains(_disabledOnlyCapabilities, capability);
        }

        public bool HasMissing(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            return Contains(_missingCapabilities, capability);
        }

        public bool HasRisk(CharacterTestRuntimeAiPlannerBehaviorRiskTag riskTag)
        {
            return Contains(_riskTags, riskTag);
        }

        private static bool Contains(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> values,
            CharacterTestRuntimeAiPlannerBehaviorCapability target)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (values[i] == target)
                    return true;
            }

            return false;
        }

        private static bool Contains(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> values,
            CharacterTestRuntimeAiPlannerBehaviorRiskTag target)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (values[i] == target)
                    return true;
            }

            return false;
        }
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer
    {
        public static CharacterTestRuntimeAiPlannerBehaviorCapabilityReport Analyze(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                definitionCatalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            var provided = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>(8);
            var disabledOnly = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>(4);
            var missing = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>(6);
            var risks = new List<CharacterTestRuntimeAiPlannerBehaviorRiskTag>(6);

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries =
                profile?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (!catalog.TryGetDefinition(
                        entry.BehaviorId,
                        out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                {
                    Add(risks, CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingTargeting);
                    continue;
                }

                List<CharacterTestRuntimeAiPlannerBehaviorCapability> target =
                    entry.Disabled ? disabledOnly : provided;
                AddDefinitionCapabilities(target, definition);
                AddRiskTags(risks, definition.RiskTags);
            }

            AddMissingRequiredCapabilities(missing, provided, entries, catalog);
            RemoveProvided(disabledOnly, provided);
            AddMinimumMissing(missing, provided);
            AddGroupRisks(risks, provided);

            CharacterTestRuntimeAiPlannerBehaviorCapability[] providedArray = provided.ToArray();
            CharacterTestRuntimeAiPlannerBehaviorCapability[] disabledArray = disabledOnly.ToArray();
            CharacterTestRuntimeAiPlannerBehaviorCapability[] missingArray = missing.ToArray();
            CharacterTestRuntimeAiPlannerBehaviorRiskTag[] riskArray = risks.ToArray();
            bool meleeBasicReady = Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting)
                && Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage)
                && Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage)
                && Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);
            bool arenaCombatReady = Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting)
                && Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage)
                && Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback)
                && (Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage)
                    || Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit)
                    || Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Control));
            bool hasOnlyStallLoopRisk =
                (Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Kite)
                    || Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Defense))
                && (!Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage)
                    || !(Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage)
                        || Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit)
                        || Has(providedArray, CharacterTestRuntimeAiPlannerBehaviorCapability.Control)));

            return new CharacterTestRuntimeAiPlannerBehaviorCapabilityReport(
                providedArray,
                disabledArray,
                missingArray,
                riskArray,
                meleeBasicReady,
                arenaCombatReady,
                hasOnlyStallLoopRisk,
                FormatSummary(providedArray, missingArray, arenaCombatReady, hasOnlyStallLoopRisk),
                FormatCoverage(providedArray, disabledArray, missingArray),
                FormatRisks(riskArray, hasOnlyStallLoopRisk));
        }

        public static string FormatArenaMatchupText(
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport ally,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport enemy)
        {
            if (ally == null || enemy == null)
                return string.Empty;

            var parts = new List<string>(4);
            AppendMatchupWarning(parts, "Ally", ally, "Enemy", enemy);
            AppendMatchupWarning(parts, "Enemy", enemy, "Ally", ally);
            if (parts.Count == 0)
                return "Capability matchup: no obvious chase/kite deadlock risk.";
            return "Capability matchup:\n" + string.Join("\n", parts);
        }

        public static string FormatCapabilityLabel(
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (capability)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting:
                    return "Targeting";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                    return "Engage";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                    return "Damage";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit:
                    return "Pursuit";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Control:
                    return "Control";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Kite:
                    return "Kite";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Defense:
                    return "Defense";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation:
                    return "Navigation";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                    return "Fallback";
                case CharacterTestRuntimeAiPlannerBehaviorCapability.Support:
                    return "Support";
                default:
                    return "None";
            }
        }

        public static string FormatRiskLabel(CharacterTestRuntimeAiPlannerBehaviorRiskTag riskTag)
        {
            switch (riskTag)
            {
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.NoDamageIfAlone:
                    return "No damage if alone";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.NoReengageIfAlone:
                    return "No re-engage if alone";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.NeedsAbilitySlot:
                    return "Needs ability slot";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.CanStallAgainstKite:
                    return "Can stall against kite";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingTargeting:
                    return "Missing targeting";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingDamage:
                    return "Missing damage";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingFallback:
                    return "Missing fallback";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingEngageOrPressure:
                    return "Missing engage or pressure";
                case CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingKiteCounter:
                    return "Missing kite counter";
                default:
                    return "None";
            }
        }

        private static void AddDefinitionCapabilities(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> target,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            Add(target, definition.PrimaryCapability);
            for (int i = 0; i < definition.SecondaryCapabilities.Count; i++)
                Add(target, definition.SecondaryCapabilities[i]);
        }

        private static void AddMissingRequiredCapabilities(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> missing,
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> provided,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                if (entry == null || entry.Disabled)
                    continue;
                if (!catalog.TryGetDefinition(
                        entry.BehaviorId,
                        out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                {
                    continue;
                }

                AddMissingRequiredCapabilities(missing, provided, definition.RequiredCapabilities);
            }
        }

        private static void AddMissingRequiredCapabilities(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> missing,
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> provided,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> required)
        {
            for (int i = 0; required != null && i < required.Count; i++)
            {
                if (!Has(provided, required[i]))
                    Add(missing, required[i]);
            }
        }

        private static void AddMinimumMissing(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> missing,
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> provided)
        {
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting))
                Add(missing, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage))
                Add(missing, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback))
                Add(missing, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage)
                && !Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit)
                && !Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Control))
            {
                Add(missing, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage);
            }
        }

        private static void AddGroupRisks(
            List<CharacterTestRuntimeAiPlannerBehaviorRiskTag> risks,
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> provided)
        {
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting))
                Add(risks, CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingTargeting);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage))
                Add(risks, CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingDamage);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback))
                Add(risks, CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingFallback);
            if (!Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage)
                && !Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit)
                && !Has(provided, CharacterTestRuntimeAiPlannerBehaviorCapability.Control))
            {
                Add(risks, CharacterTestRuntimeAiPlannerBehaviorRiskTag.MissingEngageOrPressure);
            }
        }

        private static void AppendMatchupWarning(
            List<string> parts,
            string sideName,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport side,
            string opponentName,
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport opponent)
        {
            bool opponentCanKiteOrDefend =
                opponent.HasProvided(CharacterTestRuntimeAiPlannerBehaviorCapability.Kite)
                || opponent.HasProvided(CharacterTestRuntimeAiPlannerBehaviorCapability.Defense);
            bool sideCanCounter =
                side.HasProvided(CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit)
                || side.HasProvided(CharacterTestRuntimeAiPlannerBehaviorCapability.Control);
            if (opponentCanKiteOrDefend && !sideCanCounter)
            {
                parts.Add(sideName
                    + " may fail to catch "
                    + opponentName
                    + " because Pursuit/Control is missing.");
            }

            if (side.HasOnlyStallLoopRisk)
            {
                parts.Add(sideName
                    + " may only kite/defend without a reliable finish or re-engage chain.");
            }
        }

        private static string FormatSummary(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> provided,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> missing,
            bool arenaCombatReady,
            bool hasOnlyStallLoopRisk)
        {
            string readiness = arenaCombatReady ? "Arena runnable" : "Needs capability fixes";
            string missingText = missing != null && missing.Count > 0
                ? "missing " + JoinCapabilities(missing)
                : "minimum capabilities covered";
            string riskText = hasOnlyStallLoopRisk
                ? "; risk: may kite/defend without ending the fight"
                : string.Empty;
            return readiness
                + ": "
                + JoinCapabilities(provided)
                + " | "
                + missingText
                + riskText;
        }

        private static string FormatCoverage(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> provided,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> disabledOnly,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> missing)
        {
            return "Capability coverage: provided ["
                + JoinCapabilities(provided)
                + "] | disabled-only ["
                + JoinCapabilities(disabledOnly)
                + "] | missing ["
                + JoinCapabilities(missing)
                + "]";
        }

        private static string FormatRisks(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> riskTags,
            bool hasOnlyStallLoopRisk)
        {
            string riskText = JoinRiskTags(riskTags);
            if (hasOnlyStallLoopRisk)
            {
                if (!string.IsNullOrWhiteSpace(riskText) && riskText != "none")
                    riskText += ", ";
                riskText += "may only kite/defend";
            }

            return "Capability risks: " + riskText;
        }

        private static string JoinCapabilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        {
            if (capabilities == null || capabilities.Count == 0)
                return "none";

            var parts = new string[capabilities.Count];
            for (int i = 0; i < capabilities.Count; i++)
                parts[i] = FormatCapabilityLabel(capabilities[i]);
            return string.Join(", ", parts);
        }

        private static string JoinRiskTags(IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> riskTags)
        {
            if (riskTags == null || riskTags.Count == 0)
                return "none";

            var parts = new string[riskTags.Count];
            for (int i = 0; i < riskTags.Count; i++)
                parts[i] = FormatRiskLabel(riskTags[i]);
            return string.Join(", ", parts);
        }

        private static void RemoveProvided(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> disabledOnly,
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> provided)
        {
            for (int i = disabledOnly.Count - 1; i >= 0; i--)
            {
                if (Has(provided, disabledOnly[i]))
                    disabledOnly.RemoveAt(i);
            }
        }

        private static void AddRiskTags(
            List<CharacterTestRuntimeAiPlannerBehaviorRiskTag> risks,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                Add(risks, source[i]);
        }

        private static void Add(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> list,
            CharacterTestRuntimeAiPlannerBehaviorCapability value)
        {
            if (value == CharacterTestRuntimeAiPlannerBehaviorCapability.None || Has(list, value))
                return;
            list.Add(value);
        }

        private static void Add(
            List<CharacterTestRuntimeAiPlannerBehaviorRiskTag> list,
            CharacterTestRuntimeAiPlannerBehaviorRiskTag value)
        {
            if (value == CharacterTestRuntimeAiPlannerBehaviorRiskTag.None || Has(list, value))
                return;
            list.Add(value);
        }

        private static bool Has(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> list,
            CharacterTestRuntimeAiPlannerBehaviorCapability value)
        {
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }

            return false;
        }

        private static bool Has(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> list,
            CharacterTestRuntimeAiPlannerBehaviorRiskTag value)
        {
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }

            return false;
        }
    }
}
