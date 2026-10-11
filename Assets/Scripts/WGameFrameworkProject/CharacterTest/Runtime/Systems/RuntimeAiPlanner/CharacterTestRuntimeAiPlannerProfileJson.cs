using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestRuntimeAiPlannerProfileJson
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.profile.v3";
        public const string LegacySchemaV2 = "mx.character-test.runtime-ai-planner.profile.v2";
        public const string LegacySchemaV1 = "mx.character-test.runtime-ai-planner.profile.v1";
        public const string DefaultProjectAssetPath =
            "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/default.json";
        public const string DefaultStreamingAssetsPath =
            "MxFramework/CharacterTest/runtime_ai_planner/default.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// AllocByDesign: serializes low-frequency CharacterTest Runtime AI Planner authoring data.
        /// </summary>
        public static string ToJson(CharacterTestRuntimeAiPlannerProfile profile)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                CharacterTestRuntimeAiPlannerProfileSource.Normalize(profile);
            return JsonConvert.SerializeObject(ProfileDocument.FromProfile(source), JsonSettings);
        }

        /// <summary>
        /// AllocByDesign: parses and validates low-frequency CharacterTest Runtime AI Planner authoring data.
        /// </summary>
        public static bool TryLoad(
            string json,
            out CharacterTestRuntimeAiPlannerProfile profile,
            out CharacterTestRuntimeAiPlannerValidationReport report)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "json.empty",
                    "Runtime AI Planner profile JSON is empty.");
                return false;
            }

            ProfileDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<ProfileDocument>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "json.parse",
                    "Runtime AI Planner profile JSON parse failed: " + exception.Message);
                return false;
            }

            if (document == null)
            {
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "json.null",
                    "Runtime AI Planner profile JSON payload is null.");
                return false;
            }

            CharacterTestRuntimeAiPlannerValidationReport schemaReport = null;
            if (string.Equals(document.Schema, LegacySchemaV1, StringComparison.Ordinal))
            {
                schemaReport = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning,
                    "json.schema.legacy",
                    "Runtime AI Planner profile JSON uses v1 schema; default perception and movement settings were applied.");
            }
            else if (string.Equals(document.Schema, LegacySchemaV2, StringComparison.Ordinal))
            {
                schemaReport = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning,
                    "json.schema.legacy",
                    "Runtime AI Planner profile JSON uses v2 schema; default movement settings were applied.");
            }
            else if (!string.Equals(document.Schema, Schema, StringComparison.Ordinal))
            {
                string code = string.IsNullOrWhiteSpace(document.Schema)
                    ? "json.schema.missing"
                    : "json.schema.unsupported";
                report = CreateReport(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    code,
                    "Unsupported Runtime AI Planner profile schema: " + (document.Schema ?? string.Empty));
                return false;
            }

            profile = document.ToProfile();
            report = CombineReports(schemaReport, CharacterTestRuntimeAiPlannerProfileValidator.Validate(profile));
            return report.Success;
        }

        public static CharacterTestRuntimeAiPlannerProfileLoadResult LoadFromStreamingAssetsOrDefault(
            string streamingAssetsRelativePath,
            CharacterTestAiBehaviorProfile fallbackLegacyProfile = null)
        {
            CharacterTestRuntimeAiPlannerProfile fallback =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(fallbackLegacyProfile);
            string relativePath = NormalizeStreamingAssetsRelativePath(streamingAssetsRelativePath);
            if (string.IsNullOrWhiteSpace(streamingAssetsRelativePath))
            {
                return CharacterTestRuntimeAiPlannerProfileLoadResult.Fallback(
                    fallback,
                    relativePath,
                    CreateFallbackReport("source.path.empty", "Runtime AI Planner profile path is empty; using default profile."));
            }

            string fullPath = ResolveStreamingAssetsFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                return CharacterTestRuntimeAiPlannerProfileLoadResult.Fallback(
                    fallback,
                    relativePath,
                    CreateFallbackReport(
                        "source.file.missing",
                        "Runtime AI Planner profile JSON file is missing: " + relativePath));
            }

            string json;
            try
            {
                json = File.ReadAllText(fullPath);
            }
            catch (IOException exception)
            {
                return CharacterTestRuntimeAiPlannerProfileLoadResult.Fallback(
                    fallback,
                    relativePath,
                    CreateFallbackReport(
                        "source.file.read",
                        "Runtime AI Planner profile JSON read failed: " + exception.Message));
            }

            if (!TryLoad(json, out CharacterTestRuntimeAiPlannerProfile profile, out CharacterTestRuntimeAiPlannerValidationReport report))
            {
                return CharacterTestRuntimeAiPlannerProfileLoadResult.Fallback(
                    fallback,
                    relativePath,
                    CreateFallbackReport(
                        "source.file.invalid",
                        "Runtime AI Planner profile JSON is invalid; using default profile. " + FirstDiagnosticMessage(report)));
            }

            return CharacterTestRuntimeAiPlannerProfileLoadResult.Loaded(profile, relativePath, report);
        }

        internal static string NormalizeStreamingAssetsRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return DefaultStreamingAssetsPath;

            string normalized = path.Replace('\\', '/').Trim();
            const string prefix = "Assets/StreamingAssets/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(prefix.Length);
            return normalized.TrimStart('/');
        }

        internal static string ResolveStreamingAssetsFullPath(string streamingAssetsRelativePath)
        {
            string relativePath = NormalizeStreamingAssetsRelativePath(streamingAssetsRelativePath);
            return Path.Combine(Application.streamingAssetsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static CharacterTestRuntimeAiPlannerValidationReport CombineReports(
            CharacterTestRuntimeAiPlannerValidationReport first,
            CharacterTestRuntimeAiPlannerValidationReport second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>(
                first.Diagnostics.Count + second.Diagnostics.Count);
            for (int i = 0; i < first.Diagnostics.Count; i++)
                diagnostics.Add(first.Diagnostics[i]);
            for (int i = 0; i < second.Diagnostics.Count; i++)
                diagnostics.Add(second.Diagnostics[i]);
            return new CharacterTestRuntimeAiPlannerValidationReport(diagnostics);
        }

        private static CharacterTestRuntimeAiPlannerValidationReport CreateFallbackReport(
            string code,
            string message)
        {
            return CreateReport(
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning,
                code,
                message);
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

        private static string FirstDiagnosticMessage(CharacterTestRuntimeAiPlannerValidationReport report)
        {
            if (report == null || report.Diagnostics.Count == 0)
                return string.Empty;

            return report.Diagnostics[0].Message;
        }

        private sealed class ProfileDocument
        {
            public string Schema { get; set; }
            public string ProfileId { get; set; }
            public int InitialStateId { get; set; }
            public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; set; }
            public float AttackRange { get; set; }
            public PerceptionDto Perception { get; set; }
            public MovementDto Movement { get; set; }
            public StateDto[] States { get; set; }
            public TransitionDto[] Transitions { get; set; }
            public ActionDto[] Actions { get; set; }

            public static ProfileDocument FromProfile(CharacterTestRuntimeAiPlannerProfile profile)
            {
                var document = new ProfileDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerProfileJson.Schema,
                    ProfileId = profile.ProfileId,
                    InitialStateId = profile.InitialStateId,
                    TargetSelectionMode = profile.TargetSelectionMode,
                    AttackRange = profile.AttackRange,
                    Perception = PerceptionDto.FromSettings(profile.PerceptionSettings),
                    Movement = MovementDto.FromSettings(profile.MovementSettings),
                    States = new StateDto[profile.States.Count],
                    Transitions = new TransitionDto[profile.Transitions.Count],
                    Actions = new ActionDto[profile.Actions.Count]
                };

                for (int i = 0; i < profile.States.Count; i++)
                    document.States[i] = StateDto.FromDefinition(profile.States[i]);
                for (int i = 0; i < profile.Transitions.Count; i++)
                    document.Transitions[i] = TransitionDto.FromDefinition(profile.Transitions[i]);
                for (int i = 0; i < profile.Actions.Count; i++)
                    document.Actions[i] = ActionDto.FromDefinition(profile.Actions[i]);
                return document;
            }

            public CharacterTestRuntimeAiPlannerProfile ToProfile()
            {
                return new CharacterTestRuntimeAiPlannerProfile(
                    ProfileId,
                    InitialStateId,
                    TargetSelectionMode,
                    AttackRange,
                    CopyStates(),
                    CopyTransitions(),
                    CopyActions(),
                    Perception != null
                        ? Perception.ToSettings()
                        : CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                    Movement != null
                        ? Movement.ToSettings()
                        : CharacterTestRuntimeAiPlannerMovementSettings.CreateDefault());
            }

            private CharacterTestRuntimeAiPlannerStateDefinition[] CopyStates()
            {
                if (States == null || States.Length == 0)
                    return Array.Empty<CharacterTestRuntimeAiPlannerStateDefinition>();

                var states = new CharacterTestRuntimeAiPlannerStateDefinition[States.Length];
                for (int i = 0; i < States.Length; i++)
                    states[i] = (States[i] ?? new StateDto()).ToDefinition();
                return states;
            }

            private CharacterTestRuntimeAiPlannerTransitionDefinition[] CopyTransitions()
            {
                if (Transitions == null || Transitions.Length == 0)
                    return Array.Empty<CharacterTestRuntimeAiPlannerTransitionDefinition>();

                var transitions = new CharacterTestRuntimeAiPlannerTransitionDefinition[Transitions.Length];
                for (int i = 0; i < Transitions.Length; i++)
                    transitions[i] = (Transitions[i] ?? new TransitionDto()).ToDefinition();
                return transitions;
            }

            private CharacterTestRuntimeAiPlannerActionDefinition[] CopyActions()
            {
                if (Actions == null || Actions.Length == 0)
                    return Array.Empty<CharacterTestRuntimeAiPlannerActionDefinition>();

                var actions = new CharacterTestRuntimeAiPlannerActionDefinition[Actions.Length];
                for (int i = 0; i < Actions.Length; i++)
                    actions[i] = (Actions[i] ?? new ActionDto()).ToDefinition();
                return actions;
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

        private sealed class StateDto
        {
            public int StateId { get; set; }
            public int ParentStateId { get; set; }
            public string DisplayName { get; set; }

            public static StateDto FromDefinition(CharacterTestRuntimeAiPlannerStateDefinition definition)
            {
                return new StateDto
                {
                    StateId = definition != null ? definition.StateId : 0,
                    ParentStateId = definition != null ? definition.ParentStateId : 0,
                    DisplayName = definition != null ? definition.DisplayName : string.Empty
                };
            }

            public CharacterTestRuntimeAiPlannerStateDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerStateDefinition(
                    StateId,
                    ParentStateId,
                    DisplayName);
            }
        }

        private sealed class TransitionDto
        {
            public int SourceStateId { get; set; }
            public int TargetStateId { get; set; }
            public int Priority { get; set; }
            public string DisplayName { get; set; }
            public ConditionDto Condition { get; set; }

            public static TransitionDto FromDefinition(CharacterTestRuntimeAiPlannerTransitionDefinition definition)
            {
                return new TransitionDto
                {
                    SourceStateId = definition != null ? definition.SourceStateId : 0,
                    TargetStateId = definition != null ? definition.TargetStateId : 0,
                    Priority = definition != null ? definition.Priority : 0,
                    DisplayName = definition != null ? definition.DisplayName : string.Empty,
                    Condition = ConditionDto.FromDefinition(definition != null ? definition.Condition : default)
                };
            }

            public CharacterTestRuntimeAiPlannerTransitionDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerTransitionDefinition(
                    SourceStateId,
                    TargetStateId,
                    Priority,
                    Condition != null ? Condition.ToDefinition() : default,
                    DisplayName);
            }
        }

        private sealed class ActionDto
        {
            public int ActionId { get; set; }
            public int StateId { get; set; }
            public CharacterTestRuntimeAiPlannerActionKind Kind { get; set; }
            public float Score { get; set; }
            public float Cost { get; set; }
            public string DisplayName { get; set; }
            public int AbilitySlot { get; set; }
            public int AbilityId { get; set; }
            public int ReactionCooldownFrames { get; set; }
            public MovementDto Movement { get; set; }
            public ConditionDto[] Conditions { get; set; }

            public static ActionDto FromDefinition(CharacterTestRuntimeAiPlannerActionDefinition definition)
            {
                var dto = new ActionDto
                {
                    ActionId = definition != null ? definition.ActionId : 0,
                    StateId = definition != null ? definition.StateId : 0,
                    Kind = definition != null ? definition.Kind : CharacterTestRuntimeAiPlannerActionKind.Wait,
                    Score = definition != null ? definition.Score : 0f,
                    Cost = definition != null ? definition.Cost : 0f,
                    DisplayName = definition != null ? definition.DisplayName : string.Empty,
                    AbilitySlot = definition != null ? definition.AbilitySlot : 0,
                    AbilityId = definition != null ? definition.AbilityId : 0,
                    ReactionCooldownFrames = definition != null ? definition.ReactionCooldownFrames : 0,
                    Movement = definition != null && definition.MovementSettings != null
                        ? MovementDto.FromSettings(definition.MovementSettings)
                        : null,
                    Conditions = definition != null
                        ? new ConditionDto[definition.Conditions.Count]
                        : Array.Empty<ConditionDto>()
                };
                if (definition == null)
                    return dto;

                for (int i = 0; i < definition.Conditions.Count; i++)
                    dto.Conditions[i] = ConditionDto.FromDefinition(definition.Conditions[i]);
                return dto;
            }

            public CharacterTestRuntimeAiPlannerActionDefinition ToDefinition()
            {
                return new CharacterTestRuntimeAiPlannerActionDefinition(
                    ActionId,
                    StateId,
                    Kind,
                    Score,
                    Cost,
                    DisplayName,
                    AbilitySlot,
                    AbilityId,
                    CopyConditions(),
                    Movement != null ? Movement.ToSettings() : null,
                    ReactionCooldownFrames);
            }

            private CharacterTestRuntimeAiPlannerConditionDefinition[] CopyConditions()
            {
                if (Conditions == null || Conditions.Length == 0)
                    return Array.Empty<CharacterTestRuntimeAiPlannerConditionDefinition>();

                var conditions = new CharacterTestRuntimeAiPlannerConditionDefinition[Conditions.Length];
                for (int i = 0; i < Conditions.Length; i++)
                    conditions[i] = (Conditions[i] ?? new ConditionDto()).ToDefinition();
                return conditions;
            }
        }

        private sealed class ConditionDto
        {
            public CharacterTestRuntimeAiPlannerFactKind Fact { get; set; }
            public CharacterTestRuntimeAiPlannerConditionOperator Operator { get; set; }
            public bool BoolValue { get; set; }
            public float FloatValue { get; set; }

            public static ConditionDto FromDefinition(CharacterTestRuntimeAiPlannerConditionDefinition definition)
            {
                return new ConditionDto
                {
                    Fact = definition.Fact,
                    Operator = definition.Operator,
                    BoolValue = definition.BoolValue,
                    FloatValue = definition.FloatValue
                };
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

    public readonly struct CharacterTestRuntimeAiPlannerProfileLoadResult
    {
        private CharacterTestRuntimeAiPlannerProfileLoadResult(
            CharacterTestRuntimeAiPlannerProfile profile,
            string sourcePath,
            CharacterTestRuntimeAiPlannerValidationReport report,
            bool usedFallback)
        {
            Profile = profile;
            SourcePath = sourcePath ?? string.Empty;
            Report = report;
            UsedFallback = usedFallback;
        }

        public CharacterTestRuntimeAiPlannerProfile Profile { get; }
        public string SourcePath { get; }
        public CharacterTestRuntimeAiPlannerValidationReport Report { get; }
        public bool UsedFallback { get; }

        public static CharacterTestRuntimeAiPlannerProfileLoadResult Loaded(
            CharacterTestRuntimeAiPlannerProfile profile,
            string sourcePath,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return new CharacterTestRuntimeAiPlannerProfileLoadResult(
                profile,
                sourcePath,
                report,
                usedFallback: false);
        }

        public static CharacterTestRuntimeAiPlannerProfileLoadResult Fallback(
            CharacterTestRuntimeAiPlannerProfile profile,
            string sourcePath,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return new CharacterTestRuntimeAiPlannerProfileLoadResult(
                profile,
                sourcePath,
                report,
                usedFallback: true);
        }
    }
}
