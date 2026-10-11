using MxFramework.Config.Levels;
using MxFramework.Gameplay;

namespace MxFramework.Editor.Levels
{
    /// <summary>
    /// Projects formal level-definition usage into the shared gameplay target classification.
    /// </summary>
    /// <remarks>
    /// Allocation: NoAlloc. This Editor adapter keeps Config.Levels independent of Gameplay.
    /// </remarks>
    public static class MxLevelAuthoringTargetClassification
    {
        public static bool TryResolve(
            LevelAuthoringDefinitionUsage usage,
            out GameplayTargetClassificationComponent classification)
        {
            switch (usage)
            {
                case LevelAuthoringDefinitionUsage.Movable:
                    classification =
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.WorldObject,
                            GameplayTargetCapabilities.Movable);
                    return true;

                case LevelAuthoringDefinitionUsage.StaticCollider:
                    classification =
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.WorldObject,
                            GameplayTargetCapabilities.Static);
                    return true;

                case LevelAuthoringDefinitionUsage.Point:
                    classification =
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.SpatialPoint);
                    return true;

                case LevelAuthoringDefinitionUsage.Region:
                    classification =
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.SpatialRegion);
                    return true;

                default:
                    classification = default;
                    return false;
            }
        }
    }
}
