using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void LayoutStages(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            int startFrame,
            float frameSpan,
            float canvasWidth,
            float verticalOffset,
            int cardDetectorCount,
            bool revealAppendedBehaviorLogicStage)
        {
            if (_behaviorLogicMode)
            {
                LayoutBehaviorLogicStages(
                    stageCount,
                    canvasWidth,
                    revealAppendedBehaviorLogicStage);
                return;
            }

            for (int i = 0; i < stageCount; i++)
            {
                _laneHeight[i] = 0f;
                _laneY[i] = 0f;
            }
            int laneCount = 0;
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                float x = CanvasLeft + (stage.StartFrame - startFrame) / frameSpan * canvasWidth;
                float plannedWidth = stage.DurationFrames / frameSpan * canvasWidth;
                float width = Mathf.Clamp(plannedWidth, 400f, 680f);
                float height = ResolveStageCardHeight(stage, _stageDetectorCount[i]);
                int lane = 0;
                bool intersects;
                do
                {
                    intersects = false;
                    for (int previous = 0; previous < i; previous++)
                    {
                        if (_stageLane[previous] != lane)
                            continue;
                        if (x < _stageX[previous] + _stageWidth[previous]
                            && x + width > _stageX[previous])
                        {
                            intersects = true;
                            lane++;
                            break;
                        }
                    }
                }
                while (intersects);

                _stageX[i] = x;
                _stageWidth[i] = width;
                _stageHeight[i] = height;
                _stageLane[i] = lane;
                _laneHeight[lane] = Mathf.Max(_laneHeight[lane], height);
                laneCount = Math.Max(laneCount, lane + 1);
            }

            float nextY = ContentTop
                + cardDetectorCount * (DetectorBandHeight + 6f)
                + (cardDetectorCount > 0 ? 18f : 0f);
            for (int lane = 0; lane < laneCount; lane++)
            {
                _laneY[lane] = nextY;
                nextY += _laneHeight[lane] + StageLaneGap;
            }
            for (int i = 0; i < stageCount; i++)
                _stageY[i] = _laneY[_stageLane[i]] - verticalOffset;
            _contentExtentHeight = Mathf.Max(ContentHeight, nextY - ContentTop);
        }

        private void LayoutBehaviorLogicStages(
            int stageCount,
            float canvasWidth,
            bool revealAppendedStage = false)
        {
            const float outerPadding = 20f;
            const float structureGap = 16f;
            float viewportHeight = _contentLayer != null
                ? Math.Max(1f, _contentLayer.height)
                : ContentHeight;
            float addStageWidth = CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                .BehaviorLogicAddStageWidth;
            _behaviorLogicContentWidth = outerPadding
                + stageCount * BehaviorLogicStageWidth
                + Math.Max(0, stageCount - 1) * BehaviorLogicStageGap
                + (stageCount > 0 ? structureGap : 0f)
                + addStageWidth
                + outerPadding;
            _behaviorLogicContentWidth = Math.Max(canvasWidth, _behaviorLogicContentWidth);
            float maximumOffset = Math.Max(0f, _behaviorLogicContentWidth - canvasWidth);
            _behaviorLogicHorizontalOffset = revealAppendedStage
                ? maximumOffset
                : Mathf.Clamp(_behaviorLogicHorizontalOffset, 0f, maximumOffset);

            float x = CanvasLeft + outerPadding - _behaviorLogicHorizontalOffset;
            float y = ContentTop + BehaviorLogicStageTop;
            for (int i = 0; i < stageCount; i++)
            {
                _stageX[i] = x;
                _stageY[i] = y;
                _stageWidth[i] = BehaviorLogicStageWidth;
                _stageHeight[i] = BehaviorLogicStageHeight;
                _stageLane[i] = 0;
                x += BehaviorLogicStageWidth + BehaviorLogicStageGap;
            }
            _laneHeight[0] = BehaviorLogicStageHeight;
            _laneY[0] = y;
            _contentExtentHeight = viewportHeight;
            UpdateBehaviorLogicHorizontalScrollBar();
        }

        private static float ResolveBehaviorLogicStageHeight(float width, int detectorCount)
        {
            int columns = ResolveBehaviorLogicDetectorColumns(width, detectorCount);
            int rows = detectorCount > 0
                ? (detectorCount + columns - 1) / columns
                : 1;
            return Mathf.Clamp(36f + rows * 44f, 80f, 212f);
        }

        private bool CanUseCompactBehaviorLogicCard(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            if (!_behaviorLogicMode || stageCount <= 0 || stages == null)
            {
                return false;
            }

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                if (stageIndex >= stages.Length || stages[stageIndex] == null)
                    return false;
            }
            return true;
        }

        private static int ResolveBehaviorLogicDetectorColumns(float width, int detectorCount)
        {
            if (detectorCount <= 1)
                return 1;
            float available = Math.Max(
                220f,
                width - ResolveBehaviorLogicSummaryWidth(width) - 28f);
            int columns = Mathf.FloorToInt((available + 12f) / 252f);
            return Mathf.Clamp(columns, 1, Math.Min(3, detectorCount));
        }

        private static float ResolveBehaviorLogicSummaryWidth(float width)
        {
            return Mathf.Clamp(width * 0.32f, 220f, 300f);
        }

        private static float ResolveStageCardHeight(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int stageDetectorCount)
        {
            float detailsTop = stageDetectorCount > 0 ? 116f : 138f;
            float detectorHeight = stageDetectorCount > 0
                ? Math.Max(50f, stageDetectorCount * (DetectorBandHeight + 4f))
                : EstimateWrappedTextHeight(stage.DetectorSummaryText, 50f);
            float transitionHeight = EstimateWrappedTextHeight(stage.TransitionSummaryText, 48f);
            float detailsBottom = detailsTop + detectorHeight + 6f + transitionHeight;
            if (!string.IsNullOrEmpty(stage.RuntimeStatusText))
                detailsBottom += 26f;
            return Math.Max(StageCardHeight, detailsBottom + 14f);
        }

        private static float EstimateWrappedTextHeight(string text, float minimum)
        {
            int length = text?.Length ?? 0;
            int lines = Math.Max(1, (length + 27) / 28);
            return Math.Max(minimum, lines * 16f + 4f);
        }

        private void FillStageDetectorCounts(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int stageCount)
        {
            for (int i = 0; i < stageCount; i++)
                _stageDetectorCount[i] = 0;
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                if (detector == null)
                    continue;
                if (detector.CardScope)
                    continue;
                if (detector.StageIndex >= 0 && detector.StageIndex < stageCount)
                    _stageDetectorCount[detector.StageIndex]++;
            }
        }

        private static int CountDetectors(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int stageIndex)
        {
            int result = 0;
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                if (detector == null)
                    continue;
                if (stageIndex < 0 ? detector.CardScope : !detector.CardScope && detector.StageIndex == stageIndex)
                    result++;
            }
            return result;
        }

        private void BindStages(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                StageSlot slot = GetStageSlot(i);
                if (!string.Equals(slot.StableId, stage.StageId, StringComparison.Ordinal))
                    slot.DetectorScrollOffset = 0;
                slot.StableId = stage.StageId ?? string.Empty;
                Fgui.GComponent card = slot.Visual;
                card.visible = true;
                card.sortingOrder = StageSortingOrder;
                card.SetXY(_stageX[i] - CanvasLeft, _stageY[i] - ContentTop);
                card.SetSize(_stageWidth[i], _stageHeight[i]);
                card.rootContainer.clipRect = new Rect(0f, 0f, _stageWidth[i], _stageHeight[i]);
                HideCompactBehaviorLogicStageDecorations(slot);
                SetObjectVisible(card, "background", true);
                SetObjectVisible(card, "stripe", true);
                SetObjectVisible(card, "kind", true);
                SetObjectVisible(card, "time", true);
                Fgui.GGraph background = card.GetChild("background")?.asGraph;
                if (background != null)
                {
                    background.SetXY(0f, 0f);
                    background.SetSize(_stageWidth[i], _stageHeight[i]);
                    background.DrawRoundRect(
                        _stageWidth[i],
                        _stageHeight[i],
                        Color.white,
                        StandardStageCornerRadii);
                }
                SetObjectXY(card, "stripe", 0f, 0f);
                SetObjectSize(card, "stripe", 7f, _stageHeight[i]);
                Fgui.GTextField kind = card.GetChild("kind")?.asTextField;
                if (kind != null)
                {
                    kind.align = Fgui.AlignType.Left;
                    kind.color = new Color32(143, 214, 226, 255);
                }
                Fgui.GTextField runtime = card.GetChild("runtime")?.asTextField;
                if (runtime != null)
                {
                    runtime.align = Fgui.AlignType.Left;
                    runtime.color = new Color32(111, 227, 207, 255);
                }
                SetText(
                    card,
                    "title",
                    _behaviorLogicMode
                        ? CharacterTestRuntimeAiPlannerDevPresentationText
                            .BuildBehaviorStageLabel(stage.Title, stage.Kind)
                        : stage.Title);
                SetObjectVisible(card, "title", !_behaviorLogicMode);
                SetText(
                    card,
                    "kind",
                    _behaviorLogicMode
                        ? CharacterTestRuntimeAiPlannerDevPresentationText
                            .BuildBehaviorGameplayActionLabel(stage)
                        : stage.Subtitle);
                SetText(
                    card,
                    "time",
                    _behaviorLogicMode
                        ? ResolveBehaviorLogicStageTiming(stage, i, stageCount)
                        : stage.TimeText);
                SetText(
                    card,
                    "action",
                    _behaviorLogicMode
                        ? stage.Selected && IsBehaviorLogicStageIncomplete(i)
                            ? "1  选择行为    2  添加检测    3  设置结果"
                            : string.Empty
                        : stage.ActionText);
                SetText(card, "detectors", stage.DetectorSummaryText);
                SetText(card, "transitions", stage.TransitionSummaryText);
                string runtimeStatus = _compactBehaviorLogicCard
                    ? ResolveCompactBehaviorLogicRuntimeStatus(stage)
                    : stage.RuntimeStatusText;
                SetText(card, "runtime", runtimeStatus);
                SetText(card, "stableId", stage.StageId);
                LayoutStageDetails(card, stage, _stageDetectorCount[i]);
                if (_behaviorLogicMode)
                {
                    if (_compactBehaviorLogicCard)
                    {
                        ConfigureFixedBehaviorLogicStageCard(
                            slot,
                            stage,
                            _stageDetectorCount[i]);
                    }
                    else
                        ConfigureBehaviorLogicStageCard(card, stage, _stageDetectorCount[i]);
                }
                SetStageConflictVisible(slot, stage.ActionConflict);
                SetObjectVisible(card, "runtime", !string.IsNullOrEmpty(runtimeStatus));
                ColorCard(card, stage);
                ConfigureStageButtons(card, stage);
                if (slot.InputPort != null)
                {
                    slot.InputPort.visible = _behaviorLogicMode || !_compactBehaviorLogicCard;
                    slot.InputPort.SetXY(
                        -6f,
                        _behaviorLogicMode ? BehaviorLogicDetectorTop + 16f : 24f);
                    slot.InputPort.sortingOrder = 75;
                }
            }
            HideStageSlots(stageCount);
        }

        private static void ConfigureBehaviorLogicStageCard(
            Fgui.GComponent card,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int detectorCount)
        {
            float width = card.width;
            float height = card.height;
            SetObjectVisible(card, "background", true);
            SetObjectVisible(card, "stripe", true);
            SetObjectVisible(card, "kind", true);
            SetObjectVisible(card, "time", true);
            SetObjectSize(card, "background", width, height);
            SetObjectSize(card, "stripe", 7f, height);
            float summaryWidth = ResolveBehaviorLogicSummaryWidth(width);
            SetObjectVisible(card, "title", false);
            SetObjectXY(card, "kind", 28f, 8f);
            SetObjectSize(card, "kind", summaryWidth - 84f, 26f);
            SetObjectXY(card, "time", 28f, 34f);
            SetObjectSize(card, "time", summaryWidth - 48f, 18f);
            SetObjectXY(card, "runtime", 28f, height - 26f);
            SetObjectSize(card, "runtime", width - 56f, 20f);
            SetObjectXY(card, "disclosure", summaryWidth - 96f, 4f);
            SetObjectSize(card, "disclosure", 84f, 36f);
            Fgui.GTextField editStage = card.GetChild("disclosure")?.asTextField;
            if (editStage != null)
                editStage.textFormat.size = 16;
            SetObjectVisible(card, "stableId", false);
            SetObjectVisible(card, "action", false);
            SetObjectVisible(card, "detectors", detectorCount == 0);
            SetObjectVisible(card, "transitions", false);
            if (detectorCount == 0)
            {
                SetText(card, "detectors", "尚未配置检测");
                SetObjectXY(card, "detectors", summaryWidth + 12f, 18f);
                SetObjectSize(card, "detectors", width - summaryWidth - 40f, 32f);
            }
            SetTimelineTextFontSize(card.GetChild("kind")?.asTextField, 22);
            SetTimelineTextFontSize(card.GetChild("time")?.asTextField, 16);
            SetTimelineTextFontSize(card.GetChild("detectors")?.asTextField, 20);
            SetTimelineTextFontSize(card.GetChild("runtime")?.asTextField, 18);
        }

        private static void ConfigureFixedBehaviorLogicStageCard(
            StageSlot slot,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int detectorCount)
        {
            Fgui.GComponent card = slot.Visual;
            float width = card.width;
            float height = card.height;
            int maximumDetectorOffset = Math.Max(
                0,
                detectorCount - BehaviorLogicVisibleDetectorRows);
            slot.DetectorScrollOffset = Mathf.Clamp(
                slot.DetectorScrollOffset,
                0,
                maximumDetectorOffset);

            if (slot.CompactFrame != null)
            {
                slot.CompactFrame.visible = true;
                slot.CompactFrame.SetXY(0f, 0f);
                slot.CompactFrame.SetSize(width, height);
                slot.CompactFrame.DrawRoundRect(
                    width,
                    height,
                    ResolveCompactBehaviorLogicFrameColor(stage),
                    CompactStageFrameCornerRadii);
            }
            Fgui.GGraph background = card.GetChild("background")?.asGraph;
            if (background != null)
            {
                background.SetXY(2f, 2f);
                background.SetSize(width - 4f, height - 4f);
                background.DrawRoundRect(
                    width - 4f,
                    height - 4f,
                    Color.white,
                    CompactStageFillCornerRadii);
            }
            SetObjectVisible(card, "background", true);
            SetObjectVisible(card, "stripe", true);
            SetObjectXY(card, "stripe", 2f, 10f);
            SetObjectSize(card, "stripe", 6f, height - 20f);
            SetObjectVisible(card, "title", false);
            SetObjectVisible(card, "kind", true);
            float headerX = stage?.Entry == true ? 64f : 18f;
            SetObjectXY(card, "kind", headerX, 9f);
            SetObjectSize(card, "kind", Math.Max(120f, width - headerX - 88f), 28f);
            SetObjectVisible(card, "time", true);
            SetObjectXY(card, "time", headerX, 37f);
            SetObjectSize(card, "time", Math.Max(120f, width - headerX - 18f), 22f);
            SetObjectVisible(card, "stableId", false);
            SetObjectVisible(card, "action", false);
            SetObjectVisible(card, "transitions", false);
            SetObjectVisible(card, "detectors", detectorCount == 0);
            if (detectorCount == 0)
            {
                SetText(card, "detectors", "尚未配置检测");
                SetObjectXY(card, "detectors", 18f, 86f);
                SetObjectSize(card, "detectors", width - 36f, 64f);
                Fgui.GTextField empty = card.GetChild("detectors")?.asTextField;
                if (empty != null)
                {
                    empty.align = Fgui.AlignType.Center;
                    empty.color = new Color32(237, 160, 49, 255);
                }
            }
            SetObjectXY(card, "runtime", width - 112f, 10f);
            SetObjectSize(card, "runtime", 94f, 20f);

            SetObjectVisible(card, "entryBadge", stage?.Entry == true);
            SetObjectVisible(card, "entryLabel", stage?.Entry == true);
            SetText(card, "entryLabel", "入口");
            SetObjectVisible(card, "detectorOverflow", detectorCount > BehaviorLogicVisibleDetectorRows);
            if (detectorCount > BehaviorLogicVisibleDetectorRows)
            {
                int first = slot.DetectorScrollOffset + 1;
                int last = Math.Min(
                    detectorCount,
                    slot.DetectorScrollOffset + BehaviorLogicVisibleDetectorRows);
                SetText(
                    card,
                    "detectorOverflow",
                    first.ToString(CultureInfo.InvariantCulture)
                        + "-"
                        + last.ToString(CultureInfo.InvariantCulture)
                        + " / "
                        + detectorCount.ToString(CultureInfo.InvariantCulture));
            }

            bool showDetectorScroll = detectorCount > BehaviorLogicVisibleDetectorRows;
            SetObjectVisible(card, "detectorScrollTrack", showDetectorScroll);
            SetObjectVisible(card, "detectorScrollThumb", showDetectorScroll);
            if (showDetectorScroll)
            {
                const float trackHeight = 88f;
                float thumbHeight = Mathf.Max(
                    24f,
                    trackHeight * BehaviorLogicVisibleDetectorRows / detectorCount);
                float normalized = maximumDetectorOffset > 0
                    ? slot.DetectorScrollOffset / (float)maximumDetectorOffset
                    : 0f;
                SetObjectXY(card, "detectorScrollTrack", width - 7f, BehaviorLogicDetectorTop);
                SetObjectSize(card, "detectorScrollTrack", 3f, trackHeight);
                SetObjectXY(
                    card,
                    "detectorScrollThumb",
                    width - 7f,
                    BehaviorLogicDetectorTop + (trackHeight - thumbHeight) * normalized);
                SetObjectSize(card, "detectorScrollThumb", 3f, thumbHeight);
            }

            if (slot.CompactHeaderDivider != null)
            {
                slot.CompactHeaderDivider.visible = true;
                slot.CompactHeaderDivider.SetXY(18f, 68f);
                slot.CompactHeaderDivider.SetSize(width - 36f, 2f);
                slot.CompactHeaderDivider.DrawRect(
                    width - 36f,
                    2f,
                    0,
                    Color.clear,
                    new Color32(31, 115, 121, 190));
            }
            if (slot.CompactLaneDivider != null)
                slot.CompactLaneDivider.visible = false;

            Fgui.GTextField kind = card.GetChild("kind")?.asTextField;
            if (kind != null)
            {
                kind.align = Fgui.AlignType.Left;
                kind.color = new Color32(244, 247, 245, 255);
            }
            Fgui.GTextField time = card.GetChild("time")?.asTextField;
            if (time != null)
            {
                time.align = Fgui.AlignType.Left;
                time.color = new Color32(143, 214, 226, 255);
            }
            Fgui.GTextField runtime = card.GetChild("runtime")?.asTextField;
            if (runtime != null)
            {
                runtime.align = Fgui.AlignType.Right;
                runtime.color = new Color32(111, 227, 207, 255);
            }
            SetTimelineTextFontSize(kind, 22);
            SetTimelineTextFontSize(time, 14);
            SetTimelineTextFontSize(card.GetChild("detectors")?.asTextField, 16);
            SetTimelineTextFontSize(runtime, 12);
        }

        private static void ConfigureCompactBehaviorLogicStageCard(
            StageSlot slot,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int detectorCount,
            bool heroLayout)
        {
            Fgui.GComponent card = slot.Visual;
            if (!heroLayout)
            {
                ConfigureDenseBehaviorLogicStageCard(slot, stage, detectorCount);
                return;
            }
            float width = card.width;
            float height = card.height;
            SetObjectVisible(card, "background", true);
            SetObjectVisible(card, "stripe", true);
            Fgui.GGraph background = card.GetChild("background")?.asGraph;
            if (slot.CompactFrame != null)
            {
                slot.CompactFrame.visible = true;
                slot.CompactFrame.SetXY(0f, 0f);
                slot.CompactFrame.SetSize(width, height);
                slot.CompactFrame.DrawRoundRect(
                    width,
                    height,
                    ResolveCompactBehaviorLogicFrameColor(stage),
                    CompactStageFrameCornerRadii);
            }
            if (background != null)
            {
                background.SetXY(2f, 2f);
                background.SetSize(width - 4f, height - 4f);
                background.DrawRoundRect(
                    width - 4f,
                    height - 4f,
                    Color.white,
                    CompactStageFillCornerRadii);
            }
            SetObjectXY(card, "stripe", 24f, 22f);
            SetObjectSize(card, "stripe", 10f, height - 44f);
            SetObjectVisible(card, "title", false);
            SetObjectVisible(card, "kind", true);
            SetObjectXY(card, "kind", CompactBehaviorLogicHorizontalPadding, 24f);
            SetObjectSize(card, "kind", Math.Max(320f, width - 500f), 70f);
            SetObjectVisible(card, "time", false);
            SetObjectVisible(card, "stableId", false);
            bool showGuide = !string.IsNullOrWhiteSpace(card.GetChild("action")?.asTextField?.text);
            SetObjectVisible(card, "action", showGuide);
            if (showGuide)
            {
                SetObjectXY(card, "action", CompactBehaviorLogicHorizontalPadding, 88f);
                SetObjectSize(
                    card,
                    "action",
                    width - CompactBehaviorLogicHorizontalPadding * 2f,
                    28f);
                Fgui.GTextField guide = card.GetChild("action")?.asTextField;
                if (guide != null)
                    guide.color = new Color32(143, 214, 226, 220);
                SetTimelineTextFontSize(guide, 18);
            }
            SetObjectVisible(card, "detectors", false);
            SetObjectVisible(card, "transitions", false);
            SetObjectSize(card, "runtime", 304f, 54f);
            SetObjectXY(card, "runtime", Math.Max(480f, width - 382f), 32f);
            if (slot.CompactHeaderDivider != null)
            {
                slot.CompactHeaderDivider.visible = true;
                slot.CompactHeaderDivider.SetXY(
                    CompactBehaviorLogicHorizontalPadding,
                    124f);
                slot.CompactHeaderDivider.SetSize(
                    width - CompactBehaviorLogicHorizontalPadding * 2f,
                    2f);
                slot.CompactHeaderDivider.DrawRect(
                    slot.CompactHeaderDivider.width,
                    2f,
                    0,
                    Color.clear,
                    new Color32(31, 115, 121, 190));
            }
            if (slot.CompactLaneDivider != null)
            {
                slot.CompactLaneDivider.visible = true;
                slot.CompactLaneDivider.SetXY(width * 0.5f - 1f, 146f);
                slot.CompactLaneDivider.SetSize(2f, 62f);
                slot.CompactLaneDivider.DrawRect(
                    2f,
                    62f,
                    0,
                    Color.clear,
                    new Color32(42, 126, 132, 210));
            }
            Fgui.GTextField runtime = card.GetChild("runtime")?.asTextField;
            if (runtime != null)
            {
                runtime.align = Fgui.AlignType.Right;
                runtime.color = new Color32(111, 227, 207, 255);
            }
            Fgui.GTextField kind = card.GetChild("kind")?.asTextField;
            if (kind != null)
                kind.color = new Color32(244, 247, 245, 255);
            SetTimelineTextFontSize(kind, 44);
            SetTimelineTextFontSize(runtime, 30);
        }

        private static void ConfigureDenseBehaviorLogicStageCard(
            StageSlot slot,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int detectorCount)
        {
            Fgui.GComponent card = slot.Visual;
            float width = card.width;
            float height = card.height;
            Fgui.GGraph background = card.GetChild("background")?.asGraph;
            SetObjectVisible(card, "background", true);
            SetObjectVisible(card, "stripe", true);
            if (slot.CompactFrame != null)
            {
                slot.CompactFrame.visible = true;
                slot.CompactFrame.SetXY(0f, 0f);
                slot.CompactFrame.SetSize(width, height);
                slot.CompactFrame.DrawRoundRect(
                    width,
                    height,
                    ResolveCompactBehaviorLogicFrameColor(stage),
                    CompactStageFrameCornerRadii);
            }
            if (background != null)
            {
                background.SetXY(2f, 2f);
                background.SetSize(Math.Max(1f, width - 4f), Math.Max(1f, height - 4f));
                background.DrawRoundRect(
                    Math.Max(1f, width - 4f),
                    Math.Max(1f, height - 4f),
                    Color.white,
                    CompactStageFillCornerRadii);
            }
            SetObjectXY(card, "stripe", 16f, 18f);
            SetObjectSize(card, "stripe", 7f, Math.Max(24f, height - 36f));
            SetObjectVisible(card, "title", false);
            SetObjectVisible(card, "kind", true);
            SetObjectXY(card, "kind", 36f, 12f);
            SetObjectSize(card, "kind", Math.Max(100f, width - 72f), 38f);
            SetObjectVisible(card, "time", true);
            SetObjectXY(card, "time", 36f, 48f);
            SetObjectSize(card, "time", Math.Max(100f, width - 72f), 22f);
            SetObjectVisible(card, "stableId", false);
            bool showGuide = !string.IsNullOrWhiteSpace(card.GetChild("action")?.asTextField?.text);
            SetObjectVisible(card, "action", showGuide);
            if (showGuide)
            {
                SetObjectXY(card, "action", 36f, 76f);
                SetObjectSize(card, "action", Math.Max(100f, width - 72f), 28f);
                Fgui.GTextField guide = card.GetChild("action")?.asTextField;
                if (guide != null)
                {
                    guide.align = Fgui.AlignType.Left;
                    guide.color = new Color32(143, 214, 226, 220);
                }
                SetTimelineTextFontSize(guide, width >= 360f ? 16 : 13);
            }
            SetObjectVisible(card, "transitions", false);
            SetObjectVisible(card, "detectors", detectorCount == 0);
            if (detectorCount == 0)
            {
                SetText(card, "detectors", "尚未配置检测");
                SetObjectXY(card, "detectors", 36f, CompactBehaviorLogicDenseHeaderHeight + 12f);
                SetObjectSize(card, "detectors", Math.Max(100f, width - 72f), 42f);
                Fgui.GTextField empty = card.GetChild("detectors")?.asTextField;
                if (empty != null)
                    empty.color = new Color32(237, 160, 49, 255);
            }
            SetObjectXY(card, "runtime", Math.Max(36f, width * 0.52f), 48f);
            SetObjectSize(card, "runtime", Math.Max(80f, width * 0.48f - 36f), 20f);
            if (slot.CompactHeaderDivider != null)
            {
                slot.CompactHeaderDivider.visible = true;
                slot.CompactHeaderDivider.SetXY(36f, CompactBehaviorLogicDenseHeaderHeight - 2f);
                slot.CompactHeaderDivider.SetSize(Math.Max(40f, width - 72f), 2f);
                slot.CompactHeaderDivider.DrawRect(
                    slot.CompactHeaderDivider.width,
                    2f,
                    0,
                    Color.clear,
                    new Color32(31, 115, 121, 190));
            }
            if (slot.CompactLaneDivider != null)
                slot.CompactLaneDivider.visible = false;

            Fgui.GTextField kind = card.GetChild("kind")?.asTextField;
            if (kind != null)
            {
                kind.align = Fgui.AlignType.Left;
                kind.color = new Color32(244, 247, 245, 255);
            }
            Fgui.GTextField time = card.GetChild("time")?.asTextField;
            if (time != null)
            {
                time.align = Fgui.AlignType.Left;
                time.color = new Color32(143, 214, 226, 255);
            }
            Fgui.GTextField runtime = card.GetChild("runtime")?.asTextField;
            if (runtime != null)
            {
                runtime.align = Fgui.AlignType.Right;
                runtime.color = new Color32(111, 227, 207, 255);
            }
            int titleSize = width >= 500f ? 26 : width >= 320f ? 22 : 18;
            SetTimelineTextFontSize(kind, titleSize);
            SetTimelineTextFontSize(time, width >= 320f ? 16 : 14);
            SetTimelineTextFontSize(card.GetChild("detectors")?.asTextField, 18);
            SetTimelineTextFontSize(runtime, width >= 320f ? 15 : 13);
        }

        private static Color32 ResolveCompactBehaviorLogicFrameColor(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage?.ActionConflict == true)
                return new Color32(226, 82, 70, 255);
            if (stage?.Selected == true)
                return new Color32(68, 190, 229, 255);
            if (stage?.Current == true)
                return new Color32(63, 226, 199, 255);
            return new Color32(54, 151, 151, 255);
        }

        private static void HideCompactBehaviorLogicStageDecorations(StageSlot slot)
        {
            if (slot?.CompactFrame != null)
                slot.CompactFrame.visible = false;
            if (slot?.CompactHeaderDivider != null)
                slot.CompactHeaderDivider.visible = false;
            if (slot?.CompactLaneDivider != null)
                slot.CompactLaneDivider.visible = false;
        }

        private static void LayoutStageDetails(
            Fgui.GComponent card,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            int stageDetectorCount)
        {
            Fgui.GObject detectors = card.GetChild("detectors");
            Fgui.GObject transitions = card.GetChild("transitions");
            Fgui.GObject runtime = card.GetChild("runtime");
            if (detectors == null || transitions == null || runtime == null)
                return;

            float width = Math.Max(120f, card.width - 36f);
            float detailsTop = stageDetectorCount > 0 ? 116f : 138f;
            float detectorHeight = stageDetectorCount > 0
                ? Math.Max(50f, stageDetectorCount * (DetectorBandHeight + 4f))
                : EstimateWrappedTextHeight(stage.DetectorSummaryText, 50f);
            float transitionHeight = EstimateWrappedTextHeight(stage.TransitionSummaryText, 48f);
            detectors.visible = stageDetectorCount == 0;
            detectors.SetXY(18f, detailsTop);
            detectors.SetSize(width, detectorHeight);
            float transitionY = detailsTop + detectorHeight + 6f;
            transitions.SetXY(18f, transitionY);
            transitions.SetSize(width, transitionHeight);
            runtime.SetXY(18f, transitionY + transitionHeight + 4f);
            runtime.SetSize(width, 22f);
        }

        private static void SetObjectY(Fgui.GComponent root, string childName, float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.y = y;
        }

    }
}
