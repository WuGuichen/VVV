using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterIdentityComponent
    {
        public CharacterTestCharacterIdentityComponent(
            int characterId,
            string displayName,
            ResourceKey prefabKey,
            CharacterRuntimeAnimationBinding animationBinding,
            string generatedPackageRoot = "",
            string characterStableId = "")
        {
            CharacterId = characterId;
            DisplayName = displayName ?? string.Empty;
            PrefabKey = prefabKey;
            AnimationBinding = animationBinding;
            GeneratedPackageRoot = generatedPackageRoot ?? string.Empty;
            CharacterStableId = !string.IsNullOrWhiteSpace(characterStableId)
                ? characterStableId
                : ResolveCharacterStableId(prefabKey.PackageId);
        }

        public int CharacterId { get; }
        public string DisplayName { get; }
        public ResourceKey PrefabKey { get; }
        public CharacterRuntimeAnimationBinding AnimationBinding { get; }
        public string GeneratedPackageRoot { get; }
        public string CharacterStableId { get; }

        private static string ResolveCharacterStableId(string packageId)
        {
            return string.IsNullOrWhiteSpace(packageId)
                ? string.Empty
                : "char." + packageId.Trim();
        }
    }
}
