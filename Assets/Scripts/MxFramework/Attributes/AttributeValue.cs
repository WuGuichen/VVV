namespace MxFramework.Attributes
{
    /// <summary>
    /// Stores the base and final values for one attribute.
    /// </summary>
    public readonly struct AttributeValue
    {
        public readonly int BaseValue;
        public readonly int FinalValue;

        public AttributeValue(int baseValue, int finalValue)
        {
            BaseValue = baseValue;
            FinalValue = finalValue;
        }
    }
}
