using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestPlayableCharacterSelectionTests
    {
        [Test]
        public void PlayableCatalog_CharacterIdsAreStableAndPackageScoped()
        {
            int first = CharacterTestPlayableCharacterCatalog.ComputeCharacterId("frost_knight");
            int repeated = CharacterTestPlayableCharacterCatalog.ComputeCharacterId("FROST_KNIGHT");
            int second = CharacterTestPlayableCharacterCatalog.ComputeCharacterId("ember_mage");

            Assert.AreEqual(first, repeated);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000001"));
        }

        [Test]
        public void PlayableCatalog_UsesPersistedNumericIdentityForPackage000009()
        {
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(
                "Assets/MxFrameworkGenerated/CharacterPackages/000009");
            Assert.NotNull(package);
            Assert.Greater(package.Configs.RuntimeCharacterId, 0);
            Assert.AreEqual(package.Configs.RuntimeCharacterId,
                CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000009"));
            Assert.AreNotEqual(CharacterTestPlayableCharacterCatalog.ComputeLegacyCharacterId("000009"),
                package.Configs.RuntimeCharacterId);
            Assert.That(CharacterTestPlayableCharacterCatalog.Refresh(), Has.Some.Matches<CharacterTestCharacterDefinition>(
                definition => definition.CharacterId == package.Configs.RuntimeCharacterId
                    && definition.PrefabKey.PackageId == "000009"));
        }

        [Test]
        public void PlayableCatalog_DiscoversDragon14FromExplicitCreatureBodyGeometry()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();

            Assert.That(definitions, Has.Some.Matches<CharacterTestCharacterDefinition>(definition =>
                definition.CharacterId == CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003")
                && definition.PrefabKey.PackageId == "000003"
                && definition.PhysicsProfile.CapsuleHeightMeters == 4.2f
                && definition.PhysicsProfile.CapsuleRadiusMeters == 1.3f));
        }

        [Test]
        public void PlayableCatalog_RefreshRebuildsCachedSnapshot()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000004";
            CharacterImportedPackage cachedPackage = CharacterTestGeneratedPackageSource.LoadPackage(packageRoot);
            IReadOnlyList<CharacterTestCharacterDefinition> cached =
                CharacterTestPlayableCharacterCatalog.Discover();

            IReadOnlyList<CharacterTestCharacterDefinition> refreshed =
                CharacterTestPlayableCharacterCatalog.Refresh();
            CharacterImportedPackage refreshedPackage = CharacterTestGeneratedPackageSource.LoadPackage(packageRoot);

            Assert.AreNotSame(cached, refreshed);
            Assert.AreNotSame(cachedPackage, refreshedPackage);
            Assert.That(refreshed, Has.Some.Matches<CharacterTestCharacterDefinition>(definition =>
                definition.CharacterId == CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000002")
                && definition.DisplayName == "Witch"
                && definition.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                    "/CharacterPackages/000002")));
        }

        [Test]
        public void CharacterRegistry_SelectNextWrapsAcrossPlayableDefinitions()
        {
            CharacterTestCharacterDefinition first = CreateDefinition(1001, "First", "first");
            CharacterTestCharacterDefinition second = CreateDefinition(2002, "Second", "second");
            var registry = new CharacterTestCharacterDefinitionRegistry(
                new List<CharacterTestCharacterDefinition> { first, second });

            Assert.IsTrue(registry.TrySelectNext(first.CharacterId, 1, out int next));
            Assert.AreEqual(second.CharacterId, next);
            Assert.IsTrue(registry.TrySelectNext(first.CharacterId, -1, out int previous));
            Assert.AreEqual(second.CharacterId, previous);
            Assert.IsTrue(registry.TrySelectNext(second.CharacterId, 1, out int wrapped));
            Assert.AreEqual(first.CharacterId, wrapped);
        }

        [Test]
        public void ArenaCharacterCommand_UsesPlayableRegistrySelection()
        {
            GameObject host = new GameObject("PlayableCharacterSelectionTests");
            try
            {
                var controller = host.AddComponent<CharacterTestArenaSessionController>();
                CharacterTestCharacterDefinition first = CreateDefinition(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    "First",
                    "first");
                CharacterTestCharacterDefinition second = CreateDefinition(2002, "Second", "second");
                controller.SetCharacterDefinitionsForTests(
                    new CharacterTestCharacterDefinitionRegistry(
                        new List<CharacterTestCharacterDefinition> { first, second }));
                var sink = new CharacterTestFairyGuiArenaSetupPanelCommandSink(controller);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiArenaSetupPanelIds.ViewId,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.ForSlot(
                        CharacterTestFairyGuiArenaSetupPanelCommandIds.CharacterNext,
                        CharacterTestArenaMatchConfig.Team1Id,
                        0),
                    new CharacterTestFairyGuiArenaSetupPanelPayload
                    {
                        TeamId = CharacterTestArenaMatchConfig.Team1Id,
                        SlotIndex = 0
                    }));

                Assert.AreEqual(
                    second.CharacterId,
                    controller.Config.TryGetSlot(CharacterTestArenaMatchConfig.Team1Id, 0).CharacterId);
                Assert.AreEqual("Character selected.", sink.LastStatusMessage);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static CharacterTestCharacterDefinition CreateDefinition(
            int characterId,
            string displayName,
            string packageId)
        {
            return new CharacterTestCharacterDefinition(
                characterId,
                displayName,
                new ResourceKey(
                    "char." + packageId + ".prefab.character_preview",
                    ResourceTypeIds.GameObject,
                    "default",
                    packageId),
                defaultTeamId: 1,
                default,
                CharacterTestCharacterPhysicsComponent.Default,
                "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId);
        }
    }
}
