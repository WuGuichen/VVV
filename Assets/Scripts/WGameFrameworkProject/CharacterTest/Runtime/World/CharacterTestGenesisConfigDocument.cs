using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestGenesisConfigDocument
    {
        public string Schema { get; set; } = CharacterTestGenesisConfigDefaults.Schema;

        public int SchemaVersion { get; set; } = CharacterTestGenesisConfigDefaults.SchemaVersion;

        public CharacterTestGenesisPotencyProfileConfig[] PotencyProfiles { get; set; } =
            Array.Empty<CharacterTestGenesisPotencyProfileConfig>();

        public CharacterTestGenesisLivingNaturalAttributesProfileConfig[] LivingNaturalAttributesProfiles { get; set; } =
            Array.Empty<CharacterTestGenesisLivingNaturalAttributesProfileConfig>();

        public CharacterTestGenesisMaterialFeedbackMappingConfig[] FeedbackMappings { get; set; } =
            Array.Empty<CharacterTestGenesisMaterialFeedbackMappingConfig>();
    }

    internal sealed class CharacterTestGenesisPotencyProfileConfig
    {
        public string StableId { get; set; } = string.Empty;

        public CharacterTestGenesisPotencyTripletConfig BasePotency { get; set; } =
            new CharacterTestGenesisPotencyTripletConfig();

        public CharacterTestGenesisPotencyImmunityConfig Immunity { get; set; } =
            new CharacterTestGenesisPotencyImmunityConfig();

        public CharacterTestGenesisPotencyResultKindConfig ResultKinds { get; set; } =
            new CharacterTestGenesisPotencyResultKindConfig();

        public CharacterTestGenesisPotencyThresholdConfig Thresholds { get; set; } =
            CharacterTestGenesisPotencyThresholdConfig.CreateDefault();
    }

    internal sealed class CharacterTestGenesisPotencyTripletConfig
    {
        public int D { get; set; }

        public int P { get; set; }

        public int S { get; set; }

        public CharacterTestGenesisPotencyTriplet ToRuntime()
        {
            return new CharacterTestGenesisPotencyTriplet(D, P, S);
        }

        public static CharacterTestGenesisPotencyTripletConfig FromRuntime(
            CharacterTestGenesisPotencyTriplet value)
        {
            return new CharacterTestGenesisPotencyTripletConfig
            {
                D = value.Motion,
                P = value.Cohesion,
                S = value.Scatter
            };
        }
    }

    internal sealed class CharacterTestGenesisPotencyImmunityConfig
    {
        public bool ImmuneD { get; set; }

        public bool ImmuneP { get; set; }

        public bool ImmuneS { get; set; }
    }

    internal sealed class CharacterTestGenesisPotencyResultKindConfig
    {
        public bool Burning { get; set; }

        public bool CrushedOrBroken { get; set; }

        public bool Cracked { get; set; }

        public bool CondensedLayer { get; set; }
    }

    internal sealed class CharacterTestGenesisPotencyThresholdConfig
    {
        public int BurningMinD { get; set; }

        public int BurningMinS { get; set; }

        public int BurningMaxP { get; set; }

        public int CondensedLayerMinP { get; set; }

        public int CondensedLayerMaxD { get; set; }

        public static CharacterTestGenesisPotencyThresholdConfig CreateDefault()
        {
            return new CharacterTestGenesisPotencyThresholdConfig
            {
                BurningMinD = CharacterTestGenesisPotencyProfile.DefaultBurningMinMotion,
                BurningMinS = CharacterTestGenesisPotencyProfile.DefaultBurningMinScatter,
                BurningMaxP = CharacterTestGenesisPotencyProfile.DefaultBurningMaxCohesion,
                CondensedLayerMinP = CharacterTestGenesisPotencyProfile.DefaultCondensedLayerMinCohesion,
                CondensedLayerMaxD = CharacterTestGenesisPotencyProfile.DefaultCondensedLayerMaxMotion
            };
        }

        public CharacterTestGenesisPotencyProfile ToRuntimeProfile(
            CharacterTestGenesisPotencyImmunityConfig immunity,
            CharacterTestGenesisPotencyResultKindConfig resultKinds)
        {
            return new CharacterTestGenesisPotencyProfile(
                immunity != null && immunity.ImmuneD,
                immunity != null && immunity.ImmuneP,
                immunity != null && immunity.ImmuneS,
                resultKinds != null && resultKinds.Burning,
                resultKinds != null && resultKinds.CrushedOrBroken,
                resultKinds != null && resultKinds.Cracked,
                resultKinds != null && resultKinds.CondensedLayer,
                BurningMinD,
                BurningMinS,
                BurningMaxP,
                CondensedLayerMinP,
                CondensedLayerMaxD);
        }
    }

    internal sealed class CharacterTestGenesisLivingNaturalAttributesProfileConfig
    {
        public string StableId { get; set; } = string.Empty;

        public bool BodyBoundaryPresent { get; set; } = true;

        public int BodyScaleMillimeters { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultBodyScaleMillimeters;

        public int MassGrams { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultMassGrams;

        public int ContactSurfaceCount { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultContactSurfaceCount;

        public int NearFieldContactCount { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultNearFieldContactCount;

        public int SelfCohesion { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultSelfCohesion;

        public int CohesionLimit { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultCohesionLimit;

        public int DefaultExternalPressure { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultExternalPressure;

        public int DefaultRecoveryAmount { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultRecoveryAmount;

        public int DefaultOverlimitPressure { get; set; } =
            CharacterTestLivingNaturalAttributesRuntimeState.DefaultOverlimitPressure;
    }

    internal sealed class CharacterTestGenesisMaterialFeedbackMappingConfig
    {
        public CharacterTestGenesisPotencyResultKind ResultKind { get; set; }

        public string DebugLabel { get; set; } = string.Empty;

        public string Stage { get; set; } = string.Empty;

        public string NextContactModifier { get; set; } = string.Empty;

        public string PlaceholderChannel { get; set; } = string.Empty;

        public bool DebugOnly { get; set; } = true;

        public string[] ReadableFields { get; set; } = Array.Empty<string>();
    }

    internal enum CharacterTestGenesisConfigIssueSeverity
    {
        Error = 0,
        Warning = 1
    }

    internal readonly struct CharacterTestGenesisConfigValidationIssue
    {
        public CharacterTestGenesisConfigValidationIssue(
            CharacterTestGenesisConfigIssueSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = string.IsNullOrWhiteSpace(code) ? "unknown" : code.Trim();
            Path = string.IsNullOrWhiteSpace(path) ? "$" : path.Trim();
            Message = string.IsNullOrWhiteSpace(message) ? Code : message.Trim();
        }

        public CharacterTestGenesisConfigIssueSeverity Severity { get; }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Severity
                + " "
                + Code
                + " "
                + Path
                + " "
                + Message;
        }
    }

    internal sealed class CharacterTestGenesisConfigValidationReport
    {
        private readonly List<CharacterTestGenesisConfigValidationIssue> _issues =
            new List<CharacterTestGenesisConfigValidationIssue>();

        public IReadOnlyList<CharacterTestGenesisConfigValidationIssue> Issues => _issues;

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public bool Success => ErrorCount == 0;

        public void AddError(string code, string path, string message)
        {
            _issues.Add(new CharacterTestGenesisConfigValidationIssue(
                CharacterTestGenesisConfigIssueSeverity.Error,
                code,
                path,
                message));
            ErrorCount++;
        }

        public void AddWarning(string code, string path, string message)
        {
            _issues.Add(new CharacterTestGenesisConfigValidationIssue(
                CharacterTestGenesisConfigIssueSeverity.Warning,
                code,
                path,
                message));
            WarningCount++;
        }

        public bool ContainsCode(string code)
        {
            for (int i = 0; i < _issues.Count; i++)
            {
                if (string.Equals(_issues[i].Code, code, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "success={0} errors={1} warnings={2} issues={3}",
                Success ? "true" : "false",
                ErrorCount,
                WarningCount,
                _issues.Count);
        }
    }

    internal static class CharacterTestGenesisConfigValidator
    {
        public static CharacterTestGenesisConfigValidationReport Validate(
            CharacterTestGenesisConfigDocument document)
        {
            var report = new CharacterTestGenesisConfigValidationReport();
            if (document == null)
            {
                report.AddError("document-null", "$", "Genesis config document is null.");
                return report;
            }

            if (!string.Equals(document.Schema, CharacterTestGenesisConfigDefaults.Schema, StringComparison.Ordinal))
            {
                report.AddError(
                    "schema-mismatch",
                    "$.schema",
                    "Unsupported Genesis config schema: " + (document.Schema ?? string.Empty));
            }

            if (document.SchemaVersion != CharacterTestGenesisConfigDefaults.SchemaVersion)
            {
                report.AddError(
                    "schema-version-mismatch",
                    "$.schemaVersion",
                    "Unsupported Genesis config schema version: "
                    + document.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            }

            ValidatePotencyProfiles(report, document.PotencyProfiles);
            ValidateLivingProfiles(report, document.LivingNaturalAttributesProfiles);
            ValidateFeedbackMappings(report, document.FeedbackMappings);
            return report;
        }

        private static void ValidatePotencyProfiles(
            CharacterTestGenesisConfigValidationReport report,
            IReadOnlyList<CharacterTestGenesisPotencyProfileConfig> profiles)
        {
            if (profiles == null || profiles.Count == 0)
            {
                report.AddError(
                    "potency-profiles-empty",
                    "$.potencyProfiles",
                    "At least one potency profile is required.");
                return;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Count; i++)
            {
                string path = "$.potencyProfiles[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                CharacterTestGenesisPotencyProfileConfig profile = profiles[i];
                if (profile == null)
                {
                    report.AddError("potency-profile-null", path, "Potency profile entry is null.");
                    continue;
                }

                ValidateStableId(report, profile.StableId, path + ".stableId");
                if (!string.IsNullOrWhiteSpace(profile.StableId) && !stableIds.Add(profile.StableId.Trim()))
                    report.AddError("potency-profile-duplicate", path + ".stableId", "Duplicate potency profile id.");

                ValidateTriplet(report, profile.BasePotency, path + ".basePotency");
                if (profile.Immunity == null)
                    report.AddError("potency-immunity-null", path + ".immunity", "Potency immunity config is required.");
                if (profile.ResultKinds == null)
                    report.AddError("potency-result-kinds-null", path + ".resultKinds", "Potency ResultKind config is required.");
                if (profile.Thresholds == null)
                {
                    report.AddError("potency-thresholds-null", path + ".thresholds", "Potency thresholds are required.");
                    continue;
                }

                ValidateStateValue(report, profile.Thresholds.BurningMinD, path + ".thresholds.burningMinD");
                ValidateStateValue(report, profile.Thresholds.BurningMinS, path + ".thresholds.burningMinS");
                ValidateStateValue(report, profile.Thresholds.BurningMaxP, path + ".thresholds.burningMaxP");
                ValidateStateValue(report, profile.Thresholds.CondensedLayerMinP, path + ".thresholds.condensedLayerMinP");
                ValidateStateValue(report, profile.Thresholds.CondensedLayerMaxD, path + ".thresholds.condensedLayerMaxD");

                if (profile.ResultKinds != null && !HasAnyResultKind(profile.ResultKinds))
                {
                    report.AddWarning(
                        "potency-result-kinds-empty",
                        path + ".resultKinds",
                        "Profile has no enabled ResultKind; it will only accumulate D/P/S state.");
                }
            }
        }

        private static void ValidateLivingProfiles(
            CharacterTestGenesisConfigValidationReport report,
            IReadOnlyList<CharacterTestGenesisLivingNaturalAttributesProfileConfig> profiles)
        {
            if (profiles == null || profiles.Count == 0)
            {
                report.AddError(
                    "living-profiles-empty",
                    "$.livingNaturalAttributesProfiles",
                    "At least one living natural attributes profile is required.");
                return;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Count; i++)
            {
                string path = "$.livingNaturalAttributesProfiles[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                CharacterTestGenesisLivingNaturalAttributesProfileConfig profile = profiles[i];
                if (profile == null)
                {
                    report.AddError("living-profile-null", path, "Living natural attributes profile entry is null.");
                    continue;
                }

                ValidateStableId(report, profile.StableId, path + ".stableId");
                if (!string.IsNullOrWhiteSpace(profile.StableId) && !stableIds.Add(profile.StableId.Trim()))
                    report.AddError("living-profile-duplicate", path + ".stableId", "Duplicate living profile id.");

                ValidatePositive(report, profile.BodyScaleMillimeters, path + ".bodyScaleMillimeters");
                ValidatePositive(report, profile.MassGrams, path + ".massGrams");
                ValidatePositive(report, profile.ContactSurfaceCount, path + ".contactSurfaceCount");
                ValidatePositive(report, profile.NearFieldContactCount, path + ".nearFieldContactCount");
                ValidateNonNegative(report, profile.SelfCohesion, path + ".selfCohesion");
                ValidatePositive(report, profile.CohesionLimit, path + ".cohesionLimit");
                ValidateCommandMagnitude(report, profile.DefaultExternalPressure, path + ".defaultExternalPressure");
                ValidateCommandMagnitude(report, profile.DefaultRecoveryAmount, path + ".defaultRecoveryAmount");
                ValidateCommandMagnitude(report, profile.DefaultOverlimitPressure, path + ".defaultOverlimitPressure");

                if (!profile.BodyBoundaryPresent)
                {
                    report.AddWarning(
                        "living-body-boundary-disabled",
                        path + ".bodyBoundaryPresent",
                        "Profile starts without a body boundary and cannot satisfy 能成身.");
                }
            }
        }

        private static void ValidateFeedbackMappings(
            CharacterTestGenesisConfigValidationReport report,
            IReadOnlyList<CharacterTestGenesisMaterialFeedbackMappingConfig> mappings)
        {
            if (mappings == null)
                return;

            var resultKinds = new HashSet<CharacterTestGenesisPotencyResultKind>();
            for (int i = 0; i < mappings.Count; i++)
            {
                string path = "$.feedbackMappings[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                CharacterTestGenesisMaterialFeedbackMappingConfig mapping = mappings[i];
                if (mapping == null)
                {
                    report.AddError("feedback-mapping-null", path, "Feedback mapping entry is null.");
                    continue;
                }

                if (!IsFeedbackResultKind(mapping.ResultKind))
                {
                    report.AddError(
                        "feedback-result-kind-invalid",
                        path + ".resultKind",
                        "Feedback mapping requires a material transformation ResultKind.");
                }
                else if (!resultKinds.Add(mapping.ResultKind))
                {
                    report.AddError(
                        "feedback-result-kind-duplicate",
                        path + ".resultKind",
                        "Duplicate feedback mapping ResultKind.");
                }

                ValidateRequiredString(report, mapping.DebugLabel, path + ".debugLabel", "Feedback debug label is required.");
                ValidateRequiredString(report, mapping.Stage, path + ".stage", "Feedback stage is required.");
                ValidateRequiredString(
                    report,
                    mapping.NextContactModifier,
                    path + ".nextContactModifier",
                    "Feedback next-contact modifier is required.");
                ValidateRequiredString(
                    report,
                    mapping.PlaceholderChannel,
                    path + ".placeholderChannel",
                    "Feedback placeholder channel is required.");

                if (!mapping.DebugOnly)
                {
                    report.AddError(
                        "feedback-not-debug-only",
                        path + ".debugOnly",
                        "Feedback mapping must stay debug-only until ArtSource or visual strategy exists.");
                }

                if (mapping.ReadableFields == null || mapping.ReadableFields.Length == 0)
                {
                    report.AddError(
                        "feedback-readable-fields-empty",
                        path + ".readableFields",
                        "Feedback mapping requires at least one readable field.");
                }
            }
        }

        private static bool HasAnyResultKind(CharacterTestGenesisPotencyResultKindConfig resultKinds)
        {
            return resultKinds.Burning
                || resultKinds.CrushedOrBroken
                || resultKinds.Cracked
                || resultKinds.CondensedLayer;
        }

        private static bool IsFeedbackResultKind(CharacterTestGenesisPotencyResultKind resultKind)
        {
            return resultKind == CharacterTestGenesisPotencyResultKind.Burning
                || resultKind == CharacterTestGenesisPotencyResultKind.CrushedOrBroken
                || resultKind == CharacterTestGenesisPotencyResultKind.Cracked
                || resultKind == CharacterTestGenesisPotencyResultKind.CondensedLayer;
        }

        private static void ValidateTriplet(
            CharacterTestGenesisConfigValidationReport report,
            CharacterTestGenesisPotencyTripletConfig triplet,
            string path)
        {
            if (triplet == null)
            {
                report.AddError("potency-triplet-null", path, "D/P/S triplet is required.");
                return;
            }

            ValidateStateValue(report, triplet.D, path + ".d");
            ValidateStateValue(report, triplet.P, path + ".p");
            ValidateStateValue(report, triplet.S, path + ".s");
        }

        private static void ValidateStableId(
            CharacterTestGenesisConfigValidationReport report,
            string value,
            string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddError("stable-id-empty", path, "Stable id is required.");
                return;
            }

            string stableId = value.Trim();
            for (int i = 0; i < stableId.Length; i++)
            {
                char c = stableId[i];
                bool valid =
                    (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-';
                if (valid)
                    continue;

                report.AddError(
                    "stable-id-invalid",
                    path,
                    "Stable id may only contain lowercase letters, digits, '.', '_' and '-'.");
                return;
            }
        }

        private static void ValidateRequiredString(
            CharacterTestGenesisConfigValidationReport report,
            string value,
            string path,
            string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                report.AddError("required-string-empty", path, message);
        }

        private static void ValidateStateValue(
            CharacterTestGenesisConfigValidationReport report,
            int value,
            string path)
        {
            if (value < CharacterTestGenesisPotencyRuntime.MinStateValue
                || value > CharacterTestGenesisPotencyRuntime.MaxStateValue)
            {
                report.AddError(
                    "potency-state-out-of-range",
                    path,
                    "Potency state value must be in the 0..1000 range.");
            }
        }

        private static void ValidatePositive(
            CharacterTestGenesisConfigValidationReport report,
            int value,
            string path)
        {
            if (value <= 0)
                report.AddError("value-not-positive", path, "Value must be positive.");
        }

        private static void ValidateNonNegative(
            CharacterTestGenesisConfigValidationReport report,
            int value,
            string path)
        {
            if (value < 0)
                report.AddError("value-negative", path, "Value must be non-negative.");
        }

        private static void ValidateCommandMagnitude(
            CharacterTestGenesisConfigValidationReport report,
            int value,
            string path)
        {
            if (!CharacterTestLivingNaturalAttributesRuntimeState.IsValidCommandMagnitude(value))
            {
                report.AddError(
                    "command-magnitude-out-of-range",
                    path,
                    "Living natural attributes command magnitude is outside the accepted range.");
            }
        }
    }

    internal static class CharacterTestGenesisConfigJson
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static bool TryLoad(
            string json,
            out CharacterTestGenesisConfigDocument document,
            out CharacterTestGenesisConfigValidationReport report)
        {
            document = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                report = new CharacterTestGenesisConfigValidationReport();
                report.AddError("json-empty", "$", "Genesis config JSON is empty.");
                return false;
            }

            try
            {
                document = JsonConvert.DeserializeObject<CharacterTestGenesisConfigDocument>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                report = new CharacterTestGenesisConfigValidationReport();
                report.AddError("json-parse-failed", "$", "Genesis config JSON parse failed: " + exception.Message);
                return false;
            }

            report = CharacterTestGenesisConfigValidator.Validate(document);
            return report.Success;
        }

        public static string ToJson(CharacterTestGenesisConfigDocument document)
        {
            return JsonConvert.SerializeObject(document, JsonSettings);
        }
    }

    internal static class CharacterTestGenesisConfigDefaults
    {
        public const string Schema = "mx.character-test.genesis.config.v1";
        public const int SchemaVersion = 1;
        public const string GrassBurningCrushProfileId = "genesis.profile.grass_burning_crush";
        public const string StoneCrackProfileId = "genesis.profile.stone_crack";
        public const string CondensedLayerProfileId = "genesis.profile.condensed_layer";
        public const string ClampBurningCrushProfileId = "genesis.profile.clamp_burning_crush";
        public const string LivingDefaultProfileId = "genesis.living.default";

        public static CharacterTestGenesisConfigDocument CreateDefaultDocument()
        {
            return new CharacterTestGenesisConfigDocument
            {
                Schema = Schema,
                SchemaVersion = SchemaVersion,
                PotencyProfiles = new[]
                {
                    CreatePotencyProfile(
                        GrassBurningCrushProfileId,
                        new CharacterTestGenesisPotencyTriplet(0, 20, 0),
                        immuneD: false,
                        immuneP: false,
                        immuneS: false,
                        burning: true,
                        crushedOrBroken: true,
                        cracked: false,
                        condensedLayer: false),
                    CreatePotencyProfile(
                        StoneCrackProfileId,
                        new CharacterTestGenesisPotencyTriplet(0, 90, 0),
                        immuneD: false,
                        immuneP: false,
                        immuneS: true,
                        burning: false,
                        crushedOrBroken: false,
                        cracked: true,
                        condensedLayer: false),
                    CreatePotencyProfile(
                        CondensedLayerProfileId,
                        new CharacterTestGenesisPotencyTriplet(4, 18, 2),
                        immuneD: false,
                        immuneP: false,
                        immuneS: false,
                        burning: false,
                        crushedOrBroken: false,
                        cracked: false,
                        condensedLayer: true),
                    CreatePotencyProfile(
                        ClampBurningCrushProfileId,
                        new CharacterTestGenesisPotencyTriplet(995, 1, 995),
                        immuneD: false,
                        immuneP: false,
                        immuneS: false,
                        burning: true,
                        crushedOrBroken: true,
                        cracked: false,
                        condensedLayer: false)
                },
                LivingNaturalAttributesProfiles = new[]
                {
                    CreateDefaultLivingProfile()
                },
                FeedbackMappings = new[]
                {
                    CreateFeedbackMapping(
                        CharacterTestGenesisPotencyResultKind.Burning,
                        "Burning",
                        "threshold-met",
                        "support-and-structure-weakened",
                        "debug-linear-intensity",
                        "resultKind",
                        "beforeDPS",
                        "afterDPS",
                        "appliedDelta",
                        "ignoredDelta",
                        "burningThresholdMet",
                        "nextContactModifier"),
                    CreateFeedbackMapping(
                        CharacterTestGenesisPotencyResultKind.CondensedLayer,
                        "Condensed Layer",
                        "next-contact-active",
                        "friction-readable-layer",
                        "debug-layer-strength",
                        "resultKind",
                        "layerSource",
                        "frictionBefore",
                        "frictionAfter",
                        "slideDistanceDelta",
                        "velocityDecayDelta"),
                    CreateFeedbackMapping(
                        CharacterTestGenesisPotencyResultKind.Cracked,
                        "Cracked",
                        "threshold-met",
                        "structure-weakened",
                        "debug-crack-marker",
                        "resultKind",
                        "beforeDPS",
                        "afterDPS",
                        "appliedDelta",
                        "ignoredDelta",
                        "crackThresholdMet",
                        "nextContactModifier")
                }
            };
        }

        public static CharacterTestLivingNaturalAttributesRuntimeState CreateDefaultLivingNaturalAttributesState(
            MxFramework.Runtime.RuntimeFrame frame = default)
        {
            CharacterTestGenesisLivingNaturalAttributesProfileConfig config = CreateDefaultLivingProfile();
            return CharacterTestLivingNaturalAttributesRuntimeState.FromSnapshot(
                CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.None,
                config.BodyBoundaryPresent,
                config.BodyScaleMillimeters,
                config.MassGrams,
                config.ContactSurfaceCount,
                config.NearFieldContactCount,
                config.SelfCohesion,
                config.CohesionLimit,
                externalPressure: 0,
                scatter: 0,
                recoveryTicks: 0,
                recoveryAmount: 0,
                frame.Value,
                frame.Value);
        }

        private static CharacterTestGenesisPotencyProfileConfig CreatePotencyProfile(
            string stableId,
            CharacterTestGenesisPotencyTriplet basePotency,
            bool immuneD,
            bool immuneP,
            bool immuneS,
            bool burning,
            bool crushedOrBroken,
            bool cracked,
            bool condensedLayer)
        {
            return new CharacterTestGenesisPotencyProfileConfig
            {
                StableId = stableId,
                BasePotency = CharacterTestGenesisPotencyTripletConfig.FromRuntime(basePotency),
                Immunity = new CharacterTestGenesisPotencyImmunityConfig
                {
                    ImmuneD = immuneD,
                    ImmuneP = immuneP,
                    ImmuneS = immuneS
                },
                ResultKinds = new CharacterTestGenesisPotencyResultKindConfig
                {
                    Burning = burning,
                    CrushedOrBroken = crushedOrBroken,
                    Cracked = cracked,
                    CondensedLayer = condensedLayer
                },
                Thresholds = CharacterTestGenesisPotencyThresholdConfig.CreateDefault()
            };
        }

        private static CharacterTestGenesisMaterialFeedbackMappingConfig CreateFeedbackMapping(
            CharacterTestGenesisPotencyResultKind resultKind,
            string debugLabel,
            string stage,
            string nextContactModifier,
            string placeholderChannel,
            params string[] readableFields)
        {
            return new CharacterTestGenesisMaterialFeedbackMappingConfig
            {
                ResultKind = resultKind,
                DebugLabel = debugLabel,
                Stage = stage,
                NextContactModifier = nextContactModifier,
                PlaceholderChannel = placeholderChannel,
                DebugOnly = true,
                ReadableFields = readableFields ?? Array.Empty<string>()
            };
        }

        private static CharacterTestGenesisLivingNaturalAttributesProfileConfig CreateDefaultLivingProfile()
        {
            return new CharacterTestGenesisLivingNaturalAttributesProfileConfig
            {
                StableId = LivingDefaultProfileId
            };
        }
    }

    internal sealed class CharacterTestGenesisConfigRuntimeAdapter
    {
        private readonly Dictionary<string, CharacterTestGenesisPotencyProfileConfig> _potencyProfiles;
        private readonly Dictionary<string, CharacterTestGenesisLivingNaturalAttributesProfileConfig> _livingProfiles;
        private readonly Dictionary<CharacterTestGenesisPotencyResultKind, CharacterTestGenesisMaterialFeedbackMappingConfig>
            _feedbackMappings;

        private CharacterTestGenesisConfigRuntimeAdapter(CharacterTestGenesisConfigDocument document)
        {
            _potencyProfiles = new Dictionary<string, CharacterTestGenesisPotencyProfileConfig>(StringComparer.Ordinal);
            CharacterTestGenesisPotencyProfileConfig[] potencyProfiles =
                document.PotencyProfiles ?? Array.Empty<CharacterTestGenesisPotencyProfileConfig>();
            for (int i = 0; i < potencyProfiles.Length; i++)
            {
                CharacterTestGenesisPotencyProfileConfig profile = potencyProfiles[i];
                if (profile != null && !string.IsNullOrWhiteSpace(profile.StableId))
                    _potencyProfiles[profile.StableId.Trim()] = profile;
            }

            _livingProfiles =
                new Dictionary<string, CharacterTestGenesisLivingNaturalAttributesProfileConfig>(StringComparer.Ordinal);
            CharacterTestGenesisLivingNaturalAttributesProfileConfig[] livingProfiles =
                document.LivingNaturalAttributesProfiles
                ?? Array.Empty<CharacterTestGenesisLivingNaturalAttributesProfileConfig>();
            for (int i = 0; i < livingProfiles.Length; i++)
            {
                CharacterTestGenesisLivingNaturalAttributesProfileConfig profile = livingProfiles[i];
                if (profile != null && !string.IsNullOrWhiteSpace(profile.StableId))
                    _livingProfiles[profile.StableId.Trim()] = profile;
            }

            _feedbackMappings =
                new Dictionary<CharacterTestGenesisPotencyResultKind, CharacterTestGenesisMaterialFeedbackMappingConfig>();
            CharacterTestGenesisMaterialFeedbackMappingConfig[] feedbackMappings =
                document.FeedbackMappings ?? Array.Empty<CharacterTestGenesisMaterialFeedbackMappingConfig>();
            for (int i = 0; i < feedbackMappings.Length; i++)
            {
                CharacterTestGenesisMaterialFeedbackMappingConfig mapping = feedbackMappings[i];
                if (mapping != null && mapping.ResultKind != CharacterTestGenesisPotencyResultKind.None)
                    _feedbackMappings[mapping.ResultKind] = mapping;
            }
        }

        public static bool TryCreate(
            CharacterTestGenesisConfigDocument document,
            out CharacterTestGenesisConfigRuntimeAdapter adapter,
            out CharacterTestGenesisConfigValidationReport report)
        {
            adapter = null;
            report = CharacterTestGenesisConfigValidator.Validate(document);
            if (!report.Success)
                return false;

            adapter = new CharacterTestGenesisConfigRuntimeAdapter(document);
            return true;
        }

        public bool TryCreatePotencyFacet(
            string stableId,
            out CharacterTestWorldObjectPotencyFacet potency)
        {
            potency = CharacterTestWorldObjectPotencyFacet.Disabled;
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            if (!_potencyProfiles.TryGetValue(stableId.Trim(), out CharacterTestGenesisPotencyProfileConfig config))
                return false;

            CharacterTestGenesisPotencyProfile profile =
                config.Thresholds.ToRuntimeProfile(config.Immunity, config.ResultKinds);
            potency = new CharacterTestWorldObjectPotencyFacet(config.BasePotency.ToRuntime(), profile);
            return true;
        }

        public bool TryCreateLivingNaturalAttributesState(
            string stableId,
            MxFramework.Runtime.RuntimeFrame frame,
            out CharacterTestLivingNaturalAttributesRuntimeState state)
        {
            state = CharacterTestLivingNaturalAttributesRuntimeState.CreateDefault(frame);
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            if (!_livingProfiles.TryGetValue(stableId.Trim(), out CharacterTestGenesisLivingNaturalAttributesProfileConfig config))
                return false;

            state = CharacterTestLivingNaturalAttributesRuntimeState.FromSnapshot(
                CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.None,
                config.BodyBoundaryPresent,
                config.BodyScaleMillimeters,
                config.MassGrams,
                config.ContactSurfaceCount,
                config.NearFieldContactCount,
                config.SelfCohesion,
                config.CohesionLimit,
                externalPressure: 0,
                scatter: 0,
                recoveryTicks: 0,
                recoveryAmount: 0,
                frame.Value,
                frame.Value);
            return true;
        }

        public bool TryResolveLivingOperationMagnitude(
            string stableId,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            out int magnitude)
        {
            magnitude = 0;
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            if (!_livingProfiles.TryGetValue(stableId.Trim(), out CharacterTestGenesisLivingNaturalAttributesProfileConfig config))
                return false;

            switch (operation)
            {
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure:
                    magnitude = config.DefaultExternalPressure;
                    return true;
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover:
                    magnitude = config.DefaultRecoveryAmount;
                    return true;
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate:
                    magnitude = config.DefaultOverlimitPressure;
                    return true;
                default:
                    magnitude = 0;
                    return true;
            }
        }

        public bool TryGetFeedbackMapping(
            CharacterTestGenesisPotencyResultKind resultKind,
            out CharacterTestGenesisMaterialFeedbackMappingConfig mapping)
        {
            return _feedbackMappings.TryGetValue(resultKind, out mapping);
        }
    }
}
