using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelContentBinder
    {
        internal void RefreshWorkbenchRowsIfNeeded(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (root == null || viewModel == null)
                return;
            long signature = ComputeWorkbenchProjectionSignature(viewModel);
            if (!_workbenchListRebuildPending
                && _hasWorkbenchProjectionSignature
                && _workbenchProjectionSignature == signature)
            {
                return;
            }

            BindWorkbenchRows(root, viewModel);
        }

        private static long ComputeWorkbenchProjectionSignature(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            unchecked
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows =
                    viewModel?.WorkbenchRows
                    ?? Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
                int count = Mathf.Clamp(viewModel?.WorkbenchRowCount ?? 0, 0, rows.Length);
                long signature = count;
                signature = signature * 397L ^ StringComparer.Ordinal.GetHashCode(
                    viewModel?.WorkbenchGroupIdentity ?? string.Empty);
                int[] responsibilityCounts = viewModel?.WorkbenchResponsibilityCounts;
                for (int i = 0; responsibilityCounts != null && i < responsibilityCounts.Length; i++)
                    signature = signature * 397L ^ responsibilityCounts[i];
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                    if (row == null)
                    {
                        signature = signature * 397L;
                        continue;
                    }
                    signature = signature * 397L ^ (int)row.RowKind;
                    signature = signature * 397L ^ row.EntryIndex;
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
                        row.ResponsibilityKinds ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
                    for (int responsibilityIndex = 0;
                        responsibilityIndex < responsibilities.Length;
                        responsibilityIndex++)
                    {
                        signature = signature * 397L ^ (int)responsibilities[responsibilityIndex];
                    }
                    signature = signature * 397L ^ row.DraftRevision;
                    signature = signature * 397L ^ StringComparer.Ordinal.GetHashCode(
                        row.CardDraftId ?? string.Empty);
                    signature = signature * 397L ^ StringComparer.Ordinal.GetHashCode(
                        row.TitleText ?? string.Empty);
                    signature = signature * 397L ^ StringComparer.Ordinal.GetHashCode(
                        row.ResponsibilitySummaryText ?? string.Empty);
                    signature = signature * 397L ^ (row.Visible ? 1 : 0);
                    signature = signature * 397L ^ (row.Selected ? 1 : 0);
                    signature = signature * 397L ^ (row.Disabled ? 1 : 0);
                    signature = signature * 397L ^ (row.Invalid ? 1 : 0);
                }
                return signature;
            }
        }

        private void WriteWorkbenchListSnapshot(
            Fgui.GList list,
            string action,
            string reason)
        {
            var builder = new StringBuilder(512);
            builder.Append("action=").Append(action)
                .Append(" reason=").Append(reason)
                .Append(" activeTab=").Append(_activeResponsibilityFilter?.ToString() ?? "All")
                .Append(" filteredEntryCount=").Append(_visibleWorkbenchRowCount)
                .Append(" uiChildren=").Append(list?.numChildren ?? -1)
                .Append(" model=[");
            for (int i = 0; i < _visibleWorkbenchRowCount; i++)
            {
                if (i > 0)
                    builder.Append(" | ");
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = _visibleWorkbenchRows[i];
                builder.Append(row?.EntryIndex ?? -1).Append(':')
                    .Append(row?.TitleText ?? "<null>");
            }
            builder.Append("] ui=[");
            int childCount = list?.numChildren ?? 0;
            for (int i = 0; i < childCount; i++)
            {
                if (i > 0)
                    builder.Append(" | ");
                Fgui.GObject child = list.GetChildAt(i);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row =
                    child?.data as CharacterTestRuntimeAiLabWorkbenchRowViewModel;
                builder.Append(row?.EntryIndex ?? -1).Append(':')
                    .Append(row?.TitleText ?? child?.name ?? "<null>")
                    .Append('@')
                    .Append(child?.y ?? -1f);
            }
            builder.Append(']');
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                builder.ToString());
        }

        private void RebuildVisibleWorkbenchRows(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] source,
            int sourceCount)
        {
            if (_visibleWorkbenchRows.Length < sourceCount)
                Array.Resize(ref _visibleWorkbenchRows, sourceCount);

            int output = 0;
            for (int i = 0; source != null && i < sourceCount; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = source[i];
                if (row?.Visible != true)
                    continue;
                if (_activeResponsibilityFilter.HasValue
                    && !row.HasResponsibility(_activeResponsibilityFilter.Value))
                    continue;
                CharacterTestRuntimeAiLabWorkbenchRowViewModel snapshot =
                    _visibleWorkbenchRows[output]
                    ?? new CharacterTestRuntimeAiLabWorkbenchRowViewModel();
                CopyWorkbenchRow(row, snapshot);
                _visibleWorkbenchRows[output++] = snapshot;
            }
            _visibleWorkbenchRowCount = output;
        }

        private static void CopyWorkbenchRow(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel source,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel destination)
        {
            destination.RowKind = source.RowKind;
            destination.EntryIndex = source.EntryIndex;
            destination.CardDraftId = source.CardDraftId;
            destination.PublishedStableId = source.PublishedStableId;
            destination.DraftRevision = source.DraftRevision;
            destination.PublishState = source.PublishState;
            destination.PublishStatusText = source.PublishStatusText;
            destination.HasValidationProblems = source.HasValidationProblems;
            destination.ValidationStatusText = source.ValidationStatusText;
            destination.LogicPayload = source.LogicPayload;
            destination.IconKey = source.IconKey;
            destination.Label = source.Label;
            destination.TitleText = source.TitleText;
            destination.BadgeText = source.BadgeText;
            destination.TriggerText = source.TriggerText;
            destination.ActionText = source.ActionText;
            destination.OutcomeText = source.OutcomeText;
            destination.PriorityText = source.PriorityText;
            destination.ExecutionDetailText = source.ExecutionDetailText;
            destination.ExecutionState = source.ExecutionState;
            destination.ResponsibilityKind = source.ResponsibilityKind;
            destination.ResponsibilityKinds = source.ResponsibilityKinds != null
                ? (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[])source.ResponsibilityKinds.Clone()
                : Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            destination.ResponsibilityCompatibilityMask = source.ResponsibilityCompatibilityMask;
            destination.ResponsibilityDisplayText = source.ResponsibilityDisplayText;
            destination.ResponsibilityExpanded = source.ResponsibilityExpanded;
            destination.ResponsibilityTotalCount = source.ResponsibilityTotalCount;
            destination.ResponsibilityEnabledCount = source.ResponsibilityEnabledCount;
            destination.ResponsibilitySummaryText = source.ResponsibilitySummaryText;
            destination.ResponsibilityColor = source.ResponsibilityColor;
            destination.ResponsibilityWarning = source.ResponsibilityWarning;
            destination.ExecutionStepId = source.ExecutionStepId;
            destination.Visible = source.Visible;
            destination.Selected = source.Selected;
            destination.IsCurrentBehavior = source.IsCurrentBehavior;
            destination.Disabled = source.Disabled;
            destination.Invalid = source.Invalid;
        }

        private void BindResponsibilityHeader(
            Fgui.GComponent card,
            Fgui.GObject cardObject,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            float width,
            bool canEdit)
        {
            const float headerHeight = 48f;
            cardObject.data = row;
            cardObject.visible = row?.Visible == true;
            cardObject.touchable = row?.Visible == true;
            cardObject.grayed = false;
            cardObject.SetSize(width, headerHeight);
            cardObject.draggable = false;
            cardObject.onDragStart.Clear();
            cardObject.onDragMove.Clear();
            cardObject.onDragEnd.Clear();

            Color32 accent = ParseColor(row?.ResponsibilityColor, new Color32(119, 130, 140, 255));
            Fgui.GImage background = card.GetChild("cardBackground")?.asImage;
            Fgui.GGraph overlay = card.GetChild("stateOverlay")?.asGraph;
            Fgui.GGraph outline = card.GetChild("selectionOutline")?.asGraph;
            Fgui.GGraph bar = card.GetChild("selectionBar")?.asGraph;
            ApplyBehaviorCardBackgroundVisual(
                background,
                overlay,
                outline,
                bar,
                null,
                width,
                42f,
                showRuntimeState: false);
            if (background != null)
                background.color = new Color32(24, 43, 51, 255);
            if (overlay != null)
                overlay.DrawRect(width, 42f, 1, new Color32(44, 79, 90, 255), new Color32(6, 31, 38, 220));
            if (bar != null)
            {
                bar.visible = true;
                bar.SetXY(0f, 0f);
                bar.SetSize(4f, 42f);
                bar.color = accent;
            }

            string marker = row != null && row.ResponsibilityKind == _expandedResponsibility
                ? "▼ "
                : "▶ ";
            BindText(card, "title", marker + (row?.TitleText ?? string.Empty));
            BindText(card, "badgeText", row?.ResponsibilityEnabledCount.ToString() ?? "0");
            BindText(card, "triggerText", row?.ResponsibilitySummaryText ?? string.Empty);
            BindText(card, "orderText", string.Empty);
            BindText(card, "actionText", string.Empty);
            BindText(card, "outcomeText", string.Empty);
            BindText(card, "priorityText", string.Empty);
            BindText(card, "disabledText", string.Empty);
            SetWorkbenchCardChildVisible(card.GetChild("iconBackground"), false);
            SetWorkbenchCardChildVisible(card.GetChild("behaviorIcon"), false);
            SetWorkbenchCardChildVisible(card.GetChild("moreText"), false);
            Fgui.GTextField title = card.GetChild("title")?.asTextField;
            Fgui.GTextField summary = card.GetChild("triggerText")?.asTextField;
            Fgui.GTextField count = card.GetChild("badgeText")?.asTextField;
            ConfigureBehaviorLogicCardText(title, 18f, 0f, 120f, 42f, 18);
            ConfigureBehaviorLogicCardText(summary, 142f, 0f, Mathf.Max(80f, width - 220f), 42f, 12);
            ConfigureBehaviorLogicCardText(count, width - 88f, 0f, 34f, 42f, 15);
            if (title != null)
                title.color = accent;
            if (summary != null)
                summary.color = row?.ResponsibilityWarning == true
                    ? new Color32(234, 165, 70, 255)
                    : new Color32(156, 184, 194, 255);
            if (count != null)
                count.color = new Color32(226, 238, 242, 255);

            Fgui.GButton toggle = card.GetChild(WorkbenchCardToggleButton)?.asButton;
            if (toggle != null)
                toggle.visible = false;
            Fgui.GButton add = card.GetChild(WorkbenchCardDeleteButton)?.asButton;
            if (add != null)
            {
                add.visible = true;
                add.enabled = canEdit;
                add.grayed = !canEdit;
                add.SetXY(width - 46f, 5f);
                add.SetSize(34f, 32f);
                add.icon = string.Empty;
                add.title = "+";
                add.titleFontSize = 22;
                add.onClick.Set(() =>
                {
                    if (canEdit && row != null)
                        _openResponsibilityLibrary?.Invoke(row.ResponsibilityKind);
                });
            }
        }

        private static void BindResponsibilityGuide(
            Fgui.GComponent card,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            float height)
        {
            if (card == null)
                return;
            Fgui.GGraph guide = card.GetChild("responsibilityGuide")?.asGraph;
            if (guide == null)
            {
                guide = new Fgui.GGraph
                {
                    name = "responsibilityGuide",
                    touchable = false
                };
                card.AddChildAt(guide, 0);
            }
            guide.visible = height > 0f;
            guide.SetXY(0f, 0f);
            guide.SetSize(2f, height);
            Color32 color = ParseColor(row?.ResponsibilityColor, new Color32(119, 130, 140, 255));
            guide.DrawRect(2f, height, 0, Color.clear, new Color32(color.r, color.g, color.b, 130));
        }

        private static Color32 ParseColor(string value, Color32 fallback)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && ColorUtility.TryParseHtmlString(value, out Color parsed))
                return parsed;
            return fallback;
        }

        private CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ResolveResponsibilityAtPointer(
            Fgui.GList list,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows,
            int rowCount)
        {
            Vector2 point = list.GlobalToLocal(Fgui.Stage.inst.touchPosition);
            float pointerY = point.y + (list.scrollPane?.posY ?? 0f);
            float y = 0f;
            for (int i = 0; rows != null && i < rowCount; i++)
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row = rows[i];
                if (row == null)
                    continue;
                float height = row.IsHeader
                    ? 48f
                    : row.ResponsibilityKind == _expandedResponsibility ? 68f : 0f;
                if (row.IsHeader && pointerY >= y && pointerY < y + height)
                    return row.ResponsibilityKind;
                y += height;
            }
            return _expandedResponsibility;
        }

        private static void ApplyBehaviorLogicWorkbenchCardVisual(
            Fgui.GComponent card,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            bool behaviorLogic)
        {
            if (card == null)
                return;

            Fgui.GObject order = card.GetChild("orderText");
            Fgui.GObject iconBackground = card.GetChild("iconBackground");
            Fgui.GObject behaviorIcon = card.GetChild("behaviorIcon");
            Fgui.GObject more = card.GetChild("moreText");
            Fgui.GObject dynamicBehaviorIcon = card.GetChild("behaviorCardIconVisual");
            Fgui.GTextField title = card.GetChild("title")?.asTextField;
            Fgui.GTextField summary = card.GetChild("triggerText")?.asTextField;
            Fgui.GTextField priority = card.GetChild("priorityText")?.asTextField;
            Fgui.GTextField state = card.GetChild("badgeText")?.asTextField;
            Fgui.GImage background = card.GetChild("cardBackground")?.asImage;
            Fgui.GGraph stateOverlay = card.GetChild("stateOverlay")?.asGraph;
            Fgui.GGraph selectionOutline = card.GetChild("selectionOutline")?.asGraph;
            Fgui.GGraph accent = card.GetChild("selectionBar")?.asGraph;
            if (!behaviorLogic)
            {
                SetWorkbenchCardChildVisible(order, row?.IsCoreSlot != true);
                SetWorkbenchCardChildVisible(iconBackground, true);
                SetWorkbenchCardChildVisible(behaviorIcon, true);
                SetWorkbenchCardChildVisible(dynamicBehaviorIcon, true);
                SetWorkbenchCardChildVisible(more, true);
                SetWorkbenchCardChildVisible(priority, true);
                SetWorkbenchCardChildGeometry(order, 42f, 29f, 34f, 24f);
                SetWorkbenchCardChildGeometry(iconBackground, 82f, 12f, 60f, 60f);
                SetWorkbenchCardChildGeometry(behaviorIcon, 96f, 26f, 32f, 32f);
                SetWorkbenchCardChildGeometry(more, card.width - 42f, 25f, 24f, 30f);
                RestoreWorkbenchCardText(
                    title,
                    164f,
                    14f,
                    Mathf.Max(80f, card.width - 560f),
                    26f,
                    17,
                    Fgui.AlignType.Left);
                RestoreWorkbenchCardText(
                    summary,
                    164f,
                    48f,
                    Mathf.Max(80f, card.width - 640f),
                    20f,
                    11,
                    Fgui.AlignType.Left);
                RestoreWorkbenchCardText(
                    priority,
                    card.width - 350f,
                    31f,
                    130f,
                    22f,
                    12,
                    Fgui.AlignType.Right);
                RestoreWorkbenchCardText(
                    state,
                    card.width - 210f,
                    31f,
                    150f,
                    22f,
                    12,
                    Fgui.AlignType.Right);
                ApplyBehaviorCardBackgroundVisual(
                    background,
                    stateOverlay,
                    selectionOutline,
                    accent,
                    row,
                    card.width,
                    card.height,
                    showRuntimeState: true);
                if (title != null)
                {
                    title.color = CharacterTestFairyGuiRuntimeAiLabSemanticColors
                        .ResolveBehaviorCardTitleColor(
                            row?.ExecutionState ?? CharacterTestRuntimeAiLabExecutionDisplayState.None);
                }
                return;
            }

            SetWorkbenchCardChildVisible(order, false);
            SetWorkbenchCardChildVisible(iconBackground, false);
            SetWorkbenchCardChildVisible(behaviorIcon, false);
            SetWorkbenchCardChildVisible(dynamicBehaviorIcon, false);
            SetWorkbenchCardChildVisible(more, false);
            SetWorkbenchCardChildVisible(priority, false);

            float cardWidth = card.width;
            const float cardHeight = 68f;
            bool showActions = row?.Selected == true;
            ApplyBehaviorCardBackgroundVisual(
                background,
                stateOverlay,
                selectionOutline,
                accent,
                row,
                cardWidth,
                cardHeight,
                showRuntimeState: false);
            ConfigureBehaviorLogicCardText(
                title,
                18f,
                0f,
                Mathf.Max(80f, cardWidth - (showActions ? 170f : 128f)),
                cardHeight,
                19);
            SetWorkbenchCardChildVisible(summary, false);
            ConfigureBehaviorLogicCardText(
                state,
                Mathf.Max(18f, cardWidth - 110f),
                0f,
                92f,
                cardHeight,
                15);
            if (state != null)
                state.align = Fgui.AlignType.Right;
            SetWorkbenchCardChildVisible(state, !showActions);
            if (title != null)
            {
                title.color = CharacterTestFairyGuiRuntimeAiLabSemanticColors
                    .ResolveBehaviorCardTitleColor(
                        row?.ExecutionState ?? CharacterTestRuntimeAiLabExecutionDisplayState.None);
            }
        }

        private void BindBehaviorLogicWorkbenchCardActions(
            Fgui.GComponent card,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (card == null)
                return;

            bool visible = viewModel?.ShowBehaviorLogic == true
                && row?.Visible == true
                && row.Selected
                && !row.IsCoreSlot;
            Fgui.GButton responsibilities = card.GetChild(WorkbenchCardResponsibilitiesButton)?.asButton;
            Fgui.GButton toggle = card.GetChild(WorkbenchCardToggleButton)?.asButton;
            Fgui.GButton delete = card.GetChild(WorkbenchCardDeleteButton)?.asButton;
            if (responsibilities != null)
            {
                responsibilities.relations?.ClearAll();
                responsibilities.SetXY(card.width - 120f, 17f);
                responsibilities.SetSize(34f, 34f);
                responsibilities.visible = visible;
                responsibilities.touchable = visible && viewModel?.CanEditWorkbench == true;
                responsibilities.grayed = responsibilities.visible && !responsibilities.touchable;
                responsibilities.title = string.Empty;
                responsibilities.icon = Fgui.UIPackage.GetItemURL(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "action_slots_32x32");
                Fgui.GObject respBase = responsibilities.GetChild("base");
                if (respBase != null) respBase.visible = false;
                Fgui.GObject respOver = responsibilities.GetChild("overOverlay");
                if (respOver != null) respOver.visible = false;
                Fgui.GObject respDown = responsibilities.GetChild("downOverlay");
                if (respDown != null) respDown.visible = false;
                Fgui.GLoader responsibilityIcon = responsibilities.GetChild("icon")?.asLoader;
                if (responsibilityIcon != null)
                {
                    responsibilityIcon.visible = !string.IsNullOrEmpty(responsibilities.icon);
                    responsibilityIcon.SetSize(22f, 22f);
                    responsibilityIcon.color = new Color32(140, 185, 205, 220);
                }
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(
                    responsibilities,
                    0f);
                responsibilities.onTouchBegin.Set(context => context.StopPropagation());
                responsibilities.onClick.Set(context =>
                {
                    context.StopPropagation();
                    ShowResponsibilityEditor(responsibilities, row, viewModel?.CanEditWorkbench == true);
                });
                BindTooltip(responsibilities, "设置职责分类");
            }
            BindWorkbenchCardActionButton(
                toggle,
                row?.Disabled == true
                    ? "action_slot_enable_32x32"
                    : "action_slot_disable_32x32",
                viewModel?.ToggleEntryLabel ?? (row?.Disabled == true ? "启用" : "禁用"),
                new Color32(130, 195, 175, 220),
                card.width - 80f,
                17f,
                34f,
                34f,
                visible,
                visible && viewModel?.CanEditWorkbench == true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry,
                row?.EntryIndex);
            BindWorkbenchCardActionButton(
                delete,
                "action_remove_32x32",
                "从行为组移除",
                new Color32(215, 125, 130, 220),
                card.width - 40f,
                17f,
                34f,
                34f,
                visible,
                visible
                    && viewModel?.CanEditWorkbench == true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                row?.EntryIndex);
        }

        private void BindWorkbenchCardActionButton(
            Fgui.GButton button,
            string iconResourceName,
            string tooltip,
            Color32 iconColor,
            float x,
            float y,
            float width,
            float height,
            bool visible,
            bool enabled,
            string commandId,
            object commandPayload)
        {
            if (button == null)
                return;

            button.relations?.ClearAll();
            button.SetXY(x, y);
            button.SetSize(width, height);
            string iconUrl = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                iconResourceName);
            button.icon = iconUrl;
            button.title = string.IsNullOrEmpty(iconUrl) && !string.IsNullOrEmpty(tooltip)
                ? tooltip.Substring(0, 1)
                : string.Empty;
            Fgui.GObject btnBase = button.GetChild("base");
            if (btnBase != null) btnBase.visible = false;
            Fgui.GObject btnOver = button.GetChild("overOverlay");
            if (btnOver != null) btnOver.visible = false;
            Fgui.GObject btnDown = button.GetChild("downOverlay");
            if (btnDown != null) btnDown.visible = false;
            Fgui.GLoader icon = button.GetChild("icon")?.asLoader;
            if (icon != null)
            {
                icon.visible = !string.IsNullOrEmpty(iconUrl);
                icon.SetSize(22f, 22f);
                icon.color = iconColor;
            }
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(button, 0f);
            BindTooltip(button, tooltip ?? string.Empty);
            button.visible = visible;
            button.touchable = enabled;
            button.grayed = visible && !enabled;
            button.onTouchBegin.Set(context => context.StopPropagation());
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                if (!button.touchable)
                    return;
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    commandId,
                    commandPayload));
            });
            if (button.parent == null)
                return;
            button.parent.SetChildIndex(button, button.parent.numChildren - 1);
        }

        private static void ApplyBehaviorCardBackgroundVisual(
            Fgui.GImage background,
            Fgui.GGraph stateOverlay,
            Fgui.GGraph selectionOutline,
            Fgui.GGraph selectionBar,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            float width,
            float height,
            bool showRuntimeState)
        {
            if (background != null)
            {
                background.relations?.ClearAll();
                background.visible = true;
                background.SetXY(0f, 0f);
                background.SetSize(width, height);
                background.color = ResolveBehaviorCardBaseTint(row, showRuntimeState);
            }

            if (stateOverlay != null)
            {
                stateOverlay.relations?.ClearAll();
                stateOverlay.visible = true;
                stateOverlay.SetXY(1f, 1f);
                stateOverlay.SetSize(
                    Mathf.Max(1f, width - 2f),
                    Mathf.Max(1f, height - 2f));
                stateOverlay.DrawRoundRect(
                    stateOverlay.width,
                    stateOverlay.height,
                    ResolveBehaviorLogicCardFill(row, showRuntimeState),
                    BehaviorCardOverlayCornerRadii);
            }

            bool selected = row?.Selected == true;
            if (selectionOutline != null)
            {
                selectionOutline.relations?.ClearAll();
                selectionOutline.visible = selected;
                if (selected)
                {
                    selectionOutline.SetXY(1f, 1f);
                    selectionOutline.SetSize(
                        Mathf.Max(1f, width - 2f),
                        Mathf.Max(1f, height - 2f));
                    selectionOutline.DrawRect(
                        selectionOutline.width,
                        selectionOutline.height,
                        1,
                        new Color32(36, 88, 80, 140),
                        new Color32(0, 0, 0, 0));
                }
            }

            if (selectionBar != null)
            {
                selectionBar.relations?.ClearAll();
                selectionBar.visible = selected;
                if (selected)
                {
                    selectionBar.SetXY(0f, 0f);
                    selectionBar.SetSize(3f, height);
                    selectionBar.color = new Color32(65, 224, 192, 255);
                }
            }
        }

        private static Color32 ResolveBehaviorLogicCardAccent(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            bool showRuntimeState)
        {
            if (row?.Disabled == true)
                return new Color32(78, 101, 112, 255);
            if (row?.Invalid == true)
                return new Color32(240, 161, 74, 255);
            if (showRuntimeState
                && row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow)
                return new Color32(105, 215, 140, 255);
            if (showRuntimeState
                && row?.ExecutionState
                == CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame)
            {
                return new Color32(120, 184, 255, 255);
            }
            if (row?.IsCoreSlot == true)
                return new Color32(238, 177, 55, 255);
            if (row?.Selected == true)
                return new Color32(83, 210, 189, 255);
            return new Color32(78, 101, 112, 255);
        }

        private static Color32 ResolveBehaviorLogicCardFill(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            bool showRuntimeState)
        {
            if (row?.Disabled == true)
                return new Color32(16, 20, 24, 180);
            if (row?.Invalid == true)
                return new Color32(56, 34, 16, 160);
            if (showRuntimeState
                && row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow)
                return new Color32(18, 76, 54, 160);
            if (showRuntimeState
                && row?.ExecutionState
                == CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame)
            {
                return new Color32(20, 56, 88, 150);
            }
            if (row?.IsCoreSlot == true)
                return new Color32(56, 42, 14, 150);
            if (row?.Selected == true)
                return new Color32(20, 56, 60, 180);
            return new Color32(16, 23, 28, 140);
        }

        private static Color32 ResolveBehaviorCardBaseTint(
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            bool showRuntimeState)
        {
            if (!showRuntimeState)
            {
                if (row?.Disabled == true)
                    return new Color32(28, 36, 42, 255);
                if (row?.Invalid == true)
                    return new Color32(60, 42, 24, 255);
                if (row?.Selected == true)
                    return new Color32(24, 44, 50, 255);
                return new Color32(20, 28, 34, 255);
            }
            if (row?.Disabled == true)
                return new Color32(46, 54, 60, 255);
            if (row?.Invalid == true)
                return new Color32(110, 74, 32, 255);
            if (showRuntimeState
                && row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow)
                return new Color32(28, 92, 66, 255);
            if (showRuntimeState
                && row?.ExecutionState
                == CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame)
            {
                return new Color32(32, 70, 102, 255);
            }
            if (row?.IsCoreSlot == true)
                return new Color32(84, 64, 24, 255);
            if (row?.Selected == true)
                return new Color32(28, 56, 64, 255);
            return new Color32(22, 32, 38, 255);
        }

        private static void ConfigureBehaviorLogicCardText(
            Fgui.GTextField text,
            float x,
            float y,
            float width,
            float height,
            int fontSize)
        {
            if (text == null)
                return;
            text.relations?.ClearAll();
            text.visible = true;
            text.SetXY(x, y);
            text.SetSize(width, height);
            text.verticalAlign = Fgui.VertAlignType.Middle;
            text.singleLine = true;
            text.textFormat.size = fontSize;
        }

        private static void RestoreWorkbenchCardText(
            Fgui.GTextField text,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Fgui.AlignType align)
        {
            if (text == null)
                return;
            text.relations?.ClearAll();
            text.visible = true;
            text.SetXY(x, y);
            text.SetSize(width, height);
            text.textFormat.size = fontSize;
            text.align = align;
        }

        private static void SetWorkbenchCardChildVisible(Fgui.GObject child, bool visible)
        {
            if (child != null)
                child.visible = visible;
        }

        private static void SetWorkbenchCardChildGeometry(
            Fgui.GObject child,
            float x,
            float y,
            float width,
            float height)
        {
            if (child == null)
                return;
            child.relations?.ClearAll();
            child.SetXY(x, y);
            child.SetSize(width, height);
        }

    }
}
