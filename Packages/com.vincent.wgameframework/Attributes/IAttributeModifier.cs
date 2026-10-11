namespace MxFramework.Attributes
{
    public interface IAttributeModifier
    {
        int Id { get; }
        int AttributeId { get; }
        AttributeModifierPhase Phase { get; }
        int Priority { get; }
        int Modify(int currentValue, IAttributeOwner owner);
    }

    public enum AttributeModifierPhase
    {
        PreAdd = 0,
        Add = 10,
        Multiply = 20,
        PostAdd = 30,
        Clamp = 40,
    }
}
