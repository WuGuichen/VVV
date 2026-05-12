namespace MxFramework.Gameplay
{
    /// <summary>Event payload published during an ability cast lifecycle.</summary>
    public readonly struct AbilityEvent
    {
        public readonly AbilityEventType Type;
        public readonly int AbilityId;
        public readonly IRuntimeEntity Caster;
        public readonly IRuntimeEntity Target;
        public readonly string FailureReason;

        public AbilityEvent(
            AbilityEventType type,
            int abilityId,
            IRuntimeEntity caster,
            IRuntimeEntity target = null,
            string failureReason = null)
        {
            Type = type;
            AbilityId = abilityId;
            Caster = caster;
            Target = target;
            FailureReason = failureReason;
        }
    }
}
