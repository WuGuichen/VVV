using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleCharacterActionLibraryApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/character-actions/catalog" && context.Request.HttpMethod == "GET")
        {
            string packageRelative = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterActionCatalog(rootPath, packageRelative, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/character-actions/copy-to-character" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterActionCopyRequest request = JsonSerializer.Deserialize<CharacterActionCopyRequest>(body, jsonOptions)
                ?? new CharacterActionCopyRequest();
            try
            {
                WriteJson(context.Response, CopyCommonActionToCharacter(rootPath, request, jsonOptions), jsonOptions);
            }
            catch (CharacterPackageWriteConflictException ex)
            {
                WriteCharacterPackageWriteConflict(context.Response, ex, jsonOptions);
            }
            catch (CharacterActionSourceConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = CharacterActionSourceConflictException.ConflictCode,
                    expectedHash = ex.ExpectedHash,
                    currentHash = ex.CurrentHash,
                    message = ex.Message,
                }, jsonOptions, 409);
            }
            return true;
        }

        return false;
    }

    internal static object ReadCharacterActionCatalog(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string packageSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
        string commonPath = package.CommonCharacterActionLibraryPath;
        string characterPath = package.CharacterActionLibraryPath;
        string commonHash = File.Exists(commonPath) ? CharacterPackageHashUtility.ComputeFileSha256(commonPath) : string.Empty;
        string characterHash = File.Exists(characterPath) ? CharacterPackageHashUtility.ComputeFileSha256(characterPath) : string.Empty;

        var actions = new List<object>();
        AddCharacterActionCatalogItems(
            actions,
            package,
            package.CommonCharacterActionLibrary,
            "Common",
            "common",
            commonPath,
            commonHash,
            readOnly: true);
        AddCharacterActionCatalogItems(
            actions,
            package,
            package.CharacterActionLibrary,
            packageSegment,
            "character",
            characterPath,
            characterHash,
            readOnly: false);

        var actionSets = new List<object>();
        List<CombinedWeaponConfigurationAuthoringSummary> configurations =
            GlobalWeaponConfigurationResolver.ResolveCombinedWeapons(package.ApplicationConfig, package.GlobalWeaponLibrary);
        for (int i = 0; i < configurations.Count; i++)
        {
            CombinedWeaponConfigurationAuthoringSummary configuration = configurations[i];
            if (configuration == null)
                continue;
            string identity = GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                packageId,
                configuration.CombinedWeaponConfigurationId);
            actionSets.Add(new
            {
                combinedWeaponConfigurationId = configuration.CombinedWeaponConfigurationId,
                actionSetId = packageSegment + "." + CharacterPackageResourceKeyGenerator.NormalizeSegment(identity),
            });
        }

        return new
        {
            schemaVersion = "1.0",
            packageRelative,
            packageId,
            packageSegment,
            packageRevision = ComputeCharacterPackageRevision(packagePath),
            profileId = packageSegment + ".default",
            profileSourceDirectory = $"Assets/MxFrameworkGenerated/CharacterActions/Profiles/{packageSegment}",
            actionSetSourceDirectory = $"Assets/MxFrameworkGenerated/CharacterActions/ActionSets/{packageSegment}",
            libraries = new[]
            {
                new
                {
                    libraryId = "Common",
                    sourceKind = "common",
                    sourcePath = ToProjectRelativePath(rootPath, commonPath),
                    sourceDirectory = ToProjectRelativePath(rootPath, Path.GetDirectoryName(commonPath) ?? string.Empty),
                    sourceHash = commonHash,
                    readOnly = true,
                },
                new
                {
                    libraryId = packageSegment,
                    sourceKind = "character",
                    sourcePath = ToProjectRelativePath(rootPath, characterPath),
                    sourceDirectory = ToProjectRelativePath(rootPath, Path.GetDirectoryName(characterPath) ?? string.Empty),
                    sourceHash = characterHash,
                    readOnly = false,
                },
            },
            actionSets,
            actions,
        };
    }

    private static void AddCharacterActionCatalogItems(
        List<object> destination,
        CharacterResourcePackage package,
        CharacterActionSourceLibrary library,
        string libraryId,
        string sourceKind,
        string sourcePath,
        string sourceHash,
        bool readOnly)
    {
        for (int i = 0; library?.Actions != null && i < library.Actions.Count; i++)
        {
            JsonElement action = library.Actions[i];
            if (action.ValueKind != JsonValueKind.Object)
                continue;
            int actionId = ReadInt32(action, "actionId");
            if (actionId <= 0)
                continue;
            JsonElement animationEvent = FindFirstTrackEvent(action, "animationTrack");
            int hitTrackCount = CountHitTrackEvents(action);
            GlobalAnimationCompatibilityResult compatibility =
                CharacterActionAnimationCompatibilityResolver.Resolve(
                    package,
                    action,
                    package.CommonCharacterActionLibrary,
                    package.CharacterActionLibrary);
            string compatibilityKind = compatibility.Kind switch
            {
                GlobalAnimationCompatibilityKind.Native => "Native",
                GlobalAnimationCompatibilityKind.HumanoidRetargeting => "HumanoidRetarget",
                _ => "Incompatible",
            };
            string compatibilityLabel = compatibility.Kind switch
            {
                GlobalAnimationCompatibilityKind.Native => "原生",
                GlobalAnimationCompatibilityKind.HumanoidRetargeting => "Humanoid 重定向",
                _ => "不兼容",
            };
            destination.Add(new
            {
                actionId,
                displayName = ReadString(action, "displayName"),
                category = ReadString(action, "category"),
                durationFrames = ReadInt32(action, "durationFrames"),
                animationResourceKey = ReadString(animationEvent, "animationClipResourceKey"),
                animationActionKey = ReadString(animationEvent, "animationActionKey"),
                hitTrackCount,
                hasHitTrack = hitTrackCount > 0,
                animationOnly = ContainsString(action, "tags", "animation_only"),
                libraryId,
                sourceKind,
                sourcePath,
                sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty,
                sourceHash,
                readOnly,
                compatibilityKind,
                compatibilityLabel,
                compatibilityMessage = compatibility.Message,
            });
        }
    }

    internal static object CopyCommonActionToCharacter(
        string rootPath,
        CharacterActionCopyRequest request,
        JsonSerializerOptions jsonOptions)
    {
        return CopyCommonActionToCharacter(
            rootPath,
            request,
            jsonOptions,
            beforeApplicationWrite: null);
    }

    internal static object CopyCommonActionToCharacter(
        string rootPath,
        CharacterActionCopyRequest request,
        JsonSerializerOptions jsonOptions,
        Action beforeApplicationWrite)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.package))
            throw new ArgumentException("package is required.");
        if (request.sourceActionId <= 0)
            throw new ArgumentException("sourceActionId is required.");
        if (string.IsNullOrWhiteSpace(request.combinedWeaponConfigurationId))
            throw new ArgumentException("combinedWeaponConfigurationId is required.");
        if (string.IsNullOrWhiteSpace(request.actionRole))
            throw new ArgumentException("actionRole is required.");

        string packageRelative = NormalizePackageRelativePath(request.package);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        EnsureCharacterPackageRevision(packagePath, request.expectedRevision, force: false);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string commonPath = package.CommonCharacterActionLibraryPath;
        string currentCommonHash = File.Exists(commonPath)
            ? CharacterPackageHashUtility.ComputeFileSha256(commonPath)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(request.sourceHash)
            && !string.Equals(request.sourceHash, currentCommonHash, StringComparison.Ordinal))
        {
            throw new CharacterActionSourceConflictException(
                request.sourceHash,
                currentCommonHash,
                "Common CharacterAction source changed after CharacterStudio loaded it. Refresh before copying.");
        }
        if (!package.CommonCharacterActionLibrary.TryGetAction(request.sourceActionId, out JsonElement sourceAction))
            throw new ArgumentException("Common action was not found: " + request.sourceActionId);

        CombinedWeaponConfigurationAuthoringSummary publicConfiguration =
            GlobalWeaponConfigurationResolver.FindCombinedWeapon(
                package.GlobalWeaponLibrary,
                request.combinedWeaponConfigurationId)
            ?? throw new ArgumentException("Combined weapon configuration was not found: " + request.combinedWeaponConfigurationId);

        string packageSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(
            package.Manifest?.PackageId ?? string.Empty);
        string characterActionPath = Path.Combine(packagePath, "config", "character_actions.json");
        CharacterActionIdCatalogLease catalogLease = AllocateCharacterActionId(
            rootPath,
            packageSegment,
            ToProjectRelativePath(rootPath, characterActionPath));
        int actionId = catalogLease.ActionId;
        JsonObject copiedNode = JsonNode.Parse(sourceAction.GetRawText()) as JsonObject
            ?? throw new InvalidDataException("Common action JSON was empty.");
        copiedNode.Remove("stableId");
        copiedNode.Remove("id");
        copiedNode["actionId"] = actionId;
        string displayName = copiedNode["displayName"]?.GetValue<string>() ?? ("Action #" + request.sourceActionId);
        copiedNode["displayName"] = displayName + "（角色专属）";
        JsonElement copiedAction = JsonSerializer.Deserialize<JsonElement>(copiedNode.ToJsonString(), jsonOptions);

        CharacterCombinedWeaponConfigurationBinding binding = FindOrCreateCombinedWeaponBinding(
            package.ApplicationConfig,
            request.combinedWeaponConfigurationId);
        binding.Overrides ??= new CharacterCombinedWeaponConfigurationOverrides();
        CombinedWeaponActionConfiguration actionConfiguration = binding.Overrides.ActionConfiguration
            ?? CloneActionConfiguration(publicConfiguration.ActionConfiguration, jsonOptions);
        actionConfiguration.Actions ??= new List<CombinedWeaponActionEntry>();
        CombinedWeaponActionEntry actionEntry = actionConfiguration.Actions.Find(item =>
            item != null && string.Equals(item.ActionRole, request.actionRole, StringComparison.Ordinal));
        if (actionEntry == null)
        {
            actionEntry = new CombinedWeaponActionEntry { ActionRole = request.actionRole };
            actionConfiguration.Actions.Add(actionEntry);
        }
        actionEntry.ActionId = actionId;
        binding.Overrides.ActionConfiguration = actionConfiguration;
        package.CharacterActionLibrary.Actions.Add(copiedAction);

        string applicationPath = Path.Combine(packagePath, "config", "character_application.json");
        string previousActions = File.Exists(characterActionPath) ? File.ReadAllText(characterActionPath) : null;
        string previousApplication = File.Exists(applicationPath) ? File.ReadAllText(applicationPath) : null;
        try
        {
            WriteJsonFileAtomic(characterActionPath, package.CharacterActionLibrary, jsonOptions);
            beforeApplicationWrite?.Invoke();
            WriteJsonFileAtomic(applicationPath, package.ApplicationConfig, jsonOptions);
        }
        catch
        {
            RestoreCharacterActionCopyFile(characterActionPath, previousActions);
            RestoreCharacterActionCopyFile(applicationPath, previousApplication);
            catalogLease.Rollback();
            throw;
        }

        return new
        {
            success = true,
            package = packageRelative,
            actionId,
            actionRole = request.actionRole,
            combinedWeaponConfigurationId = request.combinedWeaponConfigurationId,
            revision = ComputeCharacterPackageRevision(packagePath),
            sourcePath = ToProjectRelativePath(rootPath, characterActionPath),
            message = "Common action was copied to the character library and the selected action slot was updated.",
        };
    }

    private static CharacterCombinedWeaponConfigurationBinding FindOrCreateCombinedWeaponBinding(
        CharacterApplicationAuthoringSummary application,
        string configurationId)
    {
        application.CombinedWeaponConfigurationBindings ??= new List<CharacterCombinedWeaponConfigurationBinding>();
        CharacterCombinedWeaponConfigurationBinding binding = application.CombinedWeaponConfigurationBindings.Find(item =>
            item != null && string.Equals(item.CombinedWeaponConfigurationId, configurationId, StringComparison.Ordinal));
        if (binding != null)
            return binding;
        binding = new CharacterCombinedWeaponConfigurationBinding { CombinedWeaponConfigurationId = configurationId };
        application.CombinedWeaponConfigurationBindings.Add(binding);
        return binding;
    }

    private static CombinedWeaponActionConfiguration CloneActionConfiguration(
        CombinedWeaponActionConfiguration source,
        JsonSerializerOptions jsonOptions)
    {
        if (source == null)
            return new CombinedWeaponActionConfiguration();
        return JsonSerializer.Deserialize<CombinedWeaponActionConfiguration>(
            JsonSerializer.Serialize(source, jsonOptions),
            jsonOptions) ?? new CombinedWeaponActionConfiguration();
    }

    private static void RestoreCharacterActionCopyFile(string path, string contents)
    {
        if (contents == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, contents);
    }

    private static JsonElement FindFirstTrackEvent(JsonElement action, string trackName)
    {
        if (action.ValueKind == JsonValueKind.Object
            && action.TryGetProperty(trackName, out JsonElement track)
            && track.ValueKind == JsonValueKind.Object
            && track.TryGetProperty("events", out JsonElement events)
            && events.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in events.EnumerateArray())
                return item;
        }
        return default;
    }

    private static int CountHitTrackEvents(JsonElement action)
    {
        if (action.ValueKind != JsonValueKind.Object
            || !action.TryGetProperty("combatTrack", out JsonElement track)
            || track.ValueKind != JsonValueKind.Object
            || !track.TryGetProperty("events", out JsonElement events)
            || events.ValueKind != JsonValueKind.Array)
            return 0;
        int count = 0;
        foreach (JsonElement item in events.EnumerateArray())
        {
            string kind = ReadString(item, "kind");
            if (string.Equals(kind, "HitTraceWindow", StringComparison.Ordinal)
                || string.Equals(kind, "HitVolumeWindow", StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    private static string ReadString(JsonElement value, string propertyName)
    {
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }

    private static int ReadInt32(JsonElement value, string propertyName)
    {
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(propertyName, out JsonElement property)
            && property.TryGetInt32(out int result)
                ? result
                : 0;
    }

    private static bool ContainsString(JsonElement value, string propertyName, string expected)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(propertyName, out JsonElement items)
            || items.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement item in items.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String
                && string.Equals(item.GetString(), expected, StringComparison.Ordinal))
                return true;
        return false;
    }

    internal sealed class CharacterActionCopyRequest
    {
        public string package { get; set; } = string.Empty;
        public string combinedWeaponConfigurationId { get; set; } = string.Empty;
        public string actionRole { get; set; } = string.Empty;
        public int sourceActionId { get; set; }
        public string sourceHash { get; set; } = string.Empty;
        public string expectedRevision { get; set; } = string.Empty;
    }

    internal sealed class CharacterActionSourceConflictException : InvalidOperationException
    {
        public const string ConflictCode = "CHARACTER_ACTION_SOURCE_CONFLICT";

        public CharacterActionSourceConflictException(string expectedHash, string currentHash, string message)
            : base(message)
        {
            ExpectedHash = expectedHash ?? string.Empty;
            CurrentHash = currentHash ?? string.Empty;
        }

        public string ExpectedHash { get; }
        public string CurrentHash { get; }
    }
}
