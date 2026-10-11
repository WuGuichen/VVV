using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestCharacterActionConfigSource
    {
        private static CharacterTestCharacterActionConfigSourcePayload ResolvePayload(
            CharacterTestCharacterActionConfigSourceOptions options)
        {
            return new ExternalJsonResolver(
                options?.CandidateDirectories,
                options != null && options.AllowEmbeddedFallback).Resolve();
        }

        private sealed class ExternalJsonResolver
        {
            private readonly string[] _directories;
            private readonly bool _allowEmbeddedFallback;
            private readonly List<CharacterActionProfileJsonDto> _profiles =
                new List<CharacterActionProfileJsonDto>();
            private readonly List<JToken> _actionSetEntries = new List<JToken>();
            private readonly HashSet<int> _usedActionSetIds = new HashSet<int>();
            private readonly List<string> _actionSetSourcePaths = new List<string>();
            private readonly List<JToken> _actionEntries = new List<JToken>();
            private readonly HashSet<int> _usedActionIds = new HashSet<int>();
            private readonly List<string> _actionSourcePaths = new List<string>();
            private readonly List<CharacterTestCharacterActionConfigSourceDiagnostic> _diagnostics =
                new List<CharacterTestCharacterActionConfigSourceDiagnostic>();

            private string _actionSetsJson = string.Empty;
            private string _actionSetsPath = string.Empty;
            private string _reactionProfilesJson = string.Empty;
            private string _reactionProfilesPath = string.Empty;
            private readonly List<JToken> _reactionProfileEntries = new List<JToken>();
            private readonly List<string> _reactionProfileSourcePaths = new List<string>();
            private string _landingProfilesJson = string.Empty;
            private string _landingProfilesPath = string.Empty;
            private readonly List<JToken> _landingProfileEntries = new List<JToken>();
            private readonly List<string> _landingProfileSourcePaths = new List<string>();
            private readonly List<string> _profilePaths = new List<string>();
            private string _invalidActionsJson = string.Empty;
            private string _invalidActionsPath = string.Empty;

            public ExternalJsonResolver(string[] directories, bool allowEmbeddedFallback)
            {
                _directories = directories ?? Array.Empty<string>();
                _allowEmbeddedFallback = allowEmbeddedFallback;
            }

            public CharacterTestCharacterActionConfigSourcePayload Resolve()
            {
                ReadProfile();
                ReadReactionProfiles();
                ReadLandingProfiles();
                ReadActionSources();

                if (HasCompleteExternalSource())
                    return CreateExternalPayload();
                if (HasPartialExternalSource())
                    return CreateIncompleteExternalPayload();

                return _allowEmbeddedFallback
                    ? CreateEmbeddedPayload()
                    : CreateMissingExternalPayload();
            }

            private void ReadLandingProfiles()
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    string directory = NormalizePath(_directories[i]);
                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    string candidatePath = NormalizePath(Path.Combine(directory, LandingProfilesFileName));
                    if (!File.Exists(candidatePath) || Contains(_landingProfileSourcePaths, candidatePath))
                        continue;

                    string json = File.ReadAllText(candidatePath);
                    if (!TryReadNamedEntries(json, "landingProfiles", _landingProfileEntries))
                    {
                        if (string.IsNullOrWhiteSpace(_landingProfilesPath))
                        {
                            _landingProfilesPath = candidatePath;
                            _landingProfilesJson = json;
                        }
                        AddLoadedDiagnostic(candidatePath, "Using invalid Character Landing Profile JSON for diagnostics.");
                        continue;
                    }

                    _landingProfileSourcePaths.Add(candidatePath);
                    _landingProfilesPath = string.Join(", ", _landingProfileSourcePaths.ToArray());
                    _landingProfilesJson = CreateMergedNamedEntriesJson(
                        "landingProfiles",
                        _landingProfileEntries);
                    AddLoadedDiagnostic(candidatePath, "Using editor-exported Character Landing Profile JSON.");
                }
            }

            private void ReadReactionProfiles()
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    string directory = NormalizePath(_directories[i]);
                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    string candidatePath = NormalizePath(Path.Combine(directory, ReactionProfilesFileName));
                    if (!File.Exists(candidatePath) || Contains(_reactionProfileSourcePaths, candidatePath))
                        continue;

                    string json = File.ReadAllText(candidatePath);
                    if (!TryReadNamedEntries(json, "reactionProfiles", _reactionProfileEntries))
                    {
                        if (string.IsNullOrWhiteSpace(_reactionProfilesPath))
                        {
                            _reactionProfilesPath = candidatePath;
                            _reactionProfilesJson = json;
                        }
                        AddLoadedDiagnostic(candidatePath, "Using invalid Character Reaction Profile JSON for diagnostics.");
                        continue;
                    }

                    _reactionProfileSourcePaths.Add(candidatePath);
                    _reactionProfilesPath = string.Join(", ", _reactionProfileSourcePaths.ToArray());
                    _reactionProfilesJson = CreateMergedNamedEntriesJson(
                        "reactionProfiles",
                        _reactionProfileEntries);
                    AddLoadedDiagnostic(candidatePath, "Using editor-exported Character Reaction Profile JSON.");
                }
            }

            private void ReadProfile()
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    string directory = NormalizePath(_directories[i]);
                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    string candidateProfilePath = NormalizePath(Path.Combine(directory, ProfilesFileName));
                    if (!File.Exists(candidateProfilePath))
                        continue;

                    if (Contains(_profilePaths, candidateProfilePath))
                        continue;

                    CharacterActionProfileJsonDto[] profiles = ReadProfiles(
                        File.ReadAllText(candidateProfilePath));
                    for (int profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
                    {
                        if (!ContainsProfile(_profiles, profiles[profileIndex].StableId))
                            _profiles.Add(profiles[profileIndex]);
                    }
                    _profilePaths.Add(candidateProfilePath);
                    AddLoadedDiagnostic(candidateProfilePath, "Using editor-exported Character Action Profile JSON.");
                }
            }

            private void ReadActionSources()
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    string directory = NormalizePath(_directories[i]);
                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    string candidateActionSetsPath = NormalizePath(Path.Combine(directory, ActionSetsFileName));
                    string actionsPath = NormalizePath(Path.Combine(directory, ActionsFileName));
                    bool hasActionSets = File.Exists(candidateActionSetsPath);
                    bool hasActions = File.Exists(actionsPath);
                    if (!hasActionSets && !hasActions)
                        continue;

                    if (hasActionSets)
                        TryAcceptActionSet(candidateActionSetsPath);
                    if (hasActions)
                        TryAddActionLibrary(directory, actionsPath);
                }
            }

            private void TryAcceptActionSet(string candidateActionSetsPath)
            {
                if (Contains(_actionSetSourcePaths, candidateActionSetsPath))
                    return;

                string candidateActionSetsJson = File.ReadAllText(candidateActionSetsPath);
                if (!TryReadActionSetEntries(
                        candidateActionSetsJson,
                        _profiles,
                        _actionSetEntries,
                        _usedActionSetIds))
                {
                    if (string.IsNullOrWhiteSpace(_actionSetsPath))
                    {
                        _actionSetsPath = candidateActionSetsPath;
                        _actionSetsJson = candidateActionSetsJson;
                    }
                    _actionSetSourcePaths.Add(candidateActionSetsPath);
                    AddLoadedDiagnostic(candidateActionSetsPath, "Using invalid editor-exported Character ActionSet JSON for diagnostics.");
                    return;
                }

                _actionSetSourcePaths.Add(candidateActionSetsPath);
                _actionSetsPath = string.Join(", ", _actionSetSourcePaths.ToArray());
                _actionSetsJson = CreateMergedActionSetsJson(_actionSetEntries);
                AddLoadedDiagnostic(candidateActionSetsPath, "Using editor-exported Character ActionSet JSON.");
            }

            private void TryAddActionLibrary(string directory, string actionsPath)
            {
                string libraryId = ReadLibraryId(directory);
                if (_profiles.Count > 0 && !AnyProfileContainsLibrary(_profiles, libraryId))
                    return;
                if (ContainsLibrarySource(_actionSourcePaths, libraryId))
                    return;

                string candidateActionsJson = File.ReadAllText(actionsPath);
                if (TryReadActionEntries(candidateActionsJson, _actionEntries, _usedActionIds))
                {
                    _actionSourcePaths.Add(actionsPath);
                }
                else if (string.IsNullOrWhiteSpace(_invalidActionsPath))
                {
                    _invalidActionsJson = candidateActionsJson;
                    _invalidActionsPath = actionsPath;
                    _actionSourcePaths.Add(actionsPath);
                }

                AddLoadedDiagnostic(actionsPath, "Using editor-exported Character Action library JSON.");
            }

            private bool HasCompleteExternalSource()
            {
                return !string.IsNullOrWhiteSpace(_actionSetsPath) && _actionSourcePaths.Count > 0;
            }

            private bool HasPartialExternalSource()
            {
                return !string.IsNullOrWhiteSpace(_actionSetsPath) || _actionSourcePaths.Count > 0;
            }

            private CharacterTestCharacterActionConfigSourcePayload CreateExternalPayload()
            {
                string description = _profilePaths.Count == 0
                    ? "external json: " + _actionSetsPath + " + " + string.Join(", ", _actionSourcePaths.ToArray())
                    : "external profiles: " + string.Join(", ", _profilePaths.ToArray());
                string actionsJson = string.IsNullOrWhiteSpace(_invalidActionsPath)
                    ? CreateMergedActionsJson(_actionEntries)
                    : _invalidActionsJson;
                string actionsSourcePath = string.IsNullOrWhiteSpace(_invalidActionsPath)
                    ? string.Join(", ", _actionSourcePaths.ToArray())
                    : _invalidActionsPath;
                return new CharacterTestCharacterActionConfigSourcePayload(
                    CharacterTestCharacterActionConfigSourceKind.ExternalJson,
                    description,
                    _actionSetsJson,
                    actionsJson,
                    ResolveReactionProfilesJson(),
                    ResolveLandingProfilesJson(),
                    _actionSetsPath,
                    actionsSourcePath,
                    ResolveReactionProfilesPath(),
                    ResolveLandingProfilesPath(),
                    _diagnostics.ToArray());
            }

            private CharacterTestCharacterActionConfigSourcePayload CreateIncompleteExternalPayload()
            {
                string missingPath = string.IsNullOrWhiteSpace(_actionSetsPath)
                    ? ActionSetsFileName
                    : ActionsFileName;
                _diagnostics.Add(new CharacterTestCharacterActionConfigSourceDiagnostic(
                    CharacterActionDiagnosticSeverity.Error,
                    ExternalJsonIncompleteCode,
                    missingPath,
                    "External Character Action JSON source is incomplete; embedded fallback is not used when a partial external source is present."));
                return new CharacterTestCharacterActionConfigSourcePayload(
                    CharacterTestCharacterActionConfigSourceKind.ExternalJson,
                    "external json incomplete",
                    _actionSetsJson,
                    CreateMergedActionsJson(_actionEntries),
                    ResolveReactionProfilesJson(),
                    ResolveLandingProfilesJson(),
                    string.IsNullOrWhiteSpace(_actionSetsPath) ? ActionSetsFileName : _actionSetsPath,
                    _actionSourcePaths.Count == 0 ? ActionsFileName : string.Join(", ", _actionSourcePaths.ToArray()),
                    ResolveReactionProfilesPath(),
                    ResolveLandingProfilesPath(),
                    _diagnostics.ToArray());
            }

            private CharacterTestCharacterActionConfigSourcePayload CreateMissingExternalPayload()
            {
                _diagnostics.Add(new CharacterTestCharacterActionConfigSourceDiagnostic(
                    CharacterActionDiagnosticSeverity.Error,
                    ExternalJsonMissingCode,
                    "Assets/MxFrameworkGenerated/CharacterActions",
                    "No generated Character Action Profile, ActionSet, or Action Library was found. "
                    + "Formal CharacterTest runtime does not fall back to the Codex Flow sample."));
                return new CharacterTestCharacterActionConfigSourcePayload(
                    CharacterTestCharacterActionConfigSourceKind.ExternalJson,
                    "generated character action source missing",
                    CreateMergedActionSetsJson(_actionSetEntries),
                    CreateMergedActionsJson(_actionEntries),
                    CreateEmptyNamedEntriesJson("reactionProfiles"),
                    CreateEmptyNamedEntriesJson("landingProfiles"),
                    ActionSetsFileName,
                    ActionsFileName,
                    ReactionProfilesFileName,
                    LandingProfilesFileName,
                    _diagnostics.ToArray());
            }

            private static CharacterTestCharacterActionConfigSourcePayload CreateEmbeddedPayload()
            {
                return new CharacterTestCharacterActionConfigSourcePayload(
                    CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback,
                    "embedded fallback",
                    ActionSetsJson,
                    ActionsJson,
                    ReactionProfilesJson,
                    LandingProfilesJson,
                    EmbeddedActionSetsSourcePath,
                    EmbeddedActionsSourcePath,
                    EmbeddedReactionProfilesSourcePath,
                    EmbeddedLandingProfilesSourcePath,
                    new[]
                    {
                        new CharacterTestCharacterActionConfigSourceDiagnostic(
                            CharacterActionDiagnosticSeverity.Info,
                            EmbeddedFallbackCode,
                            "CharacterTest",
                            "No complete external Character Action JSON source was found; using embedded sample data.")
                    });
            }

            private string ResolveLandingProfilesJson()
            {
                return string.IsNullOrWhiteSpace(_landingProfilesJson) || _landingProfileEntries.Count == 0
                    ? LandingProfilesJson
                    : _landingProfilesJson;
            }

            private string ResolveLandingProfilesPath()
            {
                return string.IsNullOrWhiteSpace(_landingProfilesPath)
                    ? LandingProfilesFileName
                    : _landingProfilesPath;
            }

            private string ResolveReactionProfilesJson()
            {
                return string.IsNullOrWhiteSpace(_reactionProfilesJson)
                    ? ReactionProfilesJson
                    : _reactionProfilesJson;
            }

            private string ResolveReactionProfilesPath()
            {
                return string.IsNullOrWhiteSpace(_reactionProfilesPath)
                    ? EmbeddedReactionProfilesSourcePath
                    : _reactionProfilesPath;
            }

            private void AddLoadedDiagnostic(string sourcePath, string message)
            {
                _diagnostics.Add(new CharacterTestCharacterActionConfigSourceDiagnostic(
                    CharacterActionDiagnosticSeverity.Info,
                    ExternalJsonLoadedCode,
                    sourcePath,
                    message));
            }
        }

        private static string CreateMergedActionsJson(List<JToken> actions)
        {
            var document = new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                ["actions"] = new JArray(actions ?? new List<JToken>())
            };
            return JsonConvert.SerializeObject(
                document,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }

        private static string CreateEmptyNamedEntriesJson(string propertyName)
        {
            var document = new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                [propertyName] = new JArray()
            };
            return JsonConvert.SerializeObject(
                document,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }

        private static string CreateMergedActionSetsJson(List<JToken> actionSets)
        {
            var document = new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                ["actionSets"] = new JArray(actionSets ?? new List<JToken>())
            };
            return JsonConvert.SerializeObject(
                document,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }

        private static CharacterActionProfileJsonDto[] ReadProfiles(string json)
        {
            CharacterActionProfilesJsonDocument document =
                JsonConvert.DeserializeObject<CharacterActionProfilesJsonDocument>(json);
            return document?.Profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
        }

        private static bool TryReadActionSetEntries(
            string json,
            List<CharacterActionProfileJsonDto> profiles,
            List<JToken> target,
            HashSet<int> usedIds)
        {
            try
            {
                JObject root = JObject.Parse(json);
                JArray actionSets = root["actionSets"] as JArray;
                if (actionSets == null)
                    return true;

                for (int i = 0; i < actionSets.Count; i++)
                {
                    JObject actionSet = actionSets[i] as JObject;
                    string stableId = (string)actionSet?["stableId"] ?? string.Empty;
                    if (profiles.Count > 0 && !AnyProfileContainsActionSet(profiles, stableId))
                        continue;
                    if (ContainsStableId(target, stableId))
                        continue;

                    JObject clone = actionSet.DeepClone() as JObject;
                    if (clone == null)
                        continue;
                    int id = (int?)clone["id"] ?? 0;
                    if (id <= 0 || usedIds.Contains(id))
                        id = AllocateActionSetId(usedIds);
                    clone["id"] = id;
                    usedIds.Add(id);
                    target.Add(clone);
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadActionEntries(
            string json,
            List<JToken> actionEntries,
            HashSet<int> usedIds)
        {
            try
            {
                JObject actionsRoot = JObject.Parse(json);
                JArray actions = actionsRoot["actions"] as JArray;
                if (actions != null)
                {
                    for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                    {
                        JObject clone = actions[actionIndex].DeepClone() as JObject;
                        if (clone == null)
                            continue;

                        int id = (int?)clone["id"] ?? 0;
                        if (id <= 0 || usedIds.Contains(id))
                            id = AllocateActionId(usedIds);
                        clone["id"] = id;
                        usedIds.Add(id);
                        actionEntries.Add(clone);
                    }
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static int AllocateActionId(HashSet<int> usedIds)
        {
            int id = 1_000_000;
            while (usedIds.Contains(id))
                id++;
            return id;
        }

        private static int AllocateActionSetId(HashSet<int> usedIds)
        {
            int id = 2_000_000;
            while (usedIds.Contains(id))
                id++;
            return id;
        }

        private static bool TryReadNamedEntries(
            string json,
            string arrayName,
            List<JToken> target)
        {
            try
            {
                JObject root = JObject.Parse(json);
                JArray entries = root[arrayName] as JArray;
                if (entries == null)
                    return false;

                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    string stableId = (string)entry?["stableId"] ?? string.Empty;
                    if (entry != null && !ContainsStableId(target, stableId))
                        target.Add(entry.DeepClone());
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string CreateMergedNamedEntriesJson(
            string arrayName,
            List<JToken> entries)
        {
            var document = new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                [arrayName] = new JArray(entries ?? new List<JToken>())
            };
            return JsonConvert.SerializeObject(
                document,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }

        private static bool Contains(string[] values, string value)
        {
            values = values ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool Contains(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsProfile(
            List<CharacterActionProfileJsonDto> profiles,
            string stableId)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool AnyProfileContainsActionSet(
            List<CharacterActionProfileJsonDto> profiles,
            string actionSetId)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (Contains(profiles[i]?.ActionSetIds, actionSetId))
                    return true;
            }

            return false;
        }

        private static bool AnyProfileContainsLibrary(
            List<CharacterActionProfileJsonDto> profiles,
            string libraryId)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (Contains(profiles[i]?.ActionLibraryIds, libraryId))
                    return true;
            }

            return false;
        }

        private static bool ContainsStableId(List<JToken> values, string stableId)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals((string)values[i]?["stableId"], stableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool ContainsLibrarySource(List<string> paths, string libraryId)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string directory = Path.GetDirectoryName(paths[i]) ?? string.Empty;
                if (string.Equals(ReadLibraryId(directory), libraryId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string ReadLibraryId(string directory)
        {
            directory = NormalizePath(directory).TrimEnd('/');
            int index = directory.LastIndexOf('/');
            return index < 0 ? directory : directory.Substring(index + 1);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }

        private readonly struct CharacterTestCharacterActionConfigSourcePayload
        {
            public CharacterTestCharacterActionConfigSourcePayload(
                CharacterTestCharacterActionConfigSourceKind sourceKind,
                string sourceDescription,
                string actionSetsJson,
                string actionsJson,
                string reactionProfilesJson,
                string landingProfilesJson,
                string actionSetsSourcePath,
                string actionsSourcePath,
                string reactionProfilesSourcePath,
                string landingProfilesSourcePath,
                CharacterTestCharacterActionConfigSourceDiagnostic[] diagnostics)
            {
                SourceKind = sourceKind;
                SourceDescription = sourceDescription ?? string.Empty;
                ActionSetsJson = actionSetsJson ?? string.Empty;
                ActionsJson = actionsJson ?? string.Empty;
                ReactionProfilesJson = reactionProfilesJson ?? string.Empty;
                LandingProfilesJson = landingProfilesJson ?? string.Empty;
                ActionSetsSourcePath = actionSetsSourcePath ?? string.Empty;
                ActionsSourcePath = actionsSourcePath ?? string.Empty;
                ReactionProfilesSourcePath = reactionProfilesSourcePath ?? string.Empty;
                LandingProfilesSourcePath = landingProfilesSourcePath ?? string.Empty;
                Diagnostics = diagnostics ?? Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>();
            }

            public CharacterTestCharacterActionConfigSourceKind SourceKind { get; }
            public string SourceDescription { get; }
            public string ActionSetsJson { get; }
            public string ActionsJson { get; }
            public string ReactionProfilesJson { get; }
            public string LandingProfilesJson { get; }
            public string ActionSetsSourcePath { get; }
            public string ActionsSourcePath { get; }
            public string ReactionProfilesSourcePath { get; }
            public string LandingProfilesSourcePath { get; }
            public CharacterTestCharacterActionConfigSourceDiagnostic[] Diagnostics { get; }
        }
    }
}
