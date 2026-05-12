namespace MxFramework.Modifiers
{
    public interface IModifierFactory
    {
        bool TryCreate(int modifierId, out IModifier modifier);
    }
}
