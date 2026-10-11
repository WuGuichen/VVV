using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestCharacterAnimationResources
    {
        internal static bool TryPrepareLocomotionDefinition(CharacterRuntimeAnimationBinding binding, out CharacterRuntimeLocomotionDefinition definition) =>
            CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition(binding, NullRuntimeLogger.Instance,
                out definition, CharacterTestFallbackActionBindings);
        private static readonly CharacterRuntimeAnimationFallbackActionBinding[] CharacterTestFallbackActionBindings =
        {
            new CharacterRuntimeAnimationFallbackActionBinding(
                231, // Domain 400, persisted binding.jump.basic.
                CharacterTestCharacterActionConfigSource.JumpAnimationActionKey,
                "combat.attack.light",
                0.08f, "binding.jump.basic")
        };

        public static IEnumerable<ResourceCatalogEntry> CreateCatalogEntries(
            CharacterRuntimeAnimationBinding binding)
        {
            return CharacterRuntimeAnimationResources.CreateCatalogEntries(
                binding,
                CharacterTestEditorAnimationAssetProvider.Id,
                useRuntimeResourceKeyAsAddress: false,
                labels: new[] { "CharacterTest", "CharacterAnimation", "CharacterLocomotion" });
        }

        public static bool TryConfigureLocomotion(
            CharacterRuntimeLocomotionBlendController locomotion,
            IResourceManager resourceManager,
            CharacterRuntimeAnimationBinding binding,
            string actorId,
            IRuntimeLogger logger,
            bool logResult = true)
        {
            return CharacterRuntimeAnimationResources.TryConfigureLocomotion(
                locomotion,
                resourceManager,
                binding,
                actorId,
                logger,
                CharacterTestFallbackActionBindings,
                logResult);
        }

    }
}
