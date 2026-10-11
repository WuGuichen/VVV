using System;

namespace MxFramework.Gameplay
{
    /// <summary>Logical team relation filter used by target queries.</summary>
    public enum GameplayTargetRelationFilter
    {
        Any = 0,
        Self = 1,
        SameTeam = 2,
        Enemy = 3,
        Neutral = 4,
    }

    internal static class GameplayTargetRelationFilterValidation
    {
        /// <remarks>NoAlloc on the valid path.</remarks>
        internal static void Validate(
            GameplayTargetRelationFilter relationFilter,
            string parameterName)
        {
            switch (relationFilter)
            {
                case GameplayTargetRelationFilter.Any:
                case GameplayTargetRelationFilter.Self:
                case GameplayTargetRelationFilter.SameTeam:
                case GameplayTargetRelationFilter.Enemy:
                case GameplayTargetRelationFilter.Neutral:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        relationFilter,
                        "Target relation filter is not defined.");
            }
        }
    }
}
