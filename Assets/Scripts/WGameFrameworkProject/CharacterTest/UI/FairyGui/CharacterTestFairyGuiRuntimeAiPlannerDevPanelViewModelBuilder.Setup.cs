using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        /// <remarks>NoAlloc after catalog initialization. Includes direct slot/position edits as well as command revisions.</remarks>
        internal static long AppendSetupBuildSourceRevision(
            long revision,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int pendingDeleteDraftId)
        {
            unchecked
            {
                CharacterTestArenaMatchConfig config = controller.MatchConfig;
                revision = revision * 397L ^ pendingDeleteDraftId;
                revision = revision * 397L ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(config);
                revision = revision * 397L ^ (config.BehaviorGroups?.Revision ?? 0);
                revision = revision * 397L ^ (config.BehaviorGroups?.Drafts.Count ?? 0);
                revision = revision * 397L ^ (config.ProjectCatalogState == null ? 0
                    : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(config.ProjectCatalogState));
                revision = revision * 397L ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(
                    CharacterTestPlayableCharacterCatalog.Discover());
                revision = revision * 397L ^ (controller.TestCurrentChangesQueued ? 1 : 0);
                revision = revision * 397L ^ (controller.TestCurrentChangesInProgress ? 1 : 0);
                revision = revision * 397L ^ (controller.PublishCurrentChangesRecoveryRequired ? 1 : 0);
                revision = revision * 397L ^ (int)config.PreferredCameraMode;
                revision = revision * 397L ^ config.PresentationTeamId;
                revision = AppendSetupSlotsRevision(revision, config, config.Team1Slots);
                return AppendSetupSlotsRevision(revision, config, config.Team2Slots);
            }
        }

        private static long AppendSetupSlotsRevision(long revision,
            CharacterTestArenaMatchConfig config, IReadOnlyList<CharacterTestArenaSlotConfig> slots)
        {
            unchecked
            {
                revision = revision * 397L ^ slots.Count;
                for (int i = 0; i < slots.Count; i++)
                {
                    CharacterTestArenaSlotConfig slot = slots[i];
                    revision = revision * 397L ^ (slot.Enabled ? 1 : 0);
                    revision = revision * 397L ^ slot.CharacterId;
                    revision = revision * 397L ^ slot.LoadoutId;
                    revision = revision * 397L ^ slot.VariantId;
                    revision = revision * 397L ^ slot.BehaviorGroupDraftId;
                    config.TryGetSpawnPosition(slot.TeamId, slot.SlotIndex, out Vector2 position);
                    revision = revision * 397L ^ BitConverter.SingleToInt32Bits(position.x);
                    revision = revision * 397L ^ BitConverter.SingleToInt32Bits(position.y);
                }
                return revision;
            }
        }

        private static void FillSetupPlacement(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabSpawnValidationResult validation,
            int pendingSetupBehaviorGroupDeleteDraftId)
        {
            if (viewModel == null || controller == null)
                return;

            validation ??= controller.ValidateSetupSpawns();
            if (string.IsNullOrEmpty(viewModel.SetupValidationText))
            {
                viewModel.SetupValidationText = validation != null
                    ? validation.Summary
                    : "出生点校验不可用。";
            }
            viewModel.SetupSlotRows = BuildSetupSlotRows(controller, validation);
            viewModel.SetupCharacterOptions = BuildSetupCharacterOptions(controller);
            viewModel.SetupBehaviorGroupOptions = BuildSetupBehaviorGroupOptions(controller);
            FillSetupSummary(viewModel, controller, validation);
            if (!controller.TryGetSelectedSetupSpawn(out CharacterTestRuntimeAiLabSpawnSlotValidation slot))
            {
                ResetSetupBehaviorGroupActions(viewModel);
                viewModel.SetupSelectedSlotText = string.Empty;
                viewModel.SetupCharacterText = "-";
                viewModel.SetupBehaviorGroupText = "-";
                viewModel.SetupPositionText = "位置  -";
                return;
            }

            CharacterTestArenaSlotConfig configSlot =
                controller.MatchConfig.TryGetSlot(slot.TeamId, slot.SlotIndex);
            viewModel.ToggleSetupSlotLabel = configSlot != null && configSlot.Enabled ? "禁用" : "启用";
            viewModel.SetupSelectedSlotText = string.Empty;
            viewModel.SetupPositionText =
                "位置  "
                + slot.Position.x.ToString("0.##", CultureInfo.InvariantCulture)
                + ", "
                + slot.Position.y.ToString("0.##", CultureInfo.InvariantCulture);
            viewModel.SetupCharacterText = ResolveCharacterDisplay(configSlot);
            if (controller.DraftWorkspace != null
                && controller.DraftWorkspace.TryGetGroupForSlot(
                    slot.TeamId,
                    slot.SlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup))
            {
                viewModel.IsBehaviorTestSetup = false;
                // Opening the editor is the recovery path for an invalid behavior card.
                // Do not couple that navigation permission to start validation or project-catalog
                // writability; the editor can still explain a read-only/save failure in context.
                viewModel.CanOpenBehaviorGroup = controller.CanEditBehaviorCardDraft;
                ResolveProjectCatalogActionState(
                    viewModel,
                    controller,
                    selectedGroup,
                    configSlot?.BehaviorGroupDraftId ?? -1,
                    pendingSetupBehaviorGroupDeleteDraftId);
                bool exactProjectSelection = controller.MatchConfig?.BehaviorGroups != null
                    && controller.MatchConfig.BehaviorGroups.TryGetDraft(
                        configSlot?.BehaviorGroupDraftId ?? -1,
                        out CharacterTestArenaBehaviorGroupDraft arenaGroup)
                    && CharacterTestRuntimeAiLabDraftWorkspace.MatchesUnmodifiedArenaGroup(
                        selectedGroup,
                        arenaGroup);
                viewModel.SetupBehaviorGroupText = exactProjectSelection
                    ? FormatBehaviorGroupDisplayName(selectedGroup.DisplayName)
                        + "  ·  r" + selectedGroup.SourceProjectRevision.ToString(CultureInfo.InvariantCulture)
                        + "  ·  项目 JSON"
                    : FormatBehaviorGroupDisplayName(selectedGroup.DisplayName)
                        + "  ·  本地草稿";
            }
            else
            {
                viewModel.IsBehaviorTestSetup = false;
                viewModel.CanOpenBehaviorGroup = false;
                ResetSetupBehaviorGroupActions(viewModel);
                viewModel.SetupBehaviorGroupText =
                    ResolveBehaviorGroupDisplay(controller.MatchConfig.BehaviorGroups, configSlot);
            }
            viewModel.SetupValidationText = slot.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error
                ? "● " + TranslateSetupValidationMessage(slot.Message)
                : string.Empty;
            viewModel.SetupAgentBadgeText = (slot.TeamId == CharacterTestArenaMatchConfig.Team2Id ? "T2·" : "T1·")
                + (slot.SlotIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static void ResetSetupBehaviorGroupActions(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            viewModel.CanCreateSetupBehaviorGroup = false;
            viewModel.CanDeleteSetupBehaviorGroup = false;
            viewModel.SetupBehaviorGroupDeleteConfirming = false;
            viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
            viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText = "尚未选择可复制的行为组。";
            viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText = "尚未选择可删除的行为组。";
            viewModel.SetupBehaviorGroupEditorGroupDraftId = string.Empty;
            viewModel.SetupBehaviorGroupEditorStableId = string.Empty;
            viewModel.SetupBehaviorGroupEditorSourceRevision = 0;
            viewModel.SetupBehaviorGroupEditorSourceContentHash = 0;
            viewModel.SetupBehaviorGroupEditorIdentityText = "尚未选择行为组";
            viewModel.SetupBehaviorGroupEditorDisplayName = string.Empty;
            viewModel.SetupBehaviorGroupEditorDescription = string.Empty;
            viewModel.SetupBehaviorGroupEditorTagsText = string.Empty;
            viewModel.SetupBehaviorGroupEditorIsLocalDraft = false;
            viewModel.SetupBehaviorGroupEditorSaveLabel = "保存修改";
            viewModel.CanEditSetupBehaviorGroupBasicInfo = false;
            viewModel.SetupBehaviorGroupEditorDisabledReasonText = "尚未选择可编辑的项目行为组。";
        }

        private static void ResolveProjectCatalogActionState(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup,
            int selectedArenaDraftId,
            int pendingDraftId)
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state = controller.ProjectCatalogState;
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision exactGroup = null;
            bool exactArenaProjection = controller.MatchConfig?.BehaviorGroups != null
                && controller.MatchConfig.BehaviorGroups.TryGetDraft(
                    selectedArenaDraftId,
                    out CharacterTestArenaBehaviorGroupDraft selectedArenaGroup)
                && CharacterTestRuntimeAiLabDraftWorkspace.MatchesUnmodifiedArenaGroup(
                    selectedGroup,
                    selectedArenaGroup);
            bool isLocalDraft = selectedGroup != null && !exactArenaProjection;
            bool exactRevision = selectedGroup != null
                && selectedGroup.HasProjectSource
                && state?.GroupCatalog != null
                && state.GroupCatalog.TryGetExact(
                    selectedGroup.PublishedStableId,
                    selectedGroup.SourceProjectRevision,
                    out exactGroup)
                && exactGroup.ContentHash == selectedGroup.SourceProjectContentHash;
            FillSetupBehaviorGroupBasicInfo(
                viewModel,
                selectedGroup,
                exactGroup,
                isLocalDraft,
                state?.Success == true
                    && state.CanWriteProjectCatalogs
                    && (isLocalDraft || exactRevision));
            if (state?.Success != true || !state.CanWriteProjectCatalogs)
            {
                viewModel.CanCreateSetupBehaviorGroup = selectedGroup != null
                    && controller.DraftWorkspace?.IsReadOnly != true;
                viewModel.CanDeleteSetupBehaviorGroup = false;
                viewModel.SetupBehaviorGroupPickerCreateLabel = "新建副本";
                viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText =
                    viewModel.CanCreateSetupBehaviorGroup
                        ? string.Empty
                        : "当前本地工作区不可复制行为组。";
                viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText = "项目行为组目录不可写。";
                viewModel.SetupBehaviorGroupDeleteConfirming = false;
                viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
                return;
            }

            if (isLocalDraft)
            {
                viewModel.CanCreateSetupBehaviorGroup = true;
                viewModel.SetupBehaviorGroupPickerCreateLabel = "新建副本";
                viewModel.CanDeleteSetupBehaviorGroup = false;
                viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText = string.Empty;
                viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText =
                    "本地草稿只能在确认或放弃草稿流程中移除。";
                viewModel.SetupBehaviorGroupDeleteConfirming = false;
                viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
                return;
            }

            if (state.GroupCatalog.Groups.Count == 0)
            {
                viewModel.CanCreateSetupBehaviorGroup = true;
                viewModel.CanDeleteSetupBehaviorGroup = false;
                viewModel.SetupBehaviorGroupPickerCreateLabel = "创建木桩";
                viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText = string.Empty;
                viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText = "当前没有可删除的行为组。";
                viewModel.SetupBehaviorGroupDeleteConfirming = false;
                viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
                return;
            }

            if (selectedGroup == null || !selectedGroup.HasProjectSource)
            {
                viewModel.CanCreateSetupBehaviorGroup = false;
                viewModel.CanDeleteSetupBehaviorGroup = false;
                viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText = "当前行为组没有可复制的 project revision。";
                viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText = "当前行为组不是可删除的项目行为组。";
                viewModel.SetupBehaviorGroupDeleteConfirming = false;
                viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = -1;
                return;
            }

            bool canDelete = controller.MatchConfig?.BehaviorGroups?.Drafts?.Count > 1;
            int confirmingDraftId = pendingDraftId >= 0 && pendingDraftId == selectedArenaDraftId
                ? pendingDraftId
                : -1;
            viewModel.CanCreateSetupBehaviorGroup = selectedGroup != null
                && controller.DraftWorkspace?.IsReadOnly != true;
            viewModel.SetupBehaviorGroupPickerCreateLabel = "新建副本";
            viewModel.CanDeleteSetupBehaviorGroup = canDelete;
            viewModel.SetupBehaviorGroupPickerCreateDisabledReasonText = viewModel.CanCreateSetupBehaviorGroup
                ? string.Empty
                : "当前本地工作区不可复制行为组。";
            viewModel.SetupBehaviorGroupPickerDeleteDisabledReasonText = canDelete
                ? string.Empty
                : "至少保留一个行为组，当前不能删除。";
            viewModel.SetupBehaviorGroupDeleteConfirming = confirmingDraftId >= 0;
            viewModel.SetupBehaviorGroupDeleteConfirmingDraftId = confirmingDraftId;
        }

        private static void FillSetupBehaviorGroupBasicInfo(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision exactGroup,
            bool isLocalDraft,
            bool canEdit)
        {
            viewModel.SetupBehaviorGroupEditorGroupDraftId = selectedGroup?.GroupDraftId ?? string.Empty;
            viewModel.SetupBehaviorGroupEditorStableId = selectedGroup?.PublishedStableId ?? string.Empty;
            viewModel.SetupBehaviorGroupEditorSourceRevision = selectedGroup?.SourceProjectRevision ?? 0;
            viewModel.SetupBehaviorGroupEditorSourceContentHash =
                selectedGroup?.SourceProjectContentHash ?? 0;
            viewModel.SetupBehaviorGroupEditorDisplayName = exactGroup?.DisplayName
                ?? selectedGroup?.DisplayName
                ?? string.Empty;
            viewModel.SetupBehaviorGroupEditorDescription = exactGroup?.Description ?? string.Empty;
            viewModel.SetupBehaviorGroupEditorTagsText = exactGroup != null
                ? string.Join(", ", exactGroup.Tags)
                : string.Empty;
            viewModel.SetupBehaviorGroupEditorIsLocalDraft = isLocalDraft;
            viewModel.SetupBehaviorGroupEditorSaveLabel = isLocalDraft
                ? "确认为正式行为组"
                : "保存修改";
            viewModel.SetupBehaviorGroupEditorIdentityText = isLocalDraft
                ? "本地草稿\n仅当前实验使用"
                : exactGroup != null
                ? exactGroup.StableGroupId
                    + "\nr" + exactGroup.Revision.ToString(CultureInfo.InvariantCulture)
                    + "  ·  项目 JSON"
                : "项目 revision 不可用";
            viewModel.CanEditSetupBehaviorGroupBasicInfo = canEdit;
            viewModel.SetupBehaviorGroupEditorDisabledReasonText = canEdit
                ? string.Empty
                : isLocalDraft
                    ? "项目行为组目录不可写，当前草稿仍可继续用于本次实验。"
                : exactGroup == null
                    ? "当前行为组的精确项目 revision 已不存在或内容已变化。"
                    : "项目行为组目录不可写。";
        }

        private static CharacterTestRuntimeAiLabSetupCharacterOptionViewModel[]
            BuildSetupCharacterOptions(CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            IReadOnlyList<CharacterTestCharacterDefinition> definitions = registry.Definitions;
            if (definitions == null || definitions.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabSetupCharacterOptionViewModel>();

            int selectedCharacterId = controller.MatchConfig
                .TryGetSlot(controller.SelectedSetupTeamId, controller.SelectedSetupSlotIndex)
                ?.CharacterId ?? 0;
            var options = new CharacterTestRuntimeAiLabSetupCharacterOptionViewModel[
                definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = definitions[i];
                string displayName = definition.DisplayName ?? string.Empty;
                options[i] = new CharacterTestRuntimeAiLabSetupCharacterOptionViewModel
                {
                    CharacterId = definition.CharacterId,
                    DisplayName = displayName,
                    MonogramText = ResolveBehaviorGroupMonogram(displayName),
                    Selected = definition.CharacterId == selectedCharacterId
                };
            }

            return options;
        }

        private static CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel[]
            BuildSetupBehaviorGroupOptions(CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = controller?.MatchConfig?.BehaviorGroups;
            IReadOnlyList<CharacterTestArenaBehaviorGroupDraft> drafts = store?.Drafts;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = controller?.DraftWorkspace;
            if ((drafts == null || drafts.Count == 0)
                && (workspace?.Groups == null || workspace.Groups.Count == 0))
                return Array.Empty<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>();

            CharacterTestRuntimeAiLabDraftSlot selectedSlot = controller.DraftWorkspace?.TryGetSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex);
            int selectedDraftId = selectedSlot?.ArenaBehaviorGroupDraftId
                ?? controller.MatchConfig
                    .TryGetSlot(controller.SelectedSetupTeamId, controller.SelectedSetupSlotIndex)
                    ?.BehaviorGroupDraftId
                ?? 0;
            string selectedGroupDraftId = selectedSlot?.GroupDraftId ?? string.Empty;
            var options = new List<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>();
            var projectedWorkspaceGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; drafts != null && i < drafts.Count; i++)
            {
                CharacterTestArenaBehaviorGroupDraft draft = drafts[i];
                CharacterTestRuntimeAiLabBehaviorGroupDraft workspaceGroup = null;
                if (workspace?.Groups != null)
                {
                    foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft candidate in workspace.Groups.Values)
                    {
                        if (CharacterTestRuntimeAiLabDraftWorkspace.MatchesUnmodifiedArenaGroup(candidate, draft))
                        {
                            workspaceGroup = candidate;
                            projectedWorkspaceGroups.Add(candidate.GroupDraftId);
                            break;
                        }
                        if (workspaceGroup == null
                            && candidate?.HasProjectSource == true
                            && string.Equals(
                                candidate.PublishedStableId,
                                draft.StableId,
                                StringComparison.Ordinal))
                        {
                            workspaceGroup = candidate;
                        }
                    }
                    if (workspaceGroup != null)
                        projectedWorkspaceGroups.Add(workspaceGroup.GroupDraftId);
                }
                bool available = store.TryGetDraftDisplay(
                    draft.DraftId,
                    out string displayName,
                    out string statusText,
                    out bool compileSuccess) && compileSuccess;
                string formattedDisplayName = FormatBehaviorGroupDisplayName(displayName)
                    + "  ·  r" + draft.SourceProjectRevision.ToString(CultureInfo.InvariantCulture)
                    + "  ·  项目 JSON";
                bool unpublished = workspaceGroup != null
                    && controller.HasUnpublishedBehaviorGroupContent(workspaceGroup, out _);
                CharacterTestRuntimeAiLabPublishUxState publishState =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                        hasPublishedSource: true,
                        hasUnpublishedChanges: unpublished);
                options.Add(new CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel
                {
                    DraftId = draft.DraftId,
                    GroupDraftId = workspaceGroup?.GroupDraftId ?? string.Empty,
                    ArenaBehaviorGroupDraftId = draft.DraftId,
                    IsLocalDraft = false,
                    DisplayName = formattedDisplayName,
                    MonogramText = ResolveBehaviorGroupMonogram(formattedDisplayName),
                    CardCount = CountProjectBehaviorCards(draft),
                    UnavailableReasonText = available
                        ? string.Empty
                        : TranslateBehaviorGroupStatus(statusText),
                    PublishState = publishState,
                    PublishStatusText =
                        CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                            publishState),
                    HasValidationProblems = !available,
                    ValidationStatusText =
                        CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(
                            !available),
                    Available = available,
                    Selected = !string.IsNullOrWhiteSpace(selectedGroupDraftId)
                        ? string.Equals(workspaceGroup?.GroupDraftId, selectedGroupDraftId, StringComparison.Ordinal)
                        : draft.DraftId == selectedDraftId
                });
            }

            if (workspace?.Groups != null)
            {
                foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
                {
                    if (group == null || group.IsBehaviorTestComposition
                        || projectedWorkspaceGroups.Contains(group.GroupDraftId))
                        continue;
                    CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                            group.Profile,
                            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                    options.Add(new CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel
                    {
                        DraftId = 0,
                        GroupDraftId = group.GroupDraftId,
                        ArenaBehaviorGroupDraftId = 0,
                        IsLocalDraft = true,
                        DisplayName = FormatBehaviorGroupDisplayName(group.DisplayName) + "  ·  本地草稿",
                        MonogramText = ResolveBehaviorGroupMonogram(group.DisplayName),
                        CardCount = CountProjectBehaviorCards(group),
                        UnavailableReasonText = compile.Success ? string.Empty : "草稿编译失败 · 不可用于测试",
                        PublishState = CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly,
                        PublishStatusText =
                            CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                                CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly),
                        HasValidationProblems = !compile.Success,
                        ValidationStatusText =
                            CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(
                                !compile.Success),
                        Available = compile.Success,
                        Selected = string.Equals(group.GroupDraftId, selectedGroupDraftId, StringComparison.Ordinal)
                    });
                }
            }

            return options.ToArray();
        }

        private static int CountProjectBehaviorCards(CharacterTestRuntimeAiLabBehaviorGroupDraft draft)
        {
            int count = 0;
            for (int i = 0; draft?.Entries != null && i < draft.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slot = draft.Entries[i].CoreSlotKind;
                if (slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting
                    && slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                    count++;
            }
            return count;
        }

        private static int CountProjectBehaviorCards(CharacterTestArenaBehaviorGroupDraft draft)
        {
            int count = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries = draft?.Profile?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slot = entries[i].CoreSlotKind;
                if (slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting
                    && slot != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                {
                    count++;
                }
            }
            return count;
        }

        private static string ResolveBehaviorGroupMonogram(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "?";
            return StringInfo.GetNextTextElement(displayName.Trim(), 0);
        }

        private static string TranslateBehaviorGroupStatus(string statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
                return "不可用于测试";
            if (statusText.StartsWith("Compile failed:", StringComparison.Ordinal))
                return "编译失败 · 不可用于测试";
            if (string.Equals(statusText, "Missing draft", StringComparison.Ordinal))
                return "草稿缺失 · 不可用于测试";
            return statusText;
        }

        private static void FillSetupSummary(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabSpawnValidationResult validation)
        {
            int team1Enabled = 0;
            int team2Enabled = 0;
            bool team1Error = false;
            bool team2Error = false;
            Vector2 team1Position = default;
            Vector2 team2Position = default;
            bool hasTeam1Position = false;
            bool hasTeam2Position = false;
            IReadOnlyList<CharacterTestRuntimeAiLabSpawnSlotValidation> slots = validation?.Slots;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    CharacterTestRuntimeAiLabSpawnSlotValidation slot = slots[i];
                    if (!slot.Enabled)
                        continue;

                    bool isTeam2 = slot.TeamId == CharacterTestArenaMatchConfig.Team2Id;
                    if (isTeam2)
                    {
                        team2Enabled++;
                        team2Error |= slot.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error;
                        if (!hasTeam2Position)
                        {
                            team2Position = slot.Position;
                            hasTeam2Position = true;
                        }
                    }
                    else
                    {
                        team1Enabled++;
                        team1Error |= slot.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error;
                        if (!hasTeam1Position)
                        {
                            team1Position = slot.Position;
                            hasTeam1Position = true;
                        }
                    }
                }
            }

            bool ready = validation != null && validation.CanStart;
            viewModel.SetupTopValidationText = ready ? "● 配置有效" : "● 配置待修复";
            viewModel.SetupTeam1CountText = "队伍 1      "
                + team1Enabled.ToString(CultureInfo.InvariantCulture)
                + "/3";
            viewModel.SetupTeam2CountText = "队伍 2      "
                + team2Enabled.ToString(CultureInfo.InvariantCulture)
                + "/3";
            viewModel.SetupTeam1ValidationText = team1Enabled > 0 && !team1Error ? "● 配置有效" : "● 需要检查";
            viewModel.SetupTeam2ValidationText = team2Enabled > 0 && !team2Error ? "● 配置有效" : "● 需要检查";
            viewModel.SetupReadyTitleText = ready ? "● 已准备" : "● 待修复";
            viewModel.SetupReadyDescriptionText = ready ? string.Empty : BuildSetupValidationMessage(validation);
            viewModel.SetupDistanceText = hasTeam1Position && hasTeam2Position
                ? "距离  "
                    + Vector2.Distance(team1Position, team2Position).ToString("0.#", CultureInfo.InvariantCulture)
                    + "m"
                : "距离  --";
            int agentCount = team1Enabled + team2Enabled;
            int behaviorGroupCount = controller.MatchConfig.BehaviorGroups?.Drafts.Count ?? 0;
            viewModel.SetupSceneText = "Arena · "
                + agentCount.ToString(CultureInfo.InvariantCulture)
                + " 智能体 · "
                + controller.FrameLimit.ToString(CultureInfo.InvariantCulture)
                + " 帧";
            viewModel.SetupAgentCountText = "智能体  "
                + agentCount.ToString(CultureInfo.InvariantCulture);
            viewModel.SetupBehaviorGroupCountText = "行为组  "
                + behaviorGroupCount.ToString(CultureInfo.InvariantCulture);
            viewModel.SetupFrameLimitStatusText = "帧上限  "
                + controller.FrameLimit.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildSetupValidationMessage(
            CharacterTestRuntimeAiLabSpawnValidationResult validation)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabSpawnSlotValidation> slots = validation?.Slots;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    CharacterTestRuntimeAiLabSpawnSlotValidation slot = slots[i];
                    if (slot.Enabled && slot.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error)
                    {
                        return "请检查队伍 "
                            + slot.TeamId.ToString(CultureInfo.InvariantCulture)
                            + " 槽位 "
                            + (slot.SlotIndex + 1).ToString(CultureInfo.InvariantCulture)
                            + "："
                            + TranslateSetupValidationMessage(slot.Message);
                    }
                }
            }

            return "双方队伍至少各启用一个有效智能体。";
        }

        private static string TranslateSetupValidationMessage(string message)
        {
            switch (message)
            {
                case "Outside arena bounds.":
                    return "出生点超出场地范围";
                case "Inside wall or blocking fixture.":
                    return "出生点位于墙体或阻挡物内";
                case "Too close to another enabled spawn.":
                    return "与其他启用出生点距离过近";
                default:
                    return string.IsNullOrEmpty(message) ? "配置无效" : message;
            }
        }

        private static CharacterTestRuntimeAiLabSetupSlotRowViewModel[] BuildSetupSlotRows(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabSpawnValidationResult validation)
        {
            var rows = new CharacterTestRuntimeAiLabSetupSlotRowViewModel[
                CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
            int index = 0;
            AppendSetupTeamRows(rows, ref index, controller, validation, CharacterTestArenaMatchConfig.Team1Id);
            AppendSetupTeamRows(rows, ref index, controller, validation, CharacterTestArenaMatchConfig.Team2Id);
            return rows;
        }

        private static void AppendSetupTeamRows(
            CharacterTestRuntimeAiLabSetupSlotRowViewModel[] rows,
            ref int index,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabSpawnValidationResult validation,
            int teamId)
        {
            for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
            {
                CharacterTestArenaSlotConfig slot = controller.MatchConfig.TryGetSlot(teamId, slotIndex);
                CharacterTestRuntimeAiLabSpawnSlotValidation slotValidation = default;
                validation?.TryGetSlot(teamId, slotIndex, out slotValidation);
                bool enabled = slot != null && slot.Enabled;
                bool hasError = enabled
                    && slotValidation.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error;
                rows[index++] = new CharacterTestRuntimeAiLabSetupSlotRowViewModel
                {
                    TeamId = teamId,
                    SlotIndex = slotIndex,
                    Enabled = enabled,
                    Selected = controller.SelectedSetupTeamId == teamId
                        && controller.SelectedSetupSlotIndex == slotIndex,
                    HasError = hasError,
                    Label = "队伍 "
                        + teamId.ToString(CultureInfo.InvariantCulture)
                        + " · 槽位 "
                        + (slotIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + " · "
                        + (hasError ? "需要修复" : enabled ? "已启用" : "未启用")
                };
            }
        }

        private static string ResolveCharacterDisplay(CharacterTestArenaSlotConfig slot)
        {
            if (slot == null)
                return "不可用";

            var registry = new CharacterTestCharacterDefinitionRegistry();
            return registry.TryGet(slot.CharacterId, out CharacterTestCharacterDefinition definition)
                ? definition.DisplayName
                : "未知角色 " + slot.CharacterId.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveBehaviorGroupDisplay(
            CharacterTestArenaBehaviorGroupDraftStore store,
            CharacterTestArenaSlotConfig slot)
        {
            if (store == null || slot == null)
                return "不可用";

            return store.TryGetDraftDisplay(
                    slot.BehaviorGroupDraftId,
                    out string displayName,
                    out _,
                    out bool compileSuccess)
                ? FormatBehaviorGroupDisplayName(displayName) + (compileSuccess ? string.Empty : " !")
                : "行为组缺失";
        }

        private static string FormatBehaviorGroupDisplayName(string displayName)
        {
            const string labDraftSuffix = " · Lab Draft";
            if (!string.IsNullOrEmpty(displayName)
                && displayName.EndsWith(labDraftSuffix, StringComparison.Ordinal))
            {
                displayName = displayName.Substring(
                    0,
                    displayName.Length - labDraftSuffix.Length);
            }

            switch (displayName)
            {
                case "Default melee":
                    return "默认近战";
                case "Kite survival":
                    return "风筝生存";
                case "Training dummy":
                    return "木桩";
                default:
                    return NonEmpty(displayName, "未命名行为组");
            }
        }

        private static string ResolveBehaviorDisplayName(
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog)
        {
            if (entry == null)
                return "未知行为";
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                return "攻击威胁闪避";
            }
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                return "持续靠近目标";
            }
            return catalog != null
                && catalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                ? CharacterTestRuntimeAiPlannerBehaviorCardPresentation.FormatBehaviorName(definition)
                : NonEmpty(entry.BehaviorId, "未知行为");
        }

        private static string BuildPreviewStatus(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            if (controller == null)
                return "场景标记不可用。";

            if (!controller.PreviewOverlayEnabled)
                return "场景标记已关闭。开启后可查看当前智能体的目标、距离、路径和移动意图。";

            if (selectedFrame == null || selectedFrame.Agents == null || selectedFrame.Agents.Count == 0)
                return "场景标记已开启。请开始或推进模拟，直到运行时帧中出现智能体。";

            if (!controller.TryBuildSelectedPreview(out CharacterTestRuntimeAiPlannerDevPreviewItem preview))
                return "场景标记已开启。" + preview.StatusLabel;

            string state = preview.HasGeometry ? preview.Headline : "等待场景几何信息";
            return "场景标记已开启 | 帧 "
                + preview.Frame.ToString(CultureInfo.InvariantCulture)
                + " | "
                + state
                + " | "
                + preview.StatusLabel;
        }

        private static string BuildAgentListText(
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            int selectedAgentIndex)
        {
            if (agents == null || agents.Count == 0)
                return "已选帧中没有运行时智能规划智能体。";

            var builder = new StringBuilder(768);
            for (int i = 0; i < agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[i];
                builder.Append(i == selectedAgentIndex ? "> " : "  ")
                    .Append("#")
                    .Append((i + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" 智能体 ")
                    .Append(agent.EntityId.ToString(CultureInfo.InvariantCulture))
                    .Append("  ")
                    .Append(CharacterTestRuntimeAiPlannerDevPresentationText.BuildActionLabel(agent))
                    .Append("  目标 ")
                    .Append(agent.TargetEntityId.ToString(CultureInfo.InvariantCulture))
                    .Append("  d ")
                    .Append(agent.TargetDistance.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append("  ")
                    .Append(agent.TargetVisible ? "可见" : "不可见")
                    .Append(agent.TargetDead ? " 已死亡" : " 未死亡")
                    .Append(agent.TargetLineOfSightBlocked ? " 受阻" : " 畅通")
                    .Append(agent.MovePathAvailable ? " 有路径" : " 无路径");
                if (i + 1 < agents.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

    }
}
