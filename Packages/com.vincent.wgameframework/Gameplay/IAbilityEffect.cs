namespace MxFramework.Gameplay
{
    /// <summary>Applies one runtime ability effect to one selected target.</summary>
    public interface IAbilityEffect
    {
        void Apply(AbilityContext context, IRuntimeEntity target);
    }
}
