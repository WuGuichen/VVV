using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    internal enum CharacterActionAuthoringDocumentKind
    {
        Profile = 0,
        ActionSetBundle = 1,
        ActionLibrary = 2,
        Projectile = 3,
        MechanicTuning = 4,
        AbilityEffectTimeline = 5,
    }

    internal readonly struct CharacterActionAuthoringDocumentKey : IEquatable<CharacterActionAuthoringDocumentKey>
    {
        internal CharacterActionAuthoringDocumentKey(
            CharacterActionAuthoringDocumentKind kind,
            string sourcePath)
        {
            Kind = kind;
            SourcePath = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
        }

        internal CharacterActionAuthoringDocumentKind Kind { get; }
        internal string SourcePath { get; }

        public bool Equals(CharacterActionAuthoringDocumentKey other)
        {
            return Kind == other.Kind
                && string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionAuthoringDocumentKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (SourcePath?.GetHashCode() ?? 0);
            }
        }

        public override string ToString()
        {
            return Kind + ":" + SourcePath;
        }

        public static bool operator ==(
            CharacterActionAuthoringDocumentKey left,
            CharacterActionAuthoringDocumentKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CharacterActionAuthoringDocumentKey left,
            CharacterActionAuthoringDocumentKey right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct CharacterActionAuthoringPreparedDocumentWrite
    {
        internal CharacterActionAuthoringPreparedDocumentWrite(string path, string contents, string expectedFileHash = null)
        {
            Path = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(path);
            Contents = contents ?? string.Empty;
            ExpectedFileHash = expectedFileHash;
        }

        internal string Path { get; }
        internal string Contents { get; }
        internal string ExpectedFileHash { get; }
    }

    internal sealed class CharacterActionAuthoringDocumentState
    {
        [ThreadStatic] private static Dictionary<CharacterActionAuthoringDocumentState, DirtyRead> _dirtyReads;

        // A UI projection may ask for the same document status dozens of times.
        // Cache only inside one synchronous projection/save operation, never across frames.
        internal static IDisposable BeginDirtyReadScope()
        {
            return new DirtyReadScope();
        }

        private sealed class DirtyRead
        {
            internal string Json;
            internal bool IsDirty;
        }

        private sealed class DirtyReadScope : IDisposable
        {
            private readonly bool _ownsCache;
            internal DirtyReadScope()
            {
                _ownsCache = _dirtyReads == null;
                if (_ownsCache)
                    _dirtyReads = new Dictionary<CharacterActionAuthoringDocumentState, DirtyRead>();
            }
            public void Dispose()
            {
                if (_ownsCache)
                    _dirtyReads = null;
            }
        }
        private readonly Func<string> _serializeState;
        private readonly Func<CharacterActionAuthoringPreparedDocumentWrite[]> _prepareWrites;
        private readonly Action<CharacterActionAuthoringPreparedDocumentWrite[]> _acceptWrites;
        private string _baselineCanonicalJson;
        private string _baselineSha256;
        private string _lastComparedJson;
        private bool _lastComparedDirty;

        internal CharacterActionAuthoringDocumentState(
            CharacterActionAuthoringDocumentKey key,
            Func<string> serializeState,
            Func<CharacterActionAuthoringPreparedDocumentWrite[]> prepareWrites,
            Action<CharacterActionAuthoringPreparedDocumentWrite[]> acceptWrites)
        {
            Key = key;
            _serializeState = serializeState ?? throw new ArgumentNullException(nameof(serializeState));
            _prepareWrites = prepareWrites ?? throw new ArgumentNullException(nameof(prepareWrites));
            _acceptWrites = acceptWrites ?? throw new ArgumentNullException(nameof(acceptWrites));
            _lastComparedJson = _serializeState();
            _baselineCanonicalJson = CanonicalizeJson(_lastComparedJson);
            _baselineSha256 = ComputeSha256(_baselineCanonicalJson);
        }

        internal CharacterActionAuthoringDocumentKey Key { get; }
        internal long Revision { get; private set; }
        internal string BaselineCanonicalJson => _baselineCanonicalJson;
        internal string BaselineSha256 => _baselineSha256;
        internal bool IsDirty
        {
            get
            {
                if (_dirtyReads != null && _dirtyReads.TryGetValue(this, out DirtyRead cached))
                    return cached.IsDirty;
                string json = _serializeState();
                // Compare the actual serialized content, so direct DTO changes are
                // still detected without relying on a revision notification.
                bool dirty = string.Equals(json, _lastComparedJson, StringComparison.Ordinal)
                    ? _lastComparedDirty
                    : !string.Equals(_baselineCanonicalJson, CanonicalizeJson(json), StringComparison.Ordinal);
                _lastComparedJson = json;
                _lastComparedDirty = dirty;
                if (_dirtyReads != null)
                    _dirtyReads[this] = new DirtyRead { Json = json, IsDirty = dirty };
                return dirty;
            }
        }

        internal void MarkChanged()
        {
            _dirtyReads?.Remove(this);
            Revision++;
        }

        internal string SerializeCurrentState()
        {
            if (_dirtyReads != null && _dirtyReads.TryGetValue(this, out DirtyRead cached))
                return cached.Json;
            return _serializeState();
        }

        internal void RestoreRevision(long revision)
        {
            _dirtyReads?.Remove(this);
            Revision = revision > 0 ? revision : 1;
        }

        internal CharacterActionAuthoringPreparedDocumentWrite[] PrepareWrites()
        {
            return _prepareWrites();
        }

        internal string[] Save()
        {
            CharacterActionAuthoringPreparedDocumentWrite[] writes = _prepareWrites();
            for (int i = 0; i < writes.Length; i++)
            {
                string directory = Path.GetDirectoryName(writes[i].Path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(writes[i].Path, writes[i].Contents);
            }

            AcceptPreparedWrites(writes);

            var paths = new string[writes.Length];
            for (int i = 0; i < writes.Length; i++)
                paths[i] = writes[i].Path;
            return paths;
        }

        internal void AcceptPreparedWrites(CharacterActionAuthoringPreparedDocumentWrite[] writes)
        {
            _dirtyReads?.Remove(this);
            writes = writes ?? Array.Empty<CharacterActionAuthoringPreparedDocumentWrite>();
            _acceptWrites(writes);
            _lastComparedJson = _serializeState();
            _lastComparedDirty = false;
            _baselineCanonicalJson = CanonicalizeJson(_lastComparedJson);
            _baselineSha256 = ComputeSha256(_baselineCanonicalJson);
            if (_dirtyReads != null)
                _dirtyReads[this] = new DirtyRead { Json = _lastComparedJson, IsDirty = false };
        }

        internal static string CanonicalizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;
            JToken token = JToken.Parse(json);
            NormalizeWholeNumberTokens(token);
            return token.ToString(Formatting.None);
        }

        private static void NormalizeWholeNumberTokens(JToken token)
        {
            for (JToken child = token?.First; child != null;)
            {
                JToken next = child.Next;
                if (child is JValue value
                    && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                    && decimal.TryParse(
                        Convert.ToString(value.Value, CultureInfo.InvariantCulture),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out decimal number)
                    && number == decimal.Truncate(number)
                    && number >= long.MinValue
                    && number <= long.MaxValue)
                {
                    child.Replace(new JValue(decimal.ToInt64(number)));
                }
                else if (child is JContainer)
                {
                    NormalizeWholeNumberTokens(child);
                }

                child = next;
            }
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    internal sealed class CharacterActionAuthoringProfileDocument
    {
        private readonly CharacterActionAuthoringJsonArrayBacking<CharacterActionProfileJsonDto> _backing;

        internal CharacterActionAuthoringProfileDocument(
            CharacterActionAuthoringSourceEntry source,
            string json)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Document = DeserializeDocument<CharacterActionProfilesJsonDocument>(json, source.ProfilePath);
            Document.Profiles = Document.Profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
            _backing = new CharacterActionAuthoringJsonArrayBacking<CharacterActionProfileJsonDto>(
                json,
                "profiles",
                () => Document.SchemaVersion,
                () => Document.Profiles,
                profile => profile?.StableId ?? string.Empty);
            State = new CharacterActionAuthoringDocumentState(
                new CharacterActionAuthoringDocumentKey(
                    CharacterActionAuthoringDocumentKind.Profile,
                    source.ProfilePath),
                () => _backing.Serialize(),
                () => new[]
                {
                    new CharacterActionAuthoringPreparedDocumentWrite(source.ProfilePath, State.SerializeCurrentState()),
                },
                AcceptWrites);
        }

        internal CharacterActionAuthoringSourceEntry Source { get; }
        internal CharacterActionProfilesJsonDocument Document { get; }
        internal CharacterActionAuthoringDocumentState State { get; }
        internal string Json => _backing.Serialize();

        internal void RestoreDraft(string json)
        {
            CharacterActionProfilesJsonDocument restored =
                DeserializeDocument<CharacterActionProfilesJsonDocument>(json, Source.ProfilePath);
            Document.SchemaVersion = restored.SchemaVersion ?? string.Empty;
            Document.Profiles = restored.Profiles ?? Array.Empty<CharacterActionProfileJsonDto>();
        }

        private void AcceptWrites(CharacterActionAuthoringPreparedDocumentWrite[] writes)
        {
            if (writes.Length > 0)
                _backing.AcceptSerialized(writes[0].Contents);
        }

        private static TDocument DeserializeDocument<TDocument>(string json, string sourcePath)
            where TDocument : class
        {
            try
            {
                TDocument document = JsonConvert.DeserializeObject<TDocument>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
                return document ?? throw new InvalidDataException("JSON document is null: " + sourcePath);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Failed to parse Character Action JSON: " + sourcePath, ex);
            }
        }
    }

    internal sealed class CharacterActionAuthoringActionSetBundleDocument
    {
        private readonly CharacterActionAuthoringJsonArrayBacking<CharacterActionSetJsonDto> _actionSetsBacking;
        private readonly CharacterActionAuthoringJsonArrayBacking<CharacterReactionProfileJsonDto> _reactionProfilesBacking;
        private readonly CharacterActionAuthoringJsonArrayBacking<CharacterLandingProfileJsonDto> _landingProfilesBacking;

        internal CharacterActionAuthoringActionSetBundleDocument(
            CharacterActionAuthoringSourceEntry source,
            string actionSetsJson,
            string reactionProfilesJson,
            string landingProfilesJson)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ActionSetsDocument = DeserializeDocument<CharacterActionSetsJsonDocument>(
                actionSetsJson,
                source.ActionSetsPath);
            ReactionProfilesDocument = DeserializeDocument<CharacterReactionProfilesJsonDocument>(
                reactionProfilesJson,
                source.ReactionProfilesPath);
            LandingProfilesDocument = DeserializeDocument<CharacterLandingProfilesJsonDocument>(
                landingProfilesJson,
                source.LandingProfilesPath);
            ActionSetsDocument.ActionSets = ActionSetsDocument.ActionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            ReactionProfilesDocument.ReactionProfiles = ReactionProfilesDocument.ReactionProfiles
                ?? Array.Empty<CharacterReactionProfileJsonDto>();
            LandingProfilesDocument.LandingProfiles = LandingProfilesDocument.LandingProfiles
                ?? Array.Empty<CharacterLandingProfileJsonDto>();

            _actionSetsBacking = new CharacterActionAuthoringJsonArrayBacking<CharacterActionSetJsonDto>(
                actionSetsJson,
                "actionSets",
                () => ActionSetsDocument.SchemaVersion,
                () => ActionSetsDocument.ActionSets,
                actionSet => actionSet?.StableId ?? string.Empty);
            _reactionProfilesBacking = new CharacterActionAuthoringJsonArrayBacking<CharacterReactionProfileJsonDto>(
                reactionProfilesJson,
                "reactionProfiles",
                () => ReactionProfilesDocument.SchemaVersion,
                () => ReactionProfilesDocument.ReactionProfiles,
                profile => profile?.StableId ?? string.Empty);
            _landingProfilesBacking = new CharacterActionAuthoringJsonArrayBacking<CharacterLandingProfileJsonDto>(
                landingProfilesJson,
                "landingProfiles",
                () => LandingProfilesDocument.SchemaVersion,
                () => LandingProfilesDocument.LandingProfiles,
                profile => profile?.StableId ?? string.Empty);
            State = new CharacterActionAuthoringDocumentState(
                new CharacterActionAuthoringDocumentKey(
                    CharacterActionAuthoringDocumentKind.ActionSetBundle,
                    source.ActionSetsPath),
                SerializeState,
                PrepareWrites,
                AcceptWrites);
        }

        internal CharacterActionAuthoringSourceEntry Source { get; }
        internal CharacterActionSetsJsonDocument ActionSetsDocument { get; }
        internal CharacterReactionProfilesJsonDocument ReactionProfilesDocument { get; }
        internal CharacterLandingProfilesJsonDocument LandingProfilesDocument { get; }
        internal CharacterActionAuthoringDocumentState State { get; }
        internal string ActionSetsJson => _actionSetsBacking.Serialize();
        internal string ReactionProfilesJson => _reactionProfilesBacking.Serialize();
        internal string LandingProfilesJson => _landingProfilesBacking.Serialize();

        internal void RestoreDraft(string json)
        {
            JObject state = JObject.Parse(json);
            JToken actionSetsToken = state["actionSets"];
            JToken reactionProfilesToken = state["reactionProfiles"];
            JToken landingProfilesToken = state["landingProfiles"];
            if (actionSetsToken == null || reactionProfilesToken == null)
                throw new InvalidDataException("ActionSet bundle draft is incomplete.");
            if (landingProfilesToken == null)
            {
                landingProfilesToken = JObject.FromObject(
                    new CharacterLandingProfilesJsonDocument
                    {
                        SchemaVersion = ActionSetsDocument.SchemaVersion,
                        LandingProfiles = Array.Empty<CharacterLandingProfileJsonDto>(),
                    });
            }

            CharacterActionSetsJsonDocument restoredActionSets =
                DeserializeDocument<CharacterActionSetsJsonDocument>(
                    actionSetsToken.ToString(Formatting.None),
                    Source.ActionSetsPath);
            CharacterReactionProfilesJsonDocument restoredReactionProfiles =
                DeserializeDocument<CharacterReactionProfilesJsonDocument>(
                    reactionProfilesToken.ToString(Formatting.None),
                    Source.ReactionProfilesPath);
            CharacterLandingProfilesJsonDocument restoredLandingProfiles =
                DeserializeDocument<CharacterLandingProfilesJsonDocument>(
                    landingProfilesToken.ToString(Formatting.None),
                    Source.LandingProfilesPath);

            ActionSetsDocument.SchemaVersion = restoredActionSets.SchemaVersion ?? string.Empty;
            ActionSetsDocument.ActionSets = restoredActionSets.ActionSets
                ?? Array.Empty<CharacterActionSetJsonDto>();
            ReactionProfilesDocument.SchemaVersion = restoredReactionProfiles.SchemaVersion ?? string.Empty;
            ReactionProfilesDocument.ReactionProfiles = restoredReactionProfiles.ReactionProfiles
                ?? Array.Empty<CharacterReactionProfileJsonDto>();
            LandingProfilesDocument.SchemaVersion = restoredLandingProfiles.SchemaVersion ?? string.Empty;
            LandingProfilesDocument.LandingProfiles = restoredLandingProfiles.LandingProfiles
                ?? Array.Empty<CharacterLandingProfileJsonDto>();
        }

        private string SerializeState()
        {
            return new JObject
            {
                ["actionSets"] = JToken.Parse(_actionSetsBacking.Serialize()),
                ["reactionProfiles"] = JToken.Parse(_reactionProfilesBacking.Serialize()),
                ["landingProfiles"] = JToken.Parse(_landingProfilesBacking.Serialize()),
            }.ToString(Formatting.None);
        }

        private CharacterActionAuthoringPreparedDocumentWrite[] PrepareWrites()
        {
            JObject state = JObject.Parse(State.SerializeCurrentState());
            return new[]
            {
                new CharacterActionAuthoringPreparedDocumentWrite(
                    Source.ActionSetsPath,
                    state["actionSets"].ToString(Formatting.Indented)),
                new CharacterActionAuthoringPreparedDocumentWrite(
                    Source.ReactionProfilesPath,
                    state["reactionProfiles"].ToString(Formatting.Indented)),
                new CharacterActionAuthoringPreparedDocumentWrite(
                    Source.LandingProfilesPath,
                    state["landingProfiles"].ToString(Formatting.Indented)),
            };
        }

        private void AcceptWrites(CharacterActionAuthoringPreparedDocumentWrite[] writes)
        {
            for (int i = 0; i < writes.Length; i++)
            {
                if (string.Equals(writes[i].Path, Source.ActionSetsPath, StringComparison.Ordinal))
                    _actionSetsBacking.AcceptSerialized(writes[i].Contents);
                else if (string.Equals(writes[i].Path, Source.ReactionProfilesPath, StringComparison.Ordinal))
                    _reactionProfilesBacking.AcceptSerialized(writes[i].Contents);
                else if (string.Equals(writes[i].Path, Source.LandingProfilesPath, StringComparison.Ordinal))
                    _landingProfilesBacking.AcceptSerialized(writes[i].Contents);
            }
        }

        private static TDocument DeserializeDocument<TDocument>(string json, string sourcePath)
            where TDocument : class
        {
            try
            {
                TDocument document = JsonConvert.DeserializeObject<TDocument>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
                return document ?? throw new InvalidDataException("JSON document is null: " + sourcePath);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Failed to parse Character Action JSON: " + sourcePath, ex);
            }
        }
    }

    internal sealed class CharacterActionAuthoringActionLibraryDocument
    {
        private readonly CharacterActionAuthoringJsonArrayBacking<CharacterActionJsonDto> _backing;

        internal CharacterActionAuthoringActionLibraryDocument(
            CharacterActionAuthoringSourceEntry source,
            string libraryId,
            string json)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            LibraryId = libraryId ?? string.Empty;
            Document = DeserializeDocument<CharacterActionsJsonDocument>(json, source.ActionsPath);
            Document.Actions = Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < Document.Actions.Length; i++)
            {
                Document.Actions[i] = CharacterActionAuthoringModel.NormalizeAction(
                    Document.Actions[i]);
            }
            _backing = new CharacterActionAuthoringJsonArrayBacking<CharacterActionJsonDto>(
                json,
                "actions",
                () => Document.SchemaVersion,
                () => Document.Actions,
                action => (action?.ActionId ?? 0).ToString(CultureInfo.InvariantCulture));
            State = new CharacterActionAuthoringDocumentState(
                new CharacterActionAuthoringDocumentKey(
                    CharacterActionAuthoringDocumentKind.ActionLibrary,
                    source.ActionsPath),
                () => SerializeActions(),
                () => new[]
                {
                    new CharacterActionAuthoringPreparedDocumentWrite(source.ActionsPath, State.SerializeCurrentState()),
                },
                AcceptWrites);
        }

        internal CharacterActionAuthoringSourceEntry Source { get; }
        internal string LibraryId { get; }
        internal CharacterActionsJsonDocument Document { get; }
        internal CharacterActionAuthoringDocumentState State { get; }
        internal string Json => SerializeActions();

        private string SerializeActions()
        {
            foreach (CharacterActionJsonDto action in Document.Actions)
                CharacterActionAuthoringModel.NormalizeAction(action);
            return _backing.Serialize();
        }

        internal void RestoreDraft(string json)
        {
            CharacterActionsJsonDocument restored =
                DeserializeDocument<CharacterActionsJsonDocument>(json, Source.ActionsPath);
            Document.SchemaVersion = restored.SchemaVersion ?? string.Empty;
            Document.Actions = restored.Actions ?? Array.Empty<CharacterActionJsonDto>();
        }

        private void AcceptWrites(CharacterActionAuthoringPreparedDocumentWrite[] writes)
        {
            if (writes.Length > 0)
                _backing.AcceptSerialized(writes[0].Contents);
        }

        private static TDocument DeserializeDocument<TDocument>(string json, string sourcePath)
            where TDocument : class
        {
            try
            {
                TDocument document = JsonConvert.DeserializeObject<TDocument>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
                return document ?? throw new InvalidDataException("JSON document is null: " + sourcePath);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Failed to parse Character Action JSON: " + sourcePath, ex);
            }
        }
    }

    internal sealed class CharacterActionAuthoringActionReference
    {
        internal CharacterActionAuthoringActionReference(
            CharacterActionAuthoringActionLibraryDocument library,
            CharacterActionJsonDto action)
        {
            Library = library;
            Action = action;
        }

        internal CharacterActionAuthoringActionLibraryDocument Library { get; }
        internal CharacterActionJsonDto Action { get; }
        internal string LibraryId => Library?.LibraryId ?? string.Empty;
    }

    internal sealed class CharacterActionAuthoringSelectionState
    {
        internal string ProfileStableId { get; set; } = string.Empty;
        internal string ActionSetStableId { get; set; } = string.Empty;
        internal string ActionLibraryId { get; set; } = string.Empty;
        internal int ActionId { get; set; }
        internal string TimelineItemStableId { get; set; } = string.Empty;
    }

    internal sealed class CharacterActionAuthoringDocumentSession
    {
        private const int DirtyDraftPayloadVersion = 1;

        private readonly List<CharacterActionAuthoringActionSetBundleDocument> _actionSetBundles =
            new List<CharacterActionAuthoringActionSetBundleDocument>();
        private readonly List<CharacterActionAuthoringActionLibraryDocument> _actionLibraries =
            new List<CharacterActionAuthoringActionLibraryDocument>();
        private readonly List<CharacterActionAuthoringDocumentState> _documentStates =
            new List<CharacterActionAuthoringDocumentState>();
        private readonly List<CharacterActionAuthoringActionReference> _profileActions =
            new List<CharacterActionAuthoringActionReference>();
        private string[] _unresolvedActionSetIds = Array.Empty<string>();
        private string[] _unresolvedActionLibraryIds = Array.Empty<string>();

        private CharacterActionAuthoringDocumentSession(CharacterActionAuthoringProfileDocument profileDocument)
        {
            ProfileDocument = profileDocument ?? throw new ArgumentNullException(nameof(profileDocument));
            _documentStates.Add(profileDocument.State);
        }

        internal CharacterActionAuthoringProfileDocument ProfileDocument { get; }
        internal IReadOnlyList<CharacterActionAuthoringActionSetBundleDocument> ActionSetBundles => _actionSetBundles;
        internal IReadOnlyList<CharacterActionAuthoringActionLibraryDocument> ActionLibraries => _actionLibraries;
        internal IReadOnlyList<CharacterActionAuthoringDocumentState> DocumentStates => _documentStates;
        internal IReadOnlyList<CharacterActionAuthoringActionReference> ProfileActions => _profileActions;
        internal CharacterActionAuthoringSelectionState Selection { get; } =
            new CharacterActionAuthoringSelectionState();
        internal string[] UnresolvedActionSetIds => _unresolvedActionSetIds;
        internal string[] UnresolvedActionLibraryIds => _unresolvedActionLibraryIds;

        internal CharacterActionProfileJsonDto CurrentProfile => FindProfile(Selection.ProfileStableId);
        internal CharacterActionSetJsonDto CurrentActionSet => FindActionSet(Selection.ActionSetStableId);
        internal CharacterActionAuthoringActionLibraryDocument CurrentActionLibrary =>
            FindActionLibrary(Selection.ActionLibraryId);
        internal CharacterActionJsonDto CurrentAction => FindActionReference(Selection.ActionId)?.Action;

        internal CharacterReactionProfileJsonDto CurrentReactionProfile
        {
            get
            {
                CharacterActionSetJsonDto actionSet = CurrentActionSet;
                if (actionSet == null || string.IsNullOrWhiteSpace(actionSet.ReactionProfileId))
                    return null;

                CharacterActionAuthoringActionSetBundleDocument bundle =
                    FindActionSetBundle(actionSet.StableId);
                CharacterReactionProfileJsonDto[] profiles =
                    bundle?.ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                for (int i = 0; i < profiles.Length; i++)
                {
                    if (string.Equals(
                        profiles[i]?.StableId,
                        actionSet.ReactionProfileId,
                        StringComparison.Ordinal))
                    {
                        return profiles[i];
                    }
                }

                return null;
            }
        }

        internal static CharacterActionAuthoringDocumentSession Load(
            CharacterActionAuthoringSourceEntry profileSource,
            string selectedProfileStableId = null)
        {
            return Load(
                profileSource,
                CharacterActionAuthoringSourceCatalog.DiscoverActionSetSources(),
                CharacterActionAuthoringSourceCatalog.DiscoverActionLibrarySources(),
                selectedProfileStableId);
        }

        internal static CharacterActionAuthoringDocumentSession Load(
            CharacterActionAuthoringSourceEntry profileSource,
            CharacterActionAuthoringSourceEntry[] actionSetSources,
            CharacterActionAuthoringSourceEntry[] actionLibrarySources,
            string selectedProfileStableId = null)
        {
            if (profileSource == null)
                throw new ArgumentNullException(nameof(profileSource));
            if (!profileSource.HasProfile)
            {
                throw new FileNotFoundException(
                    "Character Action Profile source is missing character_action_profile.json.",
                    profileSource.ProfilePath);
            }

            var profileDocument = new CharacterActionAuthoringProfileDocument(
                profileSource,
                File.ReadAllText(profileSource.ProfilePath));
            var session = new CharacterActionAuthoringDocumentSession(profileDocument);
            session.LoadReferencedDocuments(
                actionSetSources ?? Array.Empty<CharacterActionAuthoringSourceEntry>(),
                actionLibrarySources ?? Array.Empty<CharacterActionAuthoringSourceEntry>());
            session.InitializeSelection(selectedProfileStableId);
            return session;
        }

        internal bool SelectProfile(string stableId)
        {
            CharacterActionProfileJsonDto profile = FindProfile(stableId);
            if (profile == null)
                return false;

            Selection.ProfileStableId = profile.StableId ?? string.Empty;
            AdjustSelectionForCurrentProfile();
            return true;
        }

        internal bool SelectActionSet(string stableId)
        {
            CharacterActionProfileJsonDto profile = CurrentProfile;
            if (profile == null
                || !Contains(profile.ActionSetIds, stableId)
                || FindActionSet(stableId) == null)
            {
                return false;
            }

            Selection.ActionSetStableId = stableId ?? string.Empty;
            return true;
        }

        internal bool SelectActionLibrary(string libraryId)
        {
            CharacterActionProfileJsonDto profile = CurrentProfile;
            CharacterActionAuthoringActionLibraryDocument library = FindActionLibrary(libraryId);
            if (profile == null
                || library == null
                || !Contains(profile.ActionLibraryIds, libraryId))
            {
                return false;
            }

            Selection.ActionLibraryId = libraryId ?? string.Empty;
            if (!ContainsAction(library, Selection.ActionId))
                Selection.ActionId = FirstActionId(library);
            return true;
        }

        internal bool SelectAction(int actionId)
        {
            CharacterActionAuthoringActionReference action = FindActionReference(actionId);
            if (action == null)
            {
                // Structural edits replace a library array before the window reports the
                // document mutation. Rebuild the profile index on demand so a newly added
                // action can become the numeric-id selection in the same UI transaction.
                RebuildProfileActions(CurrentProfile);
                action = FindActionReference(actionId);
            }
            if (action == null)
                return false;

            Selection.ActionLibraryId = action.LibraryId;
            Selection.ActionId = action.Action?.ActionId ?? 0;
            return true;
        }

        internal void SelectTimelineItem(string stableId)
        {
            Selection.TimelineItemStableId = stableId ?? string.Empty;
        }

        internal void RefreshActionIndex()
        {
            RebuildProfileActions(CurrentProfile);
            AdjustSelectionForCurrentProfile();
        }

        internal CharacterActionAuthoringDocumentState FindDocumentState(
            CharacterActionAuthoringDocumentKey key)
        {
            for (int i = 0; i < _documentStates.Count; i++)
            {
                if (_documentStates[i].Key == key)
                    return _documentStates[i];
            }

            return null;
        }

        internal CharacterActionAuthoringActionSetBundleDocument FindActionSetBundle(string actionSetStableId)
        {
            for (int i = 0; i < _actionSetBundles.Count; i++)
            {
                CharacterActionSetJsonDto[] actionSets =
                    _actionSetBundles[i].ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int j = 0; j < actionSets.Length; j++)
                {
                    if (string.Equals(actionSets[j]?.StableId, actionSetStableId, StringComparison.Ordinal))
                        return _actionSetBundles[i];
                }
            }

            return null;
        }

        internal CharacterActionAuthoringActionLibraryDocument FindActionLibraryForAction(int actionId)
        {
            return FindActionReference(actionId)?.Library;
        }

        internal void MarkChanged(CharacterActionAuthoringDocumentKey key)
        {
            FindDocumentState(key)?.MarkChanged();
        }

        internal void MarkCurrentActionChanged()
        {
            CharacterActionAuthoringActionLibraryDocument library = CurrentActionLibrary
                ?? FindActionLibraryForAction(Selection.ActionId);
            library?.State.MarkChanged();
            RebuildProfileActions(CurrentProfile);
        }

        internal void MarkCurrentActionSetChanged(CharacterActionSetJsonDto actionSet = null)
        {
            CharacterActionAuthoringActionSetBundleDocument bundle = null;
            if (actionSet != null)
            {
                for (int i = 0; i < _actionSetBundles.Count && bundle == null; i++)
                {
                    CharacterActionSetJsonDto[] actionSets =
                        _actionSetBundles[i].ActionSetsDocument.ActionSets
                        ?? Array.Empty<CharacterActionSetJsonDto>();
                    for (int j = 0; j < actionSets.Length; j++)
                    {
                        if (!ReferenceEquals(actionSets[j], actionSet))
                            continue;
                        bundle = _actionSetBundles[i];
                        break;
                    }
                }
            }

            bundle = bundle ?? FindActionSetBundle(Selection.ActionSetStableId);
            bundle?.State.MarkChanged();
            if (actionSet != null && bundle != null)
                Selection.ActionSetStableId = actionSet.StableId ?? string.Empty;
        }

        internal void MarkCurrentProfileChanged(CharacterActionProfileJsonDto profile)
        {
            ProfileDocument.State.MarkChanged();
            if (profile != null)
                Selection.ProfileStableId = profile.StableId ?? string.Empty;
            RebuildProfileActions(profile ?? CurrentProfile);
            AdjustSelectionForCurrentProfile();
        }

        internal string SerializeDirtyDrafts()
        {
            var documents = new JArray();
            for (int i = 0; i < _documentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documentStates[i];
                if (!state.IsDirty)
                    continue;

                documents.Add(new JObject
                {
                    ["kind"] = (int)state.Key.Kind,
                    ["sourcePath"] = state.Key.SourcePath,
                    ["baselineSha256"] = state.BaselineSha256,
                    ["revision"] = state.Revision,
                    ["content"] = JToken.Parse(state.SerializeCurrentState()),
                });
            }

            return new JObject
            {
                ["version"] = DirtyDraftPayloadVersion,
                ["selection"] = new JObject
                {
                    ["profileStableId"] = Selection.ProfileStableId,
                    ["actionSetStableId"] = Selection.ActionSetStableId,
                    ["actionLibraryId"] = Selection.ActionLibraryId,
                    ["actionId"] = Selection.ActionId,
                    ["timelineItemStableId"] = Selection.TimelineItemStableId,
                },
                ["documents"] = documents,
            }.ToString(Formatting.None);
        }

        internal bool RestoreDirtyDrafts(string json, out string[] conflictPaths)
        {
            conflictPaths = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                JObject root = JObject.Parse(json);
                if ((int?)root["version"] != DirtyDraftPayloadVersion
                    || !(root["selection"] is JObject selection)
                    || !TryParseDirtyDraftSelection(selection, out DirtyDraftSelection restoredSelection)
                    || !(root["documents"] is JArray documents))
                {
                    return false;
                }

                var drafts = new List<RestorableDirtyDraft>(documents.Count);
                var conflicts = new HashSet<string>(StringComparer.Ordinal);
                var seenKeys = new HashSet<CharacterActionAuthoringDocumentKey>();
                for (int i = 0; i < documents.Count; i++)
                {
                    if (!(documents[i] is JObject entry)
                        || !TryParseDirtyDraftEntry(entry, out RestorableDirtyDraft draft)
                        || !seenKeys.Add(draft.Key))
                    {
                        return false;
                    }

                    CharacterActionAuthoringDocumentState state = FindDocumentState(draft.Key);
                    if (state == null
                        || !string.Equals(
                            state.BaselineSha256,
                            draft.BaselineSha256,
                            StringComparison.Ordinal))
                    {
                        conflicts.Add(draft.Key.SourcePath);
                        continue;
                    }

                    draft = draft.WithState(state);
                    if (!CanRestoreDirtyDraft(draft))
                        return false;
                    drafts.Add(draft);
                }

                for (int i = 0; i < drafts.Count; i++)
                {
                    RestoreDirtyDraft(drafts[i]);
                    drafts[i].State.RestoreRevision(drafts[i].Revision);
                }

                RebuildProfileActions(CurrentProfile);
                RestoreStableSelection(restoredSelection);

                var orderedConflicts = new List<string>(conflicts);
                orderedConflicts.Sort(StringComparer.Ordinal);
                conflictPaths = orderedConflicts.ToArray();
                return true;
            }
            catch (JsonException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
            catch (InvalidDataException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
            catch (FormatException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
            catch (InvalidCastException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
            catch (OverflowException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
            catch (ArgumentException)
            {
                conflictPaths = Array.Empty<string>();
                return false;
            }
        }

        internal string[] SaveAllDirty()
        {
            var dirty = new List<CharacterActionAuthoringDocumentState>();
            for (int i = 0; i < _documentStates.Count; i++)
            {
                if (_documentStates[i].IsDirty)
                    dirty.Add(_documentStates[i]);
            }

            dirty.Sort(CompareSaveOrder);
            var savedPaths = new List<string>();
            for (int i = 0; i < dirty.Count; i++)
                savedPaths.AddRange(dirty[i].Save());
            return savedPaths.ToArray();
        }

        internal CharacterActionAuthoringExportResult Validate(
            CharacterActionAuthoringReferenceContext referenceContext,
            bool buildWorkstationSnapshots = false)
        {
            using IDisposable sourceKeyReadScope = CharacterContentIdentityAuthoringCatalog.BeginSourceKeyReadScope();
            CharacterActionSetJsonDto[] actionSets = GetAllActionSets();
            CharacterActionJsonDto[] actions = GetAllActions();
            CharacterReactionProfileJsonDto[] reactionProfiles = GetAllReactionProfiles();
            CharacterLandingProfileJsonDto[] landingProfiles = GetAllLandingProfiles();
            CharacterActionAuthoringReferenceContext validationContext =
                BuildCompositeReferenceContext(referenceContext, actionSets, actions);
            string actionSetsJson = JsonConvert.SerializeObject(
                new CharacterActionSetsJsonDocument
                {
                    SchemaVersion = ResolveProjectionSchemaVersion(),
                    ActionSets = actionSets,
                },
                CharacterActionAuthoringJsonService.JsonSettings);
            string actionsJson = JsonConvert.SerializeObject(
                new CharacterActionsJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                    Actions = actions,
                },
                CharacterActionAuthoringJsonService.JsonSettings);
            string reactionProfilesJson = JsonConvert.SerializeObject(
                new CharacterReactionProfilesJsonDocument
                {
                    SchemaVersion = ResolveProjectionSchemaVersion(),
                    ReactionProfiles = reactionProfiles,
                },
                CharacterActionAuthoringJsonService.JsonSettings);
            string landingProfilesJson = JsonConvert.SerializeObject(
                new CharacterLandingProfilesJsonDocument
                {
                    SchemaVersion = ResolveProjectionSchemaVersion(),
                    LandingProfiles = landingProfiles,
                },
                CharacterActionAuthoringJsonService.JsonSettings);
            return CharacterActionAuthoringJsonService.ValidateJson(
                actionSetsJson,
                actionsJson,
                reactionProfilesJson,
                landingProfilesJson,
                validationContext,
                buildWorkstationSnapshots);
        }

        private static CharacterActionAuthoringReferenceContext BuildCompositeReferenceContext(
            CharacterActionAuthoringReferenceContext referenceContext,
            CharacterActionSetJsonDto[] actionSets,
            CharacterActionJsonDto[] actions)
        {
            var result = CharacterActionAuthoringReferenceContext.CreateDefault();
            result.MergeFromReferenceContext(referenceContext);
            actionSets = actionSets ?? Array.Empty<CharacterActionSetJsonDto>();
            actions = actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < actionSets.Length; i++)
            {
                var model = new CharacterActionAuthoringModel(result)
                {
                    ActionSet = actionSets[i],
                };
                model.Actions.AddRange(actions);
                result.MergeFromModel(model);
            }
            result.Normalize();
            return result;
        }

        private static bool TryParseDirtyDraftEntry(
            JObject entry,
            out RestorableDirtyDraft draft)
        {
            draft = default;
            int? kindValue = (int?)entry["kind"];
            string sourcePath = (string)entry["sourcePath"];
            string baselineSha256 = (string)entry["baselineSha256"];
            long revision = (long?)entry["revision"] ?? 0;
            if (!kindValue.HasValue
                || !Enum.IsDefined(typeof(CharacterActionAuthoringDocumentKind), kindValue.Value)
                || string.IsNullOrWhiteSpace(sourcePath)
                || !IsSha256(baselineSha256)
                || revision < 0
                || !(entry["content"] is JObject content))
            {
                return false;
            }

            draft = new RestorableDirtyDraft(
                new CharacterActionAuthoringDocumentKey(
                    (CharacterActionAuthoringDocumentKind)kindValue.Value,
                    sourcePath),
                baselineSha256,
                revision,
                content.ToString(Formatting.None),
                null);
            return true;
        }

        private bool CanRestoreDirtyDraft(RestorableDirtyDraft draft)
        {
            switch (draft.Key.Kind)
            {
                case CharacterActionAuthoringDocumentKind.Profile:
                {
                    JObject profile = JObject.Parse(draft.ContentJson);
                    return draft.Key == ProfileDocument.State.Key
                        && profile["profiles"] is JArray
                        && TryDeserializeDraft<CharacterActionProfilesJsonDocument>(draft.ContentJson);
                }

                case CharacterActionAuthoringDocumentKind.ActionSetBundle:
                {
                    CharacterActionAuthoringActionSetBundleDocument bundle =
                        FindActionSetBundleDocument(draft.Key);
                    if (bundle == null)
                        return false;
                    JObject state = JObject.Parse(draft.ContentJson);
                    return state["actionSets"] is JObject actionSets
                        && state["reactionProfiles"] is JObject reactionProfiles
                        && actionSets["actionSets"] is JArray
                        && reactionProfiles["reactionProfiles"] is JArray
                        && TryDeserializeDraft<CharacterActionSetsJsonDocument>(
                            actionSets.ToString(Formatting.None))
                        && TryDeserializeDraft<CharacterReactionProfilesJsonDocument>(
                            reactionProfiles.ToString(Formatting.None));
                }

                case CharacterActionAuthoringDocumentKind.ActionLibrary:
                {
                    JObject library = JObject.Parse(draft.ContentJson);
                    return FindActionLibraryDocument(draft.Key) != null
                        && library["actions"] is JArray
                        && TryDeserializeDraft<CharacterActionsJsonDocument>(draft.ContentJson);
                }

                default:
                    return false;
            }
        }

        private void RestoreDirtyDraft(RestorableDirtyDraft draft)
        {
            switch (draft.Key.Kind)
            {
                case CharacterActionAuthoringDocumentKind.Profile:
                    ProfileDocument.RestoreDraft(draft.ContentJson);
                    break;

                case CharacterActionAuthoringDocumentKind.ActionSetBundle:
                    FindActionSetBundleDocument(draft.Key)?.RestoreDraft(draft.ContentJson);
                    break;

                case CharacterActionAuthoringDocumentKind.ActionLibrary:
                    FindActionLibraryDocument(draft.Key)?.RestoreDraft(draft.ContentJson);
                    break;
            }
        }

        private CharacterActionAuthoringActionSetBundleDocument FindActionSetBundleDocument(
            CharacterActionAuthoringDocumentKey key)
        {
            for (int i = 0; i < _actionSetBundles.Count; i++)
            {
                if (_actionSetBundles[i].State.Key == key)
                    return _actionSetBundles[i];
            }

            return null;
        }

        private CharacterActionAuthoringActionLibraryDocument FindActionLibraryDocument(
            CharacterActionAuthoringDocumentKey key)
        {
            for (int i = 0; i < _actionLibraries.Count; i++)
            {
                if (_actionLibraries[i].State.Key == key)
                    return _actionLibraries[i];
            }

            return null;
        }

        private static bool TryParseDirtyDraftSelection(
            JObject selection,
            out DirtyDraftSelection restoredSelection)
        {
            restoredSelection = default;
            if (!TryReadDraftSelectionString(selection, "profileStableId", out string profileStableId)
                || !TryReadDraftSelectionString(selection, "actionSetStableId", out string actionSetStableId)
                || !TryReadDraftSelectionString(selection, "actionLibraryId", out string actionLibraryId)
                || !TryReadDraftSelectionInt(selection, "actionId", out int actionId)
                || !TryReadDraftSelectionString(
                    selection,
                    "timelineItemStableId",
                    out string timelineItemStableId))
            {
                return false;
            }

            restoredSelection = new DirtyDraftSelection(
                profileStableId,
                actionSetStableId,
                actionLibraryId,
                actionId,
                timelineItemStableId);
            return true;
        }

        private static bool TryReadDraftSelectionString(
            JObject selection,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            JToken token = selection[propertyName];
            if (token == null || (token.Type != JTokenType.String && token.Type != JTokenType.Null))
                return false;
            value = (string)token ?? string.Empty;
            return true;
        }

        private static bool TryReadDraftSelectionInt(
            JObject selection,
            string propertyName,
            out int value)
        {
            value = 0;
            JToken token = selection[propertyName];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Null))
                return false;
            value = token.Type == JTokenType.Integer ? token.Value<int>() : 0;
            return value >= 0;
        }

        private void RestoreStableSelection(DirtyDraftSelection selection)
        {
            if (!SelectProfile(selection.ProfileStableId))
                InitializeSelection(selection.ProfileStableId);

            SelectActionSet(selection.ActionSetStableId);

            if (selection.ActionId > 0)
            {
                SelectAction(selection.ActionId);
            }
            else
            {
                SelectActionLibrary(selection.ActionLibraryId);
            }

            SelectTimelineItem(selection.TimelineItemStableId);
        }

        private static bool TryDeserializeDraft<TDocument>(string json)
            where TDocument : class
        {
            TDocument document = JsonConvert.DeserializeObject<TDocument>(
                json,
                CharacterActionAuthoringJsonService.JsonSettings);
            return document != null;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }

            return true;
        }

        private void LoadReferencedDocuments(
            CharacterActionAuthoringSourceEntry[] actionSetSources,
            CharacterActionAuthoringSourceEntry[] actionLibrarySources)
        {
            var referencedActionSetIds = new HashSet<string>(StringComparer.Ordinal);
            var referencedActionLibraryIds = new HashSet<string>(StringComparer.Ordinal);
            CharacterActionProfileJsonDto[] profiles = ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                AddIds(referencedActionSetIds, profiles[i]?.ActionSetIds);
                AddIds(referencedActionLibraryIds, profiles[i]?.ActionLibraryIds);
            }

            var resolvedActionSetIds = new HashSet<string>(StringComparer.Ordinal);
            var loadedBundlePaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actionSetSources.Length; i++)
            {
                CharacterActionAuthoringSourceEntry source = actionSetSources[i];
                if (source == null || !source.HasActionSet)
                    continue;

                try
                {
                    string actionSetsJson = File.ReadAllText(source.ActionSetsPath);
                    CharacterActionSetsJsonDocument preview = JsonConvert.DeserializeObject<CharacterActionSetsJsonDocument>(
                        actionSetsJson,
                        CharacterActionAuthoringJsonService.JsonSettings);
                    CharacterActionSetJsonDto[] actionSets = preview?.ActionSets
                        ?? Array.Empty<CharacterActionSetJsonDto>();
                    for (int j = 0; j < actionSets.Length; j++)
                    {
                        string stableId = actionSets[j]?.StableId ?? string.Empty;
                        if (referencedActionSetIds.Contains(stableId))
                            resolvedActionSetIds.Add(stableId);
                    }

                    // Keep catalog documents available even when the current Profile has not
                    // referenced them yet. Profile composition can then add membership and use
                    // the document immediately without a destructive source reload.
                    if (!loadedBundlePaths.Add(source.ActionSetsPath))
                        continue;

                    string reactionProfilesJson = source.HasReactionProfiles
                        ? File.ReadAllText(source.ReactionProfilesPath)
                        : CreateEmptyReactionProfilesJson(preview?.SchemaVersion);
                    string landingProfilesJson = source.HasLandingProfiles
                        ? File.ReadAllText(source.LandingProfilesPath)
                        : CreateEmptyLandingProfilesJson(preview?.SchemaVersion);
                    var bundle = new CharacterActionAuthoringActionSetBundleDocument(
                        source,
                        actionSetsJson,
                        reactionProfilesJson,
                        landingProfilesJson);
                    _actionSetBundles.Add(bundle);
                    _documentStates.Add(bundle.State);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[CharacterActionAuthoring] Failed to load action set bundle from '{source.DirectoryPath}': {ex.Message}");
                }
            }

            var resolvedActionLibraryIds = new HashSet<string>(StringComparer.Ordinal);
            var loadedLibraryPaths = new HashSet<string>(StringComparer.Ordinal);
            var loadedLibraryIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actionLibrarySources.Length; i++)
            {
                CharacterActionAuthoringSourceEntry source = actionLibrarySources[i];
                if (source == null || !source.HasActions)
                    continue;

                string libraryId = ResolveLibraryId(source, referencedActionLibraryIds);
                if (string.IsNullOrWhiteSpace(libraryId))
                    continue;
                if (loadedLibraryPaths.Contains(source.ActionsPath) || loadedLibraryIds.Contains(libraryId))
                    continue;

                try
                {
                    var library = new CharacterActionAuthoringActionLibraryDocument(
                        source,
                        libraryId,
                        File.ReadAllText(source.ActionsPath));

                    loadedLibraryPaths.Add(source.ActionsPath);
                    loadedLibraryIds.Add(libraryId);
                    if (referencedActionLibraryIds.Contains(libraryId))
                        resolvedActionLibraryIds.Add(libraryId);

                    _actionLibraries.Add(library);
                    _documentStates.Add(library.State);
                }
                catch (Exception ex)
                {
                    loadedLibraryPaths.Add(source.ActionsPath);
                    Debug.LogWarning(
                        $"[CharacterActionAuthoring] Failed to parse action library from '{source.ActionsPath}': {ex.Message}");
                }
            }

            _unresolvedActionSetIds = ExceptSorted(referencedActionSetIds, resolvedActionSetIds);
            _unresolvedActionLibraryIds = ExceptSorted(
                referencedActionLibraryIds,
                resolvedActionLibraryIds);
        }

        private void InitializeSelection(string selectedProfileStableId)
        {
            CharacterActionProfileJsonDto[] profiles = ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            CharacterActionProfileJsonDto selected = null;
            if (!string.IsNullOrWhiteSpace(selectedProfileStableId))
                selected = FindProfile(selectedProfileStableId);
            if (selected == null && profiles.Length > 0)
                selected = profiles[0];

            Selection.ProfileStableId = selected?.StableId ?? string.Empty;
            AdjustSelectionForCurrentProfile();
        }

        private void AdjustSelectionForCurrentProfile()
        {
            CharacterActionProfileJsonDto profile = CurrentProfile;
            if (profile == null)
            {
                Selection.ActionSetStableId = string.Empty;
                Selection.ActionLibraryId = string.Empty;
                Selection.ActionId = 0;
                _profileActions.Clear();
                return;
            }

            if (!Contains(profile.ActionSetIds, Selection.ActionSetStableId)
                || FindActionSet(Selection.ActionSetStableId) == null)
            {
                Selection.ActionSetStableId = FindInitialActionSetId(profile.ActionSetIds);
            }

            if (!Contains(profile.ActionLibraryIds, Selection.ActionLibraryId)
                || FindActionLibrary(Selection.ActionLibraryId) == null)
            {
                Selection.ActionLibraryId = FirstResolvedLibraryId(profile.ActionLibraryIds);
            }

            RebuildProfileActions(profile);
            CharacterActionAuthoringActionReference current = FindActionReference(Selection.ActionId);
            if (current == null)
            {
                CharacterActionAuthoringActionLibraryDocument library = CurrentActionLibrary;
                Selection.ActionId = FirstActionId(library);
            }
            else
            {
                Selection.ActionLibraryId = current.LibraryId;
            }
        }

        private void RebuildProfileActions(CharacterActionProfileJsonDto profile)
        {
            _profileActions.Clear();
            string[] libraryIds = profile?.ActionLibraryIds ?? Array.Empty<string>();
            for (int i = 0; i < libraryIds.Length; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = FindActionLibrary(libraryIds[i]);
                CharacterActionJsonDto[] actions = library?.Document.Actions
                    ?? Array.Empty<CharacterActionJsonDto>();
                for (int j = 0; j < actions.Length; j++)
                    _profileActions.Add(new CharacterActionAuthoringActionReference(library, actions[j]));
            }
        }

        private CharacterActionProfileJsonDto FindProfile(string stableId)
        {
            CharacterActionProfileJsonDto[] profiles = ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return profiles[i];
            }

            return null;
        }

        private CharacterActionSetJsonDto FindActionSet(string stableId)
        {
            CharacterActionAuthoringActionSetBundleDocument bundle = FindActionSetBundle(stableId);
            CharacterActionSetJsonDto[] actionSets = bundle?.ActionSetsDocument.ActionSets
                ?? Array.Empty<CharacterActionSetJsonDto>();
            for (int i = 0; i < actionSets.Length; i++)
            {
                if (string.Equals(actionSets[i]?.StableId, stableId, StringComparison.Ordinal))
                    return actionSets[i];
            }

            return null;
        }

        private CharacterActionAuthoringActionLibraryDocument FindActionLibrary(string libraryId)
        {
            for (int i = 0; i < _actionLibraries.Count; i++)
            {
                if (string.Equals(_actionLibraries[i].LibraryId, libraryId, StringComparison.Ordinal))
                    return _actionLibraries[i];
            }

            return null;
        }

        private CharacterActionAuthoringActionReference FindActionReference(int actionId)
        {
            for (int i = 0; i < _profileActions.Count; i++)
            {
                if (_profileActions[i].Action?.ActionId == actionId)
                    return _profileActions[i];
            }

            return null;
        }

        private CharacterActionSetJsonDto[] GetAllActionSets()
        {
            var result = new List<CharacterActionSetJsonDto>();
            HashSet<string> referencedIds = CollectReferencedActionSetIds();
            for (int i = 0; i < _actionSetBundles.Count; i++)
            {
                CharacterActionSetJsonDto[] actionSets =
                    _actionSetBundles[i].ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int j = 0; j < actionSets.Length; j++)
                {
                    if (referencedIds.Contains(actionSets[j]?.StableId ?? string.Empty))
                        result.Add(actionSets[j]);
                }
            }

            return result.ToArray();
        }

        private CharacterActionJsonDto[] GetAllActions()
        {
            var result = new List<CharacterActionJsonDto>();
            HashSet<string> referencedIds = CollectReferencedActionLibraryIds();
            for (int i = 0; i < _actionLibraries.Count; i++)
            {
                if (referencedIds.Contains(_actionLibraries[i].LibraryId))
                {
                    result.AddRange(
                        _actionLibraries[i].Document.Actions
                        ?? Array.Empty<CharacterActionJsonDto>());
                }
            }
            return result.ToArray();
        }

        private CharacterReactionProfileJsonDto[] GetAllReactionProfiles()
        {
            var result = new List<CharacterReactionProfileJsonDto>();
            HashSet<string> referencedIds = CollectReferencedActionSetIds();
            for (int i = 0; i < _actionSetBundles.Count; i++)
            {
                CharacterActionSetJsonDto[] actionSets =
                    _actionSetBundles[i].ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                bool referenced = false;
                for (int j = 0; j < actionSets.Length; j++)
                {
                    if (referencedIds.Contains(actionSets[j]?.StableId ?? string.Empty))
                    {
                        referenced = true;
                        break;
                    }
                }
                if (!referenced)
                    continue;
                result.AddRange(
                    _actionSetBundles[i].ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>());
            }

            return result.ToArray();
        }

        private CharacterLandingProfileJsonDto[] GetAllLandingProfiles()
        {
            var result = new List<CharacterLandingProfileJsonDto>();
            HashSet<string> referencedIds = CollectReferencedActionSetIds();
            for (int i = 0; i < _actionSetBundles.Count; i++)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle = _actionSetBundles[i];
                CharacterActionSetJsonDto[] actionSets = bundle.ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                bool referenced = false;
                for (int j = 0; j < actionSets.Length; j++)
                {
                    if (referencedIds.Contains(actionSets[j]?.StableId ?? string.Empty))
                    {
                        referenced = true;
                        break;
                    }
                }
                if (!referenced)
                    continue;

                result.AddRange(bundle.LandingProfilesDocument.LandingProfiles
                    ?? Array.Empty<CharacterLandingProfileJsonDto>());
            }
            return result.ToArray();
        }

        private HashSet<string> CollectReferencedActionSetIds()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            CharacterActionProfileJsonDto[] profiles = ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
                AddIds(result, profiles[i]?.ActionSetIds);
            return result;
        }

        private HashSet<string> CollectReferencedActionLibraryIds()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            CharacterActionProfileJsonDto[] profiles = ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
                AddIds(result, profiles[i]?.ActionLibraryIds);
            return result;
        }

        private string ResolveProjectionSchemaVersion()
        {
            if (!string.IsNullOrWhiteSpace(ProfileDocument.Document.SchemaVersion))
                return ProfileDocument.Document.SchemaVersion;
            return CharacterActionJsonImporter.CurrentProjectionSchemaVersion;
        }

        private string FindInitialActionSetId(string[] ids)
        {
            ids = ids ?? Array.Empty<string>();
            string firstResolved = string.Empty;
            using (CharacterActionPreviewBindingDiscovery.BeginReadScope())
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    CharacterActionSetJsonDto actionSet = FindActionSet(ids[i]);
                    if (actionSet == null)
                        continue;
                    if (string.IsNullOrEmpty(firstResolved))
                        firstResolved = ids[i] ?? string.Empty;

                    // Profile order is not a preview default: an unarmed variant may
                    // precede the character's configured default equipment.
                    if (!string.IsNullOrWhiteSpace(actionSet.CharacterStableId)
                        && (string.IsNullOrWhiteSpace(actionSet.EquipmentStateStableId)
                            || (CharacterActionPreviewBindingDiscovery.TryGetDefaultEquipmentStateStableId(
                                    actionSet.CharacterStableId, out string defaultStateId)
                                && string.Equals(actionSet.EquipmentStateStableId, defaultStateId, StringComparison.Ordinal))))
                    {
                        return ids[i] ?? string.Empty;
                    }
                }
            }

            return firstResolved;
        }

        private string FirstResolvedLibraryId(string[] ids)
        {
            ids = ids ?? Array.Empty<string>();
            string firstResolved = string.Empty;
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = FindActionLibrary(ids[i]);
                if (library == null)
                    continue;
                if (string.IsNullOrEmpty(firstResolved))
                    firstResolved = ids[i] ?? string.Empty;
                if ((library.Document.Actions?.Length ?? 0) > 0)
                    return ids[i] ?? string.Empty;
            }

            return firstResolved;
        }

        private static bool ContainsAction(
            CharacterActionAuthoringActionLibraryDocument library,
            int actionId)
        {
            CharacterActionJsonDto[] actions = library?.Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i]?.ActionId == actionId)
                    return true;
            }

            return false;
        }

        private static int FirstActionId(CharacterActionAuthoringActionLibraryDocument library)
        {
            CharacterActionJsonDto[] actions = library?.Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            return actions.Length == 0 ? 0 : actions[0]?.ActionId ?? 0;
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

        private static void AddIds(HashSet<string> destination, string[] values)
        {
            values = values ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    destination.Add(values[i]);
            }
        }

        private static string ResolveLibraryId(
            CharacterActionAuthoringSourceEntry source,
            HashSet<string> referencedIds)
        {
            if (referencedIds.Contains(source.DisplayName))
                return source.DisplayName;

            string leaf = CharacterActionAuthoringSourceEntry.GetLeafDirectoryName(source.DirectoryPath);
            if (referencedIds.Contains(leaf))
                return leaf;
            return !string.IsNullOrWhiteSpace(source.DisplayName)
                ? source.DisplayName
                : leaf;
        }

        private static string[] ExceptSorted(HashSet<string> expected, HashSet<string> actual)
        {
            var result = new List<string>();
            foreach (string value in expected)
            {
                if (!actual.Contains(value))
                    result.Add(value);
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static string CreateEmptyReactionProfilesJson(string schemaVersion)
        {
            return JsonConvert.SerializeObject(
                new CharacterReactionProfilesJsonDocument
                {
                    SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion)
                        ? CharacterActionJsonImporter.CurrentProjectionSchemaVersion
                        : schemaVersion,
                    ReactionProfiles = Array.Empty<CharacterReactionProfileJsonDto>(),
                },
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private static string CreateEmptyLandingProfilesJson(string schemaVersion)
        {
            return JsonConvert.SerializeObject(
                new CharacterLandingProfilesJsonDocument
                {
                    SchemaVersion = string.IsNullOrWhiteSpace(schemaVersion)
                        ? CharacterActionJsonImporter.CurrentProjectionSchemaVersion
                        : schemaVersion,
                    LandingProfiles = Array.Empty<CharacterLandingProfileJsonDto>(),
                },
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private static int CompareSaveOrder(
            CharacterActionAuthoringDocumentState left,
            CharacterActionAuthoringDocumentState right)
        {
            int compare = SaveRank(left.Key.Kind).CompareTo(SaveRank(right.Key.Kind));
            return compare != 0
                ? compare
                : string.CompareOrdinal(left.Key.SourcePath, right.Key.SourcePath);
        }

        private static int SaveRank(CharacterActionAuthoringDocumentKind kind)
        {
            switch (kind)
            {
                case CharacterActionAuthoringDocumentKind.ActionLibrary:
                    return 0;
                case CharacterActionAuthoringDocumentKind.ActionSetBundle:
                    return 1;
                case CharacterActionAuthoringDocumentKind.Projectile:
                    return 2;
                case CharacterActionAuthoringDocumentKind.MechanicTuning:
                    return 3;
                case CharacterActionAuthoringDocumentKind.Profile:
                    return 4;
                case CharacterActionAuthoringDocumentKind.AbilityEffectTimeline:
                    return 5;
                default:
                    return 6;
            }
        }

        private readonly struct RestorableDirtyDraft
        {
            internal RestorableDirtyDraft(
                CharacterActionAuthoringDocumentKey key,
                string baselineSha256,
                long revision,
                string contentJson,
                CharacterActionAuthoringDocumentState state)
            {
                Key = key;
                BaselineSha256 = baselineSha256 ?? string.Empty;
                Revision = revision;
                ContentJson = contentJson ?? string.Empty;
                State = state;
            }

            internal CharacterActionAuthoringDocumentKey Key { get; }
            internal string BaselineSha256 { get; }
            internal long Revision { get; }
            internal string ContentJson { get; }
            internal CharacterActionAuthoringDocumentState State { get; }

            internal RestorableDirtyDraft WithState(CharacterActionAuthoringDocumentState state)
            {
                return new RestorableDirtyDraft(
                    Key,
                    BaselineSha256,
                    Revision,
                    ContentJson,
                    state);
            }
        }

        private readonly struct DirtyDraftSelection
        {
            internal DirtyDraftSelection(
                string profileStableId,
                string actionSetStableId,
                string actionLibraryId,
                int actionId,
                string timelineItemStableId)
            {
                ProfileStableId = profileStableId ?? string.Empty;
                ActionSetStableId = actionSetStableId ?? string.Empty;
                ActionLibraryId = actionLibraryId ?? string.Empty;
                ActionId = actionId;
                TimelineItemStableId = timelineItemStableId ?? string.Empty;
            }

            internal string ProfileStableId { get; }
            internal string ActionSetStableId { get; }
            internal string ActionLibraryId { get; }
            internal int ActionId { get; }
            internal string TimelineItemStableId { get; }
        }
    }

    internal sealed class CharacterActionAuthoringJsonArrayBacking<TItem>
        where TItem : class
    {
        private readonly string _arrayPropertyName;
        private readonly Func<string> _schemaVersion;
        private readonly Func<TItem[]> _items;
        private readonly Func<TItem, string> _stableId;
        private readonly Dictionary<string, BaselineItem> _baselineItems =
            new Dictionary<string, BaselineItem>(StringComparer.Ordinal);
        private JObject _baselineRoot;

        internal CharacterActionAuthoringJsonArrayBacking(
            string json,
            string arrayPropertyName,
            Func<string> schemaVersion,
            Func<TItem[]> items,
            Func<TItem, string> stableId)
        {
            _arrayPropertyName = arrayPropertyName ?? throw new ArgumentNullException(nameof(arrayPropertyName));
            _schemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _stableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
            AcceptSerialized(json);
        }

        internal string Serialize()
        {
            JObject root = (JObject)_baselineRoot.DeepClone();
            string schemaVersion = _schemaVersion() ?? string.Empty;
            if (root["schemaVersion"] != null || schemaVersion.Length > 0)
                root["schemaVersion"] = schemaVersion;

            TItem[] items = _items() ?? Array.Empty<TItem>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var array = new JArray();
            for (int i = 0; i < items.Length; i++)
            {
                TItem item = items[i];
                string key = BuildOccurrenceKey(_stableId(item), occurrences);
                JToken currentToken = ToToken(item);
                string currentCanonical = CharacterActionAuthoringDocumentState.CanonicalizeJson(
                    currentToken.ToString(Formatting.None));
                if (_baselineItems.TryGetValue(key, out BaselineItem baseline))
                {
                    array.Add(string.Equals(
                        currentCanonical,
                        baseline.Canonical,
                        StringComparison.Ordinal)
                        ? baseline.RawToken.DeepClone()
                        : MergeKnownProperties(baseline.RawToken, currentToken));
                }
                else
                {
                    array.Add(currentToken);
                }
            }

            root[_arrayPropertyName] = array;
            return root.ToString(Formatting.Indented);
        }

        internal void AcceptSerialized(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Character Action JSON is empty for " + _arrayPropertyName + ".");

            JToken parsed;
            try
            {
                parsed = JToken.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "Failed to parse Character Action JSON for " + _arrayPropertyName + ".",
                    ex);
            }

            _baselineRoot = parsed as JObject
                ?? throw new InvalidDataException("Character Action JSON root must be an object.");
            CaptureBaselineItems();
        }

        private void CaptureBaselineItems()
        {
            _baselineItems.Clear();
            JArray rawItems = _baselineRoot[_arrayPropertyName] as JArray ?? new JArray();
            TItem[] items = _items() ?? Array.Empty<TItem>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < items.Length; i++)
            {
                TItem item = items[i];
                string key = BuildOccurrenceKey(_stableId(item), occurrences);
                JToken rawToken = i < rawItems.Count ? rawItems[i] : ToToken(item);
                _baselineItems[key] = new BaselineItem(
                    rawToken.DeepClone(),
                    CharacterActionAuthoringDocumentState.CanonicalizeJson(
                        ToToken(item).ToString(Formatting.None)));
            }
        }

        private static string BuildOccurrenceKey(
            string stableId,
            Dictionary<string, int> occurrences)
        {
            stableId = stableId ?? string.Empty;
            occurrences.TryGetValue(stableId, out int occurrence);
            occurrences[stableId] = occurrence + 1;
            return stableId + "\u001f" + occurrence;
        }

        private static JToken ToToken(TItem item)
        {
            if (item == null)
                return JValue.CreateNull();

            JsonSerializer serializer = JsonSerializer.Create(
                CharacterActionAuthoringJsonService.JsonSettings);
            return JToken.FromObject(item, serializer);
        }

        private static JToken MergeKnownProperties(JToken baseline, JToken current)
        {
            if (!(baseline is JObject baselineObject) || !(current is JObject currentObject))
                return current;

            var merged = (JObject)baselineObject.DeepClone();
            foreach (JProperty property in currentObject.Properties())
                merged[property.Name] = property.Value.DeepClone();
            return merged;
        }

        private readonly struct BaselineItem
        {
            internal BaselineItem(JToken rawToken, string canonical)
            {
                RawToken = rawToken;
                Canonical = canonical ?? string.Empty;
            }

            internal JToken RawToken { get; }
            internal string Canonical { get; }
        }
    }
}
