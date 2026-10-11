using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerBehaviorCompositionJson
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.composition.v1";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string ToJson(CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile source =
                profile ?? CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            return JsonConvert.SerializeObject(CompositionDocument.FromProfile(source), JsonSettings);
        }

        public static bool TryLoad(
            string json,
            out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            out CharacterTestRuntimeAiPlannerValidationReport report)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "composition.json.empty",
                    "Runtime AI Planner behavior composition JSON is empty.");
                return false;
            }

            CompositionDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CompositionDocument>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "composition.json.parse",
                    "Runtime AI Planner behavior composition JSON parse failed: " + exception.Message);
                return false;
            }

            if (document == null)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "composition.json.null",
                    "Runtime AI Planner behavior composition JSON payload is null.");
                return false;
            }

            if (!string.Equals(document.Schema, Schema, StringComparison.Ordinal))
            {
                if (string.Equals(
                        document.Schema,
                        CharacterTestRuntimeAiPlannerBehaviorGroupJson.Schema,
                        StringComparison.Ordinal))
                {
                    return CharacterTestRuntimeAiPlannerBehaviorGroupJson.TryLoad(
                        json,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn(),
                        out profile,
                        out report);
                }

                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "composition.schema.unsupported",
                    "Runtime AI Planner behavior composition schema is unsupported: " + document.Schema);
                return false;
            }

            profile = document.ToProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile);
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

        private sealed class CompositionDocument
        {
            public string Schema { get; set; }
            public string CompositionId { get; set; }
            public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; set; }
            public float AttackRange { get; set; }
            public PerceptionDto Perception { get; set; }
            public MovementDto Movement { get; set; }
            public ModuleDto[] Modules { get; set; }

            public static CompositionDocument FromProfile(
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
            {
                var document = new CompositionDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerBehaviorCompositionJson.Schema,
                    CompositionId = profile.CompositionId,
                    TargetSelectionMode = profile.TargetSelectionMode,
                    AttackRange = profile.AttackRange,
                    Perception = PerceptionDto.FromSettings(profile.PerceptionSettings),
                    Movement = MovementDto.FromSettings(profile.MovementSettings),
                    Modules = new ModuleDto[profile.Modules.Count]
                };
                for (int i = 0; i < profile.Modules.Count; i++)
                    document.Modules[i] = ModuleDto.FromEntry(profile.Modules[i]);
                return document;
            }

            public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile ToProfile()
            {
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                    CompositionId,
                    TargetSelectionMode,
                    AttackRange,
                    Perception != null
                        ? Perception.ToSettings()
                        : CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                    Movement != null
                        ? Movement.ToSettings()
                        : CharacterTestRuntimeAiPlannerMovementSettings.CreateDefault());
                if (Modules != null)
                {
                    for (int i = 0; i < Modules.Length; i++)
                        profile.Modules.Add((Modules[i] ?? new ModuleDto()).ToEntry());
                }

                return profile;
            }
        }

        private sealed class ModuleDto
        {
            public string ModuleId { get; set; }
            public bool Enabled { get; set; }
            public int Order { get; set; }
            public ParameterDto[] Parameters { get; set; }

            public static ModuleDto FromEntry(
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
            {
                var parameters = new List<ParameterDto>();
                foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in entry.Parameters)
                    parameters.Add(ParameterDto.FromValue(pair.Key, pair.Value));
                return new ModuleDto
                {
                    ModuleId = entry.ModuleId,
                    Enabled = entry.Enabled,
                    Order = entry.Order,
                    Parameters = parameters.ToArray()
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorModuleEntry ToEntry()
            {
                var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(ModuleId, Enabled, Order);
                if (Parameters != null)
                {
                    for (int i = 0; i < Parameters.Length; i++)
                    {
                        ParameterDto parameter = Parameters[i];
                        if (parameter == null)
                            continue;

                        entry.Parameters[parameter.Key ?? string.Empty] = parameter.ToValue();
                    }
                }

                return entry;
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
    }

}
