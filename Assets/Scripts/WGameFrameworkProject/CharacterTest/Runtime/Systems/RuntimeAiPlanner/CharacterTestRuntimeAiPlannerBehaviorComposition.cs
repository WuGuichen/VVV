using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerBehaviorParameterKind
    {
        Bool = 0,
        Int = 1,
        Float = 2
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorModuleCategory
    {
        Targeting = 0,
        Movement = 1,
        Survival = 2,
        Combat = 3,
        Fallback = 4
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorCapability
    {
        None = 0,
        Targeting = 1,
        Engage = 2,
        Damage = 3,
        Pursuit = 4,
        Control = 5,
        Kite = 6,
        Defense = 7,
        Navigation = 8,
        Fallback = 9,
        Support = 10
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorRiskTag
    {
        None = 0,
        NoDamageIfAlone = 1,
        NoReengageIfAlone = 2,
        NeedsAbilitySlot = 3,
        CanStallAgainstKite = 4,
        MissingTargeting = 5,
        MissingDamage = 6,
        MissingFallback = 7,
        MissingEngageOrPressure = 8,
        MissingKiteCounter = 9
    }

    internal enum CharacterTestRuntimeAiPlannerBehaviorActionPrimitive
    {
        None = 0,
        Wait = 1,
        MoveToTarget = 2,
        Strafe = 3,
        Retreat = 4,
        PrimaryAttack = 5,
        CastAbilitySlot = 6,
        Dodge = 7
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorConditionDefinition
    {
        public CharacterTestRuntimeAiPlannerBehaviorConditionDefinition(
            CharacterTestRuntimeAiPlannerFactKind fact,
            CharacterTestRuntimeAiPlannerConditionOperator op,
            bool boolValue = false,
            float floatValue = 0f,
            string floatParameterKey = "")
        {
            Fact = fact;
            Operator = op;
            BoolValue = boolValue;
            FloatValue = floatValue;
            FloatParameterKey = floatParameterKey ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerFactKind Fact { get; }
        public CharacterTestRuntimeAiPlannerConditionOperator Operator { get; }
        public bool BoolValue { get; }
        public float FloatValue { get; }
        public string FloatParameterKey { get; }

        public CharacterTestRuntimeAiPlannerConditionDefinition ToRuntimeCondition(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            float resolvedFloat = !string.IsNullOrWhiteSpace(FloatParameterKey)
                ? entry.GetFloat(definition, FloatParameterKey)
                : FloatValue;
            return new CharacterTestRuntimeAiPlannerConditionDefinition(
                Fact,
                Operator,
                BoolValue,
                resolvedFloat);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorConditionDefinition Bool(
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool value)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorConditionDefinition(
                fact,
                CharacterTestRuntimeAiPlannerConditionOperator.Equals,
                value);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorConditionDefinition FloatParameter(
            CharacterTestRuntimeAiPlannerFactKind fact,
            CharacterTestRuntimeAiPlannerConditionOperator op,
            string parameterKey)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorConditionDefinition(
                fact,
                op,
                floatParameterKey: parameterKey);
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorParameterDefinition
    {
        public CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
            string key,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorParameterKind kind,
            bool defaultBool = false,
            int defaultInt = 0,
            float defaultFloat = 0f,
            float minFloat = 0f,
            float maxFloat = 0f,
            int minInt = 0,
            int maxInt = 0)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            DefaultBool = defaultBool;
            DefaultInt = defaultInt;
            DefaultFloat = defaultFloat;
            MinFloat = minFloat;
            MaxFloat = maxFloat;
            MinInt = minInt;
            MaxInt = maxInt;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public CharacterTestRuntimeAiPlannerBehaviorParameterKind Kind { get; }
        public bool DefaultBool { get; }
        public int DefaultInt { get; }
        public float DefaultFloat { get; }
        public float MinFloat { get; }
        public float MaxFloat { get; }
        public int MinInt { get; }
        public int MaxInt { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorParameterValue
    {
        private CharacterTestRuntimeAiPlannerBehaviorParameterValue(
            CharacterTestRuntimeAiPlannerBehaviorParameterKind kind,
            bool boolValue,
            int intValue,
            float floatValue)
        {
            Kind = kind;
            BoolValue = boolValue;
            IntValue = intValue;
            FloatValue = floatValue;
        }

        public CharacterTestRuntimeAiPlannerBehaviorParameterKind Kind { get; }
        public bool BoolValue { get; }
        public int IntValue { get; }
        public float FloatValue { get; }

        public static CharacterTestRuntimeAiPlannerBehaviorParameterValue Bool(bool value)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorParameterValue(
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool,
                value,
                value ? 1 : 0,
                value ? 1f : 0f);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorParameterValue Int(int value)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorParameterValue(
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int,
                value != 0,
                value,
                value);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorParameterValue Float(float value)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorParameterValue(
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float,
                value != 0f,
                (int)value,
                value);
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorModuleDefinition
    {
        private readonly CharacterTestRuntimeAiPlannerFactKind[] _requiredFacts;
        private readonly CharacterTestRuntimeAiPlannerBehaviorParameterDefinition[] _parameters;
        private readonly CharacterTestRuntimeAiPlannerBehaviorConditionDefinition[] _conditions;
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _secondaryCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _requiredCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] _counterCapabilities;
        private readonly CharacterTestRuntimeAiPlannerBehaviorRiskTag[] _riskTags;

        public CharacterTestRuntimeAiPlannerBehaviorModuleDefinition(
            string moduleId,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorModuleCategory category,
            string intent,
            IEnumerable<CharacterTestRuntimeAiPlannerFactKind> requiredFacts,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition> parameters,
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive actionPrimitive =
                CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None,
            string stateId = "",
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition> conditions = null,
            string abilitySlotParameterKey = "",
            bool usesMovementOverride = false,
            CharacterTestRuntimeAiPlannerBehaviorCapability primaryCapability =
                CharacterTestRuntimeAiPlannerBehaviorCapability.None,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> secondaryCapabilities = null,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> requiredCapabilities = null,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> counterCapabilities = null,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorRiskTag> riskTags = null)
        {
            ModuleId = moduleId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            Intent = intent ?? string.Empty;
            _requiredFacts = Copy(requiredFacts);
            _parameters = Copy(parameters);
            ActionPrimitive = actionPrimitive;
            StateId = stateId ?? string.Empty;
            _conditions = Copy(conditions);
            AbilitySlotParameterKey = abilitySlotParameterKey ?? string.Empty;
            UsesMovementOverride = usesMovementOverride;
            PrimaryCapability = primaryCapability;
            _secondaryCapabilities = CopyCapabilities(secondaryCapabilities);
            _requiredCapabilities = CopyCapabilities(requiredCapabilities);
            _counterCapabilities = CopyCapabilities(counterCapabilities);
            _riskTags = CopyRiskTags(riskTags);
        }

        public string ModuleId { get; }
        public string BehaviorId => ModuleId;
        public string DisplayName { get; }
        public CharacterTestRuntimeAiPlannerBehaviorModuleCategory Category { get; }
        public string Intent { get; }
        public CharacterTestRuntimeAiPlannerBehaviorActionPrimitive ActionPrimitive { get; }
        public string StateId { get; }
        public string AbilitySlotParameterKey { get; }
        public bool UsesMovementOverride { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCapability PrimaryCapability { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerFactKind> RequiredFacts => _requiredFacts;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition> Parameters => _parameters;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition> Conditions => _conditions;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> SecondaryCapabilities =>
            _secondaryCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> RequiredCapabilities =>
            _requiredCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> CounterCapabilities =>
            _counterCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> RiskTags => _riskTags;

        public bool TryGetParameter(
            string key,
            out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition definition)
        {
            for (int i = 0; i < _parameters.Length; i++)
            {
                if (!string.Equals(_parameters[i].Key, key, StringComparison.Ordinal))
                    continue;

                definition = _parameters[i];
                return true;
            }

            definition = default;
            return false;
        }

        private static CharacterTestRuntimeAiPlannerFactKind[] Copy(
            IEnumerable<CharacterTestRuntimeAiPlannerFactKind> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerFactKind>();

            var list = new List<CharacterTestRuntimeAiPlannerFactKind>();
            foreach (CharacterTestRuntimeAiPlannerFactKind item in source)
                list.Add(item);
            return list.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorParameterDefinition[] Copy(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorParameterDefinition item in source)
                list.Add(item);
            return list.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorConditionDefinition[] Copy(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorConditionDefinition item in source)
                list.Add(item);
            return list.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCapability[] CopyCapabilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorCapability item in source)
            {
                if (item != CharacterTestRuntimeAiPlannerBehaviorCapability.None)
                    list.Add(item);
            }

            return list.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorRiskTag[] CopyRiskTags(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorRiskTag> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorRiskTag>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorRiskTag>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorRiskTag item in source)
            {
                if (item != CharacterTestRuntimeAiPlannerBehaviorRiskTag.None)
                    list.Add(item);
            }

            return list.ToArray();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorModuleEntry
    {
        private readonly Dictionary<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> _parameters =
            new Dictionary<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue>(StringComparer.Ordinal);
        private CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] _responsibilityKinds =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };

        public CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
            string moduleId,
            bool enabled = true,
            int order = 0)
        {
            EntryId = moduleId ?? string.Empty;
            BehaviorId = moduleId ?? string.Empty;
            Disabled = !enabled;
            Order = order;
        }

        public string EntryId { get; set; }
        public int NumericId { get; set; }
        public int DefinitionNumericId { get; set; }
        public string BehaviorId { get; set; }
        public bool Disabled { get; set; }
        public string ModuleId
        {
            get => BehaviorId;
            set
            {
                BehaviorId = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(EntryId))
                    EntryId = BehaviorId;
            }
        }

        public bool Enabled
        {
            get => !Disabled;
            set => Disabled = !value;
        }

        public int Order { get; set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> ResponsibilityKinds =>
            _responsibilityKinds;

        [Obsolete("Use ResponsibilityKinds and SetResponsibilities for multi-responsibility authoring.")]
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ResponsibilityKind
        {
            get => _responsibilityKinds[0];
            set => SetResponsibilities(new[] { value });
        }

        public bool HasResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility) =>
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Contains(
                _responsibilityKinds,
                responsibility);

        public void SetResponsibilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities)
        {
            _responsibilityKinds = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(
                responsibilities);
        }

        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind CoreSlotKind
        {
            get => CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToLegacySlot(_responsibilityKinds[0]);
            set => SetResponsibilities(new[] { CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(value) });
        }
        public IDictionary<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> Parameters => _parameters;
        public IDictionary<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> ParameterOverrides => _parameters;

        public CharacterTestRuntimeAiPlannerBehaviorModuleEntry Clone()
        {
            var copy = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(ModuleId, Enabled, Order);
            copy.EntryId = EntryId;
            copy.NumericId = NumericId;
            copy.DefinitionNumericId = DefinitionNumericId;
            copy.BehaviorId = BehaviorId;
            copy.Disabled = Disabled;
            copy.SetResponsibilities(_responsibilityKinds);
            foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in _parameters)
                copy.Parameters[pair.Key] = pair.Value;
            return copy;
        }

        public void SetBool(string key, bool value)
        {
            _parameters[key ?? string.Empty] = CharacterTestRuntimeAiPlannerBehaviorParameterValue.Bool(value);
        }

        public void SetInt(string key, int value)
        {
            _parameters[key ?? string.Empty] = CharacterTestRuntimeAiPlannerBehaviorParameterValue.Int(value);
        }

        public void SetFloat(string key, float value)
        {
            _parameters[key ?? string.Empty] = CharacterTestRuntimeAiPlannerBehaviorParameterValue.Float(value);
        }

        public bool GetBool(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string key)
        {
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                ResolveParameter(definition, key);
            return _parameters.TryGetValue(key ?? string.Empty, out CharacterTestRuntimeAiPlannerBehaviorParameterValue value)
                ? value.BoolValue
                : parameter.DefaultBool;
        }

        public int GetInt(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string key)
        {
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                ResolveParameter(definition, key);
            int value = _parameters.TryGetValue(key ?? string.Empty, out CharacterTestRuntimeAiPlannerBehaviorParameterValue actual)
                ? actual.IntValue
                : parameter.DefaultInt;
            return Clamp(value, parameter.MinInt, parameter.MaxInt);
        }

        public float GetFloat(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string key)
        {
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                ResolveParameter(definition, key);
            float value = _parameters.TryGetValue(key ?? string.Empty, out CharacterTestRuntimeAiPlannerBehaviorParameterValue actual)
                ? actual.FloatValue
                : parameter.DefaultFloat;
            return Clamp(value, parameter.MinFloat, parameter.MaxFloat);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorParameterDefinition ResolveParameter(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string key)
        {
            if (definition != null && definition.TryGetParameter(key, out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter))
                return parameter;

            return new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                key,
                key,
                CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (min != max)
            {
                if (value < min)
                    return min;
                if (value > max)
                    return max;
            }

            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (!CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(value))
                return min;
            if (min != max)
            {
                if (value < min)
                    return min;
                if (value > max)
                    return max;
            }

            return value;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCompositionProfile
    {
        private readonly List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> _modules =
            new List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>();

        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
            string compositionId = "",
            CharacterTestAiTargetSelectionMode targetSelectionMode =
                CharacterTestAiTargetSelectionMode.NearestOpposingTeam,
            float attackRange = CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
            CharacterTestRuntimeAiPlannerPerceptionSettings perceptionSettings = null,
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings = null)
        {
            CompositionId = string.IsNullOrWhiteSpace(compositionId)
                ? "character-test.runtime-ai-planner.composition.default"
                : compositionId;
            DisplayName = CompositionId;
            TargetSelectionMode = targetSelectionMode;
            AttackRange = attackRange;
            PerceptionSettings = CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(perceptionSettings);
            MovementSettings = CharacterTestRuntimeAiPlannerMovementSettings.Normalize(movementSettings);
        }

        public string CompositionId { get; set; }
        public string GroupId
        {
            get => CompositionId;
            set => CompositionId = value ?? string.Empty;
        }

        public string DisplayName { get; set; }
        public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; set; }
        public float AttackRange { get; set; }
        public CharacterTestRuntimeAiPlannerPerceptionSettings PerceptionSettings { get; set; }
        public CharacterTestRuntimeAiPlannerMovementSettings MovementSettings { get; set; }
        public List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> Modules => _modules;
        public List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> Entries => _modules;

        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Clone()
        {
            var copy = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                CompositionId,
                TargetSelectionMode,
                AttackRange,
                PerceptionSettings,
                MovementSettings);
            copy.DisplayName = DisplayName;
            for (int i = 0; i < _modules.Count; i++)
                copy.Modules.Add(_modules[i].Clone());
            return copy;
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorModuleSummary
    {
        public CharacterTestRuntimeAiPlannerBehaviorModuleSummary(
            string moduleId,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorModuleCategory category,
            bool enabled,
            string intent,
            string requiredFactsText,
            string generatedActionsText,
            int generatedActionCount,
            string diagnosticsText,
            string entryId = null)
        {
            ModuleId = moduleId ?? string.Empty;
            EntryId = string.IsNullOrWhiteSpace(entryId) ? ModuleId : entryId;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            Enabled = enabled;
            Intent = intent ?? string.Empty;
            RequiredFactsText = requiredFactsText ?? string.Empty;
            GeneratedActionsText = generatedActionsText ?? string.Empty;
            GeneratedActionCount = generatedActionCount;
            DiagnosticsText = diagnosticsText ?? string.Empty;
        }

        public string EntryId { get; }
        public string ModuleId { get; }
        public string DisplayName { get; }
        public CharacterTestRuntimeAiPlannerBehaviorModuleCategory Category { get; }
        public bool Enabled { get; }
        public string Intent { get; }
        public string RequiredFactsText { get; }
        public string GeneratedActionsText { get; }
        public int GeneratedActionCount { get; }
        public string DiagnosticsText { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCompileResult
    {
        public CharacterTestRuntimeAiPlannerBehaviorCompileResult(
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestRuntimeAiPlannerValidationReport report,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary[] moduleSummaries)
        {
            Profile = profile;
            Report = report;
            ModuleSummaries = moduleSummaries ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorModuleSummary>();
        }

        public CharacterTestRuntimeAiPlannerProfile Profile { get; }
        public CharacterTestRuntimeAiPlannerValidationReport Report { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleSummary> ModuleSummaries { get; }
        public bool Success => Profile != null && Report != null && Report.Success;
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult
    {
        public CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
            bool success,
            string compositionId,
            int profileSlot,
            string reportText)
        {
            Success = success;
            CompositionId = compositionId ?? string.Empty;
            ProfileSlot = profileSlot;
            ReportText = reportText ?? string.Empty;
        }

        public bool Success { get; }
        public string CompositionId { get; }
        public int ProfileSlot { get; }
        public string ReportText { get; }
    }

}
