namespace MxFramework.Gameplay
{
    public static class GameplayComponentAttributeQuery
    {
        public static bool TryGetCurrentValue(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            int attributeId,
            out int value,
            bool includeModifiers = true)
        {
            value = 0;
            if (world == null
                || !entityId.IsValid
                || attributeId <= 0
                || !world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributeStore)
                || !attributeStore.TryGet(entityId, out GameplayAttributeSetComponent attributes))
            {
                return false;
            }

            if (!includeModifiers)
            {
                value = attributes.GetCurrentValueOrDefault(attributeId);
                return true;
            }

            if (world.TryGetStore(out GameplayComponentStore<GameplayComponentModifierSetComponent> modifierStore)
                && modifierStore.TryGet(entityId, out GameplayComponentModifierSetComponent modifiers))
            {
                value = GameplayComponentModifierEvaluator.GetModifiedCurrentValue(attributes, modifiers, attributeId);
                return true;
            }

            value = attributes.GetCurrentValueOrDefault(attributeId);
            return true;
        }

        public static int GetCurrentValueOrDefault(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            int attributeId,
            bool includeModifiers = true)
        {
            return TryGetCurrentValue(world, entityId, attributeId, out int value, includeModifiers)
                ? value
                : 0;
        }
    }
}
