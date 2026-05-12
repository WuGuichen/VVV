namespace MxFramework.Gameplay
{
    /// <summary>Logical team relation filter used by target queries.</summary>
    public enum GameplayTargetRelationFilter
    {
        Any = 0,
        Self = 1,
        SameTeam = 2,
        Enemy = 3,
    }
}
