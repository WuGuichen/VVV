using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleCharacterActionReactionProfileApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path != "/api/character-action/reaction-profile/save" || context.Request.HttpMethod != "POST")
            return false;

        CharacterActionReactionProfileSaveRequest request;
        try
        {
            request = ReadCharacterActionReactionProfileSaveRequest(context, jsonOptions);
        }
        catch (JsonException ex)
        {
            WriteJson(context.Response, new
            {
                success = false,
                error = "CHARACTER_ACTION_REACTION_PROFILE_INVALID_JSON",
                message = ex.Message
            }, jsonOptions, 400);
            return true;
        }

        CharacterActionReactionProfileSaveResult result = SaveCharacterActionReactionProfile(rootPath, request);
        WriteJson(context.Response, result, jsonOptions, result.success ? 200 : 400);
        return true;
    }

    private static CharacterActionReactionProfileSaveRequest ReadCharacterActionReactionProfileSaveRequest(
        HttpListenerContext context,
        JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new CharacterActionReactionProfileSaveRequest()
            : JsonSerializer.Deserialize<CharacterActionReactionProfileSaveRequest>(body, jsonOptions)
                ?? new CharacterActionReactionProfileSaveRequest();
    }

    internal static CharacterActionReactionProfileSaveResult SaveCharacterActionReactionProfile(
        string rootPath,
        CharacterActionReactionProfileSaveRequest request)
    {
        if (UsesCompilerOwnedReactionProjection(rootPath, request, out string schemaVersion))
        {
            return new CharacterActionReactionProfileSaveResult
            {
                success = false,
                error = "CHARACTER_ACTION_REACTION_PROFILE_COMPILER_OWNED",
                message = "Character Application schema " + schemaVersion
                    + " owns reaction configuration through Combined Weapon Configuration. "
                    + "Compile the package instead of writing generated ReactionProfile JSON directly."
            };
        }

        string packageSegment = NormalizeCharacterActionSegment(FirstCharacterActionNonEmpty(request.packageId, request.packageSegment, request.package, "character"));
        string actionSetStableId = FirstCharacterActionNonEmpty(request.actionSetStableId, packageSegment + ".default");
        string reactionProfileId = FirstCharacterActionNonEmpty(request.reactionProfileId, "reaction." + packageSegment + ".default");
        CharacterActionReactionSlotSaveRequest[] slots = request.slots ?? Array.Empty<CharacterActionReactionSlotSaveRequest>();
        if (slots.Length == 0)
        {
            return new CharacterActionReactionProfileSaveResult
            {
                success = false,
                error = "CHARACTER_ACTION_REACTION_PROFILE_EMPTY",
                message = "Reaction profile save request must include at least one slot."
            };
        }
        EnsureCharacterActionIdsAreAllocated(rootPath, slots.Select(slot => slot.actionId));

        string actionSetDirectory = Path.Combine(
            rootPath,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterActions",
            "ActionSets",
            packageSegment);
        string libraryDirectory = Path.Combine(
            rootPath,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterActions",
            "Libraries",
            "Common");
        Directory.CreateDirectory(actionSetDirectory);
        Directory.CreateDirectory(libraryDirectory);

        string actionSetsPath = Path.Combine(actionSetDirectory, "character_action_sets.json");
        string reactionProfilesPath = Path.Combine(actionSetDirectory, "character_reaction_profiles.json");
        string actionsPath = Path.Combine(libraryDirectory, "character_actions.json");

        JsonObject actionSetsDocument = ReadCharacterActionJsonObject(actionSetsPath);
        EnsureArray(actionSetsDocument, "actionSets");
        UpsertActionSetReactionProfile(actionSetsDocument, actionSetStableId, reactionProfileId, packageSegment);
        WriteCharacterActionJsonObject(actionSetsPath, actionSetsDocument);

        JsonObject reactionProfilesDocument = ReadCharacterActionJsonObject(reactionProfilesPath);
        EnsureArray(reactionProfilesDocument, "reactionProfiles");
        UpsertReactionProfile(reactionProfilesDocument, reactionProfileId, request.displayName, slots);
        WriteCharacterActionJsonObject(reactionProfilesPath, reactionProfilesDocument);

        JsonObject actionsDocument = ReadCharacterActionJsonObject(actionsPath);
        EnsureArray(actionsDocument, "actions");
        UpsertReactionActions(actionsDocument, slots);
        WriteCharacterActionJsonObject(actionsPath, actionsDocument);

        return new CharacterActionReactionProfileSaveResult
        {
            success = true,
            packageSegment = packageSegment,
            actionSetStableId = actionSetStableId,
            reactionProfileId = reactionProfileId,
            actionSetsPath = ToForwardSlash(actionSetsPath),
            reactionProfilesPath = ToForwardSlash(reactionProfilesPath),
            actionsPath = ToForwardSlash(actionsPath)
        };
    }

    private static bool UsesCompilerOwnedReactionProjection(
        string rootPath,
        CharacterActionReactionProfileSaveRequest request,
        out string schemaVersion)
    {
        schemaVersion = string.Empty;
        if (request == null || string.IsNullOrWhiteSpace(request.package))
            return false;

        try
        {
            string packagePath = ResolveSafePath(rootPath, request.package);
            string applicationPath = Path.Combine(packagePath, "config", "character_application.json");
            if (!File.Exists(applicationPath))
                return false;

            using JsonDocument application = JsonDocument.Parse(File.ReadAllText(applicationPath));
            if (!application.RootElement.TryGetProperty("schemaVersion", out JsonElement schemaElement))
                return false;

            schemaVersion = schemaElement.GetString()?.Trim() ?? string.Empty;
            int separator = schemaVersion.IndexOf('.');
            string majorText = separator >= 0 ? schemaVersion[..separator] : schemaVersion;
            return int.TryParse(majorText, out int majorVersion) && majorVersion >= 8;
        }
        catch (Exception)
        {
            schemaVersion = "unreadable";
            return true;
        }
    }

    private static JsonObject ReadCharacterActionJsonObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject
            {
                ["schemaVersion"] = string.Equals(
                    Path.GetFileName(path),
                    "character_actions.json",
                    StringComparison.OrdinalIgnoreCase) ? "0.3.0" : "0.4.0"
            };
        }

        JsonNode node = JsonNode.Parse(File.ReadAllText(path)) ?? new JsonObject();
        JsonObject obj = node as JsonObject ?? new JsonObject();
        if (string.IsNullOrWhiteSpace(obj["schemaVersion"]?.GetValue<string>()))
            obj["schemaVersion"] = string.Equals(
                Path.GetFileName(path),
                "character_actions.json",
                StringComparison.OrdinalIgnoreCase) ? "0.3.0" : "0.4.0";
        return obj;
    }

    private static void WriteCharacterActionJsonObject(string path, JsonObject document)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, document.ToJsonString(options));
    }

    private static JsonArray EnsureArray(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is JsonArray existing)
            return existing;

        var array = new JsonArray();
        obj[propertyName] = array;
        return array;
    }

    private static void UpsertActionSetReactionProfile(
        JsonObject document,
        string actionSetStableId,
        string reactionProfileId,
        string packageSegment)
    {
        JsonArray actionSets = EnsureArray(document, "actionSets");
        JsonObject actionSet = FindObjectByString(actionSets, "stableId", actionSetStableId);
        if (actionSet == null)
        {
            actionSet = new JsonObject
            {
                ["id"] = NextContainerNumericId(actionSets),
                ["stableId"] = actionSetStableId,
                ["displayName"] = packageSegment + " ActionSet",
                ["characterStableId"] = "",
                ["equipmentStateStableId"] = "",
                ["movementProfileId"] = "movement.default",
                ["defaultActionId"] = 0,
                ["defaultActionSlotMask"] = 415,
                ["commandBindings"] = new JsonArray(),
                ["abilityBindings"] = new JsonArray(),
                ["reactionBindings"] = new JsonArray()
            };
            actionSets.Add(actionSet);
        }

        actionSet["reactionProfileId"] = reactionProfileId;
    }

    private static void UpsertReactionProfile(
        JsonObject document,
        string reactionProfileId,
        string displayName,
        CharacterActionReactionSlotSaveRequest[] slots)
    {
        JsonArray profiles = EnsureArray(document, "reactionProfiles");
        JsonObject profile = FindObjectByString(profiles, "stableId", reactionProfileId);
        if (profile == null)
        {
            profile = new JsonObject
            {
                ["stableId"] = reactionProfileId
            };
            profiles.Add(profile);
        }

        profile["displayName"] = FirstCharacterActionNonEmpty(displayName, reactionProfileId + " Reaction");
        profile["defaultActionId"] = GetDefaultReactionActionId(slots);
        var rules = new JsonArray();
        for (int i = 0; i < slots.Length; i++)
        {
            CharacterActionReactionSlotSaveRequest slot = slots[i];
            int actionId = slot.actionId;
            if (actionId <= 0)
                continue;

            var rule = new JsonObject
            {
                ["actionId"] = actionId,
                ["trigger"] = FirstCharacterActionNonEmpty(slot.trigger, "Hit"),
                ["currentScatterPressureBand"] = slot.pressureBand ?? string.Empty,
                ["priority"] = slot.priority
            };
            if (!string.IsNullOrWhiteSpace(slot.reactionGroupId))
                rule["reactionGroupId"] = slot.reactionGroupId;
            if (slot.minImpactForce.HasValue)
                rule["minImpactForce"] = slot.minImpactForce.Value;
            if (slot.maxImpactForce.HasValue)
                rule["maxImpactForce"] = slot.maxImpactForce.Value;
            rules.Add(rule);
        }

        profile["rules"] = rules;
    }

    private static void UpsertReactionActions(
        JsonObject document,
        CharacterActionReactionSlotSaveRequest[] slots)
    {
        JsonArray actions = EnsureArray(document, "actions");
        for (int i = 0; i < slots.Length; i++)
        {
            CharacterActionReactionSlotSaveRequest slot = slots[i];
            int actionId = slot.actionId;
            if (actionId <= 0)
                continue;

            JsonObject action = FindObjectByInt(actions, "actionId", actionId);
            JsonObject generated = CreateReactionActionObject(slot, actionId);
            if (action == null)
            {
                actions.Add(generated);
                continue;
            }

            ReplaceJsonObject(action, generated);
        }
    }

    private static JsonObject CreateReactionActionObject(
        CharacterActionReactionSlotSaveRequest slot,
        int actionId)
    {
        int duration = slot.durationFrames <= 0 ? 1 : slot.durationFrames;
        int endFrame = Math.Max(0, duration - 1);
        string slotSegment = NormalizeCharacterActionSegment(FirstCharacterActionNonEmpty(slot.slotId, "reaction"));
        string eventPrefix = "character_action." + actionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        JsonArray motionEvents = CreateReactionMotionEvents(slot, eventPrefix, slotSegment, endFrame);
        var action = new JsonObject
        {
            ["actionId"] = actionId,
            ["displayName"] = FirstCharacterActionNonEmpty(slot.displayName, "Reaction #" + actionId),
            ["category"] = "Reaction",
            ["timelineAuthority"] = "CharacterAuthored",
            ["tags"] = new JsonArray("reaction", slotSegment, "hit_stun"),
            ["priority"] = slot.priority,
            ["durationFrames"] = duration,
            ["requirements"] = new JsonArray(),
            ["phases"] = new JsonArray(new JsonObject
            {
                ["kind"] = "Active",
                ["startFrame"] = 0,
                ["endFrame"] = endFrame
            }),
            ["cancelRules"] = new JsonArray(),
            ["interruptRules"] = new JsonArray(),
            ["cohesionSupport"] = null,
            ["motionTrack"] = new JsonObject
            {
                ["usesRootMotion"] = false,
                ["rootMotion"] = null,
                ["events"] = motionEvents
            },
            ["combatTrack"] = null,
            ["gameplayTrack"] = null,
            ["animationTrack"] = CreateReactionAnimationTrackObject(slot, eventPrefix, slotSegment, endFrame),
            ["presentationTrack"] = null,
            ["debugTrack"] = null
        };
        return action;
    }

    private static JsonArray CreateReactionMotionEvents(
        CharacterActionReactionSlotSaveRequest slot,
        string eventPrefix,
        string slotSegment,
        int endFrame)
    {
        var events = new JsonArray
        {
            new JsonObject
            {
                ["frame"] = 0,
                ["endFrame"] = endFrame,
                ["kind"] = "SetControlScale",
                ["movementMode"] = "ControlLocked",
                ["x"] = 0.0,
                ["y"] = 0.0,
                ["z"] = 0.0,
                ["movementControlScale"] = 0.0,
                ["rotationControlScale"] = 0.0,
                ["stableEventId"] = eventPrefix + ".control_lock"
            }
        };

        if (IsDeathReactionSlot(slot, slotSegment))
        {
            events.Add(new JsonObject
            {
                ["frame"] = Math.Min(24, endFrame),
                ["endFrame"] = endFrame,
                ["kind"] = "SetCollisionProfile",
                ["collisionProfileId"] = "corpse",
                ["collisionRestorePolicy"] = "None",
                ["collisionTransitionFrames"] = 0,
                ["stableEventId"] = eventPrefix + ".collision_corpse"
            });
        }
        else if (IsReviveReactionSlot(slot, slotSegment))
        {
            events.Add(new JsonObject
            {
                ["frame"] = 0,
                ["endFrame"] = endFrame,
                ["kind"] = "SetCollisionProfile",
                ["collisionProfileId"] = "revive_low",
                ["collisionRestorePolicy"] = "OnActionEnd",
                ["collisionTransitionFrames"] = 0,
                ["stableEventId"] = eventPrefix + ".collision_revive_low"
            });
        }

        return events;
    }

    private static bool IsDeathReactionSlot(CharacterActionReactionSlotSaveRequest slot, string slotSegment)
    {
        return string.Equals(slot?.slotId, "death", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotSegment, "death", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReviveReactionSlot(CharacterActionReactionSlotSaveRequest slot, string slotSegment)
    {
        return string.Equals(slot?.slotId, "revive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotSegment, "revive", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject CreateReactionAnimationTrackObject(
        CharacterActionReactionSlotSaveRequest slot,
        string eventPrefix,
        string slotSegment,
        int endFrame)
    {
        string resourceKey = FirstCharacterActionNonEmpty(slot.resourceKey, slot.resourceSelection?.runtimeResourceKey, slot.resourceSelection?.packageResourceKey);
        if (string.IsNullOrWhiteSpace(resourceKey))
            return new JsonObject { ["events"] = new JsonArray() };

        return new JsonObject
        {
            ["events"] = new JsonArray(new JsonObject
            {
                ["frame"] = 0,
                ["endFrame"] = endFrame,
                ["playbackSpeed"] = 1.0,
                ["kind"] = "CrossFadeAnimation",
                ["animationActionKey"] = "anim.reaction." + slotSegment,
                ["animationClipResourceKey"] = resourceKey,
                ["animationStartOffsetFrames"] = 0,
                ["animationEndOffsetFrames"] = 0,
                ["transitionSeconds"] = 0.04,
                ["stableEventId"] = eventPrefix + ".animation"
            })
        };
    }

    private static JsonObject FindObjectByString(JsonArray array, string propertyName, string value)
    {
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject obj
                && string.Equals(obj[propertyName]?.GetValue<string>() ?? string.Empty, value, StringComparison.Ordinal))
            {
                return obj;
            }
        }

        return null;
    }

    private static JsonObject FindObjectByInt(JsonArray array, string propertyName, int value)
    {
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject obj && ReadJsonInt(obj, propertyName, 0) == value)
                return obj;
        }
        return null;
    }

    private static int NextContainerNumericId(JsonArray array)
    {
        int next = 1;
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject obj)
                next = Math.Max(next, ReadJsonInt(obj, "id", 0) + 1);
        }
        return next;
    }

    private static int ReadJsonInt(JsonObject obj, string propertyName, int fallback)
    {
        return obj != null && obj[propertyName] is JsonValue value && value.TryGetValue<int>(out int parsed)
            ? parsed
            : fallback;
    }

    private static int GetDefaultReactionActionId(CharacterActionReactionSlotSaveRequest[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.Equals(slots[i].slotId, "cohesionBreak", StringComparison.Ordinal))
                return slots[i].actionId;
        }

        return slots.Length == 0
            ? 0
            : slots[0].actionId;
    }

    private static void ReplaceJsonObject(JsonObject target, JsonObject source)
    {
        target.Clear();
        foreach (KeyValuePair<string, JsonNode> pair in source)
            target[pair.Key] = pair.Value?.DeepClone();
    }

    private static string NormalizeCharacterActionSegment(string value)
    {
        value = value ?? string.Empty;
        var chars = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            chars[count++] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_';
        }

        string normalized = new string(chars, 0, count).Trim('_');
        while (normalized.Contains("__", StringComparison.Ordinal))
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(normalized) ? "character" : normalized;
    }

    private static string FirstCharacterActionNonEmpty(params string[] values)
    {
        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return string.Empty;
    }

    private static string ToForwardSlash(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    internal sealed class CharacterActionReactionProfileSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string packageSegment { get; set; } = string.Empty;
        public string actionSetStableId { get; set; } = string.Empty;
        public string reactionProfileId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public CharacterActionReactionSlotSaveRequest[] slots { get; set; } = Array.Empty<CharacterActionReactionSlotSaveRequest>();
    }

    internal sealed class CharacterActionReactionSlotSaveRequest
    {
        public string slotId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public string trigger { get; set; } = string.Empty;
        public string pressureBand { get; set; } = string.Empty;
        public string reactionGroupId { get; set; } = string.Empty;
        public int? minImpactForce { get; set; }
        public int? maxImpactForce { get; set; }
        public int actionId { get; set; }
        public int durationFrames { get; set; }
        public int priority { get; set; }
        public string resourceKey { get; set; } = string.Empty;
        public CharacterActionReactionResourceSelectionSaveRequest resourceSelection { get; set; }
    }

    internal sealed class CharacterActionReactionResourceSelectionSaveRequest
    {
        public string runtimeResourceKey { get; set; } = string.Empty;
        public string packageResourceKey { get; set; } = string.Empty;
        public string providerResourceKey { get; set; } = string.Empty;
    }

    internal sealed class CharacterActionReactionProfileSaveResult
    {
        public bool success { get; set; }
        public string error { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string packageSegment { get; set; } = string.Empty;
        public string actionSetStableId { get; set; } = string.Empty;
        public string reactionProfileId { get; set; } = string.Empty;
        public string actionSetsPath { get; set; } = string.Empty;
        public string reactionProfilesPath { get; set; } = string.Empty;
        public string actionsPath { get; set; } = string.Empty;
    }
}
