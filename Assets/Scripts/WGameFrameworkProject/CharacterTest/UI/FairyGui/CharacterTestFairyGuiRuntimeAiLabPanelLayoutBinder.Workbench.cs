using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder
    {
        private static void ApplyWorkbenchLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool visible = viewModel != null
                && (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                    || viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            bool card = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool group = visible
                && !card
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            string[] controls =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack,
                WorkbenchListPanel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
                WorkbenchCountBadge,
                WorkbenchDetailSurface,
                WorkbenchDetailIconBackground,
                WorkbenchDetailIcon,
                WorkbenchDiagnosticBackground,
                WorkbenchDiagnosticText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                WorkbenchEditButton,
                WorkbenchMoreButton,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup
            };
            for (int i = 0; i < controls.Length; i++)
                SetObjectVisible(root, controls[i], visible);
            string[] responsibilityTabs =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther
            };
            bool showResponsibilityTabs = viewModel != null && viewModel.ShowBehaviorLogic;
            for (int i = 0; i < responsibilityTabs.Length; i++)
                SetObjectVisible(root, responsibilityTabs[i], showResponsibilityTabs);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground,
                card);
            SetObjectVisible(root, WorkbenchCountBadge, false);
            if (!visible)
                return;
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close,
                !card);
            Fgui.GObject behaviorCardBackground = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground);
            if (behaviorCardBackground != null)
            {
                SetObjectGeometry(behaviorCardBackground, 0f, 0f, 1920f, 1080f);
                behaviorCardBackground.sortingOrder = -10;
            }
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle, visible);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack, true);
            SetObjectGeometry(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack),
                32f,
                1028f,
                112f,
                36f);
            CenterButtonContent(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack)?.asButton,
                8f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle, 648f, 36f);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle, 168f);
            Fgui.GTextField workbenchTitle = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle)?.asTextField;
            if (workbenchTitle != null)
                workbenchTitle.verticalAlign = Fgui.VertAlignType.Middle;
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb, 1500f);
            SetObjectGeometry(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText),
                36f,
                72f,
                400f,
                28f);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
                group ? "已装配行为卡" : "行为步骤");
            SetObjectGeometry(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText),
                656f,
                124f,
                1160f,
                64f);
            SetObjectGeometry(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar),
                600f,
                896f,
                1296f,
                96f);
            if (group)
            {
                Fgui.GObject workspacePanel = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel);
                if (workspacePanel != null)
                    workspacePanel.sortingOrder = -5;
                Fgui.GObject workspaceTitle = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle);
                if (workspaceTitle != null)
                    workspaceTitle.sortingOrder = 1;
                Fgui.GObject workspaceBreadcrumb = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb);
                if (workspaceBreadcrumb != null)
                    workspaceBreadcrumb.sortingOrder = 1;
                string[] groupControls =
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
                    WorkbenchCountBadge,
                    WorkbenchDetailIconBackground,
                    WorkbenchDetailIcon,
                    WorkbenchDiagnosticBackground,
                    WorkbenchDiagnosticText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                    WorkbenchEditButton,
                    WorkbenchMoreButton,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup
                };
                for (int i = 0; i < groupControls.Length; i++)
                    ClearObjectRelations(root, groupControls[i]);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel),
                    600f,
                    24f,
                    1296f,
                    968f);
                Fgui.GObject listPanel = root.GetChild(WorkbenchListPanel);
                if (listPanel != null)
                {
                    SetObjectGeometry(listPanel, 24f, 24f, 552f, 968f);
                    listPanel.sortingOrder = -5;
                }
                Fgui.GObject detailSurface = root.GetChild(WorkbenchDetailSurface);
                if (detailSurface != null)
                {
                    SetObjectGeometry(detailSurface, 632f, 96f, 1232f, 104f);
                    detailSurface.sortingOrder = -4;
                }
                Fgui.GObject workbenchList = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList);
                if (workbenchList != null)
                {
                    SetObjectGeometry(workbenchList, 32f, 148f, 536f, 830f);
                    workbenchList.sortingOrder = 1;
                }
                SetObjectGeometry(root.GetChild(WorkbenchDetailIconBackground), 0f, 0f, 1f, 1f);
                SetObjectGeometry(root.GetChild(WorkbenchDetailIcon), 0f, 0f, 1f, 1f);
                SetObjectGeometry(root.GetChild(WorkbenchDiagnosticBackground), 648f, 212f, 1192f, 52f);
                SetObjectGeometry(root.GetChild(WorkbenchDiagnosticText), 672f, 223f, 1144f, 30f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary),
                    632f,
                    350f,
                    1232f,
                    530f);
                Fgui.GTextField detail = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText)?.asTextField;
                if (detail != null)
                {
                    detail.sortingOrder = 2;
                    detail.autoSize = Fgui.AutoSizeType.None;
                    detail.align = Fgui.AlignType.Left;
                    detail.verticalAlign = Fgui.VertAlignType.Top;
                    detail.singleLine = false;
                }
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry),
                    648f,
                    280f,
                    112f,
                    52f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp),
                    0f,
                    0f,
                    1f,
                    1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown),
                    0f,
                    0f,
                    1f,
                    1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry),
                    772f,
                    280f,
                    112f,
                    52f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry),
                    896f,
                    280f,
                    160f,
                    52f);
                SetObjectGeometry(root.GetChild(WorkbenchMoreButton), 0f, 0f, 1f, 1f);
                SetObjectGeometry(root.GetChild(WorkbenchEditButton), 1068f, 280f, 220f, 52f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry)?.asButton,
                    8f);
                CenterButtonContent(root.GetChild(WorkbenchEditButton)?.asButton, 8f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText),
                    624f,
                    912f,
                    596f,
                    56f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText),
                    0f,
                    0f,
                    1f,
                    1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText),
                    1236f,
                    930f,
                    220f,
                    20f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft),
                    1468f,
                    912f,
                    152f,
                    54f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft),
                    1636f,
                    912f,
                    228f,
                    54f);
            }
            else if (card)
            {
                Fgui.GObject listPanel = root.GetChild(WorkbenchListPanel);
                if (listPanel != null)
                {
                    SetObjectGeometry(listPanel, 1328f, 76f, 568f, 438f);
                    listPanel.sortingOrder = -4;
                }
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText),
                    1352f,
                    88f,
                    520f,
                    34f);
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
                    "测试组合");
                Fgui.GObject workbenchList = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList);
                if (workbenchList != null)
                {
                    SetObjectGeometry(workbenchList, 1344f, 128f, 536f, 240f);
                    workbenchList.sortingOrder = 1;
                }
                SetObjectGeometry(
                    root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader),
                    24f,
                    76f,
                    1280f,
                    438f);
                ClearObjectRelations(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline);
                SetObjectGeometry(
                    root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline),
                    24f,
                    668f,
                    1872f,
                    412f);
                // Keep project-card metadata editable, but place it inside the
                // otherwise unused right side of the behavior editor instead of
                // covering the test-composition panel.
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataPanel),
                    1392f, 630f, 488f, 410f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataTitle),
                    1412f, 644f, 448f, 32f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDisplayName),
                    1412f, 686f, 324f, 44f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardCost),
                    1748f, 686f, 112f, 44f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardTags),
                    1412f, 742f, 448f, 44f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDescription),
                    1412f, 798f, 448f, 52f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardUseSummary),
                    1412f, 862f, 448f, 72f);
                SetObjectGeometry(root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardRiskSummary),
                    1412f, 946f, 448f, 72f);
                // Draft actions belong to the persistent shell while authoring. Keep
                // them out of the full-height timeline canvas so routes and cards can
                // use the complete lower workspace.
                ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft);
                ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft);
                ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft);
                ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup);
                ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft),
                    0f,
                    0f,
                    1f,
                    1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack),
                    24f,
                    14f,
                    96f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start),
                    1040f,
                    14f,
                    156f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft),
                    1208f,
                    14f,
                    210f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft),
                    1430f,
                    14f,
                    190f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry),
                    1344f, 424f, 130f, 44f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp),
                    0f, 0f, 1f, 1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown),
                    0f, 0f, 1f, 1f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry),
                    1486f, 424f, 130f, 44f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry),
                    1628f, 424f, 130f, 44f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry)?.asButton,
                    8f);
                CenterButtonContent(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry)?.asButton,
                    8f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle),
                    140f, 16f, 720f, 38f);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel currentBehavior = null;
                for (int i = 0; viewModel.WorkbenchRows != null
                    && i < viewModel.WorkbenchRows.Length; i++)
                {
                    if (viewModel.WorkbenchRows[i]?.IsCurrentBehavior == true)
                    {
                        currentBehavior = viewModel.WorkbenchRows[i];
                        break;
                    }
                }
                Fgui.GTextField behaviorTitle = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle)?.asTextField;
                if (behaviorTitle != null)
                {
                    behaviorTitle.textFormat.size = 26;
                    behaviorTitle.textFormat.bold = true;
                    behaviorTitle.color = new Color32(243, 238, 220, 255);
                    behaviorTitle.align = Fgui.AlignType.Left;
                    behaviorTitle.verticalAlign = Fgui.VertAlignType.Middle;
                    behaviorTitle.sortingOrder = 50;
                }
                BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                    currentBehavior?.TitleText ?? "当前行为");
                string visibleBehaviorTitle = !string.IsNullOrWhiteSpace(currentBehavior?.TitleText)
                    ? currentBehavior.TitleText
                    : !string.IsNullOrWhiteSpace(currentBehavior?.Label)
                        ? currentBehavior.Label
                        : "当前行为";
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                    visibleBehaviorTitle);
            }
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
                visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
                visible && !card);
            SetObjectVisible(
                root,
                WorkbenchCountBadge,
                false);
            SetObjectVisible(root, WorkbenchDetailSurface, group);
            SetObjectVisible(root, WorkbenchDetailIconBackground, false);
            SetObjectVisible(root, WorkbenchDetailIcon, false);
            bool showWorkbenchDiagnostic = group
                && !string.IsNullOrWhiteSpace(viewModel.WorkbenchDiagnosticText);
            SetObjectVisible(root, WorkbenchDiagnosticBackground, showWorkbenchDiagnostic);
            SetObjectVisible(root, WorkbenchDiagnosticText, showWorkbenchDiagnostic);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground,
                card);
            SetObjectVisible(root, WorkbenchListPanel, visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList,
                visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader,
                visible && card);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary,
                group);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline,
                visible && card);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
                card);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
                visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown,
                false);
            SetObjectVisible(root, WorkbenchEditButton, false);
            SetObjectVisible(root, WorkbenchMoreButton, false);
            SetObjectVisible(root, "workspaceDivider", !visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
                visible && !card);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
                visible && !card);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
                visible && !card);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft, visible);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft, visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
                visible && group);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft, visible);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
                false);
            if (card)
            {
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReloadProjectDraft, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReapplyProjectDraft, false);
                SetObjectVisible(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
                    false);
            }
        }

    }
}
