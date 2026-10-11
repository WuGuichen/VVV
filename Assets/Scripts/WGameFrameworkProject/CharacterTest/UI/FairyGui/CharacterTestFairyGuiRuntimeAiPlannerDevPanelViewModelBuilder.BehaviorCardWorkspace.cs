using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        internal static void FillBehaviorCardLibrary(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState? state,
            CharacterTestRuntimeAiLabDraftWorkspace draftWorkspace = null,
            CharacterTestRuntimeAiPlannerDevSceneController controller = null)
        {
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel target =
                viewModel.BehaviorCardWorkspace.Library;
            CharacterTestRuntimeAiLabBehaviorCardLibraryState resolvedState =
                state ?? CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default;
            target.Query = resolvedState.Query;
            target.Filter = resolvedState.Filter;
            if (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                && controller != null && draftWorkspace != null)
            {
                FillGroupAssemblyLibrary(viewModel, source, draftWorkspace, controller);
                return;
            }
            if (source == null)
            {
                target.Available = false;
                target.ActiveCount = 0;
                target.SummaryText = "牌库暂不可用";
                target.EmptyText = "BEHAVIOR_CARD_LIBRARY_UNAVAILABLE";
                target.HasCachedStructure = false;
                viewModel.BehaviorCardWorkspace.WorkspaceRevision = 0L;
                return;
            }

            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> cards = source.Cards;
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identity = viewModel.BehaviorCardIdentity;
            string editStableId = identity?.EditCard != null && identity.EditCard.HasPublishedSource
                ? identity.EditCard.PublishedStableId
                : string.Empty;
            string runtimeStableId = identity?.Runtime?.CommittedCard != null
                && identity.Runtime.CommittedCard.HasPublishedSource
                    ? identity.Runtime.CommittedCard.PublishedStableId
                    : string.Empty;
            long structureKey = ComputeLibraryStructureKey(
                source,
                target.Query,
                target.Filter,
                editStableId,
                runtimeStableId,
                identity?.EditCard?.DraftRevision ?? 0);
            string editDisplayName = identity?.EditCard?.DisplayName ?? string.Empty;
            unchecked
            {
                structureKey = structureKey * 397L ^ StableTextHash(editDisplayName);
                structureKey = structureKey * 397L ^ StableTextHash(resolvedState.FirstVisibleStableCardId);
                structureKey = structureKey * 397L ^ StableTextHash(resolvedState.FocusedStableCardId);
                structureKey = structureKey * 397L ^ resolvedState.ScrollOffsetY.GetHashCode();
                structureKey = structureKey * 397L ^ (draftWorkspace?.CurrentRevision ?? 0);
                structureKey = structureKey * 397L ^ (controller?.StandaloneProjectCardDraftRevision ?? 0);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel[] groupRows = viewModel.WorkbenchRows;
                for (int i = 0; groupRows != null && i < groupRows.Length; i++)
                {
                    if (groupRows[i]?.Visible == true)
                    {
                        structureKey = structureKey * 397L ^ StableTextHash(groupRows[i].CardDraftId);
                        structureKey = structureKey * 397L ^ StableTextHash(groupRows[i].PublishedStableId);
                        structureKey = structureKey * 397L ^ groupRows[i].DraftRevision;
                    }
                }
            }
            int visibleCount = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = cards[i];
                bool currentEdit = string.Equals(card.StableCardId, editStableId, StringComparison.Ordinal);
                bool runtimeActive = string.Equals(card.StableCardId, runtimeStableId, StringComparison.Ordinal);
                if (MatchesLibraryCard(card, currentEdit, runtimeActive, target.Query, target.Filter))
                    visibleCount++;
            }
            target.Available = true;
            target.ActiveCount = visibleCount;
            viewModel.BehaviorCardWorkspace.WorkspaceRevision = structureKey;
            if (target.HasCachedStructure && target.CachedStructureKey == structureKey)
            {
                RefreshLibraryRuntimePresentation(
                    target,
                    visibleCount,
                    editStableId,
                    runtimeStableId,
                    viewModel.BehaviorCardWorkspace.Timeline,
                    viewModel.CanEditProjectCardMetadata,
                    viewModel.CanEditWorkbench,
                    viewModel.WorkbenchRows,
                    draftWorkspace,
                    controller);
                return;
            }

            EnsureLibraryCapacity(target, visibleCount);
            int rowIndex = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = cards[i];
                bool currentEdit = string.Equals(card.StableCardId, editStableId, StringComparison.Ordinal);
                bool runtimeActive = string.Equals(card.StableCardId, runtimeStableId, StringComparison.Ordinal);
                if (!MatchesLibraryCard(card, currentEdit, runtimeActive, target.Query, target.Filter))
                    continue;
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel row = target.Cards[rowIndex++];
                CharacterTestRuntimeAiLabBehaviorCardDraft standaloneDraft = null;
                controller?.TryGetStandaloneProjectCardDraft(
                    card.StableCardId,
                    out standaloneDraft);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel groupRow = FindWorkbenchCardRow(
                    viewModel.WorkbenchRows,
                    card.StableCardId);
                row.StableCardId = card.StableCardId;
                row.PublishedRevision = card.Revision;
                row.PublishState = ResolveLibraryCardAuthoringState(
                    row.PublishedRevision,
                    groupRow,
                    standaloneDraft);
                row.IconKey = card.IconKey ?? string.Empty;
                row.Title = standaloneDraft != null
                    ? standaloneDraft.DisplayName
                    : currentEdit
                    ? NonEmpty(editDisplayName, NonEmpty(card.DisplayName, card.StableCardId))
                    : NonEmpty(card.DisplayName, card.StableCardId);
                row.Description = NonEmpty(card.UseSummary, card.ShortDescription);
                row.UseText = card.UseSummary ?? string.Empty;
                row.RiskText = card.RiskSummary ?? string.Empty;
                row.TagsText = JoinLibraryTags(card.Tags);
                row.CostText = card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                    ? card.Cost + " 费"
                    : "-- 费";
                row.RevisionText = card.Revision > 0 ? "rev " + card.Revision : "无兼容版本";
                row.StatusText = row.PublishState == CharacterTestRuntimeAiLabPublishUxState.Unavailable
                    ? BuildLibraryCardStatus(card, currentEdit, runtimeActive)
                    : CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                        row.PublishState);
                row.HasValidationProblems = groupRow?.HasValidationProblems == true;
                row.ValidationStatusText = groupRow?.ValidationStatusText ?? string.Empty;
                row.BlockReasonText = BuildLibraryBlockReason(card);
                row.CompatibleResponsibilities =
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy
                        .ResolveCompatibleResponsibilities(card.RequiredCapabilities);
                row.ResponsibilitySummaryText = BuildResponsibilitySummary(
                    row.CompatibleResponsibilities);
                row.Available = card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available;
                row.Unlocked = card.IsUnlocked;
                row.Installed = card.IsInstalled;
                row.InCurrentBehaviorGroup = groupRow != null;
                row.EntryIndex = groupRow?.EntryIndex ?? -1;
                row.ResponsibilityKinds = CopyResponsibilities(groupRow?.ResponsibilityKinds);
                row.ResponsibilityCompatibilityMask = groupRow?.ResponsibilityCompatibilityMask
                    ?? BuildResponsibilityCompatibilityMask(row.CompatibleResponsibilities);
                FillLibraryCardReferences(row, draftWorkspace);
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic =
                    ResolveLibraryCardLogic(viewModel.WorkbenchRows, card);
                FillLibraryCardConfiguration(
                    row,
                    logic,
                    row.InCurrentBehaviorGroup);
                FillLibraryCardPrimaryStage(row, logic);
                row.CurrentEdit = currentEdit;
                row.RuntimeActive = runtimeActive;
                row.ConfigurationEditable = row.InCurrentBehaviorGroup
                    && currentEdit
                    && viewModel.BehaviorCardWorkspace.Timeline?.CanEdit == true;
                row.DisplayNameEditable = controller?.ProjectCatalogState?.CanWriteProjectCatalogs == true;
                row.HasUnsavedMetadataChanges = standaloneDraft != null;
                row.ResponsibilityEditable = row.InCurrentBehaviorGroup
                    && currentEdit
                    && viewModel.CanEditWorkbench;
                row.ObservedTargetDistanceDecimeters = runtimeActive
                    ? viewModel.BehaviorCardWorkspace.Timeline?.ObservedTargetDistanceDecimeters ?? -1
                    : -1;
                row.Draft = currentEdit && (identity?.EditCard?.DraftRevision ?? 0) > 0;
                row.PinnedRevision = card.PinnedRevision;
                row.HasNewerRevision = card.HasNewerRevision;
            }

            SortLibraryRows(target.Cards, visibleCount);
            RefreshLibraryRuntimePresentation(
                target,
                visibleCount,
                editStableId,
                runtimeStableId,
                viewModel.BehaviorCardWorkspace.Timeline,
                viewModel.CanEditProjectCardMetadata,
                viewModel.CanEditWorkbench,
                viewModel.WorkbenchRows,
                draftWorkspace,
                controller);
            target.FirstVisibleIndex = ResolveLibraryRowIndex(
                target.Cards,
                visibleCount,
                resolvedState.FirstVisibleStableCardId,
                0);
            target.FirstVisibleIndex = Math.Min(
                target.FirstVisibleIndex,
                Math.Max(0, visibleCount - target.VisibleCapacity));
            target.FocusedIndex = ResolveLibraryRowIndex(
                target.Cards,
                visibleCount,
                resolvedState.FocusedStableCardId,
                -1);

            target.SummaryText = source.SlotCount + "/" + source.MaximumSlots
                + " 槽 · " + source.KnownCost + "/" + source.MaximumCost + " 费"
                + (source.IsSessionLocked ? " · 战斗已锁定" : string.Empty);
            target.EmptyText = visibleCount == 0 ? "当前角色还没有可显示的行为卡。" : string.Empty;
            target.CachedStructureKey = structureKey;
            target.HasCachedStructure = true;
        }

        private static void FillGroupAssemblyLibrary(CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source, CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            var target = view.BehaviorCardWorkspace.Library;
            workspace.TryGetGroupForSlot(controller.SelectedSetupTeamId, controller.SelectedSetupSlotIndex, out var group);
            long key = unchecked(((long)workspace.CurrentRevision * 397L ^ StableTextHash(group?.GroupDraftId)) * 397L
                ^ StableTextHash(controller.ProjectCatalogState?.CardFileHash));
            key = unchecked((key * 397L ^ StableTextHash(target.Query)) * 397L ^ (int)target.Filter);
            key = unchecked(key * 397L ^ (view.CanEditWorkbench ? 1L : 0L));
            view.BehaviorCardWorkspace.WorkspaceRevision = key;
            if (target.HasCachedStructure && target.CachedStructureKey == key) return;
            var rows = new List<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>();
            var represented = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in controller.GetBaseTacticalLibraryEntries().Where(x => x.Draft))
            {
                string id = item.Key.Substring(6);
                var draft = workspace.Cards[id];
                if (!string.IsNullOrEmpty(draft.PublishedStableId)) represented.Add(draft.PublishedStableId);
                bool installed = group?.Entries.Any(e => e.CardDraftId == id) == true;
                var responsibilities = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.ResolveCompatibleResponsibilities(draft.RequiredCapabilities);
                rows.Add(new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel {
                    StableCardId = item.Key, Title = item.Name, Description = item.Description, IconKey = draft.IconKey,
                    StatusText = item.Revision, RevisionText = "草稿 r" + draft.CurrentRevision,
                    Draft = true, Available = view.CanEditWorkbench, Unlocked = true,
                    Installed = installed, InCurrentBehaviorGroup = installed,
                    EntryIndex = installed ? group.Entries.FindIndex(e => e.CardDraftId == id) : -1,
                    CompatibleResponsibilities = responsibilities,
                    ResponsibilitySummaryText = BuildResponsibilitySummary(responsibilities),
                    ResponsibilityKinds = CopyResponsibilities(draft.ResponsibilityKinds),
                    ResponsibilityCompatibilityMask = BuildResponsibilityCompatibilityMask(responsibilities) });
            }
            if (source != null)
                foreach (var card in source.Cards)
                {
                    if (represented.Contains(card.StableCardId)) continue;
                    var groupRow = FindWorkbenchCardRow(view.WorkbenchRows, card.StableCardId);
                    var responsibilities = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.ResolveCompatibleResponsibilities(card.RequiredCapabilities);
                    rows.Add(new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel {
                        StableCardId = card.StableCardId, Title = NonEmpty(card.DisplayName, card.StableCardId), IconKey = card.IconKey,
                        Description = NonEmpty(card.UseSummary, card.ShortDescription),
                        StatusText = "正式 r" + card.Revision, RevisionText = "正式 r" + card.Revision,
                        Available = view.CanEditWorkbench && card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available,
                        Unlocked = card.IsUnlocked, Installed = card.IsInstalled, InCurrentBehaviorGroup = groupRow != null,
                        EntryIndex = groupRow?.EntryIndex ?? -1, PinnedRevision = card.PinnedRevision,
                        HasNewerRevision = card.HasNewerRevision, CompatibleResponsibilities = responsibilities,
                        ResponsibilitySummaryText = BuildResponsibilitySummary(responsibilities),
                        ResponsibilityKinds = CopyResponsibilities(groupRow?.ResponsibilityKinds),
                        ResponsibilityCompatibilityMask = BuildResponsibilityCompatibilityMask(responsibilities) });
                }
            string[] tokens = (target.Query ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var filter = target.Filter;
            target.Cards = rows.Where(row => tokens.All(token => (row.Title + " " + row.Description).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                && (filter == CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All
                    || ((filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed) != 0 && row.InCurrentBehaviorGroup)
                    || ((filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.CurrentDraft) != 0 && row.Draft)
                    || ((filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnlockedAvailable) != 0 && row.Available && row.Unlocked)
                    || ((filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible) != 0 && !row.Available)
                    || ((filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Locked) != 0 && !row.Unlocked)))
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.StableCardId, StringComparer.Ordinal).ToArray();
            target.ActiveCount = target.Cards.Length;
            target.Available = group != null;
            target.SummaryText = target.ActiveCount + " 张 · 正式卡与当前工作区草稿";
            target.EmptyText = target.ActiveCount == 0 ? "没有匹配的行为卡。" : string.Empty;
            target.CachedStructureKey = key;
            target.HasCachedStructure = true;
        }

        private static CharacterTestRuntimeAiLabWorkbenchRowViewModel FindWorkbenchCardRow(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            string publishedStableId)
        {
            if (string.IsNullOrEmpty(publishedStableId))
                return null;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i]?.Visible == true
                    && rows[i].RowKind == CharacterTestRuntimeAiLabWorkbenchRowKind.BehaviorCard
                    && string.Equals(
                        rows[i].PublishedStableId,
                        publishedStableId,
                        StringComparison.Ordinal))
                    return rows[i];
            }
            return null;
        }

        private static void RefreshLibraryRuntimePresentation(
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library,
            int count,
            string editStableId,
            string runtimeStableId,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            bool canEditProjectCardMetadata,
            bool canEditWorkbench,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] workbenchRows,
            CharacterTestRuntimeAiLabDraftWorkspace draftWorkspace,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            int observedDistance = timeline?.ObservedTargetDistanceDecimeters ?? -1;
            for (int i = 0; library?.Cards != null && i < count && i < library.Cards.Length; i++)
            {
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card = library.Cards[i];
                if (card == null)
                    continue;
                card.CurrentEdit = string.Equals(card.StableCardId, editStableId, StringComparison.Ordinal);
                card.RuntimeActive = string.Equals(card.StableCardId, runtimeStableId, StringComparison.Ordinal);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel groupRow = FindWorkbenchCardRow(
                    workbenchRows,
                    card.StableCardId);
                card.InCurrentBehaviorGroup = groupRow != null;
                card.EntryIndex = groupRow?.EntryIndex ?? -1;
                card.ResponsibilityKinds = CopyResponsibilities(groupRow?.ResponsibilityKinds);
                card.ResponsibilityCompatibilityMask = groupRow?.ResponsibilityCompatibilityMask
                    ?? BuildResponsibilityCompatibilityMask(card.CompatibleResponsibilities);
                CharacterTestRuntimeAiLabBehaviorCardDraft standaloneDraft = null;
                controller?.TryGetStandaloneProjectCardDraft(
                    card.StableCardId,
                    out standaloneDraft);
                if (standaloneDraft != null)
                    card.Title = standaloneDraft.DisplayName;
                card.PublishState = ResolveLibraryCardAuthoringState(
                    card.PublishedRevision,
                    groupRow,
                    standaloneDraft);
                if (card.PublishState != CharacterTestRuntimeAiLabPublishUxState.Unavailable)
                    card.StatusText =
                        CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                            card.PublishState);
                card.HasValidationProblems = groupRow?.HasValidationProblems == true;
                card.ValidationStatusText = groupRow?.ValidationStatusText ?? string.Empty;
                FillLibraryCardReferences(card, draftWorkspace);
                card.ConfigurationEditable = card.InCurrentBehaviorGroup
                    && card.CurrentEdit
                    && timeline?.CanEdit == true;
                card.DisplayNameEditable = controller?.ProjectCatalogState?.CanWriteProjectCatalogs == true;
                card.HasUnsavedMetadataChanges = standaloneDraft != null;
                card.ResponsibilityEditable = card.InCurrentBehaviorGroup
                    && card.CurrentEdit
                    && canEditWorkbench;
                card.ObservedTargetDistanceDecimeters = card.RuntimeActive
                    ? observedDistance
                    : -1;
            }
        }

        private static CharacterTestRuntimeAiLabPublishUxState ResolveLibraryCardAuthoringState(
            int publishedRevision,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel groupRow,
            CharacterTestRuntimeAiLabBehaviorCardDraft standaloneDraft)
        {
            if (publishedRevision <= 0)
                return CharacterTestRuntimeAiLabPublishUxState.Unavailable;
            bool hasUnpublishedChanges = groupRow != null
                ? groupRow.PublishState
                    == CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges
                : standaloneDraft != null;
            return CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                true,
                hasUnpublishedChanges);
        }

        internal static void FillLibraryCardReferences(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel target,
            CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            target.ReferencingGroupCount = 0;
            target.ReferenceCount = 0;
            if (workspace == null || string.IsNullOrWhiteSpace(target.StableCardId))
            {
                target.ReferenceSummaryText = "引用信息暂不可用";
                return;
            }

            var groupLabels = new List<string>();
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
            {
                if (group == null)
                    continue;
                int groupReferenceCount = 0;
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (entry != null
                        && workspace.Cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                        && string.Equals(
                            card.PublishedStableId,
                            target.StableCardId,
                            StringComparison.Ordinal))
                    {
                        groupReferenceCount++;
                    }
                }
                if (groupReferenceCount == 0)
                    continue;

                target.ReferencingGroupCount++;
                target.ReferenceCount += groupReferenceCount;
                groupLabels.Add(FormatReferencingGroupLabel(group));
            }

            if (target.ReferenceCount == 0)
            {
                target.ReferenceSummaryText = "未被任何行为组引用";
                return;
            }

            groupLabels.Sort(StringComparer.Ordinal);
            target.ReferenceSummaryText = target.ReferencingGroupCount.ToString(CultureInfo.InvariantCulture)
                + " 个行为组 · "
                + target.ReferenceCount.ToString(CultureInfo.InvariantCulture)
                + " 处引用 · "
                + string.Join("、", groupLabels);
        }

        private static string FormatReferencingGroupLabel(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            string displayName = NonEmpty(group?.DisplayName, group?.PublishedStableId);
            if (group?.HasProjectSource == true)
            {
                return displayName
                    + "（项目 r"
                    + group.SourceProjectRevision.ToString(CultureInfo.InvariantCulture)
                    + "）";
            }
            return displayName + (group?.IsBehaviorTestComposition == true
                ? "（测试）"
                : "（草稿）");
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] source) =>
            source == null || source.Length == 0
                ? new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other }
                : (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[])source.Clone();

        private static int BuildResponsibilityCompatibilityMask(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities)
        {
            int mask = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> ordered =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; i < ordered.Count; i++)
                mask |= 1 << (int)ordered[i];
            return mask;
        }

        private static void FillLibraryCardPrimaryStage(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel target,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            target.HasPrimaryStage = payload != null && payload.Stages.Count > 0;
            if (!target.HasPrimaryStage)
                return;
            CharacterTestRuntimeAiLabLogicStage source = ResolvePrimaryStage(payload);
            CharacterTestRuntimeAiLabLogicStageViewModel stage = target.PrimaryStage;
            stage.Index = 0;
            stage.Order = source.Order;
            stage.StartFrame = source.PlannedStartFrame;
            stage.DurationFrames = source.PlannedDurationFrames;
            stage.Kind = source.Kind;
            stage.DurationMode = source.DurationMode;
            stage.AbilityId = source.AbilityId;
            stage.CharacterActionId = source.CharacterActionId;
            stage.LegacyActionKind = source.LegacyActionKind;
            stage.MovementStrategy = source.MovementStrategy;
            stage.MovementTargetKind = source.MovementTargetKind;
            stage.MoveTargetPositionX = source.MoveTargetPositionX;
            stage.MoveTargetPositionZ = source.MoveTargetPositionZ;
            stage.MoveTargetEntityId = source.MoveTargetEntityId;
            stage.MoveMinDistance = source.MoveMinDistance;
            stage.MoveMaxDistance = source.MoveMaxDistance;
            stage.WaitFrames = source.WaitFrames;
            stage.Note = source.Note ?? string.Empty;
            stage.StageId = source.StageId ?? string.Empty;
            stage.Title = source.Title ?? string.Empty;
            stage.OwnsPrimaryAction = source.OwnsPrimaryAction;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload ResolveLibraryCardLogic(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            string stableCardId = card?.StableCardId ?? string.Empty;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                if (row?.Visible == true
                    && string.Equals(
                        row.PublishedStableId,
                        stableCardId,
                        StringComparison.Ordinal)
                    && row.LogicPayload != null)
                    return row.LogicPayload;
            }
            return card?.CloneLogicPayload();
        }

        private static void FillLibraryCardConfiguration(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel target,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            bool usesCurrentGroupDraft)
        {
            target.ConfigurationFieldCount = 0;
            target.ConfigurationSourceText = usesCurrentGroupDraft
                ? "当前行为组配置"
                : "项目默认配置 · 只读预览";
            target.LogicStructureText = payload == null
                ? "尚无可用配置"
                : payload.Stages.Count.ToString(CultureInfo.InvariantCulture) + " 阶段 · "
                    + payload.Detectors.Count.ToString(CultureInfo.InvariantCulture) + " 检测 · "
                    + payload.Transitions.Count.ToString(CultureInfo.InvariantCulture) + " 跳转";
            if (payload == null || payload.Stages.Count == 0)
                return;

            CharacterTestRuntimeAiLabLogicStage stage = ResolvePrimaryStage(payload);
            AddConfigurationField(target, "阶段类型", StageKindLabel(stage.Kind));
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    AddConfigurationField(target, "移动方式", MovementStrategyLabel(stage.MovementStrategy));
                    AddConfigurationField(target, "目标", MovementTargetLabel(stage));
                    if (UsesDistanceSetting(stage.MovementStrategy))
                    {
                        AddConfigurationField(
                            target,
                            stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                                ? "停止距离"
                                : "目标距离",
                            FormatMovementDistance(stage));
                    }
                    else
                    {
                        AddConfigurationField(
                            target,
                            "检测规则",
                            payload.Detectors.Count.ToString(CultureInfo.InvariantCulture) + " 个");
                    }
                    AddConfigurationField(target, "持续方式", DurationModeLabel(stage.DurationMode, stage.PlannedDurationFrames));
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    AddConfigurationField(target, "Ability", stage.AbilityId > 0
                        ? stage.AbilityId.ToString(CultureInfo.InvariantCulture)
                        : "尚未指定");
                    AddConfigurationField(target, "持续方式", DurationModeLabel(stage.DurationMode, stage.PlannedDurationFrames));
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    AddConfigurationField(target, "角色动作", !stage.CharacterActionId.IsValid
                        ? "尚未指定"
                        : stage.CharacterActionId.ToString());
                    AddConfigurationField(target, "持续方式", DurationModeLabel(stage.DurationMode, stage.PlannedDurationFrames));
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    AddConfigurationField(target, "等待时间", stage.WaitFrames.ToString(CultureInfo.InvariantCulture) + " 帧");
                    break;
                default:
                    AddConfigurationField(target, "阶段名称", string.IsNullOrWhiteSpace(stage.Title) ? "未命名阶段" : stage.Title);
                    AddConfigurationField(target, "持续方式", DurationModeLabel(stage.DurationMode, stage.PlannedDurationFrames));
                    break;
            }
        }

    }
}
