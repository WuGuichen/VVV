using System.Globalization;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterActionHandoffSuggestedFix =
        "Open CharacterStudio > Reaction Profile and choose Save/Regenerate Reaction Profile.";

    internal static CharacterActionHandoffConsistencyReport ReadCharacterActionHandoffConsistency(
        string rootPath,
        string packageId)
    {
        var report = new CharacterActionHandoffConsistencyReport();
        if (string.IsNullOrWhiteSpace(rootPath))
            return report;

        string packageSegment = NormalizeCharacterActionSegment(packageId);
        string expectedProfileId = "reaction." + packageSegment + ".default";
        string actionSetDirectory = Path.Combine(
            rootPath,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterActions",
            "ActionSets",
            packageSegment);
        string packageLibraryDirectory = Path.Combine(
            rootPath,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterActions",
            "Libraries",
            packageSegment);
        string commonLibraryDirectory = Path.Combine(
            rootPath,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterActions",
            "Libraries",
            "Common");
        string actionSetsPath = Path.Combine(actionSetDirectory, "character_action_sets.json");
        string reactionProfilesPath = Path.Combine(actionSetDirectory, "character_reaction_profiles.json");
        string packageActionsPath = Path.Combine(packageLibraryDirectory, "character_actions.json");
        string commonActionsPath = Path.Combine(commonLibraryDirectory, "character_actions.json");
        string actionsPath = File.Exists(packageActionsPath) ? packageActionsPath : commonActionsPath;
        string actionSetsSource = ToProjectRelativePath(rootPath, actionSetsPath);
        string reactionProfilesSource = ToProjectRelativePath(rootPath, reactionProfilesPath);
        string actionsSource = ToProjectRelativePath(rootPath, actionsPath);
        AddSourcePath(report, actionSetsSource);
        AddSourcePath(report, reactionProfilesSource);
        AddSourcePath(report, actionsSource);

        if (!File.Exists(actionSetsPath))
            return report;

        List<CharacterActionSetReactionBindingRecord> bindings = ReadActionSetReactionBindings(
            report,
            actionSetsPath,
            actionSetsSource);
        report.ActionSetReactionBindingCount = bindings.Count;
        if (bindings.Count == 0)
            return report;

        Dictionary<string, CharacterActionReactionProfileRecord> profiles =
            ReadReactionProfileRecords(report, reactionProfilesPath, reactionProfilesSource);
        report.ReactionProfileCount = profiles.Count;

        HashSet<int> actionIds = ReadCharacterActionIds(report, actionsPath, actionsSource);
        if (File.Exists(commonActionsPath)
            && !string.Equals(actionsPath, commonActionsPath, StringComparison.OrdinalIgnoreCase))
        {
            string commonActionsSource = ToProjectRelativePath(rootPath, commonActionsPath);
            AddSourcePath(report, commonActionsSource);
            actionIds.UnionWith(ReadCharacterActionIds(report, commonActionsPath, commonActionsSource));
        }
        for (int i = 0; i < bindings.Count; i++)
        {
            CharacterActionSetReactionBindingRecord binding = bindings[i];
            if (string.Equals(binding.ReactionProfileId, "reaction.default", StringComparison.Ordinal))
            {
                AddIssue(
                    report,
                    "Error",
                    "CHARACTER_ACTION_REACTION_PROFILE_LEGACY_ID",
                    "ActionSet '" + binding.ActionSetStableId + "' still references legacy reaction.default; expected '" + expectedProfileId + "'.",
                    binding.SourcePath,
                    binding.SourceObjectPath,
                    "reactionProfileId");
            }

            if (!File.Exists(reactionProfilesPath))
            {
                AddIssue(
                    report,
                    "Error",
                    "CHARACTER_ACTION_REACTION_PROFILES_FILE_MISSING",
                    "ActionSet '" + binding.ActionSetStableId + "' references reaction profile '" + binding.ReactionProfileId + "', but " + reactionProfilesSource + " is missing.",
                    reactionProfilesSource,
                    string.Empty,
                    "character_reaction_profiles.json");
                continue;
            }

            if (!profiles.TryGetValue(binding.ReactionProfileId, out CharacterActionReactionProfileRecord profile))
            {
                AddIssue(
                    report,
                    "Error",
                    "CHARACTER_ACTION_REACTION_PROFILE_MISSING",
                    "ActionSet '" + binding.ActionSetStableId + "' references reaction profile '" + binding.ReactionProfileId + "', but that stableId is not present in " + reactionProfilesSource + ".",
                    binding.SourcePath,
                    binding.SourceObjectPath,
                    "reactionProfileId");
                continue;
            }

            ValidateReactionActionReference(report, actionsPath, actionsSource, actionIds, profile.DefaultActionId, reactionProfilesSource, profile.DefaultActionSourceObjectPath, "defaultActionId");
            for (int actionIndex = 0; actionIndex < profile.RuleActionIds.Count; actionIndex++)
            {
                ValidateReactionActionReference(
                    report,
                    actionsPath,
                    actionsSource,
                    actionIds,
                    profile.RuleActionIds[actionIndex],
                    reactionProfilesSource,
                    profile.RuleActionSourceObjectPaths[actionIndex],
                    "actionId");
            }
        }

        return report;
    }

    private static List<CharacterActionSetReactionBindingRecord> ReadActionSetReactionBindings(
        CharacterActionHandoffConsistencyReport report,
        string path,
        string sourcePath)
    {
        var bindings = new List<CharacterActionSetReactionBindingRecord>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("actionSets", out JsonElement actionSets) || actionSets.ValueKind != JsonValueKind.Array)
                return bindings;

            int index = 0;
            foreach (JsonElement actionSet in actionSets.EnumerateArray())
            {
                string stableId = GetJsonString(actionSet, "stableId");
                string profileId = GetJsonString(actionSet, "reactionProfileId");
                if (!string.IsNullOrWhiteSpace(profileId))
                {
                    bindings.Add(new CharacterActionSetReactionBindingRecord
                    {
                        ActionSetStableId = string.IsNullOrWhiteSpace(stableId)
                            ? index.ToString(CultureInfo.InvariantCulture)
                            : stableId,
                        ReactionProfileId = profileId,
                        SourcePath = sourcePath,
                        SourceObjectPath = "actionSets[" + (string.IsNullOrWhiteSpace(stableId) ? index.ToString(CultureInfo.InvariantCulture) : stableId) + "].reactionProfileId"
                    });
                }

                index++;
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException)
        {
            AddIssue(
                report,
                "Error",
                "CHARACTER_ACTION_ACTION_SETS_JSON_INVALID",
                "Character ActionSet JSON could not be read for reaction handoff validation: " + ex.Message,
                sourcePath,
                string.Empty,
                "character_action_sets.json");
        }

        return bindings;
    }

    private static Dictionary<string, CharacterActionReactionProfileRecord> ReadReactionProfileRecords(
        CharacterActionHandoffConsistencyReport report,
        string path,
        string sourcePath)
    {
        var profiles = new Dictionary<string, CharacterActionReactionProfileRecord>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return profiles;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("reactionProfiles", out JsonElement reactionProfiles) || reactionProfiles.ValueKind != JsonValueKind.Array)
                return profiles;

            foreach (JsonElement profileElement in reactionProfiles.EnumerateArray())
            {
                string stableId = GetJsonString(profileElement, "stableId");
                if (string.IsNullOrWhiteSpace(stableId))
                    continue;

                var profile = new CharacterActionReactionProfileRecord
                {
                    StableId = stableId,
                    DefaultActionId = GetJsonInt(profileElement, "defaultActionId"),
                    DefaultActionSourceObjectPath = "reactionProfiles[" + stableId + "].defaultActionId"
                };
                if (profileElement.TryGetProperty("rules", out JsonElement rules) && rules.ValueKind == JsonValueKind.Array)
                {
                    int ruleIndex = 0;
                    foreach (JsonElement rule in rules.EnumerateArray())
                    {
                        int actionId = GetJsonInt(rule, "actionId");
                        if (actionId > 0)
                        {
                            profile.RuleActionIds.Add(actionId);
                            profile.RuleActionSourceObjectPaths.Add("reactionProfiles[" + stableId + "].rules[" + ruleIndex.ToString(CultureInfo.InvariantCulture) + "].actionId");
                        }

                        ruleIndex++;
                    }
                }

                profiles[stableId] = profile;
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException)
        {
            AddIssue(
                report,
                "Error",
                "CHARACTER_ACTION_REACTION_PROFILES_JSON_INVALID",
                "Character reaction profile JSON could not be read: " + ex.Message,
                sourcePath,
                string.Empty,
                "character_reaction_profiles.json");
        }

        return profiles;
    }

    private static HashSet<int> ReadCharacterActionIds(
        CharacterActionHandoffConsistencyReport report,
        string path,
        string sourcePath)
    {
        var actionIds = new HashSet<int>();
        if (!File.Exists(path))
            return actionIds;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("actions", out JsonElement actions) || actions.ValueKind != JsonValueKind.Array)
                return actionIds;

            foreach (JsonElement action in actions.EnumerateArray())
            {
                int actionId = GetJsonInt(action, "actionId");
                if (actionId > 0)
                    actionIds.Add(actionId);
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException)
        {
            AddIssue(
                report,
                "Error",
                "CHARACTER_ACTION_LIBRARY_JSON_INVALID",
                "Character action library JSON could not be read for reaction handoff validation: " + ex.Message,
                sourcePath,
                string.Empty,
                "character_actions.json");
        }

        return actionIds;
    }

    private static void ValidateReactionActionReference(
        CharacterActionHandoffConsistencyReport report,
        string actionsPath,
        string actionsSource,
        HashSet<int> actionIds,
        int actionId,
        string sourcePath,
        string sourceObjectPath,
        string field)
    {
        if (actionId <= 0)
            return;

        report.ReactionActionReferenceCount++;
        if (!File.Exists(actionsPath))
        {
            AddIssue(
                report,
                "Error",
                "CHARACTER_ACTION_LIBRARY_MISSING",
                "Reaction action '" + actionId + "' is referenced, but " + actionsSource + " is missing.",
                actionsSource,
                string.Empty,
                "character_actions.json");
            return;
        }

        if (!actionIds.Contains(actionId))
        {
            AddIssue(
                report,
                "Error",
                "CHARACTER_ACTION_REACTION_ACTION_MISSING",
                "Reaction profile references action '" + actionId + "', but that action is not present in " + actionsSource + ".",
                sourcePath,
                sourceObjectPath,
                field);
        }
    }

    private static void AddIssue(
        CharacterActionHandoffConsistencyReport report,
        string severity,
        string code,
        string message,
        string sourcePath,
        string sourceObjectPath,
        string field)
    {
        report.Issues.Add(new CharacterActionHandoffConsistencyIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            SourcePath = sourcePath,
            SourceObjectPath = sourceObjectPath,
            Field = field,
            SuggestedFix = CharacterActionHandoffSuggestedFix
        });
    }

    private static void AddSourcePath(CharacterActionHandoffConsistencyReport report, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath) && !report.SourcePaths.Contains(sourcePath))
            report.SourcePaths.Add(sourcePath);
    }

    private sealed class CharacterActionSetReactionBindingRecord
    {
        public string ActionSetStableId { get; set; } = string.Empty;
        public string ReactionProfileId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
    }

    private sealed class CharacterActionReactionProfileRecord
    {
        public string StableId { get; set; } = string.Empty;
        public int DefaultActionId { get; set; }
        public string DefaultActionSourceObjectPath { get; set; } = string.Empty;
        public List<int> RuleActionIds { get; } = new List<int>();
        public List<string> RuleActionSourceObjectPaths { get; } = new List<string>();
    }
}

internal sealed class CharacterActionHandoffConsistencyReport
{
    public List<string> SourcePaths { get; } = new List<string>();
    public List<CharacterActionHandoffConsistencyIssue> Issues { get; } = new List<CharacterActionHandoffConsistencyIssue>();
    public int ActionSetReactionBindingCount { get; set; }
    public int ReactionProfileCount { get; set; }
    public int ReactionActionReferenceCount { get; set; }
    public bool HasBlockingIssues => Issues.Exists(issue => !string.IsNullOrWhiteSpace(issue.Severity) && issue.Severity.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0);
}

internal sealed class CharacterActionHandoffConsistencyIssue
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceObjectPath { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}
