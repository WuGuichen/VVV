using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public sealed class CharacterActionAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public const string GeneratedRootRelativePath = "Assets/MxFrameworkGenerated/CharacterActions";
        public const string UsageProfile = "characterActionProfile";
        public const string UsageActionSet = "characterActionSet";
        public const string UsageReactionProfile = "characterReactionProfile";
        public const string UsageAction = "characterAction";
        public const string UsageActionLibrary = "characterActionLibrary";

        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.CharacterAction; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            string rootPath = ResolveGeneratedRoot(context);
            bool available = !string.IsNullOrWhiteSpace(rootPath) && Directory.Exists(rootPath);
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Character Action Config",
                SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available
                    ? string.Empty
                    : "Generated CharacterAction source folder was not found."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : BuildScopeId(context)
            };
            AddIfPresent(collection.Metadata, "packageId", context != null ? context.PackageId : string.Empty);
            AddIfPresent(collection.Metadata, "projectRootPath", context != null ? context.ProjectRootPath : string.Empty);
            collection.Providers.Add(Describe(context));

            string rootPath = ResolveGeneratedRoot(context);
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = ProviderId,
                    Message = "Generated CharacterAction source folder was not found.",
                    SuggestedFix = "Run Unity import or CharacterAction generation before browsing generated action config."
                });
                return collection;
            }

            string[] files = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
                AddFileItems(collection, context, rootPath, files[i]);

            return collection;
        }

        private static void AddFileItems(
            AuthoringResourceCollection collection,
            AuthoringResourceProviderContext context,
            string rootPath,
            string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(filePath));
            }
            catch (JsonException ex)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.UnsupportedFormat,
                    ProviderId = AuthoringResourceProviderIds.CharacterAction,
                    SourceField = ToProjectRelativePath(context, filePath),
                    Message = "CharacterAction JSON could not be parsed: " + ex.Message,
                    SuggestedFix = "Fix the generated CharacterAction JSON or regenerate it from Unity import."
                });
                return;
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (string.Equals(fileName, "character_action_profile.json", StringComparison.OrdinalIgnoreCase))
                {
                    AddProfiles(collection, context, filePath, root);
                    return;
                }

                if (string.Equals(fileName, "character_action_sets.json", StringComparison.OrdinalIgnoreCase))
                {
                    AddActionSets(collection, context, filePath, root);
                    return;
                }

                if (string.Equals(fileName, "character_reaction_profiles.json", StringComparison.OrdinalIgnoreCase))
                {
                    AddReactionProfiles(collection, context, filePath, root);
                    return;
                }

                if (string.Equals(fileName, "character_actions.json", StringComparison.OrdinalIgnoreCase))
                    AddActionLibrary(collection, context, rootPath, filePath, root);
            }
        }

        private static void AddProfiles(
            AuthoringResourceCollection collection,
            AuthoringResourceProviderContext context,
            string filePath,
            JsonElement root)
        {
            if (!TryGetArray(root, "profiles", out JsonElement profiles))
                return;

            int index = 0;
            foreach (JsonElement profile in profiles.EnumerateArray())
            {
                string stableId = FirstNonEmpty(ReadString(profile, "stableId"), "profile." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var metadata = BuildBaseMetadata(context, filePath, stableId, ResolvePackageIdFromPath(filePath));
                AddIfPresent(metadata, "actionSetIds", JoinStringArray(profile, "actionSetIds"));
                AddIfPresent(metadata, "actionLibraryIds", JoinStringArray(profile, "actionLibraryIds"));
                collection.Items.Add(CreateConfigItem(
                    stableId,
                    FirstNonEmpty(ReadString(profile, "displayName"), stableId),
                    UsageProfile,
                    filePath,
                    metadata));
                index++;
            }
        }

        private static void AddActionSets(
            AuthoringResourceCollection collection,
            AuthoringResourceProviderContext context,
            string filePath,
            JsonElement root)
        {
            if (!TryGetArray(root, "actionSets", out JsonElement actionSets))
                return;

            int index = 0;
            foreach (JsonElement actionSet in actionSets.EnumerateArray())
            {
                string stableId = FirstNonEmpty(ReadString(actionSet, "stableId"), "actionSet." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var metadata = BuildBaseMetadata(context, filePath, stableId, ResolvePackageIdFromPath(filePath));
                AddIfPresent(metadata, "movementProfileId", ReadString(actionSet, "movementProfileId"));
                AddIfPresent(metadata, "reactionProfileId", ReadString(actionSet, "reactionProfileId"));
                AddIfPresent(metadata, "defaultActionId", ReadString(actionSet, "defaultActionId"));
                AddIfPresent(metadata, "characterStableId", ReadString(actionSet, "characterStableId"));
                collection.Items.Add(CreateConfigItem(
                    stableId,
                    FirstNonEmpty(ReadString(actionSet, "displayName"), stableId),
                    UsageActionSet,
                    filePath,
                    metadata));
                index++;
            }
        }

        private static void AddReactionProfiles(
            AuthoringResourceCollection collection,
            AuthoringResourceProviderContext context,
            string filePath,
            JsonElement root)
        {
            if (!TryGetArray(root, "reactionProfiles", out JsonElement profiles))
                return;

            int index = 0;
            foreach (JsonElement profile in profiles.EnumerateArray())
            {
                string stableId = FirstNonEmpty(ReadString(profile, "stableId"), "reactionProfile." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var metadata = BuildBaseMetadata(context, filePath, stableId, ResolvePackageIdFromPath(filePath));
                AddIfPresent(metadata, "defaultActionId", ReadString(profile, "defaultActionId"));
                AddIfPresent(metadata, "ruleActionIds", JoinRuleTargets(profile));
                AddIfPresent(metadata, "ruleTriggers", JoinRuleTriggers(profile));
                collection.Items.Add(CreateConfigItem(
                    stableId,
                    FirstNonEmpty(ReadString(profile, "displayName"), stableId),
                    UsageReactionProfile,
                    filePath,
                    metadata));
                index++;
            }
        }

        private static void AddActionLibrary(
            AuthoringResourceCollection collection,
            AuthoringResourceProviderContext context,
            string rootPath,
            string filePath,
            JsonElement root)
        {
            if (!TryGetArray(root, "actions", out JsonElement actions))
                return;

            string libraryId = ResolveLibraryId(rootPath, filePath);
            var libraryMetadata = BuildBaseMetadata(context, filePath, libraryId, ResolvePackageIdFromPath(filePath));
            libraryMetadata["libraryId"] = libraryId;
            libraryMetadata["actionCount"] = actions.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture);
            collection.Items.Add(CreateConfigItem(
                "characterActionLibrary." + NormalizeStableSegment(libraryId),
                libraryId,
                UsageActionLibrary,
                filePath,
                libraryMetadata));

            int index = 0;
            foreach (JsonElement action in actions.EnumerateArray())
            {
                string stableId = FirstNonEmpty(ReadString(action, "stableId"), "action." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var metadata = BuildBaseMetadata(context, filePath, stableId, ResolvePackageIdFromPath(filePath));
                metadata["libraryId"] = libraryId;
                AddIfPresent(metadata, "category", ReadString(action, "category"));
                AddIfPresent(metadata, "durationFrames", ReadInt(action, "durationFrames").ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddIfPresent(metadata, "animationActionKeys", JoinAnimationEventField(action, "animationActionKey"));
                string clipKeys = JoinAnimationEventField(action, "animationClipResourceKey");
                AddIfPresent(metadata, "animationClipResourceKeys", clipKeys);
                AddIfPresent(metadata, "animationClipResourceKey", FirstCsvValue(clipKeys));
                collection.Items.Add(CreateConfigItem(
                    stableId,
                    FirstNonEmpty(ReadString(action, "displayName"), stableId),
                    UsageAction,
                    filePath,
                    metadata));
                index++;
            }
        }

        private static AuthoringResourceItem CreateConfigItem(
            string stableId,
            string displayName,
            string usage,
            string filePath,
            Dictionary<string, string> metadata)
        {
            string providerResourceKey = stableId;
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceNaming.BuildNormalizedProviderResourceId(
                    AuthoringResourceProviderIds.CharacterAction,
                    stableId,
                    providerResourceKey),
                StableId = stableId,
                DisplayName = displayName,
                Kind = CharacterPackageResourceTypeIds.Config,
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.CharacterAction,
                SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
                BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
            item.Tags.Add("character-action");
            item.Tags.Add(usage);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.CharacterAction,
                BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.Address,
                DisplayValue = providerResourceKey,
                IsPrimary = true,
                ProviderResourceKey = providerResourceKey,
                ExternalSourcePath = filePath,
                AssetType = CharacterPackageResourceTypeIds.Config,
                ProviderData = new Dictionary<string, string>(item.Metadata)
            });
            return item;
        }

        private static Dictionary<string, string> BuildBaseMetadata(
            AuthoringResourceProviderContext context,
            string filePath,
            string stableId,
            string packageId)
        {
            var metadata = new Dictionary<string, string>();
            metadata["sourcePath"] = ToProjectRelativePath(context, filePath);
            metadata["stableId"] = stableId ?? string.Empty;
            AddIfPresent(metadata, "packageId", FirstNonEmpty(packageId, context != null ? context.PackageId : string.Empty));
            return metadata;
        }

        private static string ResolveGeneratedRoot(AuthoringResourceProviderContext context)
        {
            string projectRoot = context != null ? context.ProjectRootPath : string.Empty;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return string.Empty;
            return Path.Combine(projectRoot, GeneratedRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string BuildScopeId(AuthoringResourceProviderContext context)
        {
            string packageId = context != null ? context.PackageId : string.Empty;
            return string.IsNullOrWhiteSpace(packageId)
                ? "characterAction.generated"
                : "characterAction." + packageId.Trim();
        }

        private static string ResolvePackageIdFromPath(string filePath)
        {
            string normalized = NormalizePath(filePath);
            string marker = "/ActionSets/";
            int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                marker = "/Profiles/";
                index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            }
            if (index < 0)
                return string.Empty;

            string tail = normalized.Substring(index + marker.Length);
            int slash = tail.IndexOf('/');
            return slash > 0 ? tail.Substring(0, slash) : string.Empty;
        }

        private static string ResolveLibraryId(string rootPath, string filePath)
        {
            string normalizedRoot = NormalizePath(rootPath).TrimEnd('/');
            string normalizedFile = NormalizePath(filePath);
            string relative = normalizedFile.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalizedFile.Substring(normalizedRoot.Length + 1)
                : normalizedFile;
            string[] parts = relative.Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "Libraries", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    return parts[i + 1];
            }

            return "Common";
        }

        private static string ToProjectRelativePath(AuthoringResourceProviderContext context, string filePath)
        {
            string projectRoot = context != null ? context.ProjectRootPath : string.Empty;
            string normalizedFile = NormalizePath(filePath);
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                string normalizedRoot = NormalizePath(projectRoot).TrimEnd('/');
                if (normalizedFile.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return normalizedFile.Substring(normalizedRoot.Length + 1);
            }

            return normalizedFile;
        }

        private static string JoinRuleTargets(JsonElement profile)
        {
            if (!TryGetArray(profile, "rules", out JsonElement rules))
                return string.Empty;

            var values = new List<string>();
            foreach (JsonElement rule in rules.EnumerateArray())
                AddDistinct(values, ReadString(rule, "actionId"));
            return string.Join(",", values);
        }

        private static string JoinRuleTriggers(JsonElement profile)
        {
            if (!TryGetArray(profile, "rules", out JsonElement rules))
                return string.Empty;

            var values = new List<string>();
            foreach (JsonElement rule in rules.EnumerateArray())
                AddDistinct(values, ReadString(rule, "trigger"));
            return string.Join(",", values);
        }

        private static string JoinStringArray(JsonElement element, string propertyName)
        {
            if (!TryGetArray(element, propertyName, out JsonElement values))
                return string.Empty;

            var result = new List<string>();
            foreach (JsonElement value in values.EnumerateArray())
                AddDistinct(result, value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString());
            return string.Join(",", result);
        }

        private static string JoinAnimationEventField(JsonElement action, string propertyName)
        {
            if (!TryGetObject(action, "animationTrack", out JsonElement track) ||
                !TryGetArray(track, "events", out JsonElement events))
                return string.Empty;

            var values = new List<string>();
            foreach (JsonElement evt in events.EnumerateArray())
                AddDistinct(values, ReadString(evt, propertyName));
            return string.Join(",", values);
        }

        private static void AddDistinct(List<string> values, string value)
        {
            value = value != null ? value.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(value) || values.Contains(value))
                return;
            values.Add(value);
        }

        private static string FirstCsvValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            int comma = value.IndexOf(',');
            return comma >= 0 ? value.Substring(0, comma) : value;
        }

        private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement value)
        {
            if (TryGetObject(element, propertyName, out value) && value.ValueKind == JsonValueKind.Array)
                return true;
            value = default;
            return false;
        }

        private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
                return true;
            value = default;
            return false;
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (!TryGetObject(element, propertyName, out JsonElement value))
                return string.Empty;
            if (value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
            if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                return value.ToString();
            return string.Empty;
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (!TryGetObject(element, propertyName, out JsonElement value))
                return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result))
                return result;
            return 0;
        }

        private static void AddIfPresent(Dictionary<string, string> metadata, string key, string value)
        {
            value = value != null ? value.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                metadata[key] = value;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }
            return string.Empty;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string NormalizeStableSegment(string value)
        {
            value = value != null ? value.Trim().ToLowerInvariant() : string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return "default";

            var chars = new char[value.Length];
            int count = 0;
            bool previousDot = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool allowed = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-';
                if (allowed)
                {
                    chars[count++] = ch;
                    previousDot = false;
                    continue;
                }

                if (!previousDot)
                {
                    chars[count++] = '.';
                    previousDot = true;
                }
            }

            string normalized = new string(chars, 0, count).Trim('.');
            return string.IsNullOrWhiteSpace(normalized) ? "default" : normalized;
        }
    }
}
