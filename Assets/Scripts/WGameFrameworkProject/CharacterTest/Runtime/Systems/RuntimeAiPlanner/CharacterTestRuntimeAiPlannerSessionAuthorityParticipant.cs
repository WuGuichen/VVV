using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityParticipant :
        INetworkSnapshotParticipant,
        INetworkRollbackParticipant
    {
        public const string ParticipantIdValue = "mxframework.character-test.runtime-ai-planner.session-authority";
        public const int SchemaVersionValue = 2;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog _catalog;
        private readonly int _runtimeContractVersion;
        private CharacterTestRuntimeAiPlannerSessionDeckLock[] _sessionLocks =
            Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>();
        private IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> _readOnlySessionLocks =
            Array.AsReadOnly(Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>());

        public CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (runtimeContractVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(runtimeContractVersion));
            _runtimeContractVersion = runtimeContractVersion;
        }

        public string ParticipantId => ParticipantIdValue;
        public bool IsLocked => _sessionLocks.Length > 0;
        public CharacterTestRuntimeAiPlannerSessionDeckLock SessionLock =>
            _sessionLocks.Length == 0 ? null : _sessionLocks[0];
        public IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> SessionLocks =>
            _readOnlySessionLocks;
        public long AuthorityHash => ComputeAuthorityHash(_sessionLocks);

        internal CharacterTestRuntimeAiPlannerSessionAuthorityParticipant CreateReplayReplica()
        {
            return new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(
                _catalog,
                _runtimeContractVersion);
        }

        /// <remarks>LowFreqAlloc: freezes replay metadata when recording starts or validation completes.</remarks>
        internal CharacterTestReplaySessionAuthorityIdentity CaptureReplayIdentity()
        {
            return new CharacterTestReplaySessionAuthorityIdentity(
                _catalog.CatalogId,
                _catalog.Revision,
                _catalog.CatalogNumericId,
                _catalog.NumericIdentityHash,
                _catalog.CatalogHash,
                _runtimeContractVersion,
                AuthorityHash,
                _sessionLocks);
        }

        public bool TryGetSessionLock(
            string characterStableId,
            out CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock)
        {
            int index = FindLockIndex(characterStableId);
            sessionLock = index >= 0 ? _sessionLocks[index] : null;
            return sessionLock != null;
        }

        /// <remarks>LowFreqAlloc: selection-phase validation and commit only.</remarks>
        public CharacterTestRuntimeAiPlannerSessionAuthorityResult TryLock(
            CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            CharacterTestRuntimeAiPlannerSessionAuthorityContext context)
        {
            if (request != null && FindLockIndex(request.CharacterStableId) >= 0)
            {
                return Failed(
                    "SESSION_DECK_SELECTION_LOCKED",
                    "$.characterStableId",
                    "Behavior deck selection is already locked for this character.");
            }
            if (context == null)
            {
                return Failed(
                    "SESSION_DECK_AUTHORITY_CONTEXT_MISSING",
                    "$.authority",
                    "Session authority context is required.");
            }
            if (!ReferenceEquals(context.Catalog, _catalog)
                || context.RuntimeContractVersion != _runtimeContractVersion)
            {
                return Failed(
                    "SESSION_DECK_PARTICIPANT_CONTEXT_MISMATCH",
                    "$.authority",
                    "Session authority context does not match participant catalog/runtime contract.");
            }

            CharacterTestRuntimeAiPlannerSessionAuthorityResult result =
                CharacterTestRuntimeAiPlannerSessionAuthority.ValidateAndLock(request, context);
            if (result.Success)
                AddLock(result.SessionLock);
            return result;
        }

        public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
        {
            SnapshotDocument document = SnapshotDocument.FromLocks(_sessionLocks);
            return new RuntimeCustomState(
                RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority,
                ParticipantId,
                SchemaVersionValue,
                JsonConvert.SerializeObject(document, JsonSettings));
        }

        public NetworkSnapshotApplyResult ApplySnapshot(
            RuntimeFrame frame,
            RuntimeCustomState snapshot)
        {
            if (!TryRestoreSnapshot(
                snapshot,
                out CharacterTestRuntimeAiPlannerSessionDeckLock[] restored,
                out string failure))
                return NetworkSnapshotApplyResult.Failed(failure);
            if (!ContainsExistingLocks(restored, out string monotonicFailure))
                return NetworkSnapshotApplyResult.Failed(monotonicFailure);
            SetLocks(restored);
            return NetworkSnapshotApplyResult.Succeeded();
        }

        internal NetworkSnapshotApplyResult ValidateSnapshot(RuntimeCustomState snapshot)
        {
            if (!TryRestoreSnapshot(
                snapshot,
                out CharacterTestRuntimeAiPlannerSessionDeckLock[] restored,
                out string failure))
            {
                return NetworkSnapshotApplyResult.Failed(failure);
            }
            return ContainsExistingLocks(restored, out string monotonicFailure)
                ? NetworkSnapshotApplyResult.Succeeded()
                : NetworkSnapshotApplyResult.Failed(monotonicFailure);
        }

        public object CaptureRollbackState(RuntimeFrame frame)
        {
            return new RollbackState(CopyLocks(_sessionLocks));
        }

        public bool RestoreRollbackState(RuntimeFrame frame, object state, out string failure)
        {
            if (!(state is RollbackState rollback))
            {
                failure = "Runtime AI Planner session authority rollback state type is invalid.";
                return false;
            }
            if (!ValidateLocks(rollback.SessionLocks, out failure))
                return false;
            SetLocks(CopyLocks(rollback.SessionLocks));
            return true;
        }

        private bool TryRestoreSnapshot(
            RuntimeCustomState snapshot,
            out CharacterTestRuntimeAiPlannerSessionDeckLock[] restored,
            out string failure)
        {
            restored = null;
            failure = string.Empty;
            if (snapshot == null)
            {
                failure = "Runtime AI Planner session authority snapshot is null.";
                return false;
            }
            if (snapshot.NumericTypeId != RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority)
            {
                failure = "Runtime AI Planner session authority participant mismatch: " + snapshot.TypeId;
                return false;
            }
            if (snapshot.SchemaVersion != SchemaVersionValue)
            {
                failure = "Runtime AI Planner session authority snapshot schema mismatch.";
                return false;
            }

            SnapshotDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<SnapshotDocument>(snapshot.PayloadJson, JsonSettings);
            }
            catch (Exception exception)
            {
                failure = "Runtime AI Planner session authority snapshot JSON parse failed: " + exception.Message;
                return false;
            }
            if (document == null || document.SchemaVersion != SchemaVersionValue)
            {
                failure = "Runtime AI Planner session authority snapshot document is invalid.";
                return false;
            }
            if (document.Locks == null)
            {
                failure = "Runtime AI Planner session authority lock list is missing.";
                return false;
            }
            restored = new CharacterTestRuntimeAiPlannerSessionDeckLock[document.Locks.Length];
            string previousCharacterStableId = string.Empty;
            for (int i = 0; i < document.Locks.Length; i++)
            {
                LockDocument candidate = document.Locks[i];
                if (candidate == null)
                {
                    failure = "Runtime AI Planner session authority locks are not uniquely normalized.";
                    restored = null;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(candidate.CharacterStableId)
                    || !string.Equals(
                        candidate.CharacterStableId,
                        candidate.CharacterStableId.Trim(),
                        StringComparison.Ordinal))
                {
                    failure = "Runtime AI Planner session authority locks are not uniquely normalized.";
                    restored = null;
                    return false;
                }
                if (!TryCreateLock(candidate, out restored[i], out failure))
                {
                    restored = null;
                    return false;
                }
                if (i > 0 && string.Compare(
                    previousCharacterStableId,
                    restored[i].CharacterStableId,
                    StringComparison.Ordinal) >= 0)
                {
                    failure = "Runtime AI Planner session authority locks are not uniquely normalized.";
                    restored = null;
                    return false;
                }
                previousCharacterStableId = restored[i].CharacterStableId;
            }
            if (ComputeAuthorityHash(restored) != document.AuthorityHash)
            {
                restored = null;
                failure = "Runtime AI Planner session authority roster hash mismatch.";
                return false;
            }
            return true;
        }

        private bool TryCreateLock(
            LockDocument document,
            out CharacterTestRuntimeAiPlannerSessionDeckLock restored,
            out string failure)
        {
            restored = null;
            failure = string.Empty;
            if (!string.Equals(document.CatalogId, _catalog.CatalogId, StringComparison.Ordinal)
                || document.CatalogRevision != _catalog.Revision
                || document.CatalogNumericId != _catalog.CatalogNumericId
                || document.CatalogNumericIdentityHash != _catalog.NumericIdentityHash
                || document.CatalogHash != _catalog.CatalogHash)
            {
                failure = "Runtime AI Planner session authority catalog identity mismatch.";
                return false;
            }
            if (document.RuntimeContractVersion != _runtimeContractVersion)
            {
                failure = "Runtime AI Planner session authority runtime contract mismatch.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(document.CharacterStableId))
            {
                failure = "Runtime AI Planner session authority character stable id is missing.";
                return false;
            }
            if (document.CharacterNumericId <= 0)
            {
                failure = "Runtime AI Planner session authority character numeric id is invalid.";
                return false;
            }
            if (document.CatalogNumericId <= 0
                || !CharacterTestAuthoredContentIds.IsActive(
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId,
                    document.CatalogNumericId))
            {
                failure = "Runtime AI Planner session authority catalog numeric id is unknown or retired.";
                return false;
            }
            if (document.Cards == null)
            {
                failure = "Runtime AI Planner session authority pinned card list is missing.";
                return false;
            }
            if (document.Cards.Length > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots)
            {
                failure = "Runtime AI Planner session authority pinned card count exceeds slot limit.";
                return false;
            }

            var pinned = new CharacterTestRuntimeAiPlannerPinnedBehaviorCard[document.Cards.Length];
            var stableCardIds = new string[document.Cards.Length];
            var cardNumericIds = new int[document.Cards.Length];
            int totalCost = 0;
            int previousNumericId = 0;
            for (int i = 0; i < document.Cards.Length; i++)
            {
                PinnedCardDocument candidate = document.Cards[i];
                if (candidate == null
                    || !CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(candidate.StableCardId)
                    || candidate.NumericId <= 0
                    || !CharacterTestAuthoredContentIds.IsActive(
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId,
                        candidate.NumericId))
                {
                    failure = "Runtime AI Planner session authority pinned card identity is invalid.";
                    return false;
                }
                if (i > 0 && previousNumericId >= candidate.NumericId)
                {
                    failure = "Runtime AI Planner session authority pinned cards are not uniquely normalized.";
                    return false;
                }
                if (!_catalog.TryGetExact(
                    candidate.NumericId,
                    candidate.Revision,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published))
                {
                    failure = "Runtime AI Planner session authority pinned Published revision is unavailable.";
                    return false;
                }
                if (!published.IsCompatibleWith(_runtimeContractVersion)
                    || !string.Equals(published.StableCardId, candidate.StableCardId, StringComparison.Ordinal)
                    || published.SemanticHash != candidate.SemanticHash
                    || published.ContentHash != candidate.ContentHash
                    || published.Metadata.Cost != candidate.Cost
                    || published.NumericId != candidate.NumericId)
                {
                    failure = "Runtime AI Planner session authority pinned Published card identity mismatch.";
                    return false;
                }
                pinned[i] = new CharacterTestRuntimeAiPlannerPinnedBehaviorCard(
                    published.StableCardId,
                    published.Revision,
                    published.SemanticHash,
                    published.ContentHash,
                    published.Metadata.Cost,
                    published.NumericId);
                stableCardIds[i] = published.StableCardId;
                cardNumericIds[i] = published.NumericId;
                totalCost += published.Metadata.Cost;
                previousNumericId = published.NumericId;
            }
            if (totalCost != document.TotalCost
                || totalCost > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost)
            {
                failure = "Runtime AI Planner session authority pinned card cost mismatch.";
                return false;
            }

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult build =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                    new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                        document.CharacterStableId.Trim(),
                        cardNumericIds,
                        stableCardIds,
                        document.CharacterNumericId),
                    _catalog,
                    _runtimeContractVersion,
                    diagnosticsInput: null);
            if (!build.Success
                || build.Deck.TotalCost != totalCost
                || build.Deck.Cards.Count != pinned.Length)
            {
                failure = "Runtime AI Planner session authority deck validation failed during restore.";
                return false;
            }
            for (int i = 0; i < pinned.Length; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckCard rebuilt = build.Deck.Cards[i];
                CharacterTestRuntimeAiPlannerPinnedBehaviorCard expected = pinned[i];
                if (rebuilt.Revision != expected.Revision
                    || rebuilt.SemanticHash != expected.SemanticHash
                    || rebuilt.ContentHash != expected.ContentHash
                    || rebuilt.Cost != expected.Cost)
                {
                    failure = "Runtime AI Planner session authority rebuilt card identity mismatch.";
                    return false;
                }
            }
            if (build.Deck.DeckHash != document.DeckHash)
            {
                failure = "Runtime AI Planner session authority deck hash mismatch.";
                return false;
            }
            restored = CharacterTestRuntimeAiPlannerSessionDeckLock.FromInstalledDeck(build.Deck);
            if (restored.AuthorityHash != document.LockAuthorityHash)
            {
                restored = null;
                failure = "Runtime AI Planner session authority hash mismatch.";
                return false;
            }
            return true;
        }

        private bool ValidateLocks(
            CharacterTestRuntimeAiPlannerSessionDeckLock[] sessionLocks,
            out string failure)
        {
            failure = string.Empty;
            string previousCharacterStableId = string.Empty;
            for (int i = 0; i < sessionLocks.Length; i++)
            {
                CharacterTestRuntimeAiPlannerSessionDeckLock candidate = sessionLocks[i];
                if (candidate == null
                    || (i > 0 && string.Compare(
                        previousCharacterStableId,
                        candidate.CharacterStableId,
                        StringComparison.Ordinal) >= 0))
                {
                    failure = "Runtime AI Planner session authority rollback locks are invalid.";
                    return false;
                }
                LockDocument document = LockDocument.FromLock(candidate);
                if (!TryCreateLock(document, out CharacterTestRuntimeAiPlannerSessionDeckLock rebuilt, out failure)
                    || rebuilt.AuthorityHash != candidate.AuthorityHash)
                {
                    return false;
                }
                previousCharacterStableId = candidate.CharacterStableId;
            }
            return true;
        }

        private bool ContainsExistingLocks(
            CharacterTestRuntimeAiPlannerSessionDeckLock[] restored,
            out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < _sessionLocks.Length; i++)
            {
                CharacterTestRuntimeAiPlannerSessionDeckLock existing = _sessionLocks[i];
                int index = FindLockIndex(restored, existing.CharacterStableId);
                if (index < 0 || restored[index].AuthorityHash != existing.AuthorityHash)
                {
                    failure = "Runtime AI Planner session authority snapshot cannot remove or replace an existing character lock.";
                    return false;
                }
            }
            return true;
        }

        private void AddLock(CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock)
        {
            var result = new CharacterTestRuntimeAiPlannerSessionDeckLock[_sessionLocks.Length + 1];
            int insertIndex = ~FindLockIndex(sessionLock.CharacterStableId);
            if (insertIndex > 0)
                Array.Copy(_sessionLocks, 0, result, 0, insertIndex);
            result[insertIndex] = sessionLock;
            if (insertIndex < _sessionLocks.Length)
            {
                Array.Copy(
                    _sessionLocks,
                    insertIndex,
                    result,
                    insertIndex + 1,
                    _sessionLocks.Length - insertIndex);
            }
            SetLocks(result);
        }

        private int FindLockIndex(string characterStableId)
        {
            return FindLockIndex(_sessionLocks, characterStableId);
        }

        private static int FindLockIndex(
            CharacterTestRuntimeAiPlannerSessionDeckLock[] locks,
            string characterStableId)
        {
            string normalized = characterStableId?.Trim() ?? string.Empty;
            int low = 0;
            int high = locks.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = string.Compare(
                    locks[middle].CharacterStableId,
                    normalized,
                    StringComparison.Ordinal);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return ~low;
        }

        private void SetLocks(CharacterTestRuntimeAiPlannerSessionDeckLock[] locks)
        {
            _sessionLocks = locks ?? Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>();
            _readOnlySessionLocks = Array.AsReadOnly(_sessionLocks);
        }

        private static CharacterTestRuntimeAiPlannerSessionDeckLock[] CopyLocks(
            CharacterTestRuntimeAiPlannerSessionDeckLock[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerSessionDeckLock>();
            var result = new CharacterTestRuntimeAiPlannerSessionDeckLock[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private static long ComputeAuthorityHash(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> locks)
        {
            if (locks == null || locks.Count == 0)
                return 0L;
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerSessionParticipant, 2);
            hash.AddInt(1, RuntimeHashSchemaIds.RuntimeAiPlannerSessionParticipant);
            hash.AddInt(2, locks.Count);
            for (int i = 0; i < locks.Count; i++)
            {
                hash.AddInt(3, i);
                hash.AddInt(4, locks[i].CharacterNumericId);
                hash.AddLong(5, locks[i].AuthorityHash);
            }
            return hash.ToHash();
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityResult Failed(
            string code,
            string path,
            string message)
        {
            return new CharacterTestRuntimeAiPlannerSessionAuthorityResult(
                null,
                new[]
                {
                    new CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic(
                        CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Error,
                        code,
                        path,
                        message)
                });
        }

        private sealed class RollbackState
        {
            public RollbackState(CharacterTestRuntimeAiPlannerSessionDeckLock[] sessionLocks)
            {
                SessionLocks = sessionLocks;
            }

            public CharacterTestRuntimeAiPlannerSessionDeckLock[] SessionLocks { get; }
        }

        private sealed class SnapshotDocument
        {
            public int SchemaVersion { get; set; }
            public long AuthorityHash { get; set; }
            public LockDocument[] Locks { get; set; }

            public static SnapshotDocument FromLocks(
                IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> sessionLocks)
            {
                var locks = new LockDocument[sessionLocks.Count];
                for (int i = 0; i < locks.Length; i++)
                    locks[i] = LockDocument.FromLock(sessionLocks[i]);
                return new SnapshotDocument
                {
                    SchemaVersion = SchemaVersionValue,
                    AuthorityHash = ComputeAuthorityHash(sessionLocks),
                    Locks = locks
                };
            }
        }

        private sealed class LockDocument
        {
            public string CharacterStableId { get; set; }
            public int CharacterNumericId { get; set; }
            public string CatalogId { get; set; }
            public int CatalogRevision { get; set; }
            public int CatalogNumericId { get; set; }
            public long CatalogNumericIdentityHash { get; set; }
            public long CatalogHash { get; set; }
            public int RuntimeContractVersion { get; set; }
            public PinnedCardDocument[] Cards { get; set; }
            public int TotalCost { get; set; }
            public long DeckHash { get; set; }
            public long LockAuthorityHash { get; set; }

            public static LockDocument FromLock(
                CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock)
            {
                var document = new LockDocument
                {
                    Cards = new PinnedCardDocument[sessionLock.Cards.Count]
                };
                document.CharacterStableId = sessionLock.CharacterStableId;
                document.CharacterNumericId = sessionLock.CharacterNumericId;
                document.CatalogId = sessionLock.CatalogId;
                document.CatalogRevision = sessionLock.CatalogRevision;
                document.CatalogNumericId = sessionLock.CatalogNumericId;
                document.CatalogNumericIdentityHash = sessionLock.CatalogNumericIdentityHash;
                document.CatalogHash = sessionLock.CatalogHash;
                document.RuntimeContractVersion = sessionLock.RuntimeContractVersion;
                for (int i = 0; i < document.Cards.Length; i++)
                {
                    CharacterTestRuntimeAiPlannerPinnedBehaviorCard card = sessionLock.Cards[i];
                    document.Cards[i] = new PinnedCardDocument
                    {
                        StableCardId = card.StableCardId,
                        Revision = card.Revision,
                        SemanticHash = card.SemanticHash,
                        ContentHash = card.ContentHash,
                        Cost = card.Cost,
                        NumericId = card.NumericId
                    };
                }
                document.TotalCost = sessionLock.TotalCost;
                document.DeckHash = sessionLock.DeckHash;
                document.LockAuthorityHash = sessionLock.AuthorityHash;
                return document;
            }
        }

        private sealed class PinnedCardDocument
        {
            public string StableCardId { get; set; }
            public int Revision { get; set; }
            public long SemanticHash { get; set; }
            public long ContentHash { get; set; }
            public int Cost { get; set; }
            public int NumericId { get; set; }
        }
    }
}
