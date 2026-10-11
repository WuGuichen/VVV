using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Author-facing organization for an installed behavior. Responsibilities do not affect
    /// candidate eligibility, score, ordering, or runtime arbitration.
    /// </summary>
    internal enum CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind
    {
        Other = 0,
        Targeting = 1,
        EngageOrPressure = 2,
        Damage = 3,
        Fallback = 4
    }

    /// <summary>
    /// Legacy v1/v2 persistence value. New code must use BehaviorResponsibilityKind.
    /// </summary>
    internal enum CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind
    {
        Flexible = 0,
        Targeting = 1,
        EngageOrPressure = 2,
        Damage = 3,
        Fallback = 4
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorResponsibilityState
    {
        Ready = 0,
        Missing = 1,
        AllDisabled = 2,
        InvalidAssignment = 3
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport
    {
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            int totalCount,
            int enabledCount,
            int compatibleCount,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityState state,
            string message)
        {
            ResponsibilityKind = responsibilityKind;
            TotalCount = totalCount;
            EnabledCount = enabledCount;
            CompatibleCount = compatibleCount;
            State = state;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ResponsibilityKind { get; }
        public int TotalCount { get; }
        public int EnabledCount { get; }
        public int CompatibleCount { get; }
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityState State { get; }
        public string Message { get; }
        public bool BlocksRuntime => State == CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.InvalidAssignment;
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy
    {
        private static readonly CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] Responsibilities =
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> OrderedResponsibilities =>
            Responsibilities;

        public static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] Normalize(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
        {
            bool targeting = false;
            bool engage = false;
            bool damage = false;
            bool fallback = false;
            if (source != null)
            {
                foreach (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility in source)
                {
                    switch (responsibility)
                    {
                        case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting:
                            targeting = true;
                            break;
                        case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure:
                            engage = true;
                            break;
                        case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage:
                            damage = true;
                            break;
                        case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback:
                            fallback = true;
                            break;
                    }
                }
            }

            int explicitCount = (targeting ? 1 : 0)
                + (engage ? 1 : 0)
                + (damage ? 1 : 0)
                + (fallback ? 1 : 0);
            if (explicitCount == 0)
                return new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };

            var result = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[explicitCount];
            int index = 0;
            if (targeting)
                result[index++] = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting;
            if (engage)
                result[index++] = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure;
            if (damage)
                result[index++] = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage;
            if (fallback)
                result[index] = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback;
            return result;
        }

        /// <summary>
        /// Projects authored behavior capabilities into the explicit responsibilities the behavior
        /// can actually fulfil. Other is used only when no explicit responsibility matches.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResolveCompatibleResponsibilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        {
            var compatible = new List<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>(4);
            for (int i = 0; i < Responsibilities.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility = Responsibilities[i];
                if (responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
                    continue;
                if (IsCompatible(responsibility, capabilities))
                    compatible.Add(responsibility);
            }
            return Normalize(compatible);
        }

        public static bool Contains(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            for (int i = 0; responsibilities != null && i < responsibilities.Count; i++)
            {
                if (responsibilities[i] == responsibility)
                    return true;
            }
            return false;
        }

        public static bool SetEquals(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> left,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0))
                return false;
            for (int i = 0; left != null && i < left.Count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }

        public static string GetDisplayName(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind)
        {
            switch (responsibilityKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting:
                    return "索敌";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure:
                    return "接战";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage:
                    return "输出";
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback:
                    return "兜底";
                default:
                    return "其他";
            }
        }

        public static bool IsCompatible(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (responsibilityKind == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
                return true;
            if (definition == null)
                return false;
            if (AcceptsCapability(responsibilityKind, definition.PrimaryCapability))
                return true;

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> secondary =
                definition.SecondaryCapabilities;
            for (int i = 0; i < secondary.Count; i++)
            {
                if (AcceptsCapability(responsibilityKind, secondary[i]))
                    return true;
            }
            return false;
        }

        public static bool IsCompatible(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        {
            if (responsibilityKind == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
                return true;
            if (capabilities == null)
                return false;

            for (int i = 0; i < capabilities.Count; i++)
            {
                if (AcceptsCapability(responsibilityKind, capabilities[i]))
                    return true;
            }
            return false;
        }

        public static bool IsCompatible(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind invalidResponsibility)
        {
            for (int i = 0; responsibilities != null && i < responsibilities.Count; i++)
            {
                if (IsCompatible(responsibilities[i], definition))
                    continue;
                invalidResponsibility = responsibilities[i];
                return false;
            }
            invalidResponsibility = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            return true;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[] BuildReports(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitions =
                catalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            var reports = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[Responsibilities.Length];
            for (int responsibilityIndex = 0; responsibilityIndex < Responsibilities.Length; responsibilityIndex++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility =
                    Responsibilities[responsibilityIndex];
                int totalCount = 0;
                int enabledCount = 0;
                int compatibleCount = 0;
                bool invalidAssignment = false;
                for (int entryIndex = 0; entries != null && entryIndex < entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[entryIndex];
                    if (entry == null || !entry.HasResponsibility(responsibility))
                        continue;

                    totalCount++;
                    if (!entry.Disabled)
                        enabledCount++;
                    if (!definitions.TryGetDefinition(
                            entry.BehaviorId,
                            out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                        || !IsCompatible(responsibility, definition))
                    {
                        invalidAssignment = true;
                        continue;
                    }
                    compatibleCount++;
                }

                CharacterTestRuntimeAiPlannerBehaviorResponsibilityState state;
                string message;
                if (invalidAssignment)
                {
                    state = CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.InvalidAssignment;
                    message = GetDisplayName(responsibility) + "中存在不兼容或无效的行为。";
                }
                else if (totalCount == 0)
                {
                    state = CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.Missing;
                    message = GetDisplayName(responsibility) + "尚未配置。";
                }
                else if (enabledCount == 0)
                {
                    state = CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.AllDisabled;
                    message = "0 可用 · " + totalCount + " 已禁用";
                }
                else
                {
                    state = CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.Ready;
                    message = enabledCount + " 可用";
                }

                reports[responsibilityIndex] =
                    new CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport(
                        responsibility,
                        totalCount,
                        enabledCount,
                        compatibleCount,
                        state,
                        message);
            }
            return reports;
        }

        public static bool TryValidateAssignments(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog,
            out string message)
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[] reports =
                BuildReports(entries, catalog);
            for (int i = 0; i < reports.Length; i++)
            {
                if (!reports[i].BlocksRuntime)
                    continue;
                message = reports[i].Message;
                return false;
            }
            message = string.Empty;
            return true;
        }

        private static bool AcceptsCapability(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            switch (responsibilityKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting:
                    return capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting;
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure:
                    return capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Engage
                        || capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit
                        || capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Control;
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage:
                    return capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Damage;
                case CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback:
                    return capability == CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// Compatibility facade for v1/v2 call sites. It deliberately does not enforce cardinality
    /// and never inserts, replaces, enables, disables, reorders, or reclassifies entries.
    /// </summary>
    internal static class CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy
    {
        private static readonly CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind[] LegacyCoreSlots =
        {
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind> OrderedCoreSlots =>
            LegacyCoreSlots;

        public static bool IsCore(CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind) => false;

        public static string GetDisplayName(CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind) =>
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(ToResponsibility(slotKind));

        public static bool IsCompatible(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition) =>
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.IsCompatible(
                ToResponsibility(slotKind),
                definition);

        public static CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport Validate(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            bool success = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.TryValidateAssignments(
                entries,
                catalog,
                out string message);
            return new CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport(
                success,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible,
                message);
        }

        public static bool EnsureContract(
            List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            return false;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorModuleEntry CreateDefaultEntry(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            string behaviorId;
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting:
                    behaviorId = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack;
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    behaviorId = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    behaviorId = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
                    break;
                default:
                    behaviorId = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
                    break;
            }

            return CreateEntry(slotKind, behaviorId);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorModuleEntry CreateEntry(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            string behaviorId)
        {
            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                behaviorId ?? string.Empty,
                enabled: true,
                order: 0)
            {
                EntryId = behaviorId ?? string.Empty,
            };
            entry.SetResponsibilities(new[] { ToResponsibility(slotKind) });

            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            if (!catalog.TryGetDefinition(entry.BehaviorId, out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                return entry;

            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter = definition.Parameters[i];
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

            return entry;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ToResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind) =>
            slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible
                ? CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                : (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind)(int)slotKind;

        public static CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind ToLegacySlot(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind) =>
            responsibilityKind == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                ? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible
                : (CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind)(int)responsibilityKind;
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport
    {
        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport(
            bool success,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind missingSlot,
            string message)
        {
            Success = success;
            MissingSlot = missingSlot;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind MissingSlot { get; }
        public string Message { get; }
    }
}
