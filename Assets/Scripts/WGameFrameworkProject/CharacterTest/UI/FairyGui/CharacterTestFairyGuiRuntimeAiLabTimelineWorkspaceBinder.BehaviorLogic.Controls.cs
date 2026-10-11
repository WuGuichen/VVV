using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder
    {
        private void BindBehaviorLogicDetectorTypedControls(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector == null)
                return;
            bool enabled = CanEditBehaviorLogicFields(timeline);
            BindBehaviorLogicChoiceRow(
                details,
                "detectorTrigger",
                "planActionStartValue",
                new[] { "条件", "完成", "失败" },
                new[]
                {
                    detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                    detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted,
                    detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetDetectorTrigger(
                    detector.DetectorId,
                    (CharacterTestRuntimeAiLabLogicDetectorTrigger)index));
            BindBehaviorLogicChoiceRow(
                details,
                "detectorScope",
                "planActionCompleteValue",
                new[] { "整张卡", "当前阶段" },
                new[]
                {
                    detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                    detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetDetectorScope(
                    detector.DetectorId,
                    (CharacterTestRuntimeAiLabLogicDetectorScope)index));
            BindBehaviorLogicChoiceRow(
                details,
                "detectorWindow",
                "planActionFailureValue",
                new[] { "固定", "阶段末", "持续" },
                new[]
                {
                    detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed,
                    detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd,
                    detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetDetectorWindowMode(
                    detector.DetectorId,
                    index == 0
                        ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                        : index == 1
                            ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                            : CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite));
        }

        private void BindBehaviorLogicTransitionTypedControls(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition == null)
                return;
            bool enabled = CanEditBehaviorLogicFields(timeline);
            BindBehaviorLogicChoiceRow(
                details,
                "transitionPolicy",
                "planActionStartValue",
                new[] { "立即打断", "动作后", "保留动作" },
                new[]
                {
                    transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                    transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd,
                    transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetTransitionPolicy(
                    transition.TransitionId,
                    (CharacterTestRuntimeAiLabLogicTransitionPolicy)index));

            int stageCount = Math.Min(timeline.StageCount, timeline.Stages?.Length ?? 0);
            int optionCount = stageCount + 1;
            string[] titles = new string[optionCount];
            bool[] selected = new bool[optionCount];
            string[] targets = new string[optionCount];
            for (int i = 0; i < optionCount - 1; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[i];
                titles[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
                targets[i] = stage?.StageId ?? string.Empty;
                selected[i] = string.Equals(
                    transition.TargetStageId,
                    targets[i],
                    StringComparison.Ordinal);
            }
            int endIndex = optionCount - 1;
            titles[endIndex] = "结束";
            targets[endIndex] = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            selected[endIndex] = transition.EndsCard;
            BindBehaviorLogicChoiceRow(
                details,
                "transitionTarget",
                "planActionDurationValue",
                titles,
                selected,
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetTransitionTarget(
                    transition.TransitionId,
                    targets[index]));
        }

        private void BindBehaviorLogicFrameStepper(
            Fgui.GComponent details,
            string rowName,
            string valueName,
            string stageId,
            int frames,
            bool enabled)
        {
            HideBehaviorLogicValueForTypedControl(details, valueName);
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            Fgui.GObject value = details.GetChild(valueName);
            if (host == null || value == null)
                return;
            float y = value.y + 2f;
            AddBehaviorLogicTypedButton(
                host, rowName + "Minus", "−", value.x, y, 68f, 40f,
                selected: false, enabled: enabled && frames > 1,
                () => _behaviorCardWorkspaceBinder.TrySetStageDurationFrames(
                    stageId,
                    Math.Max(1, frames - 5)));
            AddBehaviorLogicTypedButton(
                host, rowName + "Value", frames + " 帧", value.x + 74f, y, 172f, 40f,
                selected: true, enabled: false, null);
            AddBehaviorLogicTypedButton(
                host, rowName + "Plus", "+", value.x + 252f, y, 76f, 40f,
                selected: false, enabled: enabled,
                () => _behaviorCardWorkspaceBinder.TrySetStageDurationFrames(
                    stageId,
                    frames + 5));
        }

        private void BindBehaviorLogicChoiceRow(
            Fgui.GComponent details,
            string rowName,
            string valueName,
            string[] titles,
            bool[] selected,
            bool enabled,
            Func<int, bool> apply)
        {
            HideBehaviorLogicValueForTypedControl(details, valueName);
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            Fgui.GObject value = details.GetChild(valueName);
            int count = Math.Min(titles?.Length ?? 0, selected?.Length ?? 0);
            if (host == null || value == null || count == 0)
                return;
            const float gap = 4f;
            float width = (value.width - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                AddBehaviorLogicTypedButton(
                    host,
                    rowName + i,
                    titles[i],
                    value.x + i * (width + gap),
                    value.y + 2f,
                    width,
                    Math.Max(1f, value.height - 4f),
                    selected[i],
                    enabled,
                    () =>
                    {
                        bool changed = apply?.Invoke(index) ?? false;
                        if (changed)
                            ApplyBehaviorLogicChoiceSelection(host, rowName, index, enabled);
                        return changed;
                    });
            }
        }

        private static bool CanEditBehaviorLogicFields(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            return timeline?.CanEdit == true
                && !timeline.RequiresDetectorWindowCanonicalization;
        }

        private static void ApplyBehaviorLogicChoiceSelection(
            Fgui.GComponent host,
            string rowName,
            int selectedIndex,
            bool editable)
        {
            if (host == null)
                return;
            for (int i = 0; i < host.numChildren; i++)
            {
                Fgui.GButton button = host.GetChild(rowName + i)?.asButton;
                if (button == null)
                    continue;
                bool selected = i == selectedIndex;
                button.touchable = editable && !selected;
                ApplyBehaviorLogicTypedButtonPresentation(button, selected, editable || selected);
            }
        }

        private Fgui.GButton AddBehaviorLogicTypedButton(
            Fgui.GComponent host,
            string name,
            string title,
            float x,
            float y,
            float width,
            float height,
            bool selected,
            bool enabled,
            Func<bool> action)
        {
            Fgui.GButton button = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "CompactNeutralButton")?.asButton;
            button ??= new Fgui.GButton();
            button.name = name;
            button.mode = Fgui.ButtonMode.Common;
            button.icon = string.Empty;
            button.text = title;
            button.titleFontSize = width < 72f ? 20 : 22;
            button.SetSize(width, height);
            button.SetXY(x, y);
            button.touchable = enabled && !selected && action != null;
            button.grayed = !enabled && !selected;
            BindTooltip(button, selected
                ? "当前选择"
                : enabled ? "点击切换为“" + title + "”" : "当前配置为只读，不能切换。");
            host.AddChild(button);
            ApplyBehaviorLogicTypedButtonPresentation(button, selected, enabled || selected);
            button.onTouchBegin.Set(context => context.StopPropagation());
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                if (button.touchable)
                    action?.Invoke();
            });
            return button;
        }

        private static void ApplyBehaviorLogicTypedButtonPresentation(
            Fgui.GButton button,
            bool selected,
            bool available)
        {
            Fgui.GGraph surface = button.GetChild("typedSurface")?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = "typedSurface",
                    touchable = false,
                    sortingOrder = 1
                };
                button.AddChild(surface);
            }
            surface.SetXY(0f, 0f);
            surface.SetSize(button.width, button.height);
            Color border = selected
                ? RuntimeFrameSrgb(65, 225, 218, 235)
                : RuntimeFrameSrgb(49, 86, 95, available ? (byte)210 : (byte)130);
            Color fill = selected
                ? RuntimeFrameSrgb(13, 112, 106, 245)
                : RuntimeFrameSrgb(10, 38, 47, available ? (byte)230 : (byte)175);
            surface.DrawRect(surface.width, surface.height, selected ? 2 : 1, border, fill);
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = selected
                    ? RuntimeFrameSrgb(239, 255, 253)
                    : RuntimeFrameSrgb(185, 210, 216);
                title.sortingOrder = 4;
            }
            Fgui.GObject icon = button.GetChild("icon");
            if (icon != null)
                icon.visible = false;
        }

        private static void HideBehaviorLogicValueForTypedControl(
            Fgui.GComponent details,
            string valueName)
        {
            SetTimelineChildVisible(details, valueName, false);
            SetTimelineChildVisible(details, valueName + "EditSurface", false);
        }

        private static Fgui.GComponent EnsureBehaviorLogicTypedControlHost(Fgui.GComponent details)
        {
            if (details == null)
                return null;
            Fgui.GComponent host = details.GetChild("behaviorLogicTypedControls")?.asCom;
            if (host == null)
            {
                host = new Fgui.GComponent
                {
                    name = "behaviorLogicTypedControls",
                    sortingOrder = 6
                };
                details.AddChild(host);
            }
            host.SetXY(0f, 0f);
            host.SetSize(details.width, details.height);
            host.visible = true;
            return host;
        }

        private bool HasFocusedBehaviorLogicTypedInput(Fgui.GComponent details)
        {
            Fgui.GComponent host =
                details?.GetChild("behaviorLogicTypedControls")?.asCom;
            return host != null
                && _activeBehaviorLogicTypedInput != null
                && ReferenceEquals(_activeBehaviorLogicTypedInput.parent, host);
        }

        private static void ResetBehaviorLogicTypedControlHost(Fgui.GComponent details)
        {
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            if (host == null)
                return;
            while (host.numChildren > 0)
                host.RemoveChildAt(host.numChildren - 1, true);
        }

        private static void BindBehaviorLogicContextSummary(
            Fgui.GComponent details,
            string title,
            string firstLine,
            string secondLine)
        {
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            if (host == null)
                return;
            var background = new Fgui.GGraph
            {
                name = "contextSummaryBackground",
                touchable = false,
                sortingOrder = 1
            };
            background.SetXY(36f, DecisionContentY + 318f);
            background.SetSize(448f, 170f);
            background.DrawRect(
                background.width,
                background.height,
                1,
                RuntimeFrameSrgb(41, 83, 92, 185),
                RuntimeFrameSrgb(7, 28, 36, 225));
            host.AddChild(background);
            AddBehaviorLogicContextText(
                host,
                "contextSummaryTitle",
                title,
                48f,
                DecisionContentY + 326f,
                424f,
                34f,
                22,
                RuntimeFrameSrgb(148, 205, 211));
            AddBehaviorLogicContextText(
                host,
                "contextSummaryFirst",
                firstLine,
                48f,
                DecisionContentY + 366f,
                424f,
                40f,
                20,
                RuntimeFrameSrgb(224, 239, 242));
            AddBehaviorLogicContextText(
                host,
                "contextSummarySecond",
                secondLine,
                48f,
                DecisionContentY + 414f,
                424f,
                40f,
                20,
                RuntimeFrameSrgb(180, 207, 213));
        }

        private static void AddBehaviorLogicContextText(
            Fgui.GComponent host,
            string name,
            string value,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color)
        {
            var text = new Fgui.GTextField
            {
                name = name,
                text = value ?? string.Empty,
                color = color,
                singleLine = true,
                verticalAlign = Fgui.VertAlignType.Middle,
                sortingOrder = 3
            };
            text.SetXY(x, y);
            text.SetSize(width, height);
            SetTimelineFontSize(text, fontSize);
            host.AddChild(text);
        }

        private static void SetBehaviorLogicTypedRowVisible(
            Fgui.GComponent details,
            string rowName,
            bool visible)
        {
            Fgui.GComponent host = details?.GetChild("behaviorLogicTypedControls")?.asCom;
            for (int i = 0; host != null && i < host.numChildren; i++)
            {
                Fgui.GObject child = host.GetChildAt(i);
                if (child?.name?.StartsWith(rowName, StringComparison.Ordinal) == true)
                    child.visible = visible;
            }
        }

        private static void ApplyBehaviorLogicInspectorPresentation(Fgui.GComponent details)
        {
            Fgui.GTextField title = details?.GetChild("planActionTitle")?.asTextField;
            if (title != null)
            {
                title.color = RuntimeFrameSrgb(238, 248, 250);
                title.sortingOrder = 4;
            }
            Fgui.GTextField identity = details?.GetChild("detailEntities")?.asTextField;
            if (identity != null)
            {
                identity.color = RuntimeFrameSrgb(148, 205, 211);
                identity.sortingOrder = 4;
            }
            Fgui.GObject background = details?.GetChild("planActionBackground");
            if (background != null)
                background.sortingOrder = 0;
        }

        private static void DrawBehaviorLogicInspectorFieldSurface(
            Fgui.GComponent details,
            string valueName,
            bool editable)
        {
            Fgui.GObject value = details?.GetChild(valueName);
            if (details == null || value == null)
                return;
            string surfaceName = valueName + "EditSurface";
            Fgui.GGraph surface = details.GetChild(surfaceName)?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = surfaceName,
                    touchable = false,
                    sortingOrder = 2
                };
                details.AddChild(surface);
            }
            surface.visible = value.visible;
            surface.SetXY(value.x - 8f, value.y + 2f);
            surface.SetSize(value.width + 8f, Math.Max(1f, value.height - 4f));
            Color border = editable
                ? RuntimeFrameSrgb(65, 225, 218, 220)
                : RuntimeFrameSrgb(40, 75, 84, 150);
            Color fill = editable
                ? RuntimeFrameSrgb(8, 55, 64, 235)
                : RuntimeFrameSrgb(8, 34, 43, 175);
            surface.DrawRect(surface.width, surface.height, editable ? 2 : 1, border, fill);
        }

        private static void SetBehaviorLogicInspectorFieldSurfacesVisible(
            Fgui.GComponent details,
            bool visible)
        {
            SetTimelineChildVisible(details, "planActionStartValueEditSurface", visible);
            SetTimelineChildVisible(details, "planActionDurationValueEditSurface", visible);
            SetTimelineChildVisible(details, "planActionCompleteValueEditSurface", visible);
            SetTimelineChildVisible(details, "planActionFailureValueEditSurface", visible);
        }

        private static int CountBehaviorLogicStageDetectors(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            int stageIndex)
        {
            int count = 0;
            int detectorCount = Math.Min(
                timeline?.DetectorCount ?? 0,
                timeline?.Detectors?.Length ?? 0);
            for (int i = 0; i < detectorCount; i++)
            {
                if (timeline.Detectors[i]?.StageIndex == stageIndex)
                    count++;
            }
            return count;
        }

        private static int CountBehaviorLogicStageTransitions(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            int stageIndex)
        {
            int count = 0;
            int transitionCount = Math.Min(
                timeline?.TransitionCount ?? 0,
                timeline?.Transitions?.Length ?? 0);
            for (int i = 0; i < transitionCount; i++)
            {
                if (timeline.Transitions[i]?.SourceStageIndex == stageIndex)
                    count++;
            }
            return count;
        }

        private static void SetBehaviorLogicInspectorChrome(Fgui.GComponent details)
        {
            SetTimelineChildVisible(details, "detailFrame", false);
            SetTimelineChildVisible(details, "detailTrace", false);
            SetTimelineChildVisible(details, "detailEvidence", false);
            SetTimelineChildVisible(details, "detailParticipants", false);
            SetTimelineChildVisible(details, "memberScrollHint", false);
            SetTimelineChildVisible(details, "btnLocateStart", false);
            SetTimelineChildVisible(details, "btnLocateEnd", false);
            SetTimelineChildVisible(details, "btnCloseDetail", false);
            for (int i = 1; i <= 5; i++)
                SetTimelineChildVisible(details, "memberRow" + i, false);
        }

        private static void ConfigureBehaviorLogicInspectorGeometry(Fgui.GComponent details)
        {
            ConfigureBehaviorPlanActionSummaryGeometry(details);
            ConfigureBehaviorPlanActionText(
                details.GetChild("detailTitle")?.asTextField,
                24f, DecisionContentY + 8f, 472f, 34f, 26);
            SetObjectGeometry(
                details.GetChild("planActionBackground"),
                24f, DecisionContentY + 52f, 472f, 528f);
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionTitle")?.asTextField,
                36f, DecisionContentY + 60f, 448f, 36f, 24);
            ConfigureBehaviorPlanActionRow(
                details, "planActionStartLabel", "planActionStartValue", DecisionContentY + 108f);
            ConfigureBehaviorPlanActionRow(
                details, "planActionDurationLabel", "planActionDurationValue", DecisionContentY + 158f);
            ConfigureBehaviorPlanActionRow(
                details, "planActionCompleteLabel", "planActionCompleteValue", DecisionContentY + 208f);
            ConfigureBehaviorPlanActionRow(
                details, "planActionFailureLabel", "planActionFailureValue", DecisionContentY + 258f);
        }

        private void BindBehaviorLogicStageActions(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            bool hasStage)
        {
            SetTimelineChildVisible(details, "btnLocateStart", false);
            BindBehaviorLogicActionButton(
                details, "btnLocateEnd", "删除阶段", 268f,
                hasStage && timeline?.CanEdit == true);
        }

        private void BindBehaviorLogicRemoveAction(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            string title)
        {
            BindBehaviorLogicActionButton(
                details, "btnLocateEnd", title, 268f, timeline?.CanEdit == true);
        }

        private static void BindBehaviorLogicActionButton(
            Fgui.GComponent details,
            string childName,
            string title,
            float x,
            bool enabled)
        {
            Fgui.GButton button = details?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.visible = true;
            button.touchable = enabled;
            button.grayed = !enabled;
            button.icon = string.Empty;
            button.text = title;
            button.SetSize(216f, 44f);
            button.SetXY(x, DecisionContentY + 510f);
            button.titleFontSize = 22;
            ApplyBehaviorLogicActionButtonPresentation(button, title, enabled);
        }

        private static void ApplyBehaviorLogicActionButtonPresentation(
            Fgui.GButton button,
            string title,
            bool enabled)
        {
            if (button == null)
                return;
            bool destructive = title?.StartsWith("删除", StringComparison.Ordinal) == true;
            string surfaceName = "behaviorLogicActionSurface";
            Fgui.GGraph surface = button.GetChild(surfaceName)?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = surfaceName,
                    touchable = false,
                    sortingOrder = 1
                };
                button.AddChild(surface);
            }
            surface.SetXY(0f, 0f);
            surface.SetSize(button.width, button.height);
            Color border;
            Color fill;
            Color textColor;
            if (!enabled)
            {
                border = RuntimeFrameSrgb(46, 69, 76, 150);
                fill = RuntimeFrameSrgb(13, 31, 39, 190);
                textColor = RuntimeFrameSrgb(92, 112, 119);
            }
            else if (destructive)
            {
                border = RuntimeFrameSrgb(241, 91, 101, 220);
                fill = RuntimeFrameSrgb(66, 22, 30, 235);
                textColor = RuntimeFrameSrgb(255, 157, 164);
            }
            else
            {
                border = RuntimeFrameSrgb(65, 225, 218, 230);
                fill = RuntimeFrameSrgb(15, 116, 109, 245);
                textColor = RuntimeFrameSrgb(238, 255, 253);
            }
            surface.DrawRect(surface.width, surface.height, enabled ? 2 : 1, border, fill);
            Fgui.GTextField titleField = button.GetChild("title")?.asTextField;
            if (titleField != null)
            {
                titleField.color = textColor;
                titleField.sortingOrder = 4;
            }
            Fgui.GObject icon = button.GetChild("icon");
            if (icon != null)
                icon.visible = false;
        }

        private void BindBehaviorLogicRepairAction(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            bool missingCondition = detector != null
                && detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                && detector.ConditionCount == 0;
            Fgui.GButton action = details?.GetChild("btnLocateStart")?.asButton;
            if (action == null)
                return;

            action.visible = missingCondition;
            action.touchable = missingCondition && timeline?.CanEdit == true;
            if (!missingCondition)
                return;

            action.text = "+ 添加条件";
            action.icon = string.Empty;
            action.SetSize(216f, 44f);
            action.SetXY(36f, DecisionContentY + 510f);
            action.titleFontSize = 22;
            action.grayed = !action.touchable;
            ApplyBehaviorLogicActionButtonPresentation(
                action,
                action.text,
                action.touchable);
            BindTooltip(
                action,
                timeline.CanEdit
                    ? "为当前条件出口添加第一个触发条件。"
                    : "当前为只读状态，不能修改行为卡。");
            SetTimelineChildVisible(details, "planActionFailureLabel", false);
            SetTimelineChildVisible(details, "planActionFailureValue", false);
            SetTimelineChildVisible(details, "planActionFailureValueEditSurface", false);
            SetBehaviorLogicTypedRowVisible(details, "detectorWindow", false);
        }

    }
}
