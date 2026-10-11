using System;
using System.Globalization;
using MxFramework.CharacterAction;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static void BuildBehaviorCardFlowRuler(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            if (preview == null)
                return;

            var ruler = new VisualElement();
            ruler.name = BehaviorCardFlowTimeRulerName;
            ruler.style.flexDirection = FlexDirection.Row;
            ruler.style.flexWrap = Wrap.Wrap;
            ruler.style.marginTop = 4;
            ruler.style.marginBottom = 4;
            root.Add(ruler);
            ruler.Add(CreateMiniBadge(preview.SummaryText, new Color(0.13f, 0.17f, 0.20f)));
            if (!string.IsNullOrWhiteSpace(preview.WarningText))
                ruler.Add(CreateMiniBadge("鎻愮ず: " + preview.WarningText, new Color(0.35f, 0.24f, 0.10f)));
            ruler.Add(CreateMiniBadge("成功分支", new Color(0.16f, 0.36f, 0.20f)));
            ruler.Add(CreateMiniBadge("失败分支", new Color(0.44f, 0.24f, 0.08f)));
            ruler.Add(CreateMiniBadge("条件满足分支", new Color(0.16f, 0.28f, 0.46f)));

            var tickRow = new VisualElement();
            tickRow.name = BehaviorCardBranchLegendName;
            tickRow.style.flexDirection = FlexDirection.Row;
            tickRow.style.flexWrap = Wrap.Wrap;
            tickRow.style.marginBottom = 6;
            root.Add(tickRow);
            tickRow.Add(CreateMiniBadge("全局帧刻度", new Color(0.20f, 0.22f, 0.24f)));
            int total = Math.Max(1, preview.TotalFrames);
            int tick = Math.Max(10, ((total + 4) / 5 / 10) * 10);
            for (int frame = 0; frame <= total; frame += tick)
            {
                tickRow.Add(CreateMiniBadge(
                    frame.ToString(CultureInfo.InvariantCulture),
                    new Color(0.14f, 0.16f, 0.18f)));
            }
        }

        private static string FormatBehaviorCardDurationChip(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            if (timing.DurationKnown)
            {
                return timing.DurationFrames.ToString(CultureInfo.InvariantCulture)
                    + " frames";
            }

            return "? frames";
        }

        private static void BuildBehaviorCardStepTimeline(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft draftStep,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step,
            bool expanded)
        {
            if (expanded)
            {
                Label title = CreateWrappedLabel("鏃堕棿杞达紙甯э級");
                title.style.marginTop = 4;
                title.style.color = new Color(0.84f, 0.88f, 0.92f);
                root.Add(title);
            }

            var track = new VisualElement();
            track.name = BehaviorCardStepTimelineTrackName;
            track.style.marginTop = 2;
            track.style.marginBottom = expanded ? 5 : 1;
            track.style.height = expanded ? 54 : 24;
            track.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
            track.style.borderTopWidth = 1;
            track.style.borderBottomWidth = 1;
            track.style.borderLeftWidth = 1;
            track.style.borderRightWidth = 1;
            track.style.borderTopColor = new Color(0.22f, 0.24f, 0.27f);
            track.style.borderBottomColor = new Color(0.22f, 0.24f, 0.27f);
            track.style.borderLeftColor = new Color(0.22f, 0.24f, 0.27f);
            track.style.borderRightColor = new Color(0.22f, 0.24f, 0.27f);
            root.Add(track);

            if (!expanded)
            {
                var compactMarker = new VisualElement();
                compactMarker.name = BehaviorCardStepCompactTimelineName;
                compactMarker.style.display = DisplayStyle.None;
                track.Add(compactMarker);
            }

            if (expanded)
                BuildBehaviorCardStepTickRow(track, step);

            var eventRow = new VisualElement();
            eventRow.style.flexDirection = FlexDirection.Row;
            eventRow.style.flexWrap = expanded ? Wrap.Wrap : Wrap.NoWrap;
            eventRow.style.paddingLeft = 6;
            eventRow.style.paddingRight = 6;
            eventRow.style.paddingTop = expanded ? 0 : 3;
            track.Add(eventRow);
            BuildBehaviorCardTimelineEvents(eventRow, draftStep, step, expanded);
        }

        private static void BuildBehaviorCardStepTickRow(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step)
        {
            var ticks = new VisualElement();
            ticks.style.flexDirection = FlexDirection.Row;
            ticks.style.paddingLeft = 6;
            ticks.style.paddingRight = 6;
            ticks.style.paddingTop = 4;
            ticks.style.marginBottom = 4;
            root.Add(ticks);

            int duration = Math.Max(1, step.DurationFrames);
            ticks.Add(BuildBehaviorCardTickLabel("0"));
            ticks.Add(BuildBehaviorCardTickLabel((duration / 3).ToString(CultureInfo.InvariantCulture)));
            ticks.Add(BuildBehaviorCardTickLabel(((duration * 2) / 3).ToString(CultureInfo.InvariantCulture)));
            ticks.Add(BuildBehaviorCardTickLabel(duration.ToString(CultureInfo.InvariantCulture)));
        }

        private static Label BuildBehaviorCardTickLabel(string text)
        {
            Label tick = CreateWrappedLabel(text);
            tick.style.flexGrow = 1;
            tick.style.unityTextAlign = TextAnchor.MiddleLeft;
            tick.style.color = new Color(0.70f, 0.76f, 0.82f);
            return tick;
        }

        private static void BuildBehaviorCardTimelineEvents(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft draftStep,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step,
            bool expanded)
        {
            if (draftStep.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
            {
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "浣嶇Щ/鍛戒腑 6-20" : "鍛戒腑",
                    new Color(0.16f, 0.42f, 0.22f)));
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded
                        ? "鍒ゆ柇 0-" + step.DurationFrames.ToString(CultureInfo.InvariantCulture)
                        : "鍒ゆ柇",
                    new Color(0.12f, 0.28f, 0.48f)));
                if (!step.DurationKnown)
                    root.Add(BuildBehaviorCardTimelineEvent(
                        expanded ? "鏃堕暱鏈煡" : "?",
                        new Color(0.32f, 0.32f, 0.32f)));
                return;
            }

            if (!step.DurationKnown)
            {
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鏃堕暱鏈煡" : "?",
                    new Color(0.32f, 0.32f, 0.32f)));
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鍒ゆ柇 " + FormatFrameRange(step) : "鍒ゆ柇",
                    new Color(0.12f, 0.28f, 0.48f)));
                return;
            }

            if (draftStep.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction
                && draftStep.CharacterActionId == new CharacterActionId(301))
            {
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鍛戒腑 18-24" : "鍛戒腑",
                    new Color(0.16f, 0.42f, 0.22f)));
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鍒囨崲绐楀彛 62-72" : "鍒囨崲",
                    new Color(0.58f, 0.36f, 0.10f)));
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "璺濈鍒ゆ柇 62-72" : "璺濈",
                    new Color(0.12f, 0.28f, 0.48f)));
                return;
            }

            if (draftStep.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction
                && draftStep.CharacterActionId == new CharacterActionId(324))
            {
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鍛戒腑 20-26" : "鍛戒腑",
                    new Color(0.16f, 0.42f, 0.22f)));
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "缁撴潫鍒ゆ柇 70-76" : "缁撴潫",
                    new Color(0.12f, 0.28f, 0.48f)));
                return;
            }

            if (step.HasConditionWindow)
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "鍒ゆ柇 " + FormatFrameRange(step) : "鍒ゆ柇",
                    new Color(0.12f, 0.28f, 0.48f)));
            else
                root.Add(BuildBehaviorCardTimelineEvent(
                    expanded ? "无显式判断窗口" : "无窗口",
                    new Color(0.24f, 0.25f, 0.26f)));
        }

        private static Label BuildBehaviorCardTimelineEvent(string text, Color color)
        {
            Label label = CreateMiniBadge(text, color);
            label.name = BehaviorCardStepTimelineEventName;
            label.style.marginRight = 5;
            label.style.marginBottom = 5;
            label.style.height = 18;
            label.style.minHeight = 18;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static VisualElement BuildBehaviorCardInfoLine(string label, string value, Color chipColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;

            Label name = CreateWrappedLabel(label);
            name.style.width = 44;
            name.style.color = new Color(0.72f, 0.78f, 0.84f);
            row.Add(name);

            Label text = CreateMiniBadge(value, chipColor);
            text.style.flexGrow = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);
            return row;
        }
    }
}
