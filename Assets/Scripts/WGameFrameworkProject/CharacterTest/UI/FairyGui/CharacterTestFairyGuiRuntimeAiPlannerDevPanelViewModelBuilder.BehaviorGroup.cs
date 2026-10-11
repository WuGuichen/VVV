using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using MxFramework.CharacterAction;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static readonly Unity.Profiling.ProfilerMarker WorkbenchBuildMarker = new Unity.Profiling.ProfilerMarker("CharacterTest.UI.BuildAiLabWorkbench");
        private static void FillWorkbench(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabWorkspaceMode workspaceMode,
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab,
            int selectedEntryIndex,
            int selectedCompiledStepIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedLogicElementKind,
            int selectedLogicElementIndex,
            bool isSelectedCardExpanded,
            CharacterTestRuntimeAiPlannerDevPresentationContext presentationContext,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame,
            in CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent,
            bool hasSelectedAgent,
            int latestFrame,
            int workbenchScrollOffset)
        {
            using var profileScope = WorkbenchBuildMarker.Auto();
            // Installed-card selection only projects group metadata. It must not create a card editor/probe session.
            bool groupManagementOnly = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            if (groupManagementOnly) isSelectedCardExpanded = false;
            viewModel.WorkspaceMode = workspaceMode;
            viewModel.DiagnosticsTab = diagnosticsTab;
            viewModel.WorkbenchCoreReplacementActive =
                controller?.IsCoreSlotReplacementActive == true;
            viewModel.IsSelectedCardExpanded =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool runtimeBehaviorGroupEditing = viewModel.ShowBehaviorLogic;
            bool behaviorGroupAuthoring =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || runtimeBehaviorGroupEditing;
            viewModel.ShowWorkbench =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || runtimeBehaviorGroupEditing;
            CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.Reset(viewModel.AuthoringStatus);
            viewModel.DraftStatusText = string.Empty;
            viewModel.PublishDraftStatusText = string.Empty;
            CharacterTestRuntimeAiLabWorkspaceViewModel workspaceView = viewModel.Workspace
                ?? new CharacterTestRuntimeAiLabWorkspaceViewModel();
            viewModel.Workspace = workspaceView;
            workspaceView.ActiveWorkspace = workspaceMode;
            workspaceView.ActiveDiagnosticsTab = diagnosticsTab;
            workspaceView.ShowSetup =
                workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            workspaceView.ShowBehaviorGroup = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            workspaceView.ShowBehaviorCard = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            workspaceView.ShowDiagnostics = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
            viewModel.ToggleDetailsLabel = viewModel.ShowWorkbench ? "运行调试" : "行为编辑";
            if (controller != null)
            {
                viewModel.AuthoringStatus.TestSourceState = (CharacterTestRuntimeAiLabTestSourceUxState)
                    controller.CurrentTestSource;
                viewModel.AuthoringStatus.TestSourceStatusText = controller.CurrentTestSourceStatusText;
            }
            if (!viewModel.ShowWorkbench)
                return;
            if (controller == null)
            {
                FillUnavailableAuthoringWorkspace(viewModel, "Runtime AI Lab 控制器不可用。");
                return;
            }
            CharacterTestRuntimeAiLabDraftWorkspace workspace = controller.DraftWorkspace;
            if (workspace == null)
            {
                FillUnavailableAuthoringWorkspace(
                    viewModel,
                    ResolveAuthoringWorkspaceUnavailableDiagnostic(controller));
                return;
            }
            if (!controller.TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft draft))
            {
                viewModel.WorkbenchSummaryText = "行为组草稿不可用。";
                return;
            }
            viewModel.WorkbenchGroupIdentity = draft.GroupDraftId ?? string.Empty;
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorEntryDraft> entries = draft.Entries;
            bool entriesStructurallyComplete = true;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i]?.RuntimeEntry == null
                    || string.IsNullOrWhiteSpace(entries[i].CardDraftId)
                    || !workspace.Cards.ContainsKey(entries[i].CardDraftId))
                {
                    entriesStructurallyComplete = false;
                    break;
                }
            }
            CharacterTestRuntimeAiPlannerStateSnapshot selectedPlannerState = selectedFrame != null
                ? selectedFrame.RuntimeAiPlannerStateSnapshot
                : controller.CaptureRuntimeAiPlannerStateForLab();
            int identityFrame = selectedFrame?.Frame ?? Math.Max(0, latestFrame);
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identityProjection =
                viewModel.BehaviorCardIdentity
                ?? new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection();
            viewModel.BehaviorCardIdentity = identityProjection;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtimeIdentity =
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntimeInto(
                    identityProjection.Runtime,
                    controller,
                    selectedPlannerState,
                    identityFrame,
                    selectedAgent,
                    hasSelectedAgent,
                    requireCapturedState: selectedFrame != null);
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot selectedLogicAgentState =
                runtimeIdentity.LogicAgent;
            int executingEntryIndex = entriesStructurallyComplete
                ? CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.FindMatchingEditEntryIndex(
                    draft,
                    runtimeIdentity.CommittedCard)
                : -1;
            bool selectedFrameIsLatest = selectedFrame != null
                && selectedFrame.Frame == latestFrame;
            bool selectedFrameIsHistorical = selectedFrame != null
                && selectedFrame.Frame < latestFrame;
            CharacterTestRuntimeAiLabExecutionDisplayState executionState = executingEntryIndex < 0
                ? CharacterTestRuntimeAiLabExecutionDisplayState.None
                : selectedFrameIsLatest
                    ? CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow
                    : CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame;

            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog =
                controller.MatchConfig.BehaviorGroups.DefinitionCatalog;
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                behaviorGroupAuthoring
                    ? CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                        draft.Profile,
                        definitionCatalog)
                    : null;
            bool editable = presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Setup
                || (runtimeBehaviorGroupEditing && !selectedFrameIsHistorical)
                || (isSelectedCardExpanded
                    && controller.CanEditBehaviorCardDraft
                    && !selectedFrameIsHistorical);
            bool selectedEntryStructurallyValid = selectedEntryIndex >= 0
                && selectedEntryIndex < entries.Count
                && entries[selectedEntryIndex]?.RuntimeEntry != null
                && !string.IsNullOrWhiteSpace(entries[selectedEntryIndex].CardDraftId)
                && workspace.Cards.ContainsKey(entries[selectedEntryIndex].CardDraftId);
            const string InvalidSelectedEntryReason =
                "所选行为卡条目已损坏；请选择有效条目，或删除后重新创建。";
            CharacterTestRuntimeAiLabPlayerLegalityState playerLegality = entriesStructurallyComplete
                ? controller.EvaluateSelectedPlayerLegality(draft)
                : CharacterTestRuntimeAiLabPlayerLegalityState.Unavailable(
                    InvalidSelectedEntryReason);
            viewModel.PlayerLegalityState = playerLegality;
            viewModel.PlayerLegalityStatusText = FormatPlayerLegalityStatus(playerLegality);
            viewModel.PlayerLegalityReasonText = FormatPlayerLegalityReasons(playerLegality);
            bool selectedCardHasUnsavedChanges = selectedEntryStructurallyValid
                && controller.HasBehaviorRecipeChangesToSaveForEntry(selectedEntryIndex);
            string selectedCardSaveReason = InvalidSelectedEntryReason;
            bool selectedCardCanSave = selectedEntryStructurallyValid
                && controller.CanSaveBehaviorRecipeForEntry(
                    selectedEntryIndex,
                    out selectedCardSaveReason);
            if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                viewModel.TimelineStatusText = controller.BehaviorRecipeSyncStatusText;
            viewModel.CanEditWorkbench = editable;
            viewModel.CanUndoDraft = editable && workspace.CanUndo;
            viewModel.CanSaveDraft = !selectedFrameIsHistorical
                && !workspace.IsReadOnly
                && selectedCardHasUnsavedChanges;
            viewModel.CanApplyDraft = !selectedFrameIsHistorical
                && !workspace.IsReadOnly
                && selectedCardHasUnsavedChanges
                && selectedCardCanSave
                && controller.CanSaveBehaviorRecipeAndRun;
            viewModel.ApplyDraftDisabledReasonText = selectedFrameIsHistorical
                ? "HISTORICAL_FRAME_READ_ONLY"
                : !selectedEntryStructurallyValid
                ? InvalidSelectedEntryReason
                : !selectedCardHasUnsavedChanges
                ? "当前没有需要保存的修改。"
                : !selectedCardCanSave
                ? selectedCardSaveReason
                : viewModel.CanApplyDraft
                ? string.Empty
                : "当前处于只读、Seek、回放重算或未完成确认事务中。";
            string publishReason = string.Empty;
            if (!selectedCardHasUnsavedChanges && !selectedFrameIsHistorical)
            {
                if (behaviorGroupAuthoring)
                {
                    if (entriesStructurallyComplete)
                    {
                        viewModel.CanPublishDraft =
                            controller.CanPublishBehaviorGroupDraft(draft, out publishReason);
                    }
                    else
                    {
                        viewModel.CanPublishDraft = false;
                        publishReason = InvalidSelectedEntryReason;
                    }
                }
                else if (selectedEntryStructurallyValid)
                {
                    viewModel.CanPublishDraft = controller.CanPublishBehaviorCardDraft(
                        draft,
                        selectedEntryIndex,
                        out publishReason);
                }
                else
                {
                    viewModel.CanPublishDraft = false;
                    publishReason = InvalidSelectedEntryReason;
                }
            }
            else
            {
                viewModel.CanPublishDraft = false;
            }
            viewModel.PublishDraftLabel = behaviorGroupAuthoring
                ? "保存行为组"
                : "发布行为版本";
            if (workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
            {
                viewModel.ApplyDraftLabel = "保存";
                viewModel.WorkspaceBackLabel = "返回";
            }
            if (selectedFrameIsHistorical)
                publishReason = "HISTORICAL_FRAME_READ_ONLY";
            else if (!selectedEntryStructurallyValid && !behaviorGroupAuthoring)
                publishReason = InvalidSelectedEntryReason;
            else if (selectedCardHasUnsavedChanges)
                publishReason = "有未保存修改；请先保存。";
            viewModel.PublishDraftDisabledReasonText = publishReason;
            string saveBehaviorGroupReason = string.Empty;
            viewModel.CanSaveBehaviorGroup = entriesStructurallyComplete
                && !selectedFrameIsHistorical
                && !workspace.IsReadOnly
                && controller.CanPublishBehaviorGroupDraft(draft, out saveBehaviorGroupReason);
            if (!entriesStructurallyComplete)
                saveBehaviorGroupReason = InvalidSelectedEntryReason;
            else if (selectedFrameIsHistorical)
                saveBehaviorGroupReason = "HISTORICAL_FRAME_READ_ONLY";
            else if (workspace.IsReadOnly)
                saveBehaviorGroupReason = "当前行为组处于只读状态。";
            viewModel.SaveBehaviorGroupDisabledReasonText = saveBehaviorGroupReason;
            viewModel.CanReloadProjectDraft = behaviorGroupAuthoring
                && draft.HasProjectSource;
            viewModel.CanReapplyProjectDraft = viewModel.CanReloadProjectDraft
                && controller.HasProjectCatalogWriteConflict;
            CharacterTestRuntimeAiLabBehaviorCardDraft publishedSourceCard = null;
            if (selectedEntryIndex >= 0
                && selectedEntryIndex < draft.Entries.Count
                && draft.Entries[selectedEntryIndex] != null
                && !string.IsNullOrWhiteSpace(draft.Entries[selectedEntryIndex].CardDraftId))
            {
                workspace.Cards.TryGetValue(
                    draft.Entries[selectedEntryIndex].CardDraftId,
                    out publishedSourceCard);
            }
            viewModel.ShowProjectCardMetadata = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                && publishedSourceCard?.HasPublishedSource == true;
            viewModel.CanEditProjectCardMetadata = viewModel.ShowProjectCardMetadata
                && !selectedFrameIsHistorical
                && controller.ProjectCatalogState?.CanWriteProjectCatalogs == true;
            if (viewModel.ShowProjectCardMetadata)
            {
                viewModel.ProjectCardMetadataTitle = publishedSourceCard.DisplayName
                    + " · r"
                    + publishedSourceCard.SourcePublishedRevision.ToString(CultureInfo.InvariantCulture);
                viewModel.ProjectCardDisplayName = publishedSourceCard.DisplayName;
                viewModel.ProjectCardCost = publishedSourceCard.Cost.ToString(CultureInfo.InvariantCulture);
                viewModel.ProjectCardDescription = publishedSourceCard.ShortDescription;
                viewModel.ProjectCardUseSummary = publishedSourceCard.UseSummary;
                viewModel.ProjectCardRiskSummary = publishedSourceCard.RiskSummary;
                viewModel.ProjectCardTags = publishedSourceCard.TagsText;
            }
            if (entriesStructurallyComplete)
                FillWorkbenchLibraryRows(viewModel, controller, workspace, draft, editable);
            else
                viewModel.WorkbenchLibraryRows = Array.Empty<CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel>();

            bool hideRuntimeSupportEntries = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                && draft.IsBehaviorTestComposition;
            string primaryBehaviorCardDraftId = hideRuntimeSupportEntries
                && entriesStructurallyComplete
                ? draft.PrimaryBehaviorTestCardDraftId(workspace.Cards)
                : string.Empty;
            int visibleEntryCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!hideRuntimeSupportEntries
                    || !IsBehaviorTestRuntimeSupportEntry(entries[i], primaryBehaviorCardDraftId))
                    visibleEntryCount++;
            }
            viewModel.WorkbenchTitleText = FormatBehaviorGroupDisplayName(draft.DisplayName);
            viewModel.WorkbenchEntryCountText =
                visibleEntryCount.ToString(CultureInfo.InvariantCulture);
            viewModel.WorkbenchDiagnosticText = string.Empty;
            bool showGroupRows = behaviorGroupAuthoring
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            int visibleRowCapacity = showGroupRows ? visibleEntryCount : 0;
            viewModel.WorkbenchRowCount = visibleRowCapacity;
            viewModel.WorkbenchScrollOffset = 0;
            viewModel.WorkbenchMaxScrollOffset = 0;
            int invalidCardCount = 0;
            bool groupHasValidationProblems = !entriesStructurallyComplete
                || compile?.Success != true;
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows = EnsureWorkbenchRowCapacity(
                viewModel,
                visibleRowCapacity);
            if (showGroupRows)
            {
                int rowIndex = 0;
                for (int entryIndex = 0; entryIndex < entries.Count && rowIndex < visibleEntryCount; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = entries[entryIndex];
                    if (hideRuntimeSupportEntries
                        && IsBehaviorTestRuntimeSupportEntry(entry, primaryBehaviorCardDraftId))
                        continue;
                    if (entry?.RuntimeEntry == null
                        || string.IsNullOrWhiteSpace(entry.CardDraftId)
                        || !workspace.Cards.ContainsKey(entry.CardDraftId))
                    {
                        FillInvalidWorkbenchRow(
                            rows[rowIndex],
                            entryIndex,
                            entryIndex == selectedEntryIndex);
                        invalidCardCount++;
                        rowIndex++;
                        continue;
                    }
                string title = "未命名行为卡";
                string output = "仅提供事实";
                if (definitionCatalog.TryGetDefinition(
                        entry.BehaviorId,
                        out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                {
                    output = CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                        definition.ActionPrimitive);
                }
                workspace.Cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft rowCard);
                title = rowCard != null && !string.IsNullOrWhiteSpace(rowCard.DisplayName)
                    ? rowCard.DisplayName
                    : "未命名行为卡";
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult rowCompile =
                    controller.CompileBehaviorCardLogic(rowCard);
                string compileBadge = FormatCompileBadge(
                    rowCompile,
                    rowCard?.LogicPayloadMigratedFromV1 == true);
                bool rowInvalid = rowCompile?.Success != true;
                if (rowInvalid)
                    invalidCardCount++;
                bool reactiveDodgeRow = string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal);
                bool continuousApproachRow = string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal);
                float rowScore = rowCompile?.Program?.SelectionDefinition?.Priority
                    ?? CharacterTestBehaviorCardSelectionDefinition.FromCard(rowCard).Priority;
                bool rowExecuting = entryIndex == executingEntryIndex;
                string semanticBadge = reactiveDodgeRow
                    ? "威胁响应"
                    : continuousApproachRow ? "持续行为" : compileBadge;
                string executionStepId = rowExecuting
                    ? runtimeIdentity.CurrentStageId
                    : string.Empty;
                string executionBadge = executionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow
                    ? "● 正在执行"
                    : executionState == CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame
                        ? "◷ 当时执行"
                        : string.Empty;
                string executionDetail = rowExecuting
                    ? executionBadge
                        + " · 帧 " + (selectedFrame?.Frame ?? 0).ToString(CultureInfo.InvariantCulture)
                        + (string.IsNullOrEmpty(executionStepId) ? string.Empty : " · 步骤 " + executionStepId)
                    : string.Empty;

                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[rowIndex];
                row.RowKind = CharacterTestRuntimeAiLabWorkbenchRowKind.BehaviorCard;
                row.EntryIndex = entryIndex;
                row.ResponsibilityKinds = CopyResponsibilities(
                    rowCard?.HasPublishedSource == true
                        ? rowCard.ResponsibilityKinds
                        : entry.ResponsibilityKinds);
                row.ResponsibilityKind = row.ResponsibilityKinds[0];
                row.ResponsibilityCompatibilityMask = BuildResponsibilityCompatibilityMask(
                    entry,
                    rowCard,
                    definition);
                row.ResponsibilityDisplayText = FormatResponsibilitySelection(
                    row.ResponsibilityKinds);
                row.CardDraftId = rowCard?.CardDraftId ?? entry.CardDraftId;
                row.PublishedStableId = rowCard?.HasPublishedSource == true
                    ? rowCard.PublishedStableId
                    : string.Empty;
                row.DraftRevision = rowCard?.CurrentRevision ?? 0;
                row.PublishState = rowCard == null
                    ? CharacterTestRuntimeAiLabPublishUxState.Unavailable
                    : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                        rowCard.HasPublishedSource,
                        rowCard.HasPublishedSource
                            && controller.HasUnpublishedBehaviorCardContentForDisplay(
                                draft,
                                entryIndex,
                                out _));
                row.PublishStatusText =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                        row.PublishState);
                row.HasValidationProblems = rowInvalid;
                row.ValidationStatusText =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(
                        rowInvalid);
                row.LogicPayload = rowCard?.LogicPayload;
                row.IconKey = ResolveWorkbenchBehaviorIconKey(rowCard, entry.CoreSlotKind);
                row.IsCurrentBehavior = draft.IsBehaviorTestComposition
                    && rowCard != null
                    && string.Equals(
                        rowCard.CardDraftId,
                        draft.PrimaryBehaviorTestCardDraftId(workspace.Cards),
                        StringComparison.Ordinal);
                row.TitleText = row.IsCurrentBehavior
                    ? "当前行为 · " + title
                    : title;
                row.BadgeText = entry.Disabled
                    ? "已禁用"
                    : rowInvalid
                        ? "条件不完整"
                        : "启用";
                row.TriggerText = rowInvalid ? string.Empty : semanticBadge;
                row.ActionText = CharacterTestRuntimeAiLabGameplayAuthoring.BuildActionSummary(rowCard?.Payload);
                row.OutcomeText = CharacterTestRuntimeAiLabGameplayAuthoring.BuildTriggerSummary(
                    rowCard?.Payload);
                if (rowCard?.HasAuthoredSource == true)
                {
                    row.BadgeText = entry.Disabled ? "已禁用" : "战术源";
                    row.ActionText = rowCompile?.Program?.TacticalSource?.Description ?? "当前源尚未成功编译";
                    row.OutcomeText = row.PublishStatusText;
                }
                row.PriorityText = rowExecuting
                        ? executionDetail
                        : "优先级 " + rowScore.ToString("0.##", CultureInfo.InvariantCulture);
                row.ExecutionDetailText = executionDetail;
                row.ExecutionState = rowExecuting
                        ? executionState
                        : CharacterTestRuntimeAiLabExecutionDisplayState.None;
                row.ExecutionStepId = executionStepId;
                row.Label = (rowIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + ". "
                        + title
                        + "  ->  "
                        + output
                        + "  ["
                        + compileBadge
                        + "]"
                        + (entry.Disabled ? "  [已禁用]" : string.Empty);
                row.Visible = true;
                row.Selected = entryIndex == selectedEntryIndex;
                row.Disabled = entry.Disabled;
                row.Invalid = rowInvalid;
                    rowIndex++;
                }
                viewModel.WorkbenchResponsibilityCounts = BuildResponsibilityCounts(
                    rows,
                    rowIndex);
                viewModel.WorkbenchRowCount = rowIndex;
                for (int i = rowIndex; i < rows.Length; i++)
                    rows[i].Visible = false;
                string behaviorGroupSummary = compile != null
                    && compile.Success
                    && invalidCardCount == 0
                    ? string.Empty
                    : "无效：存在配置问题";
                bool responsibilitiesValid = controller.DraftWorkspace
                    .TryValidateResponsibilityAssignments(
                        draft,
                        out string responsibilityMessage);
                var contract = new CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport(
                    responsibilitiesValid,
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible,
                    responsibilityMessage);
                if (!contract.Success)
                    behaviorGroupSummary = contract.Message;
                else if (string.IsNullOrEmpty(behaviorGroupSummary))
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[] responsibilityReports =
                        CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.BuildReports(
                            draft.Profile.Entries,
                            definitionCatalog);
                    for (int reportIndex = 0; reportIndex < responsibilityReports.Length; reportIndex++)
                    {
                        CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport responsibilityReport =
                            responsibilityReports[reportIndex];
                        if (responsibilityReport.ResponsibilityKind
                                == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                            || responsibilityReport.State
                                == CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.Ready
                            || responsibilityReport.State
                                == CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.InvalidAssignment)
                        {
                            continue;
                        }

                        behaviorGroupSummary = "职责提醒：" + responsibilityReport.Message;
                        break;
                    }
                }
                if (contract.Success && compile?.Success == true && invalidCardCount == 0)
                    behaviorGroupSummary = string.Empty;
                groupHasValidationProblems = !contract.Success
                    || compile?.Success != true
                    || invalidCardCount > 0;
                if (executingEntryIndex >= 0)
                {
                    viewModel.WorkbenchSummaryText = behaviorGroupSummary;
                }
                else
                {
                    viewModel.WorkbenchSummaryText = behaviorGroupSummary;
                }
            }
            else
            {
                for (int i = 0; i < rows.Length; i++)
                    rows[i].Visible = false;
                viewModel.WorkbenchResponsibilityCounts = new int[5];
                viewModel.WorkbenchSummaryText = string.Empty;
            }

            if (entries.Count <= 0)
            {
                FillAuthoringUxStatus(
                    viewModel,
                    controller,
                    workspace,
                    draft,
                    null,
                    -1,
                    groupHasValidationProblems,
                    false,
                    presentationContext,
                    isSelectedCardExpanded);
                viewModel.WorkbenchDetailText = "此行为组中没有行为卡。";
                viewModel.WorkbenchDiagnosticText = string.Empty;
                return;
            }

            int clamped = Math.Max(0, Math.Min(selectedEntryIndex, entries.Count - 1));
            if (hideRuntimeSupportEntries
                && IsBehaviorTestRuntimeSupportEntry(entries[clamped], primaryBehaviorCardDraftId))
            {
                clamped = FirstBehaviorEntryIndex(rows, clamped);
            }
            if (entries[clamped]?.RuntimeEntry == null
                || string.IsNullOrWhiteSpace(entries[clamped].CardDraftId))
            {
                int replacement = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft candidate = entries[i];
                    if (candidate?.RuntimeEntry != null
                        && !string.IsNullOrWhiteSpace(candidate.CardDraftId)
                        && (!hideRuntimeSupportEntries
                            || !IsBehaviorTestRuntimeSupportEntry(
                                candidate,
                                primaryBehaviorCardDraftId)))
                    {
                        replacement = i;
                        break;
                    }
                }
                if (replacement < 0)
                {
                    FillAuthoringUxStatus(
                        viewModel,
                        controller,
                        workspace,
                        draft,
                        null,
                        -1,
                        true,
                        true,
                        presentationContext,
                        isSelectedCardExpanded);
                    viewModel.WorkbenchDetailText = "行为组中存在损坏的行为卡条目。";
                    viewModel.WorkbenchDiagnosticText = "请删除损坏条目或从有效来源重新创建。";
                    return;
                }
                clamped = replacement;
            }
            for (int row = 0; row < visibleRowCapacity; row++)
                rows[row].Selected = rows[row].EntryIndex == clamped;
            CharacterTestRuntimeAiLabBehaviorEntryDraft selectedDraft = entries[clamped];
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry selected = selectedDraft.RuntimeEntry;
            workspace.Cards.TryGetValue(
                selectedDraft.CardDraftId,
                out CharacterTestRuntimeAiLabBehaviorCardDraft selectedCard);
            definitionCatalog.TryGetDefinition(
                selected.BehaviorId,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition selectedDefinition);

            string name = selectedCard != null && !string.IsNullOrWhiteSpace(selectedCard.DisplayName)
                ? selectedCard.DisplayName
                : "未命名行为卡";
            string trigger = selectedDefinition != null
                ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatConditions(
                    selectedDefinition,
                    selected)
                : "行为定义不可用";
            string action = selectedDefinition != null
                ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatActionPrimitive(
                    selectedDefinition.ActionPrimitive)
                : "行为定义不可用";
            bool selectedEntryExecuting = clamped == executingEntryIndex;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot logicAgentState = selectedLogicAgentState;
            bool selectedLogicEntryActive = selectedEntryExecuting
                && runtimeIdentity.Status == CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved;
            string executingStepId = selectedLogicEntryActive
                ? runtimeIdentity.CurrentStageId
                : string.Empty;
            var frozenTactical = selectedFrameIsHistorical && runtimeIdentity.CommittedProgram?.TacticalSource?.CardId == selectedCard?.CardDraftId
                ? runtimeIdentity.CommittedProgram.TacticalSource : null;
            CharacterTestTacticalAuthoringPreview tacticalPreview = groupManagementOnly ? null
                : controller.BuildTacticalPreview(selectedCard,
                    frozenTactical?.IsLegacy == false ? frozenTactical : null, runtimeIdentity);
            viewModel.BehaviorCardWorkspace.Timeline.TacticalPreview = tacticalPreview;
            viewModel.BehaviorCardWorkspace.Timeline.CompositionEditor = !groupManagementOnly && selectedCard?.HasCompositionSource == true
                ? controller.CompositionEditingSession : null;
            viewModel.BehaviorCardWorkspace.Timeline.NativeExecution = viewModel.BehaviorCardWorkspace.Timeline.InspectCompiledExecution
                ? CharacterTestNativeExecutionView.Get(tacticalPreview?.WholeCard?.Program) : null;
            viewModel.BehaviorCardWorkspace.Timeline.CompositionEditor?.ApplyPendingLocation(viewModel.BehaviorCardWorkspace.Timeline);
            viewModel.PrepareTacticalSummary = tacticalPreview?.SummaryRequest == null ? null
                : () => controller.RequestTacticalSummary(tacticalPreview);
            if (!groupManagementOnly && selectedCard?.HasCompositionSource == true
                && controller.CompositionEditingSession?.SummaryRequest != null && !selectedFrameIsHistorical)
                viewModel.PrepareTacticalSummary = controller.RequestCompositionSummary;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult logicCompile =
                selectedCard?.HasCompositionSource == true ? controller.CompileBehaviorCardLogic(selectedCard)
                    : tacticalPreview?.AsCompileResult() ?? controller.CompileBehaviorCardLogic(selectedCard);
            viewModel.BehaviorCardIdentity =
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.BuildProjectionInto(
                    identityProjection,
                    controller,
                    draft,
                    clamped,
                    logicCompile?.Program?.SemanticHash ?? 0L,
                    runtimeIdentity,
                    selectedAgent,
                    hasSelectedAgent);
            FillAuthoringUxStatus(
                viewModel,
                controller,
                workspace,
                draft,
                selectedCard,
                clamped,
                groupHasValidationProblems,
                logicCompile?.Success != true,
                presentationContext,
                isSelectedCardExpanded);
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection historicalProjection =
                selectedFrameIsHistorical
                    ? controller.ResolveHistoricalBehaviorProjection(
                        selectedFrame.Frame,
                        latestFrame,
                        runtimeIdentity)
                    : null;
            bool historicalOverlayAvailable = historicalProjection?.HasSnapshotState == true
                && (viewModel.BehaviorCardIdentity.Relationship
                        == CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardSameSemantic
                    || viewModel.BehaviorCardIdentity.Relationship
                        == CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardDifferentSemantic);
            bool committedProgramMatches = selectedEntryExecuting
                && runtimeIdentity.Status == CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved
                && logicCompile?.Program != null
                && runtimeIdentity.CommittedProgram != null
                && logicCompile.Program.SemanticHash == runtimeIdentity.CommittedProgram.SemanticHash;
            selectedLogicEntryActive = ((selectedFrameIsLatest && committedProgramMatches)
                    || historicalOverlayAvailable)
                && controller.BehaviorRecipeSyncState
                    != CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RuntimeLoadFailed;
            executingStepId = selectedLogicEntryActive
                ? runtimeIdentity.CurrentStageId
                : string.Empty;
            if (isSelectedCardExpanded)
            {
                bool useFrozenHistoricalProgram = historicalOverlayAvailable
                    && viewModel.BehaviorCardIdentity.Relationship
                        == CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardDifferentSemantic;
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult displayedLogicCompile =
                    selectedCard?.HasCompositionSource == true && tacticalPreview != null ? tacticalPreview.AsCompileResult() : useFrozenHistoricalProgram
                        ? new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                            historicalProjection.FrozenProgram,
                            Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>())
                        : logicCompile;
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload displayedPayload =
                    viewModel.BehaviorCardWorkspace.Timeline.NativeExecution?.Payload ?? tacticalPreview?.WholeCard?.DisplayPayload ?? tacticalPreview?.DisplayPayload ?? (useFrozenHistoricalProgram ? null : selectedCard?.LogicPayload);
                string displayedName = useFrozenHistoricalProgram
                    ? runtimeIdentity.CommittedCard?.DisplayName ?? name
                    : name;
                string displayedRuntimeEntryId = selectedLogicEntryActive
                    && runtimeIdentity.CommittedCard != null
                        ? runtimeIdentity.CommittedCard.RuntimeEntryId
                        : selected.EntryId;
                CharacterTestRuntimeAiLabActiveActionProgress activeActionProgress = default;
                if (selectedLogicEntryActive && selectedFrameIsLatest && historicalProjection == null)
                {
                    controller.TryCaptureActiveActionProgress(
                        selectedAgent.EntityId,
                        out activeActionProgress);
                }
                viewModel.BehaviorCardWorkspace.Timeline.CanEdit = editable && !viewModel.BehaviorCardWorkspace.Timeline.InspectCompiledExecution;
                viewModel.BehaviorCardWorkspace.Timeline.CanEditCardDisplayName =
                    viewModel.CanEditProjectCardMetadata;
                viewModel.BehaviorCardWorkspace.Timeline.CanEditResponsibilities = editable;
                viewModel.BehaviorCardWorkspace.Timeline.EntryIndex = clamped;
                viewModel.BehaviorCardWorkspace.Timeline.ResponsibilityKinds =
                    CopyResponsibilities(
                        selectedCard?.HasPublishedSource == true
                            ? selectedCard.ResponsibilityKinds
                            : selectedDraft.ResponsibilityKinds);
                viewModel.BehaviorCardWorkspace.Timeline.ResponsibilityCompatibilityMask =
                    BuildResponsibilityCompatibilityMask(selectedDraft, selectedCard, selectedDefinition);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                    viewModel,
                    displayedName,
                    displayedLogicCompile,
                    displayedPayload,
                    selectedLogicElementIndex >= 0 ? selectedLogicElementIndex : selectedCompiledStepIndex,
                    selectedLogicElementKind,
                    executingStepId,
                    selectedLogicEntryActive && (selectedCard?.HasCompositionSource != true || viewModel.BehaviorCardWorkspace.Timeline.InspectCompiledExecution)
                        ? historicalProjection?.RuntimeState ?? logicAgentState
                        : null,
                    historicalOverlayAvailable
                        ? historicalProjection.ExactFrameEvents
                        : selectedLogicEntryActive && selectedFrame != null
                            ? controller.GetBehaviorLogicEventsForFrame(selectedFrame.Frame)
                            : null,
                    selectedFrame?.Frame ?? -1,
                    displayedRuntimeEntryId,
                    historicalProjection,
                    activeActionProgress,
                    controller,
                    hasSelectedAgent ? selectedAgent.TargetDistance : -1f,
                    ResolveMovementCompletionDistance(
                        displayedLogicCompile?.Program,
                        selected.BehaviorId),
                    hasSelectedAgent
                        ? selectedAgent
                        : (CharacterTestRuntimeAiPlannerAgentSnapshot?)null);
                if (tacticalPreview != null && selectedCard?.HasCompositionSource != true)
                {
                    var tacticalTimeline = viewModel.BehaviorCardWorkspace.Timeline;
                    tacticalTimeline.Available = true;
                    tacticalTimeline.CanEdit = tacticalPreview.Editor?.CanEdit == true;
                    tacticalTimeline.CanEditCardDisplayName = false;
                    tacticalTimeline.CanEditResponsibilities = false;
                    tacticalTimeline.ValidationBadgeText = tacticalPreview.IsFrozen ? "历史冻结源" : tacticalPreview.Ready ? "当前源已编译" : "源保留 · 未求值";
                    tacticalTimeline.DiagnosticsText = tacticalPreview.Diagnostic;
                    tacticalTimeline.HasBlockingDiagnostics = !tacticalPreview.Ready;
                    tacticalTimeline.SummaryText = "战术作者源 v2 · 当前角色编译预览；当前测试仍使用冻结版本。";
                    viewModel.ShowProjectCardMetadata = true;
                    viewModel.CanEditProjectCardMetadata = false;
                    viewModel.ProjectCardMetadataTitle = selectedCard.DisplayName + " · 战术源";
                    viewModel.ProjectCardDisplayName = selectedCard.DisplayName;
                    viewModel.ProjectCardDescription = tacticalPreview.IsConditionProjection ? tacticalPreview.StatusText
                        : tacticalPreview.Ready && tacticalPreview.Compilation != null ? "当前装配动作 " + tacticalPreview.Compilation.ActionId
                        : "存在编译问题，源条目仍可选择。";
                    viewModel.ProjectCardUseSummary = "标准靶 v1 · 50mm · 0~5m";
                    viewModel.ProjectCardRiskSummary = tacticalPreview.Ready ? "当前源预览，不代表已测试或发布。" : tacticalPreview.StatusText;
                    viewModel.CanPublishCurrentChanges = false;
                    viewModel.CanPublishDraft = false;
                    viewModel.CanApplyDraft = false;
                }
                if (selectedCard?.HasCompositionSource == true)
                {
                    var composition = controller.CompositionEditingSession;
                    viewModel.ShowProjectCardMetadata = true; viewModel.CanEditProjectCardMetadata = false;
                    viewModel.ProjectCardMetadataTitle = composition.Frozen != null ? "历史战术卡 · 只读" : "战术卡 · 共享编排";
                    viewModel.ProjectCardDisplayName = composition.Document.Name;
                    viewModel.ProjectCardDescription = composition.DescriptionText;
                    viewModel.ProjectCardUseSummary = composition.DetailsOpen ? composition.DescriptionDetails : "说明由编排、共享封装及装配自动生成。";
                    viewModel.ProjectCardRiskSummary = composition.DetailsOpen ? composition.DescriptionWarnings : "展开共享内部前请核对引用影响。";
                    var timeline = viewModel.BehaviorCardWorkspace.Timeline;
                    timeline.CanEditCardDisplayName = false; timeline.CanEditResponsibilities = false;
                    timeline.CanEdit = tacticalPreview?.Editor?.CanEdit == true;
                    timeline.SummaryText = composition.Frozen != null ? "历史冻结共享源 · 只读" : "共享封装内部 · 卡层编排在上方";
                    if (tacticalPreview == null) timeline.Available = false;
                    else if (composition.EditingSourceTimeline) timeline.Available = tacticalPreview.DisplayPayload != null;
                }
                if (viewModel.BehaviorCardWorkspace?.Timeline != null && !selectedFrameIsHistorical)
                {
                    viewModel.BehaviorCardWorkspace.Timeline.RuntimeStatusText =
                        controller.BehaviorRecipeSyncStatusText;
                }
                if (useFrozenHistoricalProgram
                    && viewModel.BehaviorCardWorkspace?.Timeline != null)
                {
                    viewModel.BehaviorCardWorkspace.Timeline.RuntimeStatusText +=
                        " · 冻结旧 revision";
                }
                viewModel.BehaviorCardWorkspace.Timeline.CanEdit = editable && !viewModel.BehaviorCardWorkspace.Timeline.InspectCompiledExecution;
                FillBehaviorCardRuntimeHeader(
                    viewModel,
                    viewModel.BehaviorCardIdentity,
                    hasSelectedAgent);
                if (!selectedFrameIsHistorical)
                    viewModel.BehaviorCardRuntimeStateText = viewModel.CurrentTestSourceStatusText;
                viewModel.WorkbenchDetailText = string.Empty;
                viewModel.WorkbenchDiagnosticText = string.Empty;
            }
            else
            {
                float selectedScore = selectedDefinition != null
                    ? selected.GetFloat(
                        selectedDefinition,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey)
                    : 0f;
                CharacterTestRuntimeAiLabCardDiagnostic? primaryDiagnostic =
                    logicCompile != null && logicCompile.Diagnostics.Count > 0
                        ? logicCompile.Diagnostics[0]
                        : (CharacterTestRuntimeAiLabCardDiagnostic?)null;
                string selectedCategory = selectedDraft.Disabled
                    ? "已禁用"
                    : logicCompile?.Success == true
                        ? "已启用"
                        : primaryDiagnostic.HasValue
                            ? FormatCardDiagnosticStatus(primaryDiagnostic.Value)
                            : "配置错误";
                string diagnostic = primaryDiagnostic.HasValue
                    ? FormatCardDiagnostic(primaryDiagnostic.Value)
                    : string.Empty;
                var detailBuilder = new StringBuilder(256);
                detailBuilder.Append("状态  ")
                    .Append(selectedCategory)
                    .Append("    优先级  ")
                    .Append(selectedScore.ToString("0.##", CultureInfo.InvariantCulture))
                    .Append("\n")
                    .Append("触发  ")
                    .Append(trigger)
                    .Append("        执行  ")
                    .Append(action);
                viewModel.WorkbenchDetailText = detailBuilder.ToString();
                viewModel.WorkbenchDiagnosticText = diagnostic;
            }
            if (isSelectedCardExpanded)
            {
                viewModel.DuplicateEntryLabel = "添加陪测";
                viewModel.MoveEntryUpLabel = "上移";
                viewModel.MoveEntryDownLabel = "下移";
                viewModel.ToggleEntryLabel = selectedDraft.Disabled ? "启用" : "禁用";
                viewModel.DeleteEntryLabel = "删除";
                viewModel.ApplyDraftLabel = "保存";
            }
            else
            {
                viewModel.DuplicateEntryLabel = "复制行为卡";
                viewModel.MoveEntryUpLabel = "上移";
                viewModel.MoveEntryDownLabel = "下移";
                viewModel.ToggleEntryLabel = selected.Disabled ? "启用" : "禁用";
                viewModel.DeleteEntryLabel = "删除";
                viewModel.ApplyDraftLabel = "应用更改";
            }
        }

        private static void FillUnavailableAuthoringWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            string diagnostic)
        {
            diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "创作工作区不可用。"
                : diagnostic;
            viewModel.WorkbenchGroupIdentity = string.Empty;
            viewModel.WorkbenchRows = Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
            viewModel.WorkbenchRowCount = 0;
            viewModel.WorkbenchLibraryRows =
                Array.Empty<CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel>();
            viewModel.WorkbenchSummaryText = "创作工作区不可用";
            viewModel.WorkbenchDetailText = diagnostic;
            viewModel.WorkbenchDiagnosticText = diagnostic;
            viewModel.DraftStatusText = "创作工作区不可用";
            viewModel.PublishDraftStatusText = string.Empty;
            viewModel.ApplyDraftDisabledReasonText = diagnostic;
            viewModel.PublishDraftDisabledReasonText = diagnostic;
            viewModel.SaveBehaviorGroupDisabledReasonText = diagnostic;
            viewModel.CanEditWorkbench = false;
            viewModel.CanUndoDraft = false;
            viewModel.CanSaveDraft = false;
            viewModel.CanApplyDraft = false;
            viewModel.CanPublishDraft = false;
            viewModel.CanSaveBehaviorGroup = false;
            viewModel.CanReloadProjectDraft = false;
            viewModel.CanReapplyProjectDraft = false;
            viewModel.ShowProjectCardMetadata = false;
            viewModel.CanEditProjectCardMetadata = false;
            viewModel.AuthoringStatus.AdvancedDiagnosticText =
                viewModel.ShowAdvancedDiagnostics || viewModel.DetailsExpanded
                    ? diagnostic
                    : string.Empty;
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library =
                viewModel.BehaviorCardWorkspace?.Library;
            if (library != null)
            {
                library.Available = false;
                library.ActiveCount = 0;
                library.SummaryText = "创作工作区不可用";
                library.EmptyText = diagnostic;
                library.HasCachedStructure = false;
            }
        }

        private static string ResolveAuthoringWorkspaceUnavailableDiagnostic(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
#if UNITY_EDITOR
            if (controller != null && controller.HasAuthoringWorkspaceRoleOpenFailure)
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    controller.LastAuthoringWorkspaceRoleResult;
                return "创作工作区不可用（" + result.Status + "）：" + result.Message;
            }
