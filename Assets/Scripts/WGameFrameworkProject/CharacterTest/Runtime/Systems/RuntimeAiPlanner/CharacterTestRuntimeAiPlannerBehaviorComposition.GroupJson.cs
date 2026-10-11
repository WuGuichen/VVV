using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerBehaviorGroupJson
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.behavior-group.v4";
        private const string LegacySchemaV3 = "mx.character-test.runtime-ai-planner.behavior-group.v3";
        private const string LegacySchemaV2 = "mx.character-test.runtime-ai-planner.behavior-group.v2";
        private const string LegacySchemaV1 = "mx.character-test.runtime-ai-planner.behavior-group.v1";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string ToJson(CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile source =
                group ?? CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            return JsonConvert.SerializeObject(GroupDocument.FromProfile(source), JsonSettings);
        }

        public static bool TryLoad(
            string json,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog,
            out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group,
            out CharacterTestRuntimeAiPlannerValidationReport report)
        {
            group = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorGroup.json.empty",
                    "Runtime AI Planner behavior group JSON is empty.");
                return false;
            }

            GroupDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<GroupDocument>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorGroup.json.parse",
                    "Runtime AI Planner behavior group JSON parse failed: " + exception.Message);
                return false;
            }

            if (document == null
                || (!string.Equals(document.Schema, Schema, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, LegacySchemaV3, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, LegacySchemaV2, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, LegacySchemaV1, StringComparison.Ordinal)))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorGroup.schema.unsupported",
                    "Runtime AI Planner behavior group schema is unsupported.");
                return false;
            }

            group = document.ToProfile();
            if (!CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.TryValidateAssignments(
                    group.Entries,
                    catalog,
                    out string assignmentMessage))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorGroup.responsibility.invalidAssignment",
                    assignmentMessage);
                return false;
            }
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(group, catalog);
            report = compile.Report;
            return report != null && report.Success;
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

        private sealed class GroupDocument
        {
            public string Schema { get; set; }
            public string GroupId { get; set; }
            public string StableId { get; set; }
            public string DisplayName { get; set; }
            public CharacterTestAiTargetSelectionMode Targeting { get; set; }
            public float AttackRange { get; set; }
            public PerceptionDto Perception { get; set; }
            public MovementDto Movement { get; set; }
            public EntryDto[] Entries { get; set; }

            public static GroupDocument FromProfile(
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
            {
                var document = new GroupDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerBehaviorGroupJson.Schema,
                    GroupId = profile.GroupId,
                    StableId = profile.GroupId,
                    DisplayName = profile.DisplayName,
                    Targeting = profile.TargetSelectionMode,
                    AttackRange = profile.AttackRange,
                    Perception = PerceptionDto.FromSettings(profile.PerceptionSettings),
                    Movement = MovementDto.FromSettings(profile.MovementSettings),
                    Entries = new EntryDto[profile.Entries.Count]
                };
                for (int i = 0; i < profile.Entries.Count; i++)
                    document.Entries[i] = EntryDto.FromEntry(profile.Entries[i]);
                return document;
            }

            public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile ToProfile()
            {
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                    GroupId,
                    Targeting,
                    AttackRange,
                    Perception != null
                        ? Perception.ToSettings()
                        : CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                    Movement != null
                        ? Movement.ToSettings()
                        : CharacterTestRuntimeAiPlannerMovementSettings.CreateDefault());
                profile.DisplayName = !string.IsNullOrWhiteSpace(DisplayName)
                    ? DisplayName
                    : GroupId;
                if (Entries != null)
                {
                    for (int i = 0; i < Entries.Length; i++)
                    {
                        if (Entries[i] != null)
                            profile.Entries.Add(Entries[i].ToEntry());
                    }
                }

                return profile;
            }
        }

        private sealed class EntryDto
        {
            public string EntryId { get; set; }
            public int NumericId { get; set; }
            public int DefinitionNumericId { get; set; }
            public string BehaviorId { get; set; }
            public int Order { get; set; }
            public bool Disabled { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; set; }

            [JsonProperty("responsibilityKind")]
            public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? LegacyResponsibilityKind { get; set; }

            [JsonProperty("slotKind")]
            public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind? LegacySlotKind { get; set; }

            public bool ShouldSerializeLegacySlotKind() => false;
            public bool ShouldSerializeLegacyResponsibilityKind() => false;
            public ParameterDto[] ParameterOverrides { get; set; }

            public static EntryDto FromEntry(CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
            {
                var parameters = new List<ParameterDto>();
                foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in
                    entry.ParameterOverrides)
                {
                    parameters.Add(ParameterDto.FromValue(pair.Key, pair.Value));
                }

                return new EntryDto
                {
                    EntryId = entry.EntryId,
                    NumericId = entry.NumericId,
                    DefinitionNumericId = entry.DefinitionNumericId,
                    BehaviorId = entry.BehaviorId,
                    Order = entry.Order,
                    Disabled = entry.Disabled,
                    ResponsibilityKinds = CopyResponsibilities(entry.ResponsibilityKinds),
                    ParameterOverrides = parameters.ToArray()
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorModuleEntry ToEntry()
            {
                var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    BehaviorId,
                    !Disabled,
                    Order)
                {
                    EntryId = string.IsNullOrWhiteSpace(EntryId) ? BehaviorId : EntryId,
                    NumericId = NumericId,
                    DefinitionNumericId = DefinitionNumericId,
                    BehaviorId = BehaviorId ?? string.Empty,
                    Disabled = Disabled
                };
                entry.SetResponsibilities(
                    ResponsibilityKinds
                    ?? new[]
                    {
                        LegacyResponsibilityKind
                        ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(
                            LegacySlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible)
                    });
                if (ParameterOverrides != null)
                {
                    for (int i = 0; i < ParameterOverrides.Length; i++)
                    {
                        ParameterDto parameter = ParameterOverrides[i];
                        if (parameter == null)
                            continue;

                        entry.ParameterOverrides[parameter.Key ?? string.Empty] = parameter.ToValue();
                    }
                }

                return entry;
            }

            private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
            {
                var result = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[source?.Count ?? 0];
                for (int i = 0; i < result.Length; i++)
                    result[i] = source[i];
                return result;
            }
        }

        private sealed class PerceptionDto
        {
            public float VisionRange { get; set; }
            public int TargetMemoryFrames { get; set; }
            public int LineOfSightObstacleLayerMask { get; set; }
            public float EnvironmentSampleRadius { get; set; }
            public float ThreatDistanceWeight { get; set; }

            public static PerceptionDto FromSettings(
                CharacterTestRuntimeAiPlannerPerceptionSettings settings)
            {
                CharacterTestRuntimeAiPlannerPerceptionSettings source =
                    CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
                return new PerceptionDto
                {
                    VisionRange = source.VisionRange,
                    TargetMemoryFrames = source.TargetMemoryFrames,
                    LineOfSightObstacleLayerMask = source.LineOfSightObstacleLayerMask,
                    EnvironmentSampleRadius = source.EnvironmentSampleRadius,
                    ThreatDistanceWeight = source.ThreatDistanceWeight
                };
            }

            public CharacterTestRuntimeAiPlannerPerceptionSettings ToSettings()
            {
                return new CharacterTestRuntimeAiPlannerPerceptionSettings(
                    VisionRange,
                    TargetMemoryFrames,
                    LineOfSightObstacleLayerMask,
                    EnvironmentSampleRadius,
                    ThreatDistanceWeight);
            }
        }

        private sealed class MovementDto
        {
            public float SpeedScale { get; set; }
            public float SteeringResponsiveness { get; set; }
            public float ApproachLateralBlend { get; set; }
            public float PreferredDistanceMin { get; set; }
            public float PreferredDistanceMax { get; set; }
            public float ArrivalSlowdownDistance { get; set; }
            public int StrafeSwitchMinFrames { get; set; }
            public int StrafeSwitchMaxFrames { get; set; }
            public int DirectionHoldFrames { get; set; }
            public int ReactionDelayFrames { get; set; }

            public static MovementDto FromSettings(
                CharacterTestRuntimeAiPlannerMovementSettings settings)
            {
                CharacterTestRuntimeAiPlannerMovementSettings source =
                    CharacterTestRuntimeAiPlannerMovementSettings.Normalize(settings);
                return new MovementDto
                {
                    SpeedScale = source.SpeedScale,
                    SteeringResponsiveness = source.SteeringResponsiveness,
                    ApproachLateralBlend = source.ApproachLateralBlend,
                    PreferredDistanceMin = source.PreferredDistanceMin,
                    PreferredDistanceMax = source.PreferredDistanceMax,
                    ArrivalSlowdownDistance = source.ArrivalSlowdownDistance,
                    StrafeSwitchMinFrames = source.StrafeSwitchMinFrames,
                    StrafeSwitchMaxFrames = source.StrafeSwitchMaxFrames,
                    DirectionHoldFrames = source.DirectionHoldFrames,
                    ReactionDelayFrames = source.ReactionDelayFrames
                };
            }

            public CharacterTestRuntimeAiPlannerMovementSettings ToSettings()
            {
                return new CharacterTestRuntimeAiPlannerMovementSettings(
                    SpeedScale,
                    SteeringResponsiveness,
                    ApproachLateralBlend,
                    PreferredDistanceMin,
                    PreferredDistanceMax,
                    ArrivalSlowdownDistance,
                    StrafeSwitchMinFrames,
                    StrafeSwitchMaxFrames,
                    DirectionHoldFrames,
                    ReactionDelayFrames);
            }
        }

        private sealed class ParameterDto
        {
            public string Key { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorParameterKind Kind { get; set; }
            public bool BoolValue { get; set; }
            public int IntValue { get; set; }
            public float FloatValue { get; set; }

            public static ParameterDto FromValue(
                string key,
                CharacterTestRuntimeAiPlannerBehaviorParameterValue value)
            {
                return new ParameterDto
                {
                    Key = key,
                    Kind = value.Kind,
                    BoolValue = value.BoolValue,
                    IntValue = value.IntValue,
                    FloatValue = value.FloatValue
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorParameterValue ToValue()
            {
                switch (Kind)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Bool(BoolValue);
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Int(IntValue);
                    default:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Float(FloatValue);
                }
            }
        }
    }
}
