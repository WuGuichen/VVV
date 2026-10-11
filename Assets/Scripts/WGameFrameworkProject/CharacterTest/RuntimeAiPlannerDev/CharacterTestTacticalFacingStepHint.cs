using System;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Strict presentation of target alignment followed by one functional forward step.</summary>
    /// <remarks>LowFreqAlloc. Reads source and prepared data only.</remarks>
    internal sealed class CharacterTestTacticalFacingStepHint
    {
        internal string NodeId, Text, DetailsText, RotationText, FacingText, PreparationText, DurationText;

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events,
            double frameSeconds, CharacterTestTacticalCardInputs inputs, IMxUiTextProvider provider,
            out CharacterTestTacticalFacingStepHint hint)
        {
            hint = null;
            if (events.Length != 3 || block.Completion != "Action" || !block.RequireLivingTarget
                || block.PreparationTimeoutFrames <= 0 || block.CompletionTimeoutFrames <= 0 || frameSeconds <= 0) return false;
            var face = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            var wait = events.FirstOrDefault(e => e.Operation?.Kind == "WaitForCondition");
            var step = events.FirstOrDefault(e => e.Operation?.Kind == "CharacterAction");
            if (face == null || wait == null || step == null || block.CompletionEventId != step.EventId
                || face.Operation.Target != "currentCombatTarget"
                || definition.Source.Slots.FirstOrDefault(s => s.SlotId == step.Operation.SlotId)?.Resolver != "stepForward"
                || !AtStart(face.Start) || !AtStart(wait.Start) || wait.TimingKind != "Instant"
                || !Anchor(face.End, step.EventId, "Start") || !Anchor(step.Start, wait.EventId, "End")
                || !CharacterTestTacticalApproachActionHint.Alive(face.ConditionRoot)
                || events.Any(e => e.MaxExecutionsPerPass != 1 || e.CooldownFrames != 0)
                || !FacingCondition(wait.ConditionRoot, false, out float tolerance)) return false;
            string L(string key, string fallback) => CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description." + key, fallback);
            string Time(string key, string fallback, int frames) => L(key, fallback).Replace("{seconds}",
                (frames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture));
            hint = new CharacterTestTacticalFacingStepHint { NodeId = instance,
                Text = block.ContinueToActionOnPreparationTimeout
                    ? L("facing_forward_step_bounded", "向前垫步")
                    : L("facing_forward_step", "向前垫步"),
                DetailsText = block.ContinueToActionOnPreparationTimeout
                    ? L("facing_forward_step_bounded_details", "尝试面向当前目标，达标或超时后按当前朝向播放向前垫步一次。")
                        + CharacterTestTacticalBlockDescription.ActionLimitsText(block, frameSeconds, provider)
                    : L("facing_forward_step_details", "面向当前存活目标并达到朝向容差后，播放向前垫步一次；转向超时则结束此行为。")
                        + CharacterTestTacticalBlockDescription.ActionLimitsText(block, frameSeconds, provider),
                RotationText = "×" + face.Operation.Speed.ToString("0.###", CultureInfo.InvariantCulture),
                FacingText = "±" + tolerance.ToString("0.###", CultureInfo.InvariantCulture) + "°",
                PreparationText = Time("maximum_duration_seconds", "≤ {seconds} 秒", block.PreparationTimeoutFrames),
                DurationText = inputs?.StepForwardAction == null ? "—" : Time("expected_duration_seconds", "≈ {seconds} 秒", inputs.StepForwardAction.DurationFrames.Value) };
            return true;
        }

        private static bool AtStart(CharacterTestTacticalFrameReference frame) => frame?.Kind == "Absolute" && frame.Frame == 0 && frame.Space == "Block";
        private static bool Anchor(CharacterTestTacticalFrameReference frame, string id, string side)
            => frame?.Kind == "Anchor" && frame.TargetKind == "Event" && frame.SourceEventId == id && frame.Anchor == side && frame.OffsetFrames == 0;
        private static bool FacingCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, bool idle, out float tolerance, bool requireFacing = true)
        {
            tolerance = 0;
            if (root?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                || root.Children.Count != 1 + (idle ? 1 : 0) + (requireFacing ? 1 : 0)) return false;
            int alive = 0, facing = 0, neutral = 0;
            foreach (var child in root.Children)
            {
                if (CharacterTestTacticalApproachActionHint.Alive(child)) { alive++; continue; }
                if (child.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope || child.Children.Count != 1) return false;
                var leaf = child.Children[0]; var p = leaf.Predicate;
                if (leaf.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate || p == null) return false;
                if (child.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                    && p.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle && p.Operator == CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual)
                { tolerance = p.Value; facing++; }
                else if (child.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self
                    && p.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible
                    && p.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals && p.BoolValue) neutral++;
                else return false;
            }
            return alive == 1 && facing == (requireFacing ? 1 : 0) && neutral == (idle ? 1 : 0) && tolerance >= 0 && tolerance <= 180;
        }
    }
}
