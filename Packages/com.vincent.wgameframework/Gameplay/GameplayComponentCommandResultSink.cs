using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public enum GameplayComponentCommandResultStatus
    {
        None = 0,
        Succeeded = 1,
        Rejected = 2
    }

    public enum GameplayComponentCommandResultReasonCode
    {
        None = 0,
        ComponentEntityDestroyed = 1,
        ComponentAttributeSet = 2,
        ComponentAttributeAdded = 3,
        ComponentAbilityCastSucceeded = 4,
        MissingComponentWorld = 1001,
        InvalidComponentEntity = 1002,
        MissingComponentEntity = 1003,
        InvalidAttributeId = 1101,
        MissingAttributeSet = 1102,
        MissingAttribute = 1103,
        AttributeUpdateFailed = 1104,
        InvalidComponentAbilityCommandPayload = 1201,
        MissingComponentAbility = 1202,
        MissingAbilityAttributeSet = 1203,
        MissingAbilityTarget = 1204,
        ComponentAbilityEffectFailed = 1205,
        ComponentAbilityOnCooldown = 1206,
        ComponentAbilityInsufficientCost = 1207,
        InvalidComponentAbilityRule = 1208,
        InvalidComponentAbilityResult = 1209
    }

    /// <summary>Flat numeric terminal result for one typed component command.</summary>
    public readonly struct GameplayComponentCommandResult
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayComponentCommandResult(
            RuntimeFrame frame,
            int sourceId,
            int commandId,
            int targetId,
            int commandPayload0,
            int commandPayload1,
            int commandPayload2,
            long commandSequence,
            bool traceIdIsEmpty,
            RuntimeCommandEnvelope envelope,
            GameplayComponentCommandResultStatus status,
            GameplayComponentCommandResultReasonCode reasonCode,
            long resultPayload0 = 0L,
            long resultPayload1 = 0L,
            long resultPayload2 = 0L)
        {
            Frame = frame;
            SourceId = sourceId;
            CommandId = commandId;
            TargetId = targetId;
            CommandPayload0 = commandPayload0;
            CommandPayload1 = commandPayload1;
            CommandPayload2 = commandPayload2;
            CommandSequence = commandSequence;
            TraceIdIsEmpty = traceIdIsEmpty;
            Envelope = envelope;
            Status = status;
            ReasonCode = reasonCode;
            ResultPayload0 = resultPayload0;
            ResultPayload1 = resultPayload1;
            ResultPayload2 = resultPayload2;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public int CommandId { get; }
        public int TargetId { get; }
        public int CommandPayload0 { get; }
        public int CommandPayload1 { get; }
        public int CommandPayload2 { get; }
        public long CommandSequence { get; }
        public bool TraceIdIsEmpty { get; }
        public RuntimeCommandEnvelope Envelope { get; }
        public GameplayComponentCommandResultStatus Status { get; }
        public GameplayComponentCommandResultReasonCode ReasonCode { get; }
        public long ResultPayload0 { get; }
        public long ResultPayload1 { get; }
        public long ResultPayload2 { get; }

        /// <remarks>NoAlloc.</remarks>
        internal static GameplayComponentCommandResult FromCommand(
            RuntimeFrame frame,
            in RuntimeCommand command,
            GameplayComponentCommandResultStatus status,
            GameplayComponentCommandResultReasonCode reasonCode,
            long resultPayload0 = 0L,
            long resultPayload1 = 0L,
            long resultPayload2 = 0L)
        {
            return new GameplayComponentCommandResult(
                frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.Sequence,
                command.TraceId.IsNone,
                command.Envelope,
                status,
                reasonCode,
                resultPayload0,
                resultPayload1,
                resultPayload2);
        }
    }

    /// <summary>Optional target-owned sink for flat numeric component command results.</summary>
    public interface IGameplayComponentCommandResultSink
    {
        /// <remarks>NoAlloc. Implementations must not throw or retry.</remarks>
        void Publish(in GameplayComponentCommandResult result);
    }
}
