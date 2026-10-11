using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Validates the global weapon library and character selections/overrides of public combinations.
    /// AllocByDesign: authoring validation only; never called from runtime ticks.
    /// </summary>
    public static class CharacterWeaponConfigurationValidator
    {
        public const string MainHandSlotId = "mainHand";
        public const string OffHandSlotId = "offHand";

        public static void Validate(CharacterAuthoringValidationReport report, CharacterResourcePackage package)
        {
            if (report == null || package == null)
                return;

            CharacterApplicationAuthoringSummary application = package.ApplicationConfig ??
                new CharacterApplicationAuthoringSummary();
            if (!string.Equals(application.SchemaVersion, "8.0", StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationCodes.WeaponConfigurationUpgradeRequired, "schemaVersion",
                    "Character Application must be migrated to schema 8.0 before compile or Unity import.",
                    "Run the transactional package/global weapon-library migration.");
                return;
            }

            GlobalWeaponLibrary library = package.GlobalWeaponLibrary;
            if (library == null)
            {
                Add(report, CharacterAuthoringValidationCodes.GlobalWeaponLibraryMissing, "WeaponSource/global_weapon_library.json",
                    "The global single-weapon library is missing or unreadable.",
                    "Create or restore WeaponSource/global_weapon_library.json.");
                return;
            }

            if (!string.Equals(library.SchemaVersion, GlobalWeaponLibrarySchema.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationCodes.GlobalWeaponLibrarySchemaUnsupported, "schemaVersion",
                    "The global single-weapon library schema is unsupported.",
                    "Migrate the global weapon library to schema 6.0.", "WeaponSource/global_weapon_library.json");
                return;
            }

            GlobalWeaponConfigurationResolver.HydrateCombinedWeapons(package);
            Dictionary<string, SingleWeaponConfiguration> singles = ValidateLibrary(report, library);
            ValidatePublicActionReferences(report, library, package.CommonCharacterActionLibrary);
            Dictionary<string, CharacterWeaponSlotProfile> slots = IndexWeaponSlots(report, package.Geometry);
            ValidateCharacterCombinedBindings(report, application, library);
            ValidateCombinedConfigurations(report, package, application, singles, slots);
        }

        private static void ValidatePublicActionReferences(
            CharacterAuthoringValidationReport report,
            GlobalWeaponLibrary library,
            CharacterActionSourceLibrary commonLibrary)
        {
            var commonIds = new HashSet<int>();
            for (int i = 0; commonLibrary?.Actions != null && i < commonLibrary.Actions.Count; i++)
            {
                int actionId = CharacterActionSourceLibrary.GetActionId(commonLibrary.Actions[i]);
                if (actionId > 0)
                    commonIds.Add(actionId);
            }

            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                library?.CombinedWeaponConfigurations;
            for (int configurationIndex = 0; configurations != null && configurationIndex < configurations.Count; configurationIndex++)
            {
                List<CombinedWeaponActionEntry> actions =
                    configurations[configurationIndex]?.ActionConfiguration?.Actions;
                for (int actionIndex = 0; actions != null && actionIndex < actions.Count; actionIndex++)
                {
                    int actionId = actions[actionIndex]?.ActionId ?? 0;
                    if (!commonIds.Contains(actionId))
                    {
                        Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                            "combinedWeaponConfigurations/" + configurationIndex +
                            "/actionConfiguration/actions/" + actionIndex + "/actionId",
                            "A public combined weapon configuration may reference only Common CharacterActions.",
                            "Choose a Common action, or move this selection into a character override.",
                            "WeaponSource/global_weapon_library.json");
                    }
                }
            }
        }

        private static Dictionary<string, SingleWeaponConfiguration> ValidateLibrary(
            CharacterAuthoringValidationReport report,
            GlobalWeaponLibrary library)
        {
            var singles = new Dictionary<string, SingleWeaponConfiguration>(StringComparer.Ordinal);
            List<SingleWeaponConfiguration> items = library.SingleWeaponConfigurations ??
                new List<SingleWeaponConfiguration>();
            bool hasPhysicalWeapons = items.Exists(item => item != null &&
                !GlobalWeaponLibrarySchema.IsUnarmed(item.SingleWeaponConfigurationId));
            MxFramework.CharacterApplication.WeaponTypeCatalog weaponTypes = null;
            if (hasPhysicalWeapons && (library.WeaponTypes == null || library.WeaponTypes.Count == 0))
            {
                Add(report, "WeaponTypeCatalogMissing", "weaponTypes",
                    "A schema 6.0 library containing physical weapons requires its authoritative weapon type catalog.",
                    "Restore weaponTypes and explicitly classify every physical single weapon.",
                    "WeaponSource/global_weapon_library.json");
            }
            else if (library.WeaponTypes != null && library.WeaponTypes.Count > 0)
            {
                try { weaponTypes = new MxFramework.CharacterApplication.WeaponTypeCatalog(library.WeaponTypes); }
                catch (System.ArgumentException error) { Add(report, "WeaponTypeCatalogInvalid", "weaponTypes", error.Message,
                    "Repair the authoritative weapon type catalog.", "WeaponSource/global_weapon_library.json"); }
            }
            // Unarmed-only authoring fixtures have no physical weapon kinds to classify.
            int unarmedCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                SingleWeaponConfiguration item = items[i];
                string path = "singleWeaponConfigurations/" + i;
                string id = item?.SingleWeaponConfigurationId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || !singles.TryAdd(id, item))
                {
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponConfigurationIdDuplicate,
                        path + "/singleWeaponConfigurationId",
                        "singleWeaponConfigurationId is required and must be globally unique.",
                        "Assign a unique stable ID.", "WeaponSource/global_weapon_library.json");
                    continue;
                }

                if (GlobalWeaponLibrarySchema.IsUnarmed(id))
                {
                    unarmedCount++;
                    if (!IsValidUnarmed(item))
                        Add(report, CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid, path,
                            "weapon.unarmed must keep its fixed category, slots, empty occupiesSlots and null weaponRef.",
                            "Restore the built-in weapon.unarmed definition.", "WeaponSource/global_weapon_library.json");
                }
                else if (item.ModelPose == null)
                {
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponModelPoseMissing,
                        path + "/modelPose", "A single weapon requires one weapon-local model pose.",
                        "Create an identity ModelRoot pose.", "WeaponSource/global_weapon_library.json");
                }

                if (!GlobalWeaponLibrarySchema.IsUnarmed(id) && weaponTypes != null
                    && !weaponTypes.ValidateWeapon(item.WeaponTypeId, item.Category, out string typeIssue))
                    Add(report, "WeaponTypeBindingInvalid", path + "/weaponTypeId", typeIssue,
                        "Select a registered weapon kind matching its equipment category.", "WeaponSource/global_weapon_library.json");
                if (GlobalWeaponLibrarySchema.IsUnarmed(id) && !string.IsNullOrEmpty(item.WeaponTypeId))
                    Add(report, "UnarmedWeaponTypeInvalid", path + "/weaponTypeId", "Virtual unarmed is not a concrete weapon kind.",
                        "Clear the physical weapon kind.", "WeaponSource/global_weapon_library.json");
                var traceIds = new HashSet<string>(StringComparer.Ordinal);
                for (int traceIndex = 0; item?.TraceProfiles != null && traceIndex < item.TraceProfiles.Count; traceIndex++)
                {
                    SingleWeaponTraceProfile trace = item.TraceProfiles[traceIndex];
                    string tracePath = path + "/traceProfiles/" + traceIndex;
                    if (trace == null || string.IsNullOrWhiteSpace(trace.TraceId) || !traceIds.Add(trace.TraceId))
                        Add(report, CharacterAuthoringValidationCodes.SingleWeaponTraceIdDuplicate,
                            tracePath + "/traceId", "traceId must be non-empty and unique within one single weapon.",
                            "Assign a stable weapon-local trace ID.", "WeaponSource/global_weapon_library.json");
                    if (trace != null && (trace.StartPose?.ParentKind != CharacterPoseParentKind.ModelRoot ||
                        trace.EndPose?.ParentKind != CharacterPoseParentKind.ModelRoot))
                        Add(report, CharacterAuthoringValidationCodes.SingleWeaponTraceSpaceInvalid,
                            tracePath, "single-weapon traces must use weapon-local ModelRoot space.",
                            "Move trace endpoints into the weapon model coordinate space.", "WeaponSource/global_weapon_library.json");
                }
                string trailResource = item.PresentationConfiguration?.BladeTrailResourceKey ?? string.Empty;
                int trailResidue = item.PresentationConfiguration?.BladeTrailResidueMilliseconds ?? 100;
                int trailStyle = item.PresentationConfiguration?.BladeTrailStyleId ?? 0;
                if (trailStyle < 0 || trailStyle > 4)
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound,
                        path + "/presentationConfiguration/bladeTrailStyleId",
                        "Blade trail style must be 0 (resource default), 1 (thin), 2 (ribbon), 3 (double), or 4 (energy).",
                        "Select a supported blade trail style.", "WeaponSource/global_weapon_library.json");
                if ((!string.IsNullOrWhiteSpace(trailResource) &&
                        (!trailResource.Contains(":GameObject@") || GlobalWeaponLibrarySchema.IsUnarmed(id)))
                    || (trailResidue != 0 && (trailResidue < 80 || trailResidue > 150)))
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound,
                        path + "/presentationConfiguration/bladeTrailResourceKey",
                        "Blade trail requires a formal GameObject VFX key on an equipped weapon; residue is 0 or 80–150 ms.",
                        "Select a registered blade trail VFX and a supported residue duration.", "WeaponSource/global_weapon_library.json");
                if (!GlobalWeaponLibrarySchema.IsUnarmed(id) && item.WeaponRef == null)
                {
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound, path + "/weaponRef",
                        "A non-unarmed single weapon must reference a Resource Manager asset.",
                        "Select a stable weapon resource.", "WeaponSource/global_weapon_library.json");
                }
            }

            if (unarmedCount != 1)
                Add(report, CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid,
                    "singleWeaponConfigurations/weapon.unarmed",
                    "The global library must contain exactly one weapon.unarmed entry.",
                    "Restore the built-in weapon.unarmed definition.", "WeaponSource/global_weapon_library.json");
            ValidateGlobalConfigurationIds(report, library);
            return singles;
        }

        private static void ValidateGlobalConfigurationIds(
            CharacterAuthoringValidationReport report,
            GlobalWeaponLibrary library)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; library.AnimationConfigurations != null && i < library.AnimationConfigurations.Count; i++)
            {
                GlobalAnimationConfiguration item = library.AnimationConfigurations[i];
                if (item == null || string.IsNullOrWhiteSpace(item.AnimationConfigurationId) || !ids.Add(item.AnimationConfigurationId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAnimationConfigurationIdDuplicate,
                        "animationConfigurations/" + i + "/animationConfigurationId",
                        "animationConfigurationId is required and must be globally unique within its type.",
                        "Assign a unique global animation configuration ID.", "WeaponSource/global_weapon_library.json");
            }
            ids.Clear();
            for (int i = 0; library.AbilityConfigurations != null && i < library.AbilityConfigurations.Count; i++)
            {
                GlobalAbilityConfiguration item = library.AbilityConfigurations[i];
                if (item == null || string.IsNullOrWhiteSpace(item.AbilityConfigurationId) || !ids.Add(item.AbilityConfigurationId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityConfigurationIdDuplicate,
                        "abilityConfigurations/" + i + "/abilityConfigurationId",
                        "abilityConfigurationId is required and must be globally unique within its type.",
                        "Assign a unique global ability configuration ID.", "WeaponSource/global_weapon_library.json");
                else
                    ValidateAbilityEntries(report, item, i);
            }
            ids.Clear();
            for (int i = 0; library.ReactionConfigurations != null && i < library.ReactionConfigurations.Count; i++)
            {
                GlobalReactionConfiguration item = library.ReactionConfigurations[i];
                if (item == null || string.IsNullOrWhiteSpace(item.ReactionConfigurationId) || !ids.Add(item.ReactionConfigurationId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalReactionConfigurationIdDuplicate,
                        "reactionConfigurations/" + i + "/reactionConfigurationId",
                        "reactionConfigurationId is required and must be globally unique within its type.",
                        "Assign a unique global reaction configuration ID.", "WeaponSource/global_weapon_library.json");
                else if (item.Slots != null && item.Slots.Count > 0 && string.IsNullOrWhiteSpace(item.DefaultSlotId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalReactionDefaultSlotMissing,
                        "reactionConfigurations/" + i + "/defaultSlotId",
                        "defaultSlotId must explicitly select one reaction Slot.",
                        "Select an existing Slot as the default.", "WeaponSource/global_weapon_library.json");
                else if (!string.IsNullOrWhiteSpace(item.DefaultSlotId) &&
                    (item.Slots == null || !item.Slots.Exists(slot =>
                        slot != null && string.Equals(slot.SlotId, item.DefaultSlotId, StringComparison.Ordinal))))
                    Add(report, CharacterAuthoringValidationCodes.GlobalReactionDefaultSlotNotFound,
                        "reactionConfigurations/" + i + "/defaultSlotId",
                        "defaultSlotId must reference an existing reaction Slot in the same configuration.",
                        "Select an existing Slot as the default.", "WeaponSource/global_weapon_library.json");
            }
            ids.Clear();
            for (int i = 0; library.CombinedWeaponConfigurations != null && i < library.CombinedWeaponConfigurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary item = library.CombinedWeaponConfigurations[i];
                if (item == null || string.IsNullOrWhiteSpace(item.CombinedWeaponConfigurationId) ||
                    !ids.Add(item.CombinedWeaponConfigurationId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalCombinedWeaponConfigurationIdDuplicate,
                        "combinedWeaponConfigurations/" + i + "/combinedWeaponConfigurationId",
                        "combinedWeaponConfigurationId is required and must be globally unique.",
                        "Assign a unique public combination ID.", "WeaponSource/global_weapon_library.json");
            }
        }

        private static void ValidateCharacterCombinedBindings(
            CharacterAuthoringValidationReport report,
            CharacterApplicationAuthoringSummary application,
            GlobalWeaponLibrary library)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            List<CharacterCombinedWeaponConfigurationBinding> bindings =
                application?.CombinedWeaponConfigurationBindings ??
                new List<CharacterCombinedWeaponConfigurationBinding>();
            for (int i = 0; i < bindings.Count; i++)
            {
                string id = bindings[i]?.CombinedWeaponConfigurationId ?? string.Empty;
                string path = "combinedWeaponConfigurationBindings/" + i + "/combinedWeaponConfigurationId";
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponConfigurationIdDuplicate,
                        path,
                        "Each character must reference a public combination at most once.",
                        "Remove the duplicate character binding.");
                    continue;
                }
                if (GlobalWeaponConfigurationResolver.FindCombinedWeapon(library, id) == null)
                    Add(report, CharacterAuthoringValidationCodes.GlobalCombinedWeaponConfigurationNotFound,
                        path,
                        "The referenced public combined weapon configuration does not exist.",
                        "Choose an existing public combination or restore it in the global library.");
            }
        }

        private static Dictionary<string, CharacterWeaponSlotProfile> IndexWeaponSlots(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            var result = new Dictionary<string, CharacterWeaponSlotProfile>(StringComparer.Ordinal);
            List<CharacterWeaponSlotProfile> items = geometry?.WeaponSlots ?? new List<CharacterWeaponSlotProfile>();
            for (int i = 0; i < items.Count; i++)
            {
                CharacterWeaponSlotProfile item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SlotId) || !result.TryAdd(item.SlotId, item))
                    Add(report, CharacterAuthoringValidationCodes.DuplicateWeaponAttachmentId,
                        "geometry/weaponSlots/" + i + "/slotId",
                        "slotId is required and must be unique per character.",
                        "Keep one character-owned binding for each semantic weapon slot.");
            }
            return result;
        }

        private static void ValidateCombinedConfigurations(
            CharacterAuthoringValidationReport report,
            CharacterResourcePackage package,
            CharacterApplicationAuthoringSummary application,
            Dictionary<string, SingleWeaponConfiguration> singles,
            Dictionary<string, CharacterWeaponSlotProfile> characterSlots)
        {
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                application.CombinedWeaponConfigurations ?? new List<CombinedWeaponConfigurationAuthoringSummary>();
            if (configurations.Count == 0)
            {
                Add(report, CharacterAuthoringValidationCodes.CombinedWeaponConfigurationMissing,
                    "combinedWeaponConfigurations",
                    "At least one explicit combined weapon configuration is required.",
                    "Create a combination with explicit mainHand and offHand entries.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < configurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = configurations[i];
                string id = configuration?.CombinedWeaponConfigurationId ?? string.Empty;
                string path = "combinedWeaponConfigurations/" + i;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponConfigurationIdDuplicate,
                        path + "/combinedWeaponConfigurationId",
                        "The resolved combinedWeaponConfigurationId must be unique for this character.",
                        "Assign a unique stable combination ID.");
                    continue;
                }
                ValidateCombinedConfiguration(report, package, application, configuration, path, singles, characterSlots);
            }

            if (string.IsNullOrWhiteSpace(application.DefaultCombinedWeaponConfigurationId) ||
                !ids.Contains(application.DefaultCombinedWeaponConfigurationId))
                Add(report, CharacterAuthoringValidationCodes.CombinedWeaponConfigurationDefaultInvalid,
                    "defaultCombinedWeaponConfigurationId",
                    "defaultCombinedWeaponConfigurationId must reference exactly one existing combination.",
                    "Select one existing combined weapon configuration as the default.");
        }

        private static void ValidateCombinedConfiguration(
            CharacterAuthoringValidationReport report,
            CharacterResourcePackage package,
            CharacterApplicationAuthoringSummary application,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            string path,
            Dictionary<string, SingleWeaponConfiguration> singles,
            Dictionary<string, CharacterWeaponSlotProfile> characterSlots)
        {
            var usedSlots = new HashSet<string>(StringComparer.Ordinal);
            List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration.Slots ??
                new List<CombinedWeaponConfigurationSlotAuthoringSummary>();
            for (int i = 0; i < slots.Count; i++)
            {
                CombinedWeaponConfigurationSlotAuthoringSummary slot = slots[i];
                string slotPath = path + "/slots/" + i;
                string slotId = slot?.SlotId ?? string.Empty;
                if (!IsSupportedSlot(slotId) || !usedSlots.Add(slotId))
                {
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponSlotDuplicate,
                        slotPath + "/slotId",
                        "Each combination must contain mainHand and offHand exactly once.",
                        "Keep one mainHand and one offHand entry.");
                    continue;
                }

                if (!singles.TryGetValue(slot.SingleWeaponConfigurationId ?? string.Empty, out SingleWeaponConfiguration single))
                {
                    Add(report, CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound,
                        slotPath + "/singleWeaponConfigurationId",
                        "The referenced global single-weapon configuration does not exist.",
                        "Choose an item from the global weapon library.");
                    continue;
                }

                if (GlobalWeaponLibrarySchema.IsUnarmed(single.SingleWeaponConfigurationId))
                    continue;

                if (single.AllowedSlots == null || !single.AllowedSlots.Contains(slotId))
                    Add(report, CharacterAuthoringValidationCodes.WeaponSlotNotAllowed,
                        slotPath + "/slotId", "The single weapon does not allow this slot.",
                        "Choose a compatible weapon or update allowedSlots in the global library.");

                if (!characterSlots.TryGetValue(slotId, out CharacterWeaponSlotProfile characterSlot) ||
                    string.IsNullOrWhiteSpace(characterSlot.AttachSocketId))
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponAttachmentRequired,
                        "geometry/weaponSlots/" + slotId,
                        "A non-unarmed slot requires one character-owned socket binding.",
                        "Bind the semantic weapon slot to a character socket.");
            }

            if (!usedSlots.Contains(MainHandSlotId) || !usedSlots.Contains(OffHandSlotId) || slots.Count != 2)
                Add(report, CharacterAuthoringValidationCodes.CombinedWeaponSlotMissing, path + "/slots",
                    "Every combination must explicitly contain exactly mainHand and offHand.",
                    "Add the missing slot and select weapon.unarmed when the hand is empty.");

            ValidateTwoHandRule(report, configuration, singles, path);
            GlobalAnimationConfiguration animation = GlobalWeaponConfigurationResolver.FindAnimation(
                package.GlobalWeaponLibrary, configuration.AnimationConfigurationId);
            if (animation == null)
                Add(report, CharacterAuthoringValidationCodes.GlobalAnimationConfigurationNotFound,
                    path + "/animationConfigurationId",
                    "The combined weapon configuration must reference one global animation configuration.",
                    "Choose or create a global animation configuration.");
            else if (!HasLocomotion(animation))
                Add(report, CharacterAuthoringValidationCodes.GlobalAnimationLocomotionMissing,
                    path + "/animationConfigurationId",
                    "The selected animation configuration must contain complete locomotion data.",
                    "Add one required case-sensitive slotId=\"locomotion\" with setId, groupId and defaultBlendId.");
            else
            {
                GlobalAnimationCompatibilityResult compatibility =
                    GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(package, animation);
                if (!compatibility.IsCompatible)
                {
                    Add(report, CharacterAuthoringValidationCodes.GlobalAnimationConfigurationIncompatible,
                        path + "/animationConfigurationId",
                        compatibility.Message,
                        "Choose a native rig configuration, or use a Humanoid configuration whose sets and profiles allow retargeting.");
                }
            }

            GlobalAbilityConfiguration ability = string.IsNullOrWhiteSpace(configuration.AbilityConfigurationId)
                ? null
                : GlobalWeaponConfigurationResolver.FindAbility(package.GlobalWeaponLibrary, configuration.AbilityConfigurationId);
            if (!string.IsNullOrWhiteSpace(configuration.AbilityConfigurationId) && ability == null)
                Add(report, CharacterAuthoringValidationCodes.GlobalAbilityConfigurationNotFound,
                    path + "/abilityConfigurationId",
                    "The selected global ability configuration does not exist.",
                    "Choose an existing ability configuration or select no ability configuration.");

            GlobalReactionConfiguration reaction = GlobalWeaponConfigurationResolver.FindReaction(
                package.GlobalWeaponLibrary, configuration.ReactionConfigurationId);
            if (reaction == null)
                Add(report, CharacterAuthoringValidationCodes.GlobalReactionConfigurationNotFound,
                    path + "/reactionConfigurationId",
                    "The combined weapon configuration must reference one global reaction configuration.",
                    "Choose or create a global reaction configuration.");

            ValidateActionSourceReferences(report, package, application, configuration, path);
            ValidateActionRoles(report, configuration, ability, reaction, path);
            ValidateCommandIntentConflicts(report, configuration?.ActionConfiguration, ability, path);
        }

        private static void ValidateActionSourceReferences(
            CharacterAuthoringValidationReport report,
            CharacterResourcePackage package,
            CharacterApplicationAuthoringSummary application,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            string path)
        {
            var effectiveIds = new HashSet<int>();
            var actionsById = new Dictionary<int, JsonElement>();
            AddActionLibraryIds(report, package?.CommonCharacterActionLibrary, effectiveIds, actionsById,
                package?.CommonCharacterActionLibraryPath ?? "CharacterActionSource/Libraries/Common/character_actions.json");
            AddActionLibraryIds(report, package?.CharacterActionLibrary, effectiveIds, actionsById,
                package?.CharacterActionLibraryPath ?? "config/character_actions.json");

            List<CombinedWeaponActionEntry> actions = configuration?.ActionConfiguration?.Actions;
            for (int i = 0; actions != null && i < actions.Count; i++)
            {
                int actionId = actions[i]?.ActionId ?? 0;
                if (actionId <= 0 || !effectiveIds.Contains(actionId))
                {
                    bool mayBelongToExternalLibrary = actionId > 0
                        && application?.AdditionalActionLibraryIds != null
                        && application.AdditionalActionLibraryIds.Count > 0;
                    if (mayBelongToExternalLibrary)
                        continue;
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                        path + "/actionConfiguration/actions/" + i + "/actionId",
                        "The selected CharacterAction does not exist in Common or this character's source library.",
                        "Choose an action from the CharacterStudio action library selector.");
                    continue;
                }

                GlobalAnimationCompatibilityResult compatibility =
                    CharacterActionAnimationCompatibilityResolver.Resolve(
                        package,
                        actionsById[actionId],
                        package.CommonCharacterActionLibrary,
                        package.CharacterActionLibrary);
                if (!compatibility.IsCompatible)
                {
                    Add(report, CharacterAuthoringValidationCodes.CharacterActionAnimationIncompatible,
                        path + "/actionConfiguration/actions/" + i + "/actionId",
                        "CharacterAction '" + actionId + "' cannot drive this character: " + compatibility.Message,
                        "Choose a native action or a declared Humanoid source that allows retargeting.");
                }
            }

            ValidateCommandSequences(report, configuration?.ActionConfiguration, path);
        }

        private static void AddActionLibraryIds(
            CharacterAuthoringValidationReport report,
            CharacterActionSourceLibrary library,
            HashSet<int> effectiveIds,
            Dictionary<int, JsonElement> actionsById,
            string sourcePath)
        {
            if (library == null || !string.Equals(
                library.SchemaVersion,
                CharacterActionSourceLibrary.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                    sourcePath + "/schemaVersion",
                    "CharacterAction source library is missing or uses an unsupported schema.",
                    "Restore a schema 0.3.0 CharacterAction source library.", sourcePath);
                return;
            }

            for (int i = 0; library.Actions != null && i < library.Actions.Count; i++)
            {
                int actionId = CharacterActionSourceLibrary.GetActionId(library.Actions[i]);
                if (actionId <= 0 || !effectiveIds.Add(actionId))
                {
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionIdShared,
                        sourcePath + "/actions/" + i + "/actionId",
                        "Action ids must be globally unique across effective libraries.",
                        "Assign a free id from the CharacterAction ID catalog.", sourcePath);
                    continue;
                }
                actionsById[actionId] = library.Actions[i];
            }
        }

        private static void ValidateCommandSequences(
            CharacterAuthoringValidationReport report,
            CombinedWeaponActionConfiguration configuration,
            string path)
        {
            var roles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; configuration?.Actions != null && i < configuration.Actions.Count; i++)
                if (!string.IsNullOrWhiteSpace(configuration.Actions[i]?.ActionRole))
                    roles.Add(configuration.Actions[i].ActionRole);

            var sequenceIntents = new HashSet<string>(StringComparer.Ordinal);
            var bindingIntents = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; configuration?.CommandBindings != null && i < configuration.CommandBindings.Count; i++)
            {
                CombinedWeaponCommandBinding binding = configuration.CommandBindings[i];
                bool valid = binding != null
                    && !string.IsNullOrWhiteSpace(binding.IntentId)
                    && binding.QueueWindowFrames >= 0
                    && bindingIntents.Add(binding.IntentId)
                    && (binding.ActionId > 0 || roles.Contains(binding.ActionRole ?? string.Empty));
                if (!valid)
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                        path + "/actionConfiguration/commandBindings/" + i,
                        "A command binding must have a unique intent and resolve an actionRole or actionId.",
                        "Choose one existing action role for the intent.");
            }
            for (int i = 0; configuration?.CommandSequences != null && i < configuration.CommandSequences.Count; i++)
            {
                CombinedWeaponCommandSequence sequence = configuration.CommandSequences[i];
                bool valid = sequence != null && !string.IsNullOrWhiteSpace(sequence.IntentId) &&
                    sequence.ActionRoles != null && sequence.ActionRoles.Count > 0 &&
                    sequence.QueueWindowFrames >= 0 && sequenceIntents.Add(sequence.IntentId)
                    && !bindingIntents.Contains(sequence.IntentId);
                for (int roleIndex = 0; valid && roleIndex < sequence.ActionRoles.Count; roleIndex++)
                    valid = roles.Contains(sequence.ActionRoles[roleIndex] ?? string.Empty);
                if (!valid)
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                        path + "/actionConfiguration/commandSequences/" + i,
                        "A command sequence must have a unique intent and resolve every ordered actionRole.",
                        "Choose existing action slots for each combo step.");
            }
        }

        private static void ValidateCommandIntentConflicts(
            CharacterAuthoringValidationReport report,
            CombinedWeaponActionConfiguration configuration,
            GlobalAbilityConfiguration ability,
            string path)
        {
            var sequenceIntents = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; configuration?.CommandSequences != null && i < configuration.CommandSequences.Count; i++)
            {
                string intentId = configuration.CommandSequences[i]?.IntentId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(intentId))
                    sequenceIntents.Add(intentId);
            }

            List<GlobalAbilityEntry> abilities = ability?.Abilities;
            for (int i = 0; abilities != null && i < abilities.Count; i++)
            {
                GlobalAbilityEntry entry = abilities[i];
                if (entry == null
                    || !string.Equals(entry.TriggerMode, GlobalAbilityTriggerModes.Input, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(entry.InputIntentId)
                    || !sequenceIntents.Contains(entry.InputIntentId))
                {
                    continue;
                }

                int actionId = ResolveAbilityActionId(
                    configuration,
                    entry.AbilityId,
                    entry.ActionRole);
                if (actionId <= 0)
                    continue;

                Add(report, CharacterAuthoringValidationCodes.CombinedWeaponCommandIntentConflict,
                    path + "/actionConfiguration/commandSequences",
                    "Intent '" + entry.InputIntentId + "' would be emitted by both commandBindings and commandSequences.",
                    "Keep the intent either as one ability command binding or as one ordered command sequence.");
            }
        }

        private static int ResolveAbilityActionId(
            CombinedWeaponActionConfiguration configuration,
            int abilityId,
            string actionRole)
        {
            List<CombinedWeaponAbilityActionBinding> bindings = configuration?.AbilityActionBindings;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                CombinedWeaponAbilityActionBinding binding = bindings[i];
                if (binding != null
                    && binding.AbilityId == abilityId
                    && string.Equals(binding.ActionRole, actionRole, StringComparison.Ordinal))
                {
                    return binding.ActionId;
                }
            }
            return 0;
        }

        private static bool HasLocomotion(GlobalAnimationConfiguration animation)
        {
            for (int i = 0; animation?.Profiles != null && i < animation.Profiles.Count; i++)
            {
                List<AnimationProfileSlotAuthoring> slots = animation.Profiles[i]?.Slots;
                for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                {
                    AnimationProfileSlotAuthoring slot = slots[slotIndex];
                    if (string.Equals(slot?.SlotId, "locomotion", StringComparison.Ordinal) &&
                        slot.Required &&
                        !string.IsNullOrWhiteSpace(slot.SetId) &&
                        !string.IsNullOrWhiteSpace(slot.GroupId) &&
                        !string.IsNullOrWhiteSpace(slot.DefaultBlendId))
                        return true;
                }
            }
            return false;
        }

        private static void ValidateActionRoles(
            CharacterAuthoringValidationReport report,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            GlobalAbilityConfiguration ability,
            GlobalReactionConfiguration reaction,
            string path)
        {
            var roles = new HashSet<string>(StringComparer.Ordinal);
            var actionIdsByRole = new Dictionary<string, int>(StringComparer.Ordinal);
            List<CombinedWeaponActionEntry> actions = configuration.ActionConfiguration?.Actions;
            for (int i = 0; actions != null && i < actions.Count; i++)
            {
                string role = actions[i]?.ActionRole ?? string.Empty;
                if (string.IsNullOrWhiteSpace(role) || !roles.Add(role) || actions[i].ActionId <= 0)
                    Add(report, CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate,
                        path + "/actionConfiguration/actions/" + i,
                        "Every effective combination action must have a unique actionRole and a non-empty actionId.",
                        "Assign one action to each required role.");
                else
                    actionIdsByRole[role] = actions[i].ActionId;
            }
            List<GlobalAbilityEntry> abilityBindings = ability?.Abilities;
            for (int i = 0; abilityBindings != null && i < abilityBindings.Count; i++)
            {
                GlobalAbilityEntry required = abilityBindings[i];
                if (required == null ||
                    string.Equals(required.ActionBindingMode, GlobalAbilityActionBindingModes.None, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(required.ActionRole))
                    continue;
                ValidateAbilityActionBinding(report, configuration.ActionConfiguration, required, actionIdsByRole, path, i);
            }
            List<GlobalReactionSlotConfiguration> reactionSlots = reaction?.Slots;
            for (int i = 0; reactionSlots != null && i < reactionSlots.Count; i++)
            {
                GlobalReactionSlotConfiguration slot = reactionSlots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.ActionRole))
                    continue;
                bool requiredBinding = slot.Required || string.Equals(
                    slot.SlotId,
                    reaction.DefaultSlotId,
                    StringComparison.Ordinal);
                ValidateReactionActionBinding(
                    report,
                    configuration.ActionConfiguration,
                    slot,
                    actionIdsByRole,
                    path,
                    i,
                    requiredBinding);
            }
        }

        private static void ValidateAbilityActionBinding(
            CharacterAuthoringValidationReport report,
            CombinedWeaponActionConfiguration configuration,
            GlobalAbilityEntry required,
            Dictionary<string, int> actionIdsByRole,
            string path,
            int index)
        {
            int count = 0;
            CombinedWeaponAbilityActionBinding match = null;
            List<CombinedWeaponAbilityActionBinding> bindings = configuration?.AbilityActionBindings;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                CombinedWeaponAbilityActionBinding candidate = bindings[i];
                if (candidate != null && candidate.AbilityId == required.AbilityId &&
                    string.Equals(candidate.ActionRole, required.ActionRole, StringComparison.Ordinal))
                {
                    count++;
                    match = candidate;
                }
            }
            bool requiredBinding = string.Equals(required.ActionBindingMode, GlobalAbilityActionBindingModes.Required, StringComparison.Ordinal);
            if (count != 1 || !actionIdsByRole.TryGetValue(required.ActionRole, out int actionId) ||
                match?.ActionId != actionId)
            {
                if (!requiredBinding && count == 0)
                    return;
                Add(report, count > 1
                        ? CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate
                        : CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                    path + "/actionConfiguration/abilityActionBindings/" + index,
                    "Every required ability actionRole must resolve exactly once to an effective combination Action.",
                    "Bind ability " + required.AbilityId + " role '" + required.ActionRole + "' in the public default or character override.");
            }
        }

        private static void ValidateAbilityEntries(
            CharacterAuthoringValidationReport report,
            GlobalAbilityConfiguration configuration,
            int configurationIndex)
        {
            var ids = new HashSet<int>();
            var inputSlots = new HashSet<string>(StringComparer.Ordinal);
            List<GlobalAbilityEntry> entries = configuration.Abilities ?? new List<GlobalAbilityEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                GlobalAbilityEntry entry = entries[i];
                string path = "abilityConfigurations/" + configurationIndex + "/abilities/" + i;
                if (entry == null || entry.AbilityId <= 0 || !ids.Add(entry.AbilityId))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityEntryInvalid, path + "/abilityId",
                        "abilityId must be positive and unique within one ability configuration.",
                        "Assign a unique positive CharacterAbilityId.", "WeaponSource/global_weapon_library.json");
                if (entry == null)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityEntryInvalid, path + "/displayName",
                        "Every ability requires a human-readable displayName.",
                        "Describe the ability purpose, for example 闪避 or 护盾被动.", "WeaponSource/global_weapon_library.json");
                if (!IsAbilityTier(entry.Tier) || !IsAbilityExecutionMode(entry.ExecutionMode) ||
                    !IsAbilityTriggerMode(entry.TriggerMode) || !IsAbilityActionBindingMode(entry.ActionBindingMode))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityEntryInvalid, path,
                        "Ability semantics contain a value outside the structured ability domains.",
                        "Choose values from the structured ability editor.", "WeaponSource/global_weapon_library.json");
                if (string.Equals(entry.TriggerMode, GlobalAbilityTriggerModes.Input, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(entry.SlotId) || string.IsNullOrWhiteSpace(entry.InputIntentId) ||
                     !inputSlots.Add(entry.SlotId)))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityInputBindingInvalid, path,
                        "Input abilities require a unique slotId and a non-empty inputIntentId.",
                        "Complete the input fields or choose another trigger mode.", "WeaponSource/global_weapon_library.json");
                if (string.Equals(entry.ActionBindingMode, GlobalAbilityActionBindingModes.Required, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(entry.ActionRole))
                    Add(report, CharacterAuthoringValidationCodes.GlobalAbilityActionRoleInvalid, path + "/actionRole",
                        "Required action binding needs a non-empty actionRole.",
                        "Assign an actionRole and map it in the public default or character action override.", "WeaponSource/global_weapon_library.json");
            }
        }

        private static bool IsAbilityTier(string value) => value == GlobalAbilityTiers.Unspecified ||
            value == GlobalAbilityTiers.Basic || value == GlobalAbilityTiers.Special || value == GlobalAbilityTiers.Passive;
        private static bool IsAbilityExecutionMode(string value) => value == GlobalAbilityExecutionModes.ActionOnly ||
            value == GlobalAbilityExecutionModes.GameplayRuntime || value == GlobalAbilityExecutionModes.External;
        private static bool IsAbilityTriggerMode(string value) => value == GlobalAbilityTriggerModes.Input ||
            value == GlobalAbilityTriggerModes.Event || value == GlobalAbilityTriggerModes.Passive ||
            value == GlobalAbilityTriggerModes.Automatic || value == GlobalAbilityTriggerModes.External;
        private static bool IsAbilityActionBindingMode(string value) => value == GlobalAbilityActionBindingModes.Required ||
            value == GlobalAbilityActionBindingModes.Optional || value == GlobalAbilityActionBindingModes.None;

        private static void ValidateReactionActionBinding(
            CharacterAuthoringValidationReport report,
            CombinedWeaponActionConfiguration configuration,
            GlobalReactionSlotConfiguration required,
            Dictionary<string, int> actionIdsByRole,
            string path,
            int index,
            bool requiredBinding)
        {
            int count = 0;
            CombinedWeaponReactionActionBinding match = null;
            List<CombinedWeaponReactionActionBinding> bindings = configuration?.ReactionActionBindings;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                CombinedWeaponReactionActionBinding candidate = bindings[i];
                if (candidate != null && string.Equals(candidate.ReactionSlotId, required.SlotId, StringComparison.Ordinal) &&
                    string.Equals(candidate.ActionRole, required.ActionRole, StringComparison.Ordinal))
                {
                    count++;
                    match = candidate;
                }
            }
            bool hasCombinationAction = actionIdsByRole.TryGetValue(required.ActionRole, out int actionId);
            bool hasBoundAction = (match?.ActionId ?? 0) > 0;
            if (!requiredBinding && count == 0)
                return;
            if (count != 1
                || !hasBoundAction
                || (hasCombinationAction && match.ActionId != actionId))
                Add(report, count > 1
                        ? CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate
                        : CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing,
                    path + "/actionConfiguration/reactionActionBindings/" + index,
                    "Every required or explicitly bound reaction actionRole must resolve exactly once to an effective combination Action.",
                    "Bind reaction slot '" + required.SlotId + "' role '" + required.ActionRole + "' to one Action id.");
        }

        private static void ValidateTwoHandRule(
            CharacterAuthoringValidationReport report,
            CombinedWeaponConfigurationAuthoringSummary configuration,
            Dictionary<string, SingleWeaponConfiguration> singles,
            string path)
        {
            CombinedWeaponConfigurationSlotAuthoringSummary main = FindSlot(configuration, MainHandSlotId);
            CombinedWeaponConfigurationSlotAuthoringSummary off = FindSlot(configuration, OffHandSlotId);
            if (main != null && singles.TryGetValue(main.SingleWeaponConfigurationId ?? string.Empty, out SingleWeaponConfiguration weapon) &&
                Contains(weapon.OccupiesSlots, MainHandSlotId) && Contains(weapon.OccupiesSlots, OffHandSlotId) &&
                (off == null || !GlobalWeaponLibrarySchema.IsUnarmed(off.SingleWeaponConfigurationId)))
                Add(report, CharacterAuthoringValidationCodes.TwoHandWeaponSlotConflict, path + "/slots",
                    "A two-hand weapon must be mounted in mainHand while offHand explicitly references weapon.unarmed.",
                    "Set offHand to weapon.unarmed and remove its attachment.");
        }

        private static CombinedWeaponConfigurationSlotAuthoringSummary FindSlot(
            CombinedWeaponConfigurationAuthoringSummary configuration,
            string slotId)
        {
            List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration?.Slots;
            for (int i = 0; slots != null && i < slots.Count; i++)
                if (slots[i] != null && string.Equals(slots[i].SlotId, slotId, StringComparison.Ordinal))
                    return slots[i];
            return null;
        }

        private static void ValidateAnimationReference(
            CharacterAuthoringValidationReport report,
            AnimationAuthoringPackage animation,
            string profileId,
            string field)
        {
            List<AnimationAuthoringProfile> profiles = animation?.Profiles;
            for (int i = 0; profiles != null && i < profiles.Count; i++)
                if (profiles[i] != null && string.Equals(profiles[i].ProfileId, profileId, StringComparison.Ordinal))
                    return;
            Add(report, CharacterAuthoringValidationCodes.CombinedWeaponAnimationProfileMissing, field,
                "The combination animationProfileId must exactly reference this package's Animation Profile.",
                "Choose an existing package-local Animation Profile.");
        }

        private static void ValidateAbilityReference(
            CharacterAuthoringValidationReport report,
            CharacterApplicationAuthoringSummary application,
            string abilityLoadoutId,
            string field)
        {
            List<CharacterAbilityLoadoutAuthoringSummary> loadouts = application?.AbilityLoadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterAbilityLoadoutAuthoringSummary item = loadouts[i];
                if (item != null && (string.Equals(item.StableId, abilityLoadoutId, StringComparison.Ordinal) ||
                    string.Equals(item.LoadoutId, abilityLoadoutId, StringComparison.Ordinal)))
                    return;
            }
            Add(report, CharacterAuthoringValidationCodes.CombinedWeaponAbilityLoadoutMissing, field,
                "The combination abilityLoadoutId must exactly reference this package's AbilityLoadout.",
                "Choose an existing package-local AbilityLoadout.");
        }

        private static bool IsValidUnarmed(SingleWeaponConfiguration item)
        {
            return item != null &&
                string.Equals(item.Category, "Unarmed", StringComparison.Ordinal) &&
                item.WeaponRef == null &&
                Contains(item.AllowedSlots, MainHandSlotId) &&
                Contains(item.AllowedSlots, OffHandSlotId) &&
                (item.OccupiesSlots == null || item.OccupiesSlots.Count == 0);
        }

        private static bool IsSupportedSlot(string slotId)
        {
            return string.Equals(slotId, MainHandSlotId, StringComparison.Ordinal) ||
                string.Equals(slotId, OffHandSlotId, StringComparison.Ordinal);
        }

        private static bool Contains(List<string> values, string value)
        {
            return values != null && values.Contains(value);
        }

        private static void Add(
            CharacterAuthoringValidationReport report,
            string code,
            string field,
            string message,
            string fix,
            string sourcePath = "config/character_application.json")
        {
            report.Issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ExportBlocked,
                Code = code,
                SourcePath = sourcePath,
                SourceObjectPath = field,
                Field = field,
                Message = message,
                SuggestedFix = fix
            });
        }
    }
}
