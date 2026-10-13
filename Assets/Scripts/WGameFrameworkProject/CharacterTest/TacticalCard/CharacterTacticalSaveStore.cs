using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using MxFramework.Platform;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>One durable gameplay slot, using the framework envelope, CAS and tombstone contracts.</summary>
    internal sealed class CharacterTacticalSaveStore : ICharacterTacticalStateStore
    {
        private static readonly SaveGameProfileId Profile = new SaveGameProfileId("character-test");
        private readonly SaveGameSlotId _slot;
        private readonly SaveGameBuildDescriptor _build;
        private readonly string _codecId;
        private readonly ISaveGameStore _store;
        private SaveGameGeneration _head;

        private CharacterTacticalSaveStore(ISaveGameStore store, string slotId, string codecId)
        {
            _store = store;
            _slot = new SaveGameSlotId(slotId);
            _codecId = codecId;
            _build = new SaveGameBuildDescriptor("1", "character-test", "tactical-v1", "references-v1", slotId);
        }

        internal static Task<CharacterTacticalSaveStore> CreateAsync(string root) => Task.Run(() =>
        {
            var location = PlatformLocation.TryCreate(PlatformLocationKind.FileSystemPath, root, true, true, true);
            if (!location.Success) throw new InvalidOperationException(location.Error.ToString());
            var factory = FileSystemSaveGameStoreFactory.TryCreate(location.Location);
            if (!factory.Success) throw new InvalidOperationException(factory.Error.ToString());
            return new CharacterTacticalSaveStore(factory.Factory.CreateStore(), "tactical-player", "character-tactical-json-v1");
        });

        internal static Task<CharacterTacticalSaveStore> CreateAsync(string root, string slotId, string codecId) => Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(codecId))
                throw new ArgumentException("Save slot and codec are required.");
            var location = PlatformLocation.TryCreate(PlatformLocationKind.FileSystemPath, root, true, true, true);
            if (!location.Success) throw new InvalidOperationException(location.Error.ToString());
            var factory = FileSystemSaveGameStoreFactory.TryCreate(location.Location);
            if (!factory.Success) throw new InvalidOperationException(factory.Error.ToString());
            return new CharacterTacticalSaveStore(factory.Factory.CreateStore(), slotId, codecId);
        });

        public Task<string> LoadAsync() => Task.Run(() =>
        {
            var operation = Complete(_store.Read(new SaveGameReadRequest(Profile, _slot, SaveGameReadSelection.Current,
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood, includeDeleted: true)));
            if (!operation.TryGetResult(out var read))
            {
                if (operation.Error.Code != SaveGameErrorCode.SlotNotFound)
                    throw new InvalidOperationException(operation.Error.ToString());
                _head = SaveGameGeneration.None;
                return null;
            }
            var decoded = SaveGameEnvelopeCodec.Decode(read.Envelope);
            if (!decoded.IsSuccess) throw new InvalidOperationException(decoded.Error.ToString());
            _head = read.OriginalGeneration;
            if (decoded.Envelope.RecordKind == SaveGameRecordKind.Tombstone) return null;
            if (decoded.Envelope.CodecId != _codecId) throw new InvalidOperationException("Unknown tactical save codec.");
            return Encoding.UTF8.GetString(decoded.Envelope.CopyPayload());
        });

        public Task SaveAsync(string json) => Task.Run(() => Save(json));

        private void Save(string json)
        {
            var next = new SaveGameGeneration(checked(_head.Value + 1));
            var now = DateTime.UtcNow;
            var envelope = SaveGameEnvelope.CreateSave(Profile, _slot, next, now, now, _build,
                Array.Empty<SaveGameKeyValue>(), _codecId, 1, 0,
                Array.Empty<SaveGameKeyValue>(), Encoding.UTF8.GetBytes(json));
            var encoded = SaveGameEnvelopeCodec.Encode(envelope);
            if (!encoded.IsSuccess) throw new InvalidOperationException(encoded.Error.ToString());
            var result = Finish(_store.Commit(new SaveGameCommitRequest(Profile, _slot, _head, encoded.Encoded)));
            _head = result.Generation;
        }

        public Task ResetAsync(string json) => Task.Run(() =>
        {
            if (_head.IsPublished)
            {
                var deleted = Finish(_store.Delete(new SaveGameDeleteRequest(Profile, _slot, _head, DateTime.UtcNow, _build)));
                _head = deleted.Generation;
            }
            Save(json);
        });

        // Provider Poll publishes completion only on the thread that created the operation.
        // Keep creation and polling on one background worker, without blocking the Unity thread.
        private static T Finish<T>(ISaveGameOperation<T> operation)
        {
            Complete(operation);
            if (!operation.TryGetResult(out var result)) throw new InvalidOperationException(operation.Error.ToString());
            return result;
        }

        private static ISaveGameOperation<T> Complete<T>(ISaveGameOperation<T> operation)
        {
            while (!operation.IsDone) { operation.Poll(); if (!operation.IsDone) System.Threading.Thread.Sleep(1); }
            return operation;
        }
    }

    // Player data, preparation and completed outcomes share one framework envelope/CAS publication.
    // Legacy slots are inputs only. A failed commit never changes the published document.
    internal sealed class CharacterTacticalProgressDocument
    {
        [JsonProperty(Required = Required.Always)]
        public string Schema { get; set; } = "mx.character-test.tactical-slice-progress.v1";
        public string ActiveProfileId { get; set; } = string.Empty;
        public List<CharacterTacticalProgressProfile> Profiles { get; set; } = new();
    }

    internal sealed class CharacterTacticalProgressProfile
    {
        public string ProfileId { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string PlayerJson { get; set; }
        public string PreparationJson { get; set; }
        public List<CharacterTacticalBattleSettlement> Settlements { get; set; } = new();
    }

    internal sealed class CharacterTacticalBattleSettlement
    {
        public string BattleId { get; set; } = string.Empty;
        public int WinningTeamId { get; set; }
        public int PlayerTeamId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CharacterTacticalRewardBundle Rewards { get; set; }
        public bool Draw { get; set; }
        public string CompletedAtUtc { get; set; } = string.Empty;
    }

    internal sealed class CharacterTacticalProgressStore
    {
        private readonly ICharacterTacticalStateStore _store;
        private readonly SemaphoreSlim _commits = new(1, 1);
        private CharacterTacticalProgressDocument _published;
        private bool _writeConflict;
        internal bool HasCheckpoint => _published.Profiles.Count != 0;
        internal string ActiveProfileId => _published.ActiveProfileId;
        internal CharacterTacticalProgressProfile[] ListProfiles()
            => _published.Profiles.Select(profile => new CharacterTacticalProgressProfile {
                ProfileId = profile.ProfileId, CreatedAtUtc = profile.CreatedAtUtc
            }).ToArray();
        internal CharacterTacticalProgressStore(ICharacterTacticalStateStore store, string json)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _published = json == null ? new CharacterTacticalProgressDocument()
                : JsonConvert.DeserializeObject<CharacterTacticalProgressDocument>(json);
            if (_published == null || (_published.Schema != "mx.character-test.tactical-slice-progress.v1"
                && _published.Schema != CharacterTacticalRewardRules.ProgressSchema) || _published.Profiles == null
                || _published.Profiles.Any(profile => profile == null || string.IsNullOrWhiteSpace(profile.ProfileId)
                    || profile.Settlements == null || profile.Settlements.Any(result => result == null || string.IsNullOrWhiteSpace(result.BattleId))
                    || profile.Settlements.Select(result => result.BattleId).Distinct(StringComparer.Ordinal).Count() != profile.Settlements.Count
                    || string.IsNullOrWhiteSpace(profile.PlayerJson)
                    || string.IsNullOrWhiteSpace(profile.PreparationJson))
                || _published.Profiles.Select(profile => profile.ProfileId).Distinct(StringComparer.Ordinal).Count() != _published.Profiles.Count
                || (_published.Profiles.Count == 0 && !string.IsNullOrEmpty(_published.ActiveProfileId))
                || (_published.Profiles.Count != 0 && !_published.Profiles.Any(profile => profile.ProfileId == _published.ActiveProfileId)))
                throw new JsonSerializationException("TACTICAL_SLICE_CHECKPOINT_INVALID");
            foreach (var profile in _published.Profiles)
            {
                if (profile.Revision < 0) throw new JsonSerializationException("TACTICAL_PROFILE_REVISION_INVALID");
                foreach (var result in profile.Settlements)
                {
                    if (result.Rewards != null && _published.Schema != CharacterTacticalRewardRules.ProgressSchema)
                        throw new JsonSerializationException("TACTICAL_REWARDS_REQUIRE_PROGRESS_V2");
                    CharacterTacticalRewardRules.Validate(result);
                }
            }
        }

        internal CharacterTacticalProgressCandidate OpenActive()
        {
            CharacterTacticalProgressProfile profile = _published.Profiles.FirstOrDefault(value => value.ProfileId == _published.ActiveProfileId);
            return profile == null ? null : new CharacterTacticalProgressCandidate(this, Clone(profile));
        }

        internal CharacterTacticalProgressCandidate OpenProfile(string profileId)
        {
            var profile = _published.Profiles.FirstOrDefault(value => value.ProfileId == profileId);
            if (profile == null) throw new InvalidOperationException("所选进度已不存在，请重新选择。");
            return new CharacterTacticalProgressCandidate(this, Clone(profile));
        }

        internal CharacterTacticalProgressCandidate Create(string playerJson = null, string preparationJson = null)
            => new(this, new CharacterTacticalProgressProfile
            {
                ProfileId = Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                PlayerJson = playerJson, PreparationJson = preparationJson
            });

        internal async Task CommitAsync(CharacterTacticalProgressProfile snapshot, bool activate, CharacterTacticalProgressProfile archive = null)
        {
            await _commits.WaitAsync();
            try
            {
                if (_writeConflict) throw new InvalidOperationException("进度已被其他写入者变更，请重新打开进度；未覆盖磁盘内容。");
                var candidate = Clone(_published);
                var current = candidate.Profiles.Find(value => value.ProfileId == snapshot.ProfileId);
                if (snapshot.Revision != (current?.Revision ?? 0)) throw new InvalidOperationException("TACTICAL_PROFILE_STALE_WRITER");
                long nextRevision = checked(snapshot.Revision + 1);
                var stagedSnapshot = Clone(snapshot); stagedSnapshot.Revision = nextRevision;
                if (archive != null)
                {
                    if (archive.ProfileId == snapshot.ProfileId) throw new InvalidOperationException("TACTICAL_PROFILE_ARCHIVE_ID_CONFLICT");
                    int previous = candidate.Profiles.FindIndex(value => value.ProfileId == archive.ProfileId);
                    if (archive.Revision != (previous >= 0 ? candidate.Profiles[previous].Revision : 0))
                        throw new InvalidOperationException("TACTICAL_ARCHIVE_STALE_WRITER");
                    var stagedArchive = Clone(archive); stagedArchive.Revision = checked(archive.Revision + 1);
                    if (previous >= 0) candidate.Profiles[previous] = stagedArchive;
                    else candidate.Profiles.Add(stagedArchive);
                }
                int index = candidate.Profiles.FindIndex(value => value.ProfileId == snapshot.ProfileId);
                if (index >= 0) candidate.Profiles[index] = stagedSnapshot;
                else candidate.Profiles.Add(stagedSnapshot);
                if (candidate.Profiles.Any(value => value.Settlements.Any(result => result.Rewards != null)))
                    candidate.Schema = CharacterTacticalRewardRules.ProgressSchema;
                if (activate || string.IsNullOrEmpty(candidate.ActiveProfileId)) candidate.ActiveProfileId = snapshot.ProfileId;
                string json = JsonConvert.SerializeObject(candidate);
                try { await _store.SaveAsync(json); }
                catch
                {
                    // A lost completion receipt may follow a durable file commit. Read back before retrying.
                    // If another writer won, never update the CAS head and silently overwrite its checkpoint.
                    string recovered;
                    try { recovered = await _store.LoadAsync(); }
                    catch { _writeConflict = true; throw; }
                    Newtonsoft.Json.Linq.JToken disk;
                    try { disk = recovered == null ? null : Newtonsoft.Json.Linq.JToken.Parse(recovered); }
                    catch { _writeConflict = true; throw; }
                    if (!Newtonsoft.Json.Linq.JToken.DeepEquals(disk, Newtonsoft.Json.Linq.JToken.Parse(json)))
                    {
                        if (!(disk == null && _published.Profiles.Count == 0)
                            && !Newtonsoft.Json.Linq.JToken.DeepEquals(disk, Newtonsoft.Json.Linq.JToken.Parse(JsonConvert.SerializeObject(_published))))
                            _writeConflict = true;
                        throw;
                    }
                }
                _published = candidate;
                snapshot.Revision = nextRevision;
            }
            finally { _commits.Release(); }
        }

        private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }

    internal sealed partial class CharacterTacticalProgressCandidate
    {
        private readonly CharacterTacticalProgressStore _owner;
        private CharacterTacticalProgressProfile _profile;
        private Func<string> _capturePlayer, _capturePreparation;
        private bool _active;
        private readonly SemaphoreSlim _writes = new(1, 1);
        internal CharacterTacticalProgressCandidate(CharacterTacticalProgressStore owner, CharacterTacticalProgressProfile profile)
        {
            _owner = owner; _profile = profile;
            HasSourceProgress = profile.PlayerJson != null || profile.PreparationJson != null;
            PlayerStore = new Section(this, true); PreparationStore = new Section(this, false);
        }
        internal bool HasSourceProgress { get; }
        internal string ProfileId => _profile.ProfileId;
        internal ICharacterTacticalStateStore PlayerStore { get; }
        internal ICharacterTacticalStateStore PreparationStore { get; }
        internal void Bind(Func<string> capturePlayer, Func<string> capturePreparation)
        { _capturePlayer = capturePlayer ?? throw new ArgumentNullException(nameof(capturePlayer)); _capturePreparation = capturePreparation ?? throw new ArgumentNullException(nameof(capturePreparation)); }

        internal CharacterTacticalProgressProfile CaptureSnapshot()
        {
            if (_capturePlayer == null || _capturePreparation == null) throw new InvalidOperationException("TACTICAL_PROGRESS_CANDIDATE_NOT_READY");
            var snapshot = JsonConvert.DeserializeObject<CharacterTacticalProgressProfile>(JsonConvert.SerializeObject(_profile));
            snapshot.PlayerJson = _capturePlayer(); snapshot.PreparationJson = _capturePreparation();
            return snapshot;
        }

        internal async Task SaveAsync(bool activate = false, CharacterTacticalBattleSettlement settlement = null,
            CharacterTacticalProgressProfile archive = null)
        {
            if (_capturePlayer == null || _capturePreparation == null) throw new InvalidOperationException("TACTICAL_PROGRESS_CANDIDATE_NOT_READY");
            // Capture after the serialized write gate, on the application thread; queued saves must observe durable reward publication.
            settlement = settlement == null ? null : JsonConvert.DeserializeObject<CharacterTacticalBattleSettlement>(JsonConvert.SerializeObject(settlement));
            archive = archive == null ? null : JsonConvert.DeserializeObject<CharacterTacticalProgressProfile>(JsonConvert.SerializeObject(archive));
            await _writes.WaitAsync();
            try
            {
                var snapshot = JsonConvert.DeserializeObject<CharacterTacticalProgressProfile>(JsonConvert.SerializeObject(_profile));
                snapshot.PlayerJson = _capturePlayer(); snapshot.PreparationJson = _capturePreparation();
                if (string.IsNullOrWhiteSpace(snapshot.PlayerJson) || string.IsNullOrWhiteSpace(snapshot.PreparationJson))
                    throw new InvalidOperationException("TACTICAL_PROGRESS_CAPTURE_INVALID");
                if (settlement != null)
                {
                    CharacterTacticalRewardRules.Merge(snapshot.Settlements, settlement);
                }
                await _owner.CommitAsync(snapshot, activate, archive);
                _profile = snapshot;
                _active |= activate;
            }
            finally { _writes.Release(); }
        }

        internal void EnableAutosave() => _active = true;
        internal void Retire() => _active = false;

        private sealed class Section : ICharacterTacticalStateStore
        {
            private readonly CharacterTacticalProgressCandidate _candidate;
            private readonly bool _player;
            internal Section(CharacterTacticalProgressCandidate candidate, bool player) { _candidate = candidate; _player = player; }
            public Task<string> LoadAsync() => Task.FromResult(_player ? _candidate._profile.PlayerJson : _candidate._profile.PreparationJson);
            public Task SaveAsync(string json)
            {
                if (_candidate._capturePlayer == null)
                {
                    if (_player) _candidate._profile.PlayerJson = json;
                    else _candidate._profile.PreparationJson = json;
                    return Task.CompletedTask;
                }
                return _candidate._active ? _candidate.SaveAsync() : Task.CompletedTask;
            }
            // Resetting this profile is one whole-checkpoint commit; it never deletes a legacy slot.
            public async Task ResetAsync(string json)
            {
                await _candidate._writes.WaitAsync();
                try
                {
                    if (_player && _candidate._profile.Settlements.Any(value => value.Rewards != null))
                        throw new InvalidOperationException("含领奖记录的进度请创建独立新进度；未清空原记录。");
                    var snapshot = JsonConvert.DeserializeObject<CharacterTacticalProgressProfile>(JsonConvert.SerializeObject(_candidate._profile));
                    snapshot.PlayerJson = _player ? json : _candidate._capturePlayer();
                    snapshot.PreparationJson = _player ? _candidate._capturePreparation() : json;
                    await _candidate._owner.CommitAsync(snapshot, false);
                    _candidate._profile = snapshot;
                }
                finally { _candidate._writes.Release(); }
            }
        }
    }
}
