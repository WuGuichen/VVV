using System;

namespace MxFramework.Gameplay.Triggers.StoryBridge
{
    /// <summary>ReliableDeferred Story.RaiseTrigger action with structural preflight.</summary>
    public sealed class GameplayTriggerStoryReliableCommandActionSink :
        IGameplayTriggerActionSink,
        IGameplayTriggerReliableActionPreparationValidator
    {
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public GameplayTriggerStoryReliableCommandActionSink(
            GameplayTriggerActionKindId actionKindId)
        {
            if (actionKindId != GameplayTriggerStoryCommandActionKinds.RaiseTrigger)
                throw new ArgumentOutOfRangeException(nameof(actionKindId));
            ActionKindId = actionKindId;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerDeliveryContract DeliveryContract =>
            GameplayTriggerStoryCommandActionKinds.DeliveryContract;

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
            return instruction.ActionKindId == ActionKindId &&
                   target.DomainId ==
                       GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId &&
                   target.Value > 0L && target.Value <= int.MaxValue &&
                   target.Generation == 0 &&
                   instruction.Payload0 > 0L &&
                   instruction.Payload0 <= int.MaxValue &&
                   instruction.Payload1 >= int.MinValue &&
                   instruction.Payload1 <= int.MaxValue &&
                   instruction.Payload2 >= int.MinValue &&
                   instruction.Payload2 <= int.MaxValue;
        }
    }
}