#endif
            return "创作工作区不可用。";
        }

        private static void FillInvalidWorkbenchRow(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            int entryIndex,
            bool selected)
        {
            row.RowKind = CharacterTestRuntimeAiLabWorkbenchRowKind.BehaviorCard;
            row.EntryIndex = entryIndex;
            row.CardDraftId = string.Empty;
            row.PublishedStableId = string.Empty;
            row.DraftRevision = 0;
            row.PublishState = CharacterTestRuntimeAiLabPublishUxState.Unavailable;
            row.PublishStatusText = string.Empty;
            row.HasValidationProblems = true;
            row.ValidationStatusText =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(true);
            row.LogicPayload = null;
            row.IconKey = CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.FallbackIconKey;
            row.Label = "损坏的行为卡条目";
            row.TitleText = "损坏的行为卡条目";
            row.BadgeText = "配置错误";
            row.TriggerText = string.Empty;
            row.ActionText = string.Empty;
            row.OutcomeText = string.Empty;
            row.PriorityText = string.Empty;
            row.ExecutionDetailText = string.Empty;
            row.ExecutionState = CharacterTestRuntimeAiLabExecutionDisplayState.None;
            row.ResponsibilityKinds =
                new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
            row.ResponsibilityKind = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            row.ResponsibilityCompatibilityMask = 0;
            row.ResponsibilityDisplayText = string.Empty;
            row.ResponsibilityExpanded = false;
            row.ResponsibilityTotalCount = 0;
            row.ResponsibilityEnabledCount = 0;
            row.ResponsibilitySummaryText = string.Empty;
            row.ResponsibilityColor = string.Empty;
            row.ResponsibilityWarning = false;
            row.IsCurrentBehavior = false;
            row.ExecutionStepId = string.Empty;
            row.Visible = true;
            row.Selected = selected;
            row.Disabled = true;
            row.Invalid = true;
        }

        private static void FillAuthoringUxStatus(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorCardDraft selectedCard,
            int selectedEntryIndex,
            bool groupHasValidationProblems,
            bool cardHasValidationProblems,
            CharacterTestRuntimeAiPlannerDevPresentationContext presentationContext,
            bool isSelectedCardExpanded)
        {
            CharacterTestRuntimeAiLabAuthoringUxStatusViewModel status = viewModel.AuthoringStatus;
            string groupCompareDiagnostic = string.Empty;
            bool groupHasUnpublishedContent = group?.HasProjectSource == true
                && controller.HasUnpublishedBehaviorGroupContentForDisplay(
                    group,
                    out groupCompareDiagnostic);
            string cardCompareDiagnostic = string.Empty;
            CharacterTestRuntimeAiLabWorkbenchRowViewModel selectedRow =
                FindWorkbenchRowByEntryIndex(viewModel.WorkbenchRows, selectedEntryIndex);
            bool cardHasUnpublishedContent = selectedCard?.HasPublishedSource == true
                && (selectedRow != null
                    ? selectedRow.PublishState
                        == CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges
                    : controller.HasUnpublishedBehaviorCardContentForDisplay(
                        group,
                        selectedEntryIndex,
                        out cardCompareDiagnostic));
            bool showAdvancedDiagnostic = viewModel.ShowAdvancedDiagnostics
                || viewModel.DetailsExpanded;
            if (showAdvancedDiagnostic
                && selectedCard?.HasPublishedSource == true
                && selectedRow != null)
            {
                controller.HasUnpublishedBehaviorCardContentForDisplay(
                    group,
                    selectedEntryIndex,
                    out cardCompareDiagnostic);
            }
            status.BehaviorGroupPublishState = group == null
                ? CharacterTestRuntimeAiLabPublishUxState.Unavailable
                : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                    group.HasProjectSource,
                    groupHasUnpublishedContent);
            status.BehaviorCardPublishState = selectedCard == null
                ? CharacterTestRuntimeAiLabPublishUxState.Unavailable
                : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                    selectedCard.HasPublishedSource,
                    cardHasUnpublishedContent);
            status.DraftSaveState =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolveDraftSaveState(
                    workspace != null,
                    workspace?.IsReadOnly == true,
                    workspace?.Dirty == true,
                    controller?.DraftPersistenceStatus);
            status.BehaviorGroupHasValidationProblems = groupHasValidationProblems;
            status.BehaviorCardHasValidationProblems = cardHasValidationProblems;
            status.BehaviorGroupPublishStatusText =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                    status.BehaviorGroupPublishState);
            status.BehaviorCardPublishStatusText =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                    status.BehaviorCardPublishState);
            status.DraftSaveStatusText =
                controller.IsConfiguredTestCardEditing ? controller.ConfiguredCardSaveStatus : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatDraftSaveState(
                    status.DraftSaveState, controller.DraftLastSaveWasAutomatic);
            status.BehaviorGroupValidationStatusText =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(
                    groupHasValidationProblems);
            status.BehaviorCardValidationStatusText = selectedCard == null
                ? string.Empty
                : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(
                    cardHasValidationProblems);

            CharacterTestRuntimeAiLabCandidateRevision candidate = controller?.CandidateRevision;
            CharacterTestRuntimeAiLabCandidateRevision runtime = controller?.ActiveRevision;
            bool candidateMatchesRuntime = CandidateIdentityMatches(candidate, runtime);
            bool currentMatchesCandidate = candidate != null
                && workspace != null
                && group != null
                && workspace.CurrentRevision == candidate.Revision
                && string.Equals(
                    group.GroupDraftId,
                    candidate.GroupDraftId,
                    StringComparison.Ordinal);
            bool runtimeIdentityAvailable = runtime != null
                && viewModel.BehaviorCardIdentity?.Runtime?.Status
                    == CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved;
            bool publishedContentPristine = status.BehaviorGroupPublishState
                    == CharacterTestRuntimeAiLabPublishUxState.GameAvailable
                && (selectedCard == null
                    || status.BehaviorCardPublishState
                        == CharacterTestRuntimeAiLabPublishUxState.GameAvailable);
            if (controller != null)
            {
                status.TestSourceState = (CharacterTestRuntimeAiLabTestSourceUxState)
                    controller.CurrentTestSource;
                status.TestSourceStatusText = controller.CurrentTestSourceStatusText;
            }
            else
            {
                status.TestSourceState =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolveTestSourceState(
                        presentationContext
                            != CharacterTestRuntimeAiPlannerDevPresentationContext.Setup
                            && presentationContext
                                != CharacterTestRuntimeAiPlannerDevPresentationContext.Unavailable,
                        runtimeIdentityAvailable,
                        candidateMatchesRuntime,
                        currentMatchesCandidate,
                        DoesCurrentCardMatchCandidate(viewModel.BehaviorCardIdentity),
                        publishedContentPristine);
                status.TestSourceStatusText =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatTestSourceState(
                        status.TestSourceState);
            }
            status.AdvancedDiagnosticText = showAdvancedDiagnostic
                ? BuildAuthoringUxAdvancedDiagnostic(
                    controller,
                    groupCompareDiagnostic,
                    cardCompareDiagnostic)
                : string.Empty;
            viewModel.DraftStatusText = status.DraftSaveStatusText;
            viewModel.PublishDraftStatusText = isSelectedCardExpanded
                && status.BehaviorCardPublishState
                    != CharacterTestRuntimeAiLabPublishUxState.Unavailable
                    ? status.BehaviorCardPublishStatusText
                    : status.BehaviorGroupPublishStatusText;
        }

        private static CharacterTestRuntimeAiLabWorkbenchRowViewModel FindWorkbenchRowByEntryIndex(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            int entryIndex)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i]?.Visible == true && rows[i].EntryIndex == entryIndex)
                    return rows[i];
            }
            return null;
        }

        private static bool CandidateIdentityMatches(
            CharacterTestRuntimeAiLabCandidateRevision candidate,
            CharacterTestRuntimeAiLabCandidateRevision runtime)
        {
            return candidate != null
                && runtime != null
                && candidate.Revision == runtime.Revision
                && candidate.AuthorityHash == runtime.AuthorityHash
                && string.Equals(
                    candidate.GroupDraftId,
                    runtime.GroupDraftId,
                    StringComparison.Ordinal);
        }

        private static bool DoesCurrentCardMatchCandidate(
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identity)
        {
            CharacterTestRuntimeAiLabEditCardIdentity edit = identity?.EditCard;
            if (edit == null || edit.CardDraftId.Length == 0)
                return true;
            CharacterTestRuntimeAiLabRevisionCardIdentity candidate = identity.CandidateCard;
            if (identity.EditResolutionStatus
                    != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved
                || candidate == null)
            {
                return false;
            }
            bool sameStableIdentity = string.Equals(
                    edit.CardDraftId,
                    candidate.CardDraftId,
                    StringComparison.Ordinal)
                || string.Equals(
                    edit.DraftEntryId,
                    candidate.DraftEntryId,
                    StringComparison.Ordinal);
            return sameStableIdentity
                && edit.CurrentSemanticHash != 0L
                && edit.CurrentSemanticHash == candidate.CandidateSemanticHash;
        }

        private static string BuildAuthoringUxAdvancedDiagnostic(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            string groupCompareDiagnostic,
            string cardCompareDiagnostic)
        {
            var builder = new StringBuilder(256);
            builder.Append(controller?.DraftPersistenceStatus ?? string.Empty)
                .Append('\n')
                .Append(controller?.CandidateDiagnostic ?? string.Empty)
                .Append('\n')
                .Append(controller?.LastPublishStatus ?? string.Empty)
                .Append('\n')
                .Append(groupCompareDiagnostic ?? string.Empty)
                .Append('\n')
                .Append(cardCompareDiagnostic ?? string.Empty);
            return builder.ToString();
        }

        private static void FillAuthoringContext(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabWorkspaceMode workspaceMode,
            int selectedEntryIndex,
            CharacterTestRuntimeAiPlannerDevPresentationContext presentationContext)
        {
            if (viewModel == null)
                return;

            bool authoring = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Setup
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                || workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                || viewModel.ShowTimeline
                || viewModel.ShowBehaviorPlan
                || viewModel.ShowBehaviorLogic;
            viewModel.ShowAuthoringContextBar = authoring;
            viewModel.ShowAuthoringWorkspaceRoleControl = authoring;
            viewModel.ShowAuthoringWriteActions = authoring && Application.isEditor;
            viewModel.CanTestCurrentChanges = false;
            viewModel.CanPublishCurrentChanges = false;
            viewModel.PublishCurrentChangesLabel = "发布本次修改";
            viewModel.TestCurrentChangesDisabledReasonText = "测试当前修改将在 UX-05 接入。";
            viewModel.PublishCurrentChangesDisabledReasonText = "发布本次修改将在 UX-06 接入。";
            viewModel.AuthoringWorkspaceRoleText = Application.isEditor ? "主工作区" : "只读";
            viewModel.AuthoringGroupContextText = "行为组：未选择";
            viewModel.AuthoringCardContextText = "行为卡：未选择";
            viewModel.AuthoringPublishedStateText = "正式版本：不可用";
            viewModel.AuthoringProblemText = "问题：0";
            if (!authoring || controller == null)
                return;

#if UNITY_EDITOR
            viewModel.AuthoringWorkspaceRoleText = controller.WorkspaceAvailable
                ? FormatAuthoringWorkspaceRole(controller.ActiveAuthoringWorkspaceRole)
                : "工作区不可用";
            viewModel.PublishCurrentChangesLabel = controller.HasActiveAuthoringWorkspaceRole
                && controller.ActiveAuthoringWorkspaceRole !=
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                ? "应用到主工作区"
                : "发布本次修改";
#endif
            CharacterTestRuntimeAiLabDraftWorkspace workspace = controller.DraftWorkspace;
            if (workspace == null)
            {
#if UNITY_EDITOR
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                    controller.LastAuthoringWorkspaceRoleResult;
                viewModel.AuthoringProblemText = result.Status ==
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted
                    ? "问题：工作区不可用"
                    : "问题：" + result.Status;
#else
                viewModel.AuthoringProblemText = "问题：工作区不可用";
#endif
                return;
            }

            if (!controller.TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                viewModel.AuthoringProblemText = "问题：未选择行为组";
                return;
            }

            viewModel.AuthoringGroupContextText = "行为组：" + FormatAuthoringContextName(
                group.DisplayName,
                group.GroupDraftId,
                HasDuplicateGroupName(workspace, group));

            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            if (selectedEntryIndex >= 0 && selectedEntryIndex < group.Entries.Count)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[selectedEntryIndex];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.CardDraftId))
                    workspace.Cards.TryGetValue(entry.CardDraftId, out card);
            }
            if (card != null)
            {
                viewModel.AuthoringCardContextText = "行为卡：" + FormatAuthoringContextName(
                    card.DisplayName,
                    card.CardDraftId,
                    HasDuplicateCardName(workspace, card));
            }

            viewModel.CanTestCurrentChanges = controller.CanTestCurrentChanges(
                selectedEntryIndex,
                out string testDisabledReason);
            viewModel.TestCurrentChangesDisabledReasonText = viewModel.CanTestCurrentChanges
                ? string.Empty
                : testDisabledReason;

            bool mainWorkspace = true;
