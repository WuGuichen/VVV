namespace MxFramework.Gameplay
{
    public static class GameplayComponentSpawnEvents
    {
        public const string SpawnedReason = "SpawnComponentEntity";
        public const string MissingComponentWorldReason = "MissingComponentWorld";
        public const string MissingSpawnRegistryReason = "MissingSpawnRegistry";
        public const string MissingSpawnDefinitionReason = "MissingSpawnDefinition";
        public const string InvalidSpawnDefinitionReason = "InvalidSpawnDefinition";
        public const string SpawnInitializerFailedReason = "SpawnInitializerFailed";
    }
}
