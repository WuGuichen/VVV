using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder
    {
        private static float FrameToGameplayPlanX(
            int frame,
            int startFrame,
            int endFrame,
            float canvasLeft,
            float canvasWidth)
        {
            var viewport = new BehaviorTimelineViewport(
                startFrame,
                endFrame,
                1f,
                0f,
                false,
                1);
            var transform = new BehaviorTimelineTransform(viewport, canvasLeft, canvasWidth);
            return transform.FrameToX(frame);
        }

        private static string CompactGameplayPlanTitle(string title)
        {
            const string prefix = "目标策略：";
            string compactTitle = !string.IsNullOrEmpty(title)
                && title.StartsWith(prefix, StringComparison.Ordinal)
                    ? title.Substring(prefix.Length).Trim()
                    : title ?? string.Empty;

            return string.Equals(compactTitle, "Cast ability slot", StringComparison.Ordinal)
                ? "施放能力"
                : compactTitle;
        }

        private static string CompactGameplayPlanValidation(string validation)
        {
            if (string.Equals(validation, "验证通过", StringComparison.Ordinal))
                return "通过";
            if (string.Equals(validation, "存在阻断问题", StringComparison.Ordinal))
                return "阻断";
            if (string.Equals(validation, "等待验证", StringComparison.Ordinal))
                return "待验证";
            return validation ?? string.Empty;
        }

        private static void HideGameplayPlanBars(List<GameplayPlanBarSlot> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                pool[i].Visual.visible = false;
        }

        private void HideGameplayPlanExits(int used)
        {
            for (int i = used; i < _gameplayPlanExitCards.Count; i++)
                _gameplayPlanExitCards[i].Visual.visible = false;
        }

        private void HideGameplayPlanBranches(int used)
        {
            for (int i = used; i < _gameplayPlanBranchNodes.Count; i++)
                _gameplayPlanBranchNodes[i].Visual.visible = false;
        }

        private void HideGameplayPlanConnectors(int used)
        {
            for (int i = used; i < _gameplayPlanConnectorLines.Count; i++)
                _gameplayPlanConnectorLines[i].visible = false;
        }

        private void HideGameplayPlanGlobalBands(int used)
        {
            for (int i = used; i < _gameplayPlanGlobalBands.Count; i++)
                _gameplayPlanGlobalBands[i].visible = false;
        }

        private void HideGameplayPlanCandidates(int used)
        {
            for (int i = used; i < _gameplayPlanCandidateCards.Count; i++)
                _gameplayPlanCandidateCards[i].Visual.visible = false;
        }

        private void HideGameplayPlan()
        {
            if (_gameplayPlanLayer != null)
                _gameplayPlanLayer.visible = false;
        }

        private void ResetForNewRoot(Fgui.GComponent root)
        {
            _root = root;
            _handlersConfigured = false;
            _gameplayPlanLayer = null;
            _gameplayPlanRulerBackground = null;
            for (int i = 0; i < _gameplayPlanTrackBackgrounds.Length; i++)
            {
                _gameplayPlanTrackBackgrounds[i] = null;
                _gameplayPlanTrackLabels[i] = null;
            }
            for (int i = 0; i < _gameplayPlanRulerTicks.Length; i++)
            {
                _gameplayPlanRulerTicks[i] = null;
                _gameplayPlanRulerLabels[i] = null;
            }
            for (int i = 0; i < _gameplayPlanBehaviorNodeBackgrounds.Length; i++)
                _gameplayPlanBehaviorNodeBackgrounds[i] = null;
            for (int i = 0; i < _gameplayPlanBehaviorExitBackgrounds.Length; i++)
                _gameplayPlanBehaviorExitBackgrounds[i] = null;
            _visualPool.ResetForRoot();
            _gameplayPlanRelationshipLabel = null;
            _gameplayPlanSuccessExitLabel = null;
            _gameplayPlanFailureExitLabel = null;
            _gameplayPlanExecutionStatus = null;
            _gameplayPlanSummaryPrefix = null;
            _gameplayPlanSummaryCurrent = null;
            _gameplayPlanSummaryFramePrefix = null;
            _gameplayPlanSummaryFrame = null;
            _gameplayPlanSummaryNextPrefix = null;
            _gameplayPlanSummaryNext = null;
            _gameplayPlanExitEmpty = null;
            _gameplayPlanGeometryKey = 0L;
            _hasGameplayPlanGeometry = false;
            _gameplayPlanStageCount = 0;
            _gameplayPlanActionCount = 0;
            _gameplayPlanExitCount = 0;
            _gameplayPlanBranchCount = 0;
            _gameplayPlanConnectorCount = 0;
            _gameplayPlanGlobalBandCount = 0;
            _gameplayPlanCandidateCount = 0;
            _session.Reset();
        }

        private void ResolveSelectedToken(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            _session.ResolveSelection(
                viewModel,
                stages,
                stageCount,
                detectors,
                detectorCount,
                transitions,
                transitionCount);
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 17;
                if (value == null)
                    return hash;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        private void EnsureToolbarHandlers()
        {
            if (_handlersConfigured || _root == null)
                return;
            _handlersConfigured = true;
            BindToolbar(
                "btnTimelineAddStage",
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                panelCommand: true);
            BindToolbar(
                "btnTimelineSnap",
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap);
            BindToolbar(
                "btnTimelineZoomOut",
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut);
            BindToolbar(
                "btnTimelineZoomIn",
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn);
            BindToolbar(
                "btnTimelineFit",
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline);
        }

        private void BindToolbar(string childName, string commandId, bool panelCommand = false)
        {
            Fgui.GButton button = _root?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.onClick.Set(() =>
            {
                object payload = panelCommand
                    ? null
                    : new CharacterTestRuntimeAiLabBehaviorCardElementCommand(
                        string.Empty,
                        0,
                        CharacterTestRuntimeAiLabLogicElementKind.None);
                _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
            });
        }

        private void SetText(string childName, string value)
        {
            Fgui.GTextField label = _root?.GetChild(childName)?.asTextField;
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private void SetObjectVisible(string childName, bool visible)
        {
            Fgui.GObject child = _root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetButtonTitle(
            Fgui.GComponent root,
            string childName,
            string value)
        {
            Fgui.GButton button = root?.GetChild(childName)?.asButton;
            if (button != null)
                button.title = value ?? string.Empty;
        }
    }
}
