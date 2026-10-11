using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const string RecipeTrackComponentName = "RuntimeAiLabTimelineTrack";
        private const string RecipeSpanComponentName = "RuntimeAiLabTimelineSpan";
        private const string RecipeMarkerComponentName = "RuntimeAiLabTimelineMarker";
        private const float RecipeHeaderHeight = 0f;
        private const float RecipeRulerHeight = 40f;
        // Rows remain an internal collision-free layout detail; the editor exposes
        // Events directly and therefore does not reserve a visible Track-label column.
        private const float RecipeContentInset = 12f;
        private const float RecipeTrackHeight = 50f;
        private const float RecipeTimelineGap = 8f;
        private const float RecipeScrollbarThickness = 8f;
        private const float RecipeEventHeight = 38f;
        private const float RecipeEventY = 6f;
        private const float RecipeMinimumSpanWidth = 96f;
        private const float RecipeInstantCardWidth = 176f;
        private const float RecipePrimaryActionMinimumSpanWidth = 360f;
        private const float RecipeOpenSpanMinimumWidth = 176f;
        private const float RecipeBranchMinimumSpanWidth = 320f;
        private const float RecipeResizeHandleWidth = 14f;
        private const float RecipeVerticalWheelStep = 72f;
        private const float RecipeFollowThreshold = 0.8f;
        private const float RecipeFollowTarget = 0.65f;
        private const int RecipeFrameSnap = 1;
        private const int RecipePlayheadFrameTextCacheLimit = 4096;
        private static readonly string[] RecipePlayheadFrameText =
            CreateRecipePlayheadFrameText();

        private sealed class RecipeTrackSlot
        {
            internal Fgui.GComponent Visual;
            internal Fgui.GTextField Label;
            internal Fgui.GComponent Content;
            internal Fgui.GComponent Span;
            internal Fgui.GGraph SpanHitArea;
            internal Fgui.GComponent Marker;
            internal Fgui.GGraph MarkerCardBackground;
            internal Fgui.GTextField MarkerLabel;
            internal Fgui.GGraph ProgressFill;
            internal Fgui.GGraph ResizeHandle;
            internal Fgui.GGraph ResultBadgeBackground;
            internal Fgui.GTextField ResultBadge;
            internal Fgui.GObject LaneBackground;
            internal Fgui.GGraph RuntimeLaneBackground;
            internal BehaviorRecipeTimelineViewModel Timeline;
            internal BehaviorRecipeTrackViewModel Track;
            internal BehaviorRecipeEventViewModel Event;
        }

        private sealed class RecipeMenuItemSlot
        {
            internal Fgui.GComponent Visual;
            internal Fgui.GGraph Background;
            internal Fgui.GTextField Label;
            internal Action Action;
            internal int Index;
            internal bool Destructive;
        }

        private enum RecipeGestureKind
        {
            None,
            MoveBranch,
            ResizeBranch,
            ResizeWait
        }

        private readonly BehaviorRecipeTimelineProjection _recipeProjection =
            new BehaviorRecipeTimelineProjection();
        private readonly List<RecipeTrackSlot> _recipeTrackPool =
            new List<RecipeTrackSlot>(48);
        private readonly List<Fgui.GGraph> _recipeGridPool = new List<Fgui.GGraph>(32);
        private readonly List<Fgui.GGraph> _recipeTimelineEndPool = new List<Fgui.GGraph>(16);
        private readonly List<Fgui.GTextField> _recipeRulerLabelPool =
            new List<Fgui.GTextField>(16);
        private readonly List<RecipeMenuItemSlot> _recipeMenuItems =
            new List<RecipeMenuItemSlot>(8);
        private string _recipeCopiedTimelineStageId = string.Empty;
        private string _recipeCopiedTimelineTitle = string.Empty;

        private Fgui.GComponent _recipeLayer;
        private Fgui.GGraph _recipeBackground;
        private Fgui.GGraph _recipeRulerBackground;
        private Fgui.GTextField _recipeRulerTitle;
        private Fgui.GGraph _recipeFeedbackBackground;
        private Fgui.GTextField _recipeFeedbackText;
        private Fgui.GComponent _recipeViewport;
        private Fgui.GComponent _recipeRowsLayer;
        private Fgui.GGraph _recipePlayhead;
        private Fgui.GGraph _recipePlayheadHead;
        private Fgui.GTextField _recipePlayheadLabel;
        private Fgui.GGraph _recipeDragStartGuideLine;
        private Fgui.GTextField _recipeDragStartGuideLabel;
        private Fgui.GGraph _recipeDragEndGuideLine;
        private Fgui.GTextField _recipeDragEndGuideLabel;
        private Fgui.GGraph _recipeHorizontalScrollTrack;
        private Fgui.GGraph _recipeHorizontalScrollThumb;
        private Fgui.GGraph _recipeVerticalScrollTrack;
        private Fgui.GGraph _recipeVerticalScrollThumb;
        private Fgui.GComponent _recipeContextMenu;
        private Fgui.GGraph _recipeContextMenuBackground;
        private CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel _recipeSource;
        private float _recipeVerticalOffset;
        private float _recipeContentExtent;
        private int _recipeVisibleTrackCount;
        private int _recipeVisibleGridCount;
        private int _recipeVisibleTimelineEndCount;
        private int _recipeVisibleRulerLabelCount;
        private RecipeTrackSlot _recipeGestureSlot;
        private RecipeGestureKind _recipeGestureKind;
        private float _recipeGestureStartX;
        private float _recipeGestureGrabOffsetX;
        private float _recipeGestureBaseX;
        private float _recipeGestureBaseWidth;
        private float _recipeGestureBaseMarkerCardX;
        private float _recipeGestureBaseMarkerLabelX;
        private float _recipeGestureBaseResizeHandleX;
        private float _recipeGestureBaseSpanHitAreaX;
        private float _recipeGestureBaseSpanHitAreaWidth;
        private int _recipeGestureBaseStartFrame;
        private int _recipeGestureBaseDurationFrames;
        private int _recipeGesturePreviewStartFrame;
        private int _recipeGesturePreviewDurationFrames;
        private bool _recipeGestureMoved;
        private string _recipeLocalFeedbackText = string.Empty;
        private string _sequentialFitKey;
        private readonly Vector2[] _recipePlayheadHeadPoints = new Vector2[3];
        private Action<Fgui.GObject, Action<string>> _showCharacterActionPicker;

        internal void SetCharacterActionPicker(
            Action<Fgui.GObject, Action<string>> showCharacterActionPicker)
        {
            _showCharacterActionPicker = showCharacterActionPicker;
        }

        private void BindRecipeTimeline(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            // Undo/redo remain Composer-owned controls on the formal timeline root.
            // Bind them before projecting the recipe host so Track/Event editing keeps
            // the existing command path instead of creating a second authority.
            EnsureStaticHandlers();
            EnsureRecipeLayer();
            if (_recipeLayer == null)
                return;
            _behaviorLogicMode = true;
            _recipeSource = viewModel;
            _recipeLocalFeedbackText = string.Empty;
            _recipeLayer.visible = true;
            HideRecipeContextMenu();
            // Tactical composition owns its event selection and edit session. Its
            // standalone projection can legitimately have no legacy Stages, so
            // clear any old Stage session idempotently and leave reconciliation
            // to the tactical editor. Ordinary Behavior Logic
            // continues through the legacy Stage editing path below.
            if (viewModel.CompositionEditor != null)
                _commands?.CancelStageEditSessions(clearSelection: true);
            else
                _commands?.ReconcileStageEditing(viewModel);
            _canEdit = viewModel.CanEdit && !viewModel.RequiresDetectorWindowCanonicalization;
            _boundStages = viewModel.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            _boundDetectors = viewModel.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            _boundTransitions = viewModel.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            _activeStageCount = Math.Min(viewModel.StageCount, _boundStages.Length);
            _boundDetectorCount = Math.Min(viewModel.DetectorCount, _boundDetectors.Length);
            _boundTransitionCount = Math.Min(viewModel.TransitionCount, _boundTransitions.Length);
            ResolveSelectedToken(
                viewModel,
                _boundStages,
                _activeStageCount,
                _boundDetectors,
                _boundDetectorCount,
                _boundTransitions,
                _boundTransitionCount);
            _recipeProjection.Rebuild(viewModel);
            if (viewModel.CompositionEditor != null && viewModel.TacticalPreview?.Ready == true
                && (viewModel.TacticalPreview.WholeCard != null || !viewModel.TacticalPreview.IsConditionProjection))
            {
                string key = viewModel.CompositionEditor.CardId + "/" + (viewModel.TacticalPreview.WholeCard != null
                    ? viewModel.TacticalPreview.WholeCard.Program.SemanticHash.ToString() : viewModel.CompositionEditor.SelectedNodeId);
                if (_sequentialFitKey != key)
                {
                    _sequentialFitKey = key;
                    Fgui.Timers.inst.CallLater(ApplyInitialSequentialViewport, key);
                }
            }
            _recipeVerticalOffset = Mathf.Max(
                0f,
                Math.Max(_recipeVerticalOffset, viewModel.VerticalScrollOffset));
            // Tactical layout commits its final bounds later in the parent bind. Clamping
            // scroll against the temporary ordinary-workbench bounds loses the user's offset.
            if (viewModel.CompositionEditor == null)
                RefreshRecipeGeometry();
        }

        private void ApplyInitialSequentialViewport(object key)
        {
            if (_root == null || _root.isDisposed || _recipeSource?.CompositionEditor == null
                || !string.Equals(key as string, _sequentialFitKey, StringComparison.Ordinal)) return;
            _recipeSource.HorizontalStartFrame = 0;
            _recipeSource.HorizontalEndFrame = Math.Max(60, _recipeProjection.MaximumLocalFrame + 15);
            _recipeSource.ZoomLevel = 1f;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

        internal void RefreshRecipeGeometry()
        {
            if (_root == null || _recipeLayer == null || _recipeSource == null) return;
            ConfigureRecipeRootGeometry();
            ConfigureRecipeCommandRail();
            RedrawRecipeTimeline();
        }

        private void HideRecipeTimeline()
        {
            if (_recipeLayer != null)
                _recipeLayer.visible = false;
            _recipeSource = null;
            CancelRecipeGesture();
            HideRecipeContextMenu();
        }

        private void ResetRecipeForNewRoot()
        {
            _recipeLayer = null;
            _recipeBackground = null;
            _recipeRulerBackground = null;
            _recipeRulerTitle = null;
            _recipeFeedbackBackground = null;
            _recipeFeedbackText = null;
            _recipeViewport = null;
            _recipeRowsLayer = null;
            _recipePlayhead = null;
            _recipePlayheadHead = null;
            _recipePlayheadLabel = null;
            _recipeDragStartGuideLine = null;
            _recipeDragStartGuideLabel = null;
            _recipeDragEndGuideLine = null;
            _recipeDragEndGuideLabel = null;
            _recipeHorizontalScrollTrack = null;
            _recipeHorizontalScrollThumb = null;
            _recipeVerticalScrollTrack = null;
            _recipeVerticalScrollThumb = null;
            _recipeContextMenu = null;
            _recipeContextMenuBackground = null;
            _recipeTrackPool.Clear();
            _recipeGridPool.Clear();
            _recipeTimelineEndPool.Clear();
            _recipeRulerLabelPool.Clear();
            _recipeMenuItems.Clear();
            _recipeSource = null;
            _recipeVisibleTrackCount = 0;
            _recipeVisibleGridCount = 0;
            _recipeVisibleTimelineEndCount = 0;
            _recipeVisibleRulerLabelCount = 0;
            _recipeVerticalOffset = 0f;
            _recipeContentExtent = 0f;
            _recipeLocalFeedbackText = string.Empty;
            CancelRecipeGesture();
        }

        private void EnsureRecipeLayer()
        {
            if (_recipeLayer != null || _root == null)
                return;
            _recipeLayer = _root.GetChild("timelineGroupHost")?.asCom;
            if (_recipeLayer == null)
            {
                _recipeLayer = new Fgui.GComponent
                {
                    name = "timelineGroupHost",
                    touchable = true,
                    sortingOrder = 200
                };
                _root.AddChild(_recipeLayer);
            }
            _recipeLayer.sortingOrder = 200;
            _recipeLayer.RemoveChildren();
            _recipeBackground = NewRecipeGraph("recipeBackground", false);
            _recipeRulerBackground = NewRecipeGraph("recipeRulerBackground", false);
            _recipeRulerTitle = NewRecipeText(
                "recipeRulerTitle", "阶段帧", 14, new Color32(130, 218, 217, 255), Fgui.AlignType.Left, true);
            _recipeFeedbackBackground = NewRecipeGraph("recipeFeedbackBackground", false);
            _recipeFeedbackText = NewRecipeText(
                "recipeFeedbackText", string.Empty, 14,
                new Color32(214, 242, 241, 255), Fgui.AlignType.Left, true);
            _recipeFeedbackBackground.sortingOrder = 450;
            _recipeFeedbackText.sortingOrder = 451;
            _recipeViewport = new Fgui.GComponent
            {
                name = "recipeViewport",
                touchable = true,
                opaque = true
            };
            _recipeRowsLayer = new Fgui.GComponent { name = "recipeRows", touchable = true };
            _recipeViewport.AddChild(_recipeRowsLayer);
            _recipePlayhead = NewRecipeGraph("recipeLocalPlayhead", false);
            _recipeViewport.AddChild(_recipePlayhead);
            _recipeDragStartGuideLine = NewRecipeGraph("recipeDragStartGuideLine", false);
            _recipeDragStartGuideLine.sortingOrder = 430;
            _recipeDragStartGuideLine.visible = false;
            _recipeViewport.AddChild(_recipeDragStartGuideLine);
            _recipeDragEndGuideLine = NewRecipeGraph("recipeDragEndGuideLine", false);
            _recipeDragEndGuideLine.sortingOrder = 430;
            _recipeDragEndGuideLine.visible = false;
            _recipeViewport.AddChild(_recipeDragEndGuideLine);
            _recipePlayheadHead = NewRecipeGraph("recipeLocalPlayheadHead", false);
            _recipePlayheadHead.sortingOrder = 440;
            _recipePlayheadLabel = NewRecipeText(
                "recipeLocalPlayheadLabel",
                string.Empty,
                14,
                new Color32(112, 245, 235, 255),
                Fgui.AlignType.Center,
                true);
            _recipePlayheadLabel.sortingOrder = 441;
            _recipeDragStartGuideLabel = NewRecipeText(
                "recipeDragStartGuideLabel",
                string.Empty,
                11,
                new Color32(255, 215, 75, 255),
                Fgui.AlignType.Center,
                true);
            _recipeDragStartGuideLabel.sortingOrder = 445;
            _recipeDragStartGuideLabel.visible = false;
            _recipeDragEndGuideLabel = NewRecipeText(
                "recipeDragEndGuideLabel",
                string.Empty,
                11,
                new Color32(80, 235, 225, 255),
                Fgui.AlignType.Center,
                true);
            _recipeDragEndGuideLabel.sortingOrder = 445;
            _recipeDragEndGuideLabel.visible = false;
            _recipeHorizontalScrollTrack = NewRecipeGraph("recipeHorizontalScrollTrack", true);
            _recipeHorizontalScrollThumb = NewRecipeGraph("recipeHorizontalScrollThumb", true);
            _recipeVerticalScrollTrack = NewRecipeGraph("recipeVerticalScrollTrack", true);
            _recipeVerticalScrollThumb = NewRecipeGraph("recipeVerticalScrollThumb", true);
            _recipeContextMenu = new Fgui.GComponent
            {
                name = "recipeContextMenu",
                touchable = true,
                opaque = true,
                sortingOrder = 500,
                visible = false
            };
            _recipeContextMenuBackground = new Fgui.GGraph { name = "background", touchable = false };
            _recipeContextMenu.AddChild(_recipeContextMenuBackground);

            _recipeLayer.AddChild(_recipeBackground);
            _recipeLayer.AddChild(_recipeRulerBackground);
            _recipeLayer.AddChild(_recipeRulerTitle);
            _recipeLayer.AddChild(_recipeFeedbackBackground);
            _recipeLayer.AddChild(_recipeFeedbackText);
            _recipeLayer.AddChild(_recipeViewport);
            _recipeLayer.AddChild(_recipePlayheadHead);
            _recipeLayer.AddChild(_recipePlayheadLabel);
            _recipeLayer.AddChild(_recipeDragStartGuideLabel);
            _recipeLayer.AddChild(_recipeDragEndGuideLabel);
            _recipeLayer.AddChild(_recipeHorizontalScrollTrack);
            _recipeLayer.AddChild(_recipeHorizontalScrollThumb);
            _recipeLayer.AddChild(_recipeVerticalScrollTrack);
            _recipeLayer.AddChild(_recipeVerticalScrollThumb);
            _recipeLayer.AddChild(_recipeContextMenu);
            ConfigureRecipeInputHandlers();
        }

        private void ConfigureRecipeRootGeometry()
        {
            float rootWidth = Math.Max(1f, _root?.width ?? 1f);
            bool sequential = _recipeSource?.CompositionEditor != null && !_recipeSource.InspectCompiledExecution;
            float width = Math.Max(1f, rootWidth - (sequential ? 0f
                : CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailWidth));
            float height = Math.Max(1f, _root?.height ?? 1f);
            float viewportTop = RecipeHeaderHeight + RecipeRulerHeight;
            float viewportHeight = Math.Max(1f, height - viewportTop - 20f);
            _recipeLayer.SetXY(
                sequential ? 0f : CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineContentX,
                0f);
            _recipeLayer.SetSize(width, height);
            _recipeBackground.SetXY(0f, 0f);
            _recipeBackground.DrawRect(width, height, 1,
                new Color32(18, 48, 58, 255), new Color32(6, 17, 24, 255));
            _recipeRulerBackground.SetXY(0f, RecipeHeaderHeight);
            _recipeRulerBackground.DrawRect(width, RecipeRulerHeight, 1,
                new Color32(24, 66, 78, 220), new Color32(10, 26, 35, 255));
            _recipeRulerTitle.visible = false;
            UpdateRecipeFeedbackGeometry(width);
            _recipeViewport.SetXY(0f, viewportTop);
            _recipeViewport.SetSize(width, viewportHeight);
            if (_recipeViewport.rootContainer != null)
                _recipeViewport.rootContainer.clipRect = new Rect(0f, 0f, width, viewportHeight);
            _recipeRowsLayer.SetXY(0f, 0f);
            _recipeRowsLayer.SetSize(width, Math.Max(viewportHeight, _recipeContentExtent));
            _recipeHorizontalScrollTrack.SetXY(
                RecipeContentInset,
                height - RecipeScrollbarThickness - 2f);
            _recipeHorizontalScrollTrack.DrawRect(
                Math.Max(1f, width - RecipeContentInset - 18f),
                RecipeScrollbarThickness,
                0,
                Color.clear,
                new Color32(10, 36, 44, 200));
            _recipeVerticalScrollTrack.SetXY(width - RecipeScrollbarThickness - 2f, viewportTop);
            _recipeVerticalScrollTrack.DrawRect(
                RecipeScrollbarThickness,
                viewportHeight,
                0,
                Color.clear,
                new Color32(10, 36, 44, 200));
        }

        private void ConfigureRecipeCommandRail()
        {
            if (_root == null)
                return;
            if (_recipeSource?.CompositionEditor != null && !_recipeSource.InspectCompiledExecution)
            {
                SetObjectVisible("background", true);
                _root.GetChild("background")?.SetSize(_root.width, _root.height);
                foreach (string name in new[] { "toolbar", "timelineTitle", "timelineValidation", "btnTimelineUndo", "btnTimelineRedo",
                    "btnTimelineAddStage", "btnTimelineAddCardDetector", "btnTimelineCanonicalize", "btnTimelineDelete", "btnTimelineDuplicate",
                    "btnTimelineSnap", "timelineSnapState", "btnTimelinePanLeft", "btnTimelinePanRight", "btnTimelineScrollUp", "btnTimelineScrollDown",
                    "btnTimelineZoomOut", "btnTimelineZoomIn", "btnTimelineFit", "rulerBackground", "timelineRuntimeStatus", "timelinePlayhead", "timelineEmpty" })
                    SetObjectVisible(name, false);
                return;
            }
            float commandWidth = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicTopCommandWidth;
            float commandHeight = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicFloatingControlHeight;
            float commandX = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicCommandRailButtonX;

            SetObjectVisible("background", true);
            Fgui.GObject background = _root.GetChild("background");
            background?.SetSize(_root.width, _root.height);

            SetObjectVisible("toolbar", true);
            Fgui.GObject rail = _root.GetChild("toolbar");
            if (rail != null)
            {
                rail.relations?.ClearAll();
                rail.SetXY(0f, 0f);
                rail.SetSize(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailWidth,
                    _root.height);
                rail.sortingOrder = 201;
            }

            SetObjectVisible("timelineTitle", true);
            Fgui.GTextField title = _root.GetChild("timelineTitle")?.asTextField;
            if (title != null)
            {
                title.relations?.ClearAll();
                title.text = "行为配方";
                title.align = Fgui.AlignType.Center;
                title.verticalAlign = Fgui.VertAlignType.Middle;
                title.textFormat.size = 17;
                title.SetXY(10f, 8f);
                title.SetSize(100f, 22f);
                BindTooltip(title, _recipeSource?.CardTitleText);
                title.touchable = true;
                title.onClick.Set(context =>
                {
                    SelectRecipeActivationConditions();
                    context.StopPropagation();
                });
                title.sortingOrder = 202;
            }

            SetObjectVisible("timelineValidation", true);
            Fgui.GTextField validation = _root.GetChild("timelineValidation")?.asTextField;
            if (validation != null)
            {
                validation.relations?.ClearAll();
                validation.text = ResolveRecipeSyncBadge(_recipeSource);
                validation.align = Fgui.AlignType.Center;
                validation.verticalAlign = Fgui.VertAlignType.Middle;
                validation.textFormat.size = 14;
                validation.color = _recipeSource?.HasBlockingDiagnostics == true
                    ? new Color32(240, 91, 96, 255)
                    : new Color32(69, 222, 140, 255);
                validation.SetXY(10f, 32f);
                validation.SetSize(100f, 20f);
                string validationTooltip = string.IsNullOrWhiteSpace(_recipeSource?.DiagnosticsText)
                    ? _recipeSource?.ValidationBadgeText ?? string.Empty
                    : _recipeSource.DiagnosticsText;
                if (!string.IsNullOrWhiteSpace(_recipeSource?.RuntimeStatusText))
                    validationTooltip += "\n\n" + _recipeSource.RuntimeStatusText;
                BindTooltip(validation, validationTooltip);
                validation.sortingOrder = 203;
            }

            string[] hiddenControls =
            {
                "btnTimelineAddStage", "btnTimelineAddCardDetector",
                "btnTimelineCanonicalize", "btnTimelineDelete", "btnTimelineDuplicate",
                "btnTimelineSnap", "timelineSnapState", "timelineGesturePreview",
                "btnTimelinePanLeft", "btnTimelinePanRight", "btnTimelineScrollUp",
                "btnTimelineScrollDown", "btnTimelineZoomOut", "btnTimelineZoomIn",
                "btnTimelineFit", "rulerBackground", "timelineRuntimeStatus",
                "timelineHorizontalScrollTrack", "timelineHorizontalScrollThumb",
                "timelineVerticalScrollTrack", "timelineVerticalScrollThumb",
                "timelinePlayhead", "timelineEmpty"
            };
            for (int i = 0; i < hiddenControls.Length; i++)
                SetObjectVisible(hiddenControls[i], false);

            SetObjectVisible("btnTimelineUndo", true);
            SetObjectVisible("btnTimelineRedo", true);
            PositionTimelineButton(
                "btnTimelineUndo",
                commandX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailUndoY,
                commandWidth,
                commandHeight);
            PositionTimelineButton(
                "btnTimelineRedo",
                commandX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailRedoY,
                commandWidth,
                commandHeight);
            SetTimelineButtonFontSize("btnTimelineUndo", 15);
            SetTimelineButtonFontSize("btnTimelineRedo", 15);
            bool historyEditable = _recipeSource?.InspectCompiledExecution != true && (_recipeSource?.TacticalPreview?.Editor?.CanEdit ?? true);
            bool canUndo = historyEditable && (_commands?.CanUndoStageEdit ?? false);
            bool canRedo = historyEditable && (_commands?.CanRedoStageEdit ?? false);
            SetButtonEnabled(_root, "btnTimelineUndo", canUndo);
            SetButtonEnabled(_root, "btnTimelineRedo", canRedo);
            Fgui.GButton undoButton = _root.GetChild("btnTimelineUndo")?.asButton;
            if (undoButton != null)
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(
                    undoButton,
                    canUndo,
                    isPrimary: false);
            }
            Fgui.GButton redoButton = _root.GetChild("btnTimelineRedo")?.asButton;
            if (redoButton != null)
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(
                    redoButton,
                    canRedo,
                    isPrimary: false);
            }

            SetRootSortingOrder("btnTimelineUndo", 204);
            SetRootSortingOrder("btnTimelineRedo", 205);
        }

        private static string ResolveRecipeSyncBadge(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            if (viewModel?.TacticalPreview != null) return viewModel.TacticalPreview.Ready ? "源已编译" : "源未求值";
            if (viewModel?.HasBlockingDiagnostics == true)
                return "需要修复";
            string status = viewModel?.RuntimeStatusText ?? string.Empty;
            if (status.IndexOf("与当前编辑一致", StringComparison.Ordinal) >= 0)
                return "● 已同步";
            if (status.IndexOf("上次保存版本", StringComparison.Ordinal) >= 0)
                return "⚠ 旧草稿";
            if (status.IndexOf("未运行", StringComparison.Ordinal) >= 0
                || status.IndexOf("未装载", StringComparison.Ordinal) >= 0)
                return "○ 未运行";
            return "验证通过";
        }

    }
}
