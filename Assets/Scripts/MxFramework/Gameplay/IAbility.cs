namespace MxFramework.Gameplay
{
    /// <summary>Minimal runtime ability contract; v0 has no cooldown, cost, cast time, or animation binding.</summary>
    public interface IAbility
    {
        int AbilityId { get; }
        AbilityCastResult Cast(AbilityContext context);
    }
}
