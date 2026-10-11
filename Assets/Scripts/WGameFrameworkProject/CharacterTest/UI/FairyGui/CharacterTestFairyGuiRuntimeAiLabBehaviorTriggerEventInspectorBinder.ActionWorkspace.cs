using System;
using System.Collections.Generic;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private const float WorkspaceTrackLabelWidth = 200f;
        private const float WorkspaceTrackRowHeight = 54f;
        private const float WorkspaceResizeHandleHitWidth = 16f;
        private const float WorkspaceResizeHandleLineWidth = 4f;
        private const float WorkspacePageToolbarY = 570f;
        private const float WorkspacePageTimelineY = 640f;
        private const float WorkspaceSourceMinimumWidth = 260f;
        private const float WorkspaceSourceMaximumWidth = 320f;
        private const float WorkspaceInspectorMinimumWidth = 420f;
        private const float WorkspaceInspectorMaximumWidth = 640f;
        private Fgui.GComponent _characterActionWorkspace;
        private Fgui.GComponent _characterActionWorkspaceCloseConfirm;
        private Fgui.GComponent _characterActionWorkspaceAddMenu;
        private Fgui.GComponent _characterActionWorkspaceFieldChoiceMenu;
        private CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot _workspaceSnapshot;
        private string _workspaceQuery = string.Empty;
        private readonly List<int> _workspaceFilteredActions = new List<int>();
        private readonly List<int> _workspaceFilteredProblems = new List<int>();
        private readonly List<int> _workspaceFilteredInspector = new List<int>();
        private float _workspacePixelsPerFrame;
        private float _workspaceTimelineWidth = 700f;
        private string _workspaceProblemFilter = "all";
        private string _workspaceInspectorGroup = "basic";
        private string _workspaceInspectorSelectionId = string.Empty;
        private readonly Dictionary<Fgui.GObject, bool> _workspacePageVisibility =
            new Dictionary<Fgui.GObject, bool>();

        private void ShowEmbeddedCharacterActionWorkspace()
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot snapshot =
                sink?.CharacterActionWorkspace;
            if (snapshot?.Available != true)
                return;
            _popupService?.HideOwner(
                _viewId,
                MxFairyGuiPopupCloseReason.OwnerClosed);
            Fgui.GComponent root = ResolveCharacterActionWorkspacePageRoot();
            if (root == null)
                return;
            if (_characterActionWorkspace != null
                && !IsCharacterActionWorkspaceReusableForRoot(
                    _characterActionWorkspace,
                    root))
            {
                ResetEmbeddedCharacterActionWorkspaceReferences();
            }
            EnsureEmbeddedCharacterActionWorkspace();
            if (_characterActionWorkspace == null)
                return;
            _workspaceSnapshot = snapshot;
            _characterActionWorkspace.SetXY(0f, 0f);
            _characterActionWorkspace.SetSize(root.width, root.height);
            _characterActionWorkspace.opaque = false;
            if (!ReferenceEquals(_characterActionWorkspace.parent, root))
            {
                _characterActionWorkspace.RemoveFromParent();
                root.AddChild(_characterActionWorkspace);
            }
            else
                root.SetChildIndex(_characterActionWorkspace, root.numChildren - 1);
            ConfigureCharacterActionWorkspacePageLayout(root.width, root.height);
            SetCharacterActionWorkspacePageObstructionsHidden(root, hidden: true);
            _characterActionWorkspace.sortingOrder = 500;
            BindEmbeddedCharacterActionWorkspace();
        }

        private void EnsureEmbeddedCharacterActionWorkspace()
        {
            if (_characterActionWorkspace != null
                && _characterActionWorkspace.displayObject != null
                && !_characterActionWorkspace.displayObject.isDisposed)
                return;
            ResetEmbeddedCharacterActionWorkspaceReferences();
            _characterActionWorkspace = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabCharacterActionWorkspace")?.asCom;
            if (_characterActionWorkspace == null)
                return;

            BindWorkspaceButton("btnBack", RequestCloseEmbeddedWorkspace);
            BindWorkspaceButton("btnUndo", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.Undo));
            BindWorkspaceButton("btnRedo", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.Redo));
            BindWorkspaceButton("btnValidate", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.Validate));
            BindWorkspaceButton("btnSave", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SaveAll));
            BindWorkspaceButton("btnCreateAction", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.CreateAction,
                text: "new_action"));
            BindWorkspaceButton("btnDuplicateAction", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.DuplicateAction));
            BindWorkspaceButton("btnDeleteAction", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.DeleteAction));
            BindWorkspaceButton("btnDeleteItem", () => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.DeleteTimelineItem,
                _workspaceSnapshot?.SelectedTimelineItemId));
            BindWorkspaceButton("btnPreviewPlay", () => ExecuteWorkspacePreview(
                _workspaceSnapshot?.PreviewPlaying == true
                    ? CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPause
                    : CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPlay,
                _workspaceSnapshot?.PreviewFrame ?? 0));
            BindWorkspaceButton("btnPreviewStep", () => ExecuteWorkspacePreview(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStep,
                Math.Min(
                    Math.Max(0, (_workspaceSnapshot?.DurationFrames ?? 1) - 1),
                    (_workspaceSnapshot?.PreviewFrame ?? 0) + 1)));
            BindWorkspaceButton("btnPreviewStop", () => ExecuteWorkspacePreview(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStop,
                0));
            BindWorkspaceButton("btnAddPhase", ToggleWorkspaceAddMenu);
            BindWorkspaceButton("btnAddTransition", () => AddWorkspaceTrackItem("TransitionWindow"));
            BindWorkspaceButton("btnAddMotion", () => AddWorkspaceTrackItem("Motion"));
            BindWorkspaceButton("btnAddAnimation", () => AddWorkspaceTrackItem("Animation"));
            BindWorkspaceButton("btnAddCombat", () => AddWorkspaceTrackItem("Combat"));
            BindWorkspaceButton("btnAddGameplay", () => AddWorkspaceTrackItem("Gameplay"));
            BindWorkspaceButton("btnAddPresentation", () => AddWorkspaceTrackItem("Presentation"));
            BindWorkspaceButton("btnZoomOut", () => AdjustWorkspaceZoom(-1f));
            BindWorkspaceButton("btnZoomIn", () => AdjustWorkspaceZoom(1f));
            BindWorkspaceButton("btnFit", FitWorkspaceTimeline);
            BindWorkspaceButton("btnProblemsAll", () => SetWorkspaceInspectorGroup("basic"));
            BindWorkspaceButton("btnProblemsAction", () => SetWorkspaceInspectorGroup("timing"));
            BindWorkspaceButton("btnProblemsSource", () => SetWorkspaceInspectorGroup("requirements"));
            BindWorkspaceButton("btnProblemsReferences", () => SetWorkspaceInspectorGroup("selected"));

            Fgui.GTextInput query = _characterActionWorkspace.GetChild("actionQuery")?.asTextInput;
            if (query != null)
            {
                query.promptText = "搜索动作名称、ID、分类或 Library";
                query.textFormat.size = 18;
                query.onChanged.Set(() =>
                {
                    _workspaceQuery = query.text ?? string.Empty;
                    RebuildWorkspaceActionFilter();
                    Fgui.GList list = _characterActionWorkspace.GetChild("actionList")?.asList;
                    if (list != null)
                        list.numItems = _workspaceFilteredActions.Count;
                });
            }
            ConfigureWorkspaceList("actionList", BindWorkspaceActionRow);
            ConfigureWorkspaceList("trackList", BindWorkspaceTrackRow);
            ConfigureWorkspaceList("inspectorList", BindWorkspaceInspectorRow);
            ConfigureWorkspaceList("problemsList", BindWorkspaceProblemRow);
        }

        internal static bool IsCharacterActionWorkspaceReusableForRoot(
            Fgui.GComponent workspace,
            Fgui.GComponent root)
        {
            return workspace != null
                && root != null
                && !workspace.isDisposed
                && workspace.displayObject != null
                && !workspace.displayObject.isDisposed
                && (workspace.parent == null || ReferenceEquals(workspace.parent, root));
        }

        private void ResetEmbeddedCharacterActionWorkspaceReferences()
        {
            _characterActionWorkspace = null;
            _characterActionWorkspaceCloseConfirm = null;
            _characterActionWorkspaceAddMenu = null;
            _characterActionWorkspaceFieldChoiceMenu = null;
            _workspacePageVisibility.Clear();
        }

        private void BindEmbeddedCharacterActionWorkspace()
        {
            if (_characterActionWorkspace == null || _workspaceSnapshot == null)
                return;
            Fgui.GComponent page = ResolveCharacterActionWorkspacePageRoot();
            if (page != null)
                ConfigureCharacterActionWorkspacePageLayout(page.width, page.height);
            SetCharacterActionWorkspacePageObstructionsHidden(page, hidden: true);
            _characterActionWorkspace.sortingOrder = 500;
            SetWorkspaceText("title", "动作时间轴 · "
                + (_workspaceSnapshot.SelectedActionId > 0
                    ? "#" + _workspaceSnapshot.SelectedActionId
                    : "未选择"));
            Fgui.GTextField title = _characterActionWorkspace.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.textFormat.size = 22;
                title.textFormat.bold = true;
                title.textFormat.color = new Color32(242, 248, 252, 255);
            }
            SetWorkspaceText(
                "evidence",
                _workspaceSnapshot.ProfileId + " · "
                + _workspaceSnapshot.ActionSetId + " · "
                + _workspaceSnapshot.LibraryId);
            Fgui.GTextField evidence = _characterActionWorkspace.GetChild("evidence")?.asTextField;
            if (evidence != null)
            {
                evidence.textFormat.size = 18;
                evidence.textFormat.color = new Color32(140, 162, 178, 255);
            }
            SetWorkspaceText("sourceContextTitle", "来源上下文");
            Fgui.GTextField srcTitle = _characterActionWorkspace.GetChild("sourceContextTitle")?.asTextField;
            if (srcTitle != null)
            {
                srcTitle.textFormat.size = 18;
                srcTitle.textFormat.bold = true;
                srcTitle.textFormat.color = new Color32(220, 230, 238, 255);
            }
            SetWorkspaceText(
                "sourceContextCard",
                string.IsNullOrWhiteSpace(_model?.TimelineTitle)
                    ? "当前行为逻辑"
                    : _model.TimelineTitle);
            Fgui.GTextField srcCard = _characterActionWorkspace.GetChild("sourceContextCard")?.asTextField;
            if (srcCard != null)
            {
                srcCard.textFormat.size = 20;
                srcCard.textFormat.bold = true;
                srcCard.textFormat.color = new Color32(92, 218, 226, 255);
            }
            SetWorkspaceText(
                "sourceContextRoute",
                "Timeline  " + (_model?.TimelineId ?? string.Empty)
                + "\nEvent     " + (_model?.EventId ?? string.Empty)
                + "\n动作      #" + _workspaceSnapshot.SelectedActionId);
            Fgui.GTextField srcRoute = _characterActionWorkspace.GetChild("sourceContextRoute")?.asTextField;
            if (srcRoute != null)
            {
                srcRoute.textFormat.size = 18;
                srcRoute.textFormat.color = new Color32(170, 188, 202, 255);
            }
            SetWorkspaceText(
                "sourceContextHint",
                "当前只编辑动作正文\n返回后仍定位到原事件");
            Fgui.GTextField srcHint = _characterActionWorkspace.GetChild("sourceContextHint")?.asTextField;
            if (srcHint != null)
            {
                srcHint.textFormat.size = 18;
                srcHint.textFormat.color = new Color32(130, 152, 168, 255);
            }
            Fgui.GTextField inspectorTitle = _characterActionWorkspace.GetChild("inspectorTitle")?.asTextField;
            if (inspectorTitle != null)
            {
                inspectorTitle.text = "属性检查器";
                inspectorTitle.textFormat.size = 22;
                inspectorTitle.textFormat.bold = true;
                inspectorTitle.textFormat.color = new Color32(245, 250, 255, 255);
            }
            Fgui.GTextField trackToolbarText = _characterActionWorkspace.GetChild("trackToolbarText")?.asTextField;
            if (trackToolbarText != null)
            {
                trackToolbarText.text = "时间轴轨道";
                trackToolbarText.textFormat.size = 18;
                trackToolbarText.textFormat.bold = true;
                trackToolbarText.textFormat.color = new Color32(235, 245, 252, 255);
            }
            SetWorkspaceText("status", _workspaceSnapshot.Status);
            Fgui.GButton back = _characterActionWorkspace.GetChild("btnBack")?.asButton;
            if (back != null)
            {
                back.title = "← 返回事件";
                back.titleFontSize = 18;
            }
            Fgui.GButton previewPlay = _characterActionWorkspace.GetChild("btnPreviewPlay")?.asButton;
            if (previewPlay != null)
            {
                previewPlay.title = _workspaceSnapshot.PreviewPlaying ? "暂停" : "播放";
                previewPlay.titleFontSize = 18;
            }
            SetWorkspaceText(
                "previewFrame",
                _workspaceSnapshot.PreviewFrame + " / "
                + Math.Max(1, _workspaceSnapshot.DurationFrames) + "f"
                + (_workspaceSnapshot.PreviewActive ? " · Preview" : string.Empty));
            SetWorkspaceButtonEnabled("btnUndo", _workspaceSnapshot.CanUndo);
            SetWorkspaceButtonEnabled("btnRedo", _workspaceSnapshot.CanRedo);
            SetWorkspaceButtonEnabled("btnSave", _workspaceSnapshot.Dirty && !_workspaceSnapshot.HasErrors);
            SetWorkspaceButtonEnabled("btnDeleteItem", !_workspaceSnapshot.ReadOnly
                && !string.IsNullOrEmpty(_workspaceSnapshot.SelectedTimelineItemId));
            string[] editButtons =
            {
                "btnCreateAction", "btnDuplicateAction", "btnDeleteAction", "btnAddPhase",
                "btnAddTransition", "btnAddMotion", "btnAddAnimation", "btnAddCombat",
                "btnAddGameplay", "btnAddPresentation"
            };
            for (int i = 0; i < editButtons.Length; i++)
                SetWorkspaceButtonEnabled(editButtons[i], !_workspaceSnapshot.ReadOnly);

            RebuildWorkspaceActionFilter();
            SetWorkspaceListCount("actionList", _workspaceFilteredActions.Count);
            SetWorkspaceListCount("trackList", _workspaceSnapshot.Tracks.Length + 1);
            SynchronizeWorkspaceInspectorGroup();
            RebuildWorkspaceInspectorFilter();
            SetWorkspaceListCount("inspectorList", _workspaceFilteredInspector.Count);
            BindWorkspaceInspectorGroupButtons();
            RebuildWorkspaceProblemFilter();
            SetWorkspaceListCount("problemsList", _workspaceFilteredProblems.Count);
        }

        private void RebuildWorkspaceActionFilter()
        {
            _workspaceFilteredActions.Clear();
            string query = (_workspaceQuery ?? string.Empty).Trim();
            CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow[] rows =
                _workspaceSnapshot?.Actions ?? Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow>();
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow row = rows[i];
                string haystack = row.DisplayName + " " + row.ActionId + " " + row.Category + " " + row.LibraryId;
                if (query.Length == 0
                    || haystack.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    _workspaceFilteredActions.Add(i);
                }
            }
        }

        private void BindWorkspaceActionRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            if (item == null || index < 0 || index >= _workspaceFilteredActions.Count)
                return;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow row =
                _workspaceSnapshot.Actions[_workspaceFilteredActions[index]];
            SetChildText(item, "title", row.DisplayName);
            Fgui.GTextField title = item.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.textFormat.size = 20;
                title.textFormat.bold = true;
                title.textFormat.color = new Color32(240, 246, 252, 255);
            }
            SetChildText(item, "meta", row.LibraryId + " · " + row.ActionId + " · " + row.DurationFrames + "f");
            Fgui.GTextField meta = item.GetChild("meta")?.asTextField;
            if (meta != null)
            {
                meta.textFormat.size = 18;
                meta.textFormat.color = new Color32(150, 172, 188, 255);
            }
            SetChildText(item, "badge", row.ProblemCount > 0
                ? row.ProblemCount + " !"
                : row.Dirty ? "已修改" : string.Empty);
            Fgui.GTextField badge = item.GetChild("badge")?.asTextField;
            if (badge != null)
            {
                badge.textFormat.size = 18;
                badge.textFormat.bold = true;
            }
            Fgui.GObject selected = item.GetChild("selected");
            if (selected != null)
                selected.visible = row.Selected;
            item.onClick.Set(() => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SelectAction,
                intValue: row.ActionId));
        }

        private void BindWorkspaceTrackRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            if (item == null || _workspaceSnapshot == null)
                return;

            // Safely clean up previous dynamic children while preserving fixed children (background & label)
            for (int i = item.numChildren - 1; i >= 0; i--)
            {
                Fgui.GObject child = item.GetChildAt(i);
                if (child.name != "background" && child.name != "label")
                {
                    item.RemoveChildAt(i, true);
                }
            }

            // Ensure label exists in case it was ever removed or disposed
            Fgui.GTextField label = item.GetChild("label")?.asTextField;
            if (label == null)
            {
                label = new Fgui.GTextField { name = "label" };
                item.AddChild(label);
            }

            float pixelsPerFrame = _workspacePixelsPerFrame > 0f
                ? _workspacePixelsPerFrame
                : Mathf.Clamp(
                    _workspaceTimelineWidth / Math.Max(1, _workspaceSnapshot.DurationFrames),
                    4f,
                    18f);
            float totalContentWidth = WorkspaceTrackLabelWidth
                + Math.Max(_workspaceTimelineWidth, _workspaceSnapshot.DurationFrames * pixelsPerFrame)
                + 36f;

            if (index == 0)
            {
                BindWorkspaceRulerRow(item, totalContentWidth, pixelsPerFrame);
                return;
            }

            int trackIndex = index - 1;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack[] tracks =
                _workspaceSnapshot.Tracks ?? Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack>();
            if (trackIndex < 0 || trackIndex >= tracks.Length)
                return;

            CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack track = tracks[trackIndex];
            item.SetSize(totalContentWidth, WorkspaceTrackRowHeight);

            Fgui.GGraph bg = item.GetChild("background")?.asGraph;
            if (bg != null)
            {
                bg.SetSize(totalContentWidth, WorkspaceTrackRowHeight);
                Color32 rowBgColor = (trackIndex % 2 == 0)
                    ? new Color32(10, 18, 25, 230)
                    : new Color32(13, 22, 31, 230);
                bg.DrawRect(totalContentWidth, WorkspaceTrackRowHeight, 0, Color.clear, rowBgColor);
            }

            Color32 trackThemeColor = ResolveTrackThemeColor(track.Kind, track.Label);
            DrawTrackLabelBadge(item, track, trackThemeColor);
            DrawTrackVerticalGrid(item, _workspaceSnapshot.DurationFrames, pixelsPerFrame);

            for (int i = 0; i < track.Items.Length; i++)
                AddWorkspaceTimelineItem(item, track.Items[i], pixelsPerFrame, trackThemeColor);

            if (_workspaceSnapshot.PreviewActive && _workspaceSnapshot.PreviewFrame >= 0)
            {
                float playheadX = WorkspaceTrackLabelWidth + _workspaceSnapshot.PreviewFrame * pixelsPerFrame;
                var playheadLine = new Fgui.GGraph { name = "playheadLine", touchable = false };
                playheadLine.SetXY(playheadX, 0f);
                playheadLine.SetSize(2f, WorkspaceTrackRowHeight);
                playheadLine.DrawRect(2f, WorkspaceTrackRowHeight, 0, Color.clear, new Color32(92, 218, 226, 180));
                item.AddChild(playheadLine);
            }
        }

        private void BindWorkspaceRulerRow(
            Fgui.GComponent item,
            float totalContentWidth,
            float pixelsPerFrame)
        {
            float rulerHeight = 36f;
            item.SetSize(totalContentWidth, rulerHeight);

            Fgui.GGraph bg = item.GetChild("background")?.asGraph;
            if (bg != null)
            {
                bg.SetSize(totalContentWidth, rulerHeight);
                bg.DrawRect(
                    totalContentWidth,
                    rulerHeight,
                    1,
                    new Color32(32, 48, 64, 255),
                    new Color32(8, 16, 24, 255));
            }

            Fgui.GTextField label = item.GetChild("label")?.asTextField;
            if (label == null)
            {
                label = new Fgui.GTextField { name = "label" };
                item.AddChild(label);
            }
            label.text = "时间轴 · 帧";
            label.SetXY(12f, 7f);
            label.SetSize(WorkspaceTrackLabelWidth - 24f, 22f);
            label.textFormat.size = 14;
            label.textFormat.bold = true;
            label.textFormat.color = new Color32(115, 150, 168, 255);
            item.SetChildIndex(label, item.numChildren - 1);

            int durationFrames = Math.Max(1, _workspaceSnapshot.DurationFrames);
            int majorInterval = durationFrames <= 50 ? 5 : durationFrames <= 120 ? 10 : durationFrames <= 240 ? 20 : 30;
            int minorInterval = majorInterval >= 10 ? majorInterval / 2 : 1;

            if (pixelsPerFrame >= 5f || minorInterval > 1)
            {
                for (int frame = 0; frame <= durationFrames; frame += minorInterval)
                {
                    if (frame % majorInterval == 0)
                        continue;
                    float x = WorkspaceTrackLabelWidth + frame * pixelsPerFrame;
                    var minorTick = new Fgui.GGraph { touchable = false };
                    minorTick.SetXY(x, rulerHeight - 8f);
                    minorTick.SetSize(1f, 8f);
                    minorTick.DrawRect(1f, 8f, 0, Color.clear, new Color32(55, 88, 106, 140));
                    item.AddChild(minorTick);
                }
            }

            for (int frame = 0; frame <= durationFrames; frame += majorInterval)
            {
                float x = WorkspaceTrackLabelWidth + frame * pixelsPerFrame;
                var tick = new Fgui.GGraph { touchable = false };
                tick.SetXY(x, rulerHeight - 16f);
                tick.SetSize(1f, 16f);
                tick.DrawRect(1f, 16f, 0, Color.clear, new Color32(90, 145, 170, 220));
                item.AddChild(tick);

                var numText = new Fgui.GTextField
                {
                    touchable = false,
                    singleLine = true,
                    text = frame.ToString(),
                    color = new Color32(160, 192, 208, 255),
                    align = Fgui.AlignType.Center,
                };
                numText.SetXY(x - 22f, 2f);
                numText.SetSize(44f, 18f);
                numText.textFormat.size = 13;
                numText.textFormat.bold = true;
                item.AddChild(numText);
            }

            float endX = WorkspaceTrackLabelWidth + durationFrames * pixelsPerFrame;
            var endTick = new Fgui.GGraph { touchable = false };
            endTick.SetXY(endX, 0f);
            endTick.SetSize(2f, rulerHeight);
            endTick.DrawRect(2f, rulerHeight, 0, Color.clear, new Color32(235, 110, 80, 230));
            item.AddChild(endTick);

            var endLabel = new Fgui.GTextField
            {
                touchable = false,
                singleLine = true,
                text = durationFrames + "f 结束",
                color = new Color32(245, 140, 110, 255),
                align = Fgui.AlignType.Left,
            };
            endLabel.SetXY(endX + 6f, 8f);
            endLabel.SetSize(76f, 20f);
            endLabel.textFormat.size = 12;
            endLabel.textFormat.bold = true;
            item.AddChild(endLabel);

            if (_workspaceSnapshot.PreviewActive && _workspaceSnapshot.PreviewFrame >= 0)
            {
                float playX = WorkspaceTrackLabelWidth + _workspaceSnapshot.PreviewFrame * pixelsPerFrame;
                var needle = new Fgui.GGraph { touchable = false };
                needle.SetXY(playX - 1f, 0f);
                needle.SetSize(3f, rulerHeight);
                needle.DrawRect(3f, rulerHeight, 0, Color.clear, new Color32(92, 218, 226, 255));
                item.AddChild(needle);

                var badge = new Fgui.GTextField
                {
                    touchable = false,
                    singleLine = true,
                    text = "▼ " + _workspaceSnapshot.PreviewFrame + "f",
                    color = new Color32(92, 218, 226, 255),
                    align = Fgui.AlignType.Center,
                };
                badge.SetXY(playX - 25f, 2f);
                badge.SetSize(50f, 18f);
                badge.textFormat.size = 12;
                badge.textFormat.bold = true;
                item.AddChild(badge);
            }
        }

        private void DrawTrackLabelBadge(
            Fgui.GComponent item,
            CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack track,
            Color32 themeColor)
        {
            float badgeX = 8f;
            float badgeY = 6f;
            float badgeWidth = WorkspaceTrackLabelWidth - 18f;
            float badgeHeight = WorkspaceTrackRowHeight - 12f;

            var badgeBg = new Fgui.GGraph { name = "badgeBg", touchable = false };
            badgeBg.SetXY(badgeX, badgeY);
            badgeBg.SetSize(badgeWidth, badgeHeight);
            badgeBg.DrawRect(
                badgeWidth,
                badgeHeight,
                1,
                new Color32(32, 50, 66, 200),
                new Color32(16, 27, 38, 240));
            item.AddChild(badgeBg);

            var stripe = new Fgui.GGraph { name = "badgeStripe", touchable = false };
            stripe.SetXY(badgeX, badgeY);
            stripe.SetSize(4f, badgeHeight);
            stripe.DrawRect(4f, badgeHeight, 0, Color.clear, themeColor);
            item.AddChild(stripe);

            string cleanLabel = FormatTrackLabel(track.Kind, track.Label);
            Fgui.GTextField label = item.GetChild("label")?.asTextField;
            if (label == null)
            {
                label = new Fgui.GTextField { name = "label" };
                item.AddChild(label);
            }
            label.text = cleanLabel;
            label.SetXY(badgeX + 10f, badgeY + 6f);
            label.SetSize(badgeWidth - 14f, badgeHeight - 12f);
            label.textFormat.size = 16;
            label.textFormat.bold = true;
            label.textFormat.color = new Color32(235, 244, 250, 255);
            item.SetChildIndex(label, item.numChildren - 1);
        }

        private static string FormatTrackLabel(string kind, string rawLabel)
        {
            string key = string.IsNullOrWhiteSpace(kind) ? rawLabel ?? string.Empty : kind;
            return key switch
            {
                "Phases" or "阶段" => "阶段 · Phase",
                "TransitionWindow" or "切换窗口" or "切换窗口 / Transition Window" or "切换窗口 / TransitionWindow" => "切换窗口 · Window",
                "Motion" or "运动" => "运动 · Motion",
                "Animation" or "动画" => "动画 · Animation",
                "Combat" or "战斗" => "战斗 · Combat",
                "Gameplay" or "玩法" => "玩法 · Gameplay",
                "Presentation" or "表现" => "表现 · Presentation",
                _ => (rawLabel ?? key).Replace(" / Transition Window", "").Replace(" / TransitionWindow", ""),
            };
        }

        private static Color32 ResolveTrackThemeColor(string kind, string rawLabel)
        {
            string key = (string.IsNullOrWhiteSpace(kind) ? rawLabel ?? string.Empty : kind).ToLowerInvariant();
            if (key.Contains("phase") || key.Contains("阶段"))
                return new Color32(92, 145, 209, 255);
            if (key.Contains("transition") || key.Contains("切换"))
                return new Color32(229, 148, 56, 255);
            if (key.Contains("motion") || key.Contains("运动"))
                return new Color32(56, 178, 137, 255);
            if (key.Contains("anim") || key.Contains("动画"))
                return new Color32(142, 117, 218, 255);
            if (key.Contains("combat") || key.Contains("战斗"))
                return new Color32(224, 83, 83, 255);
            if (key.Contains("gameplay") || key.Contains("玩法"))
                return new Color32(74, 155, 232, 255);
            if (key.Contains("presentation") || key.Contains("表现"))
                return new Color32(216, 155, 56, 255);
            return new Color32(120, 160, 180, 255);
        }

        private static void DrawTrackVerticalGrid(
            Fgui.GComponent item,
            int durationFrames,
            float pixelsPerFrame)
        {
            int gridInterval = durationFrames <= 50 ? 5 : durationFrames <= 120 ? 10 : durationFrames <= 240 ? 20 : 30;
            for (int frame = gridInterval; frame <= durationFrames; frame += gridInterval)
            {
                float x = WorkspaceTrackLabelWidth + frame * pixelsPerFrame;
                var gridLine = new Fgui.GGraph { touchable = false };
                gridLine.SetXY(x, 0f);
                gridLine.SetSize(1f, WorkspaceTrackRowHeight);
                gridLine.DrawRect(1f, WorkspaceTrackRowHeight, 0, Color.clear, new Color32(255, 255, 255, 12));
                item.AddChild(gridLine);
            }

            float endX = WorkspaceTrackLabelWidth + durationFrames * pixelsPerFrame;
            var endLine = new Fgui.GGraph { touchable = false };
            endLine.SetXY(endX, 0f);
            endLine.SetSize(1f, WorkspaceTrackRowHeight);
            endLine.DrawRect(1f, WorkspaceTrackRowHeight, 0, Color.clear, new Color32(235, 110, 80, 75));
            item.AddChild(endLine);
        }

        private void AddWorkspaceTimelineItem(
            Fgui.GComponent row,
            CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem item,
            float pixelsPerFrame,
            Color32 defaultThemeColor)
        {
            float x = WorkspaceTrackLabelWidth + Math.Max(0, item.StartFrame) * pixelsPerFrame;
            float width = Math.Max(6f, (item.EndFrame - item.StartFrame + 1) * pixelsPerFrame);
            Color32 color = item.ColorRgba != 0 ? UnpackWorkspaceColor(item.ColorRgba) : defaultThemeColor;

            float itemY = 6f;
            float itemHeight = 40f;

            var body = new Fgui.GGraph { name = "item", touchable = true, draggable = item.CanEdit };
            body.SetXY(x, itemY);
            body.SetSize(width, itemHeight);

            byte fillAlpha = (byte)(item.Selected ? 215 : 170);
            Color32 fillColor = new Color32(color.r, color.g, color.b, fillAlpha);
            Color32 borderColor = item.Selected ? new Color32(255, 255, 255, 255) : new Color32(color.r, color.g, color.b, 255);
            int borderWidth = item.Selected ? 2 : 1;

            body.DrawRect(width, itemHeight, borderWidth, borderColor, fillColor);
            BindCharacterActionTooltip(
                body,
                item.Title + "\n范围：" + item.StartFrame + "f–" + item.EndFrame + "f (时长 " + (item.EndFrame - item.StartFrame + 1) + "f)");
            body.onClick.Set(() => ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SelectTimelineItem,
                item.ItemId));

            int originalStart = item.StartFrame;
            int originalEnd = item.EndFrame;
            row.AddChild(body);

            if (width >= 8f)
            {
                var topHighlight = new Fgui.GGraph { touchable = false };
                topHighlight.SetXY(x + 1f, itemY + 1f);
                topHighlight.SetSize(Math.Max(1f, width - 2f), 2f);
                topHighlight.DrawRect(
                    Math.Max(1f, width - 2f),
                    2f,
                    0,
                    Color.clear,
                    new Color32(255, 255, 255, (byte)(item.Selected ? 120 : 60)));
                row.AddChild(topHighlight);
            }

            Fgui.GTextField label = null;
            if (width >= 24f)
            {
                label = new Fgui.GTextField
                {
                    name = "clipLabel",
                    text = FormatTimelineItemTitle(item.Title, item.StartFrame, item.EndFrame, width),
                    color = new Color32(248, 252, 255, 255),
                    singleLine = true,
                    touchable = false,
                };
                label.SetXY(x + 8f, itemY + 8f);
                label.SetSize(Math.Max(1f, width - 16f), 24f);
                label.textFormat.size = 15;
                label.textFormat.bold = true;
                row.AddChild(label);
            }

            Fgui.GGraph leftHandle = null;
            Fgui.GGraph rightHandle = null;
            Fgui.GGraph leftHandleLine = null;
            Fgui.GGraph rightHandleLine = null;
            Action<int, int> updatePreview = (previewStart, previewEnd) =>
            {
                float previewX = WorkspaceTrackLabelWidth + previewStart * pixelsPerFrame;
                float previewWidth = Math.Max(
                    6f,
                    (previewEnd - previewStart + 1) * pixelsPerFrame);
                body.SetXY(previewX, itemY);
                body.SetSize(previewWidth, itemHeight);
                body.DrawRect(previewWidth, itemHeight, borderWidth, borderColor, fillColor);
                if (label == null && previewWidth >= 24f)
                {
                    label = new Fgui.GTextField
                    {
                        name = "clipLabel",
                        color = new Color32(248, 252, 255, 255),
                        singleLine = true,
                        touchable = false,
                    };
                    label.textFormat.size = 15;
                    label.textFormat.bold = true;
                    row.AddChild(label);
                }
                if (label != null)
                {
                    label.visible = previewWidth >= 24f;
                    label.SetXY(previewX + 8f, itemY + 8f);
                    label.SetSize(Math.Max(1f, previewWidth - 16f), 24f);
                    label.text = previewStart + "f–" + previewEnd + "f (" + (previewEnd - previewStart + 1) + "f)";
                }
                if (leftHandle != null)
                {
                    float hitWidth = Math.Min(
                        WorkspaceResizeHandleHitWidth,
                        Math.Max(1f, previewWidth * 0.5f));
                    leftHandle.SetSize(hitWidth, itemHeight);
                    leftHandle.SetXY(previewX, itemY);
                }
                if (rightHandle != null)
                {
                    float hitWidth = Math.Min(
                        WorkspaceResizeHandleHitWidth,
                        Math.Max(1f, previewWidth * 0.5f));
                    rightHandle.SetSize(hitWidth, itemHeight);
                    rightHandle.SetXY(
                        previewX + Math.Max(0f, previewWidth - hitWidth),
                        itemY);
                }
                if (leftHandleLine != null)
                    leftHandleLine.SetXY(previewX, itemY);
                if (rightHandleLine != null)
                {
                    rightHandleLine.SetXY(
                        previewX + Math.Max(0f, previewWidth - WorkspaceResizeHandleLineWidth),
                        itemY);
                }
            };
            Action<int, int> commitPreview = (finalStart, finalEnd) =>
            {
                updatePreview(finalStart, finalEnd);
                if (label != null)
                {
                    float finalWidth = Math.Max(6f, (finalEnd - finalStart + 1) * pixelsPerFrame);
                    label.text = FormatTimelineItemTitle(item.Title, finalStart, finalEnd, finalWidth);
                }
            };
            Action restorePreview = () =>
            {
                updatePreview(originalStart, originalEnd);
                if (label != null)
                    label.text = FormatTimelineItemTitle(item.Title, originalStart, originalEnd, width);
            };

            if (item.CanEdit)
            {
                int duration = Math.Max(0, originalEnd - originalStart);
                int maximumStart = Math.Max(
                    0,
                    Math.Max(1, _workspaceSnapshot.DurationFrames) - duration - 1);
                body.onDragMove.Set(() =>
                {
                    int nextStart = Mathf.Clamp(
                        Mathf.RoundToInt((body.x - WorkspaceTrackLabelWidth) / pixelsPerFrame),
                        0,
                        maximumStart);
                    updatePreview(nextStart, nextStart + duration);
                });
                body.onDragEnd.Set(() =>
                {
                    int nextStart = Mathf.Clamp(
                        Mathf.RoundToInt((body.x - WorkspaceTrackLabelWidth) / pixelsPerFrame),
                        0,
                        maximumStart);
                    int nextEnd = nextStart + duration;
                    if (nextStart != originalStart)
                    {
                        commitPreview(nextStart, nextEnd);
                        ExecuteWorkspaceCommand(
                            CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.MoveResizeTimelineItem,
                            item.ItemId,
                            intValue: nextStart,
                            secondaryIntValue: nextEnd);
                    }
                    else
                    {
                        restorePreview();
                    }
                });
            }

            if (item.CanResize)
            {
                leftHandle = CreateWorkspaceResizeHandle("resizeStart", itemHeight);
                rightHandle = CreateWorkspaceResizeHandle("resizeEnd", itemHeight);
                leftHandleLine = CreateWorkspaceResizeHandleLine("resizeStartLine", itemHeight);
                rightHandleLine = CreateWorkspaceResizeHandleLine("resizeEndLine", itemHeight);
                row.AddChild(leftHandleLine);
                row.AddChild(rightHandleLine);
                row.AddChild(leftHandle);
                row.AddChild(rightHandle);
                restorePreview();
                BindWorkspaceResizeHandle(
                    row,
                    leftHandle,
                    resizeStart: true,
                    originalStart,
                    originalEnd,
                    pixelsPerFrame,
                    updatePreview,
                    commitPreview,
                    restorePreview,
                    item.ItemId);
                BindWorkspaceResizeHandle(
                    row,
                    rightHandle,
                    resizeStart: false,
                    originalStart,
                    originalEnd,
                    pixelsPerFrame,
                    updatePreview,
                    commitPreview,
                    restorePreview,
                    item.ItemId);
            }
        }

        private static Fgui.GGraph CreateWorkspaceResizeHandle(string name, float height = 40f)
        {
            var handle = new Fgui.GGraph { name = name, touchable = true };
            handle.SetSize(WorkspaceResizeHandleHitWidth, height);
            handle.DrawRect(
                WorkspaceResizeHandleHitWidth,
                height,
                0,
                Color.clear,
                new Color32(255, 255, 255, 1));
            return handle;
        }

        private static Fgui.GGraph CreateWorkspaceResizeHandleLine(string name, float height = 40f)
        {
            var line = new Fgui.GGraph { name = name, touchable = false };
            line.SetSize(WorkspaceResizeHandleLineWidth, height);
            line.DrawRect(
                WorkspaceResizeHandleLineWidth,
                height,
                0,
                Color.clear,
                new Color32(255, 255, 255, 200));
            return line;
        }

        private void BindWorkspaceResizeHandle(
            Fgui.GComponent row,
            Fgui.GGraph handle,
            bool resizeStart,
            int originalStart,
            int originalEnd,
            float pixelsPerFrame,
            Action<int, int> updatePreview,
            Action<int, int> commitPreview,
            Action restorePreview,
            string itemId)
        {
            int previewStart = originalStart;
            int previewEnd = originalEnd;
            bool active = false;
            handle.onTouchBegin.Set(context =>
            {
                context?.StopPropagation();
                active = context?.inputEvent != null;
                if (active)
                    context.CaptureTouch();
            });
            handle.onTouchMove.Set(context =>
            {
                context?.StopPropagation();
                if (!active || context?.inputEvent == null)
                    return;
                float pointerX = row.GlobalToLocal(Fgui.Stage.inst.touchPosition).x;
                int frame = Mathf.Clamp(
                    Mathf.RoundToInt((pointerX - WorkspaceTrackLabelWidth) / pixelsPerFrame),
                    0,
                    Math.Max(0, _workspaceSnapshot.DurationFrames - 1));
                if (resizeStart)
                    previewStart = Math.Min(frame, originalEnd);
                else
                    previewEnd = Math.Max(originalStart, frame);
                updatePreview(previewStart, previewEnd);
            });
            handle.onTouchEnd.Set(context =>
            {
                context?.StopPropagation();
                if (!active)
                    return;
                active = false;
                if (previewStart == originalStart && previewEnd == originalEnd)
                {
                    restorePreview();
                    return;
                }
                commitPreview(previewStart, previewEnd);
                ExecuteWorkspaceCommand(
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.MoveResizeTimelineItem,
                    itemId,
                    intValue: previewStart,
                    secondaryIntValue: previewEnd);
            });
        }

        private static string FormatTimelineItemTitle(
            string title,
            int startFrame,
            int endFrame,
            float clipWidth)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            string normalized = title.Trim();
            int duration = Math.Max(1, endFrame - startFrame + 1);
            string rangeTag = $" ({startFrame}f–{endFrame}f)";

            // 1. RootMotion
            if (normalized.StartsWith("RootMotion", StringComparison.OrdinalIgnoreCase))
            {
                int sampleIdx = normalized.IndexOf("采样=", StringComparison.OrdinalIgnoreCase);
                if (sampleIdx < 0)
                    sampleIdx = normalized.IndexOf("采样", StringComparison.OrdinalIgnoreCase);
                if (sampleIdx >= 0)
                {
                    int commaIdx = normalized.IndexOfAny(new[] { ',', '，' }, sampleIdx);
                    string samplePart = commaIdx > sampleIdx ? normalized.Substring(sampleIdx, commaIdx - sampleIdx) : "采样=76";
                    samplePart = samplePart.Replace("采样=", "").Replace("采样", "").Trim();
                    return clipWidth < 90f ? $"RM ({samplePart}f)" : $"RootMotion ({samplePart} 帧采样)";
                }
                return clipWidth < 90f ? "RootMotion" : $"RootMotion ({duration}f)";
            }

            // 2. Animation / 过渡到动画
            if (normalized.StartsWith("过渡到动画", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("动画", StringComparison.OrdinalIgnoreCase))
            {
                string clipName = "animation";
                int animIdx = normalized.IndexOf("动画=", StringComparison.OrdinalIgnoreCase);
                if (animIdx >= 0)
                {
                    int commaIdx = normalized.IndexOfAny(new[] { ',', '，' }, animIdx);
                    string animPart = commaIdx > animIdx ? normalized.Substring(animIdx + 3, commaIdx - animIdx - 3) : normalized.Substring(animIdx + 3);
                    int lastDot = animPart.LastIndexOf('.');
                    clipName = lastDot >= 0 ? animPart.Substring(lastDot + 1).Trim() : animPart.Trim();
                }
                else
                {
                    int colonIdx = normalized.IndexOfAny(new[] { ':', '：' });
                    if (colonIdx >= 0)
                    {
                        string sub = normalized.Substring(colonIdx + 1);
                        int lastDot = sub.LastIndexOf('.');
                        clipName = lastDot >= 0 ? sub.Substring(lastDot + 1).Trim() : sub.Trim();
                    }
                }

                string speed = "1.0x";
                int speedIdx = normalized.IndexOf("速度=", StringComparison.OrdinalIgnoreCase);
                if (speedIdx >= 0)
                {
                    int commaIdx = normalized.IndexOfAny(new[] { ',', '，' }, speedIdx);
                    speed = commaIdx > speedIdx ? normalized.Substring(speedIdx + 3, commaIdx - speedIdx - 3).Trim() : "1.0x";
                    if (!speed.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                        speed += "x";
                }

                string transition = string.Empty;
                int transIdx = normalized.IndexOf("过渡=", StringComparison.OrdinalIgnoreCase);
                if (transIdx >= 0)
                {
                    int commaIdx = normalized.IndexOfAny(new[] { ',', '，' }, transIdx);
                    string transPart = commaIdx > transIdx ? normalized.Substring(transIdx + 3, commaIdx - transIdx - 3).Trim() : normalized.Substring(transIdx + 3).Trim();
                    transPart = transPart.Replace("帧", "f").Trim();
                    transition = $" · 过渡 {transPart}";
                }

                if (clipWidth < 90f)
                    return clipName;
                if (clipWidth < 180f)
                    return $"{clipName} ({speed})";
                return $"动画 · {clipName} ({speed}{transition})";
            }

            // 3. Combat / 武器轨迹判定
            if (normalized.StartsWith("武器轨迹判定", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("武器判定", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("判定", StringComparison.OrdinalIgnoreCase))
            {
                int slotIdx = normalized.IndexOf("装备槽位=", StringComparison.OrdinalIgnoreCase);
                string slot = "mainHand";
                if (slotIdx >= 0)
                {
                    int comma = normalized.IndexOfAny(new[] { ',', '，' }, slotIdx);
                    if (comma > slotIdx)
                        slot = normalized.Substring(slotIdx + 5, comma - slotIdx - 5).Trim();
                }

                int dmgIdx = normalized.IndexOf("伤害=", StringComparison.OrdinalIgnoreCase);
                string dmg = string.Empty;
                if (dmgIdx >= 0)
                {
                    int slash = normalized.IndexOf('/', dmgIdx);
                    if (slash > dmgIdx)
                    {
                        dmg = normalized.Substring(dmgIdx + 3, slash - dmgIdx - 3).Trim();
                        int nextSlash = normalized.IndexOf('/', slash + 1);
                        string hpPart = nextSlash > slash ? normalized.Substring(slash + 1, nextSlash - slash - 1).Trim() : "";
                        if (hpPart.Length > 0 && hpPart.Contains("HP"))
                        {
                            int hpIdx = hpPart.IndexOf("HP", StringComparison.OrdinalIgnoreCase);
                            int plusIdx = hpPart.IndexOf('+', hpIdx);
                            string hpVal = plusIdx > hpIdx ? hpPart.Substring(hpIdx, plusIdx - hpIdx).Trim() : hpPart.Substring(hpIdx).Trim();
                            dmg += " " + hpVal;
                        }
                    }
                }

                if (clipWidth < 90f)
                    return slot;
                if (dmg.Length > 0 && clipWidth >= 160f)
                    return $"武器判定 · {slot} ({dmg})";
                return $"武器判定 · {slot}";
            }

            // 4. TransitionWindow / 切换窗口
            if (normalized.StartsWith("切换窗口", StringComparison.OrdinalIgnoreCase))
            {
                if (clipWidth < 90f)
                    return "切换窗口";
                return clipWidth >= 160f ? $"切换窗口 · 派生/取消{rangeTag}" : "切换窗口 · 派生/取消";
            }

            // 5. Phases
            if (normalized.EndsWith("阶段", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("前摇") || normalized.Contains("生效") || normalized.Contains("收招"))
            {
                if (clipWidth < 70f)
                    return normalized.Replace("阶段", "");
                return clipWidth >= 140f ? $"{normalized}{rangeTag}" : normalized;
            }

            // 6. Gameplay / Presentation
            int colon = normalized.IndexOfAny(new[] { ':', '：' });
            if (colon >= 0)
            {
                string prefix = normalized.Substring(0, colon).Trim();
                string rest = normalized.Substring(colon + 1).Trim();
                int lastDot = rest.LastIndexOf('.');
                string shortRest = lastDot >= 0 ? rest.Substring(lastDot + 1).Trim() : rest;
                int comma = shortRest.IndexOfAny(new[] { ',', '，' });
                if (comma > 0)
                    shortRest = shortRest.Substring(0, comma).Trim();
                if (clipWidth < 80f)
                    return shortRest;
                return $"{prefix} · {shortRest}";
            }

            return normalized;
        }

        private void BindWorkspaceInspectorRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField[] fields =
                _workspaceSnapshot?.InspectorFields ?? Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField>();
            if (item == null || index < 0 || index >= _workspaceFilteredInspector.Count)
                return;
            int fieldIndex = _workspaceFilteredInspector[index];
            if (fieldIndex < 0 || fieldIndex >= fields.Length)
                return;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField field = fields[fieldIndex];

            float rowWidth = Math.Max(380f, _characterActionWorkspace?.GetChild("inspectorList")?.width ?? 420f);
            float rowHeight = 48f;
            item.SetSize(rowWidth, rowHeight);

            Fgui.GGraph cardBg = item.GetChild("cardBg")?.asGraph;
            if (cardBg == null)
            {
                cardBg = new Fgui.GGraph { name = "cardBg", touchable = false };
                item.AddChildAt(cardBg, 0);
            }
            cardBg.SetXY(0f, 2f);
            cardBg.SetSize(rowWidth, rowHeight - 4f);
            cardBg.DrawRect(rowWidth, rowHeight - 4f, 1,
                new Color32(32, 44, 56, 180),
                new Color32(14, 20, 26, 200));

            float labelWidth = 175f;
            Fgui.GTextField label = item.GetChild("label")?.asTextField;
            if (label != null)
            {
                label.text = FormatInspectorFieldLabel(field.Path, field.Label);
                label.textFormat.size = 18;
                label.textFormat.bold = true;
                label.textFormat.color = new Color32(165, 188, 205, 255);
                label.SetXY(10f, 6f);
                label.SetSize(labelWidth, 34f);
                BindCharacterActionTooltip(label, field.Path);
            }

            bool arrayField = string.Equals(field.Kind, "Array", StringComparison.Ordinal);
            bool choiceField = field.Editable
                && field.Options?.Length > 0
                && (string.Equals(field.Kind, "Enum", StringComparison.Ordinal)
                    || string.Equals(field.Kind, "Flags", StringComparison.Ordinal)
                    || string.Equals(field.Kind, "Reference", StringComparison.Ordinal)
                    || string.Equals(field.Kind, "Toggle", StringComparison.Ordinal));
            bool numericField = field.Editable
                && (string.Equals(field.Kind, "Integer", StringComparison.Ordinal)
                    || string.Equals(field.Kind, "Float", StringComparison.Ordinal));
            float inputX = labelWidth + 16f;
            float inputWidth = arrayField || numericField
                ? Math.Max(100f, rowWidth - inputX - 86f)
                : choiceField
                    ? Math.Max(100f, rowWidth - inputX - 48f)
                    : Math.Max(100f, rowWidth - inputX - 10f);

            Fgui.GGraph inputBg = item.GetChild("inputBg")?.asGraph;
            if (inputBg == null)
            {
                inputBg = new Fgui.GGraph { name = "inputBg", touchable = false };
                item.AddChildAt(inputBg, 1);
            }
            inputBg.SetXY(inputX, 5f);
            inputBg.SetSize(inputWidth, 36f);
            inputBg.DrawRect(inputWidth, 36f, 1,
                field.Editable && !arrayField ? new Color32(48, 70, 92, 255) : new Color32(28, 38, 48, 180),
                field.Editable && !arrayField ? new Color32(18, 26, 35, 250) : new Color32(12, 16, 22, 160));

            Fgui.GTextInput input = item.GetChild("input")?.asTextInput;
            if (input != null)
            {
                input.text = choiceField
                    ? ResolveInspectorChoiceLabel(field)
                    : FormatInspectorFieldValue(field.Path, field.Value);
                input.textFormat.size = 18;
                input.textFormat.color = field.Editable && !arrayField
                    ? new Color32(245, 250, 255, 255)
                    : new Color32(160, 175, 188, 200);
                input.SetXY(inputX + 6f, 7f);
                input.SetSize(inputWidth - 12f, 32f);
                input.editable = field.Editable && !arrayField && !choiceField;
                input.touchable = field.Editable && !arrayField;
                input.grayed = !field.Editable || arrayField;
                BindCharacterActionTooltip(input, field.Path + " = " + field.Value);
                input.onSubmit.Clear();
                input.onFocusOut.Clear();
                input.onClick.Clear();
                if (choiceField)
                {
                    input.onClick.Set(() => ShowWorkspaceFieldChoiceMenu(field, input));
                }
                else
                {
                    input.onSubmit.Set(() => CommitWorkspaceField(
                        field,
                        UnformatInspectorFieldValue(input.text)));
                    input.onFocusOut.Set(() => CommitWorkspaceField(
                        field,
                        UnformatInspectorFieldValue(input.text)));
                }
            }

            Fgui.GButton add = item.GetChild("btnAdd")?.asButton;
            Fgui.GButton remove = item.GetChild("btnRemove")?.asButton;
            if (add != null)
            {
                add.visible = arrayField || choiceField || numericField;
                add.enabled = field.Editable && (arrayField || choiceField || numericField);
                add.title = choiceField ? "▼" : "+";
                add.titleFontSize = 18;
                add.SetXY(arrayField || numericField ? rowWidth - 78f : rowWidth - 40f, 5f);
                add.SetSize(34f, 36f);
                if (arrayField)
                {
                    add.onClick.Set(() => ExecuteWorkspaceCommand(
                        CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.AddArrayElement,
                        field.Path));
                }
                else if (choiceField)
                {
                    add.onClick.Set(() => ShowWorkspaceFieldChoiceMenu(field, add));
                }
                else if (numericField)
                {
                    add.onClick.Set(() => AdjustWorkspaceNumericField(field, 1));
                }
                else
                {
                    add.onClick.Clear();
                }
            }
            if (remove != null)
            {
                remove.visible = arrayField || numericField;
                remove.enabled = field.Editable && (arrayField || numericField);
                remove.title = "−";
                remove.titleFontSize = 18;
                remove.SetXY(rowWidth - 40f, 5f);
                remove.SetSize(34f, 36f);
                if (arrayField)
                {
                    remove.onClick.Set(() => ExecuteWorkspaceCommand(
                        CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.DeleteArrayElement,
                        field.Path));
                }
                else if (numericField)
                {
                    remove.onClick.Set(() => AdjustWorkspaceNumericField(field, -1));
                }
                else
                {
                    remove.onClick.Clear();
                }
            }
        }

        private void AdjustWorkspaceNumericField(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField field,
            int direction)
        {
            if (field == null || direction == 0)
                return;
            if (string.Equals(field.Kind, "Integer", StringComparison.Ordinal)
                && long.TryParse(
                    field.Value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long integerValue))
            {
                CommitWorkspaceField(
                    field,
                    (integerValue + Math.Sign(direction)).ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (string.Equals(field.Kind, "Float", StringComparison.Ordinal)
                && double.TryParse(
                    field.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double floatValue))
            {
                CommitWorkspaceField(
                    field,
                    (floatValue + Math.Sign(direction) * 0.1d).ToString(
                        "0.###",
                        System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static string ResolveInspectorChoiceLabel(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField field)
        {
            string[] values = field?.Options ?? Array.Empty<string>();
            string[] labels = field?.OptionLabels ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], field.Value, StringComparison.Ordinal))
                    return i < labels.Length ? labels[i] : values[i];
            }
            return string.IsNullOrWhiteSpace(field?.Value)
                ? "未设置"
                : "保留旧值：" + field.Value;
        }

        private void ShowWorkspaceFieldChoiceMenu(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField field,
            Fgui.GObject anchor)
        {
            CloseWorkspaceFieldChoiceMenu();
            if (field?.Options == null || field.Options.Length == 0 || anchor == null)
                return;

            float width = Mathf.Clamp(anchor.width + 48f, 280f, 460f);
            bool searchable = field.Options.Length > 10;
            int visibleCount = Math.Min(10, field.Options.Length);
            float searchHeight = searchable ? 50f : 0f;
            float itemHeight = 42f;
            float height = 16f + searchHeight + visibleCount * itemHeight;
            _characterActionWorkspaceFieldChoiceMenu = new Fgui.GComponent
            {
                name = "fieldChoiceMenu",
                opaque = true,
            };
            _characterActionWorkspaceFieldChoiceMenu.SetSize(width, height);
            var background = new Fgui.GGraph { touchable = true };
            background.SetSize(width, height);
            background.DrawRect(
                width,
                height,
                1,
                new Color32(61, 91, 112, 255),
                new Color32(7, 16, 23, 252));
            _characterActionWorkspaceFieldChoiceMenu.AddChild(background);

            Fgui.GTextInput search = null;
            if (searchable)
            {
                var searchBackground = new Fgui.GGraph { touchable = false };
                searchBackground.SetXY(8f, 8f);
                searchBackground.SetSize(width - 16f, 38f);
                searchBackground.DrawRect(
                    width - 16f,
                    38f,
                    1,
                    new Color32(48, 70, 92, 255),
                    new Color32(14, 24, 32, 255));
                _characterActionWorkspaceFieldChoiceMenu.AddChild(searchBackground);
                search = new Fgui.GTextInput
                {
                    promptText = "搜索选项或 Stable ID",
                    singleLine = true,
                };
                search.SetXY(14f, 11f);
                search.SetSize(width - 28f, 32f);
                search.textFormat.size = 16;
                search.textFormat.color = new Color32(235, 244, 250, 255);
                _characterActionWorkspaceFieldChoiceMenu.AddChild(search);
            }

            Action rebuild = () =>
            {
                while (_characterActionWorkspaceFieldChoiceMenu != null
                    && _characterActionWorkspaceFieldChoiceMenu.numChildren > (searchable ? 3 : 1))
                {
                    _characterActionWorkspaceFieldChoiceMenu.RemoveChildAt(
                        _characterActionWorkspaceFieldChoiceMenu.numChildren - 1,
                        true);
                }
                string query = search?.text?.Trim() ?? string.Empty;
                int rendered = 0;
                for (int i = 0; i < field.Options.Length && rendered < 10; i++)
                {
                    string value = field.Options[i] ?? string.Empty;
                    string label = i < (field.OptionLabels?.Length ?? 0)
                        ? field.OptionLabels[i] ?? value
                        : value;
                    if (query.Length > 0
                        && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                        && label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    Fgui.GButton button = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        string.Equals(value, field.Value, StringComparison.Ordinal)
                            ? "ToolPrimaryButton"
                            : "ToolNeutralButton")?.asButton ?? new Fgui.GButton();
                    button.title = label;
                    button.titleFontSize = 16;
                    button.SetXY(8f, 8f + searchHeight + rendered * itemHeight);
                    button.SetSize(width - 16f, itemHeight - 4f);
                    string selectedValue = value;
                    button.onClick.Set(() =>
                    {
                        CloseWorkspaceFieldChoiceMenu();
                        CommitWorkspaceField(field, selectedValue);
                    });
                    _characterActionWorkspaceFieldChoiceMenu.AddChild(button);
                    rendered++;
                }
            };
            if (search != null)
                search.onChanged.Set(_ => rebuild());
            rebuild();
            Fgui.GRoot.inst.TogglePopup(
                _characterActionWorkspaceFieldChoiceMenu,
                anchor,
                Fgui.PopupDirection.Auto,
                false);
        }

        private void CloseWorkspaceFieldChoiceMenu()
        {
            if (_characterActionWorkspaceFieldChoiceMenu?.parent != null)
                Fgui.GRoot.inst.HidePopup(_characterActionWorkspaceFieldChoiceMenu);
            _characterActionWorkspaceFieldChoiceMenu = null;
        }

        private static string FormatInspectorFieldLabel(string path, string rawLabel)
        {
            if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(rawLabel))
                return string.Empty;

            string key = rawLabel ?? string.Empty;
            int bracketIdx = key.IndexOf('[');
            string arraySuffix = string.Empty;
            if (bracketIdx >= 0)
            {
                arraySuffix = " " + key.Substring(bracketIdx);
                key = key.Substring(0, bracketIdx);
            }

            string translated = key switch
            {
                "id" => "动作序号 (ID)",
                "stableId" => "唯一标识 (StableID)",
                "displayName" => "显示名称",
                "category" => "动作分类",
                "timelineAuthority" or "timelineAuthoringMode" => "制作方式",
                "priority" => "优先级",
                "durationFrames" => "持续帧数",
                "tags" => "标签列表",
                "requirements" => "释放需求",
                "targetDistanceMin" => "最小目标距离",
                "targetDistanceMax" => "最大目标距离",
                "targetAngleMax" => "最大视角偏角",
                "energyCost" => "能量消耗",
                "cooldownSeconds" => "冷却时间(秒)",
                "resourceCost" => "资源消耗",
                "minHealthRatio" => "最小血量比例",
                "maxHealthRatio" => "最大血量比例",

                "itemId" => "片段唯一ID",
                "title" => "片段标题",
                "name" => "阶段名称",
                "startFrame" => "起始帧",
                "endFrame" => "结束帧",
                "frame" => "触发帧",
                "kind" or "trackKind" => "事件类型",
                "colorRgba" => "显示颜色",
                "clipName" => "动画片段名",
                "crossFadeDuration" => "过渡融合时长",
                "speed" => "播放速率",
                "loop" => "是否循环",
                "sampleRate" => "采样率",
                "rootMotion" => "根骨骼位移",
                "slot" or "equipSlot" => "装备槽位",
                "damage" => "伤害数值",
                "hitBox" or "hitBoxName" => "判定框名称",
                "reaction" => "受击硬直类型",
                "window" or "windowKind" => "窗口阶段类型",
                "targetActionId" => "目标转纹动作",
                "canCancel" => "允许取消",
                "canCombo" => "允许派生连段",
                "canDodge" => "允许闪避打断",
                "cueId" => "表现 Cue",
                "cameraShake" => "镜头震颤",
                "vfx" or "vfxName" => "特效资源",
                "sfx" or "sfxName" => "音效资源",
                _ => key,
            };

            return translated + arraySuffix;
        }

        private static string FormatInspectorFieldValue(string path, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return string.Empty;

            if (rawValue.EndsWith(" items", StringComparison.OrdinalIgnoreCase))
            {
                int spaceIdx = rawValue.IndexOf(' ');
                string countStr = spaceIdx > 0 ? rawValue.Substring(0, spaceIdx) : rawValue;
                return countStr + " 项";
            }

            return rawValue switch
            {
                "BasicAttack" => "基础攻击 (BasicAttack)",
                "Skill" => "技能 (Skill)",
                "Ultimate" => "终结技 (Ultimate)",
                "Dodge" => "闪避 (Dodge)",
                "HitReaction" => "受击 (HitReaction)",
                "Idle" => "待机 (Idle)",
                "Move" => "移动 (Move)",
                "CharacterAuthored" => "角色独立制作 (CharacterAuthored)",
                "LibraryShared" => "共享库动作 (LibraryShared)",
                "GeneratedOnly" => "仅生成 (GeneratedOnly)",
                "combat" => "战斗 (combat)",
                "light_attack" => "轻攻击 (light_attack)",
                "heavy_attack" => "重攻击 (heavy_attack)",
                "True" or "true" => "是 (True)",
                "False" or "false" => "否 (False)",
                _ => rawValue,
            };
        }

        private static string UnformatInspectorFieldValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string trimmed = value.Trim();
            if (trimmed.EndsWith(")"))
            {
                int openParen = trimmed.LastIndexOf('(');
                if (openParen >= 0)
                    return trimmed.Substring(openParen + 1, trimmed.Length - openParen - 2).Trim();
            }
            if (string.Equals(trimmed, "是", StringComparison.OrdinalIgnoreCase))
                return "true";
            if (string.Equals(trimmed, "否", StringComparison.OrdinalIgnoreCase))
                return "false";
            return trimmed;
        }

        private void CommitWorkspaceField(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField field,
            string value)
        {
            if (field == null || !field.Editable || string.Equals(field.Value, value, StringComparison.Ordinal))
                return;
            if (string.Equals(field.Kind, "Integer", StringComparison.Ordinal)
                && !long.TryParse(
                    value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                RefreshWorkspaceFromCommandSink(selectionOnly: false);
                return;
            }
            if (string.Equals(field.Kind, "Float", StringComparison.Ordinal)
                && !double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                RefreshWorkspaceFromCommandSink(selectionOnly: false);
                return;
            }
            ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SetField,
                field.Path,
                secondaryText: value ?? string.Empty);
        }

        private void SynchronizeWorkspaceInspectorGroup()
        {
            string selectionId = _workspaceSnapshot?.SelectedTimelineItemId ?? string.Empty;
            if (string.Equals(selectionId, _workspaceInspectorSelectionId, StringComparison.Ordinal))
                return;
            _workspaceInspectorSelectionId = selectionId;
            _workspaceInspectorGroup = selectionId.Length > 0 ? "selected" : "basic";
        }

        private void SetWorkspaceInspectorGroup(string group)
        {
            string normalized = group ?? "basic";
            if (string.Equals(normalized, "selected", StringComparison.Ordinal)
                && string.IsNullOrEmpty(_workspaceSnapshot?.SelectedTimelineItemId))
            {
                return;
            }
            _workspaceInspectorGroup = normalized;
            RebuildWorkspaceInspectorFilter();
            SetWorkspaceListCount("inspectorList", _workspaceFilteredInspector.Count);
            BindWorkspaceInspectorGroupButtons();
        }

        private void RebuildWorkspaceInspectorFilter()
        {
            _workspaceFilteredInspector.Clear();
            CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField[] fields =
                _workspaceSnapshot?.InspectorFields
                ?? Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField>();
            bool selectedItem = !string.IsNullOrEmpty(_workspaceSnapshot?.SelectedTimelineItemId);
            for (int i = 0; i < fields.Length; i++)
            {
                string path = fields[i]?.Path ?? string.Empty;
                bool include = _workspaceInspectorGroup switch
                {
                    "selected" => selectedItem,
                    "timing" => !selectedItem
                        && string.Equals(path, "durationFrames", StringComparison.Ordinal),
                    "requirements" => !selectedItem
                        && (string.Equals(path, "requirements", StringComparison.Ordinal)
                            || path.StartsWith("requirements.", StringComparison.Ordinal)
                            || path.StartsWith("requirements[", StringComparison.Ordinal)),
                    _ => !selectedItem
                        && !string.Equals(path, "durationFrames", StringComparison.Ordinal)
                        && !string.Equals(path, "requirements", StringComparison.Ordinal)
                        && !path.StartsWith("requirements.", StringComparison.Ordinal)
                        && !path.StartsWith("requirements[", StringComparison.Ordinal),
                };
                if (include)
                    _workspaceFilteredInspector.Add(i);
            }
        }

        private void BindWorkspaceInspectorGroupButtons()
        {
            BindWorkspaceInspectorGroupButton("btnProblemsAll", "基础", "basic", true);
            BindWorkspaceInspectorGroupButton("btnProblemsAction", "时间", "timing", true);
            BindWorkspaceInspectorGroupButton("btnProblemsSource", "需求", "requirements", true);
            BindWorkspaceInspectorGroupButton(
                "btnProblemsReferences",
                "当前片段",
                "selected",
                !string.IsNullOrEmpty(_workspaceSnapshot?.SelectedTimelineItemId));
        }

        private void BindWorkspaceInspectorGroupButton(
            string name,
            string title,
            string group,
            bool enabled)
        {
            Fgui.GButton button = _characterActionWorkspace?.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.title = title;
            button.titleFontSize = 18;
            button.enabled = enabled;
            button.selected = string.Equals(_workspaceInspectorGroup, group, StringComparison.Ordinal);
        }

        private void BindWorkspaceProblemRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            string[] problems = _workspaceSnapshot?.Problems ?? Array.Empty<string>();
            if (item == null || index < 0 || index >= _workspaceFilteredProblems.Count)
                return;
            string problem = problems[_workspaceFilteredProblems[index]];
            SetChildText(item, "text", problem);
            Fgui.GTextField text = item.GetChild("text")?.asTextField;
            if (text != null)
            {
                text.textFormat.size = 18;
            }
            BindCharacterActionTooltip(item, problem);
        }

        private void AdjustWorkspaceZoom(float direction)
        {
            float current = _workspacePixelsPerFrame > 0f
                ? _workspacePixelsPerFrame
                : Mathf.Clamp(
                    _workspaceTimelineWidth / Math.Max(1, _workspaceSnapshot?.DurationFrames ?? 1),
                    4f,
                    18f);
            _workspacePixelsPerFrame = Mathf.Clamp(current + direction * 2f, 2f, 32f);
            SetWorkspaceListCount("trackList", (_workspaceSnapshot?.Tracks.Length ?? 0) + 1);
        }

        private void FitWorkspaceTimeline()
        {
            _workspacePixelsPerFrame = 0f;
            Fgui.GList list = _characterActionWorkspace?.GetChild("trackList")?.asList;
            list?.scrollPane?.SetPosX(0f, false);
            SetWorkspaceListCount("trackList", (_workspaceSnapshot?.Tracks.Length ?? 0) + 1);
        }

        private void SetWorkspaceProblemFilter(string filter)
        {
            _workspaceProblemFilter = filter ?? "all";
            RebuildWorkspaceProblemFilter();
            SetWorkspaceListCount("problemsList", _workspaceFilteredProblems.Count);
        }

        private void RebuildWorkspaceProblemFilter()
        {
            _workspaceFilteredProblems.Clear();
            string[] problems = _workspaceSnapshot?.Problems ?? Array.Empty<string>();
            for (int i = 0; i < problems.Length; i++)
            {
                string problem = problems[i] ?? string.Empty;
                bool include = _workspaceProblemFilter switch
                {
                    "action" => problem.IndexOf(
                        (_workspaceSnapshot?.SelectedActionId ?? 0).ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.OrdinalIgnoreCase) >= 0,
                    "source" => problem.IndexOf(
                        _workspaceSnapshot?.LibraryId ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) >= 0,
                    "references" => problem.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0
                        || problem.IndexOf("引用", StringComparison.OrdinalIgnoreCase) >= 0
                        || problem.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0,
                    _ => true,
                };
                if (include)
                    _workspaceFilteredProblems.Add(i);
            }
        }

        private void AddWorkspaceTrackItem(string trackKind)
        {
            CloseWorkspaceAddMenu();
            ExecuteWorkspaceCommand(
                CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.AddTimelineItem,
                trackKind,
                intValue: _workspaceSnapshot?.PreviewFrame ?? 0);
        }

        private void ToggleWorkspaceAddMenu()
        {
            EnsureWorkspaceAddMenu();
            if (_characterActionWorkspaceAddMenu == null)
                return;
            if (_characterActionWorkspaceAddMenu.parent != null)
            {
                CloseWorkspaceAddMenu();
                return;
            }
            Fgui.GObject anchor = _characterActionWorkspace?.GetChild("btnAddPhase");
            Fgui.GRoot.inst.TogglePopup(
                _characterActionWorkspaceAddMenu,
                anchor,
                Fgui.PopupDirection.Auto,
                false);
        }

        private void EnsureWorkspaceAddMenu()
        {
            if (_characterActionWorkspaceAddMenu != null)
                return;
            _characterActionWorkspaceAddMenu = new Fgui.GComponent
            {
                name = "characterActionAddMenu",
                sortingOrder = 620,
            };
            const float width = 220f;
            const float itemHeight = 44f;
            string[] labels =
            {
                "阶段", "切换窗口", "Motion", "Animation", "Combat", "Gameplay", "Presentation"
            };
            string[] kinds =
            {
                "Phases", "TransitionWindow", "Motion", "Animation", "Combat", "Gameplay", "Presentation"
            };
            _characterActionWorkspaceAddMenu.SetSize(width, labels.Length * itemHeight + 16f);
            var background = new Fgui.GGraph { touchable = false };
            background.SetSize(_characterActionWorkspaceAddMenu.width, _characterActionWorkspaceAddMenu.height);
            background.DrawRect(
                background.width,
                background.height,
                1,
                WorkspaceSrgb(52, 79, 89),
                WorkspaceSrgb(5, 18, 26));
            _characterActionWorkspaceAddMenu.AddChild(background);
            for (int i = 0; i < labels.Length; i++)
            {
                Fgui.GButton button = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "ToolNeutralButton")?.asButton ?? new Fgui.GButton();
                button.title = labels[i];
                button.titleFontSize = 18;
                button.SetXY(8f, 8f + i * itemHeight);
                button.SetSize(width - 16f, itemHeight - 4f);
                string kind = kinds[i];
                button.onClick.Set(() => AddWorkspaceTrackItem(kind));
                _characterActionWorkspaceAddMenu.AddChild(button);
            }
        }

        private void CloseWorkspaceAddMenu()
        {
            if (_characterActionWorkspaceAddMenu?.parent != null)
                Fgui.GRoot.inst.HidePopup(_characterActionWorkspaceAddMenu);
        }

        private void ExecuteWorkspaceCommand(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind kind,
            string text = "",
            string secondaryText = "",
            int intValue = 0,
            int secondaryIntValue = 0)
        {
            var command = new CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
            {
                Kind = kind,
                ExpectedRevision = _workspaceSnapshot?.Revision ?? 0,
                Text = text ?? string.Empty,
                SecondaryText = secondaryText ?? string.Empty,
                IntValue = intValue,
                SecondaryIntValue = secondaryIntValue,
            };
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceCommand,
                command);
            RefreshWorkspaceFromCommandSink(
                selectionOnly: kind
                    == CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SelectTimelineItem);
        }

        private void ExecuteWorkspacePreview(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind kind,
            int frame)
        {
            bool rebuildVisualAfterRuntimeTransition = _workspaceSnapshot?.PreviewActive != true
                || kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewScrub
                || kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStop;
            if (rebuildVisualAfterRuntimeTransition)
            {
                CloseWorkspaceAddMenu();
                CloseWorkspaceFieldChoiceMenu();
                _characterActionWorkspace?.RemoveFromParent();
                _characterActionWorkspace = null;
                _characterActionWorkspaceCloseConfirm = null;
                _characterActionWorkspaceAddMenu = null;
                _characterActionWorkspaceFieldChoiceMenu = null;
            }
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspacePreview,
                new CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
                {
                    Kind = kind,
                    PreviewFrame = frame,
                });
            Fgui.Timers.inst.CallLater(_ =>
            {
                if (_root?.displayObject != null && !_root.displayObject.isDisposed)
                    RefreshWorkspaceFromCommandSink(selectionOnly: false);
            });
        }

        private void RefreshWorkspaceFromCommandSink(bool selectionOnly)
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            if (sink?.CharacterActionWorkspace != null)
            {
                _workspaceSnapshot = sink.CharacterActionWorkspace;
                Fgui.GComponent currentPageRoot = ResolveCharacterActionWorkspacePageRoot();
                bool requiresRemount = _workspaceSnapshot.Available
                    && _characterActionWorkspace != null
                    && !ReferenceEquals(_characterActionWorkspace.parent, currentPageRoot);
                if (requiresRemount || (_workspaceSnapshot.Available && _characterActionWorkspace == null))
                    ShowEmbeddedCharacterActionWorkspace();
                else if (selectionOnly)
                    BindWorkspaceSelectionOnly();
                else
                    BindEmbeddedCharacterActionWorkspace();
            }
        }

        private void RestoreEmbeddedCharacterActionWorkspaceIfOpen()
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            if (sink?.CharacterActionWorkspace?.Available == true)
                ShowEmbeddedCharacterActionWorkspace();
        }

        private void BindWorkspaceSelectionOnly()
        {
            if (_characterActionWorkspace == null || _workspaceSnapshot == null)
                return;
            SynchronizeWorkspaceInspectorGroup();
            RebuildWorkspaceInspectorFilter();
            BindWorkspaceInspectorGroupButtons();
            SetWorkspaceButtonEnabled(
                "btnDeleteItem",
                !_workspaceSnapshot.ReadOnly
                && !string.IsNullOrEmpty(_workspaceSnapshot.SelectedTimelineItemId));
            Fgui.GList trackList = _characterActionWorkspace.GetChild("trackList")?.asList;
            if (trackList != null)
            {
                trackList.numItems = 0;
                trackList.numItems = _workspaceSnapshot.Tracks.Length + 1;
            }
            Fgui.GList inspectorList = _characterActionWorkspace.GetChild("inspectorList")?.asList;
            if (inspectorList != null)
            {
                inspectorList.numItems = 0;
                inspectorList.numItems = _workspaceFilteredInspector.Count;
            }
        }

        private void RequestCloseEmbeddedWorkspace()
        {
            if (_workspaceSnapshot?.Dirty == true)
            {
                ShowWorkspaceCloseConfirmation();
                return;
            }
            CloseEmbeddedWorkspace();
        }

        private void ShowWorkspaceCloseConfirmation()
        {
            if (_characterActionWorkspace == null)
                return;
            if (_characterActionWorkspaceCloseConfirm == null)
            {
                _characterActionWorkspaceCloseConfirm = new Fgui.GComponent
                {
                    name = "closeConfirm",
                    sortingOrder = 500,
                };
                _characterActionWorkspaceCloseConfirm.SetSize(620f, 230f);
                var background = new Fgui.GGraph { touchable = false };
                background.DrawRect(620f, 230f, 2,
                    new Color32(88, 145, 156, 255),
                    new Color32(5, 22, 30, 252));
                _characterActionWorkspaceCloseConfirm.AddChild(background);
                var title = new Fgui.GTextField
                {
                    text = "当前动作有未保存修改",
                    color = new Color32(236, 245, 246, 255),
                    align = Fgui.AlignType.Center,
                };
                title.textFormat.size = 22;
                title.textFormat.bold = true;
                title.SetXY(20f, 24f);
                title.SetSize(580f, 44f);
                _characterActionWorkspaceCloseConfirm.AddChild(title);
                AddCloseConfirmButton("保存并返回", 28f, () =>
                {
                    ExecuteWorkspaceCommand(CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SaveAll);
                    if (_workspaceSnapshot?.HasErrors != true
                        && _workspaceSnapshot?.Dirty != true)
                        CloseEmbeddedWorkspace();
                });
                AddCloseConfirmButton("放弃并返回", 228f, () =>
                    CloseEmbeddedWorkspace(discardChanges: true));
                AddCloseConfirmButton("取消", 428f, HideWorkspaceCloseConfirmation);
            }
            _characterActionWorkspaceCloseConfirm.SetXY(
                (_characterActionWorkspace.width - 620f) * 0.5f,
                (_characterActionWorkspace.height - 230f) * 0.5f);
            if (_characterActionWorkspaceCloseConfirm.parent == null)
                _characterActionWorkspace.AddChild(_characterActionWorkspaceCloseConfirm);
        }

        private void AddCloseConfirmButton(string title, float x, Action action)
        {
            string componentName = string.Equals(title, "保存并返回", StringComparison.Ordinal)
                ? "ToolPrimaryButton"
                : "ToolNeutralButton";
            Fgui.GButton button = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                componentName)?.asButton ?? new Fgui.GButton();
            button.title = title;
            button.titleFontSize = 18;
            button.SetXY(x, 126f);
            button.SetSize(164f, 48f);
            button.onClick.Set(() => action?.Invoke());
            _characterActionWorkspaceCloseConfirm.AddChild(button);
        }

        private void HideWorkspaceCloseConfirmation()
        {
            _characterActionWorkspaceCloseConfirm?.RemoveFromParent();
        }

        private void CloseEmbeddedWorkspace(bool discardChanges = false)
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            if (sink != null
                && sink.PrepareCharacterActionWorkspaceReturn(discardChanges)
                    != CharacterTestRuntimeAiLabNavigationExitDisposition.Proceed)
            {
                return;
            }
            CloseWorkspaceAddMenu();
            CloseWorkspaceFieldChoiceMenu();
            HideWorkspaceCloseConfirmation();
            SetCharacterActionWorkspacePageObstructionsHidden(
                ResolveCharacterActionWorkspacePageRoot(),
                hidden: false);
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceClose,
                discardChanges);
            _characterActionWorkspace?.RemoveFromParent();
            _workspaceSnapshot = null;
        }

        internal bool IsCharacterActionWorkspaceMountedInCurrentPageForTests =>
            _characterActionWorkspace?.parent != null
            && ReferenceEquals(
                _characterActionWorkspace.parent,
                ResolveCharacterActionWorkspacePageRoot());

        internal bool TryCloseCleanCharacterActionWorkspaceForTests()
        {
            if (_workspaceSnapshot?.Dirty != false)
                return false;
            CloseEmbeddedWorkspace();
            return _workspaceSnapshot == null && _characterActionWorkspace?.parent == null;
        }

        internal bool TryPlayCharacterActionWorkspaceForTests()
        {
            Fgui.GButton play = _characterActionWorkspace?.GetChild("btnPreviewPlay")?.asButton;
            play?.onClick.Call();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            return play != null
                && sink?.CharacterActionWorkspace?.PreviewActive == true;
        }

        internal bool TryStopCharacterActionWorkspaceForTests()
        {
            Fgui.GButton stop = _characterActionWorkspace?.GetChild("btnPreviewStop")?.asButton;
            stop?.onClick.Call();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink =
                _commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            return stop != null
                && sink?.CharacterActionWorkspace?.Available == true
                && sink.CharacterActionWorkspace.PreviewActive == false;
        }

        internal bool TryRemountCharacterActionWorkspaceForTests()
        {
            _characterActionWorkspace?.RemoveFromParent();
            RestoreEmbeddedCharacterActionWorkspaceIfOpen();
            return IsCharacterActionWorkspaceMountedInCurrentPageForTests;
        }

        private Fgui.GComponent ResolveCharacterActionWorkspacePageRoot()
        {
            return _root?.parent?.parent;
        }

        private void ConfigureCharacterActionWorkspacePageLayout(float pageWidth, float pageHeight)
        {
            if (_characterActionWorkspace == null)
                return;

            string[] hidden =
            {
                "actionQuery", "actionList",
                "btnCreateAction", "btnDuplicateAction", "btnDeleteAction",
                "previewTitle", "previewFrame", "scenePreviewGuide",
                "previewStateOverlay", "previewStateTitle", "previewStateDetail",
                "btnPreviewRetry", "scenePreviewHint", "btnPreviewFit",
                "problemsTitle", "problemsList",
                "leftPanel", "sourceContextTitle", "sourceContextCard", "sourceContextRoute", "sourceContextHint"
            };
            for (int i = 0; i < hidden.Length; i++)
            {
                Fgui.GObject child = _characterActionWorkspace.GetChild(hidden[i]);
                if (child != null)
                    child.visible = false;
            }

            Fgui.GComponent page = ResolveCharacterActionWorkspacePageRoot();
            Fgui.GComponent timelineWorkspace = page?.GetChild("runtimeFrameWorkspace")?.asCom;
            Fgui.GObject formalInspector = timelineWorkspace?.GetChild("timelineEventDetails")
                ?? timelineWorkspace?.GetChild("behaviorRecipeInspector")
                ?? timelineWorkspace?.GetChild("behaviorTriggerEventInspector");
            float formalInspectorX = formalInspector == null
                ? -1f
                : (timelineWorkspace?.x ?? 0f) + formalInspector.x;
            float formalInspectorWidth = formalInspector?.width ?? 0f;
            float inspectorX = ResolveWorkspaceInspectorX(
                pageWidth,
                formalInspectorX,
                formalInspectorWidth);
            float inspectorWidth = pageWidth - inspectorX;
            float timelineX = 0f;
            float timelinePanelWidth = Math.Max(700f, inspectorX);
            float workspaceSurfaceY = WorkspacePageToolbarY - 16f;
            float workspaceSurfaceHeight = Math.Max(1f, pageHeight - workspaceSurfaceY);
            float timelineContentX = 16f;
            float timelineContentWidth = timelinePanelWidth - 32f;
            _workspaceTimelineWidth = Math.Max(
                500f,
                timelineContentWidth - WorkspaceTrackLabelWidth - 24f);

            SetWorkspaceObjectLayout(
                "background",
                0f,
                workspaceSurfaceY,
                timelinePanelWidth,
                workspaceSurfaceHeight,
                true);
            Fgui.GGraph workspaceBackground =
                _characterActionWorkspace.GetChild("background")?.asGraph;
            if (workspaceBackground != null)
                workspaceBackground.touchable = true;

            SetWorkspaceObjectLayout(
                "toolbar",
                timelineX,
                WorkspacePageToolbarY,
                timelinePanelWidth,
                68f,
                true);
            SetWorkspaceObjectLayout(
                "timelinePanel",
                timelineX,
                WorkspacePageTimelineY,
                timelinePanelWidth,
                Math.Max(1f, pageHeight - WorkspacePageTimelineY),
                true);
            SetWorkspaceObjectLayout(
                "rightPanel",
                inspectorX,
                50f,
                inspectorWidth,
                Math.Max(1f, pageHeight - 50f),
                true);
            SetWorkspaceObjectLayout(
                "leftPanel",
                0f,
                0f,
                1f,
                1f,
                false);

            float stopX = inspectorX - 76f;
            float stepX = stopX - 76f;
            float playX = stepX - 76f;
            float saveX = playX - 96f;
            float validateX = saveX - 82f;
            float redoX = validateX - 72f;
            float undoX = redoX - 72f;
            SetWorkspaceObjectLayout("btnBack", 16f, 578f, 140f, 48f, true);
            SetWorkspaceObjectLayout(
                "title",
                168f,
                574f,
                Math.Max(180f, undoX - 180f),
                30f,
                true);
            SetWorkspaceObjectLayout(
                "evidence",
                168f,
                606f,
                Math.Max(180f, undoX - 180f),
                24f,
                true);
            SetWorkspaceObjectLayout("btnUndo", undoX, 578f, 66f, 48f, true);
            SetWorkspaceObjectLayout("btnRedo", redoX, 578f, 66f, 48f, true);
            SetWorkspaceObjectLayout("btnValidate", validateX, 578f, 76f, 48f, true);
            SetWorkspaceObjectLayout("btnSave", saveX, 578f, 90f, 48f, true);
            SetWorkspaceObjectLayout("btnPreviewPlay", playX, 578f, 70f, 48f, true);
            SetWorkspaceObjectLayout("btnPreviewStep", stepX, 578f, 70f, 48f, true);
            SetWorkspaceObjectLayout("btnPreviewStop", stopX, 578f, 70f, 48f, true);
            SetWorkspaceObjectLayout("status", 0f, 0f, 1f, 1f, false);

            float deleteItemWidth = 104f;
            float fitWidth = 60f;
            float zoomWidth = 60f;
            float deleteItemX = inspectorX - deleteItemWidth - 16f;
            float fitX = deleteItemX - fitWidth - 8f;
            float zoomInX = fitX - zoomWidth - 6f;
            float zoomOutX = zoomInX - zoomWidth - 6f;

            SetWorkspaceObjectLayout("trackToolbarText", timelineContentX, 646f, 110f, 36f, true);
            SetWorkspaceObjectLayout("btnAddPhase", timelineContentX + 120f, 644f, 126f, 42f, true);
            SetWorkspaceObjectLayout("btnAddTransition", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnAddMotion", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnAddAnimation", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnAddCombat", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnAddGameplay", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnAddPresentation", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("btnDeleteItem", deleteItemX, 644f, deleteItemWidth, 42f, true);
            SetWorkspaceObjectLayout("btnFit", fitX, 644f, fitWidth, 42f, true);
            SetWorkspaceObjectLayout("btnZoomIn", zoomInX, 644f, zoomWidth, 42f, true);
            SetWorkspaceObjectLayout("btnZoomOut", zoomOutX, 644f, zoomWidth, 42f, true);
            SetWorkspaceObjectLayout(
                "trackList",
                timelineContentX,
                694f,
                timelineContentWidth,
                Math.Max(1f, pageHeight - 704f),
                true);

            float inspectorContentX = inspectorX + 16f;
            float inspectorContentWidth = inspectorWidth - 32f;
            float inspectorTabWidth = Math.Max(78f, (inspectorContentWidth - 136f) / 3f);
            SetWorkspaceObjectLayout(
                "inspectorTitle",
                inspectorContentX,
                60f,
                inspectorContentWidth,
                36f,
                true);
            SetWorkspaceObjectLayout(
                "btnProblemsAll",
                inspectorContentX,
                104f,
                inspectorTabWidth,
                42f,
                true);
            SetWorkspaceObjectLayout(
                "btnProblemsAction",
                inspectorContentX + inspectorTabWidth + 4f,
                104f,
                inspectorTabWidth,
                42f,
                true);
            SetWorkspaceObjectLayout(
                "btnProblemsSource",
                inspectorContentX + (inspectorTabWidth + 4f) * 2f,
                104f,
                inspectorTabWidth,
                42f,
                true);
            SetWorkspaceObjectLayout(
                "btnProblemsReferences",
                inspectorX + inspectorWidth - 128f,
                104f,
                120f,
                42f,
                true);
            SetWorkspaceObjectLayout(
                "inspectorList",
                inspectorContentX,
                154f,
                inspectorContentWidth,
                Math.Max(1f, pageHeight - 164f),
                true);

            SetWorkspaceObjectLayout("sourceContextTitle", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("sourceContextCard", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("sourceContextRoute", 0f, 0f, 1f, 1f, false);
            SetWorkspaceObjectLayout("sourceContextHint", 0f, 0f, 1f, 1f, false);

            SetWorkspaceButtonTitle("btnBack", "← 返回事件");
            SetWorkspaceButtonTitle("btnUndo", "撤销");
            SetWorkspaceButtonTitle("btnRedo", "重做");
            SetWorkspaceButtonTitle("btnValidate", "校验");
            SetWorkspaceButtonTitle("btnSave", "保存");
            SetWorkspaceButtonTitle("btnPreviewStep", "单帧");
            SetWorkspaceButtonTitle("btnPreviewStop", "停止");
            SetWorkspaceButtonTitle("btnAddPhase", "+ 添加条目");
            SetWorkspaceButtonTitle("btnDeleteItem", "删除片段");
            SetWorkspaceButtonTitle("btnFit", "适应");
            SetWorkspaceButtonTitle("btnZoomIn", "放大");
            SetWorkspaceButtonTitle("btnZoomOut", "缩小");

            SetWorkspaceButtonFontSize("btnBack", 17);
            SetWorkspaceButtonFontSize("btnUndo", 16);
            SetWorkspaceButtonFontSize("btnRedo", 16);
            SetWorkspaceButtonFontSize("btnValidate", 16);
            SetWorkspaceButtonFontSize("btnSave", 17);
            SetWorkspaceButtonFontSize("btnPreviewPlay", 16);
            SetWorkspaceButtonFontSize("btnPreviewStep", 16);
            SetWorkspaceButtonFontSize("btnPreviewStop", 16);
            SetWorkspaceButtonFontSize("btnAddPhase", 16);
            SetWorkspaceButtonFontSize("btnDeleteItem", 16);
            SetWorkspaceButtonFontSize("btnFit", 16);
            SetWorkspaceButtonFontSize("btnZoomIn", 16);
            SetWorkspaceButtonFontSize("btnZoomOut", 16);

            RedrawWorkspacePagePanel("background", 7, 15, 22);
            RedrawWorkspacePagePanel("toolbar", 18, 24, 30);
            RedrawWorkspacePagePanel("timelinePanel", 13, 17, 22);
            RedrawWorkspacePagePanel("rightPanel", 16, 21, 27);
            RedrawWorkspacePagePanel("leftPanel", 14, 19, 24);
        }

        internal static float ResolveWorkspaceInspectorX(
            float pageWidth,
            float formalInspectorX,
            float formalInspectorWidth)
        {
            float safePageWidth = Math.Max(1f, pageWidth);
            bool formalGeometryValid = formalInspectorX >= safePageWidth * 0.5f
                && formalInspectorX < safePageWidth
                && formalInspectorWidth >= WorkspaceInspectorMinimumWidth
                && formalInspectorX + formalInspectorWidth <= safePageWidth + 1f;
            if (formalGeometryValid)
                return formalInspectorX;
            float fallbackWidth = Mathf.Clamp(
                safePageWidth * (520f / 1920f),
                WorkspaceInspectorMinimumWidth,
                WorkspaceInspectorMaximumWidth);
            return Math.Max(0f, safePageWidth - fallbackWidth);
        }

        private void SetWorkspaceButtonTitle(string name, string title)
        {
            Fgui.GButton button = _characterActionWorkspace?.GetChild(name)?.asButton;
            if (button != null)
                button.title = title;
        }

        private void SetWorkspaceButtonFontSize(string name, int fontSize)
        {
            Fgui.GButton button = _characterActionWorkspace?.GetChild(name)?.asButton;
            if (button != null)
                button.titleFontSize = fontSize;
        }

        private void SetCharacterActionWorkspacePageObstructionsHidden(
            Fgui.GComponent page,
            bool hidden)
        {
            if (page == null)
                return;
            if (!hidden)
            {
                foreach (KeyValuePair<Fgui.GObject, bool> pair in _workspacePageVisibility)
                {
                    if (pair.Key?.displayObject != null && !pair.Key.displayObject.isDisposed)
                        pair.Key.visible = pair.Value;
                }
                _workspacePageVisibility.Clear();
                return;
            }

            HidePageObject(page.GetChild("behaviorCardTimeline"));
            HidePageObject(page.GetChild("draftActionBar"));
            HidePageObject(page.GetChild("timelineModeHost"));
            HidePageObject(page.GetChild("btnTimelineModeRuntime"));
            HidePageObject(page.GetChild("btnTimelineModePlan"));
            HidePageObject(page.GetChild("btnTimelineModeLogic"));
            HidePageObject(page.GetChild("workbenchSummaryText"));
            HidePageObject(page.GetChild("workbenchDetailText"));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb));
            HidePageObject(page.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack));
            Fgui.GComponent timelineWorkspace = page.GetChild("runtimeFrameWorkspace")?.asCom;
            HidePageObject(timelineWorkspace?.GetChild("timelineEventDetails"));
            HidePageObject(timelineWorkspace?.GetChild("behaviorRecipeInspector"));
            HidePageObject(timelineWorkspace?.GetChild("runtimeFrameFiveTrackViewport"));
        }

        private void HidePageObject(Fgui.GObject target)
        {
            if (target == null)
                return;
            if (!_workspacePageVisibility.ContainsKey(target))
                _workspacePageVisibility.Add(target, target.visible);
            target.visible = false;
        }

        private void SetWorkspaceObjectLayout(
            string name,
            float x,
            float y,
            float width,
            float height,
            bool visible)
        {
            Fgui.GObject child = _characterActionWorkspace?.GetChild(name);
            if (child == null)
                return;
            child.visible = visible;
            child.SetXY(x, y);
            child.SetSize(width, height);
        }

        private void RedrawWorkspacePagePanel(string name, byte red, byte green, byte blue)
        {
            Fgui.GGraph graph = _characterActionWorkspace?.GetChild(name)?.asGraph;
            if (graph == null)
                return;
            graph.DrawRect(
                graph.width,
                graph.height,
                0,
                Color.clear,
                WorkspaceSrgb(red, green, blue));
        }

        private static Color WorkspaceSrgb(byte red, byte green, byte blue)
        {
            return new Color(
                Mathf.GammaToLinearSpace(red / 255f),
                Mathf.GammaToLinearSpace(green / 255f),
                Mathf.GammaToLinearSpace(blue / 255f),
                1f);
        }

        private void ConfigureWorkspaceList(string name, Fgui.ListItemRenderer renderer)
        {
            Fgui.GList list = _characterActionWorkspace?.GetChild(name)?.asList;
            if (list == null)
                return;
            list.layout = Fgui.ListLayoutType.SingleColumn;
            if (string.Equals(name, "trackList", StringComparison.Ordinal))
                list.lineGap = 2;
            list.itemRenderer = renderer;
        }

        private void BindWorkspaceButton(string name, Action action)
        {
            Fgui.GButton button = _characterActionWorkspace?.GetChild(name)?.asButton;
            if (button != null)
                button.onClick.Set(() => action?.Invoke());
        }

        private void SetWorkspaceButtonEnabled(string name, bool enabled)
        {
            Fgui.GButton button = _characterActionWorkspace?.GetChild(name)?.asButton;
            if (button != null)
                button.enabled = enabled;
        }

        private void SetWorkspaceText(string name, string value)
        {
            Fgui.GTextField text = _characterActionWorkspace?.GetChild(name)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void SetWorkspaceListCount(string name, int count)
        {
            Fgui.GList list = _characterActionWorkspace?.GetChild(name)?.asList;
            if (list == null)
                return;
            int target = Math.Max(0, count);
            if (list.numItems == target)
            {
                list.numItems = 0;
                list.numItems = target;
            }
            else
            {
                list.numItems = target;
            }
        }

        private static void SetChildText(Fgui.GComponent component, string name, string value)
        {
            Fgui.GTextField text = component?.GetChild(name)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static Color32 UnpackWorkspaceColor(uint value) => new Color32(
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);
    }
}
