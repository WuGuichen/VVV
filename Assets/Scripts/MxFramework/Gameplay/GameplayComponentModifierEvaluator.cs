namespace MxFramework.Gameplay
{
    public static class GameplayComponentModifierEvaluator
    {
        public static int GetModifiedCurrentValue(
            in GameplayAttributeSetComponent attributes,
            in GameplayComponentModifierSetComponent modifiers,
            int attributeId)
        {
            int current = attributes.GetCurrentValueOrDefault(attributeId);
            return checked(current + modifiers.GetAdditiveValue(attributeId));
        }
    }
}
