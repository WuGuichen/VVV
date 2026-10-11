using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Stable ReliableDeferred Trigger action ids for component Gameplay commands.</summary>
    public static class GameplayTriggerGameplayCommandActionKinds
    {
        public const int DestroyComponentEntityValue = 1001004;
        public const int SetComponentAttributeValue = 1001006;
        public const int AddComponentAttributeValue = 1001007;
        public const int CastComponentAbilityValue = 1001008;
        public const GameplayTriggerDeliveryContract DeliveryContract =
            GameplayTriggerDeliveryContract.ReliableDeferred;

        public static readonly GameplayTriggerActionKindId DestroyComponentEntity =
            new GameplayTriggerActionKindId(DestroyComponentEntityValue);
        public static readonly GameplayTriggerActionKindId SetComponentAttribute =
            new GameplayTriggerActionKindId(SetComponentAttributeValue);
        public static readonly GameplayTriggerActionKindId AddComponentAttribute =
            new GameplayTriggerActionKindId(AddComponentAttributeValue);
        public static readonly GameplayTriggerActionKindId CastComponentAbility =
            new GameplayTriggerActionKindId(CastComponentAbilityValue);
    }

    /// <summary>Frozen numeric correlation envelope shared by the reliable transfer and result relay.</summary>
    public static class GameplayTriggerGameplayCommandContracts
    {
        public const int EnvelopeTypeId = 1002104;
        public const int EnvelopeSchemaVersion = 1;
        public const int TargetHostNotStartedDetailCode = 1001;
        public const int TargetModuleNotRegisteredDetailCode = 1002;
        public const int TargetConsumerNotRegisteredDetailCode = 1003;

        /// <remarks>NoAlloc.</remarks>
        public static bool IsSupportedActionKind(GameplayTriggerActionKindId actionKindId)
        {
            return actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity ||
                   actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                   actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute ||
                   actionKindId == GameplayTriggerGameplayCommandActionKinds.CastComponentAbility;
        }

        /// <remarks>NoAlloc.</remarks>
        public static int ResolveCommandId(GameplayTriggerActionKindId actionKindId)
        {
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
                return GameplayRuntimeCommandIds.DestroyComponentEntity;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute)
                return GameplayRuntimeCommandIds.SetComponentAttribute;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
                return GameplayRuntimeCommandIds.AddComponentAttribute;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.CastComponentAbility)
                return GameplayRuntimeCommandIds.CastComponentAbility;
            return 0;
        }
    }
}
