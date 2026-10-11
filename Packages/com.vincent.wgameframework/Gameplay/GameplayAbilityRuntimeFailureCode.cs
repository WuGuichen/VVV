namespace MxFramework.Gameplay
{
    /// <summary>Stable structured failure code for world-level ability cast attempts.</summary>
    public enum GameplayAbilityRuntimeFailureCode
    {
        None = 0,
        MissingCaster = 1,
        MissingAbility = 2,
        EmptyCandidates = 3,
        AbilityCastFailed = 4,
    }
}
