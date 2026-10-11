using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterAction
{
    public static partial class CharacterActionJsonImporter
    {
        public const string CurrentActionLibrarySchemaVersion = "0.5.0";
        public const string CurrentProjectionSchemaVersion = "0.6.0";

        // Compatibility alias for callers that specifically author/import character_actions.json.
        public const string CurrentSchemaVersion = CurrentActionLibrarySchemaVersion;
        public const string DefaultProfilesSourcePath = "character_action_profile.json";
        public const string DefaultReactionProfilesSourcePath = "character_reaction_profiles.json";
        public const string DefaultLandingProfilesSourcePath = "character_landing_profiles.json";
        public const string DefaultActionSetsSourcePath = "character_action_sets.json";
        public const string DefaultActionsSourcePath = "character_actions.json";

        public static CharacterActionJsonImportResult Import(
            string actionSetsJson,
            string actionsJson,
            string actionSetsSourcePath = DefaultActionSetsSourcePath,
            string actionsSourcePath = DefaultActionsSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterActionSetConfig[] actionSets = ImportActionSets(actionSetsJson, actionSetsSourcePath, diagnostics);
            CharacterActionConfig[] actions = ImportActions(actionsJson, actionsSourcePath, diagnostics);

            ValidateActionSetReferences(actionSets, actions, actionSetsSourcePath, diagnostics);
            Sort(actionSets);
            Sort(actions);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);

            return new CharacterActionJsonImportResult(actionSets, actions, sortedDiagnostics);
        }

        public static CharacterActionJsonImportResult Import(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            string actionSetsSourcePath,
            string actionsSourcePath,
            string reactionProfilesSourcePath)
        {
            return Import(
                actionSetsJson,
                actionsJson,
                reactionProfilesJson,
                string.Empty,
                actionSetsSourcePath,
                actionsSourcePath,
                reactionProfilesSourcePath,
                DefaultLandingProfilesSourcePath);
        }

        public static CharacterActionJsonImportResult Import(
            string actionSetsJson,
            string actionsJson,
            string reactionProfilesJson,
            string landingProfilesJson,
            string actionSetsSourcePath,
            string actionsSourcePath,
            string reactionProfilesSourcePath,
            string landingProfilesSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterActionSetConfig[] actionSets = ImportActionSets(actionSetsJson, actionSetsSourcePath, diagnostics);
            CharacterActionConfig[] actions = ImportActions(actionsJson, actionsSourcePath, diagnostics);
            CharacterReactionProfile[] reactionProfiles = ImportReactionProfiles(
                reactionProfilesJson,
                reactionProfilesSourcePath,
                diagnostics);
            CharacterLandingProfile[] landingProfiles = string.IsNullOrWhiteSpace(landingProfilesJson)
                ? Array.Empty<CharacterLandingProfile>()
                : ImportLandingProfiles(landingProfilesJson, landingProfilesSourcePath, diagnostics);

            ValidateActionSetReferences(actionSets, actions, actionSetsSourcePath, diagnostics);
            Sort(actionSets);
            Sort(actions);
            Sort(reactionProfiles);
            Sort(landingProfiles);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);

            return new CharacterActionJsonImportResult(
                actionSets,
                actions,
                reactionProfiles,
                landingProfiles,
                sortedDiagnostics);
        }

        public static CharacterActionJsonImportResult ImportActionSets(
            string actionSetsJson,
            string sourcePath = DefaultActionSetsSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterActionSetConfig[] actionSets = ImportActionSets(actionSetsJson, sourcePath, diagnostics);
            Sort(actionSets);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);
            return new CharacterActionJsonImportResult(actionSets, Array.Empty<CharacterActionConfig>(), sortedDiagnostics);
        }

        public static CharacterActionJsonImportResult ImportActions(
            string actionsJson,
            string sourcePath = DefaultActionsSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterActionConfig[] actions = ImportActions(actionsJson, sourcePath, diagnostics);
            Sort(actions);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);
            return new CharacterActionJsonImportResult(Array.Empty<CharacterActionSetConfig>(), actions, sortedDiagnostics);
        }

        public static CharacterActionJsonImportResult ImportReactionProfiles(
            string reactionProfilesJson,
            string sourcePath = DefaultReactionProfilesSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterReactionProfile[] reactionProfiles = ImportReactionProfiles(reactionProfilesJson, sourcePath, diagnostics);
            Sort(reactionProfiles);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);
            return new CharacterActionJsonImportResult(
                Array.Empty<CharacterActionSetConfig>(),
                Array.Empty<CharacterActionConfig>(),
                reactionProfiles,
                sortedDiagnostics);
        }

        public static CharacterActionJsonImportResult ImportLandingProfiles(
            string landingProfilesJson,
            string sourcePath = DefaultLandingProfilesSourcePath)
        {
            var diagnostics = new List<CharacterActionJsonDiagnostic>();
            CharacterLandingProfile[] landingProfiles =
                ImportLandingProfiles(landingProfilesJson, sourcePath, diagnostics);
            Sort(landingProfiles);
            CharacterActionJsonDiagnostic[] sortedDiagnostics = diagnostics.ToArray();
            Array.Sort(sortedDiagnostics, CompareDiagnostics);
            return new CharacterActionJsonImportResult(
                Array.Empty<CharacterActionSetConfig>(),
                Array.Empty<CharacterActionConfig>(),
                Array.Empty<CharacterReactionProfile>(),
                landingProfiles,
                sortedDiagnostics);
        }

        private static CharacterLandingProfile[] ImportLandingProfiles(
            string json,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            JObject root = ParseRoot(json, sourcePath, diagnostics);
            if (root == null)
                return Array.Empty<CharacterLandingProfile>();

            JArray profiles = root["landingProfiles"] as JArray;
            if (profiles == null)
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    "$.landingProfiles",
                    CharacterActionDiagnosticCodes.JsonMissingRootArray,
                    "character_landing_profiles.json must contain a landingProfiles array.");
                return Array.Empty<CharacterLandingProfile>();
            }

            var results = new List<CharacterLandingProfile>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Count; i++)
            {
                JObject item = profiles[i] as JObject;
                string path = "$.landingProfiles[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Landing profile entry must be an object.");
                    continue;
                }

                string stableId = ReadString(item, "stableId");
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    AddError(diagnostics, sourcePath, path + ".stableId", CharacterActionDiagnosticCodes.JsonInvalidConfig, "Landing profile stable id must be non-empty.");
                    continue;
                }
                if (!stableIds.Add(stableId))
                {
                    AddError(diagnostics, sourcePath, path + ".stableId", CharacterActionDiagnosticCodes.JsonDuplicateStableId, "Duplicate landing profile stable id '" + stableId + "'.");
                    continue;
                }

                try
                {
                    int lightThreshold = item["lightLandingMinDownwardSpeedMillimetersPerSecond"] != null
                        ? ReadInt(item, "lightLandingMinDownwardSpeedMillimetersPerSecond")
                        : CharacterLandingProfile.DefaultLightLandingMinDownwardSpeedMillimetersPerSecond;
                    int threshold = item["heavyLandingMinDownwardSpeedMillimetersPerSecond"] != null
                        ? ReadInt(item, "heavyLandingMinDownwardSpeedMillimetersPerSecond")
                        : CharacterLandingProfile.DefaultHeavyLandingMinDownwardSpeedMillimetersPerSecond;
                    results.Add(new CharacterLandingProfile(
                        stableId,
                        ReadActionId(item, "lightLandingActionId"),
                        ReadActionId(item, "heavyLandingActionId"),
                        threshold,
                        lightThreshold));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Landing profile could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterReactionProfile[] ImportReactionProfiles(
            string json,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            JObject root = ParseRoot(json, sourcePath, diagnostics);
            if (root == null)
                return Array.Empty<CharacterReactionProfile>();

            JArray profiles = root["reactionProfiles"] as JArray;
            if (profiles == null)
            {
                AddError(diagnostics, sourcePath, "$.reactionProfiles", CharacterActionDiagnosticCodes.JsonMissingRootArray, "character_reaction_profiles.json must contain a reactionProfiles array.");
                return Array.Empty<CharacterReactionProfile>();
            }

            var results = new List<CharacterReactionProfile>();
            var stableIds = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Count; i++)
            {
                JObject item = profiles[i] as JObject;
                string path = "$.reactionProfiles[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Reaction profile entry must be an object.");
                    continue;
                }

                string stableId = ReadString(item, "stableId");
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    AddError(diagnostics, sourcePath, path + ".stableId", CharacterActionDiagnosticCodes.JsonInvalidConfig, "Reaction profile stable id must be non-empty.");
                }
                else if (stableIds.ContainsKey(stableId))
                {
                    AddError(diagnostics, sourcePath, path + ".stableId", CharacterActionDiagnosticCodes.JsonDuplicateStableId, "Duplicate reaction profile stable id '" + stableId + "'.");
                }
                else
                {
                    stableIds.Add(stableId, path);
                }

                try
                {
                    results.Add(new CharacterReactionProfile(
                        stableId,
                        ReadReactionRules(item["rules"] as JArray, sourcePath, path + ".rules", diagnostics),
                        ReadActionId(item, "defaultActionId")));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Reaction profile could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterReactionRule[] ReadReactionRules(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterReactionRule>();

            var results = new List<CharacterReactionRule>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Reaction rule must be an object.");
                    continue;
                }

                bool parsed = TryReadEnum(
                    item,
                    "trigger",
                    sourcePath,
                    path + ".trigger",
                    CharacterReactionRuleTrigger.Any,
                    diagnostics,
                    out CharacterReactionRuleTrigger trigger);
                parsed &= TryReadNullableEnum(
                    item,
                    "currentScatterPressureBand",
                    sourcePath,
                    path + ".currentScatterPressureBand",
                    diagnostics,
                    out PressureBand? currentBand);
                parsed &= TryReadNullableEnum(
                    item,
                    "currentPhase",
                    sourcePath,
                    path + ".currentPhase",
                    diagnostics,
                    out CharacterActionPhaseKind? currentPhase);
                parsed &= TryReadNullableEnum(
                    item,
                    "hitDirection",
                    sourcePath,
                    path + ".hitDirection",
                    diagnostics,
                    out CharacterHitDirection? hitDirection);
                if (!parsed)
                    continue;

                try
                {
                    results.Add(new CharacterReactionRule(
                        ReadActionId(item, "actionId"),
                        trigger,
                        ReadBool(item, "requiresBodyPart"),
                        ReadBool(item, "requiresHitZone"),
                        ReadBool(item, "requiresHitDirection"),
                        ReadBool(item, "requiresDamageType"),
                        ReadBool(item, "requiresReactionGroup"),
                        currentBand,
                        ReadNullableBool(item, "isDeath"),
                        ReadNullableBool(item, "isAirborne"),
                        currentPhase,
                        ReadNullableBool(item, "currentActionCommitted"),
                        ReadNullableBool(item, "currentActionInterruptible"),
                        ReadInt(item, "priority"),
                        ReadDefinitionId(item, "bodyPartId"),
                        ReadDefinitionId(item, "hitZoneId"),
                        ReadDefinitionId(item, "damageTypeId"),
                        hitDirection,
                        ReadNullableInt(item, "minImpactForce"),
                        ReadNullableInt(item, "maxImpactForce"),
                        ReadDefinitionId(item, "reactionGroupId"),
                        ReadBool(item, "requiresImpactForce")));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Reaction rule could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterActionSetConfig[] ImportActionSets(
            string json,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            JObject root = ParseRoot(json, sourcePath, diagnostics);
            if (root == null)
                return Array.Empty<CharacterActionSetConfig>();

            string schemaVersion = ReadString(root, "schemaVersion");
            if (!string.Equals(schemaVersion, CurrentProjectionSchemaVersion, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    "$.schemaVersion",
                    CharacterActionDiagnosticCodes.JsonInvalidDocument,
                    "character_action_sets.json requires schemaVersion " + CurrentProjectionSchemaVersion + ".");
                return Array.Empty<CharacterActionSetConfig>();
            }

            JArray actionSets = root["actionSets"] as JArray;
            if (actionSets == null)
            {
                AddError(diagnostics, sourcePath, "$.actionSets", CharacterActionDiagnosticCodes.JsonMissingRootArray, "character_action_sets.json must contain an actionSets array.");
                return Array.Empty<CharacterActionSetConfig>();
            }

            var results = new List<CharacterActionSetConfig>();
            var ids = new Dictionary<int, string>();
            var stableIds = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < actionSets.Count; i++)
            {
                JObject item = actionSets[i] as JObject;
                string path = "$.actionSets[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Action set entry must be an object.");
                    continue;
                }

                int id = ReadInt(item, "id");
                string stableId = ReadString(item, "stableId");
                CheckIdentity(id, stableId, sourcePath, path, ids, stableIds, diagnostics);

                try
                {
                    results.Add(new CharacterActionSetConfig(
                        id,
                        stableId,
                        ReadString(item, "displayName"),
                        ReadString(item, "characterStableId"),
                        ReadString(item, "equipmentStateStableId"),
                        ReadCommandBindings(item["commandBindings"] as JArray, sourcePath, path + ".commandBindings", diagnostics),
                        ReadAbilityBindings(item["abilityBindings"] as JArray, sourcePath, path + ".abilityBindings", diagnostics),
                        ReadReactionBindings(item["reactionBindings"] as JArray, sourcePath, path + ".reactionBindings", diagnostics),
                        ReadDefinitionId(item, "movementProfileId"),
                        ReadString(item, "reactionProfileId"),
                        ReadActionId(item, "defaultActionId"),
                        ReadInt(item, "defaultActionSlotMask"),
                        ReadString(item, "landingProfileId"),
                        ReadCommandSequences(item["commandSequences"] as JArray, sourcePath, path + ".commandSequences", diagnostics)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Action set could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterActionConfig[] ImportActions(
            string json,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            JObject root = ParseRoot(json, sourcePath, diagnostics);
            if (root == null)
                return Array.Empty<CharacterActionConfig>();

            string schemaVersion = ReadString(root, "schemaVersion");
            if (!string.Equals(schemaVersion, CurrentActionLibrarySchemaVersion, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    sourcePath,
                    "$.schemaVersion",
                    CharacterActionDiagnosticCodes.JsonInvalidDocument,
                    "character_actions.json requires schemaVersion " + CurrentActionLibrarySchemaVersion + ".");
                return Array.Empty<CharacterActionConfig>();
            }

            JArray actions = root["actions"] as JArray;
            if (actions == null)
            {
                AddError(diagnostics, sourcePath, "$.actions", CharacterActionDiagnosticCodes.JsonMissingRootArray, "character_actions.json must contain an actions array.");
                return Array.Empty<CharacterActionConfig>();
            }

            var results = new List<CharacterActionConfig>();
            var ids = new Dictionary<int, string>();
            for (int i = 0; i < actions.Count; i++)
            {
                JObject item = actions[i] as JObject;
                string path = "$.actions[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Action entry must be an object.");
                    continue;
                }

                int actionId = ReadInt(item, "actionId");
                CheckActionIdentity(actionId, sourcePath, path, ids, diagnostics);

                bool parsed = TryReadEnum(item, "category", sourcePath, path + ".category", CharacterActionCategory.None, diagnostics, out CharacterActionCategory category);
                parsed &= TryReadEnum(item, "timelineAuthority", sourcePath, path + ".timelineAuthority", CharacterActionTimelineAuthority.CharacterAuthored, diagnostics, out CharacterActionTimelineAuthority timelineAuthority);
                if (!parsed)
                    continue;

                try
                {
                    results.Add(new CharacterActionConfig(
                        new CharacterActionId(actionId),
                        ReadString(item, "displayName"),
                        category,
                        timelineAuthority,
                        ReadStringArray(item["tags"] as JArray),
                        ReadInt(item, "priority"),
                        ReadNullableInt(item, "durationFrames"),
                        ReadRequirements(item["requirements"] as JArray, sourcePath, path + ".requirements", diagnostics),
                        ReadPhases(item["phases"] as JArray, sourcePath, path + ".phases", diagnostics),
                        ReadCancelRules(item["cancelRules"] as JArray, sourcePath, path + ".cancelRules", diagnostics),
                        ReadInterruptRules(item["interruptRules"] as JArray, sourcePath, path + ".interruptRules", diagnostics),
                        ReadMotionTrack(item["motionTrack"] as JObject, sourcePath, path + ".motionTrack", diagnostics),
                        ReadCombatTrack(item["combatTrack"] as JObject, sourcePath, path + ".combatTrack", diagnostics),
                        ReadGameplayTrack(item["gameplayTrack"] as JObject, sourcePath, path + ".gameplayTrack", diagnostics),
                        ReadAnimationTrack(item["animationTrack"] as JObject, sourcePath, path + ".animationTrack", diagnostics),
                        ReadPresentationTrack(item["presentationTrack"] as JObject, sourcePath, path + ".presentationTrack", diagnostics),
                        ReadDebugTrack(item["debugTrack"] as JObject, sourcePath, path + ".debugTrack", diagnostics),
                        ReadCohesionSupport(item["cohesionSupport"] as JObject, sourcePath, path + ".cohesionSupport", diagnostics)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Action could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterActionCohesionSupportConfig ReadCohesionSupport(
            JObject fields,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (fields == null)
                return CharacterActionCohesionSupportConfig.None;

            try
            {
                return new CharacterActionCohesionSupportConfig(
                    ReadBool(fields, "enabled"),
                    ReadFloat(fields["supportRate"], 1f),
                    ReadFloat(fields["minSupportRatio"], 1f),
                    ReadBool(fields, "hasCohesionSupportWindow"),
                    ReadInt(fields, "cohesionSupportWindowStartFrame"),
                    ReadInt(fields, "cohesionSupportWindowEndFrame"));
            }
            catch (Exception ex) when (IsConfigException(ex))
            {
                AddError(diagnostics, sourcePath, jsonPath, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Cohesion support config could not be converted to runtime config.");
                return CharacterActionCohesionSupportConfig.None;
            }
        }

        private static CharacterActionBinding[] ReadCommandBindings(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterActionBinding>();

            var results = new List<CharacterActionBinding>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Command binding must be an object.");
                    continue;
                }

                try
                {
                    results.Add(new CharacterActionBinding(
                        ReadDefinitionId(item, "intentId"),
                        ReadActionId(item, "actionId"),
                        ReadInt(item, "priority"),
                        ReadBool(item, "allowQueue"),
                        ReadInt(item, "queueWindowFrames")));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Command binding could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterActionCommandSequence[] ReadCommandSequences(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterActionCommandSequence>();

            var results = new List<CharacterActionCommandSequence>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Command sequence must be an object.");
                    continue;
                }

                try
                {
                    results.Add(new CharacterActionCommandSequence(
                        ReadDefinitionId(item, "intentId"),
                        ReadActionIdArray(item["actionIds"] as JArray),
                        ReadBool(item, "wrapAfterLast"),
                        ReadInt(item, "priority"),
                        ReadBool(item, "allowQueue"),
                        ReadInt(item, "queueWindowFrames")));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Command sequence could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static CharacterAbilityActionBinding[] ReadAbilityBindings(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterAbilityActionBinding>();

            var results = new List<CharacterAbilityActionBinding>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Ability binding must be an object.");
                    continue;
                }

                results.Add(new CharacterAbilityActionBinding(
                    ReadInt(item, "abilityId"),
                    ReadActionId(item, "actionId"),
                    ReadStringArray(item["requiredTags"] as JArray),
                    ReadStringArray(item["forbiddenTags"] as JArray)));
            }

            return results.ToArray();
        }

        private static CharacterReactionBinding[] ReadReactionBindings(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterReactionBinding>();

            var results = new List<CharacterReactionBinding>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidDocument, "Reaction binding must be an object.");
                    continue;
                }

                results.Add(new CharacterReactionBinding(
                    ReadString(item, "reactionProfileId"),
                    ReadActionId(item, "defaultActionId"),
                    ReadStringArray(item["requiredTags"] as JArray),
                    ReadStringArray(item["forbiddenTags"] as JArray)));
            }

            return results.ToArray();
        }

        private static CharacterActionRequirement[] ReadRequirements(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterActionRequirement>();

            var results = new List<CharacterActionRequirement>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionRequirementKind.None, diagnostics, out CharacterActionRequirementKind kind))
                    continue;
                var requirement = new CharacterActionRequirement(kind, ReadString(item, "stableId"), ReadInt(item, "value"), ReadDefinitionId(item, "referenceId"));
                if (!requirement.HasValidReferenceShape())
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "Requirement numeric reference, value or source label is invalid. Prepare identities before importing.");
                    continue;
                }
                results.Add(requirement);
            }

            return results.ToArray();
        }

        private static CharacterActionPhase[] ReadPhases(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterActionPhase>();

            var results = new List<CharacterActionPhase>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                    continue;

                bool parsed = TryReadEnum(item, "kind", sourcePath, path + ".kind", CharacterActionPhaseKind.None, diagnostics, out CharacterActionPhaseKind kind);
                parsed &= TryReadEnum(item, "combatPhaseAnchor", sourcePath, path + ".combatPhaseAnchor", CombatActionPhase.None, diagnostics, out CombatActionPhase anchor);
                if (!parsed)
                    continue;

                try
                {
                    results.Add(new CharacterActionPhase(
                        kind,
                        ReadInt(item, "startFrame"),
                        ReadInt(item, "endFrame"),
                        anchor,
                        ReadBool(item, "requiresCombatPhaseMatch", fallback: true)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Phase frame range is invalid.");
                }
            }

            return results.ToArray();
        }

        private static CharacterCancelRule[] ReadCancelRules(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterCancelRule>();

            var results = new List<CharacterCancelRule>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                    continue;

                try
                {
                    results.Add(new CharacterCancelRule(
                        ReadInt(item, "startFrame"),
                        ReadInt(item, "endFrame"),
                        (CharacterActionRequestType)ReadInt(item, "requestTypeMask"),
                        (CharacterCancelInputMask)ReadInt(item, "actionSlotMask")));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidFrame, "Cancel rule frame range is invalid.");
                }
            }

            return results.ToArray();
        }

        private static CharacterInterruptRule[] ReadInterruptRules(
            JArray array,
            string sourcePath,
            string jsonPath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            if (array == null)
                return Array.Empty<CharacterInterruptRule>();

            var results = new List<CharacterInterruptRule>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                string path = jsonPath + "[" + i + "]";
                if (item == null)
                    continue;
                if (!TryReadEnum(item, "sourceKind", sourcePath, path + ".sourceKind", CharacterActionSourceKind.Command, diagnostics, out CharacterActionSourceKind sourceKind))
                    continue;

                try
                {
                    results.Add(new CharacterInterruptRule(
                        sourceKind,
                        ReadInt(item, "minimumPriority"),
                        new CharacterActionId(ReadInt(item, "targetActionId")),
                        ReadBool(item, "allow", fallback: true)));
                }
                catch (Exception ex) when (IsConfigException(ex))
                {
                    AddError(diagnostics, sourcePath, path, CharacterActionDiagnosticCodes.JsonInvalidConfig, "Interrupt rule could not be converted to runtime config.");
                }
            }

            return results.ToArray();
        }

        private static void ValidateActionSetReferences(
            CharacterActionSetConfig[] actionSets,
            CharacterActionConfig[] actions,
            string sourcePath,
            List<CharacterActionJsonDiagnostic> diagnostics)
        {
            var actionIds = new HashSet<CharacterActionId>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null && actions[i].ActionId.IsValid)
                    actionIds.Add(actions[i].ActionId);
            }

            for (int setIndex = 0; setIndex < actionSets.Length; setIndex++)
            {
                CharacterActionSetConfig set = actionSets[setIndex];
                if (set == null)
                    continue;

                for (int bindingIndex = 0; bindingIndex < set.CommandBindings.Length; bindingIndex++)
                {
                    CharacterActionId actionId = set.CommandBindings[bindingIndex] != null
                        ? set.CommandBindings[bindingIndex].ActionId
                        : CharacterActionId.None;
                    if (!actionId.IsValid || actionIds.Contains(actionId))
                        continue;

                    AddError(
                        diagnostics,
                        sourcePath,
                        "$.actionSets[" + setIndex + "].commandBindings[" + bindingIndex + "].actionId",
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Command binding action '" + actionId + "' is missing.");
                }

                for (int sequenceIndex = 0; sequenceIndex < set.CommandSequences.Length; sequenceIndex++)
                {
                    CharacterActionCommandSequence sequence = set.CommandSequences[sequenceIndex];
                    if (sequence == null)
                        continue;

                    for (int actionIndex = 0; actionIndex < sequence.ActionIds.Length; actionIndex++)
                    {
                        CharacterActionId actionId = sequence.ActionIds[actionIndex];
                        if (!actionId.IsValid || actionIds.Contains(actionId))
                            continue;

                        AddError(
                            diagnostics,
                            sourcePath,
                            "$.actionSets[" + setIndex + "].commandSequences[" + sequenceIndex + "].actionIds[" + actionIndex + "]",
                            CharacterActionDiagnosticCodes.MissingActionConfig,
                            "Command sequence action '" + actionId + "' is missing.");
                    }
                }
            }
        }

    }
}
