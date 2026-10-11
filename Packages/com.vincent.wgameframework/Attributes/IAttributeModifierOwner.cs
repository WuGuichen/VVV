namespace MxFramework.Attributes
{
    public interface IAttributeModifierOwner
    {
        void AddModifier(IAttributeModifier modifier);
        bool RemoveModifier(int modifierId);
        void ClearModifiers();
    }
}
