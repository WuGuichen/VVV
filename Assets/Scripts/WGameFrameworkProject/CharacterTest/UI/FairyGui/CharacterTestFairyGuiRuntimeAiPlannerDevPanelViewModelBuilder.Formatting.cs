using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static string FormatGameplayParameters(
            CharacterTestRuntimeAiLabBehaviorGroupDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (entry == null || definition == null)
                return string.Empty;
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                int minimum = entry.GetInt(
                    definition,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey);
                int maximum = entry.GetInt(
                    definition,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey);
                int cooldown = entry.GetInt(
                    definition,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionCooldownFramesKey);
                return "玩法参数：预计命中前 "
                    + minimum.ToString(CultureInfo.InvariantCulture)
                    + "–"
                    + maximum.ToString(CultureInfo.InvariantCulture)
                    + " 帧触发 | 冷却 "
                    + cooldown.ToString(CultureInfo.InvariantCulture)
                    + " 帧 | 同一次攻击只响应一次";
            }
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                return "玩法参数：持续靠近，距离 "
                    + (draft?.Profile?.AttackRange ?? 0f).ToString("0.#", CultureInfo.InvariantCulture)
                    + "m 时停止；被闪避抢占后自动恢复";
            }
            return string.Empty;
        }

        private static string FormatCompileBadge(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            bool migratedFromLegacyPayload)
        {
            if (compile == null)
                return "待处理";
            if (!compile.Success || compile.Program == null)
                return "失败";
            return migratedFromLegacyPayload ? "兼容" : "就绪";
        }

        private static string FormatCardCompileStatus(
            CharacterTestRuntimeAiLabBehaviorCardCompileResult compile)
        {
            if (compile == null)
                return "等待编译";
            if (compile.Success)
            {
                string hash = unchecked((ulong)compile.Program.ContentHash)
                    .ToString("X16", CultureInfo.InvariantCulture);
                return "编译就绪 " + hash;
            }

            if (compile.Diagnostics.Count == 0)
                return "编译失败";
            CharacterTestRuntimeAiLabCardDiagnostic first = compile.Diagnostics[0];
            return "编译失败 " + first.Code + " @ " + first.Path;
        }

        internal static CharacterTestRuntimeAiPlannerDevPresentationContext ResolvePresentationContext(
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot)
        {
            if (snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                return CharacterTestRuntimeAiPlannerDevPresentationContext.Setup;
            if (snapshot.Phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
                return CharacterTestRuntimeAiPlannerDevPresentationContext.Completed;
            if (snapshot.LoopPlaybackActive)
                return CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview;
            if (!snapshot.Paused)
                return CharacterTestRuntimeAiPlannerDevPresentationContext.Live;

            CharacterTestRuntimeAiPlannerDevTimeline timeline = snapshot.Timeline;
            if (timeline != null
                && timeline.HasSelection
                && timeline.HasSelection
                && timeline.SelectedFrame < timeline.LatestFrame)
            {
                return CharacterTestRuntimeAiPlannerDevPresentationContext.Review;
            }
            return CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect;
        }

        internal static string BuildPhaseText(
            CharacterTestRuntimeAiPlannerDevPresentationContext context,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.Setup)
                return "配置 | 设置双方队伍";
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.Live)
                return "运行中 | 帧 " + FormatFrame(timeline?.LatestFrame ?? 0);
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.Review)
                return "回看 | 帧 " + FormatFrame(timeline?.SelectedFrame ?? 0);
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview)
            {
                return "循环回看 | " + FormatFrame(timeline?.LoopStartFrame ?? 0)
                    + "-" + FormatFrame(timeline?.LoopEndFrame ?? 0)
                    + " | 帧 " + FormatFrame(timeline?.SelectedFrame ?? 0);
            }
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect)
                return "已暂停 | 帧 " + FormatFrame(timeline?.SelectedFrame ?? 0);
            if (context == CharacterTestRuntimeAiPlannerDevPresentationContext.Completed)
                return "已完成 | 最终帧 " + FormatFrame(timeline?.LatestFrame ?? 0);
            return "不可用";
        }

        private static string FormatFrame(int frame)
        {
            return frame >= 0 ? frame.ToString(CultureInfo.InvariantCulture) : "-";
        }

        private static string FormatTimelineMode(CharacterTestRuntimeAiPlannerDevTimelineMode mode)
        {
            switch (mode)
            {
                case CharacterTestRuntimeAiPlannerDevTimelineMode.Setup:
                    return "配置";
                case CharacterTestRuntimeAiPlannerDevTimelineMode.Live:
                    return "实时";
                case CharacterTestRuntimeAiPlannerDevTimelineMode.Paused:
                    return "暂停";
                case CharacterTestRuntimeAiPlannerDevTimelineMode.Review:
                    return "回看";
                case CharacterTestRuntimeAiPlannerDevTimelineMode.LoopReview:
                    return "循环回看";
                default:
                    return "定位中";
            }
        }
    }
}
