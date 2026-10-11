using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private int BindDetectors(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int stageCount,
            int startFrame,
            float frameSpan,
            float canvasWidth,
            float verticalOffset)
        {
            _boundDetectors = detectors;
            _boundDetectorCount = detectorCount;
            int used = 0;
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                DetectorSlot slot = GetDetectorSlot(used++);
                slot.StableId = detector.DetectorId ?? string.Empty;
                slot.ViewModel = detector;
                Fgui.GComponent band = slot.Visual;
                band.visible = true;
                band.sortingOrder = detector.Selected ? 45 : DetectorSortingOrder;
                SetObjectVisible(band, "background", true);
                SetObjectVisible(band, "stripe", true);
                SetObjectVisible(band, "status", true);
                Fgui.GTextField detectorTitle = band.GetChild("title")?.asTextField;
                if (detectorTitle != null)
                {
                    detectorTitle.color = new Color32(255, 217, 138, 255);
                    detectorTitle.alpha = 1f;
                }
                float x;
                float width;
                float y;
                bool denseCompactBand = false;
                bool missingCompactTransition = false;
                bool hiddenCompactBand = false;
                int compactStageIndex = _compactBehaviorLogicCard
                    ? ResolveCompactBehaviorLogicDetectorStageIndex(detector, stageCount)
                    : -1;
                if (_compactBehaviorLogicCard && compactStageIndex >= 0)
                {
                    int localIndex = CompactDetectorIndexWithinStage(
                        detectors,
                        detectorCount,
                        i,
                        compactStageIndex,
                        stageCount);
                    int scrollOffset = compactStageIndex < _stagePool.Count
                        ? _stagePool[compactStageIndex].DetectorScrollOffset
                        : 0;
                    int visibleIndex = localIndex - scrollOffset;
                    hiddenCompactBand = visibleIndex < 0
                        || visibleIndex >= BehaviorLogicVisibleDetectorRows;
                    missingCompactTransition = !HasTransitionForDetector(i);
                    denseCompactBand = true;
                    width = _stageWidth[compactStageIndex] - BehaviorLogicDetectorInset * 2f;
                    x = _stageX[compactStageIndex] + BehaviorLogicDetectorInset;
                    y = _stageY[compactStageIndex]
                        + BehaviorLogicDetectorTop
                        + Mathf.Clamp(visibleIndex, 0, BehaviorLogicVisibleDetectorRows - 1)
                            * (DetectorBandHeight + BehaviorLogicDetectorRowGap);
                }
                else if (_compactBehaviorLogicCard && detector.CardScope)
                {
                    int rank = CardScopeDisplayRank(detectors, detectorCount, i);
                    hiddenCompactBand = rank >= BehaviorLogicVisibleGlobalDetectors;
                    missingCompactTransition = !HasTransitionForDetector(i);
                    denseCompactBand = true;
                    width = BehaviorLogicGlobalDetectorWidth;
                    x = CanvasLeft
                        + BehaviorLogicGlobalDetectorLeft
                        + rank * (BehaviorLogicGlobalDetectorWidth + BehaviorLogicGlobalDetectorGap);
                    y = ContentTop + BehaviorLogicGlobalDetectorTop;
                }
                else if (detector.CardScope || detector.StageIndex < 0 || detector.StageIndex >= stageCount)
                {
                    x = CanvasLeft + (detector.StartFrame - startFrame) / frameSpan * canvasWidth;
                    width = Mathf.Clamp(detector.DurationFrames / frameSpan * canvasWidth, 280f, 760f);
                    y = ContentTop
                        + CardScopeDisplayRank(detectors, detectorCount, i) * (DetectorBandHeight + 6f)
                        - verticalOffset;
                }
                else
                {
                    int stageIndex = detector.StageIndex;
                    if (_behaviorLogicMode)
                    {
                        int localIndex = DetectorIndexWithinStage(detectors, detectorCount, i);
                        int localCount = _stageDetectorCount[stageIndex];
                        int columns = ResolveBehaviorLogicDetectorColumns(
                            _stageWidth[stageIndex],
                            localCount);
                        int column = localIndex % columns;
                        int row = localIndex / columns;
                        float summaryWidth = ResolveBehaviorLogicSummaryWidth(_stageWidth[stageIndex]);
                        float availableWidth = _stageWidth[stageIndex] - summaryWidth - 28f;
                        width = Math.Max(
                            220f,
                            (availableWidth - (columns - 1) * 12f) / columns);
                        x = _stageX[stageIndex]
                            + summaryWidth
                            + column * (width + 12f);
                        y = _stageY[stageIndex] + 8f + row * 44f;
                    }
                    else
                    {
                        x = _stageX[stageIndex] + 12f;
                        width = Mathf.Max(210f, _stageWidth[stageIndex] - 24f);
                        y = _stageY[stageIndex]
                            + 116f
                            + DetectorIndexWithinStage(detectors, detectorCount, i) * 46f;
                    }
                }
                bool expandedEditor = detector.Selected && !_behaviorLogicMode;
                if (expandedEditor)
                {
                    width = Mathf.Max(width, 1320f);
                    x = Mathf.Clamp(x, CanvasLeft, CanvasRight - width);
                    y = Mathf.Max(y, FindMaximumStageBottom() + 24f);
                    y = Mathf.Clamp(y, ContentTop + 8f, ContentBottom - 144f);
                }
                float height = _compactBehaviorLogicCard
                    ? DetectorBandHeight
                    : _behaviorLogicMode
                        ? 36f
                        : expandedEditor ? 136f : DetectorBandHeight;
                _detectorX[i] = x;
                _detectorY[i] = y;
                _detectorWidth[i] = width;
                _detectorHeight[i] = height;
                band.SetXY(x - CanvasLeft, y - ContentTop);
                band.SetSize(width, height);
                band.visible = !hiddenCompactBand;
                band.rootContainer.clipRect = new Rect(0f, 0f, width, height);
                SetText(
                    band,
                    "title",
                    _compactBehaviorLogicCard
                        ? ResolveCompactBehaviorLogicDetectorTitle(
                            detector,
                            ResolveDetectorStage(detector))
                        : _behaviorLogicMode
                            ? ResolveBehaviorLogicDetectorTitle(detector)
                            : detector.HeaderText);
                SetText(
                    band,
                    "fact",
                    missingCompactTransition
                        ? "尚未设置结果"
                        : expandedEditor
                        ? "基本属性：名称 · 作用域 · 所属 Stage · 优先级 · 起点 · 时长"
                        : _behaviorLogicMode
                            ? ResolveBehaviorLogicDetectorFact(detector)
                            : detector.FactText);
                SetText(
                    band,
                    "scope",
                    expandedEditor
                        ? "规则与操作：触发 · 窗口 · 跳转 · 复制 · 删除"
                        : detector.ScopeText);
                SetText(
                    band,
                    "status",
                    _compactBehaviorLogicCard
                        ? string.Empty
                        : _behaviorLogicMode && detector.Trigger ==
                        CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                        && detector.ConditionCount == 0
                            ? "需要修复"
                            : _behaviorLogicMode
                                ? ResolveBehaviorLogicDetectorStatus(detector)
                                : detector.StatusText);
                SetObjectVisible(band, "fact", true);
                SetObjectVisible(band, "scope", !_behaviorLogicMode);
                LayoutDetectorEditorCard(band, expandedEditor, width);
                if (_behaviorLogicMode)
                {
                    if (_compactBehaviorLogicCard)
                    {
                        ConfigureCompactBehaviorLogicDetectorBand(
                            band,
                            width,
                            denseCompactBand,
                            missingCompactTransition);
                    }
                    else
                        ConfigureBehaviorLogicDetectorBand(band, width);
                }
                ColorDetector(band, detector);
                if (_compactBehaviorLogicCard)
                    ApplyCompactBehaviorLogicDetectorTone(band, detector);
                ConfigureDetectorButtons(band, detector);
                BindDetectorEditorFields(slot, detector);
                LayoutDetectorGestureTargets(slot, width, height, expandedEditor);
                if (_compactBehaviorLogicCard)
                {
                    SetDetectorEditorVisibility(band, false);
                    if (slot.ResizeHandle != null)
                        slot.ResizeHandle.visible = false;
                    if (slot.OutputPort != null)
                    {
                        slot.OutputPort.visible = !hiddenCompactBand
                            && detector.LinkedTransitionCount > 0;
                        slot.OutputPort.SetXY(width - 6f, (height - 12f) * 0.5f);
                        slot.OutputPort.sortingOrder = 75;
                    }
                }
                if (expandedEditor)
                {
                    float unscrolledBottom = y + height + Math.Max(0f, verticalOffset);
                    _contentExtentHeight = Mathf.Max(_contentExtentHeight, unscrolledBottom - ContentTop);
                }
            }
            HideDetectorSlots(used);
            _activeDetectorCount = used;
            return CountDetectors(detectors, detectorCount, -1);
        }

        private static string ResolveBehaviorLogicDetectorTitle(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return "动作完成";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return "动作失败";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                    return detector.ConditionCount > 0 ? "条件满足" : "触发条件";
                default:
                    return "检测";
            }
        }

        private CharacterTestRuntimeAiLabLogicStageViewModel ResolveDetectorStage(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            int stageIndex = detector?.StageIndex ?? -1;
            return stageIndex >= 0 && stageIndex < _boundStages.Length
                ? _boundStages[stageIndex]
                : null;
        }

        private static string ResolveCompactBehaviorLogicDetectorTitle(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (detector == null)
                return "条件";
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return stage?.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        ? "到达目标"
                        : "动作完成";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return stage?.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        && ContainsCompactSemantic(detector.Title, "路径不可用")
                        ? "路径不可用"
                        : "动作失败";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                    return CharacterTestRuntimeAiPlannerDevPresentationText
                        .BuildBehaviorDetectorLabel(detector.Title, detector.CardScope);
                default:
                    return "条件";
            }
        }

        private static string ResolveBehaviorLogicDetectorFact(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge)
                return detector.ConditionCount > 0 ? detector.FactText : "尚未设置";
            if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted)
                return "主动作完成时";
            if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed)
                return "主动作失败时";
            return "等待检测";
        }

        private static string ResolveBehaviorLogicDetectorStatus(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector.Triggered)
                return "已命中";
            if (detector.Armed || detector.Active)
                return "监听中";
            return "未激活";
        }

        private string ResolveBehaviorLogicStageTiming(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int stageIndex,
            int stageCount)
        {
            string timing;
            switch (stage.DurationMode)
            {
                case CharacterTestRuntimeAiLabLogicDurationMode.Fixed:
                    timing = Math.Max(1, stage.DurationFrames) + " 帧";
                    break;
                case CharacterTestRuntimeAiLabLogicDurationMode.Infinite:
                    timing = "持续运行";
                    break;
                default:
                    timing = "直到事件发生";
                    break;
            }
            string result = stageCount > 1
                ? "阶段 " + (stageIndex + 1) + " · " + timing
                : timing;
            return IsBehaviorLogicStageIncomplete(stageIndex)
                ? result + " · 未完成"
                : result;
        }

        private bool IsBehaviorLogicStageIncomplete(int stageIndex)
        {
            bool hasDetector = false;
            for (int i = 0; i < _boundDetectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = _boundDetectors[i];
                if (detector == null
                    || detector.CardScope
                    || detector.StageIndex != stageIndex)
                {
                    continue;
                }
                hasDetector = true;
                if (detector.LinkedTransitionCount <= 0
                    || detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                    && detector.ConditionCount <= 0)
                {
                    return true;
                }
            }
            return !hasDetector;
        }

        private static void ConfigureBehaviorLogicDetectorBand(
            Fgui.GComponent band,
            float width)
        {
            SetObjectVisible(band, "background", true);
            SetObjectVisible(band, "stripe", true);
            Fgui.GGraph stripe = band.GetChild("stripe")?.asGraph;
            if (stripe != null)
            {
                stripe.SetXY(0f, 0f);
                stripe.SetSize(5f, band.height);
                stripe.DrawRect(5f, band.height, 0, Color.clear, Color.white);
            }
            SetObjectXY(band, "title", 16f, 4f);
            SetObjectSize(band, "title", Math.Max(100f, width - 126f), 28f);
            SetObjectVisible(band, "fact", false);
            SetObjectXY(band, "status", Math.Max(16f, width - 108f), 4f);
            SetObjectSize(band, "status", 92f, 28f);
            SetObjectVisible(band, "status", true);
            SetObjectVisible(band, "scope", false);
            Fgui.GTextField title = band.GetChild("title")?.asTextField;
            if (title != null)
                title.color = new Color32(255, 217, 138, 255);
            SetTimelineTextFontSize(band.GetChild("title")?.asTextField, 18);
            SetTimelineTextFontSize(band.GetChild("status")?.asTextField, 15);
        }

        private static void ConfigureCompactBehaviorLogicDetectorBand(
            Fgui.GComponent band,
            float width,
            bool dense,
            bool missingTransition)
        {
            SetObjectVisible(band, "background", true);
            SetObjectVisible(band, "stripe", true);
            Fgui.GGraph background = band.GetChild("background")?.asGraph;
            if (background != null)
            {
                background.SetXY(0f, 0f);
                background.SetSize(width, band.height);
                background.DrawRoundRect(
                    width,
                    band.height,
                    Color.white,
                    StandardStageCornerRadii);
            }
            Fgui.GGraph marker = band.GetChild("stripe")?.asGraph;
            if (marker != null)
            {
                const float markerSize = 16f;
                marker.SetXY(8f, (band.height - markerSize) * 0.5f);
                marker.SetSize(markerSize, markerSize);
                marker.DrawEllipse(markerSize, markerSize, Color.white);
            }
            const float titleX = 32f;
            float titleWidth = missingTransition
                ? Math.Max(58f, width - titleX - 118f)
                : Math.Max(58f, Math.Min(126f, width * 0.44f - titleX));
            SetObjectXY(band, "title", titleX, 1f);
            SetObjectSize(band, "title", titleWidth, band.height - 2f);
            SetObjectVisible(band, "fact", false);
            SetObjectVisible(band, "scope", false);
            SetObjectVisible(band, "status", false);
            SetTimelineTextFontSize(
                band.GetChild("title")?.asTextField,
                width >= 180f ? 17 : 15);
        }

        private static void ApplyCompactBehaviorLogicDetectorTone(
            Fgui.GComponent band,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            Color32 tone;
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    tone = new Color32(118, 221, 199, 255);
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    tone = new Color32(255, 119, 128, 255);
                    break;
                default:
                    tone = new Color32(234, 194, 111, 255);
                    break;
            }
            if (detector.Triggered)
                tone = new Color32(255, 202, 82, 255);
            Fgui.GTextField title = band.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = tone;
                title.alpha = detector.Active || detector.Armed || detector.Triggered ? 1f : 0.94f;
            }
            Fgui.GGraph stripe = band.GetChild("stripe")?.asGraph;
            if (stripe != null)
            {
                stripe.color = tone;
                stripe.alpha = detector.Active || detector.Armed || detector.Triggered ? 1f : 0.94f;
            }
            Fgui.GGraph outputPort = band.GetChild("outputPort")?.asGraph;
            if (outputPort != null)
            {
                outputPort.color = tone;
                outputPort.alpha = detector.Active || detector.Armed || detector.Triggered ? 1f : 0.94f;
            }
        }

        private float ResolveCompactBehaviorLogicLaneWidth(int stageIndex, int detectorCount)
        {
            float innerWidth = Math.Max(
                320f,
                _stageWidth[stageIndex] - CompactBehaviorLogicHorizontalPadding * 2f);
            return (innerWidth
                - Math.Max(0, detectorCount - 1) * CompactBehaviorLogicLaneGap)
                / Math.Max(1, detectorCount);
        }

        private static int ResolveCompactBehaviorLogicDetectorStageIndex(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int stageCount)
        {
            if (detector == null || stageCount <= 0)
                return -1;
            if (!detector.CardScope
                && detector.StageIndex >= 0
                && detector.StageIndex < stageCount)
            {
                return detector.StageIndex;
            }
            return -1;
        }

        private static int CompactDetectorIndexWithinStage(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int detectorIndex,
            int stageIndex,
            int stageCount)
        {
            int result = 0;
            for (int i = 0; i < detectorIndex && i < detectorCount; i++)
            {
                if (ResolveCompactBehaviorLogicDetectorStageIndex(detectors[i], stageCount)
                    == stageIndex)
                {
                    result++;
                }
            }
            return result;
        }

        private bool HasTransitionForDetector(int detectorIndex)
        {
            for (int i = 0; i < _boundTransitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = _boundTransitions[i];
                if (transition?.DetectorIndex == detectorIndex)
                    return true;
            }
            return false;
        }

        private const int CompactRuntimeFrameCacheLimit = 4096;
        private static readonly string[] CompactExecutingFrameText =
            BuildCompactExecutingFrameText();

        private static string[] BuildCompactExecutingFrameText()
        {
            var result = new string[CompactRuntimeFrameCacheLimit + 1];
            for (int frame = 0; frame < result.Length; frame++)
            {
                result[frame] = "执行中 · "
                    + frame.ToString(CultureInfo.InvariantCulture)
                    + "f";
            }
            return result;
        }

        private static string ResolveCompactBehaviorLogicRuntimeStatus(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return string.Empty;
            string status = stage.RuntimeStatusText ?? string.Empty;
            bool executing = stage.ActionRunning
                || status.IndexOf("主动行为执行中", StringComparison.Ordinal) >= 0
                || status.IndexOf("执行中", StringComparison.Ordinal) >= 0;
            bool current = stage.Current
                || status.IndexOf("当前阶段", StringComparison.Ordinal) >= 0;
            if (!executing)
                return current ? "当前阶段" : string.Empty;
            int frame = stage.ActualDurationFrames >= 0
                ? stage.ActualDurationFrames
                : stage.ActionProgressFrames;
            if (frame < 0)
                frame = ParseCompactRuntimeFrame(status);
            return frame >= 0 && frame <= CompactRuntimeFrameCacheLimit
                ? CompactExecutingFrameText[frame]
                : "执行中";
        }

        private static int ParseCompactRuntimeFrame(string status)
        {
            const string marker = "实际 ";
            int markerIndex = status.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;
            int index = markerIndex + marker.Length;
            int value = 0;
            bool foundDigit = false;
            while (index < status.Length)
            {
                char character = status[index++];
                if (character < '0' || character > '9')
                    break;
                foundDigit = true;
                value = value * 10 + character - '0';
                if (value > CompactRuntimeFrameCacheLimit)
                    return -1;
            }
            return foundDigit ? value : -1;
        }

        private float FindMaximumStageBottom()
        {
            float bottom = ContentTop;
            for (int i = 0; i < _activeStageCount; i++)
                bottom = Mathf.Max(bottom, _stageY[i] + _stageHeight[i]);
            return bottom;
        }

        private static void LayoutDetectorEditorCard(Fgui.GComponent band, bool selected, float width)
        {
            Fgui.GTextField title = band.GetChild("title")?.asTextField;
            Fgui.GTextField fact = band.GetChild("fact")?.asTextField;
            Fgui.GTextField scope = band.GetChild("scope")?.asTextField;
            Fgui.GTextField status = band.GetChild("status")?.asTextField;
            if (title == null || fact == null || scope == null || status == null)
                return;

            if (!selected)
            {
                title.SetXY(12f, 4f);
                title.SetSize(Math.Max(80f, width - 124f), 18f);
                fact.SetXY(12f, 22f);
                fact.SetSize(Math.Max(80f, width - 104f), 16f);
                scope.SetXY(Math.Max(12f, width - 102f), 4f);
                scope.SetSize(90f, 16f);
                status.SetXY(Math.Max(12f, width - 102f), 22f);
                status.SetSize(90f, 16f);
                return;
            }

            title.SetXY(12f, 4f);
            title.SetSize(width - 220f, 20f);
            fact.SetXY(12f, 28f);
            fact.SetSize(732f, 16f);
            scope.SetXY(760f, 28f);
            scope.SetSize(width - 772f, 16f);
            status.SetXY(width - 196f, 4f);
            status.SetSize(184f, 18f);
            SetObjectSize(band, "btnTrigger", 140f, 28f);
            SetObjectXY(band, "btnTrigger", 760f, 48f);
            SetObjectSize(band, "btnWindowMode", 140f, 28f);
            SetObjectXY(band, "btnWindowMode", 908f, 48f);
            SetObjectSize(band, "btnAddTransition", 100f, 28f);
            SetObjectXY(band, "btnAddTransition", 1056f, 48f);
            SetObjectSize(band, "btnDuplicate", 70f, 28f);
            SetObjectXY(band, "btnDuplicate", 1164f, 48f);
            SetObjectSize(band, "btnDelete", 54f, 28f);
            SetObjectXY(band, "btnDelete", 1242f, 48f);
        }

        private static void SetObjectXY(Fgui.GComponent root, string childName, float x, float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.SetXY(x, y);
        }

        private static void SetObjectSize(Fgui.GComponent root, string childName, float width, float height)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.SetSize(width, height);
        }
    }
}
