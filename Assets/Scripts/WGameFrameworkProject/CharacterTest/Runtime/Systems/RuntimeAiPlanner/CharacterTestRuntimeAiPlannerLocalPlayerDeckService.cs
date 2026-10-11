using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiPlannerPlayerDeckTarget
    {
        public CharacterTestRuntimeAiPlannerPlayerDeckTarget(
            int characterId,
            int preferredProfileSlot,
            int teamId = CharacterTestArenaMatchConfig.Team1Id,
            string displayName = "")
        {
            CharacterId = characterId;
            PreferredProfileSlot = Math.Max(0, preferredProfileSlot);
            TeamId = teamId;
            DisplayName = displayName?.Trim() ?? string.Empty;
        }

        public int CharacterId { get; }
        public int PreferredProfileSlot { get; }
        public int TeamId { get; }
        public string DisplayName { get; }
        public string CharacterStableId => "character.definition."
            + CharacterId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// CharacterTest-only offline composition root for the player deck workbench. Production account
    /// catalogs and network authority remain separate later-stage integrations.
    /// </summary>
    internal sealed partial class CharacterTestRuntimeAiPlannerLocalPlayerDeckService :
        ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink,
        ICharacterTestRuntimeAiLabPlayerLegalitySource,
        ICharacterTestRuntimeAiLabPublishSource
    {
        public const string CatalogId =
            "character-test.runtime-ai-planner.project-behavior-cards";
        private const string LocalAuthorityPeerId = "character-test.local-player";
        private const string ApproachAndAttackCardId =
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack;
        private const string IdleCardId =
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
        private const string LivingTargetConditionId = "activation.target-alive";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly CharacterTestRuntimeAiPlannerBehaviorCapability[] CodexFlowCharacterCapabilities =
        {
            CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Kite,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Defense,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation,
            CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback
        };

        private readonly string _filePath;
        private readonly string _catalogFilePath;
        private readonly CharacterTestRuntimeAiPlannerPlayerDeckTarget[] _targets;
        private int _selectedTargetIndex;
        private int _characterId;
        private int _characterNumericId;
        private int _preferredProfileSlot;
        private string _characterStableId;
        private CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog _catalog;
        private CharacterTestRuntimeAiPlannerDeckPersistenceParticipant _participant;
        private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;
        private readonly bool _writeBlockedByRecovery;
        private readonly CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] _recoveryDiagnostics;
        private CharacterTestRuntimeAiPlannerSessionDeckLock[] _sessionLocks;

        private CharacterTestRuntimeAiPlannerLocalPlayerDeckService(
            string filePath,
            string catalogFilePath,
            CharacterTestRuntimeAiPlannerPlayerDeckTarget[] targets,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerDeckPersistenceParticipant participant,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            string loadWarning,
            string recoveryBackupPath,
            bool writeBlockedByRecovery,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] recoveryDiagnostics)
        {
            _filePath = filePath;
            _catalogFilePath = catalogFilePath;
            _targets = targets ?? Array.Empty<CharacterTestRuntimeAiPlannerPlayerDeckTarget>();
            SelectTargetFields(0);
            _catalog = catalog;
            _participant = participant;
            _references = references;
            _writeBlockedByRecovery = writeBlockedByRecovery;
            _recoveryDiagnostics = recoveryDiagnostics
                ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            LoadWarning = loadWarning ?? string.Empty;
            RecoveryBackupPath = recoveryBackupPath ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbench Workbench { get; private set; }
        internal CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog => _catalog;
        public CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot Snapshot => _participant.CurrentSnapshot;
        public CharacterTestRuntimeAiPlannerSessionDeckLock SessionLock => _sessionLocks != null
            && _selectedTargetIndex >= 0
            && _selectedTargetIndex < _sessionLocks.Length
                ? _sessionLocks[_selectedTargetIndex]
                : null;
        public string LoadWarning { get; }
        public string RecoveryBackupPath { get; }
        public int SelectedCharacterId => _characterId;
        public string SelectedCharacterDisplayName => _targets.Length > 0
            ? _targets[_selectedTargetIndex].DisplayName
            : string.Empty;
        public int SelectedCharacterIndex => _selectedTargetIndex;
        public int CharacterCount => _targets.Length;
        public bool CanSelectPreviousCharacter => _selectedTargetIndex > 0;
        public bool CanSelectNextCharacter => _selectedTargetIndex + 1 < _targets.Length;
        public bool CanPreviewRecommendedPreset => Workbench != null && !Workbench.View.IsSessionLocked;
        public bool CanUndoRecommendedPreset => Workbench != null && Workbench.CanUndoLastPreset;

        long ICharacterTestRuntimeAiLabPlayerLegalitySource.ProjectionIdentity
        {
            get
            {
                long catalogHash = _catalog != null ? _catalog.CatalogHash : 0L;
                long persistenceHash = _participant?.CurrentSnapshot?.PersistenceHash ?? 0L;
                unchecked
                {
                    return (catalogHash * 1099511628211L) ^ persistenceHash;
                }
            }
        }

        internal bool TryResolveLatestPublishedCard(
            string stableCardId,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            card = null;
            return _catalog != null
                && _catalog.TryResolveLatestCompatible(
                    stableCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out card);
        }

        CharacterTestRuntimeAiLabPlayerLegalityState ICharacterTestRuntimeAiLabPlayerLegalitySource.Evaluate(
            int characterId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            string characterStableId = "character.definition."
                + characterId.ToString(CultureInfo.InvariantCulture);
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput =
                BuildLabDiagnosticsInput(characterId, group, cards);
            return CharacterTestRuntimeAiLabPlayerLegalityEvaluator.Evaluate(
                characterStableId,
                characterId,
                group,
                cards,
                _catalog,
                _participant.CurrentSnapshot.UnlockedCards,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                diagnosticsInput);
        }

        CharacterTestRuntimeAiLabPublishResult ICharacterTestRuntimeAiLabPublishSource.TryPublish(
            int characterId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            string cardDraftId)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (_sessionLocks != null || (Workbench != null && Workbench.View.IsSessionLocked))
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_SESSION_LOCKED",
                    "$.session",
                    "Published behavior cards cannot change after the battle session is locked.");
                return FailedPublish(diagnostics);
            }
            if (group == null || cards == null || group.Entries.Count != 1)
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_SINGLE_CARD_REQUIRED",
                    "$.group.entries",
                    "Publish requires one isolated behavior-card authoring group.");
                return FailedPublish(diagnostics);
            }
            if (!HasIsolatedCanonicalProfile(group))
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_PROFILE_PROJECTION_MISMATCH",
                    "$.group.profile.entries",
                    "The authoring entry and runtime profile must describe the same isolated card.");
                return FailedPublish(diagnostics);
            }
            string normalizedDraftId = cardDraftId?.Trim() ?? string.Empty;
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[0];
            if (entry == null
                || !string.Equals(entry.CardDraftId, normalizedDraftId, StringComparison.Ordinal)
                || !cards.TryGetValue(normalizedDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
                || draft == null
                || !draft.HasPublishedSource)
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_SOURCE_DRAFT_REQUIRED",
                    "$.draft",
                    "Publish requires an isolated copy-on-write draft with a Published source.");
                return FailedPublish(diagnostics);
            }

            CharacterTestRuntimeAiLabPlayerLegalityState legality =
                CharacterTestRuntimeAiLabPlayerLegalityEvaluator.Evaluate(
                    "character.definition." + characterId.ToString(CultureInfo.InvariantCulture),
                    characterId,
                    group,
                    cards,
                    _catalog,
                    _participant.CurrentSnapshot.UnlockedCards,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    BuildLabDiagnosticsInput(characterId, group, cards));
            for (int i = 0; i < legality.Reasons.Count; i++)
            {
                CharacterTestRuntimeAiLabPlayerLegalityReason reason = legality.Reasons[i];
                if (reason.Kind == CharacterTestRuntimeAiLabPlayerLegalityReasonKind.UnpublishedSemanticChanges)
                {
                    continue;
                }
                AddPublishDiagnostic(diagnostics, reason);
            }
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return FailedPublish(diagnostics);

            if (!_catalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourcePublishedRevision,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source)
                || !draft.MatchesPublishedSource(source)
                || !_catalog.TryResolveLatestCompatible(
                    draft.PublishedStableId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard latest)
                || latest.Revision != source.Revision
                || latest.ContentHash != source.ContentHash)
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_SOURCE_OUTDATED",
                    "$.draft.source",
                    "The draft source is not the current compatible Published revision.");
                return FailedPublish(diagnostics);
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(draft.LogicPayload, _references);
            if (!compile.Success || compile.Program == null)
            {
                AddLogicDiagnostics(diagnostics, compile.Diagnostics);
                return FailedPublish(diagnostics);
            }
            if (compile.Program.SemanticHash == source.SemanticHash)
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_NO_SEMANTIC_CHANGES",
                    "$.draft.logic",
                    "The draft has no publishable semantic changes.");
                return FailedPublish(diagnostics);
            }
            if (source.Revision == int.MaxValue || _catalog.Revision == int.MaxValue)
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_REVISION_EXHAUSTED",
                    "$.revision",
                    "Published behavior-card revision range is exhausted.");
                return FailedPublish(diagnostics);
            }

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        source.StableCardId,
                        source.Revision + 1,
                        source.MinimumRuntimeContractVersion,
                        source.MaximumRuntimeContractVersion,
                        source.Metadata),
                    _references);
            AddLogicDiagnostics(diagnostics, publish.Diagnostics);
            if (!publish.Success)
                return FailedPublish(diagnostics);

            var catalogCards = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[_catalog.Cards.Count + 1];
            for (int i = 0; i < _catalog.Cards.Count; i++)
                catalogCards[i] = _catalog.Cards[i];
            catalogCards[catalogCards.Length - 1] = publish.Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult catalogCreate =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    _catalog.CatalogId,
                    _catalog.Revision + 1,
                    catalogCards,
                    _references,
                    _catalog.CatalogNumericId);
            AddLogicDiagnostics(diagnostics, catalogCreate.Diagnostics);
            if (!catalogCreate.Success)
                return FailedPublish(diagnostics);

            var candidateParticipant = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
                _participant.CurrentSnapshot,
                catalogCreate.Catalog,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
            if (!TryValidatePersistedDecks(
                    candidateParticipant.CurrentSnapshot,
                    catalogCreate.Catalog,
                    out string deckValidationError))
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_PERSISTED_DECK_INVALID",
                    "$.persistence.decks",
                    deckValidationError);
                return FailedPublish(diagnostics);
            }
            if (!TryCreateWorkbench(
                    catalogCreate.Catalog,
                    candidateParticipant,
                    _recoveryDiagnostics,
                    out CharacterTestRuntimeAiPlannerPlayerDeckWorkbench candidateWorkbench,
                    out string workbenchError))
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_WORKBENCH_REBUILD_FAILED",
                    "$.workbench",
                    workbenchError);
                return FailedPublish(diagnostics);
            }
            if (!TryWriteCatalog(catalogCreate.Catalog, out string writeError))
            {
                AddPublishError(
                    diagnostics,
                    "LAB_PUBLISH_CATALOG_WRITE_FAILED",
                    "$.catalog",
                    writeError);
                return FailedPublish(diagnostics);
            }

            _catalog = catalogCreate.Catalog;
            _participant = candidateParticipant;
            Workbench = candidateWorkbench;
            return new CharacterTestRuntimeAiLabPublishResult(
                publish.Card,
                catalogCreate.Catalog,
                diagnostics.ToArray());
        }

        private bool TryWriteCatalog(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            out string error)
        {
            string temporaryPath = _catalogFilePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(_catalogFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(
                    temporaryPath,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog),
                    Utf8WithoutBom);
                if (File.Exists(_catalogFilePath))
                    File.Replace(temporaryPath, _catalogFilePath, null);
                else
                    File.Move(temporaryPath, _catalogFilePath);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                error = "Published behavior-card catalog could not be saved: " + exception.Message;
                return false;
            }
        }

        private static CharacterTestRuntimeAiLabPublishResult FailedPublish(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiLabPublishResult(null, null, diagnostics.ToArray());
        }

        private static void AddPublishError(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static void AddPublishDiagnostic(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            CharacterTestRuntimeAiLabPlayerLegalityReason reason)
        {
            CharacterTestRuntimeAiLabCardDiagnosticSeverity severity =
                reason.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error
                    ? CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error
                    : CharacterTestRuntimeAiLabCardDiagnosticSeverity.Warning;
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                severity,
                reason.Code,
                "$.playerLegality",
                reason.Message));
        }

        private static bool HasIsolatedCanonicalProfile(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            if (group.Profile == null
                || group.Profile.Entries.Count != 1
                || group.Entries[0] == null
                || group.Entries[0].RuntimeEntry == null)
            {
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft authoring = group.Entries[0];
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtime = group.Profile.Entries[0];
            return ReferenceEquals(authoring.RuntimeEntry, runtime)
                && string.Equals(authoring.EntryId, runtime.EntryId, StringComparison.Ordinal)
                && string.Equals(authoring.BehaviorId, runtime.BehaviorId, StringComparison.Ordinal)
                && authoring.Order == runtime.Order
                && authoring.Disabled == runtime.Disabled;
        }

        private static void AddLogicDiagnostics(
            List<CharacterTestRuntimeAiLabCardDiagnostic> destination,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                destination.Add(source[i]);
        }

        private static bool TryValidatePersistedDecks(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            out string error)
        {
            for (int i = 0; i < snapshot.Decks.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck persisted = snapshot.Decks[i];
                CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result =
                    CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                        new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                            persisted.CharacterStableId,
                            persisted.StableCardIds,
                            persisted.CharacterNumericId),
                        catalog,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
                if (!result.Success)
                {
                    error = FirstDiagnostic(result.Diagnostics);
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput BuildLabDiagnosticsInput(
            int characterId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            var provided = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>(8)
            {
                CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback
            };
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorEntryDraft> entries = group?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = entries[i];
                if (entry == null
                    || entry.Disabled
                    || cards == null
                    || !cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
                    || draft == null
                    || !draft.HasPublishedSource
                    || !_catalog.TryResolveLatestCompatible(
                        draft.PublishedStableId,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published))
                {
                    continue;
                }

                if (published.TacticalSource != null)
                {
                    foreach (var capability in published.Metadata.RequiredCapabilities)
                        if (!provided.Contains(capability)) provided.Add(capability);
                }
                else AddProvidedCapabilities(provided, published.CloneLogic());
            }
            return new CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput(
                ResolveCharacterCapabilities(characterId),
                provided);
        }

        private static IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> ResolveCharacterCapabilities(
            int characterId)
        {
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            if (!definitions.TryGet(characterId, out CharacterTestCharacterDefinition definition))
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            return definition.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId
                ? CodexFlowCharacterCapabilities
                : Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
        }

        private static void AddProvidedCapabilities(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> target,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabLogicStage> stages = logic?.Stages;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = stages[i];
                if (stage == null)
                    continue;
                switch (stage.Kind)
                {
                    case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                        AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage);
                        AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit);
                        AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation);
                        break;
                    case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                        AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage);
                        break;
                    case CharacterTestRuntimeAiLabCardStepKind.Wait:
                        AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);
                        break;
                }
            }
        }

        private static void AddLegacyProvidedCapabilities(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> target,
            CharacterTestRuntimeAiPlannerActionKind actionKind)
        {
            switch (actionKind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage);
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit);
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Kite);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Defense);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Wait:
                    AddCapability(target, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);
                    break;
            }
        }

        private static void AddCapability(
            List<CharacterTestRuntimeAiPlannerBehaviorCapability> target,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            if (capability != CharacterTestRuntimeAiPlannerBehaviorCapability.None
                && !target.Contains(capability))
                target.Add(capability);
        }

        /// <remarks>LowFreqAlloc: explicit player command at the pre-battle composition boundary.</remarks>
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreviewResult TryPreviewRecommendedPreset()
        {
            return Workbench.TryPreviewPreset(
                CreateRecommendedPreset(),
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault());
        }

        /// <remarks>LowFreqAlloc: explicit player confirmation at the pre-battle composition boundary.</remarks>
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryConfirmRecommendedPreset(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview)
        {
            return Workbench.TryConfirmPreset(preview);
        }

        /// <remarks>LowFreqAlloc: explicit one-shot player undo at the pre-battle composition boundary.</remarks>
        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryUndoRecommendedPreset()
        {
            return Workbench.TryUndoLastPreset();
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition CreateRecommendedPreset()
        {
            return new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                1,
                1,
                new[]
                {
                    ApproachAndAttackCardId,
                    IdleCardId
                });
        }

        internal bool TryResolvePresentationEntityId(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out int entityId)
        {
            entityId = 0;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && entity.Identity.CharacterId == _characterId
                    && entity.Team.TeamId == _targets[_selectedTargetIndex].TeamId
                    && entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    int candidateId = entity.Id.Value;
                    if (candidateId > 0 && (entityId == 0 || candidateId < entityId))
                        entityId = candidateId;
                }
            }
            return entityId > 0;
        }

        public bool TrySelectCharacter(int targetIndex, out string error)
        {
            if (targetIndex < 0 || targetIndex >= _targets.Length)
            {
                error = "Behavior deck character selection is out of range.";
                return false;
            }
            if (targetIndex == _selectedTargetIndex)
            {
                error = string.Empty;
                return true;
            }

            int previousIndex = _selectedTargetIndex;
            SelectTargetFields(targetIndex);
            if (!TryBuildWorkbench(_recoveryDiagnostics, out error))
            {
                SelectTargetFields(previousIndex);
                return false;
            }
            if (_sessionLocks != null)
                Workbench.LockSession();
            return true;
        }

        /// <summary>
        /// Atomically compiles and installs the authority-pinned deck into the existing Logic v2 runtime
        /// overlay before exposing the immutable session lock to gameplay.
        /// </summary>
        /// <remarks>LowFreqAlloc: pre-battle session boundary only.</remarks>
        public bool TryLockAndInstallForSession(
            CharacterTestRuntimeAiPlannerCommandModule commandModule,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out string error)
        {
            if (_sessionLocks != null)
            {
                error = string.Empty;
                return true;
            }
            if (commandModule == null)
            {
                error = "Runtime AI Planner command module is unavailable.";
                return false;
            }
            var requests = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[_targets.Length];
            if (TacticalCard.CharacterTacticalServices.Player != null && !SyncTacticalInventory(out error)) return false;
            var locks = new CharacterTestRuntimeAiPlannerSessionDeckLock[_targets.Length];
            for (int i = 0; i < _targets.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckTarget target = _targets[i];
                if (!TryResolveProfileSlot(
                        entities,
                        target.CharacterId,
                        target.TeamId,
                        target.PreferredProfileSlot,
                        out int profileSlot))
                {
                    error = "Player behavior deck target character is unavailable: " + target.CharacterStableId;
                    return false;
                }
                if (!_participant.CurrentSnapshot.TryGetDeck(
                        target.CharacterStableId,
                        out CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck))
                {
                    persistedDeck = new CharacterTestRuntimeAiPlannerPersistedDeck(
                        target.CharacterStableId,
                        Array.Empty<int>(),
                        Array.Empty<string>(),
                        target.CharacterId,
                        _catalog.CatalogNumericId,
                        _catalog.NumericIdentityHash);
                }
                if (!TryCreateSessionLock(persistedDeck, out locks[i], out error))
                    return false;

                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile;
                CharacterTestRuntimeAiLabCompiledLogicProgram[] programs = null;
                CharacterTestTacticalCardInputs tacticalInputs = null;
                bool usesTactical = false;
                foreach (var pinned in locks[i].Cards)
                    if (_catalog.TryGetExact(pinned.StableCardId, pinned.Revision, out var selected) && selected.TacticalSource != null)
                        usesTactical = true;
                var cardInputs = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
                if (usesTactical && TacticalCard.CharacterTacticalServices.Player != null)
                {
                    foreach (var pinned in locks[i].Cards)
                    {
                        if (!_catalog.TryGetExact(pinned.StableCardId, pinned.Revision, out var selected) || selected.TacticalSource == null) continue;
                        if (!commandModule.TryPreparePublishedTacticalInputs(profileSlot, pinned.StableCardId, out var input, out error)) return false;
                        cardInputs.Add(pinned.StableCardId, input);
                    }
                }
                else if (usesTactical && !commandModule.TryPreparePublishedTacticalInputs(profileSlot, out tacticalInputs, out error))
                    return false;
                if (locks[i].Cards.Count == 0)
                {
                    profile = CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.CreateFallbackOnlyProfile(
                        locks[i].CharacterStableId);
                }
                else if (!CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.TryCompile(
                             locks[i],
                             _catalog,
                             out profile,
                             out programs,
                             out error, tacticalInputs, cardInputs))
                {
                    return false;
                }
                requests[i] = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(
                    profileSlot,
                    profile,
                    programs,
                    locks[i].AuthorityHash);
            }

            if (!commandModule.TryApplyBehaviorCompositionOverlayBatch(
                    requests,
                    out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result))
            {
                error = string.IsNullOrEmpty(result.ReportText)
                    ? "Player behavior deck runtime installation failed."
                    : result.ReportText;
                return false;
            }

            _sessionLocks = locks;
            Workbench.LockSession();
            error = string.Empty;
            return true;
        }

        /// <remarks>LowFreqAlloc: offline CharacterTest session bootstrap only.</remarks>
        public static bool TryCreate(
            string filePath,
            int characterId,
            out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            out string error)
        {
            return TryCreate(filePath, characterId, 0, out service, out error);
        }

        /// <remarks>LowFreqAlloc: offline CharacterTest session bootstrap only.</remarks>
        public static bool TryCreate(
            string filePath,
            int characterId,
            int preferredProfileSlot,
            out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            out string error)
        {
            return TryCreate(
                filePath,
                new[] { new CharacterTestRuntimeAiPlannerPlayerDeckTarget(characterId, preferredProfileSlot) },
                out service,
                out error);
        }

        internal static bool TryCreate(
            string filePath,
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckTarget> targets,
            out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            out string error,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            service = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "Player behavior deck save path is required.";
                return false;
            }

            CharacterTestRuntimeAiPlannerPlayerDeckTarget[] normalizedTargets =
                NormalizeTargets(targets);
            if (normalizedTargets.Length == 0)
            {
                error = "At least one explicit player behavior deck character is required.";
                return false;
            }

            if (!TryCreateCatalog(
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog foundationCatalog,
                    out CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
                    out error,
                    actionCatalog))
                return false;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = foundationCatalog;
            string catalogFilePath = ResolveCatalogFilePath(filePath);
            string catalogLoadWarning = string.Empty;
            bool legacyCatalogMigrated = false;
            if (File.Exists(catalogFilePath))
            {
                try
                {
                    string catalogJson = File.ReadAllText(catalogFilePath);
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult loadCatalog =
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                            catalogJson,
                            references);
                    if (!loadCatalog.Success)
                    {
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult migrated =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson
                                .LoadForExplicitLegacyMigration(catalogJson, references);
                        if (!migrated.Success)
                        {
                            catalogLoadWarning =
                                "已忽略不兼容的旧本地行为牌组目录："
                                + FirstLogicDiagnostic(migrated.Diagnostics);
                        }
                        else
                        {
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog
                                migrationCandidate = migrated.Catalog;
                            if (!TryValidateCatalogLineage(
                                    foundationCatalog,
                                    migrationCandidate,
                                    out _))
                            {
                                if (!TryRebaseAuthenticatedLegacyCatalog(
                                        foundationCatalog,
                                        migrationCandidate,
                                        references,
                                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog
                                            rebasedCandidate,
                                        out string rebaseError))
                                {
                                    catalogLoadWarning =
                                        "已忽略不兼容的旧本地行为牌组目录："
                                        + rebaseError;
                                }
                                else
                                {
                                    migrationCandidate = rebasedCandidate;
                                }
                            }
                            if (catalogLoadWarning.Length == 0
                                && !TryBackupLegacyCatalog(
                                         catalogFilePath,
                                         out _,
                                         out string backupError))
                            {
                                catalogLoadWarning =
                                    "旧本地行为牌组目录已通过迁移校验，但原文件无法备份："
                                    + backupError;
                            }
                            else if (catalogLoadWarning.Length == 0)
                            {
                                catalog = migrationCandidate;
                                legacyCatalogMigrated = true;
                            }
                        }
                    }
                    else if (!TryValidateCatalogLineage(
                                 foundationCatalog,
                                 loadCatalog.Catalog,
                                 out string lineageError))
                    {
                        catalogLoadWarning =
                            "已忽略不兼容的旧本地行为牌组目录：" + lineageError;
                    }
                    else
                    {
                        catalog = loadCatalog.Catalog;
                    }
                }
                catch (Exception exception)
                {
                    catalogLoadWarning =
                        "已忽略无法读取的旧本地行为牌组目录：" + exception.Message;
                }
            }

            if (!TryMigrateLivingTargetConditions(
                    catalog,
                    references,
                    out catalog,
                    out bool catalogMigrated,
                    out error))
            {
                return false;
            }

            string[] unlockedIds = CopyStableCardIds(foundationCatalog.Cards);
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlockedResult =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(unlockedIds);
            if (!unlockedResult.Success)
            {
                error = FirstDiagnostic(unlockedResult.Diagnostics);
                return false;
            }

            var emptyDecks = new CharacterTestRuntimeAiPlannerPersistedDeck[normalizedTargets.Length];
            for (int i = 0; i < normalizedTargets.Length; i++)
            {
                emptyDecks[i] = new CharacterTestRuntimeAiPlannerPersistedDeck(
                    normalizedTargets[i].CharacterStableId,
                    Array.Empty<int>(),
                    Array.Empty<string>(),
                    normalizedTargets[i].CharacterId,
                    foundationCatalog.CatalogNumericId,
                    foundationCatalog.NumericIdentityHash);
            }
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult defaultSnapshot =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlockedResult.Collection,
                    emptyDecks);
            if (!defaultSnapshot.Success)
            {
                error = FirstDiagnostic(defaultSnapshot.Diagnostics);
                return false;
            }

            var participant = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
                defaultSnapshot.Snapshot,
                catalog,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
            string deckLoadWarning = string.Empty;
            string recoveryBackupPath = string.Empty;
            bool writeBlockedByRecovery = false;
            if (File.Exists(filePath))
            {
                try
                {
                    RuntimeSaveStateResult<RuntimeSaveState> load =
                        RuntimeSaveStateJson.LoadFromJson(File.ReadAllText(filePath));
                    if (!load.Success)
                    {
                        deckLoadWarning = load.Error?.Message ?? "Player behavior deck save could not be parsed.";
                    }
                    else
                    {
                        RuntimeSaveStateResult<bool> restore = participant.RestoreSaveState(load.Value);
                        if (!restore.Success)
                            deckLoadWarning = restore.Error?.Message ?? "Player behavior deck save could not be restored.";
                    }
                }
                catch (Exception exception)
                {
                    deckLoadWarning = "Player behavior deck save could not be read: " + exception.Message;
                }
            }

            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] recoveryDiagnostics =
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (deckLoadWarning.Length > 0)
            {
                if (TryBackupCorruptSave(filePath, out recoveryBackupPath, out string backupError))
                {
                    deckLoadWarning += " The original file was preserved at " + recoveryBackupPath + ".";
                }
                else
                {
                    writeBlockedByRecovery = true;
                    deckLoadWarning += " Editing is blocked because the original file could not be backed up: " + backupError;
                }
                recoveryDiagnostics = new[]
                {
                    new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                        "PLAYER_DECK_PERSISTENCE_RESTORE_FAILED",
                        "$.persistence.file",
                        deckLoadWarning)
                };
            }
            string loadWarning = CombineLoadWarnings(catalogLoadWarning, deckLoadWarning);
            if (catalogLoadWarning.Length > 0)
            {
                var combinedDiagnostics = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[
                    recoveryDiagnostics.Length + 1];
                combinedDiagnostics[0] = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    "PLAYER_DECK_CATALOG_RECOVERY",
                    "$.publishedCatalog",
                    catalogLoadWarning);
                Array.Copy(recoveryDiagnostics, 0, combinedDiagnostics, 1, recoveryDiagnostics.Length);
                recoveryDiagnostics = combinedDiagnostics;
            }

            service = new CharacterTestRuntimeAiPlannerLocalPlayerDeckService(
                filePath,
                catalogFilePath,
                normalizedTargets,
                catalog,
                participant,
                references,
                loadWarning,
                recoveryBackupPath,
                writeBlockedByRecovery,
                recoveryDiagnostics);
            if ((catalogMigrated || legacyCatalogMigrated)
                && catalogLoadWarning.Length == 0
                && !service.TryWriteCatalog(catalog, out error))
            {
                service = null;
                return false;
            }
            if (!service.TryBuildWorkbench(recoveryDiagnostics, out error))
            {
                service = null;
                return false;
            }
            TacticalCard.CharacterTacticalServices.RegisterLegacy(service);
            return true;
        }

        private static string CombineLoadWarnings(string catalogWarning, string deckWarning)
        {
            if (string.IsNullOrEmpty(catalogWarning))
                return deckWarning ?? string.Empty;
            if (string.IsNullOrEmpty(deckWarning))
                return catalogWarning;
            return catalogWarning + " " + deckWarning;
        }

        private static bool TryMigrateLivingTargetConditions(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog sourceCatalog,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog migratedCatalog,
            out bool migrated,
            out string error)
        {
            migratedCatalog = sourceCatalog;
            migrated = false;
            error = string.Empty;
            return true;
        }

        private static bool HasLivingTargetCondition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            for (int i = 0; logic != null && i < logic.ActivationConditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = logic.ActivationConditions[i];
                if ((condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDead
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && !condition.BoolValue)
                    || IsLivingTargetState(condition))
                {
                    return true;
                }
            }
            return HasLivingTargetCondition(logic?.ActivationConditionRoot);
        }

        private static bool HasLivingTargetCondition(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null)
                return false;
            CharacterTestRuntimeAiLabCardCondition condition = node.Predicate;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                && condition != null
                && ((condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDead
                        && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                        && !condition.BoolValue)
                    || IsLivingTargetState(condition)))
            {
                return true;
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (HasLivingTargetCondition(node.Children[i]))
                    return true;
            }
            return false;
        }

        private static bool IsLivingTargetState(CharacterTestRuntimeAiLabCardCondition condition) =>
            condition != null
            && condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState
            && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
            && condition.AllowedTargetPhases
                == (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
            && condition.RequiredTargetFlags == 0u
            && condition.ForbiddenTargetFlags == 0u;

        private static void AppendActivationCondition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            CharacterTestRuntimeAiLabCardCondition condition)
        {
            var leaf = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = condition.ConditionId,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = condition.Clone()
            };
            var conditionNode = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.scope.0." + condition.ConditionId,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
            };
            conditionNode.Children.Add(leaf);
            if (logic.ActivationConditionRoot == null)
            {
                logic.ActivationConditionRoot = conditionNode;
                return;
            }
            if (logic.ActivationConditionRoot.Kind
                == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All)
            {
                logic.ActivationConditionRoot.Children.Add(conditionNode);
                return;
            }

            var root = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.activation.all",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            root.Children.Add(logic.ActivationConditionRoot);
            root.Children.Add(conditionNode);
            logic.ActivationConditionRoot = root;
        }

        private static string ResolveLivingTargetConditionId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            string candidate = LivingTargetConditionId;
            int suffix = 1;
            while (ContainsConditionId(logic, candidate))
                candidate = LivingTargetConditionId + "." + (suffix++).ToString(CultureInfo.InvariantCulture);
            return candidate;
        }

        private static bool ContainsConditionId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            string conditionId)
        {
            for (int i = 0; logic != null && i < logic.ActivationConditions.Count; i++)
            {
                if (string.Equals(
                        logic.ActivationConditions[i].ConditionId,
                        conditionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return ContainsConditionId(logic?.ActivationConditionRoot, conditionId);
        }

        private static bool ContainsConditionId(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string conditionId)
        {
            if (node == null)
                return false;
            if (string.Equals(node.NodeId, conditionId, StringComparison.Ordinal)
                || string.Equals(node.Predicate?.ConditionId, conditionId, StringComparison.Ordinal))
            {
                return true;
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (ContainsConditionId(node.Children[i], conditionId))
                    return true;
            }
            return false;
        }

        private bool TryCreateSessionLock(
            CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck,
            out CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock,
            out string error)
        {
            sessionLock = null;
            if (persistedDeck == null)
            {
                error = "Player behavior deck is unavailable.";
                return false;
            }

            if (persistedDeck.CardNumericIds.Count != persistedDeck.StableCardIds.Count)
            {
                error = "Player behavior deck persistence is missing card numeric identities; run the explicit authoring migration before session preparation.";
                return false;
            }

            var request = new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                LocalAuthorityPeerId,
                persistedDeck.CharacterStableId,
                persistedDeck.CharacterNumericId,
                _catalog.CatalogId,
                _catalog.Revision,
                _catalog.CatalogNumericId,
                _catalog.NumericIdentityHash,
                _catalog.CatalogHash,
                persistedDeck.CardNumericIds,
                persistedDeck.StableCardIds);
            var authorityContext = new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                LocalAuthorityPeerId,
                new[] { persistedDeck.CharacterStableId },
                _participant.CurrentSnapshot.UnlockedCards,
                _catalog,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result =
                CharacterTestRuntimeAiPlannerSessionAuthority.ValidateAndLock(request, authorityContext);
            if (!result.Success)
            {
                error = FirstAuthorityDiagnostic(result.Diagnostics);
                return false;
            }
            sessionLock = result.SessionLock;
            error = string.Empty;
            return true;
        }

        private static bool TryResolveProfileSlot(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int characterId,
            int teamId,
            int preferredProfileSlot,
            out int profileSlot)
        {
            if (entities == null || entities.Count == 0)
            {
                profileSlot = preferredProfileSlot;
                return true;
            }

            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && entity.Identity.CharacterId == characterId
                    && entity.Team.TeamId == teamId
                    && entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    profileSlot = Math.Max(0, entity.Control.RuntimeAiPlannerProfileSlot);
                    return true;
                }
            }
            profileSlot = 0;
            return false;
        }

        public bool TryPersist(
            CharacterTestRuntimeAiPlannerPersistedDeck deck,
            out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic)
        {
            if (TacticalInventory != null)
            {
                diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_TACTICAL_INVENTORY_OWNS_PROGRESS", "$.inventory", "请使用角色战术内容界面维护持有清单与卡槽绑定。");
                return false;
            }
            if (_writeBlockedByRecovery)
            {
                diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_PERSISTENCE_RECOVERY_REQUIRED",
                    "$.persistence.file",
                    "Behavior deck editing is blocked until the damaged save file can be preserved.");
                return false;
            }
            if (deck == null || !IsKnownTarget(deck.CharacterStableId))
            {
                diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_PERSISTENCE_CHARACTER_MISMATCH",
                    "$.persistence.characterStableId",
                    "Behavior deck save target does not match the active player character.");
                return false;
            }

            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult candidate =
                BuildCandidateSnapshot(_participant.CurrentSnapshot, deck);
            if (!candidate.Success)
            {
                diagnostic = FirstError(candidate.Diagnostics);
                return false;
            }

            // A successful user save is the first authorized point at which a stale local
            // catalog recovery may be replaced by the current foundation identity. Keep a
            // byte-for-byte rollback copy so a later deck write failure cannot partially
            // consume the stale-catalog recovery state.
            bool catalogExisted = File.Exists(_catalogFilePath);
            byte[] catalogRollbackBytes = null;
            if (catalogExisted)
            {
                try
                {
                    catalogRollbackBytes = File.ReadAllBytes(_catalogFilePath);
                }
                catch (Exception exception)
                {
                    diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "PLAYER_DECK_CATALOG_BACKUP_FAILED",
                        "$.publishedCatalog",
                        "Published behavior-card catalog could not be preserved before save: "
                            + exception.Message);
                    return false;
                }
            }
            if (!TryWriteCatalog(_catalog, out string catalogWriteError))
            {
                diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_CATALOG_WRITE_FAILED",
                    "$.publishedCatalog",
                    catalogWriteError);
                return false;
            }

            string temporaryPath = _filePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var candidateParticipant = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
                    candidate.Snapshot,
                    _catalog,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
                RuntimeSaveStateResult<RuntimeSaveState> capture = candidateParticipant.CaptureSaveState();
                if (!capture.Success)
                    throw new InvalidOperationException(capture.Error?.Message ?? "Deck save capture failed.");
                File.WriteAllText(temporaryPath, RuntimeSaveStateJson.SaveToJson(capture.Value), Utf8WithoutBom);
                if (File.Exists(_filePath))
                    File.Replace(temporaryPath, _filePath, null);
                else
                    File.Move(temporaryPath, _filePath);
                _participant.ReplaceSnapshot(candidate.Snapshot);
                diagnostic = null;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                TryRestoreCatalogAfterFailedDeckWrite(
                    catalogExisted,
                    catalogRollbackBytes,
                    out string rollbackError);
                diagnostic = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_PERSISTENCE_WRITE_FAILED",
                    "$.persistence.file",
                    "Behavior deck could not be saved: " + exception.Message
                        + (string.IsNullOrEmpty(rollbackError)
                            ? string.Empty
                            : " Catalog rollback also failed: " + rollbackError));
                return false;
            }
        }

        private bool TryRestoreCatalogAfterFailedDeckWrite(
            bool catalogExisted,
            byte[] catalogRollbackBytes,
            out string error)
        {
            string rollbackPath = _catalogFilePath + ".rollback.tmp";
            try
            {
                if (!catalogExisted)
                {
                    if (File.Exists(_catalogFilePath))
                        File.Delete(_catalogFilePath);
                    error = string.Empty;
                    return true;
                }

                File.WriteAllBytes(rollbackPath, catalogRollbackBytes ?? Array.Empty<byte>());
                if (File.Exists(_catalogFilePath))
                    File.Replace(rollbackPath, _catalogFilePath, null);
                else
                    File.Move(rollbackPath, _catalogFilePath);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                TryDelete(rollbackPath);
            }
        }

        private void SelectTargetFields(int targetIndex)
        {
            _selectedTargetIndex = targetIndex;
            if (_targets.Length == 0)
            {
                _characterId = 0;
                _characterNumericId = 0;
                _preferredProfileSlot = 0;
                _characterStableId = string.Empty;
                return;
            }
            CharacterTestRuntimeAiPlannerPlayerDeckTarget target = _targets[targetIndex];
            _characterId = target.CharacterId;
            _characterNumericId = target.CharacterId;
            _preferredProfileSlot = target.PreferredProfileSlot;
            _characterStableId = target.CharacterStableId;
        }

        private bool TryBuildWorkbench(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> extraDiagnostics,
            out string error)
        {
            if (!TryCreateWorkbench(
                    _catalog,
                    _participant,
                    extraDiagnostics,
                    out CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
                    out error))
            {
                return false;
            }
            Workbench = workbench;
            return true;
        }

        private bool TryCreateWorkbench(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerDeckPersistenceParticipant participant,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> extraDiagnostics,
            out CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            out string error)
        {
            workbench = null;
            if (!participant.CurrentSnapshot.TryGetDeck(
                    _characterStableId,
                    out CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck))
            {
                persistedDeck = new CharacterTestRuntimeAiPlannerPersistedDeck(
                    _characterStableId,
                    Array.Empty<int>(),
                    Array.Empty<string>(),
                    _characterNumericId,
                    catalog.CatalogNumericId,
                    catalog.NumericIdentityHash);
            }
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult result =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    participant.CurrentSnapshot.UnlockedCards,
                    persistedDeck,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    null,
                    this,
                    extraDiagnostics);
            if (!result.Success)
            {
                error = FirstDiagnostic(result.Diagnostics);
                return false;
            }
            workbench = result.Workbench;
            error = string.Empty;
            return true;
        }

        private static string ResolveCatalogFilePath(string deckFilePath)
        {
            string directory = Path.GetDirectoryName(deckFilePath);
            string fileName = Path.GetFileNameWithoutExtension(deckFilePath);
            return Path.Combine(
                string.IsNullOrEmpty(directory) ? "." : directory,
                fileName + ".published-behavior-cards.json");
        }

        private static bool TryRebaseAuthenticatedLegacyCatalog(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog foundation,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog legacy,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog rebased,
            out string error)
        {
            rebased = null;
            error = string.Empty;
            if (foundation == null
                || legacy == null
                || !string.Equals(foundation.CatalogId, legacy.CatalogId, StringComparison.Ordinal))
            {
                error = "Published behavior-card catalog identity does not match the CharacterTest authority.";
                return false;
            }

            int runtimeVersion =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                foundation.Cards.Count + legacy.Cards.Count);
            var nextRevisionByStableId = new Dictionary<string, int>(StringComparer.Ordinal);
            var latestIdentityByStableId = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < foundation.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = foundation.Cards[i];
                cards.Add(card);
                nextRevisionByStableId[card.StableCardId] = Math.Max(
                    nextRevisionByStableId.TryGetValue(card.StableCardId, out int revision)
                        ? revision
                        : 0,
                    card.Revision);
                latestIdentityByStableId[card.StableCardId] =
                    NormalizedPublishedCardIdentity(card, runtimeVersion);
            }

            int appendedCount = 0;
            for (int i = 0; i < legacy.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = legacy.Cards[i];
                if (!nextRevisionByStableId.TryGetValue(
                        source.StableCardId,
                        out int latestRevision)
                    || !latestIdentityByStableId.TryGetValue(
                        source.StableCardId,
                        out long latestIdentity))
                {
                    error = "Published behavior-card catalog introduced an unknown stable card id: "
                        + source.StableCardId;
                    return false;
                }

                long identity = NormalizedPublishedCardIdentity(source, runtimeVersion);
                if (identity == latestIdentity)
                    continue;
                if (latestRevision == int.MaxValue)
                {
                    error = "Published behavior-card revision range is exhausted.";
                    return false;
                }

                CharacterTestRuntimeAiLabBehaviorCardDraft draft =
                    CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(source);
                draft.LogicPayload = source.CloneLogic();
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                        draft,
                        new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                            source.StableCardId,
                            latestRevision + 1,
                            runtimeVersion,
                            runtimeVersion,
                            source.Metadata),
                        references);
                if (!publish.Success)
                {
                    error = FirstLogicDiagnostic(publish.Diagnostics);
                    return false;
                }

                cards.Add(publish.Card);
                nextRevisionByStableId[source.StableCardId] = latestRevision + 1;
                latestIdentityByStableId[source.StableCardId] = identity;
                appendedCount++;
            }

            if (foundation.Revision > int.MaxValue - appendedCount)
            {
                error = "Published behavior-card catalog revision range is exhausted.";
                return false;
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    foundation.CatalogId,
                    foundation.Revision + appendedCount,
                    cards,
                    references,
                    foundation.CatalogNumericId);
            if (!create.Success)
            {
                error = FirstLogicDiagnostic(create.Diagnostics);
                return false;
            }
            rebased = create.Catalog;
            return true;
        }

        private static long NormalizedPublishedCardIdentity(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card,
            int runtimeVersion)
        {
            return CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                card.StableCardId,
                revision: 1,
                minimumRuntimeContractVersion: runtimeVersion,
                maximumRuntimeContractVersion: runtimeVersion,
                card.Metadata,
                card.SemanticHash);
        }

        private static bool TryValidateCatalogLineage(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog foundation,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog restored,
            out string error)
        {
            if (restored == null
                || !string.Equals(restored.CatalogId, CatalogId, StringComparison.Ordinal))
            {
                error = "Published behavior-card catalog identity does not match the CharacterTest authority.";
                return false;
            }
            if (restored.Cards.Count < foundation.Cards.Count)
            {
                error = "Published behavior-card catalog is missing foundation revisions.";
                return false;
            }

            for (int i = 0; i < foundation.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard required = foundation.Cards[i];
                if (!restored.TryGetExact(required.StableCardId, required.Revision, out var actual)
                    || actual.ContentHash != required.ContentHash
                    || actual.SemanticHash != required.SemanticHash)
                {
                    error = "Published behavior-card foundation identity changed: "
                        + required.StableCardId;
                    return false;
                }
            }

            string previousStableId = string.Empty;
            int expectedRevision = 0;
            for (int i = 0; i < restored.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = restored.Cards[i];
                if (!string.Equals(previousStableId, card.StableCardId, StringComparison.Ordinal))
                {
                    previousStableId = card.StableCardId;
                    expectedRevision = 1;
                    if (!foundation.TryResolveLatestCompatible(
                            card.StableCardId,
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                            out _))
                    {
                        error = "Published behavior-card catalog introduced an unknown stable card id: "
                            + card.StableCardId;
                        return false;
                    }
                }
                if (card.Revision != expectedRevision)
                {
                    error = "Published behavior-card revision history is not contiguous: "
                        + card.StableCardId;
                    return false;
                }
                expectedRevision++;
            }

            int appendedRevisionCount = restored.Cards.Count - foundation.Cards.Count;
            if (restored.Revision != foundation.Revision + appendedRevisionCount)
            {
                error = "Published behavior-card catalog revision does not match its append-only history.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool IsKnownTarget(string characterStableId)
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (string.Equals(
                        _targets[i].CharacterStableId,
                        characterStableId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckTarget[] NormalizeTargets(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckTarget> targets)
        {
            var values = new List<CharacterTestRuntimeAiPlannerPlayerDeckTarget>();
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; targets != null && i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckTarget target = targets[i];
                if (target.CharacterId <= 0)
                    continue;
                bool duplicate = false;
                for (int j = 0; j < values.Count; j++)
                {
                    if (values[j].CharacterId == target.CharacterId)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    string displayName = target.DisplayName;
                    if (displayName.Length == 0
                        && definitions.TryGet(
                            target.CharacterId,
                            out CharacterTestCharacterDefinition definition))
                    {
                        displayName = definition.DisplayName;
                    }
                    if (displayName.Length == 0)
                    {
                        displayName = "角色 "
                            + target.CharacterId.ToString(CultureInfo.InvariantCulture);
                    }
                    values.Add(new CharacterTestRuntimeAiPlannerPlayerDeckTarget(
                        target.CharacterId,
                        target.PreferredProfileSlot,
                        target.TeamId,
                        displayName));
                }
            }
            values.Sort((left, right) => left.CharacterId.CompareTo(right.CharacterId));
            return values.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult BuildCandidateSnapshot(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot source,
            CharacterTestRuntimeAiPlannerPersistedDeck replacement)
        {
            var decks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>(source.Decks.Count + 1);
            bool replaced = false;
            for (int i = 0; i < source.Decks.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck existing = source.Decks[i];
                if (string.Equals(existing.CharacterStableId, replacement.CharacterStableId, StringComparison.Ordinal))
                {
                    decks.Add(replacement);
                    replaced = true;
                }
                else
                {
                    decks.Add(existing);
                }
            }
            if (!replaced)
                decks.Add(replacement);
            return CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                source.UnlockedCards,
                decks);
        }

        internal static bool TryCreateCatalog(
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            out CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out string error,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            catalog = null;
            error = string.Empty;
            CharacterActionConfigRegistry actionRegistry = actionCatalog != null
                ? actionCatalog.Registry
                : CharacterTestCharacterActionConfigSource.Load().Registry;
            references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRegistry);

            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
            if (state?.Success != true)
            {
                error = state?.Diagnostic ?? "版本化基础行为卡目录不可用。";
                return false;
            }
            if (!state.CardCatalog.TryResolveLatestCompatible(
                    ApproachAndAttackCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                        .CurrentRuntimeContractVersion,
                    out _)
                || !state.CardCatalog.TryResolveLatestCompatible(
                    IdleCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                        .CurrentRuntimeContractVersion,
                    out _))
            {
                error = "正式目录必须同时包含“靠近并普攻”和“发呆”两张基础行为卡。";
                return false;
            }

            catalog = state.CardCatalog;
            return true;
        }


        private static bool TryPublish(
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            string stableCardId,
            string displayName,
            int cost,
            string description,
            string[] tags,
            CharacterTestRuntimeAiPlannerBehaviorCapability[] capabilities,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card,
            out string error)
        {
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                stableCardId,
                displayName,
                "Player",
                payload);
            draft.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Templates.ConfigureNativeCard(
                draft.LogicPayload,
                stableCardId);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                displayName,
                "ui." + stableCardId,
                cost,
                description,
                description,
                payload.RiskSummary,
                tags,
                capabilities);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        stableCardId,
                        1,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                        metadata),
                    references);
            card = result.Card;
            error = result.Success ? string.Empty : FirstLogicDiagnostic(result.Diagnostics);
            return result.Success;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardPayload CreatePrimaryAttackPayload()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    "Primary Attack",
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack);
            payload.TriggerConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.target",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            payload.TriggerConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                Value = 0f,
                MaxValue = CharacterTestAiBehaviorProfileSource.DefaultAttackRange
            });
            payload.TriggerConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "trigger.idle",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = false
            });
            return payload;
        }

        private static string[] CopyStableCardIds(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> cards)
        {
            int uniqueCount = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == 0 || !string.Equals(
                        cards[i - 1].StableCardId,
                        cards[i].StableCardId,
                        StringComparison.Ordinal))
                {
                    uniqueCount++;
                }
            }
            var ids = new string[uniqueCount];
            int target = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == 0 || !string.Equals(
                        cards[i - 1].StableCardId,
                        cards[i].StableCardId,
                        StringComparison.Ordinal))
                {
                    ids[target++] = cards[i].StableCardId;
                }
            }
            return ids;
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic FirstError(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return diagnostics[i];
            }
            return new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                "PLAYER_DECK_PERSISTENCE_FAILED",
                "$.persistence",
                "Behavior deck could not be saved.");
        }

        private static string FirstDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            return diagnostics.Count > 0 ? diagnostics[0].Code + ": " + diagnostics[0].Message : "Unknown deck diagnostic.";
        }

        private static string FirstLogicDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            return diagnostics.Count > 0
                ? diagnostics[0].Code
                    + "@"
                    + diagnostics[0].Path
                    + ": "
                    + diagnostics[0].Message
                : "Unknown behavior card diagnostic.";
        }

        private static string FirstAuthorityDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            return diagnostics.Count > 0
                ? diagnostics[0].Code + ": " + diagnostics[0].Message
                : "Unknown behavior deck authority diagnostic.";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; the original save remains authoritative.
            }
        }

        private static bool TryBackupCorruptSave(
            string sourcePath,
            out string backupPath,
            out string error)
        {
            backupPath = string.Empty;
            error = string.Empty;
            try
            {
                string candidate = sourcePath + ".corrupt.bak";
                int suffix = 1;
                while (File.Exists(candidate))
                    candidate = sourcePath + ".corrupt." + (suffix++).ToString(CultureInfo.InvariantCulture) + ".bak";
                File.Copy(sourcePath, candidate, false);
                backupPath = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryBackupLegacyCatalog(
            string sourcePath,
            out string backupPath,
            out string error)
        {
            backupPath = string.Empty;
            error = string.Empty;
            try
            {
                string candidate = sourcePath + ".legacy-v8.bak";
                int suffix = 1;
                while (File.Exists(candidate))
                {
                    candidate = sourcePath
                        + ".legacy-v8."
                        + (suffix++).ToString(CultureInfo.InvariantCulture)
                        + ".bak";
                }
                File.Copy(sourcePath, candidate, false);
                backupPath = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
