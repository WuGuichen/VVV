using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalFixedCardPaymentCommit
    {
        internal const int CommandEnvelopeType = 901107;
        private bool _paid;
        private CharacterTestFixedActionCostScope _actionScope;
        internal bool IsPaid => _paid;
        internal MomentumLedger Ledger => _ledger;
        internal long ProgramIdentity => _proof.ProgramIdentity;
        internal void MarkPaid() { _paid = true; }
        internal CharacterTestFixedActionCostScope ActionScope => _actionScope;
        internal bool CanPayAction(int actionId, int amount) => _paid && HasReservation
            && _proof.TryGetActionCost(actionId, out var proved) && proved == amount && amount <= _ledger.Reserved;
        internal RuntimeCommand BindCommand(in RuntimeCommand command) => new RuntimeCommand(command.Frame,
            command.SourceId, command.CommandId, command.TargetId, command.Payload0, command.Payload1, 3,
            traceId: command.TraceId, sequence: command.Sequence, envelope: new RuntimeCommandEnvelope(CommandEnvelopeType, 1,
                RuntimeCommandProvenanceKind.Derived, unchecked((long)Record.ExecutionId), Record.Actor.Index, Record.Actor.Generation,
                unchecked((long)Record.ReservationToken), unchecked((long)Record.CommitId), ProgramIdentity,
                unchecked((long)Record.Stage), unchecked((long)Record.Turn)));
        internal bool MatchesCommand(RuntimeFrame frame, int entity, int resource, int source, RuntimeTraceIdentity trace,
            in RuntimeCommandEnvelope envelope) => IsPaid && HasReservation && frame.Value >= Record.Frame
            && source == CharacterTestRuntimeCommandSources.RuntimeAiPlanner
            && trace == CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionAction, entity, frame)
            && _proof.TryGetActionCost(resource, out _) && envelope.TypeId == CommandEnvelopeType && envelope.SchemaVersion == 1
            && envelope.Provenance == RuntimeCommandProvenanceKind.Derived
            && envelope.Data0 == unchecked((long)Record.ExecutionId) && envelope.Data1 == Record.Actor.Index
            && envelope.Data2 == Record.Actor.Generation && envelope.Data3 == unchecked((long)Record.ReservationToken)
            && envelope.Data4 == unchecked((long)Record.CommitId) && envelope.Data5 == ProgramIdentity
            && envelope.Data6 == unchecked((long)Record.Stage) && envelope.Data7 == unchecked((long)Record.Turn);
    }
}
