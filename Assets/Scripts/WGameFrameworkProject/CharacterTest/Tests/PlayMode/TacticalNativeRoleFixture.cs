using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.Items;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    // Native presentation fixtures consume the formal definitions and recipe, but own only memory stores.
    internal static class TacticalNativeRoleFixture
    {
        internal static async Task<CharacterTacticalServiceSet> Build(ICharacterTacticalStateStore playerStore,
            ICharacterTacticalStateStore preparationStore, bool secondOpponent = false)
        {
            const string root = "Assets/StreamingAssets/MxFramework/CharacterTest/";
            var catalog = JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(File.ReadAllText(root + "tactical_content.json"));
            catalog.Acquisition = JsonConvert.DeserializeObject<CharacterTacticalAcquisitionConfig>(File.ReadAllText(root + "tactical_acquisition.json")).Characters;
            CharacterTacticalActionEligibility.Load().Project(catalog, CharacterTestActionBrowserCatalog.Get());
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(published.Success, published.Diagnostic);
            catalog.SetPublishedCards(published.CardCatalog.Cards);
            var discovered = CharacterTestPlayableCharacterCatalog.Discover().Where(value => value.TacticalDefaults != null)
                .Select(value => value.TacticalDefaults).ToArray();
            var inventory = JsonConvert.DeserializeObject<CharacterTacticalRoleInventoryConfig>(File.ReadAllText(root + "character_item_inventory.json"));
            var recipe = JsonConvert.DeserializeObject<CharacterTacticalRoleNewGameConfig>(File.ReadAllText(root + "tactical_new_game.json"));
            if (secondOpponent)
            {
                var original = recipe.CharacterItemGrants.Single(value => value.Deployment?.TeamId == 2);
                var additional = JsonConvert.DeserializeObject<CharacterTacticalRoleGrantConfig>(JsonConvert.SerializeObject(original));
                additional.RequestId = recipe.CharacterItemGrants.Max(value => value.RequestId) + 1;
                additional.DisplayName += " B";
                additional.Deployment.X = 2.2f; additional.Deployment.Y = 1f; additional.Deployment.Z = 1f;
                additional.Deployment.YawDegrees = 225f;
                recipe.CharacterItemGrants = recipe.CharacterItemGrants.Concat(new[] { additional }).ToArray();
            }
            var known = discovered.Select(value => value.CharacterStableId).ToArray();
            inventory.Build(known, out var definitions, out var containers, out var mapping);
            recipe.Validate(inventory, catalog, known);
            var explicitRecipes = recipe.Characters.ToDictionary(value => value.CharacterStableId);
            var defaults = discovered.Select(value => explicitRecipes.TryGetValue(value.CharacterStableId, out var replacement) ? replacement : value).ToArray();
            var player = await CharacterTacticalPlayerService.OpenAsync(catalog, defaults, playerStore,
                addMissingDefaults: false, instantiateTemplateDefaults: false);
            Assert.IsTrue(player.ConfigureCharacterItemInventory(definitions, containers, mapping, out var issue), issue);
            var instances = CharacterTacticalRoleNewGameBootstrap.Execute(player, inventory, recipe);
            var preparation = await TacticalBattlePreparationService.OpenAsync(player, preparationStore);
            Assert.IsTrue(preparation.ConfigureCharacterItemNewGame(recipe, instances, out issue), issue);
            Assert.IsTrue(preparation.EnsurePlayerControlledEntity(out issue), issue);
            Assert.AreEqual(recipe.CharacterItemGrants.Length,
                JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson()).CharacterItems.Count);
            Assert.IsTrue(preparation.Working.Entities.All(value => value.CharacterItemInstanceId > 0));
            return new CharacterTacticalServiceSet(player, preparation, inventoryOwner: new InventoryOwnerId(inventory.PlayerOwnerId));
        }
    }
}
