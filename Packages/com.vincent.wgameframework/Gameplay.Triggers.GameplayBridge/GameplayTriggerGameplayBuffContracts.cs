namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Stable Gameplay component Buff action identities shared with numeric Gameplay commands.</summary>
    public static class GameplayTriggerGameplayBuffActionKinds
    {
        public const int ApplyComponentBuffValue = 1001010;
        public const int RemoveComponentBuffValue = 1001011;
        public const GameplayTriggerDeliveryContract DeliveryContract =
            GameplayTriggerDeliveryContract.SameFrameReserved;

        public static readonly GameplayTriggerActionKindId ApplyComponentBuff =
            new GameplayTriggerActionKindId(ApplyComponentBuffValue);

        public static readonly GameplayTriggerActionKindId RemoveComponentBuff =
            new GameplayTriggerActionKindId(RemoveComponentBuffValue);
    }
}
