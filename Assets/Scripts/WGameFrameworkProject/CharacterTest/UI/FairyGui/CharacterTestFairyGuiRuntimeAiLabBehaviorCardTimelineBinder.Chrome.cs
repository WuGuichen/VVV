using System;
using System.Collections.Generic;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void ConfigureBehaviorLogicChrome(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            if (!_behaviorLogicMode)
            {
                Fgui.GObject addStageButton = _root?.GetChild("btnTimelineAddStage");
                if (addStageButton != null)
                    addStageButton.alpha = 1f;
                SetObjectVisible("logicGlobalLaneTitle", false);
                SetObjectVisible("logicGlobalLaneHint", false);
                SetObjectVisible("logicReplanEndpoint", false);
                SetObjectVisible("logicReplanEndpointLabel", false);
                SetObjectVisible("logicEndEndpoint", false);
                SetObjectVisible("logicEndEndpointLabel", false);
                SetObjectVisible("logicEndpointTitle", false);
                SetObjectVisible("logicAddStageFrame", false);
                SetObjectVisible("logicAddStageFill", false);
                SetObjectVisible("logicAddStageHint", false);
                SetObjectVisible("logicRelationshipHint", false);
                return;
            }

            SetObjectVisible("background", true);
            SetObjectVisible("toolbar", false);
            SetObjectVisible("timelineTitle", false);
            SetObjectVisible("btnTimelineAddStage", true);
            SetObjectVisible("btnTimelineAddCardDetector", true);
            SetObjectVisible(
                "btnTimelineCanonicalize",
                viewModel?.RequiresDetectorWindowCanonicalization == true);
            SetObjectVisible("btnTimelineUndo", true);
            SetObjectVisible("btnTimelineRedo", true);
            SetObjectVisible("btnTimelineFit", false);
            SetObjectVisible("timelineValidation", true);
            SetObjectVisible("btnTimelineDelete", false);
            SetObjectVisible("btnTimelineDuplicate", false);
            SetObjectVisible("btnTimelineSnap", false);
            SetObjectVisible("timelineSnapState", false);
            SetObjectVisible("btnTimelinePanLeft", false);
            SetObjectVisible("btnTimelinePanRight", false);
            SetObjectVisible("btnTimelineScrollUp", false);
            SetObjectVisible("btnTimelineScrollDown", false);
            SetObjectVisible("btnTimelineZoomOut", false);
            SetObjectVisible("btnTimelineZoomIn", false);
            SetObjectVisible("rulerBackground", false);
            SetObjectVisible("timelineRuntimeStatus", false);
            SetObjectVisible("timelineHorizontalScrollTrack", true);
            SetObjectVisible("timelineHorizontalScrollThumb", true);
            SetObjectVisible("timelineVerticalScrollTrack", false);
            SetObjectVisible("timelineVerticalScrollThumb", false);
            SetObjectVisible("logicGlobalLaneTitle", true);
            SetObjectVisible("logicGlobalLaneHint", true);
            SetObjectVisible("logicReplanEndpoint", true);
            SetObjectVisible("logicReplanEndpointLabel", true);
            SetObjectVisible("logicEndEndpoint", true);
            SetObjectVisible("logicEndEndpointLabel", true);
            SetObjectVisible("logicEndpointTitle", true);
            SetObjectVisible("logicRelationshipHint", true);

            Fgui.GObject background = _root.GetChild("background");
            background?.SetSize(_root.width, _root.height);
            Fgui.GObject toolbar = _root.GetChild("toolbar");
            if (toolbar != null)
            {
                toolbar.SetXY(0f, 0f);
                toolbar.SetSize(1f, 1f);
            }
            Fgui.GObject title = _root.GetChild("timelineTitle");
            if (title != null)
                BindTooltip(title, viewModel?.CardTitleText);
            // Reserve the right-most top-row slot for the outer panel's Save command.
            // Undo, redo and validation stay in one horizontal group while structure
            // creation is positioned later, beside the stages it affects.
            float topY = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicFloatingControlYInset;
            float saveX = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicTimelineWidth
                - BehaviorLogicToolbarMargin
                - BehaviorLogicControlWidth;
            float redoX = saveX
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicTopCommandGap
                - BehaviorLogicControlWidth;
            float undoX = redoX
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicTopCommandGap
                - BehaviorLogicControlWidth;
            const float validationWidth = 236f;
            float validationX = undoX
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicTopCommandGap
                - validationWidth;
            float canonicalizeX = validationX
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicTopCommandGap
                - BehaviorLogicControlWidth;
            PositionTimelineButton(
                "btnTimelineUndo",
                undoX,
                topY,
                BehaviorLogicControlWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicFloatingControlHeight);
            PositionTimelineButton(
                "btnTimelineRedo",
                redoX,
                topY,
                BehaviorLogicControlWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicFloatingControlHeight);
            PositionTimelineButton(
                "btnTimelineCanonicalize",
                canonicalizeX,
                topY,
                BehaviorLogicControlWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicFloatingControlHeight);
            SetRootSortingOrder("btnTimelineCanonicalize", 107);
            SetRootSortingOrder("btnTimelineUndo", 108);
            SetRootSortingOrder("btnTimelineRedo", 109);
            SetRootSortingOrder("btnTimelineAddStage", 111);
            SetRootSortingOrder("btnTimelineAddCardDetector", 112);
            SetRootSortingOrder("timelineValidation", 113);
            SetRootSortingOrder("logicGlobalLaneTitle", 90);
            SetRootSortingOrder("logicGlobalLaneHint", 90);
            SetRootSortingOrder("logicReplanEndpoint", 90);
            SetRootSortingOrder("logicReplanEndpointLabel", 91);
            SetRootSortingOrder("logicEndEndpoint", 90);
            SetRootSortingOrder("logicEndEndpointLabel", 91);
            SetRootSortingOrder("logicEndpointTitle", 91);
            SetRootSortingOrder("logicAddStageFrame", 6);
            SetRootSortingOrder("logicAddStageFill", 7);
            SetRootSortingOrder("logicAddStageHint", 92);
            SetRootSortingOrder("logicRelationshipHint", 90);
            SetButtonTitle(_root, "btnTimelineAddStage", "+ 添加阶段");
            SetButtonTitle(_root, "btnTimelineAddCardDetector", "+ 添加检测");
            Fgui.GObject addDetector = _root.GetChild("btnTimelineAddCardDetector");
            if (addDetector != null)
            {
                BindTooltip(addDetector, "向当前阶段添加一个检测条件。");
            }
            SetTimelineButtonFontSize("btnTimelineAddStage", 16);
            SetTimelineButtonFontSize("btnTimelineAddCardDetector", 16);
            SetTimelineButtonFontSize("btnTimelineUndo", 18);
            SetTimelineButtonFontSize("btnTimelineRedo", 18);
            SetTimelineButtonFontSize("btnTimelineCanonicalize", 18);
            Fgui.GObject validation = _root.GetChild("timelineValidation");
            string validationText = ResolveBehaviorLogicValidationText(viewModel);
            if (validation != null)
            {
                validation.RemoveRelation(_root, Fgui.RelationType.Right_Right);
                validation.SetXY(
                    validationX,
                    topY);
                validation.SetSize(
                    validationWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicFloatingControlHeight);
                SetTimelineTextFontSize(validation.asTextField, 18);
                BindTooltip(validation, string.IsNullOrWhiteSpace(viewModel?.DiagnosticsText)
                    ? validationText
                    : viewModel.DiagnosticsText);
                if (validation.asTextField != null)
                {
                    validation.asTextField.color = IsBehaviorLogicValidationValid(viewModel)
                            ? new Color32(71, 220, 116, 255)
                            : new Color32(237, 160, 49, 255);
                }
            }
            SetText("timelineValidation", validationText);
            int globalDetectorCount = CountDetectors(
                _boundDetectors,
                _boundDetectorCount,
                -1);
            SetText(
                "logicGlobalLaneHint",
                globalDetectorCount > 0
                    ? "整张行为卡 · " + globalDetectorCount + " 个检测"
                    : "整张行为卡 · 未配置");
            float visibleRootWidth = Math.Min(
                _root.width,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineWidth);
            const float endpointTop = BehaviorLogicGlobalDetectorTop + 2f;
            const float endpointGap = 12f;
            const float endEndpointWidth = 124f;
            const float replanEndpointWidth = 106f;
            float endEndpointX = visibleRootWidth
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCanvasRightPadding
                - endEndpointWidth;
            float replanEndpointX = endEndpointX - endpointGap - replanEndpointWidth;
            // These four objects are positioned from the fixed 1300 px logic dock.
            // Keeping the recipe's legacy right relation would move them again when
            // the shared plan/logic component changes size after a bind, while the
            // connector geometry remains at the coordinates calculated here.
            RemoveRootRelation("logicReplanEndpoint", Fgui.RelationType.Right_Right);
            RemoveRootRelation("logicReplanEndpointLabel", Fgui.RelationType.Right_Right);
            RemoveRootRelation("logicEndEndpoint", Fgui.RelationType.Right_Right);
            RemoveRootRelation("logicEndEndpointLabel", Fgui.RelationType.Right_Right);
            SetObjectXY(_root, "logicGlobalLaneTitle", 20f, BehaviorLogicGlobalDetectorTop + 3f);
            SetObjectXY(_root, "logicGlobalLaneHint", 20f, BehaviorLogicGlobalDetectorTop + 24f);
            SetText("logicEndpointTitle", "行为卡出口");
            SetObjectXY(_root, "logicEndpointTitle", replanEndpointX - 116f, endpointTop + 6f);
            SetObjectSize(_root, "logicEndpointTitle", 104f, 24f);
            SetObjectXY(_root, "logicReplanEndpoint", replanEndpointX, endpointTop);
            SetObjectSize(_root, "logicReplanEndpoint", replanEndpointWidth, 36f);
            SetObjectXY(_root, "logicReplanEndpointLabel", replanEndpointX, endpointTop + 6f);
            SetObjectSize(_root, "logicReplanEndpointLabel", replanEndpointWidth, 24f);
            SetObjectXY(_root, "logicEndEndpoint", endEndpointX, endpointTop);
            SetObjectSize(_root, "logicEndEndpoint", endEndpointWidth, 36f);
            SetObjectXY(_root, "logicEndEndpointLabel", endEndpointX, endpointTop + 6f);
            SetObjectSize(_root, "logicEndEndpointLabel", endEndpointWidth, 24f);
            SetObjectXY(_root, "logicRelationshipHint", 20f, Math.Max(0f, _root.height - 34f));
            SetObjectSize(_root, "logicRelationshipHint", Math.Max(280f, visibleRootWidth - 40f), 18f);
            if (_contentLayer != null)
            {
                visibleRootWidth = _behaviorLogicMode
                    ? CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineWidth
                    : _root.width;
                float contentWidth = Math.Max(
                    1f,
                    visibleRootWidth
                        - CanvasLeft
                        - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicCanvasRightPadding);
                float contentHeight = Math.Max(1f, _root.height);
                _contentLayer.visible = true;
                _contentLayer.SetXY(CanvasLeft, 0f);
                _contentLayer.SetSize(contentWidth, contentHeight);
                _contentLayer.rootContainer.clipRect = new Rect(
                    0f,
                    0f,
                    contentWidth,
                    contentHeight);
            }
            UpdateBehaviorLogicHorizontalScrollBar();
        }

        private void RemoveRootRelation(string childName, Fgui.RelationType relationType)
        {
            Fgui.GObject child = _root?.GetChild(childName);
            child?.RemoveRelation(_root, relationType);
        }

        private static bool IsBehaviorLogicValidationValid(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            if (viewModel == null
                || viewModel.HasBlockingDiagnostics
                || HasIncompleteBehaviorLogicConfiguration(viewModel))
                return false;
            return string.Equals(viewModel.ValidationBadgeText, "验证通过", StringComparison.Ordinal)
                || string.Equals(viewModel.ValidationBadgeText, "有效", StringComparison.Ordinal);
        }

        private static string ResolveBehaviorLogicValidationText(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            if (viewModel?.HasBlockingDiagnostics == true
                || string.Equals(viewModel?.ValidationBadgeText, "存在阻断问题", StringComparison.Ordinal))
            {
                return "⚠ 存在问题";
            }
            if (HasIncompleteBehaviorLogicConfiguration(viewModel))
                return "⚠ 配置未完成";
            if (IsBehaviorLogicValidationValid(viewModel))
                return "✓ 验证通过";
            return "… 等待验证";
        }

        private static bool HasIncompleteBehaviorLogicConfiguration(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            int stageCount = Math.Min(
                Math.Max(0, viewModel?.StageCount ?? 0),
                viewModel?.Stages?.Length ?? 0);
            if (stageCount == 0)
                return true;

            int detectorCount = Math.Min(
                Math.Max(0, viewModel.DetectorCount),
                viewModel.Detectors?.Length ?? 0);
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = viewModel.Detectors[i];
                if (detector == null)
                    continue;
                if (detector.LinkedTransitionCount <= 0
                    || detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                    && detector.ConditionCount <= 0)
                {
                    return true;
                }
            }

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                bool hasDetector = false;
                for (int detectorIndex = 0; detectorIndex < detectorCount; detectorIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                        viewModel.Detectors[detectorIndex];
                    if (detector != null
                        && !detector.CardScope
                        && detector.StageIndex == stageIndex)
                    {
                        hasDetector = true;
                        break;
                    }
                }
                if (!hasDetector)
                    return true;
            }
            return false;
        }

        private void PositionBehaviorLogicStructureControls(int stageCount)
        {
            if (!_behaviorLogicMode || _root == null)
                return;

            Fgui.GObject addStage = _root.GetChild("btnTimelineAddStage");
            Fgui.GObject addDetector = _root.GetChild("btnTimelineAddCardDetector");
            Fgui.GObject addStageFrame = _root.GetChild("logicAddStageFrame");
            Fgui.GObject addStageFill = _root.GetChild("logicAddStageFill");
            Fgui.GObject addStageHint = _root.GetChild("logicAddStageHint");
            float actionHeight = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicStructureActionHeight;
            float rootRight = Math.Min(
                _root.width,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineWidth)
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCanvasRightPadding;
            if (addStage != null)
            {
                float x;
                if (stageCount > 0)
                {
                    int last = stageCount - 1;
                    x = _stageX[last] + _stageWidth[last] + 16f;
                }
                else
                {
                    x = CanvasLeft + 20f - _behaviorLogicHorizontalOffset;
                }
                float width = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicAddStageWidth;
                float frameY = BehaviorLogicStageTop;
                bool visibleInViewport = x + width > CanvasLeft && x < rootRight;
                // The full placeholder is the hit target. Its authored button skin
                // stays transparent so the stable outlined card and centered label
                // communicate structure instead of looking like another toolbar.
                addStage.SetXY(x, frameY);
                addStage.SetSize(width, BehaviorLogicStageHeight);
                addStage.alpha = 0.01f;
                addStage.visible = visibleInViewport;
                if (addStageFrame != null)
                {
                    addStageFrame.SetXY(x, frameY);
                    addStageFrame.SetSize(width, BehaviorLogicStageHeight);
                    addStageFrame.touchable = false;
                    addStageFrame.visible = visibleInViewport;
                }
                if (addStageFill != null)
                {
                    addStageFill.SetXY(x + 2f, frameY + 2f);
                    addStageFill.SetSize(width - 4f, BehaviorLogicStageHeight - 4f);
                    addStageFill.touchable = false;
                    addStageFill.visible = visibleInViewport;
                }
                if (addStageHint != null)
                {
                    SetText(_root, "logicAddStageHint", "+ 添加阶段");
                    addStageHint.SetXY(x + 8f, frameY + 88f);
                    addStageHint.SetSize(width - 16f, 42f);
                    addStageHint.touchable = false;
                    Fgui.GTextField hintText = addStageHint.asTextField;
                    if (hintText != null)
                    {
                        SetTimelineTextFontSize(hintText, 18);
                        hintText.color = _canEdit
                            ? new Color32(111, 227, 207, 255)
                            : new Color32(91, 119, 125, 255);
                    }
                    addStageHint.visible = visibleInViewport;
                }
            }

            int stageIndex = ResolveBehaviorLogicStageContextIndex();
            if (addDetector == null)
                return;
            bool showDetectorAction = stageIndex >= 0
                && stageIndex < stageCount
                && _stageX[stageIndex] + _stageWidth[stageIndex] > CanvasLeft
                && _stageX[stageIndex] < rootRight;
            addDetector.visible = showDetectorAction;
            if (!showDetectorAction)
                return;

            float detectorWidth = Math.Max(92f, _stageWidth[stageIndex] - 36f);
            float detectorX = _stageX[stageIndex] + 18f;
            float detectorY = _stageY[stageIndex]
                - ContentTop
                + _stageHeight[stageIndex]
                - actionHeight
                - 12f;
            addDetector.SetXY(detectorX, detectorY);
            addDetector.SetSize(detectorWidth, actionHeight);
            BindTooltip(addDetector, "向“"
                + (_boundStages[stageIndex]?.Title ?? "当前阶段")
                + "”添加一个检测条件。");
        }

        private int ResolveBehaviorLogicStageContextIndex()
        {
            if (_activeStageCount <= 0)
                return -1;
            if (TryResolveSelectedStageContext(out string stageId))
            {
                for (int i = 0; i < _activeStageCount && i < _boundStages.Length; i++)
                {
                    if (string.Equals(_boundStages[i]?.StageId, stageId, StringComparison.Ordinal))
                        return i;
                }
            }
            for (int i = 0; i < _activeStageCount && i < _boundStages.Length; i++)
            {
                if (_boundStages[i]?.Current == true)
                    return i;
            }
            return _activeStageCount - 1;
        }

        private void HideLegacyRuler()
        {
            SetObjectVisible("rulerBackground", false);
            for (int i = 0; i < _rulerTickPool.Count; i++)
                _rulerTickPool[i].visible = false;
            for (int i = 0; i < _rulerLabelPool.Count; i++)
                _rulerLabelPool[i].visible = false;
            _hasRulerLabels = false;
        }

        private void HideStageSlots(int used)
        {
            for (int i = used; i < _stagePool.Count; i++)
                _stagePool[i].Visual.visible = false;
        }

        private void HideDetectorSlots(int used)
        {
            for (int i = used; i < _detectorPool.Count; i++)
                _detectorPool[i].Visual.visible = false;
        }

        private void HideTransitionSlots(int used)
        {
            for (int i = used; i < _transitionPool.Count; i++)
                _transitionPool[i].Visual.visible = false;
        }

        private void HideConnectors(int used)
        {
            for (int i = used; i < _connectorPool.Count; i++)
                _connectorPool[i].visible = false;
        }

        private void SetText(string childName, string value) => SetText(_root, childName, value);

        private static void SetText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root?.GetChild(childName)?.asTextField;
            string next = value ?? string.Empty;
            if (text != null && !string.Equals(text.text, next, StringComparison.Ordinal))
                text.text = next;
        }

        private void SetObjectVisible(string childName, bool visible) => SetObjectVisible(_root, childName, visible);

        private static void SetObjectVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetButtonEnabled(Fgui.GComponent root, string childName, bool enabled)
        {
            Fgui.GButton button = root?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.enabled = enabled;
            button.grayed = !enabled;
        }

        private static void SetGraphColor(Fgui.GComponent root, string childName, Color32 color)
        {
            Fgui.GGraph graph = root?.GetChild(childName)?.asGraph;
            if (graph != null)
                graph.color = color;
        }
    }
}
