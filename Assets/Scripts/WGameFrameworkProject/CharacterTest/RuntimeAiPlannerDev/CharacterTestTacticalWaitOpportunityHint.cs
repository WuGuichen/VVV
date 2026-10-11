using System;
using System.Globalization;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalFacingTargetHint
    {
        internal string NodeId, Text, DetailsText;
        internal static bool TryParse(string instance, CharacterTestTacticalEventBlock block,
            CharacterTestTacticalAuthoringEvent[] events, out CharacterTestTacticalFacingTargetHint hint)
            => TryParse(instance, block, events, null, 1d / CharacterTestDeterministicUnits.DefaultTickRate, out hint);

        internal static bool TryParse(string instance, CharacterTestTacticalEventBlock block,
            CharacterTestTacticalAuthoringEvent[] events, IMxUiTextProvider provider, double frameSeconds,
            out CharacterTestTacticalFacingTargetHint hint)
        {
            hint = null;
            if (block.Completion != "Facing" || !block.RequireLivingTarget || block.Timing?.DurationFrames < 1 || events.Length != 2
                || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var gate = Array.Find(events, e => e.EventId == block.CompletionEventId && e.Operation?.Kind == "WaitForCondition");
            if (gate == null || !CharacterTestTacticalAuthoringCompiler.TryFacingThreshold(gate.ConditionRoot, out float threshold)
                || !Array.Exists(events, e => e.Operation?.Kind == "TurnToTarget" && e.Operation.Target == "currentCombatTarget")) return false;
            hint = new CharacterTestTacticalFacingTargetHint { NodeId = instance,
                Text = CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.facing_target", "面向目标"),
                DetailsText = CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.facing_target_details",
                    "转向当前存活目标，朝向夹角小于 {degrees}° 时本段完成；最多转向 {seconds} 秒，超时结束本段。")
                    .Replace("{degrees}", threshold.ToString("0.###", CultureInfo.InvariantCulture))
                    .Replace("{seconds}", (block.Timing.DurationFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)) };
            return true;
        }
    }

    /// <summary>Displays the wait contract; the author timeline retains the condition and independent upper limit.</summary>
    internal sealed class CharacterTestTacticalWaitOpportunityHint
    {
        internal string NodeId, Text, DetailsText;
        internal static bool TryParse(string instance, CharacterTestTacticalEventBlock block,
            CharacterTestTacticalAuthoringEvent[] events, int maximumFrames, double frameSeconds,
            IMxUiTextProvider provider, out CharacterTestTacticalWaitOpportunityHint hint)
        {
            hint = null;
            if (block.Completion != "Condition" || !block.RequireLivingTarget || block.Timing == null
                || events.Length != 1 || events[0].Operation?.Kind != "WaitForCondition"
                || events[0].EventId != block.CompletionEventId || maximumFrames < 1
                || events[0].TimingKind != "Instant" || events[0].MaxExecutionsPerPass != 1 || events[0].CooldownFrames != 0
                || events[0].Start?.Kind != "Absolute" || events[0].Start.Space != "Block" || events[0].Start.Frame != 0
                || !IsDefaultCondition(events[0].ConditionRoot)
                || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            string text = CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.wait_condition", "等待条件成立");
            string details = block.PassiveWait ? CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.wait_condition_details",
                "不主动移动，等待进入时目标当前有效攻击窗口结束后继续后续行为；最多等待 {seconds} 秒，超时结束本段，不自动攻击。") : CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.wait_opportunity",
                "停止主动移动，等待进入时目标当前有效攻击窗口结束；最多等待 {seconds} 秒。本段成功结束或独立超时，不自动攻击。");
            hint = new CharacterTestTacticalWaitOpportunityHint { NodeId = instance,
                Text = text,
                DetailsText = details.Replace("{seconds}", (maximumFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)) };
            return true;
        }

        private static bool IsDefaultCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root)
        {
            if (root?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || root.Children.Count != 2) return false;
            int alive = 0, ended = 0;
            foreach (var node in root.Children)
            {
                if (CharacterTestTacticalApproachActionHint.Alive(node)) { alive++; continue; }
                if (node?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                    || node.Subject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget || node.Children.Count != 1) return false;
                var fact = node.Children[0];
                if (fact.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                    || fact.Predicate?.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded
                    || fact.Predicate.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals || !fact.Predicate.BoolValue) return false;
                ended++;
            }
            return alive == 1 && ended == 1;
        }
    }
}
