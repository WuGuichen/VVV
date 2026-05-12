namespace MxFramework.Modifiers
{
    public interface IModifierCondition
    {
        bool Evaluate(ModifierContext context);
    }
}
