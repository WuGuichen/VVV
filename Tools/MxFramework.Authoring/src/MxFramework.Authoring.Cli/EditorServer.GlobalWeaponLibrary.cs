using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string GlobalWeaponLibraryRelativePath = "WeaponSource/global_weapon_library.json";

    internal static object ReadGlobalWeaponLibraryState(string rootPath, JsonSerializerOptions jsonOptions)
    {
        string path = GetGlobalWeaponLibraryPath(rootPath);
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        return new
        {
            schemaVersion = GlobalWeaponLibrarySchema.CurrentSchemaVersion,
            relativePath = GlobalWeaponLibraryRelativePath,
            exists = File.Exists(path),
            revision = ComputeGlobalWeaponLibraryRevision(rootPath),
            library
        };
    }

    private static void WriteGlobalConfigurationMutationResponse(
        HttpListenerResponse response,
        Func<object> operation,
        JsonSerializerOptions jsonOptions)
    {
        try
        {
            WriteJson(response, operation(), jsonOptions);
        }
        catch (GlobalWeaponLibraryWriteConflictException ex)
        {
            WriteJson(response, new
            {
                success = false,
                code = GlobalWeaponLibraryWriteConflictException.ConflictCode,
                expectedRevision = ex.ExpectedRevision,
                currentRevision = ex.CurrentRevision,
                message = ex.Message
            }, jsonOptions, 409);
        }
        catch (GlobalWeaponLibraryInUseException ex)
        {
            WriteJson(response, new
            {
                success = false,
                code = CharacterAuthoringValidationCodes.GlobalWeaponConfigurationInUse,
                configurationType = ex.ConfigurationType,
                configurationId = ex.ConfigurationId,
                references = ex.References,
                message = ex.Message
            }, jsonOptions, 409);
        }
        catch (GlobalConfigurationValidationException ex)
        {
            WriteJson(response, new
            {
                success = false,
                code = "GLOBAL_CONFIGURATION_VALIDATION_FAILED",
                validation = ex.Validation,
                message = ex.Message
            }, jsonOptions, 400);
        }
        catch (ArgumentException ex)
        {
            WriteJson(response, new
            {
                success = false,
                code = "GLOBAL_CONFIGURATION_REQUEST_INVALID",
                message = ex.Message
            }, jsonOptions, 400);
        }
    }

    internal static object SaveGlobalWeaponLibrary(
        string rootPath,
        GlobalWeaponLibrarySaveRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (request?.library == null)
            throw new ArgumentException("library is required.");
        string path = GetGlobalWeaponLibraryPath(rootPath);
        string currentRevision = ComputeGlobalWeaponLibraryRevision(rootPath);
        if (!request.force && !string.IsNullOrWhiteSpace(request.expectedRevision) &&
            !string.Equals(request.expectedRevision, currentRevision, StringComparison.Ordinal))
            throw new GlobalWeaponLibraryWriteConflictException(
                request.expectedRevision,
                currentRevision,
                "The global weapon library changed on disk after it was loaded. Reload or explicitly force the save.");

        request.library.SingleWeaponConfigurations ??= new List<SingleWeaponConfiguration>();
        request.library.AnimationConfigurations ??= new List<GlobalAnimationConfiguration>();
        request.library.AbilityConfigurations ??= new List<GlobalAbilityConfiguration>();
        request.library.ReactionConfigurations ??= new List<GlobalReactionConfiguration>();
        request.library.CombinedWeaponConfigurations ??= new List<CombinedWeaponConfigurationAuthoringSummary>();
        GlobalWeaponLibrary current = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        ProtectRemovedConfigurations(rootPath, current, request.library, jsonOptions);
        ValidateGlobalWeaponLibraryForWrite(request.library);
        request.library.SingleWeaponConfigurations.Sort((left, right) => string.CompareOrdinal(
            left?.SingleWeaponConfigurationId,
            right?.SingleWeaponConfigurationId));
        request.library.AnimationConfigurations.Sort((left, right) => string.CompareOrdinal(
            left?.AnimationConfigurationId,
            right?.AnimationConfigurationId));
        request.library.AbilityConfigurations.Sort((left, right) => string.CompareOrdinal(
            left?.AbilityConfigurationId,
            right?.AbilityConfigurationId));
        request.library.ReactionConfigurations.Sort((left, right) => string.CompareOrdinal(
            left?.ReactionConfigurationId,
            right?.ReactionConfigurationId));
        request.library.CombinedWeaponConfigurations.Sort((left, right) => string.CompareOrdinal(
            left?.CombinedWeaponConfigurationId,
            right?.CombinedWeaponConfigurationId));
        WriteJsonFileAtomic(path, request.library, jsonOptions);
        InvalidateAllAuthoringResourceCaches();
        return ReadGlobalWeaponLibraryState(rootPath, jsonOptions);
    }

    /// <summary>AllocByDesign: returns one configuration type with revision and reference summaries.</summary>
    internal static object ReadGlobalConfigurations(
        string rootPath,
        string configurationType,
        JsonSerializerOptions jsonOptions)
    {
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions) ??
            GlobalWeaponLibrarySchema.CreateDefault();
        object items = configurationType switch
        {
            "singleWeapon" => library.SingleWeaponConfigurations,
            "animation" => library.AnimationConfigurations,
            "ability" => library.AbilityConfigurations,
            "reaction" => library.ReactionConfigurations,
            "combinedWeapon" => library.CombinedWeaponConfigurations,
            _ => throw new ArgumentException("Unsupported global configuration type: " + configurationType)
        };
        return new
        {
            schemaVersion = GlobalWeaponLibrarySchema.CurrentSchemaVersion,
            configurationType,
            revision = ComputeGlobalWeaponLibraryRevision(rootPath),
            items,
            referenceIndex = BuildGlobalConfigurationReferenceIndex(rootPath, configurationType, jsonOptions)
        };
    }

    /// <summary>AllocByDesign: validates one configuration without writing repository state.</summary>
    internal static GlobalConfigurationValidationResult ValidateGlobalConfiguration(
        string rootPath,
        GlobalConfigurationMutationRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (request == null || request.configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ArgumentException("configuration is required.");
        object configuration = DeserializeGlobalConfiguration(
            request.configurationType,
            request.configuration,
            jsonOptions);
        var result = new GlobalConfigurationValidationResult
        {
            ConfigurationType = request.configurationType,
            ConfigurationId = GetGlobalConfigurationId(request.configurationType, configuration)
        };
        ValidateGlobalConfigurationContent(request.configurationType, configuration, result.Issues);
        result.Success = result.Issues.Count == 0;
        return result;
    }

    /// <summary>AllocByDesign: atomically creates or replaces one global configuration.</summary>
    internal static object SaveGlobalConfiguration(
        string rootPath,
        GlobalConfigurationMutationRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (request == null || request.configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ArgumentException("configuration is required.");
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions) ??
            GlobalWeaponLibrarySchema.CreateDefault();
        object configuration = DeserializeGlobalConfiguration(
            request.configurationType,
            request.configuration,
            jsonOptions);
        string id = GetGlobalConfigurationId(request.configurationType, configuration);
        var validation = new GlobalConfigurationValidationResult
        {
            ConfigurationType = request.configurationType,
            ConfigurationId = id
        };
        ValidateGlobalConfigurationContent(request.configurationType, configuration, validation.Issues);
        if (validation.Issues.Count > 0)
            throw new GlobalConfigurationValidationException(validation);

        bool create = string.Equals(request.operation, "create", StringComparison.OrdinalIgnoreCase);
        string originalId = request.configurationId ?? string.Empty;
        if (!create && !string.Equals(originalId, id, StringComparison.Ordinal))
            throw new ArgumentException("A stable global configuration ID cannot be changed after creation.");
        ReplaceGlobalConfiguration(library, request.configurationType, originalId, configuration, create);
        object state = SaveGlobalWeaponLibrary(rootPath, new GlobalWeaponLibrarySaveRequest
        {
            library = library,
            expectedRevision = request.expectedRevision,
            force = false
        }, jsonOptions);
        return new
        {
            success = true,
            configurationType = request.configurationType,
            configurationId = id,
            revision = ComputeGlobalWeaponLibraryRevision(rootPath),
            configuration,
            state
        };
    }

    /// <summary>AllocByDesign: removes one unreferenced global configuration atomically.</summary>
    internal static object DeleteGlobalConfiguration(
        string rootPath,
        GlobalConfigurationDeleteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.configurationId))
            throw new ArgumentException("configurationId is required.");
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions) ??
            throw new FileNotFoundException(GlobalWeaponLibraryRelativePath);
        bool removed = RemoveGlobalConfiguration(
            library,
            request.configurationType,
            request.configurationId);
        if (!removed)
            throw new ArgumentException("Global configuration not found: " + request.configurationId);
        SaveGlobalWeaponLibrary(rootPath, new GlobalWeaponLibrarySaveRequest
        {
            library = library,
            expectedRevision = request.expectedRevision,
            force = false
        }, jsonOptions);
        return new
        {
            success = true,
            configurationType = request.configurationType,
            configurationId = request.configurationId,
            revision = ComputeGlobalWeaponLibraryRevision(rootPath)
        };
    }

    private static object DeserializeGlobalConfiguration(
        string configurationType,
        JsonElement configuration,
        JsonSerializerOptions jsonOptions)
    {
        string json = configuration.GetRawText();
        return configurationType switch
        {
            "singleWeapon" => (object)JsonSerializer.Deserialize<SingleWeaponConfiguration>(json, jsonOptions),
            "animation" => JsonSerializer.Deserialize<GlobalAnimationConfiguration>(json, jsonOptions),
            "ability" => JsonSerializer.Deserialize<GlobalAbilityConfiguration>(json, jsonOptions),
            "reaction" => JsonSerializer.Deserialize<GlobalReactionConfiguration>(json, jsonOptions),
            "combinedWeapon" => JsonSerializer.Deserialize<CombinedWeaponConfigurationAuthoringSummary>(json, jsonOptions),
            _ => throw new ArgumentException("Unsupported global configuration type: " + configurationType)
        } ?? throw new ArgumentException("configuration could not be parsed.");
    }

    private static string GetGlobalConfigurationId(string configurationType, object configuration)
    {
        return configurationType switch
        {
            "singleWeapon" => ((SingleWeaponConfiguration)configuration).SingleWeaponConfigurationId,
            "animation" => ((GlobalAnimationConfiguration)configuration).AnimationConfigurationId,
            "ability" => ((GlobalAbilityConfiguration)configuration).AbilityConfigurationId,
            "reaction" => ((GlobalReactionConfiguration)configuration).ReactionConfigurationId,
            "combinedWeapon" => ((CombinedWeaponConfigurationAuthoringSummary)configuration).CombinedWeaponConfigurationId,
            _ => string.Empty
        } ?? string.Empty;
    }

    private static void ReplaceGlobalConfiguration(
        GlobalWeaponLibrary library,
        string configurationType,
        string originalId,
        object configuration,
        bool create)
    {
        if (configurationType == "singleWeapon")
            ReplaceConfiguration(library.SingleWeaponConfigurations, originalId,
                (SingleWeaponConfiguration)configuration, item => item?.SingleWeaponConfigurationId, create);
        else if (configurationType == "animation")
            ReplaceConfiguration(library.AnimationConfigurations, originalId,
                (GlobalAnimationConfiguration)configuration, item => item?.AnimationConfigurationId, create);
        else if (configurationType == "ability")
            ReplaceConfiguration(library.AbilityConfigurations, originalId,
                (GlobalAbilityConfiguration)configuration, item => item?.AbilityConfigurationId, create);
        else if (configurationType == "reaction")
            ReplaceConfiguration(library.ReactionConfigurations, originalId,
                (GlobalReactionConfiguration)configuration, item => item?.ReactionConfigurationId, create);
        else if (configurationType == "combinedWeapon")
            ReplaceConfiguration(library.CombinedWeaponConfigurations, originalId,
                (CombinedWeaponConfigurationAuthoringSummary)configuration,
                item => item?.CombinedWeaponConfigurationId,
                create);
        else
            throw new ArgumentException("Unsupported global configuration type: " + configurationType);
    }

    private static void ReplaceConfiguration<T>(
        List<T> items,
        string originalId,
        T configuration,
        Func<T, string> idSelector,
        bool create)
    {
        string nextId = idSelector(configuration) ?? string.Empty;
        int existingIndex = items.FindIndex(item => string.Equals(
            idSelector(item), create ? nextId : originalId, StringComparison.Ordinal));
        if (create)
        {
            if (existingIndex >= 0)
                throw new ArgumentException("Global configuration ID already exists: " + nextId);
            items.Add(configuration);
            return;
        }
        if (existingIndex < 0)
            throw new ArgumentException("Global configuration not found: " + originalId);
        items[existingIndex] = configuration;
    }

    private static bool RemoveGlobalConfiguration(
        GlobalWeaponLibrary library,
        string configurationType,
        string configurationId)
    {
        if (configurationType == "singleWeapon")
            return library.SingleWeaponConfigurations.RemoveAll(item =>
                string.Equals(item?.SingleWeaponConfigurationId, configurationId, StringComparison.Ordinal)) > 0;
        if (configurationType == "animation")
            return library.AnimationConfigurations.RemoveAll(item =>
                string.Equals(item?.AnimationConfigurationId, configurationId, StringComparison.Ordinal)) > 0;
        if (configurationType == "ability")
            return library.AbilityConfigurations.RemoveAll(item =>
                string.Equals(item?.AbilityConfigurationId, configurationId, StringComparison.Ordinal)) > 0;
        if (configurationType == "reaction")
            return library.ReactionConfigurations.RemoveAll(item =>
                string.Equals(item?.ReactionConfigurationId, configurationId, StringComparison.Ordinal)) > 0;
        if (configurationType == "combinedWeapon")
            return library.CombinedWeaponConfigurations.RemoveAll(item =>
                string.Equals(item?.CombinedWeaponConfigurationId, configurationId, StringComparison.Ordinal)) > 0;
        throw new ArgumentException("Unsupported global configuration type: " + configurationType);
    }

    private static void ValidateGlobalConfigurationContent(
        string configurationType,
        object configuration,
        List<GlobalConfigurationValidationIssue> issues)
    {
        string id = GetGlobalConfigurationId(configurationType, configuration);
        if (string.IsNullOrWhiteSpace(id))
            issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_CONFIGURATION_ID_REQUIRED",
                "configurationId", "Stable configuration ID is required."));
        if (configurationType == "animation")
            ValidateAnimationConfiguration((GlobalAnimationConfiguration)configuration, issues);
        else if (configurationType == "reaction")
            ValidateReactionConfiguration((GlobalReactionConfiguration)configuration, issues);
        else if (configurationType == "combinedWeapon")
            ValidateCombinedWeaponConfiguration(
                (CombinedWeaponConfigurationAuthoringSummary)configuration,
                issues);
    }

    private static void ValidateCombinedWeaponConfiguration(
        CombinedWeaponConfigurationAuthoringSummary configuration,
        List<GlobalConfigurationValidationIssue> issues)
    {
        var slots = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; configuration?.Slots != null && i < configuration.Slots.Count; i++)
        {
            CombinedWeaponConfigurationSlotAuthoringSummary slot = configuration.Slots[i];
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId) || !slots.Add(slot.SlotId) ||
                string.IsNullOrWhiteSpace(slot.SingleWeaponConfigurationId))
                issues.Add(GlobalConfigurationValidationIssue.Error(
                    "GLOBAL_COMBINED_WEAPON_SLOT_INVALID",
                    "slots/" + i,
                    "A public combination requires unique mainHand/offHand slots and explicit weapons."));
        }
        if (!slots.Contains(CharacterWeaponConfigurationValidator.MainHandSlotId) ||
            !slots.Contains(CharacterWeaponConfigurationValidator.OffHandSlotId) || slots.Count != 2)
            issues.Add(GlobalConfigurationValidationIssue.Error(
                CharacterAuthoringValidationCodes.CombinedWeaponSlotMissing,
                "slots",
                "A public combination must contain exactly mainHand and offHand."));
    }

    private static void ValidateAnimationConfiguration(
        GlobalAnimationConfiguration configuration,
        List<GlobalConfigurationValidationIssue> issues)
    {
        bool locomotion = false;
        var setIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; configuration?.Sets != null && i < configuration.Sets.Count; i++)
        {
            AnimationAuthoringSet set = configuration.Sets[i];
            if (set == null || string.IsNullOrWhiteSpace(set.SetId) || !setIds.Add(set.SetId))
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_ANIMATION_SET_ID_DUPLICATE",
                    "sets/" + i + "/setId", "Animation Set IDs are required and must be unique."));
            for (int groupIndex = 0; set?.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
                locomotion |= string.Equals(set.Groups[groupIndex]?.Usage, "locomotion", StringComparison.OrdinalIgnoreCase);
        }
        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; configuration?.Profiles != null && i < configuration.Profiles.Count; i++)
        {
            AnimationAuthoringProfile profile = configuration.Profiles[i];
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId) || !profileIds.Add(profile.ProfileId))
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_ANIMATION_PROFILE_ID_DUPLICATE",
                    "profiles/" + i + "/profileId", "Animation Profile IDs are required and must be unique."));
            for (int slotIndex = 0; profile?.Slots != null && slotIndex < profile.Slots.Count; slotIndex++)
                locomotion |= string.Equals(profile.Slots[slotIndex]?.SlotId, "locomotion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(profile.Slots[slotIndex]?.Purpose, "locomotion", StringComparison.OrdinalIgnoreCase);
        }
        if (!locomotion)
            issues.Add(GlobalConfigurationValidationIssue.Error(
                CharacterAuthoringValidationCodes.GlobalAnimationLocomotionMissing,
                "profiles", "A complete global animation configuration requires locomotion."));
    }

    private static void ValidateReactionConfiguration(
        GlobalReactionConfiguration configuration,
        List<GlobalConfigurationValidationIssue> issues)
    {
        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; configuration?.Slots != null && i < configuration.Slots.Count; i++)
        {
            GlobalReactionSlotConfiguration slot = configuration.Slots[i];
            string path = "slots/" + i;
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId) || !slotIds.Add(slot.SlotId))
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_REACTION_SLOT_ID_DUPLICATE",
                    path + "/slotId", "Reaction Slot IDs are required and must be unique."));
            if (slot != null && string.IsNullOrWhiteSpace(slot.ActionRole))
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_REACTION_ACTION_ROLE_REQUIRED",
                    path + "/actionRole", "Every reaction slot requires an actionRole."));
            if (slot?.MinImpactForce.HasValue == true && slot.MaxImpactForce.HasValue &&
                slot.MinImpactForce.Value > slot.MaxImpactForce.Value)
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_REACTION_IMPACT_RANGE_INVALID",
                    path + "/minImpactForce", "minImpactForce cannot exceed maxImpactForce."));
            if (slot?.Required == true && string.IsNullOrWhiteSpace(slot.ResourceKey) &&
                string.IsNullOrWhiteSpace(slot.ResourceSelection?.RuntimeResourceKey))
                issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_REACTION_RESOURCE_REQUIRED",
                    path + "/resourceSelection", "A required reaction slot needs an animation resource."));
        }
        if (!string.IsNullOrWhiteSpace(configuration?.DefaultSlotId) && !slotIds.Contains(configuration.DefaultSlotId))
            issues.Add(GlobalConfigurationValidationIssue.Error("GLOBAL_REACTION_DEFAULT_SLOT_NOT_FOUND",
                "defaultSlotId", "defaultSlotId must reference an existing reaction slot."));
    }

    internal static string ComputeGlobalWeaponLibraryRevision(string rootPath)
    {
        string path = GetGlobalWeaponLibraryPath(rootPath);
        return File.Exists(path) ? CharacterPackageHashUtility.ComputeFileSha256(path) : "missing";
    }

    internal static GlobalWeaponLibrary ReadGlobalWeaponLibraryAtRoot(
        string rootPath,
        JsonSerializerOptions jsonOptions)
    {
        string path = GetGlobalWeaponLibraryPath(rootPath);
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<GlobalWeaponLibrary>(File.ReadAllText(path), jsonOptions);
    }

    private static string GetGlobalWeaponLibraryPath(string rootPath)
    {
        return ResolveSafePath(rootPath, GlobalWeaponLibraryRelativePath);
    }

    private static void ValidateGlobalWeaponLibraryForWrite(GlobalWeaponLibrary library)
    {
        if (!string.Equals(library.SchemaVersion, GlobalWeaponLibrarySchema.CurrentSchemaVersion, StringComparison.Ordinal))
            throw new ArgumentException(CharacterAuthoringValidationCodes.GlobalWeaponLibrarySchemaUnsupported);
        if (string.IsNullOrWhiteSpace(library.LibraryId))
            throw new ArgumentException("libraryId is required.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        int unarmedCount = 0;
        List<SingleWeaponConfiguration> items = library.SingleWeaponConfigurations ??
            new List<SingleWeaponConfiguration>();
        for (int i = 0; i < items.Count; i++)
        {
            SingleWeaponConfiguration item = items[i];
            string id = item?.SingleWeaponConfigurationId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new ArgumentException(
                    CharacterAuthoringValidationCodes.SingleWeaponConfigurationIdDuplicate + ": " + id);
            if (GlobalWeaponLibrarySchema.IsUnarmed(id))
            {
                unarmedCount++;
                if (!string.Equals(item.Category, "Unarmed", StringComparison.Ordinal) ||
                    item.WeaponRef != null || item.OccupiesSlots?.Count > 0 ||
                    item.AllowedSlots == null ||
                    !item.AllowedSlots.Contains(CharacterWeaponConfigurationValidator.MainHandSlotId) ||
                    !item.AllowedSlots.Contains(CharacterWeaponConfigurationValidator.OffHandSlotId))
                    throw new ArgumentException(CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid);
            }
        }
        if (unarmedCount != 1)
            throw new ArgumentException(CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid);

        ValidateUniqueIds(
            library.AnimationConfigurations,
            item => item?.AnimationConfigurationId,
            CharacterAuthoringValidationCodes.GlobalAnimationConfigurationIdDuplicate);
        ValidateUniqueIds(
            library.AbilityConfigurations,
            item => item?.AbilityConfigurationId,
            CharacterAuthoringValidationCodes.GlobalAbilityConfigurationIdDuplicate);
        ValidateUniqueIds(
            library.ReactionConfigurations,
            item => item?.ReactionConfigurationId,
            CharacterAuthoringValidationCodes.GlobalReactionConfigurationIdDuplicate);
        ValidateUniqueIds(
            library.CombinedWeaponConfigurations,
            item => item?.CombinedWeaponConfigurationId,
            CharacterAuthoringValidationCodes.GlobalCombinedWeaponConfigurationIdDuplicate);

        for (int i = 0; library.CombinedWeaponConfigurations != null && i < library.CombinedWeaponConfigurations.Count; i++)
        {
            CombinedWeaponConfigurationAuthoringSummary combined = library.CombinedWeaponConfigurations[i];
            var issues = new List<GlobalConfigurationValidationIssue>();
            ValidateCombinedWeaponConfiguration(combined, issues);
            if (issues.Count > 0)
                throw new ArgumentException(issues[0].Code + ": " + combined?.CombinedWeaponConfigurationId);
            for (int slotIndex = 0; combined?.Slots != null && slotIndex < combined.Slots.Count; slotIndex++)
                if (!items.Exists(item => string.Equals(
                    item?.SingleWeaponConfigurationId,
                    combined.Slots[slotIndex]?.SingleWeaponConfigurationId,
                    StringComparison.Ordinal)))
                    throw new ArgumentException(
                        CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound + ": " +
                        combined.Slots[slotIndex]?.SingleWeaponConfigurationId);
            if (!string.IsNullOrWhiteSpace(combined?.AnimationConfigurationId) &&
                !library.AnimationConfigurations.Exists(item => string.Equals(
                    item?.AnimationConfigurationId,
                    combined.AnimationConfigurationId,
                    StringComparison.Ordinal)))
                throw new ArgumentException(
                    CharacterAuthoringValidationCodes.GlobalAnimationConfigurationNotFound + ": " +
                    combined.AnimationConfigurationId);
            if (!string.IsNullOrWhiteSpace(combined?.AbilityConfigurationId) &&
                !library.AbilityConfigurations.Exists(item => string.Equals(
                    item?.AbilityConfigurationId,
                    combined.AbilityConfigurationId,
                    StringComparison.Ordinal)))
                throw new ArgumentException(
                    CharacterAuthoringValidationCodes.GlobalAbilityConfigurationNotFound + ": " +
                    combined.AbilityConfigurationId);
            if (!string.IsNullOrWhiteSpace(combined?.ReactionConfigurationId) &&
                !library.ReactionConfigurations.Exists(item => string.Equals(
                    item?.ReactionConfigurationId,
                    combined.ReactionConfigurationId,
                    StringComparison.Ordinal)))
                throw new ArgumentException(
                    CharacterAuthoringValidationCodes.GlobalReactionConfigurationNotFound + ": " +
                    combined.ReactionConfigurationId);
        }
    }

    private static void ValidateUniqueIds<T>(List<T> items, Func<T, string> selector, string code)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; items != null && i < items.Count; i++)
        {
            string id = selector(items[i]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new ArgumentException(code + ": " + id);
        }
    }

    private static void ProtectRemovedConfigurations(
        string rootPath,
        GlobalWeaponLibrary current,
        GlobalWeaponLibrary next,
        JsonSerializerOptions jsonOptions)
    {
        if (current == null)
            return;
        ProtectRemoved(
            rootPath,
            "singleWeapon",
            Ids(current.SingleWeaponConfigurations, item => item?.SingleWeaponConfigurationId),
            Ids(next.SingleWeaponConfigurations, item => item?.SingleWeaponConfigurationId),
            jsonOptions);
        ProtectRemoved(
            rootPath,
            "animation",
            Ids(current.AnimationConfigurations, item => item?.AnimationConfigurationId),
            Ids(next.AnimationConfigurations, item => item?.AnimationConfigurationId),
            jsonOptions);
        ProtectRemoved(
            rootPath,
            "ability",
            Ids(current.AbilityConfigurations, item => item?.AbilityConfigurationId),
            Ids(next.AbilityConfigurations, item => item?.AbilityConfigurationId),
            jsonOptions);
        ProtectRemoved(
            rootPath,
            "reaction",
            Ids(current.ReactionConfigurations, item => item?.ReactionConfigurationId),
            Ids(next.ReactionConfigurations, item => item?.ReactionConfigurationId),
            jsonOptions);
        ProtectRemoved(
            rootPath,
            "combinedWeapon",
            Ids(current.CombinedWeaponConfigurations, item => item?.CombinedWeaponConfigurationId),
            Ids(next.CombinedWeaponConfigurations, item => item?.CombinedWeaponConfigurationId),
            jsonOptions);
    }

    private static HashSet<string> Ids<T>(List<T> items, Func<T, string> selector)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; items != null && i < items.Count; i++)
        {
            string id = selector(items[i]);
            if (!string.IsNullOrWhiteSpace(id))
                result.Add(id);
        }
        return result;
    }

    private static void ProtectRemoved(
        string rootPath,
        string configurationType,
        HashSet<string> currentIds,
        HashSet<string> nextIds,
        JsonSerializerOptions jsonOptions)
    {
        foreach (string id in currentIds)
        {
            if (nextIds.Contains(id))
                continue;
            if (configurationType == "singleWeapon" && GlobalWeaponLibrarySchema.IsUnarmed(id))
                throw new ArgumentException(CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid);
            List<GlobalWeaponLibraryReference> references = FindGlobalConfigurationReferences(
                rootPath,
                configurationType,
                id,
                jsonOptions);
            if (references.Count > 0)
                throw new GlobalWeaponLibraryInUseException(configurationType, id, references);
        }
    }

    internal static List<GlobalWeaponLibraryReference> FindGlobalConfigurationReferences(
        string rootPath,
        string configurationType,
        string configurationId,
        JsonSerializerOptions jsonOptions)
    {
        var result = new List<GlobalWeaponLibraryReference>();
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        foreach (string directory in EnumerateCharacterPackageDirectories(rootPath))
        {
            string applicationPath = Path.Combine(directory, "config", "character_application.json");
            if (!File.Exists(applicationPath))
                continue;
            CharacterApplicationAuthoringSummary application;
            try
            {
                application = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(
                    File.ReadAllText(applicationPath),
                    jsonOptions);
            }
            catch
            {
                continue;
            }
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                GlobalWeaponConfigurationResolver.ResolveCombinedWeapons(application, library);
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = configurations[i];
                if (configurationType == "combinedWeapon" && string.Equals(
                    configuration?.CombinedWeaponConfigurationId,
                    configurationId,
                    StringComparison.Ordinal))
                    AddReference(result, directory, rootPath, configuration, configurationType, configurationId, string.Empty);
                if (configurationType == "animation" && string.Equals(
                    configuration?.AnimationConfigurationId,
                    configurationId,
                    StringComparison.Ordinal))
                    AddReference(result, directory, rootPath, configuration, configurationType, configurationId, string.Empty);
                if (configurationType == "ability" && string.Equals(
                    configuration?.AbilityConfigurationId,
                    configurationId,
                    StringComparison.Ordinal))
                    AddReference(result, directory, rootPath, configuration, configurationType, configurationId, string.Empty);
                if (configurationType == "reaction" && string.Equals(
                    configuration?.ReactionConfigurationId,
                    configurationId,
                    StringComparison.Ordinal))
                    AddReference(result, directory, rootPath, configuration, configurationType, configurationId, string.Empty);
                if (configurationType != "singleWeapon")
                    continue;
                List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration?.Slots;
                for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                {
                    CombinedWeaponConfigurationSlotAuthoringSummary slot = slots[slotIndex];
                    if (slot != null && string.Equals(
                        slot.SingleWeaponConfigurationId,
                        configurationId,
                        StringComparison.Ordinal))
                        AddReference(result, directory, rootPath, configuration, configurationType, configurationId, slot.SlotId);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Builds the complete low-frequency authoring reference index in one package scan.
    /// AllocByDesign: returns lists and distinct reference counts for editor management views.
    /// </summary>
    internal static GlobalWeaponLibraryReferenceIndex BuildGlobalConfigurationReferenceIndex(
        string rootPath,
        string configurationType,
        JsonSerializerOptions jsonOptions)
    {
        GlobalWeaponLibrary library = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        List<string> configurationIds = GetGlobalConfigurationIds(library, configurationType);
        var entriesById = new Dictionary<string, GlobalWeaponLibraryReferenceIndexEntry>(StringComparer.Ordinal);
        for (int i = 0; i < configurationIds.Count; i++)
        {
            string configurationId = configurationIds[i];
            entriesById[configurationId] = new GlobalWeaponLibraryReferenceIndexEntry
            {
                ConfigurationId = configurationId
            };
        }

        foreach (string directory in EnumerateCharacterPackageDirectories(rootPath))
        {
            string applicationPath = Path.Combine(directory, "config", "character_application.json");
            CharacterApplicationAuthoringSummary application = ReadCharacterApplicationForReferences(
                applicationPath,
                jsonOptions);
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                GlobalWeaponConfigurationResolver.ResolveCombinedWeapons(application, library);
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary configuration = configurations[i];
                if (configurationType == "combinedWeapon")
                {
                    AddIndexedReference(
                        entriesById,
                        directory,
                        rootPath,
                        configuration,
                        configurationType,
                        configuration?.CombinedWeaponConfigurationId,
                        string.Empty);
                    continue;
                }
                if (configurationType == "singleWeapon")
                {
                    List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configuration?.Slots;
                    for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                    {
                        CombinedWeaponConfigurationSlotAuthoringSummary slot = slots[slotIndex];
                        AddIndexedReference(
                            entriesById,
                            directory,
                            rootPath,
                            configuration,
                            configurationType,
                            slot?.SingleWeaponConfigurationId,
                            slot?.SlotId);
                    }
                    continue;
                }

                string configurationId = configurationType switch
                {
                    "animation" => configuration?.AnimationConfigurationId,
                    "ability" => configuration?.AbilityConfigurationId,
                    "reaction" => configuration?.ReactionConfigurationId,
                    _ => string.Empty
                };
                AddIndexedReference(
                    entriesById,
                    directory,
                    rootPath,
                    configuration,
                    configurationType,
                    configurationId,
                    string.Empty);
            }
        }

        var result = new GlobalWeaponLibraryReferenceIndex
        {
            ConfigurationType = configurationType
        };
        foreach (GlobalWeaponLibraryReferenceIndexEntry entry in entriesById.Values)
        {
            entry.References.Sort(CompareGlobalWeaponLibraryReferences);
            entry.ReferenceCount = entry.References.Count;
            var characters = new HashSet<string>(StringComparer.Ordinal);
            var combinations = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entry.References.Count; i++)
            {
                GlobalWeaponLibraryReference reference = entry.References[i];
                characters.Add(reference.PackageRelative ?? string.Empty);
                combinations.Add((reference.PackageRelative ?? string.Empty) + "\n" +
                    (reference.CombinedWeaponConfigurationId ?? string.Empty));
            }
            entry.CharacterCount = characters.Count;
            entry.CombinationCount = combinations.Count;
            result.Entries.Add(entry);
        }
        result.Entries.Sort((left, right) => string.CompareOrdinal(
            left?.ConfigurationId,
            right?.ConfigurationId));
        return result;
    }

    private static List<string> GetGlobalConfigurationIds(
        GlobalWeaponLibrary library,
        string configurationType)
    {
        var result = new List<string>();
        if (configurationType == "singleWeapon")
        {
            List<SingleWeaponConfiguration> configurations = library?.SingleWeaponConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
                AddGlobalConfigurationId(result, configurations[i]?.SingleWeaponConfigurationId);
        }
        else if (configurationType == "animation")
        {
            List<GlobalAnimationConfiguration> configurations = library?.AnimationConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
                AddGlobalConfigurationId(result, configurations[i]?.AnimationConfigurationId);
        }
        else if (configurationType == "ability")
        {
            List<GlobalAbilityConfiguration> configurations = library?.AbilityConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
                AddGlobalConfigurationId(result, configurations[i]?.AbilityConfigurationId);
        }
        else if (configurationType == "reaction")
        {
            List<GlobalReactionConfiguration> configurations = library?.ReactionConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
                AddGlobalConfigurationId(result, configurations[i]?.ReactionConfigurationId);
        }
        else if (configurationType == "combinedWeapon")
        {
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                library?.CombinedWeaponConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
                AddGlobalConfigurationId(result, configurations[i]?.CombinedWeaponConfigurationId);
        }
        else
        {
            throw new ArgumentException("Unsupported global configuration type: " + configurationType);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void AddGlobalConfigurationId(List<string> result, string configurationId)
    {
        if (!string.IsNullOrWhiteSpace(configurationId))
            result.Add(configurationId);
    }

    private static CharacterApplicationAuthoringSummary ReadCharacterApplicationForReferences(
        string applicationPath,
        JsonSerializerOptions jsonOptions)
    {
        if (!File.Exists(applicationPath))
            return null;
        try
        {
            return JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(
                File.ReadAllText(applicationPath),
                jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void AddIndexedReference(
        Dictionary<string, GlobalWeaponLibraryReferenceIndexEntry> entriesById,
        string directory,
        string rootPath,
        CombinedWeaponConfigurationAuthoringSummary configuration,
        string configurationType,
        string configurationId,
        string slotId)
    {
        if (string.IsNullOrWhiteSpace(configurationId) ||
            !entriesById.TryGetValue(configurationId, out GlobalWeaponLibraryReferenceIndexEntry entry))
            return;
        entry.References.Add(CreateGlobalWeaponLibraryReference(
            directory,
            rootPath,
            configuration,
            configurationType,
            configurationId,
            slotId));
    }

    private static int CompareGlobalWeaponLibraryReferences(
        GlobalWeaponLibraryReference left,
        GlobalWeaponLibraryReference right)
    {
        int packageCompare = string.CompareOrdinal(left?.PackageRelative, right?.PackageRelative);
        if (packageCompare != 0)
            return packageCompare;
        int combinationCompare = string.CompareOrdinal(
            left?.CombinedWeaponConfigurationId,
            right?.CombinedWeaponConfigurationId);
        return combinationCompare != 0
            ? combinationCompare
            : string.CompareOrdinal(left?.SlotId, right?.SlotId);
    }

    private static void AddReference(
        List<GlobalWeaponLibraryReference> result,
        string directory,
        string rootPath,
        CombinedWeaponConfigurationAuthoringSummary configuration,
        string configurationType,
        string configurationId,
        string slotId)
    {
        result.Add(CreateGlobalWeaponLibraryReference(
            directory,
            rootPath,
            configuration,
            configurationType,
            configurationId,
            slotId));
    }

    private static GlobalWeaponLibraryReference CreateGlobalWeaponLibraryReference(
        string directory,
        string rootPath,
        CombinedWeaponConfigurationAuthoringSummary configuration,
        string configurationType,
        string configurationId,
        string slotId)
    {
        return new GlobalWeaponLibraryReference
        {
            ConfigurationType = configurationType,
            ConfigurationId = configurationId,
            PackageRelative = ToAuthoringRelativePath(rootPath, directory),
            CombinedWeaponConfigurationId = configuration?.CombinedWeaponConfigurationId ?? string.Empty,
            SlotId = slotId
        };
    }

    private static void InvalidateAllAuthoringResourceCaches()
    {
        lock (AuthoringResourceCacheGate)
            AuthoringResourceCache.Clear();
    }

    internal sealed class GlobalWeaponLibrarySaveRequest
    {
        public GlobalWeaponLibrary library { get; set; }
        public string expectedRevision { get; set; } = string.Empty;
        public bool force { get; set; }
    }

    internal sealed class GlobalConfigurationMutationRequest
    {
        public string configurationType { get; set; } = string.Empty;
        public string configurationId { get; set; } = string.Empty;
        public string operation { get; set; } = "update";
        public string expectedRevision { get; set; } = string.Empty;
        public JsonElement configuration { get; set; }
    }

    internal sealed class GlobalConfigurationDeleteRequest
    {
        public string configurationType { get; set; } = string.Empty;
        public string configurationId { get; set; } = string.Empty;
        public string expectedRevision { get; set; } = string.Empty;
    }

    internal sealed class GlobalConfigurationValidationResult
    {
        public bool Success { get; set; }
        public string ConfigurationType { get; set; } = string.Empty;
        public string ConfigurationId { get; set; } = string.Empty;
        public List<GlobalConfigurationValidationIssue> Issues { get; set; } =
            new List<GlobalConfigurationValidationIssue>();
    }

    internal sealed class GlobalConfigurationValidationIssue
    {
        public string Severity { get; set; } = "Error";
        public string Code { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public static GlobalConfigurationValidationIssue Error(string code, string field, string message)
        {
            return new GlobalConfigurationValidationIssue
            {
                Code = code ?? string.Empty,
                Field = field ?? string.Empty,
                Message = message ?? string.Empty
            };
        }
    }

    internal sealed class GlobalConfigurationValidationException : ArgumentException
    {
        public GlobalConfigurationValidationException(GlobalConfigurationValidationResult validation)
            : base("Global configuration validation failed.")
        {
            Validation = validation;
        }

        public GlobalConfigurationValidationResult Validation { get; }
    }

    internal sealed class GlobalWeaponLibraryReference
    {
        public string ConfigurationType { get; set; } = string.Empty;
        public string ConfigurationId { get; set; } = string.Empty;
        public string PackageRelative { get; set; } = string.Empty;
        public string CombinedWeaponConfigurationId { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
    }

    internal sealed class GlobalWeaponLibraryReferenceIndex
    {
        public string ConfigurationType { get; set; } = string.Empty;
        public List<GlobalWeaponLibraryReferenceIndexEntry> Entries { get; set; } =
            new List<GlobalWeaponLibraryReferenceIndexEntry>();
    }

    internal sealed class GlobalWeaponLibraryReferenceIndexEntry
    {
        public string ConfigurationId { get; set; } = string.Empty;
        public int ReferenceCount { get; set; }
        public int CharacterCount { get; set; }
        public int CombinationCount { get; set; }
        public List<GlobalWeaponLibraryReference> References { get; set; } =
            new List<GlobalWeaponLibraryReference>();
    }

    internal sealed class GlobalWeaponLibraryWriteConflictException : InvalidOperationException
    {
        public const string ConflictCode = "GLOBAL_WEAPON_LIBRARY_WRITE_CONFLICT";
        public GlobalWeaponLibraryWriteConflictException(
            string expectedRevision,
            string currentRevision,
            string message) : base(message)
        {
            ExpectedRevision = expectedRevision ?? string.Empty;
            CurrentRevision = currentRevision ?? string.Empty;
        }
        public string ExpectedRevision { get; }
        public string CurrentRevision { get; }
    }

    internal sealed class GlobalWeaponLibraryInUseException : InvalidOperationException
    {
        public GlobalWeaponLibraryInUseException(
            string configurationType,
            string configurationId,
            List<GlobalWeaponLibraryReference> references)
            : base("The global configuration is still referenced by character combinations.")
        {
            ConfigurationType = configurationType ?? string.Empty;
            ConfigurationId = configurationId ?? string.Empty;
            References = references ?? new List<GlobalWeaponLibraryReference>();
        }
        public string ConfigurationType { get; }
        public string ConfigurationId { get; }
        public string SingleWeaponConfigurationId => ConfigurationType == "singleWeapon"
            ? ConfigurationId
            : string.Empty;
        public List<GlobalWeaponLibraryReference> References { get; }
    }
}
