using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPublishCurrentChangesStage
    {
        None = 0,
        WorkspaceRole = 1,
        DraftFlush = 2,
        SelectionFreeze = 3,
        CardPreflight = 4,
        GroupPreflight = 5,
        SourceIdentity = 6,
        CatalogCommit = 7,
        CatalogReload = 8,
        DraftCheckpoint = 9,
        Completed = 10
    }

    internal enum CharacterTestRuntimeAiLabPublishCurrentChangesStatus
    {
        None = 0,
        PreviewReady = 1,
        CatalogCommitted = 2,
        Succeeded = 3,
        NoChanges = 4,
        ReadOnly = 5,
        MainWorkspaceRequired = 6,
        DraftFlushFailed = 7,
        InvalidSelection = 8,
        CardPreflightFailed = 9,
        GroupPreflightFailed = 10,
        SourceConflict = 11,
        CommitFailed = 12,
        ReloadFailed = 13,
        DraftCheckpointFailed = 14,
        PreviewExpired = 15,
        Busy = 16,
        Cancelled = 17
    }

    internal readonly struct CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic(
            CharacterTestRuntimeAiLabPublishCurrentChangesStage stage,
            string code,
            string message)
        {
            Stage = stage;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesStage Stage { get; }
        internal string Code { get; }
        internal string Message { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview(
            string cardDraftId,
            string stableCardId,
            string displayName,
            int expectedDraftRevision,
            int sourceRevision,
            int targetRevision,
            int eventCount,
            int affectedOtherGroupCount,
            string sharedDependencies = null)
        {
            CardDraftId = cardDraftId ?? string.Empty;
            StableCardId = stableCardId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ExpectedDraftRevision = expectedDraftRevision;
            SourceRevision = sourceRevision;
            TargetRevision = targetRevision;
            EventCount = Math.Max(0, eventCount);
            AffectedOtherGroupCount = Math.Max(0, affectedOtherGroupCount);
            SharedDependencies = sharedDependencies ?? string.Empty;
        }

        internal string CardDraftId { get; }
        internal string StableCardId { get; }
        internal string DisplayName { get; }
        internal int ExpectedDraftRevision { get; }
        internal int SourceRevision { get; }
        internal int TargetRevision { get; }
        internal int EventCount { get; }
        internal int AffectedOtherGroupCount { get; }
        internal string SharedDependencies { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesPreview
    {
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview[] _cards;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPreview(
            string token,
            string workspaceId,
            int workspaceRevision,
            string groupDraftId,
            int groupDraftRevision,
            string stableGroupId,
            string groupDisplayName,
            int sourceGroupRevision,
            int targetGroupRevision,
            int directCardCount,
            CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview[] cards,
            long sourceIdentityHash,
            long candidateIdentityHash)
        {
            Token = token ?? string.Empty;
            WorkspaceId = workspaceId ?? string.Empty;
            WorkspaceRevision = workspaceRevision;
            GroupDraftId = groupDraftId ?? string.Empty;
            GroupDraftRevision = groupDraftRevision;
            StableGroupId = stableGroupId ?? string.Empty;
            GroupDisplayName = groupDisplayName ?? string.Empty;
            SourceGroupRevision = sourceGroupRevision;
            TargetGroupRevision = targetGroupRevision;
            DirectCardCount = Math.Max(0, directCardCount);
            _cards = cards ?? Array.Empty<CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview>();
            SourceIdentityHash = sourceIdentityHash;
            CandidateIdentityHash = candidateIdentityHash;
        }

        internal string Token { get; }
        internal string WorkspaceId { get; }
        internal int WorkspaceRevision { get; }
        internal string GroupDraftId { get; }
        internal int GroupDraftRevision { get; }
        internal string StableGroupId { get; }
        internal string GroupDisplayName { get; }
        internal int SourceGroupRevision { get; }
        internal int TargetGroupRevision { get; }
        internal int DirectCardCount { get; }
        internal IReadOnlyList<CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview> Cards =>
            Array.AsReadOnly(_cards);
        internal int ChangedCardCount => _cards.Length;
        internal long SourceIdentityHash { get; }
        internal long CandidateIdentityHash { get; }

        internal string BuildSummary()
        {
            var lines = new List<string>(_cards.Length + 4)
            {
                "发布本次修改？"
            };
            if (_cards.Length > 0)
            {
                lines.Add("行为卡");
                for (int i = 0; i < _cards.Length; i++)
                {
                    CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview card = _cards[i];
                    string impact = card.AffectedOtherGroupCount > 0
                        ? "；另有 " + card.AffectedOtherGroupCount.ToString(CultureInfo.InvariantCulture)
                            + " 个行为组仍固定旧版本"
                        : string.Empty;
                    lines.Add("- " + card.DisplayName + "："
                        + card.EventCount.ToString(CultureInfo.InvariantCulture) + " 个事件" + impact
                        + (card.SharedDependencies.Length > 0 ? "\n  固定共享版本：" + card.SharedDependencies : string.Empty));
                }
            }
            lines.Add("行为组");
            lines.Add("- " + GroupDisplayName + " r"
                + SourceGroupRevision.ToString(CultureInfo.InvariantCulture)
                + " → r" + TargetGroupRevision.ToString(CultureInfo.InvariantCulture));
            lines.Add("发布不会中断当前测试。");
            return string.Join("\n", lines);
        }
    }

    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard
    {
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard(
            string cardDraftId,
            int expectedDraftRevision,
            int expectedSourceRevision,
            long expectedSourceSemanticHash,
            long expectedSourceContentHash,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            CardDraftId = cardDraftId ?? string.Empty;
            ExpectedDraftRevision = expectedDraftRevision;
            ExpectedSourceRevision = expectedSourceRevision;
            ExpectedSourceSemanticHash = expectedSourceSemanticHash;
            ExpectedSourceContentHash = expectedSourceContentHash;
            Card = card;
        }

        internal string CardDraftId { get; }
        internal int ExpectedDraftRevision { get; }
        internal int ExpectedSourceRevision { get; }
        internal long ExpectedSourceSemanticHash { get; }
        internal long ExpectedSourceContentHash { get; }
        internal CharacterTestRuntimeAiPlannerPublishedBehaviorCard Card { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabPublishCurrentChangesResult
    {
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard[] _cards;
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic[] _diagnostics;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult(
            CharacterTestRuntimeAiLabPublishCurrentChangesStatus status,
            CharacterTestRuntimeAiLabPublishCurrentChangesStage stage,
            CharacterTestRuntimeAiLabPublishCurrentChangesPreview preview,
            CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard[] cards,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            CharacterTestRuntimeAiPlannerProjectCatalogState state,
            bool conflict,
            bool catalogChanged,
            bool requiresRecovery,
            bool duplicateConfirmation,
            string code,
            string message)
        {
            Status = status;
            Stage = stage;
            Preview = preview;
            _cards = cards ?? Array.Empty<CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard>();
            Group = group;
            State = state;
            Conflict = conflict;
            CatalogChanged = catalogChanged;
            RequiresRecovery = requiresRecovery;
            DuplicateConfirmation = duplicateConfirmation;
            Message = message ?? string.Empty;
            _diagnostics = Message.Length == 0
                ? Array.Empty<CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic>()
                : new[]
                {
                    new CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic(
                        stage,
                        code,
                        Message)
                };
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesStatus Status { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesStage Stage { get; }
        internal CharacterTestRuntimeAiLabPublishCurrentChangesPreview Preview { get; }
        internal IReadOnlyList<CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard> PublishedCards =>
            Array.AsReadOnly(_cards);
        internal CharacterTestRuntimeAiPlannerBehaviorGroupRevision Group { get; }
        internal CharacterTestRuntimeAiPlannerProjectCatalogState State { get; }
        internal IReadOnlyList<CharacterTestRuntimeAiLabPublishCurrentChangesDiagnostic> Diagnostics =>
            Array.AsReadOnly(_diagnostics);
        internal string Message { get; }
        internal bool Conflict { get; }
        internal bool CatalogChanged { get; }
        internal bool RequiresRecovery { get; }
        internal bool DuplicateConfirmation { get; }
        internal bool Success => Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Succeeded;
        internal bool PreviewReady => Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewReady;
        internal bool CatalogCommitSucceeded =>
            Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CatalogCommitted
            || Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Succeeded;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult CompleteAfterDraftCheckpoint(
            string message)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Succeeded,
                CharacterTestRuntimeAiLabPublishCurrentChangesStage.Completed,
                Preview,
                CopyCards(_cards),
                Group,
                State,
                conflict: false,
                catalogChanged: true,
                requiresRecovery: false,
                duplicateConfirmation: DuplicateConfirmation,
                code: "PUBLISH_CURRENT_CHANGES_SUCCEEDED",
                message: message);
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult FailAfterCatalogCommit(
            CharacterTestRuntimeAiLabPublishCurrentChangesStage stage,
            CharacterTestRuntimeAiLabPublishCurrentChangesStatus status,
            string code,
            string message)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                status,
                stage,
                Preview,
                CopyCards(_cards),
                Group,
                State,
                conflict: false,
                catalogChanged: true,
                requiresRecovery: true,
                duplicateConfirmation: false,
                code: code,
                message: message);
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult AsDuplicateConfirmation()
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                Status,
                Stage,
                Preview,
                CopyCards(_cards),
                Group,
                State,
                Conflict,
                CatalogChanged,
                RequiresRecovery,
                duplicateConfirmation: true,
                code: "PUBLISH_CURRENT_CHANGES_DUPLICATE_CONFIRMATION",
                message: Message);
        }

        internal static CharacterTestRuntimeAiLabPublishCurrentChangesResult Fail(
            CharacterTestRuntimeAiLabPublishCurrentChangesStatus status,
            CharacterTestRuntimeAiLabPublishCurrentChangesStage stage,
            string code,
            string message,
            CharacterTestRuntimeAiLabPublishCurrentChangesPreview preview = null,
            bool conflict = false,
            bool catalogChanged = false,
            bool requiresRecovery = false)
        {
            return new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                status,
                stage,
                preview,
                null,
                null,
                null,
                conflict,
                catalogChanged,
                requiresRecovery,
                duplicateConfirmation: false,
                code: code,
                message: message);
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard[] CopyCards(
            IReadOnlyList<CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard> source)
        {
            var copy = new CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard[source?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    internal sealed partial class CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession
    {
        private sealed class PublishCurrentChangesPlan
        {
            internal CharacterTestRuntimeAiLabPublishCurrentChangesPreview Preview;
            internal CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CardCatalog;
            internal CharacterTestRuntimeAiPlannerBehaviorGroupCatalog GroupCatalog;
            internal string GroupCatalogJson;
            internal CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard[] PublishedCards;
            internal CharacterTestRuntimeAiPlannerBehaviorGroupRevision PublishedGroup;
        }

        private readonly Func<CharacterTestRuntimeAiPlannerProjectCatalogState>
            _publishCurrentChangesReloadOverride;
        private readonly Func<CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest,
            CharacterTestRuntimeAiPlannerProjectCatalogWriteResult> _publishCurrentChangesWriteOverride;
        private PublishCurrentChangesPlan _pendingPublishCurrentChangesPlan;
        private PublishCurrentChangesPlan _recoveryPublishCurrentChangesPlan;
        private bool _publishCurrentChangesCommitInProgress;
        private string _lastCommittedPublishCurrentChangesToken = string.Empty;
        private CharacterTestRuntimeAiLabPublishCurrentChangesResult _lastCommittedPublishCurrentChangesResult;

        internal CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
            CharacterTestRuntimeAiPlannerProjectCatalogState state,
            Func<CharacterTestRuntimeAiPlannerProjectCatalogState> reload,
            Func<CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest,
                CharacterTestRuntimeAiPlannerProjectCatalogWriteResult> write)
            : this(state)
        {
            _publishCurrentChangesReloadOverride = reload;
            _publishCurrentChangesWriteOverride = write;
            _publishRecoveryJournalPath = null;
            _hasPublishRecoveryJournal = false;
        }

        /// <remarks>LowFreqAlloc: read-only aggregate publish preview.</remarks>
        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult PreparePublishCurrentChanges(
            string workspaceId,
            int workspaceRevision,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            if (HasPendingPublishRecovery)
                return RecoveryRequired(GetPublishRecoveryPreview());
            if (!CanWritePublishCurrentChanges)
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReadOnly,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                    "PUBLISH_CURRENT_CHANGES_READ_ONLY",
                    ReadOnlyDiagnostic());
            }

            if (!TryBuildPublishCurrentChangesPlan(
                    workspaceId,
                    workspaceRevision,
                    group,
                    cards,
                    references,
                    out PublishCurrentChangesPlan plan,
                    out CharacterTestRuntimeAiLabPublishCurrentChangesResult failure))
            {
                _pendingPublishCurrentChangesPlan = null;
                return failure;
            }

            _pendingPublishCurrentChangesPlan = plan;
            return new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewReady,
                CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                plan.Preview,
                null,
                plan.PublishedGroup,
                _state,
                conflict: false,
                catalogChanged: false,
                requiresRecovery: false,
                duplicateConfirmation: false,
                code: "PUBLISH_CURRENT_CHANGES_PREVIEW_READY",
                message: plan.Preview.BuildSummary());
        }

        /// <remarks>LowFreqAlloc: explicit aggregate Card plus Group project-catalog transaction.</remarks>
        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult CommitPublishCurrentChanges(
            string previewToken,
            string workspaceId,
            int workspaceRevision,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            LastWriteConflict = false;
            string normalizedToken = previewToken?.Trim() ?? string.Empty;
            if (HasPendingPublishRecovery)
                return RecoveryRequired(GetPublishRecoveryPreview());
            if (normalizedToken.Length > 0
                && string.Equals(
                    normalizedToken,
                    _lastCommittedPublishCurrentChangesToken,
                    StringComparison.Ordinal)
                && _lastCommittedPublishCurrentChangesResult != null)
            {
                return _lastCommittedPublishCurrentChangesResult.AsDuplicateConfirmation();
            }
            if (_publishCurrentChangesCommitInProgress)
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Busy,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogCommit,
                    "PUBLISH_CURRENT_CHANGES_BUSY",
                    "发布事务正在执行；重复确认没有产生第二次写入。");
            }
            if (_pendingPublishCurrentChangesPlan == null
                || normalizedToken.Length == 0
                || !string.Equals(
                    normalizedToken,
                    _pendingPublishCurrentChangesPlan.Preview.Token,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewExpired,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                    "PUBLISH_CURRENT_CHANGES_PREVIEW_EXPIRED",
                    "发布摘要已经失效；请重新检查本次修改后再确认。");
            }

            _publishCurrentChangesCommitInProgress = true;
            try
            {
                PublishCurrentChangesPlan frozen = _pendingPublishCurrentChangesPlan;
                if (!TryBuildPublishCurrentChangesPlan(
                        workspaceId,
                        workspaceRevision,
                        group,
                        cards,
                        references,
                        out PublishCurrentChangesPlan current,
                        out CharacterTestRuntimeAiLabPublishCurrentChangesResult rebuildFailure))
                {
                    _pendingPublishCurrentChangesPlan = null;
                    return rebuildFailure;
                }
                if (current.Preview.SourceIdentityHash != frozen.Preview.SourceIdentityHash
                    || current.Preview.CandidateIdentityHash != frozen.Preview.CandidateIdentityHash)
                {
                    _pendingPublishCurrentChangesPlan = null;
                    return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewExpired,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_SOURCE_CHANGED",
                        "发布摘要生成后草稿或项目来源发生变化；正式目录未写入，请重新预览。",
                        frozen.Preview);
                }

                current.Preview = frozen.Preview;
                if (!TryWritePublishRecoveryJournal(current, out string journalIssue))
                    return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CommitFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogCommit, "PUBLISH_RECOVERY_RECORD_WRITE_FAILED",
                        "发布恢复记录无法保存，正式目录未写入：" + journalIssue, frozen.Preview);
                CharacterTestRuntimeAiPlannerProjectCatalogWriteResult write =
                    WritePublishCurrentChanges(new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                        _state.CardCatalogPath,
                        _state.GroupCatalogPath,
                        _state.CardFileHash,
                        _state.GroupFileHash,
                        _state.CardCatalog.CatalogHash,
                        current.CardCatalog,
                        current.GroupCatalogJson));
                if (!write.Success)
                {
                    LastWriteConflict = write.Conflict;
                    if (write.Conflict)
                        _pendingPublishCurrentChangesPlan = null;
                    string sourceStateDiagnostic = string.Empty;
                    bool sourceStateConfirmed = write.Conflict
                        || TryConfirmPublishCurrentChangesSourceState(
                            ReloadPublishCurrentChanges(),
                            out sourceStateDiagnostic);
                    if (!sourceStateConfirmed)
                    {
                        _recoveryPublishCurrentChangesPlan = current;
                        _pendingPublishCurrentChangesPlan = null;
                        return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                            CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CommitFailed,
                            CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                            "PUBLISH_CURRENT_CHANGES_ROLLBACK_UNCONFIRMED",
                            "项目目录事务失败，且无法确认回滚后仍为原版本："
                                + sourceStateDiagnostic,
                            frozen.Preview,
                            catalogChanged: true,
                            requiresRecovery: true);
                    }
                    if (!CompletePublishRecoveryJournal(out string clearIssue))
                    {
                        _recoveryPublishCurrentChangesPlan = current;
                        return RecoveryRequired(current.Preview);
                    }
                    return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        write.Conflict
                            ? CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict
                            : CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CommitFailed,
                        write.Conflict
                            ? CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity
                            : CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogCommit,
                        write.Conflict
                            ? "PUBLISH_CURRENT_CHANGES_WRITE_CONFLICT"
                            : "PUBLISH_CURRENT_CHANGES_COMMIT_FAILED",
                        write.Diagnostic.Length > 0
                            ? write.Diagnostic
                            : "项目目录事务写入失败；草稿仍然安全保留。",
                        frozen.Preview,
                        conflict: write.Conflict);
                }

                CharacterTestRuntimeAiPlannerProjectCatalogState reloaded =
                    ReloadPublishCurrentChanges();
                if (!TryValidateReloadedPublishCurrentChanges(current, reloaded, out string reloadDiagnostic))
                {
                    _recoveryPublishCurrentChangesPlan = current;
                    _pendingPublishCurrentChangesPlan = null;
                    return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                        "PUBLISH_CURRENT_CHANGES_RELOAD_FAILED",
                        reloadDiagnostic,
                        frozen.Preview,
                        catalogChanged: true,
                        requiresRecovery: true);
                }

                _state = reloaded;
                var committed = new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CatalogCommitted,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                    frozen.Preview,
                    current.PublishedCards,
                    current.PublishedGroup,
                    reloaded,
                    conflict: false,
                    catalogChanged: true,
                    requiresRecovery: false,
                    duplicateConfirmation: false,
                    code: "PUBLISH_CURRENT_CHANGES_CATALOG_COMMITTED",
                    message: "行为卡与行为组新版本已原子写入并完成复读校验。");
                _pendingPublishCurrentChangesPlan = null;
                _lastCommittedPublishCurrentChangesToken = normalizedToken;
                _lastCommittedPublishCurrentChangesResult = committed;
                return committed;
            }
            finally
            {
                _publishCurrentChangesCommitInProgress = false;
            }
        }

        internal bool CancelPublishCurrentChanges(string previewToken)
        {
            if (_pendingPublishCurrentChangesPlan == null)
                return false;
            string normalized = previewToken?.Trim() ?? string.Empty;
            if (!string.Equals(
                    normalized,
                    _pendingPublishCurrentChangesPlan.Preview.Token,
                    StringComparison.Ordinal))
            {
                return false;
            }
            _pendingPublishCurrentChangesPlan = null;
            return true;
        }

        private bool CanWritePublishCurrentChanges => _state != null
            && _state.Success
            && (_publishCurrentChangesWriteOverride != null
                || CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.IsRegistered);

        private CharacterTestRuntimeAiPlannerProjectCatalogWriteResult WritePublishCurrentChanges(
            CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
        {
            return _publishCurrentChangesWriteOverride != null
                ? _publishCurrentChangesWriteOverride(request)
                : CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(request);
        }

        private CharacterTestRuntimeAiPlannerProjectCatalogState ReloadPublishCurrentChanges()
        {
            return _publishCurrentChangesReloadOverride != null
                ? _publishCurrentChangesReloadOverride()
                : CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
        }

        private bool TryBuildPublishCurrentChangesPlan(
            string workspaceId,
            int workspaceRevision,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out PublishCurrentChangesPlan plan,
            out CharacterTestRuntimeAiLabPublishCurrentChangesResult failure)
        {
            plan = null;
            failure = null;
            string normalizedWorkspaceId = workspaceId?.Trim() ?? string.Empty;
            if (normalizedWorkspaceId.Length == 0
                || workspaceRevision <= 0
                || group == null
                || cards == null
                || references == null)
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.InvalidSelection,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                    "PUBLISH_CURRENT_CHANGES_SELECTION_INVALID",
                    "当前工作区、行为组或权威引用目录不可用。");
                return false;
            }
            if (!group.HasProjectSource
                || !_state.GroupCatalog.TryGetExact(
                    group.PublishedStableId,
                    group.SourceProjectRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision sourceGroup)
                || sourceGroup.ContentHash != group.SourceProjectContentHash)
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                    "PUBLISH_CURRENT_CHANGES_GROUP_SOURCE_CONFLICT",
                    "行为组草稿来源 revision/hash 已变化；正式目录未写入，请重新加载项目版本。",
                    conflict: true);
                return false;
            }

            var directCards = new List<CharacterTestRuntimeAiLabBehaviorCardDraft>();
            var directDraftIds = new HashSet<string>(StringComparer.Ordinal);
            var stableCardDraftIds = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                bool builtInReference = IsBuiltInReference(entry?.BehaviorId);
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.CardDraftId)
                    || !cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    if (builtInReference)
                        continue;
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.InvalidSelection,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                        "PUBLISH_CURRENT_CHANGES_CARD_DRAFT_MISSING",
                        "行为组包含缺失的行为卡草稿引用。");
                    return false;
                }
                if (!card.HasPublishedSource && !card.HasAuthoredSource)
                {
                    if (builtInReference)
                        continue;
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_CARD_SOURCE_MISSING",
                        "行为卡“" + card.DisplayName + "”没有可追溯的 Published 来源。",
                        conflict: true);
                    return false;
                }
                if (stableCardDraftIds.TryGetValue(
                        card.PublishedStableId,
                        out string previousDraftId)
                    && !string.Equals(previousDraftId, card.CardDraftId, StringComparison.Ordinal))
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_DUPLICATE_CARD_DRAFT",
                        "当前行为组存在同一 Stable ID 的多个不同草稿，无法确定唯一发布正文。",
                        conflict: true);
                    return false;
                }
                stableCardDraftIds[card.PublishedStableId] = card.CardDraftId;
                if (directDraftIds.Add(card.CardDraftId))
                    directCards.Add(card);
            }

            directCards.Sort((left, right) => string.Compare(
                left.PublishedStableId,
                right.PublishedStableId,
                StringComparison.Ordinal));
            var overlayCards = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorCardDraft> pair in cards)
                overlayCards[pair.Key] = pair.Value;
            var publishedCards = new List<CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard>();
            var cardPreviews = new List<CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview>();
            var tacticalBindings = new RuntimeContentHashAccumulator();
            var nextCards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                _state.CardCatalog.Cards.Count + directCards.Count);
            for (int i = 0; i < _state.CardCatalog.Cards.Count; i++)
                nextCards.Add(_state.CardCatalog.Cards[i]);

            for (int i = 0; i < directCards.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft card = directCards[i];
                var authoredCompilation = card.HasAuthoredSource ? CompileTacticalForPublish?.Invoke(card) : null;
                if (card.HasAuthoredSource)
                {
                    if (authoredCompilation?.Success != true || authoredCompilation.Program.TacticalSource?.WorkspaceId != normalizedWorkspaceId
                        || authoredCompilation.Program.TacticalSource.WorkspaceRevision != workspaceRevision)
                    {
                        failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                            CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed,
                            CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight,
                            "PUBLISHED_TACTICAL_CURRENT_COMPILE_REQUIRED", "当前战术卡、共享依赖及实际装配必须通过重新编译。");
                        return false;
                    }
                    tacticalBindings.AddStringStable("card", card.PublishedStableId);
                    tacticalBindings.AddStringStable("input", authoredCompilation.Program.TacticalSource.InputFingerprint);
                }
                bool firstTacticalPublish = card.HasAuthoredSource && !card.HasPublishedSource;
                if (firstTacticalPublish && LatestCardRevision(card.PublishedStableId) != 0)
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISHED_TACTICAL_RESERVED_ID_CONFLICT", "预留卡身份已存在，请重新加载并解决来源冲突。", conflict: true);
                    return false;
                }
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard sourceCard = null;
                if (!firstTacticalPublish && (!_state.CardCatalog.TryGetExact(
                        card.PublishedStableId,
                        card.SourcePublishedRevision,
                        out sourceCard)
                    || sourceCard.SemanticHash != card.SourcePublishedSemanticHash
                    || sourceCard.ContentHash != card.SourcePublishedContentHash))
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_CARD_SOURCE_CONFLICT",
                        "行为卡“" + card.DisplayName + "”的来源 revision/hash 已变化。",
                        conflict: true);
                    return false;
                }
                if (!TryHasUnpublishedCardChanges(
                        card,
                        references,
                        out bool cardChanged,
                        out string compareDiagnostic))
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_CARD_COMPARE_FAILED",
                        compareDiagnostic,
                        conflict: true);
                    return false;
                }
                if (!cardChanged)
                    continue;

                int nextRevision = LatestCardRevision(card.PublishedStableId) + 1;
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                        card,
                        new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                            card.PublishedStableId,
                            nextRevision,
                            sourceCard?.MinimumRuntimeContractVersion ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                            sourceCard?.MaximumRuntimeContractVersion ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                            BuildCanonicalCardMetadata(card, sourceCard),
                            sourceCard?.NumericId ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(card.PublishedStableId)),
                        references, authoredCompilation);
                if (!publish.Success)
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight,
                        publish.Diagnostics.Count > 0
                            ? publish.Diagnostics[0].Code
                            : "PUBLISH_CURRENT_CHANGES_CARD_INVALID",
                        publish.Diagnostics.Count > 0
                            ? publish.Diagnostics[0].Message
                            : "行为卡完整校验未通过。");
                    return false;
                }

                nextCards.Add(publish.Card);
                var overlay = card.Clone(newIdentity: false);
                overlay.CopyDefinitionFrom(
                    CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publish.Card));
                overlayCards[card.CardDraftId] = overlay;
                publishedCards.Add(new CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard(
                    card.CardDraftId,
                    card.CurrentRevision,
                    card.SourcePublishedRevision,
                    card.SourcePublishedSemanticHash,
                    card.SourcePublishedContentHash,
                    publish.Card));
                cardPreviews.Add(new CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview(
                    card.CardDraftId,
                    card.PublishedStableId,
                    card.DisplayName,
                    card.CurrentRevision,
                    card.SourcePublishedRevision,
                    publish.Card.Revision,
                    CountPublishedSourceEvents(card, publish.Card.TacticalSource),
                    CountAffectedOtherGroups(group.PublishedStableId, card.PublishedStableId),
                    DescribePublishedDependencies(publish.Card.TacticalSource)));
            }

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog candidateCards =
                _state.CardCatalog;
            if (publishedCards.Count > 0)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult createCards =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                        _state.CardCatalog.CatalogId,
                        _state.CardCatalog.Revision + 1,
                        nextCards,
                        references,
                        _state.CardCatalog.CatalogNumericId);
                if (!createCards.Success)
                {
                    failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight,
                        createCards.Diagnostics.Count > 0
                            ? createCards.Diagnostics[0].Code
                            : "PUBLISH_CURRENT_CHANGES_CARD_CATALOG_INVALID",
                        createCards.Diagnostics.Count > 0
                            ? createCards.Diagnostics[0].Message
                            : "候选行为卡目录校验未通过。");
                    return false;
                }
                candidateCards = createCards.Catalog;
            }

            if (!TryBuildPublishCurrentChangesGroupCandidate(
                    group,
                    overlayCards,
                    candidateCards,
                    sourceGroup,
                    sourceGroup.Revision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision comparisonGroup,
                    out string comparisonDiagnostic))
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.GroupPreflightFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.GroupPreflight,
                    "PUBLISH_CURRENT_CHANGES_GROUP_INVALID",
                    comparisonDiagnostic);
                return false;
            }
            bool groupChanged = comparisonGroup.ContentHash != sourceGroup.ContentHash;
            if (!groupChanged && publishedCards.Count == 0)
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.NoChanges,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.GroupPreflight,
                    "PUBLISH_CURRENT_CHANGES_NO_CHANGES",
                    "当前行为组及其直接引用行为卡没有需要发布的修改。");
                return false;
            }

            int nextGroupRevision = LatestGroupRevision(group.PublishedStableId) + 1;
            if (!TryBuildPublishCurrentChangesGroupCandidate(
                    group,
                    overlayCards,
                    candidateCards,
                    sourceGroup,
                    nextGroupRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision publishedGroup,
                    out string groupDiagnostic))
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.GroupPreflightFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.GroupPreflight,
                    "PUBLISH_CURRENT_CHANGES_GROUP_INVALID",
                    groupDiagnostic);
                return false;
            }

            var nextGroups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(
                _state.GroupCatalog.Groups.Count + 1);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
                nextGroups.Add(_state.GroupCatalog.Groups[i]);
            nextGroups.Add(publishedGroup);
            var candidateGroupCatalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                _state.GroupCatalog.CatalogId,
                _state.GroupCatalog.Revision + 1,
                nextGroups);
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groupValidation =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(candidateGroupCatalog),
                    candidateCards,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!groupValidation.Success)
            {
                failure = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.GroupPreflightFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.GroupPreflight,
                    "PUBLISH_CURRENT_CHANGES_GROUP_CATALOG_INVALID",
                    groupValidation.Diagnostic);
                return false;
            }

            long sourceIdentityHash = ComputePublishCurrentChangesSourceIdentity(
                normalizedWorkspaceId,
                workspaceRevision,
                group,
                directCards);
            var candidateIdentity = new RuntimeContentHashAccumulator();
            candidateIdentity.AddLong("publishCurrent.cardCatalog", candidateCards.CatalogHash);
            candidateIdentity.AddLong("publishCurrent.group", publishedGroup.ContentHash);
            candidateIdentity.AddLong("publishCurrent.tacticalBindings", tacticalBindings.ToHash());
            var preview = new CharacterTestRuntimeAiLabPublishCurrentChangesPreview(
                Guid.NewGuid().ToString("N"),
                normalizedWorkspaceId,
                workspaceRevision,
                group.GroupDraftId,
                group.CurrentRevision,
                group.PublishedStableId,
                group.DisplayName,
                sourceGroup.Revision,
                publishedGroup.Revision,
                directCards.Count,
                cardPreviews.ToArray(),
                sourceIdentityHash,
                candidateIdentity.ToHash());
            plan = new PublishCurrentChangesPlan
            {
                Preview = preview,
                CardCatalog = candidateCards,
                GroupCatalog = groupValidation.Catalog,
                GroupCatalogJson = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                    groupValidation.Catalog),
                PublishedCards = publishedCards.ToArray(),
                PublishedGroup = publishedGroup
            };
            return true;
        }

        private bool TryBuildPublishCurrentChangesGroupCandidate(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog publishedCards,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            int revision,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
            out string diagnostic)
        {
            candidate = null;
            diagnostic = string.Empty;
            if (draft?.Profile == null
                || cards == null
                || publishedCards == null
                || source == null
                || revision <= 0)
            {
                diagnostic = "行为组聚合候选输入不完整。";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] sourceEntryMatches =
                ResolveCanonicalSourceEntryMatches(draft, cards, source);
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile candidateProfile =
                BuildCanonicalGroupProfile(
                    draft,
                    cards,
                    publishedCards,
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
                bool builtInReference = IsBuiltInReference(entry.BehaviorId);
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
                    || !publishedCards.TryGetExact(
                        card.PublishedStableId,
                        card.SourcePublishedRevision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact)
                    || exact.SemanticHash != card.SourcePublishedSemanticHash
                    || exact.ContentHash != card.SourcePublishedContentHash)
                {
                    diagnostic = "行为条目“" + entry.BehaviorId
                        + "”没有可解析的候选行为卡版本。";
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

        private long ComputePublishCurrentChangesSourceIdentity(
            string workspaceId,
            int workspaceRevision,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("publishCurrent.workspace", workspaceId);
            hash.AddInt("publishCurrent.workspaceRevision", workspaceRevision);
            hash.AddStringStable("publishCurrent.groupDraft", group.GroupDraftId);
            hash.AddInt("publishCurrent.groupDraftRevision", group.CurrentRevision);
            hash.AddStringStable("publishCurrent.groupStable", group.PublishedStableId);
            hash.AddInt("publishCurrent.groupSourceRevision", group.SourceProjectRevision);
            hash.AddLong("publishCurrent.groupSourceHash", group.SourceProjectContentHash);
            hash.AddStringStable("publishCurrent.cardFileHash", _state.CardFileHash);
            hash.AddStringStable("publishCurrent.groupFileHash", _state.GroupFileHash);
            hash.AddLong("publishCurrent.cardCatalogHash", _state.CardCatalog.CatalogHash);
            hash.AddLong("publishCurrent.groupCatalogHash", _state.GroupCatalog.CatalogHash);
            hash.AddInt("publishCurrent.directCardCount", cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft card = cards[i];
                hash.AddStringStable("publishCurrent.cardDraft", card.CardDraftId);
                hash.AddInt("publishCurrent.cardDraftRevision", card.CurrentRevision);
                hash.AddStringStable("publishCurrent.cardStable", card.PublishedStableId);
                hash.AddInt("publishCurrent.cardSourceRevision", card.SourcePublishedRevision);
                hash.AddLong("publishCurrent.cardSourceSemantic", card.SourcePublishedSemanticHash);
                hash.AddLong("publishCurrent.cardSourceContent", card.SourcePublishedContentHash);
            }
            return hash.ToHash();
        }

        private bool TryValidateReloadedPublishCurrentChanges(
            PublishCurrentChangesPlan plan,
            CharacterTestRuntimeAiPlannerProjectCatalogState reloaded,
            out string diagnostic)
        {
            if (reloaded?.Success != true)
            {
                diagnostic = "项目目录已写入，但重新加载失败："
                    + (reloaded?.Diagnostic ?? "未知错误");
                return false;
            }
            for (int i = 0; i < plan.PublishedCards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard expected =
                    plan.PublishedCards[i].Card;
                if (!reloaded.CardCatalog.TryGetExact(
                        expected.StableCardId,
                        expected.Revision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard actual)
                    || actual.SemanticHash != expected.SemanticHash
                    || actual.ContentHash != expected.ContentHash)
                {
                    diagnostic = "项目目录已写入，但行为卡“" + expected.StableCardId
                        + "”的新 revision 复读不一致。";
                    return false;
                }
                if (plan.PublishedCards[i].ExpectedSourceRevision > 0 && !reloaded.CardCatalog.TryGetExact(
                    expected.StableCardId,
                    plan.PublishedCards[i].ExpectedSourceRevision,
                    out _))
                {
                    diagnostic = "项目目录复读发现旧行为卡 revision 被破坏。";
                    return false;
                }
            }
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision expectedGroup = plan.PublishedGroup;
            if (!reloaded.GroupCatalog.TryGetExact(
                    expectedGroup.StableGroupId,
                    expectedGroup.Revision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision actualGroup)
                || actualGroup.ContentHash != expectedGroup.ContentHash)
            {
                diagnostic = "项目目录已写入，但行为组新 revision 复读不一致。";
                return false;
            }
            if (!reloaded.GroupCatalog.TryGetExact(
                expectedGroup.StableGroupId,
                plan.Preview.SourceGroupRevision,
                out _))
            {
                diagnostic = "项目目录复读发现旧行为组 revision 被破坏。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private bool TryConfirmPublishCurrentChangesSourceState(
            CharacterTestRuntimeAiPlannerProjectCatalogState reloaded,
            out string diagnostic)
        {
            var expectedSource = _recoverySourceState ?? _state;
            if (reloaded?.Success != true)
            {
                diagnostic = reloaded?.Diagnostic ?? "项目目录无法重新加载。";
                return false;
            }
            if (!string.Equals(
                    reloaded.CardFileHash,
                    expectedSource.CardFileHash,
                    StringComparison.Ordinal)
                || !string.Equals(
                    reloaded.GroupFileHash,
                    expectedSource.GroupFileHash,
                    StringComparison.Ordinal)
                || reloaded.CardCatalog.CatalogHash != expectedSource.CardCatalog.CatalogHash
                || reloaded.GroupCatalog.CatalogHash != expectedSource.GroupCatalog.CatalogHash)
            {
                diagnostic = "回滚后的文件 hash 或 catalog identity 与发布前不一致。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private int CountAffectedOtherGroups(string currentStableGroupId, string stableCardId)
        {
            var groups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _state.GroupCatalog.Groups.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision group =
                    _state.GroupCatalog.Groups[i];
                if (string.Equals(group.StableGroupId, currentStableGroupId, StringComparison.Ordinal))
                    continue;
                for (int cardIndex = 0; cardIndex < group.Cards.Count; cardIndex++)
                {
                    if (!string.Equals(
                            group.Cards[cardIndex].StableCardId,
                            stableCardId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    groups.Add(group.StableGroupId);
                    break;
                }
            }
            return groups.Count;
        }

        private static string DescribePublishedDependencies(CharacterTestTacticalPublishedSource source)
        {
            if (source == null || source.IsLegacy) return string.Empty;
            var items = new List<string>();
            foreach (var definition in source.CloneDefinitions())
                items.Add(definition.Source.Name + " r" + definition.Revision.ToString(CultureInfo.InvariantCulture));
            return string.Join("、", items);
        }

        private static int CountPublishedSourceEvents(CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestTacticalPublishedSource source)
        {
            if (source == null) return CountAuthoredEvents(card.LogicPayload);
            int count = 0;
            if (source.IsLegacy)
                foreach (var timeline in source.CloneLegacy().Timelines) count += timeline.Events.Count;
            else
                foreach (var definition in source.CloneDefinitions())
                    foreach (var timeline in definition.Source.Timelines) count += timeline.Events.Count;
            return count;
        }

        private static int CountAuthoredEvents(CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            int count = (logic?.LegacySharedEvents.Count ?? 0)
                + (logic?.LegacySharedTriggerEvents.Count ?? 0);
            for (int i = 0; logic != null && i < logic.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = logic.Timelines[i];
                count += (timeline?.Events.Count ?? 0) + (timeline?.TriggerEvents.Count ?? 0);
            }
            return count;
        }

        private static bool IsBuiltInReference(string behaviorId)
        {
            return string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                || string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)
                || CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.IsNoOpPlaceholder(
                    behaviorId);
        }
    }
}
