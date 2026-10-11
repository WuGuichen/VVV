using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalogJson
    {
        public const string Schema =
            "mx.character-test.runtime-ai-planner.behavior-definition-catalog.v1";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string ToJson(CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog source =
                catalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            return JsonConvert.SerializeObject(DefinitionCatalogDocument.FromCatalog(source), JsonSettings);
        }

        public static bool TryLoad(
            string json,
            out CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog,
            out CharacterTestRuntimeAiPlannerValidationReport report)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorDefinitionCatalog.json.empty",
                    "Runtime AI Planner behavior definition catalog JSON is empty.");
                return false;
            }

            DefinitionCatalogDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<DefinitionCatalogDocument>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorDefinitionCatalog.json.parse",
                    "Runtime AI Planner behavior definition catalog JSON parse failed: " + exception.Message);
                return false;
            }

            if (document == null || !string.Equals(document.Schema, Schema, StringComparison.Ordinal))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorDefinitionCatalog.schema.unsupported",
                    "Runtime AI Planner behavior definition catalog schema is unsupported.");
                return false;
            }

            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            var definitions = new List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (document.Behaviors != null)
            {
                for (int i = 0; i < document.Behaviors.Length; i++)
                {
                    BehaviorDefinitionDto dto = document.Behaviors[i];
                    if (dto == null || string.IsNullOrWhiteSpace(dto.BehaviorId))
                    {
                        diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                            CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                            "behaviorDefinitionCatalog.behaviorId.missing",
                            "Behavior definition is missing behaviorId at index " + i.ToString()));
                        continue;
                    }

                    if (!ids.Add(dto.BehaviorId))
                    {
                        diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                            CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                            "behaviorDefinitionCatalog.behaviorId.duplicate",
                            "Duplicate behaviorId: " + dto.BehaviorId));
                        continue;
                    }

                    definitions.Add(dto.ToDefinition());
                }
            }

            catalog = new CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog(definitions);
            report = new CharacterTestRuntimeAiPlannerValidationReport(diagnostics);
            return report.Success;
        }

        private static CharacterTestRuntimeAiPlannerValidationReport CreateReport(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity,
            string code,
            string message)
        {
            return new CharacterTestRuntimeAiPlannerValidationReport(new[]
            {
                new CharacterTestRuntimeAiPlannerDiagnostic(severity, code, message)
            });
        }

        private sealed class DefinitionCatalogDocument
        {
            public string Schema { get; set; }
            public string CatalogId { get; set; }
            public int Revision { get; set; }
            public BehaviorDefinitionDto[] Behaviors { get; set; }

            public static DefinitionCatalogDocument FromCatalog(
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
            {
                var document = new DefinitionCatalogDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalogJson.Schema,
                    CatalogId = "character-test.runtime-ai-planner.behaviors",
                    Revision = 1,
                    Behaviors = new BehaviorDefinitionDto[catalog.Definitions.Count]
                };
                for (int i = 0; i < catalog.Definitions.Count; i++)
                    document.Behaviors[i] = BehaviorDefinitionDto.FromDefinition(catalog.Definitions[i]);
                return document;
            }
        }

        private sealed class BehaviorDefinitionDto
        {
            public string BehaviorId { get; set; }
            public string StableId { get; set; }
            public string DisplayName { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorModuleCategory Category { get; set; }
            public string Intent { get; set; }
            public string[] Tags { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorCapability PrimaryCapability { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorCapability[] SecondaryCapabilities { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorCapability[] RequiresCapabilities { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorCapability[] CountersCapabilities { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorRiskTag[] RiskTags { get; set; }
            public CharacterTestRuntimeAiPlannerFactKind[] RequiredFacts { get; set; }
            public ParameterDto[] Parameters { get; set; }
            public ConditionDto[] Conditions { get; set; }
            public float? Score { get; set; }
            public float? Cost { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorActionPrimitive ActionPrimitive { get; set; }
            public string StateId { get; set; }
            public string AbilitySlotParameterKey { get; set; }
            public bool MovementOverride { get; set; }

            public static BehaviorDefinitionDto FromDefinition(
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
            {
                var parameters = new ParameterDto[definition.Parameters.Count];
                for (int i = 0; i < definition.Parameters.Count; i++)
                    parameters[i] = ParameterDto.FromDefinition(definition.Parameters[i]);
                var conditions = new ConditionDto[definition.Conditions.Count];
                for (int i = 0; i < definition.Conditions.Count; i++)
                    conditions[i] = ConditionDto.FromDefinition(definition.Conditions[i]);
                var requiredFacts = new CharacterTestRuntimeAiPlannerFactKind[definition.RequiredFacts.Count];
                for (int i = 0; i < definition.RequiredFacts.Count; i++)
                    requiredFacts[i] = definition.RequiredFacts[i];
                var secondaryCapabilities =
                    new CharacterTestRuntimeAiPlannerBehaviorCapability[definition.SecondaryCapabilities.Count];
                for (int i = 0; i < definition.SecondaryCapabilities.Count; i++)
                    secondaryCapabilities[i] = definition.SecondaryCapabilities[i];
                var requiresCapabilities =
                    new CharacterTestRuntimeAiPlannerBehaviorCapability[definition.RequiredCapabilities.Count];
                for (int i = 0; i < definition.RequiredCapabilities.Count; i++)
                    requiresCapabilities[i] = definition.RequiredCapabilities[i];
                var countersCapabilities =
                    new CharacterTestRuntimeAiPlannerBehaviorCapability[definition.CounterCapabilities.Count];
                for (int i = 0; i < definition.CounterCapabilities.Count; i++)
                    countersCapabilities[i] = definition.CounterCapabilities[i];
                var riskTags = new CharacterTestRuntimeAiPlannerBehaviorRiskTag[definition.RiskTags.Count];
                for (int i = 0; i < definition.RiskTags.Count; i++)
                    riskTags[i] = definition.RiskTags[i];
                return new BehaviorDefinitionDto
                {
                    BehaviorId = definition.BehaviorId,
                    StableId = definition.BehaviorId,
                    DisplayName = definition.DisplayName,
                    Category = definition.Category,
                    Intent = definition.Intent,
                    PrimaryCapability = definition.PrimaryCapability,
                    SecondaryCapabilities = secondaryCapabilities,
                    RequiresCapabilities = requiresCapabilities,
                    CountersCapabilities = countersCapabilities,
                    RiskTags = riskTags,
                    RequiredFacts = requiredFacts,
                    Parameters = parameters,
                    Conditions = conditions,
                    ActionPrimitive = definition.ActionPrimitive,
                    StateId = definition.StateId,
                    AbilitySlotParameterKey = definition.AbilitySlotParameterKey,
                    MovementOverride = definition.UsesMovementOverride
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorModuleDefinition ToDefinition()
            {
                var parameters = new List<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition>();
                if (Parameters != null)
                {
                    for (int i = 0; i < Parameters.Length; i++)
                    {
                        if (Parameters[i] != null)
                            parameters.Add(Parameters[i].ToDefinition());
                    }
                }

                if (Score.HasValue)
                {
                    EnsureFloatParameter(
                        parameters,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                        "Utility score",
                        Score.Value,
                        0f,
                        500f);
                }

                if (Cost.HasValue)
                {
                    EnsureFloatParameter(
                        parameters,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey,
                        "Cost",
                        Cost.Value,
                        0f,
                        100f);
                }

                var conditions = new List<CharacterTestRuntimeAiPlannerBehaviorConditionDefinition>();
                if (Conditions != null)
                {
                    for (int i = 0; i < Conditions.Length; i++)
                    {
                        if (Conditions[i] != null)
                            conditions.Add(Conditions[i].ToDefinition());
                    }
                }

                return new CharacterTestRuntimeAiPlannerBehaviorModuleDefinition(
                    BehaviorId,
                    DisplayName,
                    Category,
                    Intent,
                    RequiredFacts,
                    parameters,
                    ActionPrimitive,
                    StateId,
                    conditions,
                    AbilitySlotParameterKey,
                    MovementOverride,
                    PrimaryCapability,
                    SecondaryCapabilities,
                    RequiresCapabilities,
                    CountersCapabilities,
                    RiskTags);
            }

            private static void EnsureFloatParameter(
                List<CharacterTestRuntimeAiPlannerBehaviorParameterDefinition> parameters,
                string key,
                string displayName,
                float defaultValue,
                float min,
                float max)
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (string.Equals(parameters[i].Key, key, StringComparison.Ordinal))
                        return;
                }

                parameters.Add(new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                    key,
                    displayName,
                    CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float,
                    defaultFloat: defaultValue,
                    minFloat: min,
                    maxFloat: max));
            }
        }

        private sealed class ParameterDto
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorParameterKind Kind { get; set; }
            public bool DefaultBool { get; set; }
            public int DefaultInt { get; set; }
            public float DefaultFloat { get; set; }
            public float MinFloat { get; set; }
            public float MaxFloat { get; set; }
            public int MinInt { get; set; }
            public int MaxInt { get; set; }

            public static ParameterDto FromDefinition(
                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition definition)
            {
                return new ParameterDto
                {
                    Key = definition.Key,
                    DisplayName = definition.DisplayName,
                    Kind = definition.Kind,
                    DefaultBool = definition.DefaultBool,
                    DefaultInt = definition.DefaultInt,
                    DefaultFloat = definition.DefaultFloat,
                    MinFloat = definition.MinFloat,
                    MaxFloat = definition.MaxFloat,
                    MinInt = definition.MinInt,
                    MaxInt = definition.MaxInt
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorParameterDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                    Key,
                    DisplayName,
                    Kind,
                    DefaultBool,
                    DefaultInt,
                    DefaultFloat,
                    MinFloat,
                    MaxFloat,
                    MinInt,
                    MaxInt);
            }
        }

        private sealed class ConditionDto
        {
            public CharacterTestRuntimeAiPlannerFactKind Fact { get; set; }
            public CharacterTestRuntimeAiPlannerConditionOperator Operator { get; set; }
            public bool BoolValue { get; set; }
            public float FloatValue { get; set; }
            public string FloatParameterKey { get; set; }

            public static ConditionDto FromDefinition(
                CharacterTestRuntimeAiPlannerBehaviorConditionDefinition definition)
            {
                return new ConditionDto
                {
                    Fact = definition.Fact,
                    Operator = definition.Operator,
                    BoolValue = definition.BoolValue,
                    FloatValue = definition.FloatValue,
                    FloatParameterKey = definition.FloatParameterKey
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorConditionDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerBehaviorConditionDefinition(
                    Fact,
                    Operator,
                    BoolValue,
                    FloatValue,
                    FloatParameterKey);
            }
        }
    }

}