#if UNITY_EDITOR
            mainWorkspace = controller.HasActiveAuthoringWorkspaceRole
                && controller.ActiveAuthoringWorkspaceRole ==
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main;
#endif
            bool hasPublishableChanges = HasPublishableAuthoringChanges(viewModel);
            bool publishHasProblems = viewModel.BehaviorGroupHasValidationProblems
                || viewModel.BehaviorCardHasValidationProblems;
            viewModel.CanPublishCurrentChanges = Application.isEditor
                && mainWorkspace
                && !workspace.IsReadOnly
                && presentationContext != CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                && presentationContext
                    != CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview
                && (controller.PublishCurrentChangesRecoveryRequired
                    || (!controller.TestCurrentChangesQueued && !controller.TestCurrentChangesInProgress
                        && !publishHasProblems && hasPublishableChanges));
            viewModel.PublishCurrentChangesLabel = controller.PublishCurrentChangesRecoveryRequired ? "恢复发布" : "发布本次修改";
            if (viewModel.CanPublishCurrentChanges)
            {
                viewModel.PublishCurrentChangesDisabledReasonText = string.Empty;
            }
            else if (!Application.isEditor)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "Player 构建只读；只能在 Unity Editor 中发布。";
            }
            else if (!mainWorkspace)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "实验工作区不能直接发布；请先应用到主工作区。";
            }
            else if (controller.PublishCurrentChangesRecoveryRequired)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "项目目录需要恢复，暂时不能继续发布。";
            }
            else if (workspace.IsReadOnly)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "当前工作区为只读状态。";
            }
            else if (presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                || presentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "历史帧只读；请先回到当前帧。";
            }
            else if (controller.TestCurrentChangesQueued
                || controller.TestCurrentChangesInProgress)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "测试当前修改正在执行，请等待完成。";
            }
            else if (publishHasProblems)
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "当前修改存在需要处理的问题。";
            }
            else
            {
                viewModel.PublishCurrentChangesDisabledReasonText =
                    "当前行为组没有需要发布的修改。";
            }

            string publishedState = workspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                && !string.IsNullOrWhiteSpace(viewModel.BehaviorCardPublishStatusText)
                    ? viewModel.BehaviorCardPublishStatusText
                    : viewModel.BehaviorGroupPublishStatusText;
            viewModel.AuthoringPublishedStateText = "正式版本："
                + (string.IsNullOrWhiteSpace(publishedState) ? "不可用" : publishedState);
            int problemCount = (viewModel.BehaviorGroupHasValidationProblems ? 1 : 0)
                + (viewModel.BehaviorCardHasValidationProblems ? 1 : 0);
            viewModel.AuthoringProblemText = presentationContext ==
                    CharacterTestRuntimeAiPlannerDevPresentationContext.Review
                ? "历史帧只读"
                : "问题：" + problemCount.ToString(CultureInfo.InvariantCulture);
        }

