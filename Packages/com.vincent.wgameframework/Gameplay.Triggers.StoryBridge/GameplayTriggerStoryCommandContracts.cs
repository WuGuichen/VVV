using MxFramework.Story.Runtime;

namespace MxFramework.Gameplay.Triggers.StoryBridge
{
    /// <summary>Stable ReliableDeferred Trigger action id for Story.RaiseTrigger.</summary>
    public static class GameplayTriggerStoryCommandActionKinds
    {
        public const int RaiseTriggerValue = StoryRuntimeCommandIds.RaiseTrigger;
        public const GameplayTriggerDeliveryContract DeliveryContract =
            GameplayTriggerDeliveryContract.ReliableDeferred;

        public static readonly GameplayTriggerActionKindId RaiseTrigger =
            new GameplayTriggerActionKindId(RaiseTriggerValue);
    }

    /// <summary>Frozen numeric correlation and transfer contract for Story.RaiseTrigger.</summary>
    public static class GameplayTriggerStoryCommandContracts
    {
        public const int EnvelopeTypeId = 1002105;
        public const int EnvelopeSchemaVersion = 1;
        public const int TargetHostNotStartedDetailCode = 1001;
        public const int TargetModuleNotRegisteredDetailCode = 1002;
    }
}
