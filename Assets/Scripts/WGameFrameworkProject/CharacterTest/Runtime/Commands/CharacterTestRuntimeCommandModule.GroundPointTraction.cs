using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestRuntimeCommandFactory
    {
        internal const int GroundPointTractionEnvelopeTypeId = 901105;
        internal static RuntimeCommand GroundPointTraction(RuntimeFrame frame, int entityId, GameplayEntityId actor,
            ulong stageRun, ulong executionId, TacticalWorldPoint anchor, int range, int stop, int maximumFrames,
            int speed, int acceleration, bool cancel = false)
        {
            return new RuntimeCommand(frame, entityId, CharacterTestRuntimeCommandIds.GroundPointTraction, entityId,
                cancel ? 2 : 1, stop, maximumFrames,
                envelope: new RuntimeCommandEnvelope(GroundPointTractionEnvelopeTypeId, 1,
                    RuntimeCommandProvenanceKind.Unspecified, unchecked((long)executionId),
                    (uint)actor.Index | ((long)actor.Generation << 32),
                    (uint)anchor.X | ((long)anchor.Z << 32), (uint)anchor.Y | ((long)range << 32),
                    unchecked((long)stageRun), speed, acceleration));
        }
        internal static bool IsGroundPointTractionValid(RuntimeCommand command)
        {
            var e = command.Envelope;
            return command.TargetId > 0 && command.SourceId == command.TargetId
                && (command.Payload0 == 1 || command.Payload0 == 2) && command.Payload1 > 0
                && command.Payload1 < (int)(e.Data3 >> 32) && command.Payload2 > 0 && command.Payload2 <= 600
                && e.TypeId == GroundPointTractionEnvelopeTypeId && e.SchemaVersion == 1
                && e.Provenance == RuntimeCommandProvenanceKind.Unspecified && e.Data0 > 0 && e.Data4 != 0
                && (int)e.Data1 > 0 && (int)(e.Data1 >> 32) > 0
                && (int)(e.Data3 >> 32) > 0 && (int)(e.Data3 >> 32) <= 100000
                && e.Data5 >= 100 && e.Data5 <= 20000 && e.Data6 >= 100 && e.Data6 <= 100000 && e.Data7 == 0;
        }
    }

    public sealed partial class CharacterTestRuntimeCommandModule
    {
        private void ApplyGroundPointTraction(RuntimeCommand command)
        {
            var actor = _characterRuntimeModule?.FindEntityById(command.TargetId);
            if (actor == null) return; // Missing generation cannot mutate a replacement actor.
            var e = command.Envelope;
            var identity = new GameplayEntityId((int)e.Data1, (int)(e.Data1 >> 32));
            if (actor.GameplayEntityId != identity) return;
            var prior = actor.GroundPointTraction;
            ulong executionId = unchecked((ulong)e.Data0), stageRun = unchecked((ulong)e.Data4);
            if (command.Payload0 == 2)
            {
                if (prior.IsActive && prior.Matches(stageRun, executionId))
                { prior.Finish(CharacterTestGroundPointTractionEnd.Cancelled, command.Frame.Value); actor.SetGroundPointTraction(prior); }
                return;
            }
            if (prior.IsActive || prior.Matches(stageRun, executionId)) return; // No restart or cross-owner replacement.
            var state = new CharacterTestGroundPointTractionState {
                StageRun = stageRun, ExecutionId = executionId, Actor = identity,
                AnchorX = (int)e.Data2, AnchorZ = (int)(e.Data2 >> 32), AnchorY = (int)e.Data3,
                StopMillimeters = command.Payload1, MaximumFrames = command.Payload2,
                SpeedMillimetersPerSecond = (int)e.Data5, AccelerationMillimetersPerSecondSquared = (int)e.Data6,
                AcceptedTick = command.Frame.Value, LastTick = command.Frame.Value - 1, StartedTick = -1,
                End = CharacterTestGroundPointTractionEnd.Active };
            var navigation = new CharacterTestTacticalPointNavigation(_characterRuntimeModule, actor, null);
            var anchor = new TacticalWorldPoint(state.AnchorX, state.AnchorY, state.AnchorZ);
            bool ground = navigation.TryGetPointOrigin(out var origin)
                && navigation.ValidateTraction(identity, origin, anchor, (int)(e.Data3 >> 32)) == TacticalInputStatus.Valid;
            if (!actor.CombatState.IsAlive || !actor.IsGrounded || !actor.BattleState.CanStartNormalAction
                || actor.BattleState.DominantReason != CharacterTestBattleStateDominantReason.None
                || actor.Action.State == CharacterTestActionStateId.Running || !ground)
                state.Finish(CharacterTestGroundPointTractionEnd.Rejected, command.Frame.Value);
            actor.SetGroundPointTraction(state);
        }
    }
}