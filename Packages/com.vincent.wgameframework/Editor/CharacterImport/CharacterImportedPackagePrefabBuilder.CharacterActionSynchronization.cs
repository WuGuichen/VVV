using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private static void SynchronizeCharacterActionProfile(CharacterImportedPackage package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.PackageId))
                return;

            if (UsesCompilerOwnedCharacterActionProjection(package, out string schemaVersion))
            {
                Debug.Log("MxFramework Character Actions legacy synchronization skipped: package="
                    + package.PackageId.Trim()
                    + " characterApplicationSchema=" + schemaVersion
                    + ". Schema 8+ projections are owned by the Authoring compiler.");
                return;
            }

            string packageId = package.PackageId.Trim();
            string sourceFolder = SanitizeName(packageId);
            string profileDirectory = GeneratedCharacterActionsRoot + "/Profiles/" + sourceFolder;
            string actionSetDirectory = GeneratedCharacterActionsRoot + "/ActionSets/" + sourceFolder;
            string profilePath = profileDirectory + "/" + CharacterActionJsonImporter.DefaultProfilesSourcePath;
            string actionSetPath = actionSetDirectory + "/" + CharacterActionJsonImporter.DefaultActionSetsSourcePath;
            string reactionProfilesPath = actionSetDirectory + "/" + CharacterActionJsonImporter.DefaultReactionProfilesSourcePath;
            string actionLibraryDirectory = GeneratedCharacterActionsRoot + "/Libraries/" + DefaultCharacterActionLibraryId;
            string actionLibraryPath = actionLibraryDirectory + "/" + CharacterActionJsonImporter.DefaultActionsSourcePath;
            string profileStableId = packageId + ".default";
            string reactionProfileStableId = "reaction." + sourceFolder + ".default";
            string displayName = FormatCharacterActionDisplayName(packageId);
            string characterStableId = ResolveCharacterStableId(package);
            ImportedActionSetBinding[] importedActionSets = BuildImportedActionSetBindings(package, profileStableId, displayName);
            ImportedReactionSlot[] reactionSlots = BuildImportedReactionSlots(package, sourceFolder);
            EnsureReactionActionIds(
                reactionSlots,
                DefaultCharacterActionLibraryId,
                actionLibraryPath);

            EnsureFolderPath(profileDirectory);
            EnsureFolderPath(actionSetDirectory);
            EnsureFolderPath(actionLibraryDirectory);

            bool profileChanged = SynchronizeProfileDocument(profilePath, profileStableId, importedActionSets, displayName);
            bool actionSetChanged = SynchronizeActionSetDocument(
                actionSetPath,
                profileStableId,
                importedActionSets,
                characterStableId,
                reactionProfileStableId);
            bool createReactionSources = !File.Exists(reactionProfilesPath);
            bool reactionProfileChanged = createReactionSources
                && SynchronizeReactionProfileDocument(reactionProfilesPath, reactionProfileStableId, displayName, reactionSlots);
            bool reactionActionsChanged = createReactionSources
                && SynchronizeReactionActionsDocument(actionLibraryPath, sourceFolder, reactionSlots);
            if (!profileChanged && !actionSetChanged && !reactionProfileChanged && !reactionActionsChanged)
                return;

            if (profileChanged)
                AssetDatabase.ImportAsset(profileDirectory, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            if (actionSetChanged || reactionProfileChanged)
                AssetDatabase.ImportAsset(actionSetDirectory, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            if (reactionActionsChanged)
                AssetDatabase.ImportAsset(actionLibraryDirectory, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("MxFramework Character Actions synchronized: package=" + packageId
                + " profile=" + profileDirectory
                + " actionSet=" + actionSetDirectory
                + " reactionProfile=" + reactionProfileStableId);
        }

        private static bool UsesCompilerOwnedCharacterActionProjection(
            CharacterImportedPackage package,
            out string schemaVersion)
        {
            schemaVersion = string.Empty;
            if (package == null || string.IsNullOrWhiteSpace(package.RootPath))
                return false;

            string applicationPath = Path.Combine(
                package.RootPath,
                "package_cache",
                "character_application.json");
            if (!File.Exists(applicationPath))
                return false;

            try
            {
                JObject application = JObject.Parse(File.ReadAllText(applicationPath));
                schemaVersion = application.Value<string>("schemaVersion")?.Trim() ?? string.Empty;
                int separator = schemaVersion.IndexOf('.');
                string majorText = separator >= 0 ? schemaVersion.Substring(0, separator) : schemaVersion;
                return int.TryParse(majorText, out int majorVersion) && majorVersion >= 8;
            }
            catch (Exception ex)
            {
                schemaVersion = "unreadable";
                Debug.LogWarning("MxFramework Character Actions legacy synchronization skipped because "
                    + applicationPath
                    + " could not be read safely. Existing compiler projections were preserved. "
                    + ex.Message);
                return true;
            }
        }

        private static bool SynchronizeProfileDocument(
            string path,
            string profileStableId,
            ImportedActionSetBinding[] importedActionSets,
            string displayName)
        {
            if (!TryReadCharacterActionDocument(path, out CharacterActionProfilesJsonDocument document))
                return false;

            bool changed = false;
            if (!string.Equals(document.SchemaVersion, CharacterActionJsonImporter.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                document.SchemaVersion = CharacterActionJsonImporter.CurrentSchemaVersion;
                changed = true;
            }

            CharacterActionProfileJsonDto[] profiles = document.Profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
            CharacterActionProfileJsonDto profile = FindProfile(profiles, profileStableId);
            string[] actionSetStableIds = GetActionSetStableIds(importedActionSets);
            if (profile == null)
            {
                profile = new CharacterActionProfileJsonDto
                {
                    Id = NextProfileId(profiles),
                    StableId = profileStableId,
                    DisplayName = displayName,
                    ActionSetIds = actionSetStableIds,
                    ActionLibraryIds = new[] { DefaultCharacterActionLibraryId },
                };
                profiles = Append(profiles, profile);
                document.Profiles = profiles;
                changed = true;
            }
            else
            {
                if (profile.Id <= 0)
                {
                    profile.Id = NextProfileId(profiles);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                {
                    profile.DisplayName = displayName;
                    changed = true;
                }

                if (!StringArraysEqual(profile.ActionSetIds, actionSetStableIds))
                {
                    profile.ActionSetIds = actionSetStableIds;
                    changed = true;
                }

                if (EnsureStringReference(profile.ActionLibraryIds, DefaultCharacterActionLibraryId, out string[] actionLibraryIds))
                {
                    profile.ActionLibraryIds = actionLibraryIds;
                    changed = true;
                }
            }

            if (!changed)
                return false;

            File.WriteAllText(path, JsonConvert.SerializeObject(document, CharacterActionAuthoringJsonService.JsonSettings));
            return true;
        }

        private static bool SynchronizeActionSetDocument(
            string path,
            string legacyActionSetStableId,
            ImportedActionSetBinding[] importedActionSets,
            string characterStableId,
            string reactionProfileStableId)
        {
            if (!TryReadCharacterActionDocument(path, out CharacterActionSetsJsonDocument document))
                return false;

            bool changed = false;
            if (!string.Equals(document.SchemaVersion, CharacterActionJsonImporter.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                document.SchemaVersion = CharacterActionJsonImporter.CurrentSchemaVersion;
                changed = true;
            }

            CharacterActionSetJsonDto[] actionSets = document.ActionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            CharacterActionSetJsonDto legacyActionSet = FindActionSet(actionSets, legacyActionSetStableId);
            for (int importedIndex = 0; importedIndex < importedActionSets.Length; importedIndex++)
            {
                ImportedActionSetBinding imported = importedActionSets[importedIndex];
                CharacterActionSetJsonDto actionSet = FindActionSet(actionSets, imported.StableId);
                if (actionSet == null)
                {
                    actionSet = new CharacterActionSetJsonDto
                    {
                        Id = NextActionSetId(actionSets),
                        StableId = imported.StableId,
                        DisplayName = imported.DisplayName,
                        CharacterStableId = characterStableId,
                        EquipmentStateStableId = imported.EquipmentStateStableId,
                        MovementProfileId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetExisting(402, "movement.default"),
                        ReactionProfileId = reactionProfileStableId,
                        DefaultActionId = legacyActionSet?.DefaultActionId ?? 0,
                        DefaultActionSlotMask = legacyActionSet?.DefaultActionSlotMask ?? 0,
                        CommandBindings = legacyActionSet?.CommandBindings ?? CreateDefaultCommandBindings(),
                        AbilityBindings = legacyActionSet?.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>(),
                        ReactionBindings = legacyActionSet?.ReactionBindings ?? Array.Empty<CharacterReactionBindingJsonDto>(),
                    };
                    actionSets = Append(actionSets, actionSet);
                    document.ActionSets = actionSets;
                    changed = true;
                    continue;
                }

                if (actionSet.Id <= 0)
                {
                    actionSet.Id = NextActionSetId(actionSets);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(actionSet.DisplayName))
                {
                    actionSet.DisplayName = imported.DisplayName;
                    changed = true;
                }

                if (!string.Equals(actionSet.CharacterStableId, characterStableId, StringComparison.Ordinal))
                {
                    actionSet.CharacterStableId = characterStableId;
                    changed = true;
                }

                if (!string.Equals(actionSet.EquipmentStateStableId, imported.EquipmentStateStableId, StringComparison.Ordinal))
                {
                    actionSet.EquipmentStateStableId = imported.EquipmentStateStableId;
                    changed = true;
                }

                if (actionSet.MovementProfileId <= 0)
                {
                    actionSet.MovementProfileId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetExisting(402, "movement.default");
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(actionSet.ReactionProfileId)
                    || string.Equals(actionSet.ReactionProfileId, "reaction.default", StringComparison.Ordinal))
                {
                    actionSet.ReactionProfileId = reactionProfileStableId;
                    changed = true;
                }

                if (actionSet.CommandBindings == null)
                {
                    actionSet.CommandBindings = Array.Empty<CharacterActionBindingJsonDto>();
                    changed = true;
                }

                if (actionSet.AbilityBindings == null)
                {
                    actionSet.AbilityBindings = Array.Empty<CharacterAbilityActionBindingJsonDto>();
                    changed = true;
                }

                if (actionSet.ReactionBindings == null)
                {
                    actionSet.ReactionBindings = Array.Empty<CharacterReactionBindingJsonDto>();
                    changed = true;
                }
            }

            int legacyIndex = FindActionSetIndex(actionSets, legacyActionSetStableId);
            if (legacyIndex >= 0 && !ContainsActionSet(importedActionSets, legacyActionSetStableId))
            {
                actionSets = RemoveAt(actionSets, legacyIndex);
                document.ActionSets = actionSets;
                changed = true;
            }

            if (!changed)
                return false;

            File.WriteAllText(path, JsonConvert.SerializeObject(document, CharacterActionAuthoringJsonService.JsonSettings));
            return true;
        }

        private static bool SynchronizeReactionProfileDocument(
            string path,
            string reactionProfileStableId,
            string displayName,
            ImportedReactionSlot[] slots)
        {
            if (!TryReadCharacterActionDocument(path, out CharacterReactionProfilesJsonDocument document))
                return false;

            bool changed = false;
            if (!string.Equals(document.SchemaVersion, CharacterActionJsonImporter.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                document.SchemaVersion = CharacterActionJsonImporter.CurrentSchemaVersion;
                changed = true;
            }

            CharacterReactionProfileJsonDto[] profiles = document.ReactionProfiles ?? Array.Empty<CharacterReactionProfileJsonDto>();
            CharacterReactionProfileJsonDto profile = FindReactionProfile(profiles, reactionProfileStableId);
            if (profile == null)
            {
                profile = new CharacterReactionProfileJsonDto
                {
                    StableId = reactionProfileStableId,
                    DisplayName = displayName + " Reaction",
                    DefaultActionId = GetDefaultReactionActionId(slots),
                    Rules = BuildReactionRules(slots),
                };
                profiles = Append(profiles, profile);
                document.ReactionProfiles = profiles;
                changed = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                {
                    profile.DisplayName = displayName + " Reaction";
                    changed = true;
                }

                int defaultActionId = GetDefaultReactionActionId(slots);
                if (profile.DefaultActionId != defaultActionId)
                {
                    profile.DefaultActionId = defaultActionId;
                    changed = true;
                }

                CharacterReactionRuleJsonDto[] rules = BuildReactionRules(slots);
                if (!ReactionRulesEqual(profile.Rules, rules))
                {
                    profile.Rules = rules;
                    changed = true;
                }
            }

            if (!changed)
                return false;

            File.WriteAllText(path, JsonConvert.SerializeObject(document, CharacterActionAuthoringJsonService.JsonSettings));
            return true;
        }

        private static bool SynchronizeReactionActionsDocument(
            string path,
            string packageSegment,
            ImportedReactionSlot[] slots)
        {
            if (!TryReadCharacterActionDocument(path, out CharacterActionsJsonDocument document))
                return false;

            bool changed = false;
            if (!string.Equals(document.SchemaVersion, CharacterActionJsonImporter.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                document.SchemaVersion = CharacterActionJsonImporter.CurrentSchemaVersion;
                changed = true;
            }

            CharacterActionJsonDto[] actions = document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                ImportedReactionSlot slot = slots[i];
                if (slot.ActionId <= 0)
                    continue;

                CharacterActionJsonDto action = FindAction(actions, slot.ActionId);
                CharacterActionJsonDto generated = CreateReactionAction(packageSegment, slot, action?.ActionId ?? NextActionId(actions));
                if (action == null)
                {
                    actions = Append(actions, generated);
                    document.Actions = actions;
                    changed = true;
                    continue;
                }

                if (ApplyReactionAction(action, generated))
                    changed = true;
            }

            if (!changed)
                return false;

            File.WriteAllText(path, JsonConvert.SerializeObject(document, CharacterActionAuthoringJsonService.JsonSettings));
            return true;
        }

        private static ImportedReactionSlot[] BuildImportedReactionSlots(CharacterImportedPackage package, string packageSegment)
        {
            var slots = new List<ImportedReactionSlot>(8);
            AddDefaultReactionSlots(slots, packageSegment);

            JObject application = ReadImportedCharacterApplicationConfig(package);
            JArray profiles = application?["reactionProfiles"] as JArray;
            for (int profileIndex = 0; profiles != null && profileIndex < profiles.Count; profileIndex++)
            {
                JObject profile = profiles[profileIndex] as JObject;
                JArray configuredSlots = profile?["slots"] as JArray;
                for (int slotIndex = 0; configuredSlots != null && slotIndex < configuredSlots.Count; slotIndex++)
                {
                    JObject slot = configuredSlots[slotIndex] as JObject;
                    string rawSlotId = ReadString(slot, "slotId");
                    string slotId = NormalizeLegacyReactionSlotId(rawSlotId);
                    if (string.IsNullOrWhiteSpace(slotId))
                        continue;

                    int existing = IndexOfReactionSlot(slots, slotId);
                    ImportedReactionSlot current = existing >= 0
                        ? slots[existing]
                        : CreateDefaultReactionSlot(packageSegment, slotId);
                    current.DisplayName = FirstNonEmpty(ReadString(slot, "displayName"), current.DisplayName);
                    current.Trigger = FirstNonEmpty(ReadString(slot, "trigger"), current.Trigger);
                    current.PressureBand = FirstNonEmpty(ReadString(slot, "pressureBand"), current.PressureBand);
                    if (slot["reactionGroupId"] != null)
                    {
                        JToken reactionGroup = slot["reactionGroupId"];
                        if (reactionGroup.Type != JTokenType.Integer || (long)reactionGroup < 0 || (long)reactionGroup > 4)
                            throw new InvalidDataException("Reaction group must be a declared integer category.");
                        current.ReactionGroupId = (int)reactionGroup;
                    }
                    current.MinImpactForce = ReadNullableInt(slot, "minImpactForce", current.MinImpactForce);
                    current.MaxImpactForce = ReadNullableInt(slot, "maxImpactForce", current.MaxImpactForce);
                    if (!IsLegacyReactionSlotId(rawSlotId))
                        current.ActionId = ReadInt(slot, "actionId", current.ActionId);
                    current.DurationFrames = ReadInt(slot, "durationFrames", current.DurationFrames);
                    current.Priority = ReadInt(slot, "priority", current.Priority);
                    current.ResourceKey = ResolveImportedReactionResourceKey(slot, current.ResourceKey);
                    if (existing >= 0)
                        slots[existing] = current;
                    else
                        slots.Add(current);
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                ImportedReactionSlot slot = slots[i];
                if (slot.ActionId <= 0)
                    slot.ActionId = BuildReactionActionId(packageSegment, slot.SlotId);
                if (slot.DurationFrames <= 0)
                    slot.DurationFrames = 1;
                slots[i] = slot;
            }

            return slots.ToArray();
        }

        private static void AddDefaultReactionSlots(List<ImportedReactionSlot> slots, string packageSegment)
        {
            slots.Add(new ImportedReactionSlot(
                DefaultReactionProfileSlotId,
                "Cohesion Break",
                CharacterReactionRuleTrigger.CohesionBreak.ToString(),
                "Broken",
                0,
                null,
                null,
                BuildReactionActionId(packageSegment, "cohesion_break_react"),
                18,
                120,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitHigh",
                "High Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                1,
                null,
                59,
                BuildReactionActionId(packageSegment, "hit_high_react"),
                12,
                90,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitMid",
                "Mid Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                2,
                null,
                59,
                BuildReactionActionId(packageSegment, "hit_mid_react"),
                12,
                89,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitLow",
                "Low Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                3,
                null,
                59,
                BuildReactionActionId(packageSegment, "hit_low_react"),
                12,
                88,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitHighStrong",
                "High Strong Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                1,
                60,
                null,
                BuildReactionActionId(packageSegment, "hit_high_strong_react"),
                18,
                110,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitMidStrong",
                "Mid Strong Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                2,
                60,
                null,
                BuildReactionActionId(packageSegment, "hit_mid_strong_react"),
                18,
                109,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "hitLowStrong",
                "Low Strong Hit",
                CharacterReactionRuleTrigger.Hit.ToString(),
                "Broken",
                3,
                60,
                null,
                BuildReactionActionId(packageSegment, "hit_low_strong_react"),
                18,
                108,
                string.Empty));
            slots.Add(new ImportedReactionSlot(
                "death",
                "Death",
                CharacterReactionRuleTrigger.Death.ToString(),
                string.Empty,
                0,
                null,
                null,
                BuildReactionActionId(packageSegment, "death_react"),
                60,
                200,
                DefaultDeathReactionAnimationResourceKey));
        }

        private static ImportedReactionSlot CreateDefaultReactionSlot(string packageSegment, string slotId)
        {
            string normalizedSlot = NormalizeReactionSlotSegment(slotId);
            return new ImportedReactionSlot(
                slotId,
                string.IsNullOrWhiteSpace(slotId) ? "Reaction" : slotId,
                CharacterReactionRuleTrigger.Hit.ToString(),
                string.Empty,
                0,
                null,
                null,
                BuildReactionActionId(packageSegment, normalizedSlot + "_react"),
                18,
                50,
                string.Empty);
        }

        private static string NormalizeLegacyReactionSlotId(string slotId)
        {
            if (string.Equals(slotId, "hitLight", StringComparison.Ordinal))
                return "hitMid";
            if (string.Equals(slotId, "hitHeavy", StringComparison.Ordinal))
                return "hitMidStrong";
            return slotId ?? string.Empty;
        }

        private static bool IsLegacyReactionSlotId(string slotId)
        {
            return string.Equals(slotId, "hitLight", StringComparison.Ordinal)
                || string.Equals(slotId, "hitHeavy", StringComparison.Ordinal);
        }

        private static JObject ReadImportedCharacterApplicationConfig(CharacterImportedPackage package)
        {
            string root = package?.RootPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root))
                return null;

            string path = Path.Combine(root, "package_cache", "character_application.json");
            if (!File.Exists(path))
                return null;

            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character Actions: skipped reaction slot import for invalid character_application.json. path="
                    + path + " error=" + ex.Message);
                return null;
            }
        }

        private static string ReadReactionSelectionResourceKey(JObject selection)
        {
            return FirstNonEmpty(
                ReadString(selection, "runtimeResourceKey"),
                ReadString(selection, "packageResourceKey"),
                ReadString(selection, "providerResourceKey"),
                ReadString(selection, "resourceStableId"));
        }

        private static string ResolveImportedReactionResourceKey(JObject slot, string fallback)
        {
            if (slot == null)
                return fallback ?? string.Empty;

            if (slot.Property("resourceKey") != null)
                return ReadString(slot, "resourceKey");

            JToken selectionToken = slot["resourceSelection"];
            if (selectionToken != null)
                return ReadReactionSelectionResourceKey(selectionToken as JObject);

            return fallback ?? string.Empty;
        }

        private static int GetDefaultReactionActionId(ImportedReactionSlot[] slots)
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (string.Equals(slots[i].SlotId, DefaultReactionProfileSlotId, StringComparison.Ordinal)
                    && slots[i].ActionId > 0)
                {
                    return slots[i].ActionId;
                }
            }

            return slots != null && slots.Length > 0 ? slots[0].ActionId : 0;
        }

        private static CharacterReactionRuleJsonDto[] BuildReactionRules(ImportedReactionSlot[] slots)
        {
            var rules = new List<CharacterReactionRuleJsonDto>();
            int cohesionBreakActionId = 0;
            int cohesionBreakPriority = 100;
            bool hasBrokenHitRule = false;
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                ImportedReactionSlot slot = slots[i];
                if (slot.ActionId <= 0)
                    continue;

                string trigger = FirstNonEmpty(slot.Trigger, CharacterReactionRuleTrigger.Hit.ToString());
                if (IsReactionRule(trigger, slot.PressureBand, CharacterReactionRuleTrigger.Hit, "Broken"))
                    hasBrokenHitRule = true;
                if (IsReactionRule(trigger, slot.PressureBand, CharacterReactionRuleTrigger.CohesionBreak, "Broken")
                    && cohesionBreakActionId <= 0)
                {
                    cohesionBreakActionId = slot.ActionId;
                    cohesionBreakPriority = slot.Priority;
                }

                rules.Add(new CharacterReactionRuleJsonDto
                {
                    ActionId = slot.ActionId,
                    Trigger = trigger,
                    CurrentScatterPressureBand = slot.PressureBand,
                    ReactionGroupId = slot.ReactionGroupId,
                    MinImpactForce = slot.MinImpactForce,
                    MaxImpactForce = slot.MaxImpactForce,
                    Priority = slot.Priority,
                });
            }

            if (!hasBrokenHitRule && cohesionBreakActionId > 0)
            {
                rules.Add(new CharacterReactionRuleJsonDto
                {
                    ActionId = cohesionBreakActionId,
                    Trigger = CharacterReactionRuleTrigger.Hit.ToString(),
                    CurrentScatterPressureBand = "Broken",
                    Priority = cohesionBreakPriority > 20 ? cohesionBreakPriority - 20 : cohesionBreakPriority,
                });
            }

            return rules.ToArray();
        }

        private static bool IsReactionRule(
            string trigger,
            string pressureBand,
            CharacterReactionRuleTrigger expectedTrigger,
            string expectedPressureBand)
        {
            return string.Equals(trigger, expectedTrigger.ToString(), StringComparison.Ordinal)
                && string.Equals(pressureBand ?? string.Empty, expectedPressureBand ?? string.Empty, StringComparison.Ordinal);
        }

        private static CharacterActionJsonDto CreateReactionAction(string packageSegment, ImportedReactionSlot slot, int id)
        {
            int endFrame = Math.Max(0, slot.DurationFrames - 1);
            var action = new CharacterActionJsonDto
            {
                ActionId = id,
                DisplayName = FormatReactionActionDisplayName(packageSegment, slot),
                Category = CharacterActionCategory.Reaction.ToString(),
                TimelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored.ToString(),
                Tags = BuildReactionActionTags(packageSegment, slot),
                Priority = slot.Priority,
                DurationFrames = slot.DurationFrames,
                Requirements = Array.Empty<CharacterActionRequirementJsonDto>(),
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto
                    {
                        Kind = CharacterActionPhaseKind.Active.ToString(),
                        StartFrame = 0,
                        EndFrame = endFrame,
                    }
                },
                CancelRules = Array.Empty<CharacterCancelRuleJsonDto>(),
                InterruptRules = Array.Empty<CharacterInterruptRuleJsonDto>(),
                CohesionSupport = null,
                MotionTrack = CreateReactionMotionTrack(slot, endFrame),
                CombatTrack = null,
                GameplayTrack = null,
                AnimationTrack = CreateReactionAnimationTrack(slot, endFrame),
                PresentationTrack = null,
                DebugTrack = null,
            };
            return action;
        }

        private static MotionTrackJsonDto CreateReactionMotionTrack(ImportedReactionSlot slot, int endFrame)
        {
            var events = new List<MotionTrackEventJsonDto>
            {
                new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = endFrame,
                    Kind = CharacterActionTrackEventKind.SetControlScale.ToString(),
                    MovementMode = CharacterMovementMode.ControlLocked.ToString(),
                    MovementControlScale = 0f,
                    RotationControlScale = 0f,
                    StableEventId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "action." + slot.ActionId + ".control_lock"),
                },
            };

            string slotSegment = NormalizeReactionSlotSegment(slot.SlotId);
            if (string.Equals(slotSegment, "death", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new MotionTrackEventJsonDto
                {
                    Frame = Math.Min(24, endFrame),
                    EndFrame = endFrame,
                    Kind = CharacterActionTrackEventKind.SetCollisionProfile.ToString(),
                    CollisionProfileId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetExisting(402, "corpse"),
                    CollisionRestorePolicy = CharacterActionCollisionProfileRestorePolicy.None.ToString(),
                    StableEventId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, slot.ActionId + ".collision_corpse"),
                });
            }
            else if (string.Equals(slotSegment, "revive", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = endFrame,
                    Kind = CharacterActionTrackEventKind.SetCollisionProfile.ToString(),
                    CollisionProfileId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetExisting(402, "revive_low"),
                    CollisionRestorePolicy = CharacterActionCollisionProfileRestorePolicy.OnActionEnd.ToString(),
                    StableEventId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, slot.ActionId + ".collision_revive_low"),
                });
            }

            return new MotionTrackJsonDto
            {
                UsesRootMotion = false,
                RootMotion = null,
                Events = events.ToArray(),
            };
        }

        private static AnimationTrackJsonDto CreateReactionAnimationTrack(ImportedReactionSlot slot, int endFrame)
        {
            if (string.IsNullOrWhiteSpace(slot.ResourceKey))
                return new AnimationTrackJsonDto { Events = Array.Empty<AnimationTrackEventJsonDto>() };

            return new AnimationTrackJsonDto
            {
                Events = new[]
                {
                    new AnimationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = endFrame,
                        PlaybackSpeed = 1f,
                        Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                        AnimationActionKey = "anim.reaction." + NormalizeReactionSlotSegment(slot.SlotId),
                        AnimationClipResourceKey = slot.ResourceKey,
                        TransitionSeconds = 0.04f,
                        StableEventId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, slot.ActionId + ".animation"),
                    },
                },
            };
        }

        private static bool ApplyReactionAction(CharacterActionJsonDto target, CharacterActionJsonDto source)
        {
            bool changed = false;
            if (!string.Equals(target.DisplayName, source.DisplayName, StringComparison.Ordinal))
            {
                target.DisplayName = source.DisplayName;
                changed = true;
            }

            if (!string.Equals(target.Category, source.Category, StringComparison.Ordinal))
            {
                target.Category = source.Category;
                changed = true;
            }

            if (!string.Equals(target.TimelineAuthority, source.TimelineAuthority, StringComparison.Ordinal))
            {
                target.TimelineAuthority = source.TimelineAuthority;
                changed = true;
            }

            if (target.Priority != source.Priority)
            {
                target.Priority = source.Priority;
                changed = true;
            }

            if (target.DurationFrames != source.DurationFrames)
            {
                target.DurationFrames = source.DurationFrames;
                changed = true;
            }

            if (!StringArraysEqual(target.Tags, source.Tags))
            {
                target.Tags = source.Tags;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.Requirements ?? Array.Empty<CharacterActionRequirementJsonDto>()), JToken.FromObject(source.Requirements)))
            {
                target.Requirements = source.Requirements;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>()), JToken.FromObject(source.Phases)))
            {
                target.Phases = source.Phases;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>()), JToken.FromObject(source.CancelRules)))
            {
                target.CancelRules = source.CancelRules;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.InterruptRules ?? Array.Empty<CharacterInterruptRuleJsonDto>()), JToken.FromObject(source.InterruptRules)))
            {
                target.InterruptRules = source.InterruptRules;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.MotionTrack ?? new MotionTrackJsonDto()), JToken.FromObject(source.MotionTrack)))
            {
                target.MotionTrack = source.MotionTrack;
                changed = true;
            }

            if (!JToken.DeepEquals(JToken.FromObject(target.AnimationTrack ?? new AnimationTrackJsonDto()), JToken.FromObject(source.AnimationTrack)))
            {
                target.AnimationTrack = source.AnimationTrack;
                changed = true;
            }

            if (target.CohesionSupport != null)
            {
                target.CohesionSupport = null;
                changed = true;
            }

            if (target.CombatTrack != null)
            {
                target.CombatTrack = null;
                changed = true;
            }

            if (target.GameplayTrack != null)
            {
                target.GameplayTrack = null;
                changed = true;
            }

            if (target.PresentationTrack != null)
            {
                target.PresentationTrack = null;
                changed = true;
            }

            if (target.DebugTrack != null)
            {
                target.DebugTrack = null;
                changed = true;
            }

            return changed;
        }

        private static string[] BuildReactionActionTags(string packageSegment, ImportedReactionSlot slot)
        {
            var tags = new List<string>
            {
                "reaction",
                NormalizeReactionSlotSegment(slot.SlotId),
                NormalizeReactionSlotSegment(packageSegment),
                "hit_stun",
            };
            if (string.Equals(slot.Trigger, CharacterReactionRuleTrigger.CohesionBreak.ToString(), StringComparison.Ordinal))
                tags.Add("cohesion_break");
            return tags.ToArray();
        }

        private static string FormatReactionActionDisplayName(string packageSegment, ImportedReactionSlot slot)
        {
            return FirstNonEmpty(slot.DisplayName, slot.SlotId, "Reaction") + " React";
        }

        private static int BuildReactionActionId(string packageSegment, string suffix)
        {
            return 0;
        }

        private static void EnsureReactionActionIds(
            ImportedReactionSlot[] slots,
            string libraryId,
            string sourcePath)
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (slots[i].ActionId > 0)
                    continue;
                ImportedReactionSlot slot = slots[i];
                slot.ActionId = CharacterActionIdCatalog.Allocate(libraryId, sourcePath);
                slots[i] = slot;
            }
        }

        private static string NormalizeReactionSlotSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            bool previousUnderscore = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && builder.Length > 0 && !previousUnderscore)
                    builder.Append('_');

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    previousUnderscore = false;
                }
                else
                {
                    if (builder.Length > 0 && !previousUnderscore)
                    {
                        builder.Append('_');
                        previousUnderscore = true;
                    }
                }
            }

            return builder.ToString().Trim('_');
        }

        private static int IndexOfReactionSlot(List<ImportedReactionSlot> slots, string slotId)
        {
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                if (string.Equals(slots[i].SlotId, slotId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static bool ReactionRulesEqual(CharacterReactionRuleJsonDto[] left, CharacterReactionRuleJsonDto[] right)
        {
            return JToken.DeepEquals(
                JToken.FromObject(left ?? Array.Empty<CharacterReactionRuleJsonDto>()),
                JToken.FromObject(right ?? Array.Empty<CharacterReactionRuleJsonDto>()));
        }

        private static bool StringArraysEqual(string[] left, string[] right)
        {
            left = left ?? Array.Empty<string>();
            right = right ?? Array.Empty<string>();
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool TryReadCharacterActionDocument<T>(string path, out T document)
            where T : class, new()
        {
            document = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!File.Exists(path))
            {
                document = new T();
                return true;
            }

            try
            {
                document = JsonConvert.DeserializeObject<T>(
                    File.ReadAllText(path),
                    CharacterActionAuthoringJsonService.JsonSettings) ?? new T();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character Actions: skipped auto-sync for invalid JSON. path="
                    + path + " error=" + ex.Message);
                return false;
            }
        }

        private static CharacterActionProfileJsonDto FindProfile(
            CharacterActionProfileJsonDto[] profiles,
            string stableId)
        {
            profiles = profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return profiles[i];
            }

            return null;
        }

        private static CharacterActionSetJsonDto FindActionSet(
            CharacterActionSetJsonDto[] actionSets,
            string stableId)
        {
            actionSets = actionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            for (int i = 0; i < actionSets.Length; i++)
            {
                if (string.Equals(actionSets[i]?.StableId, stableId, StringComparison.Ordinal))
                    return actionSets[i];
            }

            return null;
        }

        private static ImportedActionSetBinding[] BuildImportedActionSetBindings(
            CharacterImportedPackage package,
            string fallbackStableId,
            string displayName)
        {
            CombatActionSetConfig[] actionSets = package?.Configs?.CombatActionSets ?? Array.Empty<CombatActionSetConfig>();
            EquipmentStateConfig[] equipmentStates = package?.Configs?.EquipmentStates ?? Array.Empty<EquipmentStateConfig>();
            var bindings = new List<ImportedActionSetBinding>(actionSets.Length);
            for (int i = 0; i < actionSets.Length; i++)
            {
                CombatActionSetConfig actionSet = actionSets[i];
                if (actionSet == null || string.IsNullOrWhiteSpace(actionSet.StableId))
                    continue;

                string equipmentStateStableId = string.Empty;
                for (int stateIndex = 0; stateIndex < equipmentStates.Length; stateIndex++)
                {
                    EquipmentStateConfig state = equipmentStates[stateIndex];
                    if (state != null && state.CombatActionSetId.Equals(actionSet.ActionSetId))
                    {
                        equipmentStateStableId = state.StableId ?? string.Empty;
                        break;
                    }
                }

                bindings.Add(new ImportedActionSetBinding
                {
                    StableId = actionSet.StableId,
                    DisplayName = displayName + " / " + actionSet.StableId,
                    EquipmentStateStableId = equipmentStateStableId,
                });
            }

            if (bindings.Count == 0)
            {
                bindings.Add(new ImportedActionSetBinding
                {
                    StableId = fallbackStableId,
                    DisplayName = displayName,
                    EquipmentStateStableId = string.Empty,
                });
            }

            bindings.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return bindings.ToArray();
        }

        private static string[] GetActionSetStableIds(ImportedActionSetBinding[] bindings)
        {
            bindings = bindings ?? Array.Empty<ImportedActionSetBinding>();
            var stableIds = new string[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
                stableIds[i] = bindings[i]?.StableId ?? string.Empty;
            return stableIds;
        }

        private static bool ContainsActionSet(ImportedActionSetBinding[] bindings, string stableId)
        {
            for (int i = 0; bindings != null && i < bindings.Length; i++)
            {
                if (string.Equals(bindings[i]?.StableId, stableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int FindActionSetIndex(CharacterActionSetJsonDto[] actionSets, string stableId)
        {
            for (int i = 0; actionSets != null && i < actionSets.Length; i++)
            {
                if (string.Equals(actionSets[i]?.StableId, stableId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static CharacterActionSetJsonDto[] RemoveAt(CharacterActionSetJsonDto[] actionSets, int index)
        {
            if (actionSets == null || index < 0 || index >= actionSets.Length)
                return actionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            var result = new CharacterActionSetJsonDto[actionSets.Length - 1];
            if (index > 0)
                Array.Copy(actionSets, 0, result, 0, index);
            if (index < actionSets.Length - 1)
                Array.Copy(actionSets, index + 1, result, index, actionSets.Length - index - 1);
            return result;
        }

        private static CharacterReactionProfileJsonDto FindReactionProfile(
            CharacterReactionProfileJsonDto[] profiles,
            string stableId)
        {
            profiles = profiles ?? Array.Empty<CharacterReactionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return profiles[i];
            }

            return null;
        }

        private static CharacterActionJsonDto FindAction(
            CharacterActionJsonDto[] actions,
            int actionId)
        {
            actions = actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i]?.ActionId == actionId)
                    return actions[i];
            }

            return null;
        }

        private static int NextProfileId(CharacterActionProfileJsonDto[] profiles)
        {
            int max = 0;
            profiles = profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
                max = Math.Max(max, profiles[i]?.Id ?? 0);
            return max + 1;
        }

        private static int NextActionId(CharacterActionJsonDto[] actions)
        {
            int max = 0;
            actions = actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < actions.Length; i++)
                max = Math.Max(max, actions[i]?.ActionId ?? 0);
            return max + 1;
        }

        private static int NextActionSetId(CharacterActionSetJsonDto[] actionSets)
        {
            int max = 0;
            actionSets = actionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            for (int i = 0; i < actionSets.Length; i++)
                max = Math.Max(max, actionSets[i]?.Id ?? 0);
            return max + 1;
        }

        private static bool EnsureStringReference(string[] values, string required, out string[] result)
        {
            result = values ?? Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(required))
                return false;

            for (int i = 0; i < result.Length; i++)
            {
                if (string.Equals(result[i], required, StringComparison.Ordinal))
                    return false;
            }

            result = Append(result, required);
            return true;
        }

        private static T[] Append<T>(T[] source, T value)
        {
            source = source ?? Array.Empty<T>();
            var result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[result.Length - 1] = value;
            return result;
        }

        private static CharacterActionBindingJsonDto[] CreateDefaultCommandBindings()
        {
            return new[]
            {
                new CharacterActionBindingJsonDto
                {
                    IntentId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(404, "Jump"),
                    ActionId = 300,
                    Priority = 5,
                    AllowQueue = false,
                    QueueWindowFrames = 0,
                },
                new CharacterActionBindingJsonDto
                {
                    IntentId = MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(404, "LightAttack"),
                    ActionId = 301,
                    Priority = 10,
                    AllowQueue = false,
                    QueueWindowFrames = 0,
                },
            };
        }

        private static string ResolveCharacterStableId(CharacterImportedPackage package)
        {
            CharacterConfig[] characters = package?.Configs?.Characters ?? Array.Empty<CharacterConfig>();
            for (int i = 0; i < characters.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(characters[i]?.StableId))
                    return characters[i].StableId;
            }

            return package == null || string.IsNullOrWhiteSpace(package.PackageId)
                ? string.Empty
                : "char." + package.PackageId.Trim();
        }

        private static string FormatCharacterActionDisplayName(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return "Character Default";

            return packageId.Replace('_', ' ') + " Default";
        }
    }
}
