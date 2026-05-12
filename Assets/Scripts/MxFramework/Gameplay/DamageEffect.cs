namespace MxFramework.Gameplay
{
    /// <summary>Deals max(1, caster attack - target defense) damage to the target HP attribute.</summary>
    public sealed class DamageEffect : IAbilityEffect
    {
        private readonly int _attackAttributeId;
        private readonly int _defenseAttributeId;
        private readonly int _hpAttributeId;

        public DamageEffect(int attackAttributeId, int defenseAttributeId, int hpAttributeId)
        {
            _attackAttributeId = attackAttributeId;
            _defenseAttributeId = defenseAttributeId;
            _hpAttributeId = hpAttributeId;
        }

        public void Apply(AbilityContext context, IRuntimeEntity target)
        {
            int attack = context.Caster.Attributes.GetAttribute(_attackAttributeId);
            int defense = target.Attributes.GetAttribute(_defenseAttributeId);
            int damage = attack - defense;
            if (damage < 1)
                damage = 1;

            target.Attributes.AddAttribute(_hpAttributeId, -damage, this);
        }
    }
}
