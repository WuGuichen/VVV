using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabPublishCurrentChangesResult _lastPublishCurrentChangesResult;
        private string _lastCompletedPublishCurrentChangesToken = string.Empty;
        private bool _publishCurrentChangesRecoveryRequired;
        private CharacterTestRuntimeAiLabPublishCurrentChangesResult _publishRecoveryResult;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult LastPublishCurrentChangesResult =>
            _lastPublishCurrentChangesResult;
        internal bool PublishCurrentChangesRecoveryRequired =>
            _publishCurrentChangesRecoveryRequired || _projectCatalogAuthoring?.HasPendingPublishRecovery == true;

        /// <summary>Flushes the current Main draft and builds a frozen, read-only publish summary.</summary>
        /// <remarks>LowFreqAlloc: explicit authoring command only; does not write Published catalogs.</remarks>
        internal bool TryPreparePublishCurrentBehaviorGroupChanges(
            out CharacterTestRuntimeAiLabPublishCurrentChangesPreview preview,
            out string message)
        {
            preview = null;
            if (PublishCurrentChangesRecoveryRequired)
            {
                _lastPublishCurrentChangesResult =
                    _publishRecoveryResult ?? CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                        "PUBLISH_CURRENT_CHANGES_RECOVERY_REQUIRED",
                        "上一次发布后的 Catalog 一致性尚未恢复；请先重新加载项目版本。",
                        _publishRecoveryResult?.Preview ?? _projectCatalogAuthoring?.GetPublishRecoveryPreview(),
                        requiresRecovery: true);
                message = _lastPublishCurrentChangesResult.Message;
                _statusMessage = message;
                return false;
            }
            if (!CanPublishFromActiveAuthoringWorkspace(out message))
            {
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.MainWorkspaceRequired,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.WorkspaceRole,
                        "PUBLISH_CURRENT_CHANGES_MAIN_REQUIRED",
                        message);
                _statusMessage = message;
                return false;
            }

            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (workspace == null
                || workspace.IsReadOnly
                || !workspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                message = workspace?.IsReadOnly == true
                    ? "当前 Main Workspace 为只读状态。"
                    : "当前角色槽位没有可发布的行为组草稿。";
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.InvalidSelection,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                        "PUBLISH_CURRENT_CHANGES_SELECTION_INVALID",
                        message);
                _statusMessage = message;
                return false;
            }
            if (!CanAdoptPublishedGroupInCurrentMatch(group.PublishedStableId))
            {
                message = "当前实验配置无法收养所选行为组的新 revision；正式目录未写入。";
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.InvalidSelection,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                        "PUBLISH_CURRENT_CHANGES_GROUP_ADOPTION_UNAVAILABLE",
                        message);
                _statusMessage = message;
                return false;
            }

            if (_draftPersistence == null || !_draftPersistence.Flush())
            {
                message = "保存当前草稿失败；Published 目录没有改变。"
                    + (_draftPersistence?.LastStatus ?? string.Empty);
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.DraftFlushFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.DraftFlush,
                        "PUBLISH_CURRENT_CHANGES_DRAFT_FLUSH_FAILED",
                        message);
                _statusMessage = message;
                return false;
            }

            if (_projectCatalogAuthoring == null && ProjectCatalogState?.Success == true)
            {
                _projectCatalogAuthoring =
                    new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
                        ProjectCatalogState);
            }
            if (_projectCatalogAuthoring == null)
            {
                message = ProjectCatalogState?.Diagnostic ?? "项目行为目录不可用。";
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReadOnly,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.SourceIdentity,
                        "PUBLISH_CURRENT_CHANGES_PROJECT_CATALOG_UNAVAILABLE",
                        message);
                _statusMessage = message;
                return false;
            }

            EnsureBehaviorCardReferences();
            if (!TryRunCompatibilityPublishPreflight(group, out message))
            {
                _lastPublishCurrentChangesResult =
                    CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                        CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed,
                        CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight,
                        "PUBLISH_CURRENT_CHANGES_COMPATIBILITY_PREFLIGHT_FAILED",
                        message);
                _statusMessage = message;
                return false;
            }

            _projectCatalogAuthoring.CompileTacticalForPublish = card => CompileAuthoredCard(card, true);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                _projectCatalogAuthoring.PreparePublishCurrentChanges(
                    workspace.WorkspaceId,
                    workspace.CurrentRevision,
                    group,
                    workspace.Cards,
                    _behaviorCardReferences);
            _lastPublishCurrentChangesResult = result;
            preview = result.Preview;
            message = result.Message;
            _statusMessage = message;
            return result.PreviewReady;
        }

        /// <summary>Confirms one previously frozen summary and performs the aggregate transaction.</summary>
        /// <remarks>LowFreqAlloc: explicit authoring confirmation only.</remarks>
        internal bool TryConfirmPublishCurrentBehaviorGroupChanges(
            string previewToken,
            out CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            out string message)
        {
            string normalizedToken = previewToken?.Trim() ?? string.Empty;
            if (normalizedToken.Length > 0
                && string.Equals(
                    normalizedToken,
                    _lastCompletedPublishCurrentChangesToken,
                    StringComparison.Ordinal)
                && _lastPublishCurrentChangesResult?.Success == true)
            {
                result = _lastPublishCurrentChangesResult.AsDuplicateConfirmation();
                message = result.Message;
                return true;
            }
            if (PublishCurrentChangesRecoveryRequired)
            {
                result = _publishRecoveryResult?.AsDuplicateConfirmation() ?? CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                    "PUBLISH_CURRENT_CHANGES_RECOVERY_REQUIRED",
                    "项目目录需要恢复；重复发布已被禁用。",
                    _publishRecoveryResult?.Preview ?? _projectCatalogAuthoring?.GetPublishRecoveryPreview(),
                    requiresRecovery: true);
                return FinishPublishCurrentChanges(result, out message);
            }
            if (!CanPublishFromActiveAuthoringWorkspace(out message))
            {
                result = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.MainWorkspaceRequired,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.WorkspaceRole,
                    "PUBLISH_CURRENT_CHANGES_MAIN_REQUIRED",
                    message);
                return FinishPublishCurrentChanges(result, out message);
            }

            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (workspace == null
                || !workspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                result = CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.InvalidSelection,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                    "PUBLISH_CURRENT_CHANGES_SELECTION_INVALID",
                    "发布确认时当前行为组选择已经失效。");
                return FinishPublishCurrentChanges(result, out message);
            }

            EnsureBehaviorCardReferences();
            _projectCatalogAuthoring.CompileTacticalForPublish = card => CompileAuthoredCard(card, true);
            result = _projectCatalogAuthoring.CommitPublishCurrentChanges(
                normalizedToken,
                workspace.WorkspaceId,
                workspace.CurrentRevision,
                group,
                workspace.Cards,
                _behaviorCardReferences);
            if (!result.CatalogCommitSucceeded)
            {
                if (result.RequiresRecovery)
                    _publishCurrentChangesRecoveryRequired = true;
                return FinishPublishCurrentChanges(result, out message);
            }

            return CompletePublishedChanges(ref result, normalizedToken, false, out message);
        }

        private bool CompletePublishedChanges(ref CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            string normalizedToken, bool recovery, out string message)
        {
            var workspace = DraftWorkspace;
            if (!workspace.TryStagePublishCurrentChangesCheckpoint(
                    result,
                    result.State.GroupFileHash,
                    out CharacterTestRuntimeAiLabDraftWorkspace.PublishCurrentChangesCheckpoint checkpoint,
                    out string checkpointMessage, recovery))
            {
                result = result.FailAfterCatalogCommit(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.DraftCheckpoint,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.DraftCheckpointFailed,
                    "PUBLISH_CURRENT_CHANGES_CHECKPOINT_STAGE_FAILED",
                    "正式版本已更新，但草稿 checkpoint 无法暂存：" + checkpointMessage);
                _publishCurrentChangesRecoveryRequired = true;
                return FinishPublishCurrentChanges(result, out message);
            }

            _draftPersistence.MarkDirty();
            if (!_draftPersistence.Flush())
            {
                checkpoint.TryRollback(out string rollbackMessage);
                result = result.FailAfterCatalogCommit(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.DraftCheckpoint,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.DraftCheckpointFailed,
                    "PUBLISH_CURRENT_CHANGES_CHECKPOINT_FLUSH_FAILED",
                    "正式版本已更新，但草稿 checkpoint 持久化失败："
                        + _draftPersistence.LastStatus + " " + rollbackMessage);
                _publishCurrentChangesRecoveryRequired = true;
                return FinishPublishCurrentChanges(result, out message);
            }
            checkpoint.Commit();

            if (!_matchConfig.AdoptPublishedProjectGroup(result.Group, result.State))
            {
                result = result.FailAfterCatalogCommit(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.DraftCheckpoint,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.DraftCheckpointFailed,
                    "PUBLISH_CURRENT_CHANGES_RUNTIME_STATE_REFRESH_FAILED",
                    "正式版本和草稿 checkpoint 已更新，但当前实验目录状态刷新失败。");
                _publishCurrentChangesRecoveryRequired = true;
                return FinishPublishCurrentChanges(result, out message);
            }

            for (int i = 0; i < result.PublishedCards.Count; i++)
            {
                SetBehaviorRecipeSaveCheckpoint(
                    workspace,
                    result.PublishedCards[i].CardDraftId);
            }
            SetBehaviorGroupSaveCheckpoint(workspace, result.Preview.GroupDraftId);
            if (!_projectCatalogAuthoring.CompletePublishRecoveryJournal(out string recoveryRecordIssue))
            {
                result = result.FailAfterCatalogCommit(CharacterTestRuntimeAiLabPublishCurrentChangesStage.DraftCheckpoint,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.DraftCheckpointFailed,
                    "PUBLISH_RECOVERY_RECORD_CLEAR_FAILED", "正式版本和工作副本已同步，但恢复记录尚未清理：" + recoveryRecordIssue);
                _publishCurrentChangesRecoveryRequired = true;
                return FinishPublishCurrentChanges(result, out message);
            }
            _hasPublishPreflightCache = false;
            _behaviorCardCompileCache.Clear();
            BuildCandidateRevision();
            message = "发布完成，游戏可用版本已更新。当前测试不会自动中断。";
            result = result.CompleteAfterDraftCheckpoint(message);
            _lastCompletedPublishCurrentChangesToken = normalizedToken;
            _lastPublishSucceeded = true;
            _lastPublishStableCardId = result.PublishedCards.Count > 0
                ? result.PublishedCards[0].Card.StableCardId
                : string.Empty;
            _lastPublishStatus = message;
            _publishCurrentChangesRecoveryRequired = false;
            _publishRecoveryResult = null;
            return FinishPublishCurrentChanges(result, out message);
        }

        internal bool TryRecoverPublishCurrentBehaviorGroupChanges(string token,
            out CharacterTestRuntimeAiLabPublishCurrentChangesResult result, out string message)
        {
            if (!PublishCurrentChangesRecoveryRequired)
            {
                result = _lastPublishCurrentChangesResult;
                message = result?.Message ?? "没有待恢复的发布。";
                return result?.Success == true;
            }
            var recoveryPreview = _publishRecoveryResult?.Preview ?? _projectCatalogAuthoring?.GetPublishRecoveryPreview();
            if (recoveryPreview?.Token != token || _projectCatalogAuthoring == null)
            {
                result = _publishRecoveryResult;
                message = "恢复请求与原发布不一致，请重新打开恢复入口。";
                return false;
            }
            EnsureBehaviorCardReferences();
            result = _projectCatalogAuthoring.RecoverPublishCurrentChanges(token, _behaviorCardReferences);
            if (!result.CatalogCommitSucceeded)
            {
                if (!result.RequiresRecovery && result.Status == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Cancelled)
                { _publishCurrentChangesRecoveryRequired = false; _publishRecoveryResult = null; }
                return FinishPublishCurrentChanges(result, out message);
            }
            return CompletePublishedChanges(ref result, token, true, out message);
        }

        internal bool CancelPublishCurrentBehaviorGroupChanges(
            string previewToken,
            out string message)
        {
            bool cancelled = _projectCatalogAuthoring != null
                && _projectCatalogAuthoring.CancelPublishCurrentChanges(previewToken);
            message = cancelled
                ? "已取消发布；正式目录没有改变。"
                : "发布摘要已经失效，无需取消。";
            _lastPublishCurrentChangesResult =
                CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Cancelled,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.SelectionFreeze,
                    "PUBLISH_CURRENT_CHANGES_CANCELLED",
                    message);
            _statusMessage = message;
            return cancelled;
        }

        private bool FinishPublishCurrentChanges(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            out string message)
        {
            _lastPublishCurrentChangesResult = result;
            if (result?.RequiresRecovery == true && result.Preview != null) _publishRecoveryResult = result;
            message = result?.Message ?? "发布失败，草稿仍然安全保留。";
            _statusMessage = message;
            if (result?.Success != true)
            {
                _lastPublishSucceeded = false;
                _lastPublishStatus = message;
            }
            return result?.Success == true;
        }

        private bool TryRunCompatibilityPublishPreflight(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string message)
        {
            // Keep the legacy Card/Group gates authoritative for their existing validation and
            // source checks. Aggregate publishing intentionally treats their two old no-op reasons
            // as non-blocking because a metadata-only Card change or a Card-only change still
            // requires a new Group revision, and a running frozen test must not be interrupted.
            if (!CanPublishBehaviorGroupDraft(group, out string groupReason)
                && !string.Equals(
                    groupReason,
                    "当前行为组没有需要保存的修改。",
                    StringComparison.Ordinal))
            {
                message = groupReason;
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (DraftWorkspace.Cards.TryGetValue(entry.CardDraftId, out var authored) && authored.HasAuthoredSource)
                {
                    var compiled = CompileAuthoredCard(authored, true);
                    if (!compiled.Success) { message = compiled.Diagnostics.Count > 0 ? compiled.Diagnostics[0].Message : "战术卡编译失败。"; return false; }
                    continue;
                }
                if (!DraftWorkspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !card.HasPublishedSource)
                {
                    continue;
                }
                if (CanPublishBehaviorCardDraft(group, i, out string cardReason))
                    continue;
                bool expectedNoOp = cardReason.StartsWith(
                        "当前逻辑与 Published 来源一致",
                        StringComparison.Ordinal)
                    || string.Equals(
                        cardReason,
                        "Publish 只能在战前配置阶段执行。",
                        StringComparison.Ordinal);
                if (!expectedNoOp)
                {
                    message = cardReason;
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        private bool CanPublishFromActiveAuthoringWorkspace(out string message)
        {
            if (IsConfiguredTestCardEditing) { message = "测试单卡只保存到测试库，请回基础战术卡库发布基础卡。"; return false; }
#if UNITY_EDITOR
            if (!HasActiveAuthoringWorkspaceRole)
            {
                message = "当前没有可用的 Authoring Workspace Role。";
                return false;
            }
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition role =
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(
                    ActiveAuthoringWorkspaceRole);
            if (!role.CanPublish)
            {
                message = role.DisplayName + " 不能直接发布；请先应用到 Main Workspace。";
                return false;
            }
            message = string.Empty;
            return true;
#else
            message = "Player 构建只读；项目行为目录只能在 Unity Editor 中发布。";
            return false;
#endif
        }

        private bool CanAdoptPublishedGroupInCurrentMatch(string stableGroupId)
        {
            for (int i = 0; _matchConfig?.BehaviorGroups != null
                && i < _matchConfig.BehaviorGroups.Drafts.Count; i++)
            {
                if (string.Equals(
                    _matchConfig.BehaviorGroups.Drafts[i].StableId,
                    stableGroupId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
