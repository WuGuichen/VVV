using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerProjectCardDeleteResult
    {
        internal CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
            string stableCardId,
            CharacterTestRuntimeAiPlannerProjectCatalogState state,
            bool conflict,
            string diagnostic)
        {
            StableCardId = stableCardId ?? string.Empty;
            State = state;
            Conflict = conflict;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal string StableCardId { get; }
        internal CharacterTestRuntimeAiPlannerProjectCatalogState State { get; }
        internal bool Conflict { get; }
        internal string Diagnostic { get; }
        internal bool Success => StableCardId.Length > 0
            && State?.Success == true
            && Diagnostic.Length == 0;
    }

    internal sealed class CharacterTestRuntimeAiPlannerProjectGroupPublishResult
    {
        public CharacterTestRuntimeAiPlannerProjectGroupPublishResult(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            CharacterTestRuntimeAiPlannerProjectCatalogState state,
            bool conflict,
            string diagnostic)
        {
            Group = group;
            State = state;
            Conflict = conflict;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorGroupRevision Group { get; }
        public CharacterTestRuntimeAiPlannerProjectCatalogState State { get; }
        public bool Conflict { get; }
        public string Diagnostic { get; }
        public bool Success => Group != null && State != null && State.Success && Diagnostic.Length == 0;
    }

    internal sealed partial class CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession
    {
        private const string ProjectCardIdAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        private const int ProjectCardIdRandomLength = 10;
        private const int ProjectCardIdGenerationAttempts = 128;

        private CharacterTestRuntimeAiPlannerProjectCatalogState _state;
        private List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> _lastDeletedCards;
        private string _lastDeletedStableCardId = string.Empty;
        private long _lastDeletePostCatalogHash;

        public CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
            CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            _state = state ?? CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            _hasPublishRecoveryJournal = System.IO.File.Exists(_publishRecoveryJournalPath);
        }

        public CharacterTestRuntimeAiPlannerProjectCatalogState State => _state;
        internal Func<CharacterTestRuntimeAiLabBehaviorCardDraft, CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult> CompileTacticalForPublish { get; set; }
        internal Func<CharacterTestRuntimeAiLabBehaviorCardDraft, CharacterTestTacticalPublishedSource> ResolveTacticalSource { get; set; }
        public bool CanWrite => _state != null && _state.Success && _state.CanWriteProjectCatalogs;
        public bool LastWriteConflict { get; private set; }
        public bool CanUndoLastCardDelete => CanWrite
            && _lastDeletedCards != null
            && _lastDeletedCards.Count > 0
            && _state.CardCatalog.CatalogHash == _lastDeletePostCatalogHash
            && LatestCardRevision(_lastDeletedStableCardId) == 0;

        /// <summary>Compares one mutable card draft with its immutable Published source without writing.</summary>
        /// <remarks>LowFreqAlloc: authoring presentation and publish preflight only.</remarks>
        internal bool TryHasUnpublishedCardChanges(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out bool hasUnpublishedChanges,
            out string diagnostic)
        {
            hasUnpublishedChanges = true;
            diagnostic = string.Empty;
            if (draft?.HasAuthoredSource == true && !draft.HasPublishedSource)
                return true;
            if (draft?.HasPublishedSource != true
                || _state?.CardCatalog == null
                || !_state.CardCatalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourcePublishedRevision,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source)
                || source.SemanticHash != draft.SourcePublishedSemanticHash
                || source.ContentHash != draft.SourcePublishedContentHash)
            {
                diagnostic = "行为卡草稿来源已变化或不可用。";
                return false;
            }
            if (references == null)
            {
                diagnostic = "行为卡引用目录不可用，无法比较 Published 内容。";
                return false;
            }

            if (draft.HasAuthoredSource && source.TacticalSource != null && ResolveTacticalSource != null)
            {
                try
                {
                    var authored = ResolveTacticalSource(draft);
                    var editable = BuildCanonicalCardMetadata(draft, source);
                    var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(editable.DisplayName, editable.IconKey,
                        editable.Cost, source.Metadata.ShortDescription, source.Metadata.UseSummary, source.Metadata.RiskSummary,
                        editable.Tags, source.Metadata.RequiredCapabilities, editable.Responsibilities, editable.CooldownSeconds);
                    hasUnpublishedChanges = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                        draft.PublishedStableId, draft.SourcePublishedRevision, source.MinimumRuntimeContractVersion,
                        source.MaximumRuntimeContractVersion, metadata, authored.SemanticHash) != source.ContentHash;
                    return true;
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is Newtonsoft.Json.JsonException)
                { diagnostic = error.Message; return true; }
            }

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult candidate =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        draft.PublishedStableId,
                        draft.SourcePublishedRevision,
                        source.MinimumRuntimeContractVersion,
                        source.MaximumRuntimeContractVersion,
                        BuildCanonicalCardMetadata(draft, source),
                        source.NumericId),
                    references, draft.HasAuthoredSource ? CompileTacticalForPublish?.Invoke(draft) : null);
            if (!candidate.Success)
            {
                diagnostic = candidate.Diagnostics.Count > 0
                    ? candidate.Diagnostics[0].Message
                    : "当前行为卡无法构建 canonical Published candidate。";
                return true;
            }

            hasUnpublishedChanges = candidate.Card.SemanticHash != draft.SourcePublishedSemanticHash
                || candidate.Card.ContentHash != draft.SourcePublishedContentHash;
            return true;
        }

        /// <remarks>LowFreqAlloc: explicit Editor publish transaction only.</remarks>
        public CharacterTestRuntimeAiLabPublishResult TryPublishCard(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            if (!CanWrite || draft == null || references == null)
                return FailedCard(CanWrite ? "行为卡草稿或引用目录不可用。" : ReadOnlyDiagnostic());
            bool firstTacticalPublish = draft.HasAuthoredSource && !draft.HasPublishedSource;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = null;
            if (firstTacticalPublish)
            {
                if (LatestCardRevision(draft.PublishedStableId) != 0)
                    return FailedCard("预留卡身份已存在，请重新加载并解决来源冲突。");
            }
            else if (!draft.HasPublishedSource
                || !_state.CardCatalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourcePublishedRevision,
                    out source)
                || source.SemanticHash != draft.SourcePublishedSemanticHash
                || source.ContentHash != draft.SourcePublishedContentHash)
            {
                return FailedCard("行为卡草稿来源已变化，请重新加载项目版本。");
            }
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult authoredCompilation =
                draft.HasAuthoredSource ? CompileTacticalForPublish?.Invoke(draft) : null;
            int nextRevision = LatestCardRevision(draft.PublishedStableId) + 1;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        draft.PublishedStableId,
                        nextRevision,
                        source?.MinimumRuntimeContractVersion
                            ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                        source?.MaximumRuntimeContractVersion
                            ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                        BuildCanonicalCardMetadata(draft, source),
                        source?.NumericId
                            ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(
                                draft.PublishedStableId)),
                    references,
                    authoredCompilation);
            if (!publish.Success)
                return new CharacterTestRuntimeAiLabPublishResult(null, null, CopyDiagnostics(publish.Diagnostics));

            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(_state.CardCatalog.Cards.Count + 1);
            for (int i = 0; i < _state.CardCatalog.Cards.Count; i++)
                cards.Add(_state.CardCatalog.Cards[i]);
            cards.Add(publish.Card);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    _state.CardCatalog.CatalogId,
                    _state.CardCatalog.Revision + 1,
                    cards,
                    references,
                    _state.CardCatalog.CatalogNumericId);
            if (!create.Success)
                return new CharacterTestRuntimeAiLabPublishResult(null, null, CopyDiagnostics(create.Diagnostics));
            Func<CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest,
                CharacterTestRuntimeAiPlannerProjectCatalogWriteResult> writeCatalog =
                _publishCurrentChangesWriteOverride
                ?? CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write;
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                writeCatalog(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        create.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(_state.GroupCatalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return FailedCard(write.Diagnostic);
            }

            Func<CharacterTestRuntimeAiPlannerProjectCatalogState> reloadCatalog =
                _publishCurrentChangesReloadOverride
                ?? (() => CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load());
            _state = reloadCatalog();
            if (!_state.Success
                || !_state.CardCatalog.TryGetExact(
                    publish.Card.StableCardId,
                    publish.Card.Revision,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard reloaded)
                || reloaded.ContentHash != publish.Card.ContentHash)
            {
                return FailedCard("行为卡目录已写入，但重新加载结果与提交版本不一致。");
            }
            return new CharacterTestRuntimeAiLabPublishResult(
                reloaded,
                _state.CardCatalog,
                Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        /// <remarks>LowFreqAlloc: explicit Editor creation of one native project behavior card.</remarks>
        public CharacterTestRuntimeAiLabPublishResult TryCreateCard(
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            if (!CanWrite || references == null)
                return FailedCard(CanWrite ? "行为卡引用目录不可用。" : ReadOnlyDiagnostic());

            string stableCardId;
            try
            {
                using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[sizeof(ulong)];
                    stableCardId = GenerateProjectCardStableId(
                        candidate => LatestCardRevision(candidate) > 0,
                        () =>
                        {
                            random.GetBytes(bytes);
                            return BitConverter.ToUInt64(bytes, 0);
                        });
                }
            }
            catch (Exception exception) when (
                exception is CryptographicException || exception is InvalidOperationException)
            {
                return FailedCard("创建行为卡短 ID 失败：" + exception.Message);
            }
            int ordinal = _state.CardCatalog.Cards.Count + 1;
            string displayName = "新行为卡 "
                + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                stableCardId,
                displayName,
                "自定义",
                new CharacterTestRuntimeAiLabBehaviorCardPayload())
            {
                LogicPayload = CreateInitialNativeLogic(),
                LogicPayloadMigratedFromV1 = false,
                IconKey = "ui.behavior-card.custom",
                Cost = 0,
                ShortDescription = "新建的行为卡，请在编辑逻辑中配置。",
                UseSummary = string.Empty,
                RiskSummary = string.Empty,
                TagsText = "自定义",
                RequiredCapabilities = Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>(),
                ResponsibilityKinds = new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                }
            };
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                draft.DisplayName,
                draft.IconKey,
                draft.Cost,
                draft.ShortDescription,
                draft.UseSummary,
                draft.RiskSummary,
                new[] { "自定义" },
                draft.RequiredCapabilities,
                draft.ResponsibilityKinds, draft.CooldownSeconds);
            int numericId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(stableCardId);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        stableCardId,
                        1,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                            .CurrentRuntimeContractVersion,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                            .CurrentRuntimeContractVersion,
                        metadata,
                        numericId),
                    references);
            if (!publish.Success)
                return new CharacterTestRuntimeAiLabPublishResult(
                    null,
                    null,
                    CopyDiagnostics(publish.Diagnostics));

            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                _state.CardCatalog.Cards.Count + 1);
            for (int i = 0; i < _state.CardCatalog.Cards.Count; i++)
                cards.Add(_state.CardCatalog.Cards[i]);
            cards.Add(publish.Card);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    _state.CardCatalog.CatalogId,
                    _state.CardCatalog.Revision + 1,
                    cards,
                    references,
                    _state.CardCatalog.CatalogNumericId);
            if (!create.Success)
                return new CharacterTestRuntimeAiLabPublishResult(
                    null,
                    null,
                    CopyDiagnostics(create.Diagnostics));

            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        create.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                            _state.GroupCatalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return FailedCard(write.Diagnostic);
            }

            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!_state.Success
                || !_state.CardCatalog.TryGetExact(
                    publish.Card.StableCardId,
                    1,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard reloaded)
                || reloaded.ContentHash != publish.Card.ContentHash)
            {
                return FailedCard("行为卡目录已写入，但新建版本未能精确重新加载。");
            }
            return new CharacterTestRuntimeAiLabPublishResult(
                reloaded,
                _state.CardCatalog,
                Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        /// <remarks>LowFreqAlloc: explicit Editor deletion of one unreferenced project card.</remarks>
        public CharacterTestRuntimeAiPlannerProjectCardDeleteResult TryDeleteCard(
            string stableCardId,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            string normalized = stableCardId?.Trim() ?? string.Empty;
            if (!CanWrite || references == null || normalized.Length == 0)
            {
                return FailedCardDelete(
                    normalized,
                    CanWrite ? "行为卡 ID 或引用目录不可用。" : ReadOnlyDiagnostic());
            }
            if (LatestCardRevision(normalized) <= 0)
                return FailedCardDelete(normalized, "项目行为卡不存在或已经删除。");

            var referencingGroups = new List<string>();
            for (int groupIndex = 0;
                 groupIndex < _state.GroupCatalog.Groups.Count;
                 groupIndex++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision group =
                    _state.GroupCatalog.Groups[groupIndex];
                bool referenced = false;
                for (int cardIndex = 0; cardIndex < group.Cards.Count; cardIndex++)
                {
                    if (string.Equals(
                        group.Cards[cardIndex].StableCardId,
                        normalized,
                        StringComparison.Ordinal))
                    {
                        referenced = true;
                        break;
                    }
                }
                if (referenced)
                {
                    referencingGroups.Add(
                        group.DisplayName
                        + " r"
                        + group.Revision.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (referencingGroups.Count > 0)
            {
                referencingGroups.Sort(StringComparer.Ordinal);
                return FailedCardDelete(
                    normalized,
                    "行为卡仍被正式行为组引用，不能删除："
                    + string.Join("、", referencingGroups));
            }

            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            var deletedCards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            for (int i = 0; i < _state.CardCatalog.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card =
                    _state.CardCatalog.Cards[i];
                if (!string.Equals(card.StableCardId, normalized, StringComparison.Ordinal))
                    cards.Add(card);
                else
                    deletedCards.Add(card);
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    _state.CardCatalog.CatalogId,
                    _state.CardCatalog.Revision + 1,
                    cards,
                    references,
                    _state.CardCatalog.CatalogNumericId);
            if (!create.Success)
            {
                return FailedCardDelete(
                    normalized,
                    create.Diagnostics.Count > 0
                        ? create.Diagnostics[0].Message
                        : "删除后的行为卡目录无效。");
            }

            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        create.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                            _state.GroupCatalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return new CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
                    normalized,
                    null,
                    write.Conflict,
                    write.Diagnostic);
            }

            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!_state.Success || LatestCardRevision(normalized) > 0)
                return FailedCardDelete(normalized, "行为卡目录已写入，但删除结果复读失败。");
            _lastDeletedCards = deletedCards;
            _lastDeletedStableCardId = normalized;
            _lastDeletePostCatalogHash = _state.CardCatalog.CatalogHash;
            return new CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
                normalized,
                _state,
                false,
                string.Empty);
        }

        /// <remarks>LowFreqAlloc: explicit inverse transaction for the latest card deletion.</remarks>
        public CharacterTestRuntimeAiPlannerProjectCardDeleteResult TryUndoLastCardDelete(
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            string stableCardId = _lastDeletedStableCardId;
            if (!CanUndoLastCardDelete || references == null)
            {
                return FailedCardDelete(
                    stableCardId,
                    CanWrite
                        ? "最近一次行为卡删除已经不能安全撤销；项目目录可能已发生其他修改。"
                        : ReadOnlyDiagnostic());
            }

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CreateRestoredCardCatalog(
                    _state.CardCatalog,
                    _lastDeletedCards,
                    references);
            if (!create.Success)
            {
                return FailedCardDelete(
                    stableCardId,
                    create.Diagnostics.Count > 0
                        ? create.Diagnostics[0].Message
                        : "恢复后的行为卡目录无效。");
            }

            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        create.Catalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                            _state.GroupCatalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return new CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
                    stableCardId,
                    null,
                    write.Conflict,
                    write.Diagnostic);
            }

            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            for (int i = 0; _state.Success && i < _lastDeletedCards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard expected =
                    _lastDeletedCards[i];
                if (!_state.CardCatalog.TryGetExact(
                        expected.StableCardId,
                        expected.Revision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard restored)
                    || restored.ContentHash != expected.ContentHash)
                {
                    return FailedCardDelete(
                        stableCardId,
                        "行为卡恢复目录已写入，但复读结果与删除前版本不一致。");
                }
            }
            if (!_state.Success)
                return FailedCardDelete(stableCardId, _state.Diagnostic);

            _lastDeletedCards = null;
            _lastDeletedStableCardId = string.Empty;
            _lastDeletePostCatalogHash = 0;
            return new CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
                stableCardId,
                _state,
                false,
                string.Empty);
        }

        internal static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult
            CreateRestoredCardCatalog(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog current,
                IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> deletedCards,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                (current?.Cards.Count ?? 0) + (deletedCards?.Count ?? 0));
            for (int i = 0; current != null && i < current.Cards.Count; i++)
                cards.Add(current.Cards[i]);
            for (int i = 0; deletedCards != null && i < deletedCards.Count; i++)
                cards.Add(deletedCards[i]);
            return CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                current?.CatalogId ?? string.Empty,
                (current?.Revision ?? 0) + 1,
                cards,
                references,
                current?.CatalogNumericId ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId);
        }

        internal static string GenerateProjectCardStableId(
            Func<string, bool> exists,
            Func<ulong> nextRandomValue)
        {
            if (exists == null)
                throw new ArgumentNullException(nameof(exists));
            if (nextRandomValue == null)
                throw new ArgumentNullException(nameof(nextRandomValue));
            for (int attempt = 0; attempt < ProjectCardIdGenerationAttempts; attempt++)
            {
                string candidate = FormatProjectCardStableId(nextRandomValue());
                if (!exists(candidate))
                    return candidate;
            }
            throw new InvalidOperationException("无法生成不重复的行为卡短 ID，请重新尝试。");
        }

        internal static string FormatProjectCardStableId(ulong randomValue)
        {
            var result = new char[3 + ProjectCardIdRandomLength];
            result[0] = 'b';
            result[1] = 'c';
            result[2] = '_';
            for (int i = result.Length - 1; i >= 3; i--)
            {
                result[i] = ProjectCardIdAlphabet[(int)(randomValue & 31UL)];
                randomValue >>= 5;
            }
            return new string(result);
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateInitialNativeLogic()
        {
            const string timelineId = "timeline.main";
            var logic = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                EntryTimelineId = timelineId,
                Intent = "自定义行为卡",
                RiskSummary = string.Empty
            };
            logic.Timelines.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = timelineId,
                Order = 0,
                Title = "时间线 1",
                Note = "空白原生时间线；请添加事件来定义行为。"
            });
            return logic;
        }

        /// <summary>Compares one mutable group draft with its immutable Published source without writing.</summary>
        /// <remarks>LowFreqAlloc: authoring presentation and publish preflight only.</remarks>
        internal bool TryHasUnpublishedGroupChanges(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            out bool hasUnpublishedChanges,
            out string diagnostic)
        {
            hasUnpublishedChanges = true;
            diagnostic = string.Empty;
            if (draft?.HasProjectSource != true
                || cards == null
                || _state?.GroupCatalog == null
                || !_state.GroupCatalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourceProjectRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
                || source.ContentHash != draft.SourceProjectContentHash)
            {
                diagnostic = "行为组草稿来源已变化或不可用。";
                return false;
            }

            if (!TryBuildGroupCandidate(
                    draft,
                    cards,
                    source,
                    draft.SourceProjectRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
                    out diagnostic))
            {
                return true;
            }
            hasUnpublishedChanges = candidate.ContentHash != draft.SourceProjectContentHash;
            return true;
        }

        /// <summary>Builds the exact read-only candidate used by the Published-content comparison.</summary>
        /// <remarks>LowFreqAlloc: focused authoring diagnostics and tests only; never writes catalog state.</remarks>
        internal bool TryBuildGroupCandidateForComparison(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
            out string diagnostic)
        {
            source = null;
            candidate = null;
            diagnostic = string.Empty;
            if (draft?.HasProjectSource != true
                || cards == null
                || _state?.GroupCatalog == null
                || !_state.GroupCatalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourceProjectRevision,
                    out source)
                || source.ContentHash != draft.SourceProjectContentHash)
            {
                diagnostic = "行为组草稿来源已变化或不可用。";
                return false;
            }

            return TryBuildGroupCandidate(
                draft,
                cards,
                source,
                draft.SourceProjectRevision,
                out candidate,
                out diagnostic);
        }

        /// <remarks>LowFreqAlloc: explicit Editor group publish transaction only.</remarks>
        public CharacterTestRuntimeAiPlannerProjectGroupPublishResult TryPublishGroup(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            LastWriteConflict = false;
            if (!CanWrite || draft == null || cards == null)
                return FailedGroup(CanWrite ? "行为组草稿不可用。" : ReadOnlyDiagnostic());
            if (!draft.HasProjectSource
                || !_state.GroupCatalog.TryGetExact(
                    draft.PublishedStableId,
                    draft.SourceProjectRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
                || source.ContentHash != draft.SourceProjectContentHash)
            {
                return FailedGroup("行为组草稿来源已变化，请重新加载项目版本。");
            }
            int nextRevision = LatestGroupRevision(draft.PublishedStableId) + 1;
            if (!TryBuildGroupCandidate(
                    draft,
                    cards,
                    source,
                    nextRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision published,
                    out string candidateDiagnostic))
            {
                return FailedGroup(candidateDiagnostic);
            }
            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(_state.GroupCatalog.Groups.Count + 1);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
                groups.Add(_state.GroupCatalog.Groups[i]);
            groups.Add(published);
            var catalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                _state.GroupCatalog.CatalogId,
                _state.GroupCatalog.Revision + 1,
                groups);
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult validation =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(catalog),
                    _state.CardCatalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!validation.Success)
                return FailedGroup(validation.Diagnostic);
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        _state.CardCatalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(validation.Catalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(null, _state, write.Conflict, write.Diagnostic);
            }

            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!_state.Success
                || !_state.GroupCatalog.TryGetExact(
                    published.StableGroupId,
                    published.Revision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision reloaded)
                || reloaded.ContentHash != published.ContentHash)
            {
                return FailedGroup("行为组目录已写入，但重新加载结果与提交版本不一致。");
            }
            return new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(reloaded, _state, false, string.Empty);
        }

        private bool TryBuildGroupCandidate(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            int revision,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
            out string diagnostic)
        {
            candidate = null;
            diagnostic = string.Empty;
            if (draft?.Profile == null || cards == null || source == null || revision <= 0)
            {
                diagnostic = "行为组 canonical candidate 输入不完整。";
                return false;
            }
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] sourceEntryMatches =
                ResolveCanonicalSourceEntryMatches(draft, cards, source);
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile candidateProfile =
                BuildCanonicalGroupProfile(
                    draft,
                    cards,
                    _state?.CardCatalog,
                    source,
                    sourceEntryMatches);
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    candidateProfile,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!compile.Success)
            {
                diagnostic = "行为组校验失败：" + FirstProfileDiagnostic(compile.Report);
                return false;
            }

            var fixedCards = new List<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>();
            int totalCost = 0;
            for (int i = 0; i < draft.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = draft.Entries[i];
                if (entry?.RuntimeEntry == null || string.IsNullOrWhiteSpace(entry.CardDraftId))
                {
                    diagnostic = "行为组包含损坏的行为卡条目。";
                    return false;
                }
                bool builtInReference = string.Equals(
                        entry.BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal)
                    || string.Equals(
                        entry.BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        StringComparison.Ordinal)
                    || CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.IsNoOpPlaceholder(
                        entry.BehaviorId);
                if (builtInReference
                    && (!cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft builtInCard)
                        || !builtInCard.HasPublishedSource))
                {
                    continue;
                }
                if (!cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !card.HasPublishedSource
                    || _state?.CardCatalog == null
                    || !_state.CardCatalog.TryGetExact(
                        card.PublishedStableId,
                        card.SourcePublishedRevision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact)
                    || exact.SemanticHash != card.SourcePublishedSemanticHash
                    || exact.ContentHash != card.SourcePublishedContentHash)
                {
                    diagnostic = "行为条目“" + entry.BehaviorId
                        + "”没有可解析的项目行为卡来源版本。";
                    return false;
                }
                totalCost += exact.Metadata?.Cost ?? 0;
                string canonicalEntryId = sourceEntryMatches[i]?.EntryId
                    ?? entry.RuntimeEntry.EntryId;
                int canonicalOrder = ResolveCanonicalProfileEntryOrder(
                    candidateProfile,
                    canonicalEntryId,
                    entry.Order);
                fixedCards.Add(new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                    canonicalEntryId,
                    exact.StableCardId,
                    exact.Revision,
                    exact.SemanticHash,
                    exact.ContentHash,
                    !entry.Disabled,
                    canonicalOrder));
            }
            if (fixedCards.Count > 6 || totalCost > 10)
            {
                diagnostic = "行为组必须满足最多 6 张卡、总费用最多 10 的约束。";
                return false;
            }

            candidate = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                draft.PublishedStableId,
                revision,
                draft.DisplayName,
                source.Description,
                source.Tags,
                source.SortOrder,
                candidateProfile,
                fixedCards);
            return true;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile
            BuildCanonicalGroupProfile(
                CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
                IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog publishedCards,
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> sourceEntryMatches)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = draft.Profile.Clone();
            profile.Entries.Clear();
            int authoredIndex = 0;
            for (int i = 0; i < source.Profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry sourceEntry = source.Profile.Entries[i];
                if (FindSourceCardReference(source, sourceEntry.EntryId) == null)
                {
                    profile.Entries.Add(sourceEntry.Clone());
                    continue;
                }

                if (authoredIndex >= draft.Entries.Count)
                    continue;
                CharacterTestRuntimeAiLabBehaviorEntryDraft authored = draft.Entries[authoredIndex++];
                if (authored?.RuntimeEntry == null)
                    continue;
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                    authored.RuntimeEntry.Clone();
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry matchedSourceEntry =
                    authoredIndex - 1 < sourceEntryMatches.Count
                        ? sourceEntryMatches[authoredIndex - 1]
                        : null;
                if (matchedSourceEntry != null)
                    entry.EntryId = matchedSourceEntry.EntryId;
                entry.Disabled = authored.Disabled;
                entry.Order = sourceEntry.Order;
                RestoreImportedResponsibilityBaseline(
                    entry,
                    authored,
                    cards,
                    publishedCards,
                    matchedSourceEntry);
                profile.Entries.Add(entry);
            }

            for (; authoredIndex < draft.Entries.Count; authoredIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft authored = draft.Entries[authoredIndex];
                if (authored?.RuntimeEntry == null)
                    continue;
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = authored.RuntimeEntry.Clone();
                entry.Disabled = authored.Disabled;
                profile.Entries.Add(entry);
            }
            return profile;
        }

        private static void RestoreImportedResponsibilityBaseline(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry candidateEntry,
            CharacterTestRuntimeAiLabBehaviorEntryDraft authored,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog publishedCards,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry sourceEntry)
        {
            if (candidateEntry == null
                || authored == null
                || sourceEntry == null
                || cards == null
                || !cards.TryGetValue(
                    authored.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || publishedCards == null
                || !publishedCards.TryGetExact(
                    card.PublishedStableId,
                    card.SourcePublishedRevision,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
                || publishedCard.SemanticHash != card.SourcePublishedSemanticHash
                || publishedCard.ContentHash != card.SourcePublishedContentHash
                || !CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.SetEquals(
                    candidateEntry.ResponsibilityKinds,
                    publishedCard.Metadata?.Responsibilities))
            {
                return;
            }

            candidateEntry.SetResponsibilities(sourceEntry.ResponsibilityKinds);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry[]
            ResolveCanonicalSourceEntryMatches(
                CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
                IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            var matches = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry[
                draft?.Entries.Count ?? 0];
            if (draft == null || cards == null || source == null || source.Cards.Count == 0)
                return matches;

            var consumedSourceCards = new bool[source.Cards.Count];
            for (int authoredIndex = 0; authoredIndex < draft.Entries.Count; authoredIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft authored = draft.Entries[authoredIndex];
                if (authored?.RuntimeEntry == null
                    || !cards.TryGetValue(
                        authored.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !card.HasPublishedSource)
                {
                    continue;
                }

                int bestSourceIndex = -1;
                long bestOrderDistance = long.MaxValue;
                int bestSourceOrder = int.MaxValue;
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry bestSourceEntry = null;
                for (int sourceIndex = 0; sourceIndex < source.Cards.Count; sourceIndex++)
                {
                    if (consumedSourceCards[sourceIndex])
                        continue;
                    CharacterTestRuntimeAiPlannerBehaviorGroupCardReference sourceCard =
                        source.Cards[sourceIndex];
                    if (!string.Equals(
                            sourceCard.StableCardId,
                            card.PublishedStableId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry sourceEntry =
                        FindSourceProfileEntry(source, sourceCard.EntryId);
                    if (sourceEntry == null
                        || !string.Equals(
                            sourceEntry.BehaviorId,
                            authored.RuntimeEntry.BehaviorId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    long orderDistance = Math.Abs(
                        (long)sourceCard.Order - authored.Order);
                    if (orderDistance > bestOrderDistance
                        || (orderDistance == bestOrderDistance
                            && sourceCard.Order > bestSourceOrder)
                        || (orderDistance == bestOrderDistance
                            && sourceCard.Order == bestSourceOrder
                            && bestSourceIndex >= 0
                            && sourceIndex > bestSourceIndex))
                    {
                        continue;
                    }

                    bestSourceIndex = sourceIndex;
                    bestOrderDistance = orderDistance;
                    bestSourceOrder = sourceCard.Order;
                    bestSourceEntry = sourceEntry;
                }

                if (bestSourceIndex < 0)
                    continue;
                consumedSourceCards[bestSourceIndex] = true;
                matches[authoredIndex] = bestSourceEntry;
            }
            return matches;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry FindSourceProfileEntry(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            string entryId)
        {
            for (int i = 0; source?.Profile != null && i < source.Profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = source.Profile.Entries[i];
                if (string.Equals(entry?.EntryId, entryId, StringComparison.Ordinal))
                    return entry;
            }
            return null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCardReference
            FindSourceCardReference(
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
                string entryId)
        {
            for (int i = 0; i < source.Cards.Count; i++)
            {
                if (string.Equals(source.Cards[i].EntryId, entryId, StringComparison.Ordinal))
                    return source.Cards[i];
            }
            return null;
        }

        private static int ResolveCanonicalProfileEntryOrder(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string entryId,
            int fallback)
        {
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                if (string.Equals(profile.Entries[i].EntryId, entryId, StringComparison.Ordinal))
                    return profile.Entries[i].Order;
            }
            return fallback;
        }

        /// <remarks>LowFreqAlloc: explicit Editor metadata-only publish transaction.</remarks>
        public CharacterTestRuntimeAiPlannerProjectGroupPublishResult TryPublishGroupBasicInfo(
            string stableGroupId,
            int sourceRevision,
            long sourceContentHash,
            string displayName,
            string description,
            string tagsText)
        {
            LastWriteConflict = false;
            if (!CanWrite)
                return FailedGroup(ReadOnlyDiagnostic());
            if (string.IsNullOrWhiteSpace(stableGroupId)
                || sourceRevision <= 0
                || sourceContentHash == 0
                || !_state.GroupCatalog.TryGetExact(
                    stableGroupId,
                    sourceRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
                || source.ContentHash != sourceContentHash)
            {
                return FailedGroup("行为组来源已变化，请重新选择项目版本。");
            }

            if (!TryCreateBasicInfoRevision(
                    source,
                    LatestGroupRevision(stableGroupId) + 1,
                    displayName,
                    description,
                    tagsText,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision published,
                    out string diagnostic))
            {
                return FailedGroup(diagnostic);
            }

            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(
                _state.GroupCatalog.Groups.Count + 1);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
                groups.Add(_state.GroupCatalog.Groups[i]);
            groups.Add(published);
            var catalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                _state.GroupCatalog.CatalogId,
                _state.GroupCatalog.Revision + 1,
                groups);
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult validation =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(catalog),
                    _state.CardCatalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!validation.Success)
                return FailedGroup(validation.Diagnostic);

            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        _state.CardCatalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                            validation.Catalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                return new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(
                    null,
                    _state,
                    write.Conflict,
                    write.Diagnostic);
            }

            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!_state.Success
                || !_state.GroupCatalog.TryGetExact(
                    published.StableGroupId,
                    published.Revision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision reloaded)
                || reloaded.ContentHash != published.ContentHash)
            {
                return FailedGroup("行为组基本信息已写入，但重新加载结果与提交版本不一致。");
            }
            return new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(
                reloaded,
                _state,
                false,
                string.Empty);
        }

        internal static bool TryCreateBasicInfoRevision(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            int nextRevision,
            string displayName,
            string description,
            string tagsText,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision revision,
            out string diagnostic)
        {
            revision = null;
            string normalizedName = displayName?.Trim() ?? string.Empty;
            string normalizedDescription = description?.Trim() ?? string.Empty;
            string normalizedTags = tagsText ?? string.Empty;
            if (source == null)
            {
                diagnostic = "没有可编辑的项目行为组来源。";
                return false;
            }
            if (nextRevision <= source.Revision)
            {
                diagnostic = "目标 revision 必须晚于当前项目版本。";
                return false;
            }
            if (normalizedName.Length == 0)
            {
                diagnostic = "行为组名称不能为空。";
                return false;
            }
            if (normalizedName.Length > 64)
            {
                diagnostic = "行为组名称不能超过 64 个字符。";
                return false;
            }
            if (normalizedDescription.Length > 512)
            {
                diagnostic = "行为组描述不能超过 512 个字符。";
                return false;
            }
            if (normalizedTags.Length > 256)
            {
                diagnostic = "行为组标签不能超过 256 个字符。";
                return false;
            }

            string[] parsedTags = ParseTags(normalizedTags);
            if (string.Equals(source.DisplayName, normalizedName, StringComparison.Ordinal)
                && string.Equals(source.Description, normalizedDescription, StringComparison.Ordinal)
                && TagsEqual(source.Tags, parsedTags))
            {
                diagnostic = "行为组基本信息没有需要保存的修改。";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source.Profile.Clone();
            profile.GroupId = source.StableGroupId;
            profile.DisplayName = normalizedName;
            revision = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                source.StableGroupId,
                nextRevision,
                normalizedName,
                normalizedDescription,
                parsedTags,
                source.SortOrder,
                profile,
                source.Cards);
            diagnostic = string.Empty;
            return true;
        }

        private static bool TagsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;
            if (leftCount != rightCount)
                return false;
            for (int i = 0; i < leftCount; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <remarks>LowFreqAlloc: explicit Editor project-asset creation only.</remarks>
        public bool TryCreateGroupCopy(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision created,
            out string diagnostic)
        {
            created = null;
            diagnostic = string.Empty;
            LastWriteConflict = false;
            if (!CanWrite || source == null)
            {
                diagnostic = CanWrite ? "没有可复制的行为组。" : ReadOnlyDiagnostic();
                return false;
            }

            string stableGroupId = "user.behavior-group."
                + DateTime.UtcNow.Ticks.ToString("x", System.Globalization.CultureInfo.InvariantCulture);
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source.Profile.Clone();
            profile.GroupId = stableGroupId;
            profile.DisplayName = source.DisplayName + " 副本";
            created = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                stableGroupId,
                1,
                source.DisplayName + " 副本",
                source.Description,
                source.Tags,
                source.SortOrder + 1,
                profile,
                source.Cards);

            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(
                _state.GroupCatalog.Groups.Count + 1);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
                groups.Add(_state.GroupCatalog.Groups[i]);
            groups.Add(created);
            return TryWriteGroupCatalog(groups, created, out diagnostic);
        }

        /// <remarks>LowFreqAlloc: explicit confirmation of a local Lab group draft.</remarks>
        public CharacterTestRuntimeAiPlannerProjectGroupPublishResult TryConfirmLocalGroupDraft(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            string displayName,
            string description,
            string tagsText)
        {
            LastWriteConflict = false;
            if (!CanWrite || draft == null || cards == null)
                return FailedGroup(CanWrite ? "行为组草稿不可用。" : ReadOnlyDiagnostic());
            string normalizedName = displayName?.Trim() ?? string.Empty;
            string normalizedDescription = description?.Trim() ?? string.Empty;
            string normalizedTags = tagsText ?? string.Empty;
            if (normalizedName.Length == 0 || normalizedName.Length > 64)
                return FailedGroup(normalizedName.Length == 0 ? "行为组名称不能为空。" : "行为组名称不能超过 64 个字符。");
            if (normalizedDescription.Length > 512 || normalizedTags.Length > 256)
                return FailedGroup(normalizedDescription.Length > 512
                    ? "行为组描述不能超过 512 个字符。"
                    : "行为组标签不能超过 256 个字符。");
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    draft.Profile,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!compile.Success)
                return FailedGroup("行为组校验失败：" + FirstProfileDiagnostic(compile.Report));

            var fixedCards = new List<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>();
            int totalCost = 0;
            for (int i = 0; i < draft.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = draft.Entries[i];
                if ((string.Equals(entry.BehaviorId, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack, StringComparison.Ordinal)
                        || string.Equals(entry.BehaviorId, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, StringComparison.Ordinal)
                        || CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.IsNoOpPlaceholder(entry.BehaviorId))
                    && (!cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft builtInCard)
                        || !builtInCard.HasPublishedSource))
                    continue;
                if (!cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !card.HasPublishedSource
                    || !_state.CardCatalog.TryGetExact(card.PublishedStableId, card.SourcePublishedRevision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact)
                    || exact.SemanticHash != card.SourcePublishedSemanticHash
                    || exact.ContentHash != card.SourcePublishedContentHash)
                    return FailedGroup("行为条目“" + entry.BehaviorId + "”没有可解析的项目行为卡来源版本。");
                totalCost += exact.Metadata?.Cost ?? 0;
                fixedCards.Add(new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                    entry.RuntimeEntry.EntryId, exact.StableCardId, exact.Revision,
                    exact.SemanticHash, exact.ContentHash, !entry.Disabled, entry.Order));
            }
            if (fixedCards.Count > 6 || totalCost > 10)
                return FailedGroup("行为组必须满足最多 6 张卡、总费用最多 10 的约束。");

            string stableGroupId;
            do
            {
                stableGroupId = "bg_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            }
            while (GroupStableIdExists(stableGroupId));
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = draft.Profile.Clone();
            profile.GroupId = stableGroupId;
            profile.DisplayName = normalizedName;
            var created = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                stableGroupId, 1, normalizedName, normalizedDescription, ParseTags(normalizedTags),
                _state.GroupCatalog.Groups.Count, profile, fixedCards);
            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(_state.GroupCatalog.Groups.Count + 1);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
                groups.Add(_state.GroupCatalog.Groups[i]);
            groups.Add(created);
            return TryWriteGroupCatalog(groups, created, out string diagnostic)
                ? new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(created, _state, false, string.Empty)
                : FailedGroup(diagnostic);
        }

        private bool GroupStableIdExists(string stableGroupId)
        {
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
            {
                if (string.Equals(
                        _state.GroupCatalog.Groups[i].StableGroupId,
                        stableGroupId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <remarks>LowFreqAlloc: explicit Editor project-asset creation only.</remarks>
        public bool TryCreateInitialGroup(
            string stableGroupId,
            string displayName,
            string description,
            IEnumerable<string> tags,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile sourceProfile,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision created,
            out string diagnostic)
        {
            created = null;
            diagnostic = string.Empty;
            LastWriteConflict = false;
            if (!CanWrite)
            {
                diagnostic = ReadOnlyDiagnostic();
                return false;
            }
            if (_state.GroupCatalog.Groups.Count != 0)
            {
                diagnostic = "项目行为组目录已包含内容，请改用复制行为组。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(stableGroupId)
                || string.IsNullOrWhiteSpace(displayName)
                || sourceProfile == null)
            {
                diagnostic = "首个行为组的稳定 ID、名称或行为模板不可用。";
                return false;
            }

            string normalizedStableGroupId = stableGroupId.Trim();
            string normalizedDisplayName = displayName.Trim();
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = sourceProfile.Clone();
            profile.GroupId = normalizedStableGroupId;
            profile.DisplayName = normalizedDisplayName;
            created = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                normalizedStableGroupId,
                1,
                normalizedDisplayName,
                description,
                tags,
                0,
                profile,
                Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>());
            return TryWriteGroupCatalog(
                new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> { created },
                created,
                out diagnostic);
        }

        /// <remarks>LowFreqAlloc: explicit Editor project-asset deletion only.</remarks>
        public bool TryDeleteGroup(string stableGroupId, out string diagnostic)
        {
            diagnostic = string.Empty;
            LastWriteConflict = false;
            if (!CanWrite)
            {
                diagnostic = ReadOnlyDiagnostic();
                return false;
            }

            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(
                _state.GroupCatalog.Groups.Count);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision group =
                    _state.GroupCatalog.Groups[i];
                if (!string.Equals(group.StableGroupId, stableGroupId, StringComparison.Ordinal))
                    groups.Add(group);
            }
            if (groups.Count == _state.GroupCatalog.Groups.Count)
            {
                diagnostic = "要删除的行为组已经不存在。";
                return false;
            }
            if (groups.Count == 0)
            {
                diagnostic = "项目中必须至少保留一个行为组。";
                return false;
            }
            return TryWriteGroupCatalog(groups, null, out diagnostic);
        }

        private bool TryWriteGroupCatalog(
            List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> groups,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision expectedGroup,
            out string diagnostic)
        {
            var catalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                _state.GroupCatalog.CatalogId,
                _state.GroupCatalog.Revision + 1,
                groups);
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult validation =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(catalog),
                    _state.CardCatalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!validation.Success)
            {
                diagnostic = validation.Diagnostic;
                return false;
            }
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(
                    new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        _state.CardCatalog,
                        CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(validation.Catalog)));
            if (!write.Success)
            {
                LastWriteConflict = write.Conflict;
                diagnostic = write.Diagnostic;
                return false;
            }
            _state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!_state.Success)
            {
                diagnostic = _state.Diagnostic;
                return false;
            }
            if (expectedGroup != null
                && (!_state.GroupCatalog.TryGetExact(
                        expectedGroup.StableGroupId,
                        expectedGroup.Revision,
                        out CharacterTestRuntimeAiPlannerBehaviorGroupRevision reloaded)
                    || reloaded.ContentHash != expectedGroup.ContentHash))
            {
                diagnostic = "项目行为组目录已写入，但重新加载结果不一致。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private int LatestCardRevision(string stableCardId)
        {
            int revision = 0;
            for (int i = 0; i < _state.CardCatalog.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = _state.CardCatalog.Cards[i];
                if (string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                    revision = Math.Max(revision, card.Revision);
            }
            return revision;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata
            BuildCanonicalCardMetadata(
                CharacterTestRuntimeAiLabBehaviorCardDraft draft,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source)
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata sourceMetadata = source?.Metadata;
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                string.IsNullOrWhiteSpace(draft.DisplayName)
                    ? sourceMetadata?.DisplayName
                    : draft.DisplayName,
                string.IsNullOrWhiteSpace(draft.IconKey)
                    ? sourceMetadata?.IconKey ?? "ui.behavior-card.custom"
                    : draft.IconKey,
                draft.Cost,
                draft.ShortDescription,
                draft.UseSummary,
                draft.RiskSummary,
                ParseTags(draft.TagsText),
                draft.RequiredCapabilities,
                draft.ResponsibilityKinds, draft.CooldownSeconds);
        }

        private static string[] ParseTags(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();
            string[] split = value.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            var normalized = new List<string>(split.Length);
            for (int i = 0; i < split.Length; i++)
            {
                string tag = split[i].Trim();
                if (tag.Length > 0 && !normalized.Contains(tag))
                    normalized.Add(tag);
            }
            return normalized.ToArray();
        }

        private int LatestGroupRevision(string stableGroupId)
        {
            int revision = 0;
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision group = _state.GroupCatalog.Groups[i];
                if (string.Equals(group.StableGroupId, stableGroupId, StringComparison.Ordinal))
                    revision = Math.Max(revision, group.Revision);
            }
            return revision;
        }

        private string ReadOnlyDiagnostic()
        {
            if (_state == null || !_state.Success)
                return _state?.Diagnostic ?? "项目行为目录不可用。";
            return "Player 构建只读；项目行为目录只能在 Unity Editor 中发布。";
        }

        private CharacterTestRuntimeAiPlannerProjectGroupPublishResult FailedGroup(string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerProjectGroupPublishResult(null, _state, false, diagnostic);
        }

        private static CharacterTestRuntimeAiLabPublishResult FailedCard(string diagnostic)
        {
            return new CharacterTestRuntimeAiLabPublishResult(
                null,
                null,
                new[]
                {
                    new CharacterTestRuntimeAiLabCardDiagnostic(
                        CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        "PROJECT_CATALOG_PUBLISH_FAILED",
                        "$",
                        diagnostic)
                });
        }

        private CharacterTestRuntimeAiPlannerProjectCardDeleteResult FailedCardDelete(
            string stableCardId,
            string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerProjectCardDeleteResult(
                stableCardId,
                null,
                LastWriteConflict,
                diagnostic);
        }

        private static CharacterTestRuntimeAiLabCardDiagnostic[] CopyDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> source)
        {
            var result = new CharacterTestRuntimeAiLabCardDiagnostic[source.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static string FirstProfileDiagnostic(CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return report != null && report.Diagnostics.Count > 0
                ? report.Diagnostics[0].Message
                : "未知错误";
        }
    }
}
