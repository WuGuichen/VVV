namespace MxFramework.Modifiers
{
    public interface IModifier
    {
        int Id { get; }
        int ParamIndex { get; }

        void SetParamIndex(int index);
        void Apply(ModifierContext context);
        void Update(float deltaTime, ModifierContext context);
        void Remove(ModifierContext context);
    }
}
