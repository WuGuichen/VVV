using System;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Presentation of bounded approach or retreat-to-distance with independent target-facing.</summary>
    /// <remarks>LowFreqAlloc. Reads authored controls and prepared distances without changing execution.</remarks>
    internal sealed class CharacterTestTacticalDistanceArrivalHint
    {
        internal const string RetreatTitle = "常规移动面向目标退至期望距离";
        internal bool IsRetreat { get; private set; }
        internal string TemplateId => IsRetreat ? "retreat-to-distance-face.v1" : "approach-face.v1";
        internal string DistanceToken => IsRetreat ? "{distance}" : "{approach}";
        internal string NodeId { get; private set; }
        internal string DefinitionId { get; private set; }
        internal string BlockId { get; private set; }
        internal string TextTemplate { get; private set; }
        internal string DetailsText { get; private set; }
        internal string MovementLabel { get; private set; }
        internal string DistanceLabel { get; private set; }
        internal string DurationText { get; private set; }
        internal int DurationFrames { get; private set; }
        internal string RangeText { get; private set; }
        internal CharacterTestTacticalBasicMovementHint Movement { get; private set; }
        internal string Text => TextTemplate.Replace("{movement}", MovementLabel).Replace(DistanceToken, DistanceLabel);

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events, int duration,
            double frameSeconds, CharacterTestTacticalCardInputs inputs, IMxUiTextProvider textProvider,
            out CharacterTestTacticalDistanceArrivalHint hint)
        {
            hint = null;
            var completion = events.FirstOrDefault(e => e.Operation?.Kind == "CompleteBlock");
            if (events.Length != (completion == null ? 2 : 3) || duration <= 0 || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var move = events.FirstOrDefault(e => e.Operation?.Kind == "Movement");
            var turn = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            if (move == null || turn == null || turn.Operation.Target != "currentCombatTarget"
                || block.Completion != "Timed" && (block.Completion != "Arrived" || block.CompletionEventId != move.EventId)
                || !string.IsNullOrEmpty(block.SuccessConditionEventId)) return false;
            var movement = move.Operation.Movement;
            if (movement == null || (movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                && movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose)
                || movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget) return false;
            bool retreat = movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose;
            var scoped = instance == "legacy-use" ? inputs : inputs?.ForInstance(instance);
            float? degrees = null;
            if (completion != null)
            {
                var scope = completion.ConditionRoot;
                var all = scope?.Children.FirstOrDefault();
                if (retreat || scope?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                    || scope.Subject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget || scope.Children.Count != 1
                    || all?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || all.Children.Count != 2) return false;
                var facing = all.Children.FirstOrDefault(n => n.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle
                    && n.Predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual
                    || definition.Source.PredicateBindings.Any(b => b.NodeId == n.NodeId && b.Kind == "TurnAligned" && b.SourceTurnEventId == turn.EventId));
                if (facing == null || !all.Children.Any(n => definition.Source.PredicateBindings.Any(b => b.NodeId == n.NodeId
                    && b.Kind == "MovementArrived" && b.SourceMovementEventId == move.EventId))) return false;
                try { degrees = facing.Predicate?.Value ?? turn.Operation.ResolveFacing(scoped).Tolerance; }
                catch (InvalidOperationException) { /* Keep the authored structure visible before action measurement is prepared. */ }
            }
            foreach (var ev in events.Where(e => e != completion))
                if (ev.TimingKind != "Duration" || ev.ConditionRoot != null || ev.MaxExecutionsPerPass != 1 || ev.CooldownFrames != 0
                    || ev.Start?.Kind != "Absolute" || ev.Start.Frame != 0 || ev.Start.Space != "Block"
                    || ev.End?.Kind != "Anchor" || ev.End.TargetKind != "Builtin" || ev.End.SourceEventId != block.BlockId
                    || ev.End.Anchor != "SimulationEnd" || ev.End.OffsetFrames != 0) return false;

            string L(string key, string fallback) => CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description." + key, fallback);
            string range = "—";
            try
            {
                var resolved = movement.Compile(definition.Source, scoped, move.Operation.Speed, move.EventId);
                if (movement.DistanceSource == "Manual" || scoped != null)
                    range = CharacterTestTacticalBlockDescription.DistanceText(resolved.IsOptimalRange ? resolved.Optimal : resolved.Minimum,
                        resolved.IsOptimalRange ? resolved.Optimal : resolved.Minimum, textProvider);
            }
            catch (InvalidOperationException) { /* An unbound action-based distance has no measured range. */ }
            catch (ArgumentException) { return false; }
            hint = new CharacterTestTacticalDistanceArrivalHint {
                IsRetreat = retreat,
                NodeId = instance, DefinitionId = definition.DefinitionId, BlockId = block.BlockId,
                TextTemplate = retreat ? L("retreat_to_distance_face", "后撤至指定距离") : L("approach_face", "{movement}"),
                DetailsText = (retreat
                    ? L("retreat_to_distance_face_details", "面向目标，以常规移动后撤至期望距离；到位后停止移动，按配置时长结束本段并继续。持续 {seconds} 秒。")
                    : completion != null && !degrees.HasValue
                        ? L("approach_face_completion_unbound_details", "面向目标，以常规移动接近至期望距离；到位且满足转向事件的面向参数时结束本段并继续。执行时限 {seconds} 秒。")
                    : completion != null
                        ? L("approach_face_completion_details", "面向目标，以常规移动接近至期望距离；到位且面向夹角不超过 {degrees}° 时结束本段并继续。执行时限 {seconds} 秒。")
                        : L("approach_face_details", "面向目标，以常规移动接近至期望距离；到位后停止移动，按配置时长结束本段并继续。持续 {seconds} 秒。"))
                    .Replace("{degrees}", degrees?.ToString("0.###", CultureInfo.InvariantCulture) ?? "—")
                    .Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                MovementLabel = L("basic_movement", CharacterTestTacticalBasicMovementHint.Title),
                DistanceLabel = L("approach_distance", "期望距离"), DurationFrames = duration,
                DurationText = L("maximum_duration_seconds", "≤ {seconds} 秒").Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                RangeText = range,
                Movement = new CharacterTestTacticalBasicMovementHint(instance, move.EventId, turn.EventId, move.Operation.Speed, turn.Operation.Speed)
            };
            return true;
        }
    }
}
