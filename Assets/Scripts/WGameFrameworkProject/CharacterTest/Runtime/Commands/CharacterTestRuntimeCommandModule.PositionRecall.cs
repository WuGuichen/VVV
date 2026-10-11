using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestRuntimeCommandFactory
    {
        internal const int PositionRecallEnvelopeTypeId = 901106;
        internal static RuntimeCommand PositionRecall(RuntimeFrame frame, int entityId, GameplayEntityId actor, ulong stage,
            ulong execution, CharacterTestPositionRecallOperation operation, int lifetime, bool cancel = false) =>
            new RuntimeCommand(frame, entityId, CharacterTestRuntimeCommandIds.PositionRecall, entityId,
                cancel ? 3 : (int)operation, lifetime, 0, envelope: new RuntimeCommandEnvelope(PositionRecallEnvelopeTypeId, 1,
                    RuntimeCommandProvenanceKind.Unspecified, unchecked((long)execution), (uint)actor.Index | ((long)actor.Generation << 32), unchecked((long)stage)));
        internal static bool IsPositionRecallValid(RuntimeCommand command)
        {
            var e = command.Envelope;
            return command.TargetId > 0 && command.SourceId == command.TargetId && command.Frame.Value >= 0
                && e.TypeId == PositionRecallEnvelopeTypeId && e.SchemaVersion == 1 && e.Provenance == RuntimeCommandProvenanceKind.Unspecified
                && e.Data0 > 0 && (int)e.Data1 > 0 && (int)(e.Data1 >> 32) > 0 && e.Data2 != 0
                && e.Data3 == 0 && e.Data4 == 0 && e.Data5 == 0 && e.Data6 == 0 && e.Data7 == 0 && command.Payload2 == 0
                && (command.Payload0 == 1 ? command.Payload1 >= 1 && command.Payload1 <= 3600
                    : (command.Payload0 == 2 || command.Payload0 == 3) && command.Payload1 == 0);
        }
    }
    public sealed partial class CharacterTestRuntimeCommandModule
    {
        private void ApplyPositionRecall(RuntimeCommand command)
        {
            var actor = _characterRuntimeModule?.FindEntityById(command.TargetId);
            var e = command.Envelope;
            var identity = new GameplayEntityId((int)e.Data1, (int)(e.Data1 >> 32));
            if (actor == null || actor.GameplayEntityId != identity) return;
            var state = actor.PositionRecall;
            ulong stage = unchecked((ulong)e.Data2), execution = unchecked((ulong)e.Data0);
            if (command.Payload0 == 3)
            {
                if (state.Matches(stage, execution)) state.Complete(CharacterTestPositionRecallResult.Cancelled, command.Frame.Value);
            }
            else state.Submit(stage, identity, execution, (CharacterTestPositionRecallOperation)command.Payload0, command.Payload1, command.Frame.Value);
            actor.SetPositionRecall(state);
        }
    }
}
