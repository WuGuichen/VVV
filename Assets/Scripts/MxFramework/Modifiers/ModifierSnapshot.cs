namespace MxFramework.Modifiers
{
    public readonly struct ModifierSnapshot
    {
        public readonly int Id;
        public readonly int ParamIndex;

        public ModifierSnapshot(IModifier modifier)
        {
            Id = modifier.Id;
            ParamIndex = modifier.ParamIndex;
        }
    }
}
