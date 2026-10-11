using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildTransitionWindowTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            CharacterCancelRuleJsonDto[] rules = action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            if (rules.Length == 0)
                parent.Add(new Label("尚未配置切换窗口。"));

            for (int i = 0; i < rules.Length; i++)
                BuildTransitionWindowInspector(parent, action, i);

            AddButtonRow(
                parent,
                ("添加切换窗口", () => AddTransitionWindowAtFrame(action, _timelinePlayback?.CurrentFrame ?? 0)));
        }

        private void BuildTransitionWindowInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            TransitionWindowTimelineAdapter adapter)
        {
            if (action == null || adapter == null)
            {
                parent.Add(new Label("切换窗口不存在。"));
                return;
            }

            CharacterCancelRuleJsonDto rule = adapter.GetRule();
            if (!adapter.TryGetCurrentIndex(out int index))
            {
                parent.Add(new Label("选中的切换窗口已不存在。"));
                return;
            }

            BuildTransitionWindowFields(parent, action, rule, index, adapter);
        }

        private void BuildTransitionWindowInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index)
        {
            CharacterCancelRuleJsonDto[] rules = action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            if (index < 0 || index >= rules.Length)
            {
                parent.Add(new Label("切换窗口不存在。"));
                return;
            }

            var adapter = new TransitionWindowTimelineAdapter(
                action,
                index,
                ResolveTimelineDurationFrames(action),
                new UnityEngine.Color(0.95f, 0.62f, 0.28f, 0.88f),
                canEdit: true);
            BuildTransitionWindowFields(parent, action, rules[index], index, adapter);
        }

        private void BuildTransitionWindowFields(
            VisualElement parent,
            CharacterActionJsonDto action,
            CharacterCancelRuleJsonDto rule,
            int index,
            TransitionWindowTimelineAdapter adapter)
        {
            if (rule == null)
            {
                rule = new CharacterCancelRuleJsonDto();
                if (action.CancelRules != null && index >= 0 && index < action.CancelRules.Length)
                    action.CancelRules[index] = rule;
            }

            VisualElement row = AddCard(parent, "切换窗口 / Transition Window " + index);
            row.Add(new Label(CharacterActionAuthoringText.TransitionWindowSummary(rule)));

            AddTimelineIntegerField(row, "开始帧", rule.StartFrame, value =>
                TransitionWindowTimelineAdapter.SetTransitionWindowRange(
                    action,
                    rule,
                    value,
                    rule.EndFrame,
                    ResolveTimelineDurationFrames(action)),
                requestFullTracksRefresh: true);
            AddTimelineIntegerField(row, "结束帧", rule.EndFrame, value =>
                TransitionWindowTimelineAdapter.SetTransitionWindowRange(
                    action,
                    rule,
                    rule.StartFrame,
                    value,
                    ResolveTimelineDurationFrames(action)),
                requestFullTracksRefresh: true);
            AddTransitionRequestTypeMaskField(row, rule);
            AddTransitionActionSlotMaskField(row, rule);
            row.Add(new Button(() => ApplyStructuralChange(() => adapter.Remove(), UiRefreshScope.Tracks))
            {
                text = "删除切换窗口",
            });
        }

        private void AddTransitionWindowAtFrame(CharacterActionJsonDto action, int frame)
        {
            if (action == null)
                return;

            ApplyStructuralChange(() =>
            {
                int durationFrames = ResolveTimelineDurationFrames(action);
                var rules = new List<CharacterCancelRuleJsonDto>(action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>());
                rules.Add(TransitionWindowTimelineAdapter.CreateDefaultRule(
                    action,
                    frame,
                    durationFrames));
                action.CancelRules = rules.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private static int ResolveTimelineDurationFrames(CharacterActionJsonDto action)
        {
            return CharacterActionTimelineLayout.ResolveDurationFrames(
                action?.DurationFrames,
                DefaultDurationFrames);
        }

        private void AddTransitionRequestTypeMaskField(
            VisualElement parent,
            CharacterCancelRuleJsonDto rule)
        {
            var field = new EnumFlagsField(
                "可响应请求类型",
                (CharacterActionRequestType)rule.RequestTypeMask)
            {
                tooltip = "按最终解析动作的请求类型匹配；Any (0) 表示任意请求类型。",
            };
            field.RegisterValueChangedCallback(evt => ApplyTimelineFieldChange(
                () => rule.RequestTypeMask = (int)(CharacterActionRequestType)evt.newValue,
                requestFullTracksRefresh: true));
            parent.Add(field);
        }

        private void AddTransitionActionSlotMaskField(
            VisualElement parent,
            CharacterCancelRuleJsonDto rule)
        {
            var field = new EnumFlagsField(
                "可切换 Slot 类型",
                (CharacterCancelInputMask)rule.ActionSlotMask)
            {
                tooltip = "按动作请求进入的 Slot 匹配；Any (0) 表示任意 Slot。",
            };
            field.RegisterValueChangedCallback(evt => ApplyTimelineFieldChange(
                () => rule.ActionSlotMask = (int)(CharacterCancelInputMask)evt.newValue,
                requestFullTracksRefresh: true));
            parent.Add(field);
        }

        private static string EmptyAsUnset(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

    }
}
