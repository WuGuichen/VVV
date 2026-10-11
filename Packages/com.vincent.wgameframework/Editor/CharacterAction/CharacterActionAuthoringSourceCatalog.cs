using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    public sealed class CharacterActionAuthoringSourceEntry
    {
        public CharacterActionAuthoringSourceEntry(string directory, string displayName)
        {
            DirectoryPath = NormalizeAssetPath(directory);
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? DirectoryPath
                : displayName.Trim();
            ProfilePath = CombineAssetPath(DirectoryPath, CharacterActionJsonImporter.DefaultProfilesSourcePath);
            ActionSetsPath = CombineAssetPath(DirectoryPath, CharacterActionJsonImporter.DefaultActionSetsSourcePath);
            ReactionProfilesPath = CombineAssetPath(DirectoryPath, CharacterActionJsonImporter.DefaultReactionProfilesSourcePath);
            LandingProfilesPath = CombineAssetPath(DirectoryPath, CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);
            ActionsPath = CombineAssetPath(DirectoryPath, CharacterActionJsonImporter.DefaultActionsSourcePath);
        }

        public string DirectoryPath { get; }
        public string DisplayName { get; }
        public string ProfilePath { get; }
        public string ActionSetsPath { get; }
        public string ReactionProfilesPath { get; }
        public string LandingProfilesPath { get; }
        public string ActionsPath { get; }
        public string ChoiceLabel => DisplayName + "  (" + DirectoryPath + ")";

        /// <summary>
        /// Flat label for PopupField/GenericMenu (no '/' submenus, escaped '_' mnemonics).
        /// </summary>
        public string FlatChoiceLabel
        {
            get
            {
                string leaf = GetLeafDirectoryName(DirectoryPath);
                string core = string.IsNullOrWhiteSpace(DisplayName) || string.Equals(DisplayName, leaf, StringComparison.Ordinal)
                    ? leaf
                    : DisplayName + " · " + leaf;
                return CharacterActionAuthoringUiLabels.ToMenuSafeLabel(core);
            }
        }

        public bool HasProfile => File.Exists(ProfilePath);
        public bool HasActionSet => File.Exists(ActionSetsPath);
        public bool HasReactionProfiles => File.Exists(ReactionProfilesPath);
        public bool HasLandingProfiles => File.Exists(LandingProfilesPath);
        public bool HasActions => File.Exists(ActionsPath);
        public bool Exists => HasProfile || HasActionSet || HasActions;

        public override string ToString()
        {
            return ChoiceLabel;
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        internal static string CombineAssetPath(string directory, string fileName)
        {
            return NormalizeAssetPath(Path.Combine(directory ?? string.Empty, fileName ?? string.Empty));
        }

        internal static string GetLeafDirectoryName(string directoryPath)
        {
            directoryPath = NormalizeAssetPath(directoryPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
                return string.Empty;

            int index = directoryPath.LastIndexOf('/');
            return index >= 0 && index < directoryPath.Length - 1
                ? directoryPath.Substring(index + 1)
                : directoryPath;
        }
    }

    internal static class CharacterActionAuthoringUiLabels
    {
        internal static string ToMenuSafeLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Replace("_", "__");
        }

        internal static string FormatActionSetBindingLabel(string stableId, CharacterActionAuthoringSourceEntry entry)
        {
            stableId = stableId ?? string.Empty;
            if (entry == null)
                return ToMenuSafeLabel(string.IsNullOrWhiteSpace(stableId) ? "（未绑定）" : stableId);

            string leaf = CharacterActionAuthoringSourceEntry.GetLeafDirectoryName(entry.DirectoryPath);
            if (string.IsNullOrWhiteSpace(stableId))
                return ToMenuSafeLabel(leaf);

            return ToMenuSafeLabel(stableId + " · " + leaf);
        }
    }

    public static class CharacterActionAuthoringSourceCatalog
    {
        private static readonly string[] SearchRoots =
        {
            "Assets/MxFrameworkGenerated/CharacterActions",
            "Assets/StreamingAssets/MxFramework/CharacterActions",
            "CharacterActionSource",
            "Authoring/CharacterPackages",
        };

        public static CharacterActionAuthoringSourceEntry[] DiscoverActionSetSources()
        {
            return SelectActionSetAuthoringSources(
                Discover(CharacterActionJsonImporter.DefaultActionSetsSourcePath),
                message => Debug.LogWarning(message));
        }

        internal static CharacterActionAuthoringSourceEntry[] SelectActionSetAuthoringSources(
            CharacterActionAuthoringSourceEntry[] entries,
            Action<string> reportProjectionDifference,
            string generatedRoot = "Assets/MxFrameworkGenerated/CharacterActions",
            string publishedRoot = "Assets/StreamingAssets/MxFramework/CharacterActions")
        {
            generatedRoot = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(generatedRoot) + "/";
            publishedRoot = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(publishedRoot) + "/";
            var generated = new Dictionary<string, CharacterActionAuthoringSourceEntry>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterActionAuthoringSourceEntry entry = entries[i];
                if (entry.DirectoryPath.StartsWith(generatedRoot, StringComparison.Ordinal) && entry.HasActionSet)
                    generated[entry.DirectoryPath.Substring(generatedRoot.Length)] = entry;
            }

            var result = new List<CharacterActionAuthoringSourceEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterActionAuthoringSourceEntry entry = entries[i];
                if (!entry.DirectoryPath.StartsWith(publishedRoot, StringComparison.Ordinal)
                    || !generated.TryGetValue(entry.DirectoryPath.Substring(publishedRoot.Length), out var authoring))
                {
                    result.Add(entry);
                    continue;
                }

                // A published bundle is a projection of this exact authoring path, not a
                // second editable document. Never collapse independent paths by stable id.
                // Stale/changed projections remain visible as diagnostics; the authoring
                // bundle remains authoritative without disabling its action preview.
                if (!EquivalentJsonFile(authoring.ActionSetsPath, entry.ActionSetsPath)
                    || !EquivalentJsonFile(authoring.ReactionProfilesPath, entry.ReactionProfilesPath)
                    || !EquivalentJsonFile(authoring.LandingProfilesPath, entry.LandingProfilesPath))
                {
                    reportProjectionDifference?.Invoke(
                        "[CharacterActionAuthoring] Published ActionSet projection differs from its authoring bundle: '"
                        + entry.DirectoryPath + "'. Preview/editing uses '" + authoring.DirectoryPath
                        + "'; the published projection was not loaded as a second document. Review/rebuild publication separately.");
                }
            }
            return result.ToArray();
        }

        private static bool EquivalentJsonFile(string leftPath, string rightPath)
        {
            bool leftExists = File.Exists(leftPath);
            bool rightExists = File.Exists(rightPath);
            if (!leftExists || !rightExists)
                return leftExists == rightExists;
            try
            {
                return Newtonsoft.Json.Linq.JToken.DeepEquals(
                    Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(leftPath)),
                    Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(rightPath)));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                return false;
            }
        }

        public static CharacterActionAuthoringSourceEntry[] DiscoverProfileSources()
        {
            return Discover(CharacterActionJsonImporter.DefaultProfilesSourcePath);
        }

        public static CharacterActionAuthoringSourceEntry[] DiscoverActionLibrarySources()
        {
            return Discover(CharacterActionJsonImporter.DefaultActionsSourcePath);
        }

        public static CharacterActionAuthoringImportResult LoadActionSet(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (!entry.HasActionSet)
                throw new FileNotFoundException("Character ActionSet source is missing character_action_sets.json.", entry.DirectoryPath);

            string reactionProfilesJson = entry.HasReactionProfiles
                ? File.ReadAllText(entry.ReactionProfilesPath)
                : CharacterActionAuthoringJsonService.CreateEmptyReactionProfilesJson();
            string landingProfilesJson = entry.HasLandingProfiles
                ? File.ReadAllText(entry.LandingProfilesPath)
                : CharacterActionAuthoringJsonService.CreateEmptyLandingProfilesJson();
            return CharacterActionAuthoringJsonService.ImportActionSetsOnly(
                File.ReadAllText(entry.ActionSetsPath),
                reactionProfilesJson,
                landingProfilesJson);
        }

        public static CharacterActionAuthoringImportResult LoadProfile(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (!entry.HasProfile)
                throw new FileNotFoundException("Character Action Profile source is missing character_action_profile.json.", entry.DirectoryPath);

            return CharacterActionAuthoringJsonService.ImportProfileOnly(File.ReadAllText(entry.ProfilePath));
        }

        public static CharacterActionAuthoringImportResult LoadActionLibrary(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (!entry.HasActions)
                throw new FileNotFoundException("Character Action library source is missing character_actions.json.", entry.DirectoryPath);

            return CharacterActionAuthoringJsonService.ImportActionsOnly(File.ReadAllText(entry.ActionsPath));
        }

        public static CharacterActionAuthoringExportResult SaveActionSet(
            string directory,
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext)
        {
            EnsureReactionProfileLoaded(directory, model);
            CharacterActionAuthoringExportResult export = CharacterActionAuthoringJsonService.Export(
                model,
                referenceContext,
                buildWorkstationSnapshots: false);
            if (export.Success)
            {
                CharacterActionAuthoringJsonService.WriteActionSetsFile(NormalizeSaveDirectory(directory), export);
                CharacterActionAuthoringJsonService.WriteReactionProfilesFile(NormalizeSaveDirectory(directory), export);
                CharacterActionAuthoringJsonService.WriteLandingProfilesFile(NormalizeSaveDirectory(directory), export);
            }
            return export;
        }

        public static void SaveProfile(string directory, CharacterActionAuthoringModel model)
        {
            CharacterActionAuthoringJsonService.WriteProfileFile(NormalizeSaveDirectory(directory), model);
        }

        public static CharacterActionAuthoringExportResult SaveActionLibrary(
            string directory,
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringReferenceContext referenceContext)
        {
            EnsureReactionProfileLoaded(null, model);
            CharacterActionAuthoringExportResult export = CharacterActionAuthoringJsonService.Export(
                model,
                referenceContext,
                buildWorkstationSnapshots: false);
            if (export.Success)
                CharacterActionAuthoringJsonService.WriteActionsFile(NormalizeSaveDirectory(directory), export);
            return export;
        }

        public static void DeleteActionSet(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                return;

            DeleteAssetOrFile(entry.ActionSetsPath);
            AssetDatabase.Refresh();
        }

        public static void DeleteProfile(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                return;

            DeleteAssetOrFile(entry.ProfilePath);
            AssetDatabase.Refresh();
        }

        public static void DeleteActionLibrary(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null)
                return;

            DeleteAssetOrFile(entry.ActionsPath);
            AssetDatabase.Refresh();
        }

        public static string ProjectRelativeDirectoryFromAbsolute(string absoluteDirectory)
        {
            if (string.IsNullOrWhiteSpace(absoluteDirectory))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            string normalized = Path.GetFullPath(absoluteDirectory).Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, projectRoot, StringComparison.Ordinal))
                return string.Empty;
            if (!normalized.StartsWith(projectRoot + "/", StringComparison.Ordinal))
                return string.Empty;

            return normalized.Substring(projectRoot.Length + 1);
        }

        public static void EnsureReactionProfileLoaded(string directory, CharacterActionAuthoringModel model)
        {
            if (model?.ActionSet == null)
                return;
            string reactionProfileId = model.ActionSet.ReactionProfileId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reactionProfileId))
                return;
            for (int i = 0; i < model.ReactionProfiles.Count; i++)
            {
                if (string.Equals(model.ReactionProfiles[i]?.StableId, reactionProfileId, StringComparison.Ordinal))
                    return;
            }

            string reactionProfilesPath = FindReactionProfilesPath(directory, model.ActionSet.StableId);
            if (string.IsNullOrWhiteSpace(reactionProfilesPath) || !File.Exists(reactionProfilesPath))
                return;

            CharacterReactionProfilesJsonDocument document =
                JsonConvert.DeserializeObject<CharacterReactionProfilesJsonDocument>(
                    File.ReadAllText(reactionProfilesPath),
                    CharacterActionAuthoringJsonService.JsonSettings);
            CharacterReactionProfileJsonDto[] profiles =
                document?.ReactionProfiles ?? Array.Empty<CharacterReactionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterReactionProfileJsonDto profile = profiles[i];
                if (profile == null)
                    continue;
                if (!ContainsReactionProfile(model, profile.StableId))
                    model.ReactionProfiles.Add(profile);
            }
            model.Normalize();
        }

        private static string FindReactionProfilesPath(string directory, string actionSetStableId)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                string path = CharacterActionAuthoringSourceEntry.CombineAssetPath(
                    directory,
                    CharacterActionJsonImporter.DefaultReactionProfilesSourcePath);
                if (File.Exists(path))
                    return path;
            }

            CharacterActionAuthoringSourceEntry[] sources = DiscoverActionSetSources();
            for (int i = 0; i < sources.Length; i++)
            {
                CharacterActionAuthoringSourceEntry source = sources[i];
                if (!string.IsNullOrWhiteSpace(actionSetStableId)
                    && string.Equals(ReadFirstActionSetStableId(source), actionSetStableId, StringComparison.Ordinal)
                    && source.HasReactionProfiles)
                {
                    return source.ReactionProfilesPath;
                }
            }

            return string.Empty;
        }

        private static bool ContainsReactionProfile(CharacterActionAuthoringModel model, string stableId)
        {
            if (model == null || string.IsNullOrWhiteSpace(stableId))
                return false;
            for (int i = 0; i < model.ReactionProfiles.Count; i++)
            {
                if (string.Equals(model.ReactionProfiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static int FindIndexByDirectory(CharacterActionAuthoringSourceEntry[] entries, string directory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            entries = entries ?? Array.Empty<CharacterActionAuthoringSourceEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].DirectoryPath, directory, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public static int FindActionSetIndexByStableId(CharacterActionAuthoringSourceEntry[] entries, string stableId)
        {
            entries = entries ?? Array.Empty<CharacterActionAuthoringSourceEntry>();
            stableId = stableId ?? string.Empty;
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(ReadFirstActionSetStableId(entries[i]), stableId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public static string GetActionSetStableId(CharacterActionAuthoringSourceEntry entry)
        {
            return ReadFirstActionSetStableId(entry);
        }

        public static int FindActionLibraryIndexById(CharacterActionAuthoringSourceEntry[] entries, string libraryId)
        {
            entries = entries ?? Array.Empty<CharacterActionAuthoringSourceEntry>();
            libraryId = libraryId ?? string.Empty;
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].DisplayName, libraryId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static CharacterActionAuthoringSourceEntry[] Discover(string fileName)
        {
            var entries = new List<CharacterActionAuthoringSourceEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < SearchRoots.Length; i++)
                AddEntriesUnder(SearchRoots[i], fileName, entries, seen);

            entries.Sort((left, right) =>
            {
                int authority = GetSourceAuthorityRank(left?.DirectoryPath)
                    .CompareTo(GetSourceAuthorityRank(right?.DirectoryPath));
                return authority != 0
                    ? authority
                    : string.Compare(left?.ChoiceLabel, right?.ChoiceLabel, StringComparison.Ordinal);
            });
            return entries.ToArray();
        }

        private static int GetSourceAuthorityRank(string directory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (directory.StartsWith("Assets/MxFrameworkGenerated/", StringComparison.Ordinal))
                return 0;
            if (directory.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))
                return 1;
            if (directory.StartsWith("CharacterActionSource/", StringComparison.Ordinal)
                || directory.StartsWith("Authoring/CharacterPackages/", StringComparison.Ordinal))
                return 3;
            return 2;
        }

        private static void AddEntriesUnder(
            string root,
            string fileName,
            List<CharacterActionAuthoringSourceEntry> entries,
            HashSet<string> seen)
        {
            root = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(root);
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(Path.GetDirectoryName(files[i]));
                if (!seen.Add(directory))
                    continue;

                entries.Add(new CharacterActionAuthoringSourceEntry(directory, CreateDisplayName(directory)));
            }
        }

        private static string CreateDisplayName(string directory)
        {
            string[] parts = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory).Split('/');
            if (parts.Length >= 2 && string.Equals(parts[parts.Length - 1], "config", StringComparison.Ordinal))
            {
                string packageDirectory = Path.GetDirectoryName(directory) ?? string.Empty;
                string manifestPath = Path.Combine(packageDirectory, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        Newtonsoft.Json.Linq.JObject manifest =
                            Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(manifestPath));
                        string packageId = manifest.Value<string>("packageId") ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(packageId))
                            return packageId;
                    }
                    catch
                    {
                    }
                }
                return parts[parts.Length - 2];
            }

            return parts.Length == 0 ? directory : parts[parts.Length - 1];
        }

        private static string ReadFirstActionSetStableId(CharacterActionAuthoringSourceEntry entry)
        {
            if (entry == null || !entry.HasActionSet)
                return string.Empty;

            try
            {
                CharacterActionSetsJsonDocument document =
                    JsonConvert.DeserializeObject<CharacterActionSetsJsonDocument>(File.ReadAllText(entry.ActionSetsPath));
                CharacterActionSetJsonDto[] actionSets = document?.ActionSets ?? Array.Empty<CharacterActionSetJsonDto>();
                return actionSets.Length == 0 ? string.Empty : actionSets[0]?.StableId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeSaveDirectory(string directory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Character action config directory is empty.", nameof(directory));
            return directory;
        }

        private static void DeleteAssetOrFile(string path)
        {
            path = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(path);
            if (!File.Exists(path))
                return;

            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            else
                File.Delete(path);
        }
    }
}
