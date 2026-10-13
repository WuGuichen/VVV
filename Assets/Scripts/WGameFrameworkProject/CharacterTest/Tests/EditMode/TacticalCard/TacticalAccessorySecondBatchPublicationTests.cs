using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Gameplay;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessorySecondBatchPublicationTests
    {
        [Test] public void RealSixCandidateMatchesActualMainAndGeneratedConfiguration()
        {
            var p = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(System.IO.File.ReadAllText(
                "Authoring/TacticalAccessories/tactical_card_assembly.double-edge-six.v2.json"));
            var main = p.Configuration.MainCards.Single();
            var document = JObject.Parse(System.IO.File.ReadAllText(
                "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/tactical.bound-action.json"));
            var published = document["cards"].Single(row => (string)row["stableCardId"] == main.Card.CardId && (int)row["revision"] == main.Card.Revision);
            Assert.AreEqual(published["contentHash"].ToString(), main.Card.ContentHash);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(p), JToken.FromObject(TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(p.Configuration.MainCards))));
            var registry = p.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry());
            var loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(document.ToString(),
                new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, null));
            Assert.IsTrue(loaded.Success, string.Join(";", loaded.Diagnostics.Select(d => d.ToString())));
            var catalog = new CharacterTacticalContentCatalog(); catalog.SetPublishedCards(loaded.Catalog.Cards);
            CharacterTacticalAssemblyRules.ValidateConfiguration(p.Configuration, catalog, registry, new Dictionary<int, string>());
            Assert.AreEqual(8, registry.Definitions.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 1, 1, 2 }, p.Configuration.Accessories.Select(a => a.Execution.ConfirmationMomentumCost.Value));
        }

        [TestCase(false)][TestCase(true)] public async System.Threading.Tasks.Task MigrationToSixPreservesFullCheckpoint(bool fromFirstBatch)
        {
            var legacy = TacticalDoubleEdgePublication.LegacyRegistry();
            var old = fromFirstBatch ? TacticalDoubleEdgePublication.Create(null).BuildItemDefinitions(legacy) : legacy;
            var current = TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(null).BuildItemDefinitions(legacy);
            var inventory = new InventoryStore(); var container = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 32, "character-test.player-roles");
            inventory.Register(container);
            Assert.IsTrue(new InventoryService(old, inventory).AddStack(container.ContainerId, 7, new ItemStack(new ItemDefinitionId(1), 1)).Success);
            var ledger = new ItemAcquisitionLedger(); Assert.IsTrue(ledger.TryCommit(new ItemAcquisitionRequestId(765)));
            var items = new CharacterTacticalItemState { Frame = 123, Modules = new[] {
                new InventorySaveStateProvider(inventory, old).CaptureSaveState().Value.ModuleStates[0],
                new ItemAcquisitionSaveStateProvider(ledger).CaptureSaveState().Value.ModuleStates[0] } };
            var player = new CharacterTacticalPlayerState { Schema = CharacterTacticalPlayerState.CharacterTacticalSchema,
                Items = items, NextCardInstanceId = 79, LegacyImportCompleted = true, CharacterItems = new List<CharacterTacticalCharacterItemState>() };
            string original = JsonConvert.SerializeObject(player);
            string migrated = TacticalDoubleEdgeInventoryMigration.Prepare(original, current);
            Assert.AreNotEqual(original, migrated);
            var result = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(migrated);
            Assert.AreEqual(79, result.NextCardInstanceId); Assert.AreEqual(123, result.Items.Frame);
            Assert.AreEqual(items.Modules[1].CustomState.PayloadJson, result.Items.Modules[1].CustomState.PayloadJson);
            var before = JObject.Parse(items.Modules[0].CustomState.PayloadJson); var after = JObject.Parse(result.Items.Modules[0].CustomState.PayloadJson);
            Assert.AreNotEqual(before["definitionHash"], after["definitionHash"]); before.Remove("definitionHash"); after.Remove("definitionHash");
            Assert.IsTrue(JToken.DeepEquals(before, after), "Every stack, instance, allocator, owner and container must survive.");
            Assert.IsTrue(new InventorySaveStateProvider(new InventoryStore(), current).RestoreSaveState(result.Items.ToRuntimeState()).Success);
            Assert.AreEqual(migrated, TacticalDoubleEdgeInventoryMigration.Prepare(migrated, current));
            var unsupported = TacticalDoubleEdgePublication.Create(null); unsupported.Items[0].MaxStackCount = 2;
            Assert.Throws<JsonSerializationException>(() => TacticalDoubleEdgeInventoryMigration.Prepare(original, unsupported.BuildItemDefinitions(old)));
            Assert.AreEqual(original, JsonConvert.SerializeObject(player));
            var store = new MemoryStore(); var progress = new CharacterTacticalProgressStore(store, null);
            var initial = progress.Create(original, "unchanged-preparation"); initial.Bind(() => original, () => "unchanged-preparation");
            await initial.SaveAsync(true); string checkpoint = store.Json;
            var stale = progress.OpenActive(); var candidate = progress.OpenActive();
            await candidate.PlayerStore.SaveAsync(migrated);
            Assert.AreEqual(checkpoint, store.Json, "Preparing migration cannot write the durable checkpoint.");
            candidate.Bind(() => migrated, () => "unchanged-preparation"); store.Reject = true;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await candidate.SaveAsync(true));
            Assert.AreEqual(checkpoint, store.Json); Assert.AreEqual(original, await progress.OpenActive().PlayerStore.LoadAsync());
            store.Reject = false; await candidate.SaveAsync(true);
            Assert.AreEqual(migrated, await progress.OpenActive().PlayerStore.LoadAsync());
            Assert.AreEqual("unchanged-preparation", await progress.OpenActive().PreparationStore.LoadAsync());
            stale.Bind(() => original, () => "unchanged-preparation");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await stale.SaveAsync(true));
            Assert.AreEqual(migrated, await progress.OpenActive().PlayerStore.LoadAsync());
        }

        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            internal string Json; internal bool Reject;
            public System.Threading.Tasks.Task<string> LoadAsync() => System.Threading.Tasks.Task.FromResult(Json);
            public System.Threading.Tasks.Task SaveAsync(string value)
            { if (Reject) throw new InvalidOperationException("fixture:save-failed"); Json = value; return System.Threading.Tasks.Task.CompletedTask; }
            public System.Threading.Tasks.Task ResetAsync(string value) => throw new InvalidOperationException("Reset is forbidden in migration.");
        }
    }
}
