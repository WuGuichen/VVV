using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.Resources.Unity;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestCharacterSpawnResources
    {
        public const string PackageId = "character-test";
        public const string CatalogId = "character-test.character-spawn.temp";
        public const string DefaultPlayablePackageId = "000001";
        public const string SampleAnimationPackageId = "mxframework.samples";

        public const string DefaultPlayablePreviewPrefabId =
            "char.000001.prefab.character_preview";

        public const string DefaultMainHandWeaponResourceId = "weapon.common_weapon_1.model";
        public const string DefaultOffHandWeaponResourceId = "weapon.common_kite_shield.model";

        public const string DefaultPlayablePreviewResourcesAddress =
            "MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview";

#if UNITY_EDITOR
        private const string EditorAssetDatabaseProviderId = "editorAssetDatabase";
        private const string DefaultMainHandWeaponAssetPath =
            "Assets/MxFrameworkGenerated/Common/Weapons/common_weapon_1/prefabs/common_weapon_1_weapon.prefab";
        private const string DefaultOffHandWeaponAssetPath =
            "Assets/MxFrameworkGenerated/Common/Weapons/common_kite_shield/prefabs/common_kite_shield_weapon.prefab";
#endif

        public static ResourceKey DefaultPlayablePreviewPrefabKey =>
            new ResourceKey(
                DefaultPlayablePreviewPrefabId,
                ResourceTypeIds.GameObject,
                variant: "default",
                packageId: DefaultPlayablePackageId);

        public static ResourceCatalog CreateCatalog(
            bool includeAnimationEntries = true,
            bool includePublicMainHandWeaponFallbackEntry = true,
            bool includePublicOffHandWeaponFallbackEntry = true)
        {
            var entries = new List<ResourceCatalogEntry>();
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = registry.Definitions[i];
                string packageId = definition.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId
                    ? DefaultPlayablePackageId
                    : definition.PrefabKey.PackageId;
                string address = definition.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId
                    ? DefaultPlayablePreviewResourcesAddress
                    : "MxFrameworkGenerated/CharacterPackages/"
                        + packageId
                        + "/prefabs/"
                        + packageId
                        + "_character_preview";
                entries.Add(new ResourceCatalogEntry(
                    definition.PrefabKey.Id,
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    address,
                    definition.PrefabKey.Variant,
                    definition.PrefabKey.PackageId,
                    labels: new[] { "CharacterTest", "CharacterSpawn", "PlayableCharacter" }));

                if (includeAnimationEntries && definition.AnimationBinding.IsValid)
                {
                    entries.AddRange(CharacterTestCharacterAnimationResources.CreateCatalogEntries(
                        definition.AnimationBinding));
                }
            }
#if UNITY_EDITOR
            if (includePublicMainHandWeaponFallbackEntry)
            {
                entries.Add(new ResourceCatalogEntry(
                    DefaultMainHandWeaponResourceId,
                    ResourceTypeIds.GameObject,
                    EditorAssetDatabaseProviderId,
                    DefaultMainHandWeaponAssetPath,
                    packageId: "common",
                    labels: new[] { "CharacterEquipment", "CommonWeapon" }));
            }

            if (includePublicOffHandWeaponFallbackEntry)
            {
                entries.Add(new ResourceCatalogEntry(
                    DefaultOffHandWeaponResourceId,
                    ResourceTypeIds.GameObject,
                    EditorAssetDatabaseProviderId,
                    DefaultOffHandWeaponAssetPath,
                    packageId: "common",
                    labels: new[] { "CharacterEquipment", "CommonWeapon" }));
            }
#endif

            return new ResourceCatalog(
                CatalogId,
                PackageId,
                entries);
        }
    }
}
