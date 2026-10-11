using System;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Presentation of concurrent distance maintenance and target-facing within a complete EventBlock.</summary>
    /// <remarks>LowFreqAlloc. Reads authored controls and prepared distances without changing execution.</remarks>
    internal sealed class CharacterTestTacticalKeepDistanceHint
    {
        internal const string TemplateId = "keep-distance-face.v1";
        internal const string Title = "常规移动面向目标守距";
        internal string NodeId { get; private set; }
        internal string DefinitionId { get; private set; }
        internal string BlockId { get; private set; }
        internal string TextTemplate { get; private set; }
        internal string DetailsText { get; private set; }
        internal string MovementLabel { get; private set; }
        internal string KeepDistanceLabel { get; private set; }
        internal string DurationText { get; private set; }
        internal int DurationFrames { get; private set; }
        internal string RangeText { get; private set; }
        internal CharacterTestTacticalBasicMovementHint Movement { get; private set; }
        internal string Text => TextTemplate.Replace("{movement}", MovementLabel).Replace("{keepDistance}", KeepDistanceLabel);

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events, int duration,
            double frameSeconds, CharacterTestTacticalCardInputs inputs, IMxUiTextProvider textProvider,
            out CharacterTestTacticalKeepDistanceHint hint)
        {
            hint = null;
            if (events.Length != 2 || duration <= 0 || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var move = events.FirstOrDefault(e => e.Operation?.Kind == "Movement");
            var turn = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            if (move == null || turn == null || turn.Operation.Target != "currentCombatTarget") return false;
            var movement = move.Operation.Movement;
            if (movement == null || movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
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
            hint = new CharacterTestTacticalKeepDistanceHint {
                NodeId = instance, DefinitionId = definition.DefinitionId, BlockId = block.BlockId,
                TextTemplate = L("keep_distance_face", "保持距离"),
                DetailsText = L("keep_distance_face_details", "面向目标，以常规移动保持期望距离；本段持续 {seconds} 秒。")
                    .Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                MovementLabel = L("basic_movement", CharacterTestTacticalBasicMovementHint.Title),
                KeepDistanceLabel = L("keep_distance", "保持期望距离"), DurationFrames = duration,
                DurationText = L("duration_seconds", "{seconds} 秒").Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                RangeText = range,
                Movement = new CharacterTestTacticalBasicMovementHint(instance, move.EventId, turn.EventId, move.Operation.Speed, turn.Operation.Speed)
            };
            return true;
        }
    }
}
