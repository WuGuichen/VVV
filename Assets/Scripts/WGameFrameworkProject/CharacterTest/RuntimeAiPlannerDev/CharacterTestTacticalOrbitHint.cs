using System;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Presentation of concurrent orbit movement and target-facing within one complete EventBlock.</summary>
    /// <remarks>LowFreqAlloc. Reads authored controls and prepared slot data without changing execution state.</remarks>
    internal sealed class CharacterTestTacticalOrbitHint
    {
        internal const string TemplateId = "orbit-face.v1";
        internal const string Title = "常规移动面向目标环绕";
        internal string NodeId { get; private set; }
        internal string DefinitionId { get; private set; }
        internal string BlockId { get; private set; }
        internal string TextTemplate { get; private set; }
        internal string DetailsText { get; private set; }
        internal string MovementLabel { get; private set; }
        internal string OrbitLabel { get; private set; }
        internal string DurationText { get; private set; }
        internal int DurationFrames { get; private set; }
        internal string RangeText { get; private set; }
        internal string DirectionText { get; private set; }
        internal int Side { get; private set; }
        internal CharacterTestTacticalBasicMovementHint Movement { get; private set; }
        internal string Text => TextTemplate.Replace("{movement}", MovementLabel).Replace("{orbit}", OrbitLabel);

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events, int duration,
            double frameSeconds, CharacterTestTacticalCardInputs inputs, IMxUiTextProvider textProvider,
            out CharacterTestTacticalOrbitHint hint)
        {
            hint = null;
            if (events.Length != 2 || duration <= 0 || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var move = events.FirstOrDefault(e => e.Operation?.Kind == "Movement");
            var turn = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            if (move == null || turn == null || turn.Operation.Target != "currentCombatTarget") return false;
            var movement = move.Operation.Movement;
            if (movement == null || movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget
                || movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget) return false;
            foreach (var ev in events)
                if (ev.TimingKind != "Duration" || ev.ConditionRoot != null || ev.MaxExecutionsPerPass != 1 || ev.CooldownFrames != 0
                    || ev.Start?.Kind != "Absolute" || ev.Start.Frame != 0 || ev.Start.Space != "Block"
                    || ev.End?.Kind != "Anchor" || ev.End.TargetKind != "Builtin" || ev.End.SourceEventId != block.BlockId
                    || ev.End.Anchor != "SimulationEnd" || ev.End.OffsetFrames != 0) return false;

            string L(string key, string fallback) => CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description." + key, fallback);
            string range = "—";
            try
            {
                var scoped = instance == "legacy-use" ? inputs : inputs?.ForInstance(instance);
                var resolved = movement.Compile(definition.Source, scoped, move.Operation.Speed, move.EventId);
                if (movement.DistanceSource == "Manual" || scoped != null)
                    range = CharacterTestTacticalBlockDescription.DistanceText(resolved.Minimum, resolved.Maximum, textProvider);
            }
            catch (InvalidOperationException) { /* An unbound action-based distance has no measured range. */ }
            catch (ArgumentException) { return false; }
            hint = new CharacterTestTacticalOrbitHint {
                NodeId = instance, DefinitionId = definition.DefinitionId, BlockId = block.BlockId,
                TextTemplate = movement.BehindTarget == null ? L("orbit_face", "环绕移动")
                    : movement.BehindTarget.Maintain ? L("behind_target_maintain", "保持在目标背后") : L("behind_target_arrive", "绕至目标背后"),
                DetailsText = (movement.BehindTarget == null
                    ? L("orbit_face_details", "面向目标，以常规移动按配置的距离和方向环绕；本段持续 {seconds} 秒。")
                    : movement.BehindTarget.Maintain
                        ? L("behind_target_maintain_details", "面向目标，以常规移动保持指定距离，沿较短环绕弧进入目标当前朝向的背后范围；到位停步并持续保持，目标移动或转向后再调整。本段持续 {seconds} 秒。")
                        : L("behind_target_arrive_details", "面向目标，以常规移动保持指定距离，沿较短环绕弧进入目标当前朝向的背后范围；到达后停止并完成。执行时限 {seconds} 秒。"))
                    .Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                MovementLabel = L("basic_movement", CharacterTestTacticalBasicMovementHint.Title),
                OrbitLabel = L("orbit", "环绕"), DurationFrames = duration,
                DurationText = L("duration_seconds", "{seconds} 秒").Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                RangeText = range, Side = movement.Side,
                DirectionText = movement.BehindTarget != null ? "就近" : movement.Side == 0 ? L("direction_auto", "自动")
                    : movement.Side > 0 ? L("direction_left", "向左") : L("direction_right", "向右"),
                Movement = new CharacterTestTacticalBasicMovementHint(instance, move.EventId, turn.EventId, move.Operation.Speed, turn.Operation.Speed)
            };
            return true;
        }
    }
}
