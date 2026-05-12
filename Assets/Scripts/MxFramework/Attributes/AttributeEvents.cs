namespace MxFramework.Attributes
{
    public readonly struct AttributeChangedEvent
    {
        public readonly int AttributeId;
        public readonly int BaseValue;
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Delta;
        public readonly object Source;

        public AttributeChangedEvent(int attributeId, int baseValue, int oldValue, int newValue, object source)
        {
            AttributeId = attributeId;
            BaseValue = baseValue;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
            Source = source;
        }
    }

    public readonly struct AttributeModifierEvent
    {
        public readonly int ModifierId;
        public readonly int AttributeId;
        public readonly bool IsAdded;

        public AttributeModifierEvent(int modifierId, int attributeId, bool isAdded)
        {
            ModifierId = modifierId;
            AttributeId = attributeId;
            IsAdded = isAdded;
        }
    }
}
