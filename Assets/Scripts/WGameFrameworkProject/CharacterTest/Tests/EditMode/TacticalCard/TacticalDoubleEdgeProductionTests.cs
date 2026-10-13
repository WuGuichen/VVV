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
    public sealed class TacticalDoubleEdgeProductionTests
    {
        [Test] public void CandidatePublicationMatchesTheActualPublishedMainAndReviewedSource()
        {
            var publication = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(System.IO.File.ReadAllText(
                "Authoring/TacticalAccessories/tactical_card_assembly.double-edge-three.v1.json"));
            var main = publication.Configuration.MainCards.Single();
            var document = JObject.Parse(System.IO.File.ReadAllText(
                "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/tactical.bound-action.json"));
            var published = document["cards"].Single(row => (string)row["stableCardId"] == main.Card.CardId && (int)row["revision"] == main.Card.Revision);
            Assert.AreEqual((int)published["revision"], main.Card.Revision);
            Assert.AreEqual(published["contentHash"].ToString(), main.Card.ContentHash);
            var expected = TacticalDoubleEdgePublication.Create(publication.Configuration.MainCards);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(expected), JToken.FromObject(publication)));
        }
        [Test] public void ReviewedPublicationHasThreeExactExecutableSilverDefinitions()
        {
            var publication = TacticalDoubleEdgePublication.Create(null);
            var registry = publication.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry());
            CharacterTacticalAssemblyRules.ValidateConfiguration(publication.Configuration, new CharacterTacticalContentCatalog(),
                registry, new Dictionary<int, string> { { 1, "char.000001" }, { 2, "char.000002" } });
            Assert.AreEqual(5, registry.Definitions.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 2 }, publication.Configuration.Accessories.Select(a => a.Execution.ConfirmationMomentumCost.Value));
            foreach (var card in publication.Configuration.Accessories) {
                Assert.IsTrue(card.Execution.IsValid); Assert.AreEqual(CharacterTacticalCardQuality.Silver, card.Quality);
                Assert.IsNotEmpty(card.Reference.ContentHash);
            }
            var copy = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(JsonConvert.SerializeObject(publication));
            Assert.AreEqual(publication.Configuration.ContentHash, CharacterTacticalAssemblyRules.ConfigurationHash(copy.Configuration));
            copy.Configuration.Accessories[2].Execution.DoubleEdge.MaximumAcceleration++;
            Assert.Throws<JsonSerializationException>(() => CharacterTacticalAssemblyRules.ValidateConfiguration(copy.Configuration,
                new CharacterTacticalContentCatalog(), registry, new Dictionary<int, string>()));
        }

        [TestCase(false)][TestCase(true)]
        public void PaidRealBattleCopyExhaustsOnceOnNormalOrAbnormalExitWithoutChangingItsTemplate(bool abnormal)
        {
            var definition = TacticalFixedAssemblyManifestTests.Main(); var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            var accessory = TacticalDoubleEdgePublication.Create(null).Configuration.Accessories[1];
            snapshot.Accessories = new[] { accessory }; snapshot.Assembly.Accessories.Clear();
            snapshot.Assembly.Accessories.Add(new CharacterTacticalAccessorySlot { SlotIndex = 0, AccessoryItemInstanceId = 81, Reference = accessory.Reference });
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3,
                new Dictionary<string, int>(), out var assembly, out var issue), issue);
            definition.AttachFixedAssembly(assembly);
            var deck = new TacticalDeckManager(2); deck.InitializeDeck(new[] { definition, definition }, 7, 2);
            var card = deck.Hand[0]; var other = deck.Hand[1]; var ledger = new MomentumLedger(20);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(card, ledger));
            var actor = new GameplayEntityId(1, 1); var effects = new TacticalInteractionEffectAuthority(); effects.Bind(7, 2, actor);
            var program = TacticalFixedPaymentCoreTests.Program(Array.Empty<CharacterTestBehaviorExecutionEvent>());
            Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 0, out var proof, out issue), issue);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 0, out var fees));
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, actor, 1, 1, 10,
                0, 1, deck.FixedPendingReservation(ledger), default, out var manifest));
            var stage = new TacticalDoubleEdgeBattleState(); stage.Bind(7, 2);
            Assert.IsTrue(stage.TryPrepare(manifest.DoubleEdgeKey, 2, default, assembly.DoubleEdgeParameters, false, true, out var state));
            Assert.IsTrue(deck.TryPrepareFixedMove(card, ledger, out var move));
            Assert.IsTrue(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, ledger, effects,
                program, proof, out var transaction, out issue), issue);
            Assert.IsTrue(transaction.TryPublish(out var paid)); Assert.IsTrue(state.TryPublish()); paid.DoubleEdgeState = stage; move.Publish(paid);
            Assert.AreEqual(15, ledger.Current); Assert.AreEqual(0, effects.AccessoryActivationCount);
            Assert.IsTrue(stage.CompleteHost(manifest.DoubleEdgeKey, !abnormal, 11, 0, 0, 0));
            Assert.IsTrue(deck.ReleaseFixedReservation(ledger, paid));
            uint version = card.Version;
            Assert.IsTrue(abnormal ? deck.CompleteFixedAbnormal(paid) : deck.CompleteTurn(card.InstanceId, version));
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone); Assert.AreEqual(CardZone.Hand, other.Zone);
            Assert.AreEqual(1, deck.ExhaustPile.Count); Assert.AreEqual(1, deck.Hand.Count);
            Assert.AreEqual(CardDestination.Discard, definition.DestinationAfterUse);
            Assert.IsFalse(deck.CompleteTurn(card.InstanceId, version));
            Assert.AreEqual(15, ledger.Current); Assert.AreEqual(0, ledger.Reserved);
        }

        [Test] public async System.Threading.Tasks.Task FiniteRegistryMigrationPreservesInventoryAllocatorLedgerAndPlayerFields()
        {
            var old = TacticalDoubleEdgePublication.LegacyRegistry();
            var current = TacticalDoubleEdgePublication.Create(null).BuildItemDefinitions(old);
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
