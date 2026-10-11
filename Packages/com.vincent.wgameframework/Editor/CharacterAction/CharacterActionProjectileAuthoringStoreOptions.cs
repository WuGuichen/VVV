using System;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionProjectileAuthoringStoreOptions
    {
        public CharacterActionProjectileAuthoringStoreOptions(
            string abilityAuthoringPath,
            string generatedDefinitionsPath,
            string generatedSpawnProfilesPath,
            string generatedPresentationBindingsPath,
            string projectileSourceDirectoryPath = "",
            bool refreshAssetDatabase = true)
        {
            AbilityAuthoringPath = abilityAuthoringPath ?? string.Empty;
            GeneratedDefinitionsPath = generatedDefinitionsPath ?? string.Empty;
            GeneratedSpawnProfilesPath = generatedSpawnProfilesPath ?? string.Empty;
            GeneratedPresentationBindingsPath = generatedPresentationBindingsPath ?? string.Empty;
            ProjectileSourceDirectoryPath = string.IsNullOrWhiteSpace(projectileSourceDirectoryPath)
                ? CharacterActionProjectileAuthoringStore.ResolveDefaultProjectileSourceDirectory(AbilityAuthoringPath)
                : projectileSourceDirectoryPath;
            RefreshAssetDatabase = refreshAssetDatabase;
        }

        public string AbilityAuthoringPath { get; }
        public string GeneratedDefinitionsPath { get; }
        public string GeneratedSpawnProfilesPath { get; }
        public string GeneratedPresentationBindingsPath { get; }
        public string ProjectileSourceDirectoryPath { get; }
        public bool RefreshAssetDatabase { get; }

        public static CharacterActionProjectileAuthoringStoreOptions Default =>
            new CharacterActionProjectileAuthoringStoreOptions(
                CharacterActionProjectileAuthoringStore.DefaultAbilityAuthoringPath,
                CharacterActionProjectileAuthoringStore.DefaultGeneratedProjectileDefinitionsPath,
                CharacterActionProjectileAuthoringStore.DefaultGeneratedProjectileSpawnProfilesPath,
                CharacterActionProjectileAuthoringStore.DefaultGeneratedProjectilePresentationBindingsPath);
    }
}
