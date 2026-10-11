using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestLevelGameplayTriggerAuthoringAdapter
    {
        internal const string ModuleId = "characterTest.gameplayTriggers";
        internal const int ModuleSchemaVersion = 2;
        internal const int LegacyModuleSchemaVersion = 1;
        internal const int CombatRegionProviderId = 1;
        internal const int PlayerAttributeThresholdProviderId = 2;
        internal const int BattleVitalsSignalProviderId = 3;

        private static readonly string[] PayloadProperties =
        {
            "format",
            "schemaVersion",
            "idState",
            "authoring",
            "entries",
            "regionBindings"
        };

        private static readonly string[] IdStateProperties =
        {
            "nextSourceDefinitionId",
            "nextConditionId",
            "nextDefinitionId",
            "nextRouteId",
            "nextBindingId",
            "nextSourceBindingId",
            "retiredIds"
        };

        private static readonly string[] RetiredIdProperties =
        {
            "idKind",
            "primaryId",
            "secondaryId"
        };

        private static readonly string[] EntryProperties =
        {
            "stableId",
            "displayName",
            "templateId",
            "notes",
            "sourceDefinitionId",
            "conditionId",
            "definitionId",
            "routeIds",
            "bindingId",
            "sourceBindingId"
        };

        private static readonly string[] RegionBindingProperties =
        {
            "sourceBindingId",
            "regionStableId",
            "providerId",
            "layerMask",
            "maxQueryHits",
            "maxMappedSubjects",
            "subjectFilter"
        };

        private static readonly string[] LegacyRegionBindingProperties =
        {
            "sourceBindingId",
            "regionStableId",
            "providerId",
            "layerMask",
            "maxQueryHits",
            "maxMappedSubjects"
        };

        private static readonly string[] SubjectFilterProperties =
        {
            "acceptedIdentityTypes",
            "requiredCapabilities",
            "requireAlive",
            "relationFilter",
            "referenceTeamId"
        };

        internal static bool TryExtract(
            MxLevelAuthoringDocument envelope,
            out CharacterTestLevelGameplayTriggerAuthoringDocument document,
            out bool modulePresent,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            document = null;
            modulePresent = false;
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (envelope == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_ENVELOPE_MISSING",
                    "$",
                    "Level authoring envelope is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (!envelope.TryGetModule(ModuleId, out MxLevelAuthoringModuleDocument module))
            {
                document =
                    new CharacterTestLevelGameplayTriggerAuthoringDocument();
                report = LevelWorldObjectAuthoringValidationReport.Empty;
                return true;
            }

            modulePresent = true;
            string payloadPath = "$.modules." + ModuleId + ".payload";
            bool legacyModule =
                module.SchemaVersion == LegacyModuleSchemaVersion;
            if (!legacyModule
                && module.SchemaVersion != ModuleSchemaVersion)
            {
                issues.Add(Error(
                    "CT_TRIGGER_MODULE_VERSION_UNSUPPORTED",
                    "$.modules." + ModuleId + ".schemaVersion",
                    "Gameplay Trigger module schema version is not supported."));
            }

            JObject payload = module.Payload;
            if (payload == null
                || !ValidateObject(
                    payload,
                    payloadPath,
                    PayloadProperties,
                    issues)
                || !ValidateRequiredObject(
                    payload?["idState"],
                    payloadPath + ".idState",
                    IdStateProperties,
                    issues)
                || !ValidateRows(
                    payload?["idState"]?["retiredIds"],
                    payloadPath + ".idState.retiredIds",
                    RetiredIdProperties,
                    issues)
                || !ValidateRows(
                    payload?["entries"],
                    payloadPath + ".entries",
                    EntryProperties,
                    issues)
                || !ValidateEntryRouteArrays(
                    payload?["entries"],
                    payloadPath + ".entries",
                    issues)
                || !ValidateRows(
                    payload?["regionBindings"],
                    payloadPath + ".regionBindings",
                    legacyModule
                        ? LegacyRegionBindingProperties
                        : RegionBindingProperties,
                    issues)
                || (!legacyModule
                    && !ValidateSubjectFilterRows(
                        payload?["regionBindings"],
                        payloadPath + ".regionBindings",
                        issues)))
            {
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (payload["authoring"] is not JObject authoringToken)
            {
                issues.Add(Error(
                    "CT_TRIGGER_AUTHORING_INVALID",
                    payloadPath + ".authoring",
                    "Gameplay Trigger authoring must be an object."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (!GameplayTriggerAuthoringJson.TryDeserialize(
                    authoringToken.ToString(Formatting.None),
                    out GameplayTriggerAuthoringDocument authoring,
                    out GameplayTriggerAuthoringJsonDiagnostic diagnostic))
            {
                issues.Add(Error(
                    "CT_TRIGGER_AUTHORING_JSON_" + diagnostic.Code,
                    payloadPath + ".authoring" + TrimRootPath(diagnostic.Path),
                    diagnostic.Message));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            try
            {
                CharacterTestLevelGameplayTriggerAuthoringDocument decoded =
                    module.ReadPayload<
                        CharacterTestLevelGameplayTriggerAuthoringDocument>();
                if (decoded == null)
                    throw new JsonSerializationException("Payload decoded to null.");
                decoded.Authoring = authoring;
                CharacterTestLevelGameplayTriggerAuthoringDocument normalized =
                    decoded.Normalize();
                bool legacyPayload =
                    string.Equals(
                        normalized.Format,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .LegacyV1Format,
                        StringComparison.Ordinal)
                    && normalized.SchemaVersion
                    == CharacterTestLevelGameplayTriggerAuthoringDocument
                        .LegacyV1SchemaVersion;
                bool currentPayload =
                    string.Equals(
                        normalized.Format,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .CurrentFormat,
                        StringComparison.Ordinal)
                    && normalized.SchemaVersion
                    == CharacterTestLevelGameplayTriggerAuthoringDocument
                        .CurrentSchemaVersion;
                if ((legacyModule && !legacyPayload)
                    || (!legacyModule && !currentPayload))
                {
                    throw new JsonSerializationException(
                        "Gameplay Trigger module and payload schema versions do not match.");
                }

                document = legacyModule
                    ? normalized.UpgradeToCurrent()
                    : normalized;
            }
            catch (Exception exception)
                when (exception is JsonException
                      || exception is ArgumentException
                      || exception is InvalidOperationException)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_INVALID",
                    payloadPath,
                    exception.Message));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            report = new LevelWorldObjectAuthoringValidationReport(issues);
            return !report.HasErrors;
        }

        internal static MxLevelAuthoringDocument Upsert(
            MxLevelAuthoringDocument envelope,
            CharacterTestLevelGameplayTriggerAuthoringDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            CharacterTestLevelGameplayTriggerAuthoringDocument normalized =
                document.UpgradeToCurrent();
            envelope ??= new MxLevelAuthoringDocument();
            envelope.UpsertModulePayload(
                ModuleId,
                ModuleSchemaVersion,
                normalized,
                "CharacterTest Gameplay Triggers");
            if (envelope.TryGetModule(ModuleId, out MxLevelAuthoringModuleDocument module))
            {
                module.Payload["authoring"] = JObject.Parse(
                    GameplayTriggerAuthoringJson.Serialize(
                        normalized.Authoring,
                        indented: false));
            }

            return envelope.Normalize();
        }

        internal static LevelWorldObjectAuthoringValidationReport
            ValidateAndCompile(
                CharacterTestLevelGameplayTriggerAuthoringDocument document,
                LevelWorldObjectAuthoringDocument worldObjects,
                out GameplayTriggerCompileResult compileResult)
        {
            compileResult = null;
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (document == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_MISSING",
                    "$.modules." + ModuleId + ".payload",
                    "Gameplay Trigger payload is missing or unreadable."));
                return new LevelWorldObjectAuthoringValidationReport(issues);
            }

            CharacterTestLevelGameplayTriggerAuthoringDocument candidate;
            try
            {
                candidate = document.Normalize();
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is InvalidOperationException)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_INVALID",
                    "$.modules." + ModuleId + ".payload",
                    exception.Message));
                return new LevelWorldObjectAuthoringValidationReport(issues);
            }

            string rootPath = "$.modules." + ModuleId + ".payload";
            if (!string.Equals(
                    candidate.Format,
                    CharacterTestLevelGameplayTriggerAuthoringDocument.CurrentFormat,
                    StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "CT_TRIGGER_FORMAT_UNSUPPORTED",
                    rootPath + ".format",
                    "Gameplay Trigger payload format is not supported."));
            }
            if (candidate.SchemaVersion
                != CharacterTestLevelGameplayTriggerAuthoringDocument
                    .CurrentSchemaVersion)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SCHEMA_VERSION_UNSUPPORTED",
                    rootPath + ".schemaVersion",
                    "Gameplay Trigger payload schema version is not supported."));
            }

            ValidateWrapper(candidate, worldObjects, rootPath, issues);
            ValidateBattleTemplates(candidate, rootPath, issues);
            if (issues.Count > 0)
                return new LevelWorldObjectAuthoringValidationReport(issues);

            if (IsEmpty(candidate.Authoring))
                return LevelWorldObjectAuthoringValidationReport.Empty;

            if (!TryCreateEnvironment(
                    candidate,
                    rootPath,
                    issues,
                    out GameplayTriggerCompilerEnvironment environment))
            {
                return new LevelWorldObjectAuthoringValidationReport(issues);
            }

            compileResult = GameplayTriggerConfigCompiler.Compile(
                candidate.Authoring,
                GameplayTriggerCompilerOptions.Default,
                environment);
            if (!compileResult.Succeeded)
                AddCompilerDiagnostics(compileResult, rootPath + ".authoring", issues);
            return new LevelWorldObjectAuthoringValidationReport(issues);
        }

        /// <remarks>
        /// AllocByDesign. Runtime-startup-only rebuild from the trusted Scene cache input.
        /// Scene cross-reference validation already happened when the cache was generated.
        /// </remarks>
        internal static GameplayTriggerCompileResult CompileRuntimeCache(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            long expectedGameplayTriggerCatalogHash,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (document == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_RUNTIME_PAYLOAD_MISSING",
                    "$.runtimeCache",
                    "Cached Gameplay Trigger payload is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return null;
            }
            if (expectedGameplayTriggerCatalogHash == 0L)
            {
                issues.Add(Error(
                    "CT_TRIGGER_RUNTIME_HASH_MISSING",
                    "$.runtimeCache.triggerCatalogHash",
                    "Cached Gameplay Trigger catalog hash is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return null;
            }

            CharacterTestLevelGameplayTriggerAuthoringDocument candidate;
            try
            {
                candidate = document.Normalize();
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is InvalidOperationException)
            {
                issues.Add(Error(
                    "CT_TRIGGER_RUNTIME_PAYLOAD_INVALID",
                    "$.runtimeCache",
                    exception.Message));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return null;
            }

            if (!string.Equals(
                    candidate.Format,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .CurrentFormat,
                    StringComparison.Ordinal)
                || candidate.SchemaVersion
                != CharacterTestLevelGameplayTriggerAuthoringDocument
                    .CurrentSchemaVersion)
            {
                issues.Add(Error(
                    "CT_TRIGGER_RUNTIME_SCHEMA_UNSUPPORTED",
                    "$.runtimeCache",
                    "Cached Gameplay Trigger schema is not supported."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return null;
            }

            if (!TryCreateEnvironment(
                    candidate,
                    "$.runtimeCache",
                    issues,
                    out GameplayTriggerCompilerEnvironment environment,
                    expectedGameplayTriggerCatalogHash))
            {
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return null;
            }

            GameplayTriggerCompileResult result =
                GameplayTriggerConfigCompiler.Compile(
                    candidate.Authoring,
                    GameplayTriggerCompilerOptions.Default,
                    environment);
            if (!result.Succeeded)
            {
                AddCompilerDiagnostics(
                    result,
                    "$.runtimeCache.authoring",
                    issues);
            }
            report = new LevelWorldObjectAuthoringValidationReport(issues);
            return result;
        }

        internal static bool TryFindEntryForRegion(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            string regionStableId,
            out CharacterTestLevelGameplayTriggerEntry entry)
        {
            entry = null;
            if (document?.RegionBindings == null
                || document.Entries == null
                || string.IsNullOrWhiteSpace(regionStableId))
            {
                return false;
            }

            int sourceBindingId = 0;
            for (int i = 0; i < document.RegionBindings.Count; i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    document.RegionBindings[i];
                if (binding != null
                    && string.Equals(
                        binding.RegionStableId,
                        regionStableId,
                        StringComparison.Ordinal))
                {
                    sourceBindingId = binding.SourceBindingId;
                    break;
                }
            }
            if (sourceBindingId <= 0)
                return false;

            for (int i = 0; i < document.Entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry candidate =
                    document.Entries[i];
                if (candidate?.SourceBindingId == sourceBindingId)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void ValidateWrapper(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            LevelWorldObjectAuthoringDocument worldObjects,
            string rootPath,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            GameplayTriggerAuthoringDocument authoring = document.Authoring;
            if (authoring == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_AUTHORING_MISSING",
                    rootPath + ".authoring",
                    "Gameplay Trigger authoring is missing."));
                return;
            }

            var sources = IndexSources(authoring.Sources, rootPath, issues);
            var conditions =
                IndexConditions(authoring.Conditions, rootPath, issues);
            var definitions =
                IndexDefinitions(authoring.Definitions, rootPath, issues);
            var bindings =
                IndexBindings(authoring.Bindings, rootPath, issues);
            var bindingsBySource =
                IndexBindingsBySource(authoring.Bindings, rootPath, issues);
            var regionBindings =
                IndexRegionBindings(document.RegionBindings, rootPath, issues);
            var regions = IndexRegions(worldObjects, rootPath, issues);

            var entryStableIds = new HashSet<string>(StringComparer.Ordinal);
            var entrySourceIds = new HashSet<int>();
            var entryConditionIds = new HashSet<int>();
            var entryDefinitionIds = new HashSet<int>();
            var entryBindingIds = new HashSet<int>();
            var entrySourceBindingIds = new HashSet<int>();
            var entriesBySourceBindingId =
                new Dictionary<
                    int,
                    CharacterTestLevelGameplayTriggerEntry>();
            for (int i = 0; i < document.Entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    document.Entries[i];
                string path = rootPath + ".entries[" + i + "]";
                if (entry == null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_ENTRY_NULL",
                        path,
                        "Trigger entry cannot be null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.StableId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_STABLE_ID_MISSING",
                        path + ".stableId",
                        "Trigger entry stable ID is required."));
                }
                else if (!entryStableIds.Add(entry.StableId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_STABLE_ID_DUPLICATE",
                        path + ".stableId",
                        "Trigger entry stable ID must be unique."));
                }
                if (!CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsSupportedTemplate(entry.TemplateId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_TEMPLATE_UNSUPPORTED",
                        path + ".templateId",
                        "Trigger entry must use one of the fixed CharacterTest templates."));
                }

                AddUniqueOwnedId(
                    entry.SourceDefinitionId,
                    entrySourceIds,
                    path + ".sourceDefinitionId",
                    "source definition",
                    issues);
                AddUniqueOwnedId(
                    entry.ConditionId,
                    entryConditionIds,
                    path + ".conditionId",
                    "condition",
                    issues);
                AddUniqueOwnedId(
                    entry.DefinitionId,
                    entryDefinitionIds,
                    path + ".definitionId",
                    "definition",
                    issues);
                AddUniqueOwnedId(
                    entry.BindingId,
                    entryBindingIds,
                    path + ".bindingId",
                    "binding",
                    issues);
                AddUniqueOwnedId(
                    entry.SourceBindingId,
                    entrySourceBindingIds,
                    path + ".sourceBindingId",
                    "source binding",
                    issues);
                if (entry.SourceBindingId > 0
                    && !entriesBySourceBindingId.ContainsKey(
                        entry.SourceBindingId))
                {
                    entriesBySourceBindingId.Add(
                        entry.SourceBindingId,
                        entry);
                }

                RequireReference(
                    sources,
                    entry.SourceDefinitionId,
                    path + ".sourceDefinitionId",
                    "source definition",
                    issues);
                RequireReference(
                    conditions,
                    entry.ConditionId,
                    path + ".conditionId",
                    "condition",
                    issues);
                RequireReference(
                    definitions,
                    entry.DefinitionId,
                    path + ".definitionId",
                    "definition",
                    issues);
                RequireReference(
                    bindings,
                    entry.BindingId,
                    path + ".bindingId",
                    "binding",
                    issues);
                bool playerHpThreshold =
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry.TemplateId)
                    || CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry.TemplateId);
                if (!playerHpThreshold)
                {
                    RequireReference(
                        regionBindings,
                        entry.SourceBindingId,
                        path + ".sourceBindingId",
                        "region source binding",
                        issues);
                }
                else if (regionBindings.ContainsKey(entry.SourceBindingId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_THRESHOLD_REGION_BINDING_UNEXPECTED",
                        path + ".sourceBindingId",
                        "Player HP Threshold sources cannot carry a region binding."));
                }
                if (sources.TryGetValue(
                        entry.SourceDefinitionId,
                        out GameplayTriggerSourceAuthoring entrySource))
                {
                    ValidateEntrySource(
                        entry,
                        entrySource,
                        path + ".sourceDefinitionId",
                        issues);
                }

                if (definitions.TryGetValue(
                        entry.DefinitionId,
                        out GameplayTriggerDefinitionAuthoring definition))
                {
                    ValidateEntryRoutes(entry, definition, path, issues);
                    ValidateTemplatePayloads(
                        entry,
                        definition,
                        path,
                        issues);
                    if (definition.SourceDefinitionId
                        != entry.SourceDefinitionId)
                    {
                        issues.Add(Error(
                            "CT_TRIGGER_ENTRY_SOURCE_MISMATCH",
                            path + ".sourceDefinitionId",
                            "Entry source definition does not match its domain definition."));
                    }
                }

                if (bindings.TryGetValue(
                        entry.BindingId,
                        out GameplayTriggerBindingAuthoring binding))
                {
                    if (binding.DefinitionId != entry.DefinitionId
                        || binding.SourceBindingId != entry.SourceBindingId)
                    {
                        issues.Add(Error(
                            "CT_TRIGGER_ENTRY_BINDING_MISMATCH",
                            path + ".bindingId",
                            "Entry binding does not match its definition/source-binding IDs."));
                    }
                }
            }

            RequireOwnedRows(
                sources.Keys,
                entrySourceIds,
                rootPath + ".authoring.sources",
                "source definition",
                issues);
            RequireOwnedRows(
                conditions.Keys,
                entryConditionIds,
                rootPath + ".authoring.conditions",
                "condition",
                issues);
            RequireOwnedRows(
                definitions.Keys,
                entryDefinitionIds,
                rootPath + ".authoring.definitions",
                "definition",
                issues);
            RequireOwnedRows(
                bindings.Keys,
                entryBindingIds,
                rootPath + ".authoring.bindings",
                "binding",
                issues);

            for (int i = 0; i < document.RegionBindings.Count; i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    document.RegionBindings[i];
                if (binding == null)
                    continue;

                string path = rootPath + ".regionBindings[" + i + "]";
                if (!bindingsBySource.ContainsKey(binding.SourceBindingId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_BINDING_ORPHAN",
                        path + ".sourceBindingId",
                        "Region source binding has no matching Trigger binding."));
                }
                if (!entrySourceBindingIds.Contains(binding.SourceBindingId))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_BINDING_ENTRY_MISSING",
                        path + ".sourceBindingId",
                        "Region source binding has no owning Trigger entry."));
                }
                if (binding.ProviderId != CombatRegionProviderId)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_PROVIDER_UNSUPPORTED",
                        path + ".providerId",
                        "Region Trigger must use the CharacterTest Combat region provider."));
                }
                if (binding.LayerMask == 0)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_LAYER_MASK_EMPTY",
                        path + ".layerMask",
                        "Region query layer mask cannot be zero."));
                }
                if (binding.MaxQueryHits <= 0
                    || binding.MaxMappedSubjects <= 0
                    || binding.MaxMappedSubjects > binding.MaxQueryHits)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_CAPACITY_INVALID",
                        path,
                        "Query and mapped-subject capacities must be positive and mapped subjects cannot exceed query hits."));
                }

                if (entriesBySourceBindingId.TryGetValue(
                        binding.SourceBindingId,
                        out CharacterTestLevelGameplayTriggerEntry
                            filterEntry))
                {
                    ValidateSubjectFilter(
                        binding.SubjectFilter,
                        filterEntry.TemplateId,
                        path + ".subjectFilter",
                        issues);
                }

                if (!regions.TryGetValue(
                        binding.RegionStableId ?? string.Empty,
                        out LevelWorldObjectRegionDefinition region))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_REGION_MISSING",
                        path + ".regionStableId",
                        "Referenced world-object region does not exist."));
                }
                else if (!region.Enabled
                         || region.RegionKind
                         != LevelWorldObjectRegionKind.TriggerRegion)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_REGION_KIND_INVALID",
                        path + ".regionStableId",
                        "Referenced region must be an enabled TriggerRegion."));
                }
            }

            ValidateExplicitTargetDomains(authoring, rootPath, issues);
            ValidateIdState(document, authoring, rootPath, issues);
        }

        private static void ValidateSubjectFilter(
            CharacterTestLevelGameplayTriggerSubjectFilter filter,
            string templateId,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            int initialIssueCount = issues.Count;
            if (filter == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_FILTER_MISSING",
                    path,
                    "Region Trigger subject filter is required."));
                return;
            }

            if (filter.AcceptedIdentityTypes
                != GameplayTargetIdentityTypes.Character)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_IDENTITY_UNSUPPORTED",
                    path + ".acceptedIdentityTypes",
                    "CharacterTest Combat regions currently support only Character subjects."));
            }

            const GameplayTargetCapabilities SupportedCapabilities =
                GameplayTargetCapabilities.Damageable
                | GameplayTargetCapabilities.Movable;
            if ((filter.RequiredCapabilities & ~SupportedCapabilities) != 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_CAPABILITY_UNSUPPORTED",
                    path + ".requiredCapabilities",
                    "Subject filter contains a capability not supplied by the CharacterTest character source."));
            }

            bool buffTemplate =
                string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal);
            if (buffTemplate
                && (filter.RequiredCapabilities
                    & GameplayTargetCapabilities.Damageable) == 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_CAPABILITY_REQUIRED",
                    path + ".requiredCapabilities",
                    "Gameplay Buff region subjects must remain Damageable."));
            }

            if (!filter.RequireAlive)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_ALIVE_REQUIRED",
                    path + ".requireAlive",
                    "CharacterTest region subjects must require an alive lifecycle state."));
            }

            bool supportedRelation =
                filter.RelationFilter == GameplayTargetRelationFilter.Any
                || filter.RelationFilter
                == GameplayTargetRelationFilter.SameTeam
                || filter.RelationFilter
                == GameplayTargetRelationFilter.Enemy
                || filter.RelationFilter
                == GameplayTargetRelationFilter.Neutral;
            if (!supportedRelation)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_RELATION_UNSUPPORTED",
                    path + ".relationFilter",
                    "Static CharacterTest regions do not support this target relation."));
            }
            else if (filter.RelationFilter
                     == GameplayTargetRelationFilter.Any)
            {
                if (filter.ReferenceTeamId != 0)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SUBJECT_REFERENCE_TEAM_UNEXPECTED",
                        path + ".referenceTeamId",
                        "An unrestricted subject filter cannot carry a reference team."));
                }
            }
            else if (filter.ReferenceTeamId <= 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_REFERENCE_TEAM_REQUIRED",
                    path + ".referenceTeamId",
                    "A team-relation subject filter requires a positive reference team."));
            }

            if (issues.Count != initialIssueCount)
                return;

            try
            {
                filter.ToRuntimeFilter();
            }
            catch (ArgumentException exception)
            {
                issues.Add(Error(
                    "CT_TRIGGER_SUBJECT_FILTER_INVALID",
                    path,
                    exception.Message));
            }
        }

        private static void ValidateEntrySource(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerSourceAuthoring source,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry?.TemplateId))
            {
                if (source.SourceKind != GameplayTriggerSourceKind.Signal ||
                    source.SignalOrdering != GameplayTriggerSignalOrdering.UnorderedMultiset)
                    issues.Add(Error("CT_TRIGGER_BATTLE_SOURCE_INVALID", path, "Battle vitals requires an unordered Signal source."));
                return;
            }
            if (!CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(entry?.TemplateId))
            {
                if (source.SourceKind != GameplayTriggerSourceKind.Occupancy)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_KIND_UNSUPPORTED",
                        path,
                        "Region Trigger source must use Occupancy semantics."));
                }
                return;
            }

            bool valid = source.SourceKind
                    == GameplayTriggerSourceKind.Threshold
                && source.ThresholdDirection
                    == GameplayTriggerThresholdDirection.Low
                && source.InitialSamplePolicy
                    == GameplayTriggerInitialSamplePolicy.PrimeSilently
                && source.DeparturePolicy
                    == GameplayTriggerSubjectDeparturePolicy.EmitExited
                && source.EnterThreshold.Kind
                    == GameplayTriggerFix64InputKind.InvariantDecimal
                && source.ExitThreshold.Kind
                    == GameplayTriggerFix64InputKind.InvariantDecimal
                && int.TryParse(
                    source.EnterThreshold.InvariantDecimal,
                    out int enterThreshold)
                && int.TryParse(
                    source.ExitThreshold.InvariantDecimal,
                    out int exitThreshold)
                && enterThreshold
                    >= CharacterTestLevelGameplayTriggerAuthoringDocument
                        .MinimumPlayerHpThreshold
                && exitThreshold
                    <= CharacterTestLevelGameplayTriggerAuthoringDocument
                        .MaximumPlayerHpThreshold
                && exitThreshold >= enterThreshold;
            if (!valid)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PLAYER_HP_THRESHOLD_INVALID",
                    path,
                    "Player HP Threshold must use Low semantics, PrimeSilently, EmitExited, and integer thresholds within 1..100 with exit greater than or equal to enter."));
            }
        }

        private static bool TryCreateEnvironment(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            string rootPath,
            List<LevelWorldObjectAuthoringIssue> issues,
            out GameplayTriggerCompilerEnvironment environment,
            long expectedGameplayTriggerCatalogHash = 0L)
        {
            environment = null;
            ValidateBattleTemplates(document, rootPath, issues);
            if (issues.Count > 0) return false;
            GameplayTriggerRetiredIdDescriptor[] retiredRows =
                CreateRetiredRows(document.IdState);
            if (!GameplayTriggerRetiredIdRegistry.TryCreate(
                    retiredRows,
                    out GameplayTriggerRetiredIdRegistry retiredRegistry,
                    out GameplayTriggerConfigRegistryResult retiredResult))
            {
                AddRegistryDiagnostics(retiredResult, rootPath + ".idState", issues);
                return false;
            }

            GameplayTriggerSourceBindingContractDescriptor[] sourceRows =
                CreateSourceContracts(document, rootPath, issues);
            if (issues.Count > 0)
                return false;
            if (!GameplayTriggerSourceBindingContractRegistry.TryCreate(
                    sourceRows,
                    out GameplayTriggerSourceBindingContractRegistry
                        sourceRegistry,
                    out GameplayTriggerConfigRegistryResult sourceResult))
            {
                AddRegistryDiagnostics(
                    sourceResult,
                    rootPath + ".regionBindings",
                    issues);
                return false;
            }

            GameplayTriggerActionRequirementDescriptor[] actionRows =
            {
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                    GameplayTriggerGameplayBuffActionKinds.DeliveryContract),
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                    GameplayTriggerGameplayBuffActionKinds.DeliveryContract),
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    GameplayTriggerStoryCommandActionKinds.DeliveryContract),
                new GameplayTriggerActionRequirementDescriptor(
                    new GameplayTriggerActionKindId(CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalSetBindingEnabledActionId),
                    GameplayTriggerDeliveryContract.InternalDeferred, GameplayTriggerInternalDeferredOperationKind.SetEnabled)
            };
            if (!GameplayTriggerActionRequirementRegistry.TryCreate(
                    actionRows,
                    out GameplayTriggerActionRequirementRegistry actionRegistry,
                    out GameplayTriggerConfigRegistryResult actionResult))
            {
                AddRegistryDiagnostics(
                    actionResult,
                    rootPath + ".authoring.actions",
                    issues);
                return false;
            }

            var identity = new GameplayTriggerExpectedCatalogIdentity(
                GameplayTriggerConfigSchemas.RuntimeCatalogId,
                GameplayTriggerConfigSchemas.RuntimeCatalogVersion,
                expectedGameplayTriggerCatalogHash != 0L,
                expectedGameplayTriggerCatalogHash);
            if (!GameplayTriggerCompilerEnvironment.TryCreate(
                    retiredRegistry,
                    sourceRegistry,
                    actionRegistry,
                    identity,
                    out environment,
                    out GameplayTriggerConfigRegistryResult environmentResult))
            {
                AddRegistryDiagnostics(
                    environmentResult,
                    rootPath + ".environment",
                    issues);
                return false;
            }

            return true;
        }

        private static GameplayTriggerSourceBindingContractDescriptor[]
            CreateSourceContracts(
                CharacterTestLevelGameplayTriggerAuthoringDocument document,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            GameplayTriggerAuthoringDocument authoring = document.Authoring;
            var definitions = new Dictionary<int, GameplayTriggerDefinitionAuthoring>();
            var bindings = new Dictionary<int, GameplayTriggerBindingAuthoring>();
            var sources = new Dictionary<int, GameplayTriggerSourceAuthoring>();
            for (int i = 0; i < authoring.Definitions.Length; i++)
                definitions[authoring.Definitions[i].DefinitionId] = authoring.Definitions[i];
            for (int i = 0; i < authoring.Bindings.Length; i++)
                bindings[authoring.Bindings[i].SourceBindingId] = authoring.Bindings[i];
            for (int i = 0; i < authoring.Sources.Length; i++)
                sources[authoring.Sources[i].SourceDefinitionId] = authoring.Sources[i];

            int entryCount = document.Entries?.Count ?? 0;
            var rows =
                new GameplayTriggerSourceBindingContractDescriptor[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    document.Entries[i];
                string path = rootPath + ".entries[" + i + "]";
                if (!bindings.TryGetValue(
                        entry.SourceBindingId,
                        out GameplayTriggerBindingAuthoring binding)
                    || !definitions.TryGetValue(
                        binding.DefinitionId,
                        out GameplayTriggerDefinitionAuthoring definition)
                    || !sources.TryGetValue(
                        definition.SourceDefinitionId,
                        out GameplayTriggerSourceAuthoring source))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_CONTRACT_MISSING",
                        path,
                        "Could not derive the trusted source contract from Trigger authoring."));
                    continue;
                }

                if (CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry.TemplateId))
                {
                    rows[i] = new GameplayTriggerSourceBindingContractDescriptor(
                        new GameplayTriggerSourceBindingId(entry.SourceBindingId),
                        new GameplayTriggerSourceDefinitionId(source.SourceDefinitionId), GameplayTriggerSourceKind.Signal,
                        new GameplayTriggerSourceProviderId(BattleVitalsSignalProviderId),
                        CharacterTestGameplayTriggerBattleVitalsCatalog.EntityCapabilities,
                        completeBatchPerEvaluation: true, maxInputItemsPerEvaluation: 1, maxActiveRelations: 0,
                        signalEnvelopeSchemaVersion: 1);
                    continue;
                }
                if (CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry.TemplateId))
                {
                    if (source.SourceKind
                        != GameplayTriggerSourceKind.Threshold)
                    {
                        issues.Add(Error(
                            "CT_TRIGGER_SOURCE_KIND_UNSUPPORTED",
                            path,
                            "Player HP source must use Threshold semantics."));
                        continue;
                    }
                    rows[i] =
                        new GameplayTriggerSourceBindingContractDescriptor(
                            new GameplayTriggerSourceBindingId(
                                entry.SourceBindingId),
                            new GameplayTriggerSourceDefinitionId(
                                source.SourceDefinitionId),
                            GameplayTriggerSourceKind.Threshold,
                            new GameplayTriggerSourceProviderId(
                                PlayerAttributeThresholdProviderId),
                            GameplayTriggerSourceEntityCapability
                                .SubjectPossible
                            | GameplayTriggerSourceEntityCapability
                                .SubjectGuaranteed
                            | GameplayTriggerSourceEntityCapability
                                .PresenceEpoch,
                            completeBatchPerEvaluation: true,
                            maxInputItemsPerEvaluation: 1,
                            maxActiveRelations: 1);
                    continue;
                }

                CharacterTestLevelGameplayTriggerRegionBinding regionBinding =
                    FindRegionBinding(
                        document.RegionBindings,
                        entry.SourceBindingId);
                if (regionBinding == null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_CONTRACT_MISSING",
                        path,
                        "Region Trigger has no trusted source binding."));
                    continue;
                }
                if (source.SourceKind != GameplayTriggerSourceKind.Occupancy)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_KIND_UNSUPPORTED",
                        path,
                        "Region Trigger source must use Occupancy semantics."));
                    continue;
                }
                rows[i] = new GameplayTriggerSourceBindingContractDescriptor(
                    new GameplayTriggerSourceBindingId(
                        entry.SourceBindingId),
                    new GameplayTriggerSourceDefinitionId(
                        source.SourceDefinitionId),
                    GameplayTriggerSourceKind.Occupancy,
                    new GameplayTriggerSourceProviderId(
                        CombatRegionProviderId),
                    GameplayTriggerSourceEntityCapability.SubjectPossible
                    | GameplayTriggerSourceEntityCapability.SubjectGuaranteed
                    | GameplayTriggerSourceEntityCapability.PresenceEpoch,
                    completeBatchPerEvaluation: true,
                    maxInputItemsPerEvaluation:
                        regionBinding.MaxMappedSubjects,
                    maxActiveRelations: regionBinding.MaxMappedSubjects);
            }

            return rows;
        }

        private static CharacterTestLevelGameplayTriggerRegionBinding
            FindRegionBinding(
                IReadOnlyList<CharacterTestLevelGameplayTriggerRegionBinding>
                    rows,
                int sourceBindingId)
        {
            if (rows == null)
                return null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i]?.SourceBindingId == sourceBindingId)
                    return rows[i];
            }
            return null;
        }

        private static void ValidateIdState(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            GameplayTriggerAuthoringDocument authoring,
            string rootPath,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            CharacterTestLevelGameplayTriggerIdState state = document.IdState;
            if (state == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_ID_STATE_MISSING",
                    rootPath + ".idState",
                    "Trigger ID state is missing."));
                return;
            }

            GameplayTriggerRetiredIdDescriptor[] retired =
                CreateRetiredRows(state);
            if (!GameplayTriggerRetiredIdRegistry.TryCreate(
                    retired,
                    out _,
                    out GameplayTriggerConfigRegistryResult retiredResult))
            {
                AddRegistryDiagnostics(
                    retiredResult,
                    rootPath + ".idState.retiredIds",
                    issues);
            }

            int maxSource = 0;
            int maxCondition = 0;
            int maxDefinition = 0;
            int maxRoute = 0;
            int maxBinding = 0;
            int maxSourceBinding = 0;
            for (int i = 0; i < authoring.Sources.Length; i++)
                maxSource = Math.Max(maxSource, authoring.Sources[i].SourceDefinitionId);
            for (int i = 0; i < authoring.Conditions.Length; i++)
                maxCondition = Math.Max(maxCondition, authoring.Conditions[i].ConditionId);
            for (int i = 0; i < authoring.Definitions.Length; i++)
            {
                GameplayTriggerDefinitionAuthoring definition =
                    authoring.Definitions[i];
                maxDefinition = Math.Max(maxDefinition, definition.DefinitionId);
                for (int routeIndex = 0;
                     routeIndex < definition.Routes.Length;
                     routeIndex++)
                {
                    maxRoute = Math.Max(
                        maxRoute,
                        definition.Routes[routeIndex].RouteId);
                }
            }
            for (int i = 0; i < authoring.Bindings.Length; i++)
            {
                maxBinding = Math.Max(maxBinding, authoring.Bindings[i].BindingId);
                maxSourceBinding = Math.Max(
                    maxSourceBinding,
                    authoring.Bindings[i].SourceBindingId);
            }

            for (int i = 0; i < retired.Length; i++)
            {
                GameplayTriggerRetiredIdDescriptor row = retired[i];
                switch (row.IdKind)
                {
                    case GameplayTriggerConfigIdKind.SourceDefinition:
                        maxSource = Math.Max(maxSource, row.PrimaryId);
                        break;
                    case GameplayTriggerConfigIdKind.Condition:
                        maxCondition = Math.Max(maxCondition, row.PrimaryId);
                        break;
                    case GameplayTriggerConfigIdKind.Definition:
                        maxDefinition = Math.Max(maxDefinition, row.PrimaryId);
                        break;
                    case GameplayTriggerConfigIdKind.Route:
                        maxRoute = Math.Max(maxRoute, row.SecondaryId);
                        break;
                    case GameplayTriggerConfigIdKind.Binding:
                        maxBinding = Math.Max(maxBinding, row.PrimaryId);
                        break;
                    case GameplayTriggerConfigIdKind.SourceBinding:
                        maxSourceBinding = Math.Max(
                            maxSourceBinding,
                            row.PrimaryId);
                        break;
                    default:
                        issues.Add(Error(
                            "CT_TRIGGER_ID_KIND_NOT_LEVEL_OWNED",
                            rootPath + ".idState.retiredIds[" + i + "].idKind",
                            "Level payload cannot retire ActionKind or Provider IDs."));
                        break;
                }
            }

            RequireNextId(
                state.NextSourceDefinitionId,
                maxSource,
                rootPath + ".idState.nextSourceDefinitionId",
                issues);
            RequireNextId(
                state.NextConditionId,
                maxCondition,
                rootPath + ".idState.nextConditionId",
                issues);
            RequireNextId(
                state.NextDefinitionId,
                maxDefinition,
                rootPath + ".idState.nextDefinitionId",
                issues);
            RequireNextId(
                state.NextRouteId,
                maxRoute,
                rootPath + ".idState.nextRouteId",
                issues);
            RequireNextId(
                state.NextBindingId,
                maxBinding,
                rootPath + ".idState.nextBindingId",
                issues);
            RequireNextId(
                state.NextSourceBindingId,
                maxSourceBinding,
                rootPath + ".idState.nextSourceBindingId",
                issues);
        }

        private static void ValidateEntryRoutes(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerDefinitionAuthoring definition,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (entry.RouteIds == null
                || entry.RouteIds.Count != definition.Routes.Length)
            {
                issues.Add(Error(
                    "CT_TRIGGER_ENTRY_ROUTES_MISMATCH",
                    path + ".routeIds",
                    "Entry route IDs must exactly match its domain definition."));
                return;
            }

            var expected = new HashSet<int>();
            for (int i = 0; i < definition.Routes.Length; i++)
                expected.Add(definition.Routes[i].RouteId);
            for (int i = 0; i < entry.RouteIds.Count; i++)
            {
                if (!expected.Remove(entry.RouteIds[i]))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_ENTRY_ROUTES_MISMATCH",
                        path + ".routeIds[" + i + "]",
                        "Entry route IDs must be unique and owned by its domain definition."));
                    return;
                }
            }
            if (expected.Count > 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_ENTRY_ROUTES_MISMATCH",
                    path + ".routeIds",
                    "Entry route IDs do not include every definition route."));
            }
        }

        private static void ValidateTemplatePayloads(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerDefinitionAuthoring definition,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (entry == null
                || definition?.Routes == null
                || definition.Routes.Length == 0
                || definition.Routes[0]?.Actions == null
                || definition.Routes[0].Actions.Length == 0
                || !CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsSupportedTemplate(entry.TemplateId))
            {
                return;
            }

            GameplayTriggerActionAuthoring firstAction =
                definition.Routes[0].Actions[0];
            bool isStory = string.Equals(
                    entry.TemplateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(entry.TemplateId)
                || CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry.TemplateId);
            bool valid = firstAction.Payload0 > 0L
                && firstAction.Payload0 <= int.MaxValue
                && firstAction.Payload1 >= (isStory ? int.MinValue : 0L)
                && firstAction.Payload1 <= int.MaxValue
                && firstAction.Payload2 >= int.MinValue
                && firstAction.Payload2 <= int.MaxValue;
            if (!isStory)
                valid &= firstAction.Payload2 == 0L;

            if (string.Equals(
                    entry.TemplateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal))
            {
                valid &= definition.Routes.Length == 2
                    && definition.Routes[1]?.Actions != null
                    && definition.Routes[1].Actions.Length > 0
                    && definition.Routes[1].Actions[0].Payload0
                        == firstAction.Payload0
                    && definition.Routes[1].Actions[0].Payload1
                        == firstAction.Payload1
                    && definition.Routes[1].Actions[0].Payload2 == 0L;
            }

            if (!valid)
            {
                issues.Add(Error(
                    "CT_TRIGGER_TEMPLATE_PAYLOAD_INVALID",
                    path + ".templateId",
                    isStory
                        ? "Story template requires a positive Int32 trigger ID and Int32 parameters."
                        : "Buff template requires a positive Int32 Buff ID, a non-negative Int32 source ID, and matching Apply/Remove payloads."));
            }
        }

        private static void ValidateExplicitTargetDomains(
            GameplayTriggerAuthoringDocument authoring,
            string rootPath,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            for (int bindingIndex = 0;
                 bindingIndex < authoring.Bindings.Length;
                 bindingIndex++)
            {
                GameplayTriggerBindingAuthoring binding =
                    authoring.Bindings[bindingIndex];
                for (int targetIndex = 0;
                     targetIndex < binding.ExplicitTargets.Length;
                     targetIndex++)
                {
                    GameplayTriggerBindingTargetAuthoring target =
                        binding.ExplicitTargets[targetIndex];
                    if (target.DomainId
                            != GameplayTriggerTargetRef.GameplayEntityDomainId
                        && target.DomainId
                            != GameplayTriggerTargetRef
                                .GameplayTriggerSourceBindingDomainId)
                    {
                        issues.Add(Error(
                            "CT_TRIGGER_TARGET_DOMAIN_UNSUPPORTED",
                            rootPath
                            + ".authoring.bindings["
                            + bindingIndex
                            + "].explicitTargets["
                            + targetIndex
                            + "].domainId",
                            "Target domain is not supported by CharacterTest Trigger actions."));
                    }
                }
            }
        }

        private static Dictionary<int, GameplayTriggerSourceAuthoring>
            IndexSources(
                GameplayTriggerSourceAuthoring[] rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result = new Dictionary<int, GameplayTriggerSourceAuthoring>();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerSourceAuthoring row = rows[i];
                if (!TryAdd(result, row.SourceDefinitionId, row))
                    AddDuplicateId("sources", row.SourceDefinitionId, i, rootPath, issues);
            }
            return result;
        }

        private static Dictionary<int, GameplayTriggerConditionAuthoring>
            IndexConditions(
                GameplayTriggerConditionAuthoring[] rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<int, GameplayTriggerConditionAuthoring>();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerConditionAuthoring row = rows[i];
                if (!TryAdd(result, row.ConditionId, row))
                    AddDuplicateId("conditions", row.ConditionId, i, rootPath, issues);
            }
            return result;
        }

        private static Dictionary<int, GameplayTriggerDefinitionAuthoring>
            IndexDefinitions(
                GameplayTriggerDefinitionAuthoring[] rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<int, GameplayTriggerDefinitionAuthoring>();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerDefinitionAuthoring row = rows[i];
                if (!TryAdd(result, row.DefinitionId, row))
                    AddDuplicateId("definitions", row.DefinitionId, i, rootPath, issues);
            }
            return result;
        }

        private static Dictionary<int, GameplayTriggerBindingAuthoring>
            IndexBindings(
                GameplayTriggerBindingAuthoring[] rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<int, GameplayTriggerBindingAuthoring>();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerBindingAuthoring row = rows[i];
                if (!TryAdd(result, row.BindingId, row))
                    AddDuplicateId("bindings", row.BindingId, i, rootPath, issues);
            }
            return result;
        }

        private static Dictionary<int, GameplayTriggerBindingAuthoring>
            IndexBindingsBySource(
                GameplayTriggerBindingAuthoring[] rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<int, GameplayTriggerBindingAuthoring>();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerBindingAuthoring row = rows[i];
                if (!TryAdd(result, row.SourceBindingId, row))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_SOURCE_BINDING_ID_DUPLICATE",
                        rootPath
                        + ".authoring.bindings["
                        + i
                        + "].sourceBindingId",
                        "Source-binding ID must be unique."));
                }
            }
            return result;
        }

        private static Dictionary<int,
                CharacterTestLevelGameplayTriggerRegionBinding>
            IndexRegionBindings(
                List<CharacterTestLevelGameplayTriggerRegionBinding> rows,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<
                    int,
                    CharacterTestLevelGameplayTriggerRegionBinding>();
            for (int i = 0; i < rows.Count; i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding row = rows[i];
                if (row == null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_REGION_BINDING_NULL",
                        rootPath + ".regionBindings[" + i + "]",
                        "Region binding cannot be null."));
                    continue;
                }
                if (!TryAdd(result, row.SourceBindingId, row))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_REGION_BINDING_DUPLICATE",
                        rootPath
                        + ".regionBindings["
                        + i
                        + "].sourceBindingId",
                        "Region binding source-binding ID must be unique."));
                }
            }
            return result;
        }

        private static Dictionary<string, LevelWorldObjectRegionDefinition>
            IndexRegions(
                LevelWorldObjectAuthoringDocument worldObjects,
                string rootPath,
                List<LevelWorldObjectAuthoringIssue> issues)
        {
            var result =
                new Dictionary<string, LevelWorldObjectRegionDefinition>(
                    StringComparer.Ordinal);
            if (worldObjects?.Regions == null)
                return result;

            for (int i = 0; i < worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition row = worldObjects.Regions[i];
                if (row == null || string.IsNullOrWhiteSpace(row.StableId))
                    continue;
                if (!TryAdd(result, row.StableId, row))
                {
                    issues.Add(Error(
                        "CT_TRIGGER_REGION_STABLE_ID_DUPLICATE",
                        rootPath + ".worldObjects.regions[" + i + "].stableId",
                        "World-object region stable ID must be unique."));
                }
            }
            return result;
        }

        private static bool TryAdd<TKey, TValue>(
            Dictionary<TKey, TValue> values,
            TKey key,
            TValue value)
        {
            if (values.ContainsKey(key))
                return false;
            values.Add(key, value);
            return true;
        }

        private static void AddDuplicateId(
            string collection,
            int id,
            int index,
            string rootPath,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            issues.Add(Error(
                "CT_TRIGGER_TYPED_ID_DUPLICATE",
                rootPath
                + ".authoring."
                + collection
                + "["
                + index
                + "]",
                "Typed Trigger ID "
                + id
                + " is duplicated in "
                + collection
                + "."));
        }

        private static void AddUniqueOwnedId(
            int id,
            HashSet<int> ids,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (id <= 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_TYPED_ID_INVALID",
                    path,
                    "Entry " + label + " ID must be positive."));
            }
            else if (!ids.Add(id))
            {
                issues.Add(Error(
                    "CT_TRIGGER_ENTRY_OWNERSHIP_DUPLICATE",
                    path,
                    "Each " + label + " row must be owned by one Trigger entry."));
            }
        }

        private static void RequireReference<T>(
            Dictionary<int, T> values,
            int id,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (!values.ContainsKey(id))
            {
                issues.Add(Error(
                    "CT_TRIGGER_REFERENCE_MISSING",
                    path,
                    "Referenced " + label + " does not exist."));
            }
        }

        private static void RequireOwnedRows(
            Dictionary<int, GameplayTriggerSourceAuthoring>.KeyCollection rows,
            HashSet<int> owned,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            foreach (int id in rows)
                RequireOwnedRow(id, owned, path, label, issues);
        }

        private static void RequireOwnedRows(
            Dictionary<int, GameplayTriggerConditionAuthoring>.KeyCollection rows,
            HashSet<int> owned,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            foreach (int id in rows)
                RequireOwnedRow(id, owned, path, label, issues);
        }

        private static void RequireOwnedRows(
            Dictionary<int, GameplayTriggerDefinitionAuthoring>.KeyCollection rows,
            HashSet<int> owned,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            foreach (int id in rows)
                RequireOwnedRow(id, owned, path, label, issues);
        }

        private static void RequireOwnedRows(
            Dictionary<int, GameplayTriggerBindingAuthoring>.KeyCollection rows,
            HashSet<int> owned,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            foreach (int id in rows)
                RequireOwnedRow(id, owned, path, label, issues);
        }

        private static void RequireOwnedRow(
            int id,
            HashSet<int> owned,
            string path,
            string label,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (!owned.Contains(id))
            {
                issues.Add(Error(
                    "CT_TRIGGER_AUTHORING_ROW_ORPHAN",
                    path,
                    "Domain " + label + " ID " + id + " has no owning entry."));
            }
        }

        private static void RequireNextId(
            int nextId,
            int maximumUsedId,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (nextId <= maximumUsedId || nextId <= 0)
            {
                issues.Add(Error(
                    "CT_TRIGGER_NEXT_ID_INVALID",
                    path,
                    "Next ID must be positive and greater than every live or retired ID."));
            }
        }

        private static GameplayTriggerRetiredIdDescriptor[] CreateRetiredRows(
            CharacterTestLevelGameplayTriggerIdState state)
        {
            if (state?.RetiredIds == null || state.RetiredIds.Count == 0)
                return Array.Empty<GameplayTriggerRetiredIdDescriptor>();

            var rows =
                new GameplayTriggerRetiredIdDescriptor[state.RetiredIds.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = state.RetiredIds[i] == null
                    ? default
                    : state.RetiredIds[i].ToDescriptor();
            }
            return rows;
        }

        private static bool IsEmpty(GameplayTriggerAuthoringDocument authoring)
        {
            return authoring != null
                && authoring.Sources.Length == 0
                && authoring.Conditions.Length == 0
                && authoring.Definitions.Length == 0
                && authoring.Bindings.Length == 0;
        }

        private static void AddRegistryDiagnostics(
            GameplayTriggerConfigRegistryResult result,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (result == null)
                return;
            for (int i = 0; i < result.DiagnosticCount; i++)
            {
                GameplayTriggerConfigDiagnostic diagnostic =
                    result.GetDiagnostic(i);
                AddCompilerDiagnostic(diagnostic, path, issues);
            }
        }

        private static void AddCompilerDiagnostics(
            GameplayTriggerCompileResult result,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            for (int i = 0; i < result.DiagnosticCount; i++)
                AddCompilerDiagnostic(result.GetDiagnostic(i), path, issues);
        }

        private static void AddCompilerDiagnostic(
            GameplayTriggerConfigDiagnostic diagnostic,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            issues.Add(Error(
                "CT_TRIGGER_COMPILE_" + diagnostic.Code,
                path + TrimRootPath(diagnostic.Path),
                "Trigger compiler rejected the candidate"
                + " (primary="
                + diagnostic.PrimaryId
                + ", secondary="
                + diagnostic.SecondaryId
                + ", detail0="
                + diagnostic.Detail0
                + ", detail1="
                + diagnostic.Detail1
                + ")."));
        }

        private static bool ValidateRequiredObject(
            JToken token,
            string path,
            string[] properties,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (token is not JObject value)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_SHAPE_INVALID",
                    path,
                    "Required value must be an object."));
                return false;
            }
            return ValidateObject(value, path, properties, issues);
        }

        private static bool ValidateRows(
            JToken token,
            string path,
            string[] properties,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (token is not JArray rows)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_SHAPE_INVALID",
                    path,
                    "Required collection must be an array."));
                return false;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not JObject row
                    || !ValidateObject(
                        row,
                        path + "[" + i + "]",
                        properties,
                        issues))
                {
                    if (rows[i] is not JObject)
                    {
                        issues.Add(Error(
                            "CT_TRIGGER_PAYLOAD_SHAPE_INVALID",
                            path + "[" + i + "]",
                            "Collection row must be an object."));
                    }
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateSubjectFilterRows(
            JToken token,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (token is not JArray rows)
                return false;

            for (int i = 0; i < rows.Count; i++)
            {
                JToken filter = rows[i]?["subjectFilter"];
                if (!ValidateRequiredObject(
                        filter,
                        path + "[" + i + "].subjectFilter",
                        SubjectFilterProperties,
                        issues))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateEntryRouteArrays(
            JToken token,
            string path,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (token is not JArray rows)
                return false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i]?["routeIds"] is not JArray)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_PAYLOAD_SHAPE_INVALID",
                        path + "[" + i + "].routeIds",
                        "Entry route IDs must be an array."));
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateObject(
            JObject value,
            string path,
            string[] properties,
            List<LevelWorldObjectAuthoringIssue> issues)
        {
            if (value == null)
            {
                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_SHAPE_INVALID",
                    path,
                    "Required object is missing."));
                return false;
            }

            for (int i = 0; i < properties.Length; i++)
            {
                string propertyName = properties[i];
                if (!value.TryGetValue(
                        propertyName,
                        StringComparison.Ordinal,
                        out JToken token)
                    || token == null
                    || token.Type == JTokenType.Null)
                {
                    issues.Add(Error(
                        "CT_TRIGGER_PAYLOAD_FIELD_MISSING",
                        path + "." + propertyName,
                        "Required payload property is missing or null."));
                    return false;
                }
            }

            foreach (JProperty property in value.Properties())
            {
                bool found = false;
                for (int i = 0; i < properties.Length; i++)
                {
                    if (string.Equals(
                            property.Name,
                            properties[i],
                            StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                    continue;

                issues.Add(Error(
                    "CT_TRIGGER_PAYLOAD_FIELD_UNKNOWN",
                    path + "." + property.Name,
                    "Unknown payload property is not allowed."));
                return false;
            }
            return true;
        }

        private static string TrimRootPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "$")
                return string.Empty;
            return path[0] == '$' ? path.Substring(1) : "." + path;
        }

        private static LevelWorldObjectAuthoringIssue Error(
            string code,
            string path,
            string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }
    }
}
