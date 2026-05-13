namespace MxFramework.Gameplay
{
    /// <summary>
    /// Calculates read-only attribute values with component-native additive modifiers applied.
    /// </summary>
    public static class GameplayComponentModifierEvaluator
    {
        /// <summary>
        /// Gets an attribute current value plus all additive modifiers targeting that attribute.
        /// </summary>
        /// <param name="attributes">The source attribute set.</param>
        /// <param name="modifiers">The modifier set to evaluate.</param>
        /// <param name="attributeId">The positive attribute id to evaluate.</param>
        /// <returns>The checked sum of the current attribute value and additive modifiers.</returns>
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
