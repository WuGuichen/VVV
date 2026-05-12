namespace MxFramework.Modifiers
{
    public readonly struct ModifierEvent
    {
        public readonly int ModifierId;
        public readonly ModifierEventType Type;
        public readonly object Source;

        public ModifierEvent(int modifierId, ModifierEventType type, object source)
        {
            ModifierId = modifierId;
            Type = type;
            Source = source;
        }
    }

    public enum ModifierEventType
    {
        Added,
        Removed,
        Applied,
        Updated
    }

    public readonly struct CounterChangedEvent
    {
        public readonly int CounterId;
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Delta;
        public readonly object Source;

        public CounterChangedEvent(int counterId, int oldValue, int newValue, object source)
        {
            CounterId = counterId;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = newValue - oldValue;
            Source = source;
        }
    }
}
