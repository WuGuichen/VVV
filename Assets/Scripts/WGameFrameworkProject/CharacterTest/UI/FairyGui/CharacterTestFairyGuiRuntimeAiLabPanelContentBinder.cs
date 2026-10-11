using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns setup-slot, workbench-row and behavior-card header presentation.</summary>
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelContentBinder
    {
        private const string WorkbenchCardToggleButton = "inlineToggleEntry";
        private const string WorkbenchCardDeleteButton = "inlineDeleteEntry";
        private const string WorkbenchCardResponsibilitiesButton = "inlineResponsibilities";
        private const string ResponsibilityMissingWarning = "responsibilityMissingWarning";
        private static readonly float[] BehaviorCardOverlayCornerRadii =
            { 6f, 6f, 6f, 6f };
        private static readonly Color32 BehaviorCardSelectionColor =
            new Color32(65, 224, 192, 255);
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession _rootSession;
        private readonly CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder _panelOverlayBinder;
        private readonly ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface _behaviorCardPreviewSurface;
        private readonly Action<Fgui.GComponent, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>
            _bindBehaviorCardRuntimeControls;
        private readonly Action<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>
            _openResponsibilityLibrary;
        private readonly IMxFairyGuiPopupService _popupService;
        private Fgui.GList _boundWorkbenchList;
        private int _lastSelectedWorkbenchEntryIndex = int.MinValue;
        private CharacterTestRuntimeAiLabWorkbenchRowViewModel[] _visibleWorkbenchRows =
            Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
        private int _visibleWorkbenchRowCount;
        private bool _workbenchListRebuildPending;
        private string _workbenchListRebuildReason = string.Empty;
        private long _workbenchProjectionSignature;
        private bool _hasWorkbenchProjectionSignature;
        private CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? _activeResponsibilityFilter;
        // Kept only so the inactive legacy header helpers continue to compile until their
        // Composer component is physically removed by publication.
        private CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind _expandedResponsibility;
        private string _activeResponsibilityGroupIdentity = string.Empty;
        private Fgui.GComponent _responsibilityEditor;
        private CharacterTestRuntimeAiLabWorkbenchRowViewModel _responsibilityEditorRow;
        private readonly bool[] _pendingResponsibilities = new bool[5];

        internal CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? ActiveResponsibilityFilter =>
            _activeResponsibilityFilter;

        internal CharacterTestFairyGuiRuntimeAiLabPanelContentBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession rootSession,
            CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder panelOverlayBinder,
            ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface behaviorCardPreviewSurface,
            Action<Fgui.GComponent, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>
                bindBehaviorCardRuntimeControls,
            IMxFairyGuiPopupService popupService = null,
            Action<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> openResponsibilityLibrary = null)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _rootSession = rootSession;
            _panelOverlayBinder = panelOverlayBinder;
            _behaviorCardPreviewSurface = behaviorCardPreviewSurface;
            _bindBehaviorCardRuntimeControls = bindBehaviorCardRuntimeControls;
            _popupService = popupService;
            _openResponsibilityLibrary = openResponsibilityLibrary;
        }

        internal void BindWorkbenchRows(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GList list = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList)?.asList;
            if (list == null || viewModel == null)
                return;

            CharacterTestRuntimeAiLabWorkbenchRowViewModel[] rows = viewModel.WorkbenchRows;
            int rowCount = Mathf.Clamp(
                viewModel.WorkbenchRowCount,
                0,
                rows?.Length ?? 0);
            bool compact = viewModel.ShowBehaviorLogic
                || viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            float itemWidth = compact
                ? CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicListWidth
                : 536f;
            float itemHeight = compact ? 68f : 84f;
            int selectedEntryIndex = -1;
            for (int i = 0; i < rowCount; i++)
            {
                if (rows[i]?.Selected != true)
                    continue;
                selectedEntryIndex = rows[i].EntryIndex;
                break;
            }
            if (!string.Equals(
                    _activeResponsibilityGroupIdentity,
                    viewModel.WorkbenchGroupIdentity,
                    StringComparison.Ordinal))
            {
                _activeResponsibilityGroupIdentity = viewModel.WorkbenchGroupIdentity ?? string.Empty;
                _activeResponsibilityFilter = null;
                _workbenchListRebuildPending = true;
                _workbenchListRebuildReason = "group-changed";
            }
            BindResponsibilityTabs(root, viewModel);
            RebuildVisibleWorkbenchRows(rows, rowCount);
            list.layout = Fgui.ListLayoutType.SingleColumn;
            list.selectionMode = Fgui.ListSelectionMode.None;
            list.lineGap = 0;
            string behaviorCardUrl = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabBehaviorCard");
            list.itemProvider = _ => behaviorCardUrl;
            list.itemRenderer = (i, cardObject) =>
            {
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row =
                    _visibleWorkbenchRows != null
                        && i >= 0
                        && i < _visibleWorkbenchRowCount
                            ? _visibleWorkbenchRows[i]
                            : null;
                Fgui.GComponent card = cardObject?.asCom;
                if (card == null)
                    return;
                bool enabled = row?.Visible == true;
                cardObject.data = row;
                cardObject.visible = enabled;
                cardObject.touchable = enabled;
                cardObject.grayed = !enabled;
                cardObject.SetSize(itemWidth, itemHeight);
                cardObject.x = 0f;
                BindText(card, "title", row?.TitleText ?? string.Empty);
                BindText(card, "badgeText", row?.BadgeText ?? string.Empty);
                BindText(card, "triggerText", row?.TriggerText ?? string.Empty);
                BindText(
                    card,
                    "orderText",
                    row?.IsCoreSlot == true
                        ? string.Empty
                        : (i + 1).ToString());
                BindText(card, "actionText", row?.ActionText ?? string.Empty);
                BindText(card, "outcomeText", row?.OutcomeText ?? string.Empty);
                BindText(card, "priorityText", row?.PriorityText ?? string.Empty);
                BindText(card, "disabledText", row?.Disabled == true ? "已禁用" : string.Empty);
                Fgui.GObject dragHandle = card.GetChild("dragHandle");
                Fgui.GTextField title = card.GetChild("title")?.asTextField;
                Fgui.GTextField badge = card.GetChild("badgeText")?.asTextField;
                Fgui.GTextField priority = card.GetChild("priorityText")?.asTextField;
                Fgui.GLoader behaviorIcon = card.GetChild("behaviorIcon")?.asLoader;
                Color32 executionColor = row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow
                    ? CharacterTestFairyGuiRuntimeAiLabSemanticColors.RuntimeActiveText
                    : row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame
                        ? new Color32(120, 184, 255, 255)
                        : new Color32(127, 216, 203, 255);
                if (badge != null)
                    badge.color = row != null && row.Invalid
                        ? new Color32(240, 161, 74, 255)
                        : row != null && row.Disabled
                            ? new Color32(125, 141, 150, 255)
                            : executionColor;
                if (priority != null)
                    priority.color = row != null && row.ExecutionState != CharacterTestRuntimeAiLabExecutionDisplayState.None
                        ? executionColor
                        : new Color32(183, 200, 209, 255);
                if (behaviorIcon != null)
                    behaviorIcon.color = row != null && row.Disabled
                        ? new Color32(108, 123, 130, 255)
                        : new Color32(119, 216, 203, 255);
                Color32 iconAccent = viewModel.ShowBehaviorLogic
                    ? ResolveBehaviorLogicCardAccent(row, showRuntimeState: false)
                    : row?.Invalid == true
                        ? new Color32(240, 161, 74, 255)
                        : row?.ExecutionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow
                            ? new Color32(91, 224, 177, 255)
                            : row?.Disabled == true
                                ? new Color32(108, 123, 130, 255)
                                : row?.IsCoreSlot == true
                                    ? new Color32(231, 170, 48, 255)
                                    : new Color32(72, 174, 230, 255);
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(
                    card,
                    row?.IconKey,
                    compact ? 12f : 82f,
                    compact ? 14f : 12f,
                    compact ? 48f : 60f,
                    iconAccent,
                    row != null && row.Visible);
                ApplyBehaviorLogicWorkbenchCardVisual(card, row, compact);
                BindBehaviorLogicWorkbenchCardActions(card, row, viewModel);
                if (dragHandle != null)
                    dragHandle.visible = false;
                cardObject.draggable = false;
                cardObject.onDragStart.Clear();
                cardObject.onDragMove.Clear();
                cardObject.onDragEnd.Clear();
                if (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                {
                    _rootSession.UnbindTooltip(cardObject);
                    _rootSession.HideTooltip();
                }
                else
                    BindTooltip(cardObject, row?.ExecutionDetailText ?? string.Empty);
            };
            bool listInstanceChanged = !ReferenceEquals(_boundWorkbenchList, list);
            if (listInstanceChanged)
            {
                _boundWorkbenchList = list;
                _lastSelectedWorkbenchEntryIndex = int.MinValue;
                _workbenchListRebuildPending = true;
                _workbenchListRebuildReason = "list-instance-changed";
            }
            if (_workbenchListRebuildPending)
            {
                WriteWorkbenchListSnapshot(
                    list,
                    "rebuild-before",
                    _workbenchListRebuildReason);
                // Replacing heterogeneous responsibility-header/card rows while handling the
                // clicked list item can leave the old display objects in FairyGUI's layout pass.
                // Rebuild on the following bind instead, after the click dispatch has completed.
                list.RemoveChildrenToPool();
            }
            list.numItems = _visibleWorkbenchRowCount;
            list.EnsureBoundsCorrect();
            if (_workbenchListRebuildPending)
            {
                WriteWorkbenchListSnapshot(
                    list,
                    "rebuild-after",
                    _workbenchListRebuildReason);
                _workbenchListRebuildPending = false;
                _workbenchListRebuildReason = string.Empty;
            }
            list.onClickItem.Set(context =>
            {
                Fgui.GObject item = context.data as Fgui.GObject;
                CharacterTestRuntimeAiLabWorkbenchRowViewModel row =
                    item?.data as CharacterTestRuntimeAiLabWorkbenchRowViewModel;
                if (row?.Visible != true)
                    return;
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(
                        row.EntryIndex),
                    null));
                if (context.inputEvent != null && context.inputEvent.isDoubleClick)
                {
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                        null));
                }
            });

            int selectedListIndex = -1;
            for (int i = 0; i < _visibleWorkbenchRowCount; i++)
            {
                if (_visibleWorkbenchRows[i]?.Selected != true)
                    continue;
                selectedListIndex = i;
                selectedEntryIndex = _visibleWorkbenchRows[i].EntryIndex;
                break;
            }
            if (selectedListIndex >= 0
                && selectedEntryIndex != _lastSelectedWorkbenchEntryIndex
                && list.scrollPane != null)
            {
                list.ScrollToView(selectedListIndex);
            }
            _lastSelectedWorkbenchEntryIndex = selectedEntryIndex;
            _workbenchProjectionSignature = ComputeWorkbenchProjectionSignature(viewModel);
            _hasWorkbenchProjectionSignature = true;
        }

        private void BindResponsibilityTabs(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            int total = Mathf.Clamp(viewModel?.WorkbenchRowCount ?? 0, 0, int.MaxValue);
            // The responsibility tabs belong to the shared behavior-card list.  Behavior Logic
            // can keep that list visible even while legacy workspace flags are transitioning,
            // so use the same presentation authority as the final layout instead of leaving
            // visible tabs non-interactive.
            bool visible = viewModel != null && viewModel.ShowBehaviorLogic;
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll,
                "全部", total, null, visible, viewModel);
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting,
                "索敌", CountResponsibility(viewModel, CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting),
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting, visible, viewModel);
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage,
                "接战", CountResponsibility(viewModel, CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure),
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure, visible, viewModel);
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage,
                "输出", CountResponsibility(viewModel, CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage),
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage, visible, viewModel);
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback,
                "兜底", CountResponsibility(viewModel, CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback),
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback, visible, viewModel);
            BindResponsibilityTab(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther,
                "其他", CountResponsibility(viewModel, CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other),
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other, visible, viewModel);
        }

        private void BindResponsibilityTab(
            Fgui.GComponent root,
            string childName,
            string label,
            int count,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? filter,
            bool visible,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GButton button = root?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            bool selected = _activeResponsibilityFilter == filter;
            bool requiredResponsibility = filter.HasValue
                && filter.Value != CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            bool missing = visible && requiredResponsibility && count <= 0;
            button.visible = visible;
            button.enabled = visible;
            button.grayed = false;
            button.title = label + " " + Math.Max(0, count);
            button.titleFontSize = 14;
            button.selected = selected;
            Fgui.GObject baseImage = button.GetChild("base");
            if (baseImage != null) baseImage.visible = false;
            Fgui.GObject overOverlay = button.GetChild("overOverlay");
            if (overOverlay != null) overOverlay.visible = false;
            Fgui.GObject downOverlay = button.GetChild("downOverlay");
            if (downOverlay != null) downOverlay.visible = false;
            Fgui.GObject iconObj = button.GetChild("icon");
            if (iconObj != null) iconObj.visible = false;
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = selected
                    ? new Color32(255, 255, 255, 255)
                    : count > 0
                        ? new Color32(168, 185, 198, 255)
                        : new Color32(95, 108, 118, 255);
            }
            ApplyResponsibilityTabSurface(button, selected, count > 0, missing, label);
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                if (_activeResponsibilityFilter == filter)
                    return;
                _activeResponsibilityFilter = filter;
                _workbenchListRebuildPending = true;
                _workbenchListRebuildReason = "filter-" + (filter?.ToString() ?? "All");
                // Tabs are local UI filters and therefore do not change the ViewModel revision.
                // Rebind immediately so the click has visible feedback without waiting for an
                // unrelated authoring or runtime update.
                BindWorkbenchRows(root, viewModel);
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                    "activeTab=" + (filter?.ToString() ?? "All")
                    + " filteredEntryCount=" + _visibleWorkbenchRowCount);
            });
        }

        private void ApplyResponsibilityTabSurface(
            Fgui.GButton button,
            bool selected,
            bool hasItems,
            bool missing,
            string label)
        {
            Fgui.GGraph surface = button.GetChild(ResponsibilityMissingWarning)?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = ResponsibilityMissingWarning,
                    touchable = false,
                    sortingOrder = 1
                };
                button.AddChildAt(surface, 0);
            }

            surface.visible = true;
            surface.SetXY(0f, 0f);
            surface.SetSize(button.width, button.height);
            float[] radii = new float[] { 4f, 4f, 4f, 4f };
            if (selected)
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(32, 110, 96, 240),
                    radii);
            }
            else if (hasItems)
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(28, 36, 44, 200),
                    radii);
            }
            else
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(22, 28, 34, 140),
                    radii);
            }

            if (missing)
                BindTooltip(button, label + "职责尚未配置（提示：不会阻止保存或测试）");
            else
                _rootSession.UnbindTooltip(button);
        }

        private static int CountResponsibility(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            int[] counts = viewModel?.WorkbenchResponsibilityCounts;
            int index = (int)responsibility;
            return counts != null && index >= 0 && index < counts.Length
                ? Math.Max(0, counts[index])
                : 0;
        }

        private void ShowResponsibilityEditor(
            Fgui.GButton anchor,
            CharacterTestRuntimeAiLabWorkbenchRowViewModel row,
            bool canEdit)
        {
            if (anchor == null || row == null || !canEdit || _popupService == null)
                return;
            EnsureResponsibilityEditor();
            _responsibilityEditorRow = row;
            Array.Clear(_pendingResponsibilities, 0, _pendingResponsibilities.Length);
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] current =
                row.ResponsibilityKinds ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            for (int i = 0; i < current.Length; i++)
            {
                int index = (int)current[i];
                if (index >= 0 && index < _pendingResponsibilities.Length)
                    _pendingResponsibilities[index] = true;
            }
            RefreshResponsibilityEditorButtons();
            _popupService.Toggle(
                _viewId,
                _responsibilityEditor,
                anchor,
                new MxFairyGuiPopupOptions(MxFairyGuiPopupPlacement.Below, 8f, 4f));
        }

        private void EnsureResponsibilityEditor()
        {
            if (_responsibilityEditor != null)
                return;
            _responsibilityEditor = new Fgui.GComponent { name = "behaviorResponsibilityEditor" };
            _responsibilityEditor.SetSize(260f, 286f);
            var background = new Fgui.GGraph { name = "background", touchable = false };
            background.SetSize(260f, 286f);
            background.DrawRect(260f, 286f, 1, new Color32(49, 92, 105, 255), new Color32(5, 25, 32, 250));
            _responsibilityEditor.AddChild(background);

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; i < responsibilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility = responsibilities[i];
                Fgui.GButton option = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "CompactNeutralButton")?.asButton;
                if (option == null)
                    continue;
                option.name = "responsibilityOption" + responsibility;
                option.SetXY(10f, 10f + i * 43f);
                option.SetSize(240f, 37f);
                option.titleFontSize = 15;
                option.onClick.Set(context =>
                {
                    context.StopPropagation();
                    TogglePendingResponsibility(responsibility);
                });
                _responsibilityEditor.AddChild(option);
            }

            Fgui.GButton confirm = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "CompactNeutralButton")?.asButton;
            if (confirm != null)
            {
                confirm.name = "confirm";
                confirm.SetXY(10f, 234f);
                confirm.SetSize(116f, 40f);
                confirm.title = "确定";
                confirm.onClick.Set(context =>
                {
                    context.StopPropagation();
                    CommitPendingResponsibilities();
                });
                _responsibilityEditor.AddChild(confirm);
            }
            Fgui.GButton cancel = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "CompactNeutralButton")?.asButton;
            if (cancel != null)
            {
                cancel.name = "cancel";
                cancel.SetXY(134f, 234f);
                cancel.SetSize(116f, 40f);
                cancel.title = "取消";
                cancel.onClick.Set(context =>
                {
                    context.StopPropagation();
                    _popupService.Hide(_responsibilityEditor, MxFairyGuiPopupCloseReason.Toggle);
                });
                _responsibilityEditor.AddChild(cancel);
            }
        }

        private void TogglePendingResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            if (_responsibilityEditorRow == null)
                return;
            int index = (int)responsibility;
            bool compatible = responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                || (_responsibilityEditorRow.ResponsibilityCompatibilityMask & (1 << index)) != 0;
            if (!compatible)
                return;
            if (responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
            {
                Array.Clear(_pendingResponsibilities, 0, _pendingResponsibilities.Length);
                _pendingResponsibilities[index] = true;
            }
            else
            {
                _pendingResponsibilities[index] = !_pendingResponsibilities[index];
                _pendingResponsibilities[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = false;
                bool anyExplicit = false;
                for (int i = 1; i < _pendingResponsibilities.Length; i++)
                    anyExplicit |= _pendingResponsibilities[i];
                if (!anyExplicit)
                    _pendingResponsibilities[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = true;
            }
            RefreshResponsibilityEditorButtons();
        }

        private void RefreshResponsibilityEditorButtons()
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; _responsibilityEditor != null && i < responsibilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility = responsibilities[i];
                Fgui.GButton option = _responsibilityEditor.GetChild(
                    "responsibilityOption" + responsibility)?.asButton;
                if (option == null)
                    continue;
                int index = (int)responsibility;
                bool compatible = responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                    || (_responsibilityEditorRow?.ResponsibilityCompatibilityMask & (1 << index)) != 0;
                option.touchable = compatible;
                option.grayed = !compatible;
                option.title = (_pendingResponsibilities[index] ? "✓ " : "  ")
                    + CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(responsibility);
                BindTooltip(option, compatible ? string.Empty : "该行为不提供此职责需要的能力。");
            }
        }

        private void CommitPendingResponsibilities()
        {
            if (_responsibilityEditorRow == null)
                return;
            var builder = new StringBuilder();
            for (int i = 0; i < _pendingResponsibilities.Length; i++)
            {
                if (!_pendingResponsibilities[i])
                    continue;
                if (builder.Length > 0)
                    builder.Append(',');
                builder.Append((CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind)i);
            }
            if (builder.Length == 0)
                builder.Append(CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other);
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetEntryResponsibilities,
                _responsibilityEditorRow.EntryIndex + "|" + builder));
            _popupService.Hide(_responsibilityEditor, MxFairyGuiPopupCloseReason.Toggle);
        }

    }
}
