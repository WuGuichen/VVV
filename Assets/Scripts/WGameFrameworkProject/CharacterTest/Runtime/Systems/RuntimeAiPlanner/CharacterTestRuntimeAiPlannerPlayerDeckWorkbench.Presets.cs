using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerPlayerDeckWorkbench
    {
        internal enum PresetDiffKind
        {
            Added = 0,
            Retained = 1,
            Removed = 2
        }

        internal sealed class PresetDiffEntry
        {
            internal PresetDiffEntry(
                string stableCardId,
                string displayName,
                int revision,
                int cost,
                bool costKnown,
                CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability,
                PresetDiffKind kind)
            {
                StableCardId = stableCardId ?? string.Empty;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? StableCardId : displayName;
                Revision = revision;
                Cost = cost;
                CostKnown = costKnown;
                Availability = availability;
                Kind = kind;
            }

            public string StableCardId { get; }
            public string DisplayName { get; }
            public int Revision { get; }
            public int Cost { get; }
            public bool CostKnown { get; }
            public CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability Availability { get; }
            public PresetDiffKind Kind { get; }
        }

        internal sealed class PresetPreview
        {
            private readonly object _ownerToken;
            private readonly string[] _sourceStableCardIds;
            private readonly IReadOnlyList<PresetDiffEntry> _added;
            private readonly IReadOnlyList<PresetDiffEntry> _retained;
            private readonly IReadOnlyList<PresetDiffEntry> _removed;
            private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> _candidateDiagnostics;

            private PresetPreview(
                object ownerToken,
                long sourceEditEpoch,
                string characterStableId,
                long catalogHash,
                int runtimeContractVersion,
                string[] sourceStableCardIds,
                long sourceReferenceFingerprint,
                long sourceDeckHash,
                bool sourceResolved,
                int sourceKnownCost,
                bool sourceBudgetComplete,
                CharacterTestRuntimeAiPlannerInstalledDeck candidateDeck,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] candidateDiagnostics,
                PresetDiffEntry[] added,
                PresetDiffEntry[] retained,
                PresetDiffEntry[] removed,
                bool canConfirm)
            {
                _ownerToken = ownerToken;
                SourceEditEpoch = sourceEditEpoch;
                CharacterStableId = characterStableId;
                CatalogHash = catalogHash;
                RuntimeContractVersion = runtimeContractVersion;
                _sourceStableCardIds = sourceStableCardIds ?? Array.Empty<string>();
                SourceReferenceFingerprint = sourceReferenceFingerprint;
                SourceDeckHash = sourceDeckHash;
                SourceResolved = sourceResolved;
                SourceKnownCost = sourceKnownCost;
                SourceBudgetComplete = sourceBudgetComplete;
                CandidateDeck = candidateDeck;
                _candidateDiagnostics = Array.AsReadOnly(
                    candidateDiagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
                _added = Array.AsReadOnly(added ?? Array.Empty<PresetDiffEntry>());
                _retained = Array.AsReadOnly(retained ?? Array.Empty<PresetDiffEntry>());
                _removed = Array.AsReadOnly(removed ?? Array.Empty<PresetDiffEntry>());
                CanConfirm = canConfirm;
            }

            public string CharacterStableId { get; }
            public long CatalogHash { get; }
            public int RuntimeContractVersion { get; }
            public long SourceReferenceFingerprint { get; }
            public long SourceDeckHash { get; }
            public bool SourceResolved { get; }
            public int SourceSlotCount => _sourceStableCardIds.Length;
            public int SourceKnownCost { get; }
            public bool SourceBudgetComplete { get; }
            public int CandidateSlotCount => CandidateDeck?.SlotCount ?? 0;
            public int CandidateCost => CandidateDeck?.TotalCost ?? 0;
            public int CostDelta => SourceBudgetComplete ? CandidateCost - SourceKnownCost : 0;
            public bool DeltaKnown => SourceBudgetComplete;
            public bool CanConfirm { get; }
            public IReadOnlyList<PresetDiffEntry> Added => _added;
            public IReadOnlyList<PresetDiffEntry> Retained => _retained;
            public IReadOnlyList<PresetDiffEntry> Removed => _removed;
            public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> CandidateDiagnostics =>
                _candidateDiagnostics;

            private long SourceEditEpoch { get; }
            private CharacterTestRuntimeAiPlannerInstalledDeck CandidateDeck { get; }

            internal static PresetPreview Create(
                object ownerToken,
                long sourceEditEpoch,
                string characterStableId,
                long catalogHash,
                int runtimeContractVersion,
                string[] sourceStableCardIds,
                long sourceReferenceFingerprint,
                long sourceDeckHash,
                bool sourceResolved,
                int sourceKnownCost,
                bool sourceBudgetComplete,
                CharacterTestRuntimeAiPlannerInstalledDeck candidateDeck,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] candidateDiagnostics,
                PresetDiffEntry[] added,
                PresetDiffEntry[] retained,
                PresetDiffEntry[] removed,
                bool canConfirm)
            {
                return new PresetPreview(
                    ownerToken,
                    sourceEditEpoch,
                    characterStableId,
                    catalogHash,
                    runtimeContractVersion,
                    sourceStableCardIds,
                    sourceReferenceFingerprint,
                    sourceDeckHash,
                    sourceResolved,
                    sourceKnownCost,
                    sourceBudgetComplete,
                    candidateDeck,
                    candidateDiagnostics,
                    added,
                    retained,
                    removed,
                    canConfirm);
            }

            internal bool MatchesAuthority(
                object ownerToken,
                long sourceEditEpoch,
                string characterStableId,
                long catalogHash,
                int runtimeContractVersion,
                long sourceReferenceFingerprint,
                long sourceDeckHash)
            {
                return ReferenceEquals(_ownerToken, ownerToken)
                    && SourceEditEpoch == sourceEditEpoch
                    && string.Equals(CharacterStableId, characterStableId, StringComparison.Ordinal)
                    && CatalogHash == catalogHash
                    && RuntimeContractVersion == runtimeContractVersion
                    && SourceReferenceFingerprint == sourceReferenceFingerprint
                    && SourceDeckHash == sourceDeckHash;
            }

            internal CharacterTestRuntimeAiPlannerInstalledDeck ReadCandidate(object ownerToken)
            {
                return ReferenceEquals(_ownerToken, ownerToken) ? CandidateDeck : null;
            }
        }

        internal sealed class PresetPreviewResult
        {
            internal PresetPreviewResult(
                PresetPreview preview,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
            {
                Preview = preview;
                Diagnostics = Array.AsReadOnly(
                    diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            }

            public PresetPreview Preview { get; }
            public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
            public bool Success => Preview != null;
        }

        private sealed class PresetUndoCheckpoint
        {
            public PresetUndoCheckpoint(
                string[] sourceStableCardIds,
                long sourceReferenceFingerprint,
                long sourceDeckHash,
                bool sourceResolved,
                long candidateDeckHash,
                long confirmedEditEpoch,
                PresetPreview preview)
            {
                SourceStableCardIds = sourceStableCardIds;
                SourceReferenceFingerprint = sourceReferenceFingerprint;
                SourceDeckHash = sourceDeckHash;
                SourceResolved = sourceResolved;
                CandidateDeckHash = candidateDeckHash;
                ConfirmedEditEpoch = confirmedEditEpoch;
                Preview = preview;
            }

            public string[] SourceStableCardIds { get; }
            public long SourceReferenceFingerprint { get; }
            public long SourceDeckHash { get; }
            public bool SourceResolved { get; }
            public long CandidateDeckHash { get; }
            public long ConfirmedEditEpoch { get; }
            public PresetPreview Preview { get; }
        }

        private readonly object _presetOwnerToken = new object();
        private long _editEpoch;
        private PresetUndoCheckpoint _presetUndoCheckpoint;

        public bool CanUndoLastPreset => !_isSessionLocked && IsUndoCheckpointCurrent();

        /// <remarks>LowFreqAlloc: explicit player command at the pre-battle composition boundary.</remarks>
        public PresetPreviewResult TryPreviewPreset(
            CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition preset,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadoutCatalog)
        {
            if (_isSessionLocked)
                return PreviewFailed("PLAYER_DECK_SESSION_LOCKED", "$.session", "Behavior deck editing is locked for the active session.");
            if (preset != null)
            {
                for (int i = 0; i < preset.StableCardIds.Count; i++)
                {
                    if (!_unlockedCards.Contains(preset.StableCardIds[i]))
                    {
                        return PreviewFailed(
                            "PLAYER_DECK_PRESET_CARD_NOT_UNLOCKED",
                            "$.preset.stableCardIds[" + i + "]",
                            "Recommended preset contains a behavior card that is not unlocked: " + preset.StableCardIds[i]);
                    }
                }
            }

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult candidate =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ExpandRecommendedPreset(
                    _characterStableId,
                    _characterNumericId,
                    preset,
                    loadoutCatalog,
                    _catalog,
                    _runtimeContractVersion,
                    _diagnosticsInput);
            if (!candidate.Success)
                return PreviewFailed(candidate.Diagnostics);

            string[] candidateIds = CopyCardIds(candidate.Deck.Cards);
            PresetPreview preview = BuildPresetPreview(candidate.Deck, candidate.Diagnostics, candidateIds);
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new PresetPreviewResult(preview, CopyDiagnostics(candidate.Diagnostics));
        }

        /// <remarks>LowFreqAlloc: explicit player confirmation at the pre-battle composition boundary.</remarks>
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryConfirmPreset(PresetPreview preview)
        {
            if (_isSessionLocked)
                return Failed("PLAYER_DECK_SESSION_LOCKED", "$.session", "Behavior deck editing is locked for the active session.");
            if (!IsPreviewCurrent(preview))
                return Failed("PLAYER_DECK_PRESET_PREVIEW_STALE", "$.preview", "Recommended preset preview no longer matches the current behavior deck.");
            if (!preview.CanConfirm)
                return Failed("PLAYER_DECK_PRESET_NO_CHANGES", "$.preview", "The current behavior deck already matches the recommended preset.");

            CharacterTestRuntimeAiPlannerInstalledDeck candidateFromPreview =
                preview.ReadCandidate(_presetOwnerToken);
            if (candidateFromPreview == null
                || !string.Equals(candidateFromPreview.CharacterStableId, _characterStableId, StringComparison.Ordinal)
                || candidateFromPreview.CharacterNumericId != _characterNumericId
                || candidateFromPreview.CatalogHash != _catalog.CatalogHash
                || candidateFromPreview.RuntimeContractVersion != _runtimeContractVersion)
            {
                return Failed(
                    "PLAYER_DECK_PRESET_CANDIDATE_INVALID",
                    "$.preview.candidate",
                    "Recommended preset candidate identity does not match the current behavior deck authority.");
            }

            string[] candidateIds = CopyCardIds(candidateFromPreview.Cards);
            for (int i = 0; i < candidateIds.Length; i++)
            {
                if (!_unlockedCards.Contains(candidateIds[i]))
                {
                    return Failed(
                        "PLAYER_DECK_PRESET_CANDIDATE_NOT_UNLOCKED",
                        "$.preview.candidate.stableCardIds[" + i + "]",
                        "Recommended preset candidate contains a behavior card that is not unlocked: " + candidateIds[i]);
                }
            }

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult candidateBuild = BuildDeck(
                _catalog,
                _characterStableId,
                _characterNumericId,
                _runtimeContractVersion,
                candidateIds,
                _diagnosticsInput);
            if (!candidateBuild.Success
                || candidateBuild.Deck.DeckHash != candidateFromPreview.DeckHash
                || SameReferences(_stableCardIds, candidateIds))
            {
                return Failed(
                    "PLAYER_DECK_PRESET_CANDIDATE_INVALID",
                    "$.preview.candidate",
                    "Recommended preset candidate no longer passes the current behavior deck rules.");
            }

            string[] sourceIds = CopyCardIds(_stableCardIds);
            long sourceFingerprint = ComputeReferenceFingerprint(sourceIds);
            long sourceDeckHash = _deck?.DeckHash ?? 0L;
            bool sourceResolved = _deck != null;
            CharacterTestRuntimeAiPlannerInstalledDeck candidateDeck = candidateBuild.Deck;
            CharacterTestRuntimeAiPlannerPersistedDeck persisted = CreatePersistedDeck(candidateDeck.Cards);
            if (!TryPersist(persisted, out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic persistenceDiagnostic))
                return Failed(persistenceDiagnostic);

            _deck = candidateDeck;
            _stableCardIds = CopyCardIds(candidateDeck.Cards);
            _deckDiagnostics = CopyDiagnostics(candidateBuild.Diagnostics);
            _editEpoch++;
            _presetUndoCheckpoint = new PresetUndoCheckpoint(
                sourceIds,
                sourceFingerprint,
                sourceDeckHash,
                sourceResolved,
                candidateDeck.DeckHash,
                _editEpoch,
                preview);
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
                _view,
                CopyDiagnostics(candidateBuild.Diagnostics),
                true);
        }

        /// <remarks>LowFreqAlloc: explicit one-shot player undo at the pre-battle composition boundary.</remarks>
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryUndoLastPreset()
        {
            if (_isSessionLocked)
                return Failed("PLAYER_DECK_SESSION_LOCKED", "$.session", "Behavior deck editing is locked for the active session.");
            if (!IsUndoCheckpointCurrent())
                return Failed("PLAYER_DECK_PRESET_UNDO_UNAVAILABLE", "$.undo", "There is no current recommended preset change to undo.");

            PresetUndoCheckpoint checkpoint = _presetUndoCheckpoint;
            string[] sourceIds = CopyCardIds(checkpoint.SourceStableCardIds);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult rebuilt = BuildDeck(
                _catalog,
                _characterStableId,
                _characterNumericId,
                _runtimeContractVersion,
                sourceIds,
                _diagnosticsInput);
            if (checkpoint.SourceResolved
                && (!rebuilt.Success || rebuilt.Deck.DeckHash != checkpoint.SourceDeckHash))
            {
                return Failed(
                    "PLAYER_DECK_PRESET_UNDO_SOURCE_INVALID",
                    "$.undo.source",
                    "The source behavior deck can no longer be restored with the same identity.");
            }

            var numericIds = new int[rebuilt.Success ? rebuilt.Deck.Cards.Count : 0];
            if (rebuilt.Success)
            {
                for (int i = 0; i < numericIds.Length; i++)
                    numericIds[i] = rebuilt.Deck.Cards[i].NumericId;
            }
            var persisted = rebuilt.Success
                ? new CharacterTestRuntimeAiPlannerPersistedDeck(
                    _characterStableId,
                    numericIds,
                    sourceIds,
                    _characterNumericId,
                    rebuilt.Deck.CatalogNumericId,
                    rebuilt.Deck.CatalogNumericIdentityHash)
                : new CharacterTestRuntimeAiPlannerPersistedDeck(
                    _characterStableId,
                    sourceIds,
                    _characterNumericId);
            if (!TryPersist(persisted, out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic persistenceDiagnostic))
                return Failed(persistenceDiagnostic);

            _stableCardIds = sourceIds;
            if (rebuilt.Success)
            {
                _deck = rebuilt.Deck;
                _deckDiagnostics = CopyDiagnostics(rebuilt.Diagnostics);
            }
            else
            {
                _deck = null;
                _deckDiagnostics = BuildUnresolvedDiagnostics(
                    _catalog,
                    _runtimeContractVersion,
                    sourceIds,
                    rebuilt.Diagnostics);
            }
            _editEpoch++;
            _presetUndoCheckpoint = null;
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
                _view,
                _deckDiagnostics,
                true);
        }

        private PresetPreview BuildPresetPreview(
            CharacterTestRuntimeAiPlannerInstalledDeck candidateDeck,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> candidateDiagnostics,
            string[] candidateIds)
        {
            var added = new List<PresetDiffEntry>();
            var retained = new List<PresetDiffEntry>();
            var removed = new List<PresetDiffEntry>();
            int sourceIndex = 0;
            int candidateIndex = 0;
            while (sourceIndex < _stableCardIds.Length || candidateIndex < candidateIds.Length)
            {
                if (sourceIndex >= _stableCardIds.Length)
                {
                    added.Add(BuildDiffEntry(candidateIds[candidateIndex++], PresetDiffKind.Added));
                    continue;
                }
                if (candidateIndex >= candidateIds.Length)
                {
                    removed.Add(BuildDiffEntry(_stableCardIds[sourceIndex++], PresetDiffKind.Removed));
                    continue;
                }

                int comparison = string.Compare(
                    _stableCardIds[sourceIndex],
                    candidateIds[candidateIndex],
                    StringComparison.Ordinal);
                if (comparison < 0)
                    removed.Add(BuildDiffEntry(_stableCardIds[sourceIndex++], PresetDiffKind.Removed));
                else if (comparison > 0)
                    added.Add(BuildDiffEntry(candidateIds[candidateIndex++], PresetDiffKind.Added));
                else
                {
                    retained.Add(BuildDiffEntry(_stableCardIds[sourceIndex], PresetDiffKind.Retained));
                    sourceIndex++;
                    candidateIndex++;
                }
            }

            bool sourceBudgetComplete = IsKnownCostComplete();
            return PresetPreview.Create(
                _presetOwnerToken,
                _editEpoch,
                _characterStableId,
                _catalog.CatalogHash,
                _runtimeContractVersion,
                CopyCardIds(_stableCardIds),
                ComputeReferenceFingerprint(_stableCardIds),
                _deck?.DeckHash ?? 0L,
                _deck != null,
                ResolveKnownCost(),
                sourceBudgetComplete,
                candidateDeck,
                CopyDiagnostics(candidateDiagnostics),
                added.ToArray(),
                retained.ToArray(),
                removed.ToArray(),
                !SameReferences(_stableCardIds, candidateIds));
        }

        private PresetDiffEntry BuildDiffEntry(string stableCardId, PresetDiffKind kind)
        {
            bool available = _catalog.TryResolveLatestCompatible(
                stableCardId,
                _runtimeContractVersion,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card);
            CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability = available
                ? CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                : HasCatalogRevision(stableCardId)
                    ? CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible
                    : CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable;
            return new PresetDiffEntry(
                stableCardId,
                card?.Metadata.DisplayName,
                card?.Revision ?? 0,
                card?.Metadata.Cost ?? 0,
                available,
                availability,
                kind);
        }

        private PresetPreviewResult PreviewFailed(
            string code,
            string path,
            string message)
        {
            var diagnostics = new[]
            {
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    code,
                    path,
                    message)
            };
            _view = BuildView(diagnostics);
            return new PresetPreviewResult(null, diagnostics);
        }

        private PresetPreviewResult PreviewFailed(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> source)
        {
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics = CopyDiagnostics(source);
            _view = BuildView(diagnostics);
            return new PresetPreviewResult(null, diagnostics);
        }

        private bool IsPreviewCurrent(PresetPreview preview)
        {
            return preview != null
                && preview.MatchesAuthority(
                    _presetOwnerToken,
                    _editEpoch,
                    _characterStableId,
                    _catalog.CatalogHash,
                    _runtimeContractVersion,
                    ComputeReferenceFingerprint(_stableCardIds),
                    _deck?.DeckHash ?? 0L);
        }

        private bool IsUndoCheckpointCurrent()
        {
            return _presetUndoCheckpoint != null
                && _presetUndoCheckpoint.ConfirmedEditEpoch == _editEpoch
                && _deck != null
                && _deck.DeckHash == _presetUndoCheckpoint.CandidateDeckHash;
        }

        private void OnSuccessfulRegularMutation()
        {
            _editEpoch++;
            _presetUndoCheckpoint = null;
        }

        private static bool SameReferences(string[] left, string[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static long ComputeReferenceFingerprint(IReadOnlyList<string> stableCardIds)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("playerDeck.preset.source.schema", "v1");
            hash.AddInt("playerDeck.preset.source.count", stableCardIds?.Count ?? 0);
            for (int i = 0; stableCardIds != null && i < stableCardIds.Count; i++)
            {
                hash.AddInt("playerDeck.preset.source.index", i);
                hash.AddStringStable("playerDeck.preset.source.stableCardId", stableCardIds[i]);
            }
            return hash.ToHash();
        }
    }
}