#if UNITY_EDITOR
        private static string FormatAuthoringWorkspaceRole(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            switch (role)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA:
                    return "实验 A";
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB:
                    return "实验 B";
                default:
                    return "主工作区";
            }
        }
#endif

        private static bool HasDuplicateGroupName(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorGroupDraft selected)
        {
            int count = 0;
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
            {
                if (group != null && string.Equals(
                        group.DisplayName,
                        selected.DisplayName,
                        StringComparison.Ordinal)
                    && ++count > 1)
                    return true;
            }
            return false;
        }

        private static bool HasPublishableAuthoringChanges(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabPublishUxState groupState =
                viewModel?.AuthoringStatus?.BehaviorGroupPublishState
                ?? CharacterTestRuntimeAiLabPublishUxState.Unavailable;
            if (groupState == CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges
                || groupState == CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly)
            {
                return true;
            }

            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows = viewModel?.WorkbenchRows;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                if (row == null || row.Invalid)
                    continue;
                if (row.PublishState
                        == CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges
                    || row.PublishState == CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasDuplicateCardName(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft selected)
        {
            int count = 0;
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in workspace.Cards.Values)
            {
                if (card != null && string.Equals(
                        card.DisplayName,
                        selected.DisplayName,
                        StringComparison.Ordinal)
                    && ++count > 1)
                    return true;
            }
            return false;
        }

        private static string FormatAuthoringContextName(
            string displayName,
            string stableId,
            bool duplicate)
        {
            string name = string.IsNullOrWhiteSpace(displayName) ? "未命名" : displayName.Trim();
            if (!duplicate || string.IsNullOrWhiteSpace(stableId))
                return name;
            string compact = stableId.Replace("-", string.Empty);
            string suffix = compact.Length <= 4 ? compact : compact.Substring(compact.Length - 4);
            return name + " · " + suffix.ToUpperInvariant();
        }

    }
}
