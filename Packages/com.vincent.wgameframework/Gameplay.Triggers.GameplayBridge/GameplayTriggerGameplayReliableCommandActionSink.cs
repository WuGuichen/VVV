using System;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>ReliableDeferred Trigger action declaration with target-owned structural preflight.</summary>
    public sealed class GameplayTriggerGameplayReliableCommandActionSink :
        IGameplayTriggerActionSink,
        IGameplayTriggerReliableActionPreparationValidator
    {
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public GameplayTriggerGameplayReliableCommandActionSink(
            GameplayTriggerActionKindId actionKindId)
        {
            if (!GameplayTriggerGameplayCommandContracts.IsSupportedActionKind(actionKindId))
                throw new ArgumentOutOfRangeException(nameof(actionKindId));
            ActionKindId = actionKindId;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerDeliveryContract DeliveryContract =>
            GameplayTriggerGameplayCommandActionKinds.DeliveryContract;

        /// <remarks>NoAlloc. ReliableDeferred ownership bypasses the legacy reservation callback.</remarks>
        public bool TryPrepare(
            in GameplayTriggerActionPreparation preparation,
            out int token)
        {
            token = 0;
            return false;
        }

        /// <remarks>NoAlloc. ReliableDeferred ownership bypasses the legacy reservation callback.</remarks>
        public void Abort(int token)
        {
        }

        /// <remarks>NoAlloc. ReliableDeferred ownership bypasses the legacy reservation callback.</remarks>
        public void Commit(int token, in GameplayTriggerActionIntent intent)
        {
        }

        /// <remarks>NoAlloc. Invalid structure fails before activation, policy, or outbox mutation.</remarks>
        public bool TryValidateReliableAction(
            in GameplayTriggerActionPreparation preparation)
        {
            GameplayTriggerActionInstruction instruction = preparation.Instruction;
            GameplayTriggerTargetRef target = preparation.Target;
            if (instruction.ActionKindId != ActionKindId ||
                target.DomainId != GameplayTriggerTargetRef.GameplayEntityDomainId ||
                target.Value <= 0L || target.Value > int.MaxValue ||
                target.Generation <= 0)
            {
                return false;
            }

            if (ActionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
            {
                return instruction.Payload0 == 0L &&
                       instruction.Payload1 == 0L &&
                       instruction.Payload2 == 0L;
            }

            if (ActionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                ActionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
            {
                return instruction.Payload0 > 0L &&
                       instruction.Payload0 <= int.MaxValue &&
                       instruction.Payload1 >= int.MinValue &&
                       instruction.Payload1 <= int.MaxValue &&
                       instruction.Payload2 == 0L;
            }

            return instruction.Payload0 > 0L &&
                   instruction.Payload0 <= int.MaxValue &&
                   instruction.Payload1 == 0L &&
                   instruction.Payload2 == 0L;
        }
    }
}
