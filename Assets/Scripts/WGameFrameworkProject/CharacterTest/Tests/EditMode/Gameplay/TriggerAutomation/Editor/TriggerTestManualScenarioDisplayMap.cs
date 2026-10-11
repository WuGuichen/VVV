using System;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal readonly struct TriggerTestManualScenarioDisplay
    {
        internal TriggerTestManualScenarioDisplay(
            string displayName,
            string groupName,
            int order)
        {
            DisplayName = displayName ?? string.Empty;
            GroupName = groupName ?? string.Empty;
            Order = order;
        }

        internal string DisplayName { get; }
        internal string GroupName { get; }
        internal int Order { get; }
    }

    /// <summary>
    /// Resolves Editor-only labels, groups and ordering for manual scenarios.
    /// This map does not participate in discovery or runtime registration.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. Used only while rebuilding the
    /// manual Editor scenario list.
    /// </remarks>
    internal static class TriggerTestManualScenarioDisplayMap
    {
        private const string EnvironmentGroup = "环境准备";
        private const string ControlGroup = "测试控制";
        private const string TimelineGroup = "时间轴观察";
        private const string SetupGroup = "实验设置";
        private const string OtherGroup = "其他";
        private const int FallbackOrder = 100;

        private static readonly Entry[] Entries =
        {
            new Entry(
                "runtime-ai-lab-open-scene",
                "打开 Runtime AI Lab 场景",
                EnvironmentGroup,
                0),
            new Entry(
                "runtime-ai-lab-open-running-behavior-logic",
                "一键打开行为逻辑编辑",
                EnvironmentGroup,
                1),
            new Entry(
                "runtime-ai-lab-character-action-authoring",
                "完整 CharacterAction 动作编排",
                EnvironmentGroup,
                2),
            new Entry(
                "runtime-ai-lab-enter-play-mode",
                "进入 Play Mode",
                EnvironmentGroup,
                2),
            new Entry(
                "runtime-ai-lab-start-test",
                "开始测试",
                ControlGroup,
                2),
            new Entry(
                "runtime-ai-lab-player-publish-flow",
                "玩家创作、发布与 Apply F+1",
                ControlGroup,
                25),
            new Entry(
                "runtime-ai-lab-playmode-step",
                "真实场景 · 推进一帧",
                ControlGroup,
                3),
            new Entry(
                "runtime-ai-lab-resume-simulation",
                "继续模拟",
                ControlGroup,
                4),
            new Entry(
                "runtime-ai-lab-pause-simulation",
                "暂停模拟",
                ControlGroup,
                5),
            new Entry(
                "runtime-ai-lab-reset-test",
                "重置测试",
                ControlGroup,
                6),
            new Entry(
                "runtime-ai-lab-select-historical-frame",
                "定位到指定历史帧",
                TimelineGroup,
                7),
            new Entry(
                "runtime-ai-lab-select-latest-frame",
                "定位到最新帧",
                TimelineGroup,
                8),
            new Entry(
                "runtime-ai-lab-request-branch-recompute",
                "开启历史帧重算",
                TimelineGroup,
                9),
            new Entry(
                "runtime-ai-lab-confirm-branch-recompute",
                "确认建立新未来",
                TimelineGroup,
                10),
            new Entry(
                "runtime-ai-lab-cancel-branch-recompute",
                "取消历史帧重算",
                TimelineGroup,
                11),
            new Entry(
                "runtime-ai-lab-zoom-in-timeline",
                "放大时间轴",
                TimelineGroup,
                12),
            new Entry(
                "runtime-ai-lab-zoom-out-timeline",
                "缩小时间轴",
                TimelineGroup,
                13),
            new Entry(
                "runtime-ai-lab-fit-timeline",
                "适配完整时间轴",
                TimelineGroup,
                14),
            new Entry(
                "runtime-ai-lab-select-timeline-event",
                "选择指定时间轴事件",
                TimelineGroup,
                15),
            new Entry(
                "runtime-ai-lab-locate-event-start",
                "定位事件起点",
                TimelineGroup,
                16),
            new Entry(
                "runtime-ai-lab-locate-event-end",
                "定位事件终点",
                TimelineGroup,
                17),
            new Entry(
                "runtime-ai-lab-close-event-details",
                "关闭事件详情",
                TimelineGroup,
                18),
            new Entry(
                "runtime-ai-lab-switch-timeline-mode",
                "切换运行轨迹 / 行为计划",
                TimelineGroup,
                19),
            new Entry(
                "runtime-ai-lab-open-behavior-logic-tab",
                "打开行为逻辑编辑模式",
                TimelineGroup,
                20),
            new Entry(
                "runtime-ai-lab-select-setup-slot",
                "选择测试槽位",
                SetupGroup,
                21),
            new Entry(
                "runtime-ai-lab-select-setup-character",
                "选择槽位角色",
                SetupGroup,
                22),
            new Entry(
                "runtime-ai-lab-select-setup-behavior-group",
                "选择槽位行为组",
                SetupGroup,
                23),
            new Entry(
                "runtime-ai-lab-enable-setup-slot",
                "启用所选槽位",
                SetupGroup,
                24),
            new Entry(
                "runtime-ai-lab-disable-setup-slot",
                "禁用所选槽位",
                SetupGroup,
                25),
            new Entry(
                "runtime-ai-lab-training-dummy-copy-flow",
                "使用木桩副本开始测试",
                SetupGroup,
                26)
        };

        internal static int Count => Entries.Length;

        internal static TriggerTestManualScenarioDisplay Resolve(
            TriggerTestDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return new TriggerTestManualScenarioDisplay(
                    string.Empty,
                    OtherGroup,
                    FallbackOrder);
            }

            string slug =
                TriggerTestIdentity.GetScenarioSlug(descriptor.ScenarioId);
            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
                if (string.Equals(
                        entry.Slug,
                        slug,
                        StringComparison.Ordinal))
                {
                    return new TriggerTestManualScenarioDisplay(
                        entry.DisplayName,
                        entry.GroupName,
                        entry.Order);
                }
            }

            return new TriggerTestManualScenarioDisplay(
                string.IsNullOrWhiteSpace(descriptor.DisplayName)
                    ? slug
                    : descriptor.DisplayName,
                OtherGroup,
                FallbackOrder);
        }

        private readonly struct Entry
        {
            internal Entry(
                string slug,
                string displayName,
                string groupName,
                int order)
            {
                Slug = slug;
                DisplayName = displayName;
                GroupName = groupName;
                Order = order;
            }

            internal string Slug { get; }
            internal string DisplayName { get; }
            internal string GroupName { get; }
            internal int Order { get; }
        }
    }
}
