namespace MxFramework.Gameplay
{
    public static class GameplayComponentAbilityEvents
    {
        public const string CastComponentAbilityReason = "CastComponentAbility";
        public const string MissingComponentWorldReason = "MissingComponentWorld";
        public const string InvalidCommandPayloadReason = "InvalidComponentAbilityCommandPayload";
        public const string InvalidCasterReason = "InvalidComponentAbilityCaster";
        public const string MissingCasterReason = "MissingComponentAbilityCaster";
        public const string MissingAbilityReason = "MissingComponentAbility";
        public const string MissingRequestReason = "MissingComponentAbilityRequest";
        public const string InvalidRequestReason = "InvalidComponentAbilityRequest";
        public const string MissingAttributeSetReason = "MissingAttributeSet";
        public const string MissingTargetReason = "MissingComponentAbilityTarget";
        public const string NoValidTargetReason = "NoValidComponentAbilityTarget";
        public const string AbilityOnCooldownReason = "ComponentAbilityOnCooldown";
        public const string InsufficientCostReason = "ComponentAbilityInsufficientCost";
        public const string InvalidRuleReason = "InvalidComponentAbilityRule";
        public const string AbilityCostCommittedReason = "ComponentAbilityCostCommitted";
        public const string EffectFailedReason = "ComponentAbilityEffectFailed";
    }
}
