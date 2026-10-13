using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryThirdBatchConfigurationTests
    {
        [Test, Category("NineAccessoryDeliveryNative"), Parallelizable(ParallelScope.None)]
        public async System.Threading.Tasks.Task ActualCompositionRootMigratesFixedAssemblyThenExplicitlyGrantsNineAndReopens()
        {
            string root = Path.Combine(Path.GetTempPath(), "MxNineAccessoryDelivery", Guid.NewGuid().ToString("N"));
            string content = Path.Combine(root, "content"), saves = Path.Combine(root, "save"); Directory.CreateDirectory(content);
            string formal = Path.Combine(UnityEngine.Application.streamingAssetsPath, "MxFramework/CharacterTest");
            foreach (string file in Directory.GetFiles(formal, "*.json"))
                if (Path.GetFileName(file) != CharacterTacticalAssemblyPublication.FileName) File.Copy(file, Path.Combine(content, Path.GetFileName(file)));
            string authoring = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Authoring/TacticalAccessories"));
            string assemblyPath = Path.Combine(content, CharacterTacticalAssemblyPublication.FileName);
            File.Copy(Path.Combine(authoring, "tactical_card_assembly.double-edge-three.v1.json"), assemblyPath);
            var physical = await CharacterTacticalSaveStore.CreateAsync(saves, "tactical-slice-progress", "character-tactical-slice-json-v1");
            var progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            var first = await CharacterTacticalServices.OpenCandidateCoreAsync(progress.Create(), content);
            var owner = first.InventoryOwner;
            Assert.IsTrue(first.Player.TryReadOwnedContent(owner, out var before, out var issue), issue);
            Assert.IsEmpty(before.Accessories);
            var role = before.Characters.First(c => c.Cards.Any(card => card.CardId == "tactical.bound-action" && card.Revision == 2));
            var main = role.Cards.First(card => card.CardId == "tactical.bound-action" && card.Revision == 2);
            first.Player.TryReadCardAssemblyConfiguration(out var oldConfig, out _);
            long frame = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(first.Player.CaptureProgressJson()).Items.Frame + 1;
            var tick = new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation);
            var request = new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.Accessory,
                RequestId = 7654321, OwnerId = owner.Value, TargetContainerId = 1, ItemDefinitionId = 3003,
                ExpectedPlayerRevision = first.Player.Revision, Frame = frame };
            var receipt = first.Player.GrantContentAtTick(request, tick, out issue); Assert.IsTrue(receipt.Result.Success, issue);
            var selection = new CharacterTacticalCardAssembly { Schema = CharacterTacticalCardAssembly.CurrentSchema,
                ConfigurationRevision = oldConfig.Revision, ConfigurationHash = oldConfig.ContentHash, MainCard = main,
                Accessories = new List<CharacterTacticalAccessorySlot> { new CharacterTacticalAccessorySlot { SlotIndex = 0,
                    AccessoryItemInstanceId = receipt.ItemInstanceId.Value, Reference = oldConfig.Accessories.Single(a => a.ItemDefinitionId == 3003).Reference } } };
            Assert.IsTrue(first.Player.SetCharacterCardAssemblyAtTick(new ItemInstanceId(role.ItemInstanceId), owner, selection,
                role.Revision, first.Player.Revision, frame, tick, out issue), issue);
            Assert.IsTrue(await first.Player.FlushAsync()); await first.Progress.SaveAsync(true);
            string bindings = JsonConvert.SerializeObject(role.Bindings), preparation = first.Preparation.CaptureProgressJson(); first.Progress.Retire();

            File.Copy(Path.Combine(authoring, "tactical_card_assembly.double-edge-nine.v3.json"), assemblyPath, true);
            var reopenedProgress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            var nine = await CharacterTacticalServices.OpenCandidateCoreAsync(reopenedProgress.OpenActive(), content);
            Assert.IsTrue(nine.InventoryRegistryMigrated);
            Assert.IsTrue(nine.Player.TryReadOwnedContent(owner, out var retained, out issue), issue);
            Assert.AreEqual(before.Characters.Length, retained.Characters.Length); Assert.AreEqual(1, retained.Accessories.Length);
            var retainedRole = retained.Characters.Single(c => c.ItemInstanceId == role.ItemInstanceId);
            Assert.AreEqual(bindings, JsonConvert.SerializeObject(retainedRole.Bindings));
            Assert.AreEqual(preparation, nine.Preparation.CaptureProgressJson());
            Assert.AreEqual(receipt.ItemInstanceId.Value, retainedRole.CardAssemblies.Single().Accessories.Single().AccessoryItemInstanceId);
            nine.Player.TryReadCardAssemblyConfiguration(out var config, out _);
            Assert.AreEqual(3, config.Revision); Assert.AreEqual(9, config.Accessories.Length);
            Assert.AreEqual(config.ContentHash, retainedRole.CardAssemblies.Single().ConfigurationHash);
            int sequence = 1;
            foreach (var definition in config.Accessories.Where(a => a.ItemDefinitionId != 3003))
            {
                request = new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.Accessory,
                    RequestId = 7654321 + sequence++, OwnerId = owner.Value, TargetContainerId = 1, ItemDefinitionId = definition.ItemDefinitionId,
                    ExpectedPlayerRevision = nine.Player.Revision, Frame = frame };
                var granted = nine.Player.GrantContentAtTick(request, tick, out issue); Assert.IsTrue(granted.Result.Success, issue);
                var replay = nine.Player.GrantContentAtTick(request, tick, out issue);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, replay.Result.Status); Assert.AreEqual(granted.ItemInstanceId, replay.ItemInstanceId);
            }
            Assert.IsTrue(await nine.Player.FlushAsync()); await nine.Progress.SaveAsync(true); nine.Progress.Retire();
            var finalProgress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            var final = await CharacterTacticalServices.OpenCandidateCoreAsync(finalProgress.OpenActive(), content);
            Assert.IsFalse(final.InventoryRegistryMigrated);
            Assert.IsTrue(final.Player.TryReadOwnedContent(owner, out var owned, out issue), issue);
            Assert.AreEqual(9, owned.Accessories.Length); Assert.AreEqual(before.Characters.Length, owned.Characters.Length);
            final.Progress.Retire();
            // All publication and SaveGame writes are confined to this unique temporary root.
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void NinePublicationGatePreservesExactMainAndReviewedDefinitions(int previousBatch)
        {
            string root = Environment.GetEnvironmentVariable("MX_ACCESSORY_CANDIDATE_ROOT") ?? Directory.GetCurrentDirectory();
            string json = File.ReadAllText(Path.Combine(root, TacticalAccessoryThirdBatchPublication.NineSourcePath));
            var source = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(json);
            string previous = previousBatch == 0 ? null : JsonConvert.SerializeObject(Publication(previousBatch, source.Configuration.MainCards));
            var prepared = TacticalAccessoryThirdBatchPublication.PrepareNineForPublication(json, previous,
                ActualMainCatalog(), TacticalDoubleEdgePublication.LegacyRegistry(), new Dictionary<int, string>());
            Assert.AreEqual(9, prepared.Items.Length);
            Assert.AreEqual(CharacterTacticalCardQuality.Silver, prepared.Configuration.MainCards.Single().Quality);
            Assert.AreEqual(1, prepared.Configuration.MainCards.Single().AccessorySlotCount);
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse(json), JToken.FromObject(prepared)));
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 1, 1, 2, 1, 1, 1 },
                prepared.Configuration.Accessories.Select(a => a.Execution.ConfirmationMomentumCost.Value));
        }

        [TestCase("gold")] [TestCase("fee")] [TestCase("previous-main")]
        public void NinePublicationGateRejectsUnreviewedChanges(string change)
        {
            var mains = new[] { new CharacterTacticalMainCardDefinition { Card = ActualMainReference(), Quality = CharacterTacticalCardQuality.Silver } };
            var source = Publication(3, mains); var previous = Publication(1, Copy(mains));
            if (change == "gold") source.Configuration.MainCards[0].Quality = CharacterTacticalCardQuality.Gold;
            if (change == "fee") source.Configuration.Accessories[0].Execution.ConfirmationMomentumCost = 9;
            if (change == "previous-main") previous.Configuration.MainCards[0].Quality = CharacterTacticalCardQuality.Gold;
            source.Configuration.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(source.Configuration);
            previous.Configuration.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(previous.Configuration);
            string before = JsonConvert.SerializeObject(source), old = JsonConvert.SerializeObject(previous);
            Assert.Throws<JsonSerializationException>(() => TacticalAccessoryThirdBatchPublication.PrepareNineForPublication(before, old,
                ActualMainCatalog(), TacticalDoubleEdgePublication.LegacyRegistry(), new Dictionary<int, string>()));
            Assert.AreEqual(before, JsonConvert.SerializeObject(source));
            Assert.AreEqual(old, JsonConvert.SerializeObject(previous));
        }

        [Test]
        public async System.Threading.Tasks.Task NineDefinitionsGrantOnlyByExplicitItemsRequestsAndReopenWithoutDuplicates()
        {
            var store = new DeliveryStore(); var publication = Publication(3, FixtureMains());
            var player = await OpenDelivery(store, publication);
            Assert.IsTrue(player.TryReadOwnedContent(new InventoryOwnerId(1), out var empty, out _));
            Assert.IsEmpty(empty.Characters); Assert.IsEmpty(empty.Accessories);
            string checkpoint = store.Json;
            store.Reject = true; // Commit uses the existing automatic Flush; reject before granting.
            var receipts = new List<CharacterTacticalContentGrantReceipt>();
            for (int i = 0; i < publication.Items.Length; i++)
            {
                var request = AccessoryRequest(player, publication.Items[i].ItemDefinitionId, 100 + i);
                var receipt = player.GrantContentAtTick(request, DeliveryTick(), out var issue);
                Assert.IsTrue(receipt.Result.Success, issue); receipts.Add(receipt);
                var replay = player.GrantContentAtTick(request, DeliveryTick(), out issue);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, replay.Result.Status);
                Assert.AreEqual(receipt.ItemInstanceId, replay.ItemInstanceId);
            }
            Assert.IsTrue(player.TryReadOwnedContent(new InventoryOwnerId(1), out var owned, out _));
            Assert.IsEmpty(owned.Characters); Assert.AreEqual(9, owned.Accessories.Length);
            string accepted = player.CaptureProgressJson();
            var reused = AccessoryRequest(player, publication.Items[1].ItemDefinitionId, 100);
            Assert.IsFalse(player.GrantContentAtTick(reused, DeliveryTick(), out _).Result.Success);
            var wrongOwner = AccessoryRequest(player, publication.Items[0].ItemDefinitionId, 900); wrongOwner.OwnerId = 2;
            Assert.IsFalse(player.GrantContentAtTick(wrongOwner, DeliveryTick(), out _).Result.Success);
            Assert.AreEqual(accepted, player.CaptureProgressJson());
            Assert.IsFalse(await player.FlushAsync()); Assert.AreEqual(checkpoint, store.Json);
            store.Reject = false; Assert.IsTrue(await player.FlushAsync());
            var reopened = await OpenDelivery(store, publication);
            Assert.IsTrue(reopened.TryReadOwnedContent(new InventoryOwnerId(1), out owned, out _));
            Assert.AreEqual(9, owned.Accessories.Length); Assert.IsEmpty(owned.Characters);
            for (int i = 0; i < publication.Items.Length; i++)
            {
                var replay = reopened.GrantContentAtTick(AccessoryRequest(reopened, publication.Items[i].ItemDefinitionId, 100 + i), DeliveryTick(), out _);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, replay.Result.Status);
                Assert.AreEqual(receipts[i].ItemInstanceId, replay.ItemInstanceId);
            }
        }

        [TestCase(1)] [TestCase(2)]
        public async System.Threading.Tasks.Task ExactPriorFixedAssemblyReopensUnderNineWithoutChangingOwnershipOrBindings(int priorBatch)
        {
            string original = await FixedDeliverySave(priorBatch);
            var target = Publication(3, FixtureMains());
            var definitions = target.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry());
            string migrated = TacticalDoubleEdgeInventoryMigration.Prepare(original, definitions, target.Configuration);
            var before = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(original);
            var after = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(migrated);
            Assert.AreEqual(before.NextCardInstanceId, after.NextCardInstanceId);
            Assert.AreEqual(before.CharacterItems[0].Revision, after.CharacterItems[0].Revision);
            Assert.AreEqual(JsonConvert.SerializeObject(before.AccessoryItems), JsonConvert.SerializeObject(after.AccessoryItems));
            Assert.AreEqual(JsonConvert.SerializeObject(before.CharacterItems[0].Bindings), JsonConvert.SerializeObject(after.CharacterItems[0].Bindings));
            Assert.AreEqual(JsonConvert.SerializeObject(before.CharacterItems[0].Cards), JsonConvert.SerializeObject(after.CharacterItems[0].Cards));
            Assert.AreEqual(before.Items.Modules[1].CustomState.PayloadJson, after.Items.Modules[1].CustomState.PayloadJson);
            var priorInventory = JObject.Parse(before.Items.Modules[0].CustomState.PayloadJson);
            var nextInventory = JObject.Parse(after.Items.Modules[0].CustomState.PayloadJson);
            priorInventory.Remove("definitionHash"); nextInventory.Remove("definitionHash");
            Assert.IsTrue(JToken.DeepEquals(priorInventory, nextInventory));
            var assembly = after.CharacterItems[0].CardAssemblies.Single();
            Assert.AreEqual(target.Configuration.Revision, assembly.ConfigurationRevision);
            Assert.AreEqual(target.Configuration.ContentHash, assembly.ConfigurationHash);
            Assert.AreEqual(JsonConvert.SerializeObject(before.CharacterItems[0].CardAssemblies[0].Accessories), JsonConvert.SerializeObject(assembly.Accessories));
            Assert.AreEqual(migrated, TacticalDoubleEdgeInventoryMigration.Prepare(migrated, definitions, target.Configuration));
            var reopened = await OpenDelivery(new DeliveryStore { Json = migrated }, target);
            Assert.IsTrue(reopened.TryFreezeCardAssembly(new ItemInstanceId(after.CharacterItems[0].ItemInstanceId), new InventoryOwnerId(1),
                assembly, CharacterTacticalAssemblyMode.FixedPreassembled, out var fixedSnapshot, out var issue), issue);
            Assert.IsTrue(reopened.TryFreezeCardAssembly(new ItemInstanceId(after.CharacterItems[0].ItemInstanceId), new InventoryOwnerId(1),
                assembly, CharacterTacticalAssemblyMode.RandomDrawAssembly, out var randomSnapshot, out issue), issue);
            Assert.AreEqual(1, fixedSnapshot.AccessorySlotCount); Assert.AreEqual(fixedSnapshot.AccessorySlotCount, randomSnapshot.AccessorySlotCount);
            Assert.IsTrue(reopened.TryReadOwnedContent(new InventoryOwnerId(1), out var owned, out issue), issue);
            Assert.AreEqual(1, owned.Characters.Length); Assert.AreEqual(1, owned.Accessories.Length);
        }

        [TestCase("unknown-hash")] [TestCase("changed-quality")] [TestCase("unreviewed-registry")]
        [TestCase("already-unreviewed-registry")]
        public async System.Threading.Tasks.Task NineMigrationRefusesBroaderCompatibility(string change)
        {
            string original = await FixedDeliverySave(1);
            var target = Publication(3, FixtureMains());
            if (change == "unknown-hash")
            {
                var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(original);
                state.CharacterItems[0].CardAssemblies[0].ConfigurationHash = "lab.fixed.approved-test.v1";
                original = JsonConvert.SerializeObject(state);
            }
            if (change == "changed-quality")
            {
                target.Configuration.MainCards[0].Quality = CharacterTacticalCardQuality.Gold;
                target.Configuration.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(target.Configuration);
            }
            if (change == "unreviewed-registry" || change == "already-unreviewed-registry") target.Items[0].MaxStackCount = 2;
            if (change == "already-unreviewed-registry")
            {
                var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(original);
                var inventory = new InventoryStore();
                Assert.IsTrue(new InventorySaveStateProvider(inventory, Publication(1, FixtureMains()).BuildItemDefinitions(
                    TacticalDoubleEdgePublication.LegacyRegistry())).RestoreSaveState(state.Items.ToRuntimeState()).Success);
                state.Items.Modules[0] = new InventorySaveStateProvider(inventory, target.BuildItemDefinitions(
                    TacticalDoubleEdgePublication.LegacyRegistry())).CaptureSaveState().Value.ModuleStates[0];
                original = JsonConvert.SerializeObject(state);
            }
            string unchanged = original;
            Assert.Throws<JsonSerializationException>(() => TacticalDoubleEdgeInventoryMigration.Prepare(original,
                target.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()), target.Configuration));
            Assert.AreEqual(unchanged, original);
        }

        [Test]
        public async System.Threading.Tasks.Task NineMigrationCandidateCancelFailureAndCasKeepTheOriginalCheckpoint()
        {
            string original = await FixedDeliverySave(2);
            var target = Publication(3, FixtureMains());
            string migrated = TacticalDoubleEdgeInventoryMigration.Prepare(original,
                target.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()), target.Configuration);
            var durable = new DeliveryStore(); var progress = new CharacterTacticalProgressStore(durable, null);
            var initial = progress.Create(original, "unchanged-preparation"); initial.Bind(() => original, () => "unchanged-preparation");
            await initial.SaveAsync(true); string checkpoint = durable.Json;
            var canceled = progress.OpenActive(); await canceled.PlayerStore.SaveAsync(migrated);
            Assert.AreEqual(checkpoint, durable.Json); Assert.AreEqual(original, await progress.OpenActive().PlayerStore.LoadAsync());
            var stale = progress.OpenActive(); var candidate = progress.OpenActive();
            await candidate.PlayerStore.SaveAsync(migrated); candidate.Bind(() => migrated, () => "unchanged-preparation");
            durable.Reject = true; Assert.ThrowsAsync<InvalidOperationException>(async () => await candidate.SaveAsync(true));
            Assert.AreEqual(checkpoint, durable.Json);
            durable.Reject = false; await candidate.SaveAsync(true);
            Assert.AreEqual(migrated, await progress.OpenActive().PlayerStore.LoadAsync());
            Assert.AreEqual("unchanged-preparation", await progress.OpenActive().PreparationStore.LoadAsync());
            stale.Bind(() => original, () => "unchanged-preparation");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await stale.SaveAsync(true));
            Assert.AreEqual(migrated, await progress.OpenActive().PlayerStore.LoadAsync());
        }

        [Test]
        public async System.Threading.Tasks.Task NineMigrationCannotUseAnOldHeaderToLegalizeANewerAccessory()
        {
            string current = await FixedDeliverySave(3, 3014);
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(current);
            var first = Publication(1, FixtureMains()).Configuration;
            state.CharacterItems[0].CardAssemblies[0].ConfigurationRevision = first.Revision;
            state.CharacterItems[0].CardAssemblies[0].ConfigurationHash = first.ContentHash;
            var target = Publication(3, FixtureMains());
            Assert.Throws<JsonSerializationException>(() => TacticalDoubleEdgeInventoryMigration.Prepare(JsonConvert.SerializeObject(state),
                target.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()), target.Configuration));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void NineRegistryMigrationAcceptsOnlyExactPriorRegistriesAndAddsNoStacks(int batch)
        {
            var legacy = TacticalDoubleEdgePublication.LegacyRegistry();
            var prior = batch == 0 ? legacy : Publication(batch, FixtureMains()).BuildItemDefinitions(legacy);
            var target = Publication(3, FixtureMains()); var next = target.BuildItemDefinitions(legacy);
            var inventory = new InventoryStore(); inventory.Register(new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 32, "character-test.player-roles"));
            Assert.IsTrue(new InventoryService(prior, inventory).AddStack(new InventoryContainerId(1), 7, new ItemStack(new ItemDefinitionId(1), 1)).Success);
            var items = new CharacterTacticalItemState { Frame = 10, Modules = new[] {
                new InventorySaveStateProvider(inventory, prior).CaptureSaveState().Value.ModuleStates[0],
                new ItemAcquisitionSaveStateProvider(new ItemAcquisitionLedger()).CaptureSaveState().Value.ModuleStates[0] } };
            var state = new CharacterTacticalPlayerState { Schema = CharacterTacticalPlayerState.CharacterTacticalSchema,
                Items = items, CharacterItems = new List<CharacterTacticalCharacterItemState>() };
            string source = JsonConvert.SerializeObject(state);
            var migrated = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(TacticalDoubleEdgeInventoryMigration.Prepare(source, next, target.Configuration));
            Assert.IsNull(migrated.AccessoryItems); Assert.IsEmpty(migrated.CharacterItems);
            var before = JObject.Parse(items.Modules[0].CustomState.PayloadJson); var after = JObject.Parse(migrated.Items.Modules[0].CustomState.PayloadJson);
            before.Remove("definitionHash"); after.Remove("definitionHash"); Assert.IsTrue(JToken.DeepEquals(before, after));
            var unknown = Publication(1, FixtureMains()); unknown.Items[0].MaxStackCount = 2;
            state.Items.Modules[0] = new InventorySaveStateProvider(inventory, unknown.BuildItemDefinitions(legacy)).CaptureSaveState().Value.ModuleStates[0];
            Assert.Throws<JsonSerializationException>(() => TacticalDoubleEdgeInventoryMigration.Prepare(JsonConvert.SerializeObject(state), next, target.Configuration));
        }

        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        private static CharacterTacticalAssemblyPublication Publication(int batch, CharacterTacticalMainCardDefinition[] mains)
            => batch == 1 ? TacticalDoubleEdgePublication.Create(mains) : batch == 2
                ? TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(mains) : TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(mains);
        private static CharacterTacticalCardReference FixtureMain() => new CharacterTacticalCardReference { CardId = "fixture.main", Revision = 1, ContentHash = "fixture-main-v1" };
        private static CharacterTacticalMainCardDefinition[] FixtureMains() => new[] { new CharacterTacticalMainCardDefinition { Card = FixtureMain(), Quality = CharacterTacticalCardQuality.Silver } };
        private static CharacterTacticalContentCatalog ActualMainCatalog()
        {
            string json = File.ReadAllText(Environment.GetEnvironmentVariable("MX_ACCESSORY_PUBLISHED_MAIN")
                ?? "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/tactical.bound-action.json");
            var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(json, new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, null));
            Assert.IsTrue(result.Success); var catalog = new CharacterTacticalContentCatalog(); catalog.SetPublishedCards(result.Catalog.Cards); return catalog;
        }
        private static CharacterTacticalCardReference ActualMainReference()
        {
            var card = ActualMainCatalog().Cards.Single(c => c.CardId == "tactical.bound-action" && c.Revision == 2);
            return new CharacterTacticalCardReference { CardId = card.CardId, Revision = card.Revision, ContentHash = card.ContentHash };
        }
        private static RuntimeTickContext DeliveryTick() => new RuntimeTickContext(10, 1d / 60d, 10d / 60d, RuntimeTickStage.Simulation);
        private static CharacterTacticalContentGrantRequest AccessoryRequest(CharacterTacticalPlayerService player, int definition, long request)
            => new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.Accessory, RequestId = request,
                OwnerId = 1, TargetContainerId = 1, ItemDefinitionId = definition, ExpectedPlayerRevision = player.Revision, Frame = 10 };
        private static async System.Threading.Tasks.Task<CharacterTacticalPlayerService> OpenDelivery(DeliveryStore store, CharacterTacticalAssemblyPublication publication)
        {
            var catalog = new CharacterTacticalContentCatalog {
                Actions = new[] { new CharacterTacticalActionDefinition { Id = "action:fixture", ActionId = 301, ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "char.000001" } } },
                Cards = new[] { new CharacterTacticalCardDefinition { CardId = FixtureMain().CardId, Revision = 1, ContentHash = FixtureMain().ContentHash, Slots = new[] { "behavior.action" } } },
                Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = "char.000001", ActionIds = new[] { "action:fixture" }, Cards = new[] { FixtureMain() } } } };
            var card = FixtureMain(); card.InstanceId = 1;
            var defaults = new CharacterTacticalDefaults { CharacterStableId = "char.000001", ActionIds = new List<string> { "action:fixture" },
                Cards = new List<CharacterTacticalCardReference> { card }, Bindings = new List<CharacterTacticalSlotBinding> {
                    new CharacterTacticalSlotBinding { CardInstanceId = 1, CardId = card.CardId, SlotPath = "behavior.action", TacticalActionId = "action:fixture" } } };
            var player = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, store, addMissingDefaults: false, instantiateTemplateDefaults: false);
            Assert.IsTrue(player.ConfigureCharacterItemInventory(publication.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()),
                new[] { new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 32, "character-test.player-roles") },
                new[] { new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 1, CharacterStableId = "char.000001" } }, out var issue), issue);
            Assert.IsTrue(player.ConfigureCardAssembly(publication.Configuration, out issue), issue); return player;
        }
        private static async System.Threading.Tasks.Task<string> FixedDeliverySave(int batch, int accessoryDefinition = 3003)
        {
            var store = new DeliveryStore(); var publication = Publication(batch, FixtureMains()); var player = await OpenDelivery(store, publication);
            var role = player.GrantContentAtTick(new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.Character,
                RequestId = 1, OwnerId = 1, TargetContainerId = 1, ItemDefinitionId = 1, DisplayName = "fixture", Frame = 10,
                ExpectedPlayerRevision = player.Revision }, DeliveryTick(), out var issue);
            Assert.IsTrue(role.Result.Success, issue);
            var accessory = player.GrantContentAtTick(AccessoryRequest(player, accessoryDefinition, 100), DeliveryTick(), out issue); Assert.IsTrue(accessory.Result.Success, issue);
            Assert.IsTrue(player.TryReadCharacterItem(role.ItemInstanceId, new InventoryOwnerId(1), out var character, out issue), issue);
            var assembly = new CharacterTacticalCardAssembly { Schema = CharacterTacticalCardAssembly.CurrentSchema,
                ConfigurationRevision = publication.Configuration.Revision, ConfigurationHash = publication.Configuration.ContentHash,
                MainCard = character.Cards[0], Accessories = new List<CharacterTacticalAccessorySlot> {
                    new CharacterTacticalAccessorySlot { SlotIndex = 0, AccessoryItemInstanceId = accessory.ItemInstanceId.Value,
                        Reference = publication.Configuration.Accessories.Single(a => a.ItemDefinitionId == accessoryDefinition).Reference } } };
            Assert.IsTrue(player.SetCharacterCardAssemblyAtTick(role.ItemInstanceId, new InventoryOwnerId(1), assembly,
                character.Revision, player.Revision, 10, DeliveryTick(), out issue), issue);
            Assert.IsTrue(await player.FlushAsync()); return store.Json;
        }
        private sealed class DeliveryStore : ICharacterTacticalStateStore
        {
            internal string Json; internal bool Reject;
            public System.Threading.Tasks.Task<string> LoadAsync() => System.Threading.Tasks.Task.FromResult(Json);
            public System.Threading.Tasks.Task SaveAsync(string json)
            { if (Reject) throw new InvalidOperationException("fixture:save-failed"); Json = json; return System.Threading.Tasks.Task.CompletedTask; }
            public System.Threading.Tasks.Task ResetAsync(string json) => throw new InvalidOperationException("Reset is forbidden in delivery migration.");
        }

        [TestCase("tactical_card_assembly.third-batch-three.v3.json",3,5)]
        [TestCase("tactical_card_assembly.double-edge-nine.v3.json",9,11)]
        [TestCase("tactical_card_assembly.third-batch-trial-gold.v3.json",9,11)]
        public void GeneratedConfigurationValidatesAgainstTheActualPublishedMain(string file,int count,int itemCount)
        {
            string root=Environment.GetEnvironmentVariable("MX_ACCESSORY_CANDIDATE_ROOT")??Directory.GetCurrentDirectory();
            var p=JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(File.ReadAllText(Path.Combine(root,"Authoring/TacticalAccessories",file)));
            var main=p.Configuration.MainCards.Single();var document=JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MX_ACCESSORY_PUBLISHED_MAIN")??"Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/tactical.bound-action.json"));
            var row=document["cards"].Single(c=>(string)c["stableCardId"]==main.Card.CardId&&(int)c["revision"]==main.Card.Revision);
            Assert.AreEqual(row["contentHash"].ToString(),main.Card.ContentHash);
            var expected=count==3?TacticalAccessoryThirdBatchPublication.Create(p.Configuration.MainCards):TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(p.Configuration.MainCards);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(expected),JToken.FromObject(p)));
            var loaded=CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(document.ToString(),new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,null));Assert.IsTrue(loaded.Success);
            var catalog=new CharacterTacticalContentCatalog();catalog.SetPublishedCards(loaded.Catalog.Cards);var registry=p.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry());
            CharacterTacticalAssemblyRules.ValidateConfiguration(p.Configuration,catalog,registry,new Dictionary<int,string>());
            Assert.AreEqual(count,p.Configuration.Accessories.Length);Assert.AreEqual(itemCount,registry.Definitions.Count);
            Assert.AreEqual(file.Contains("gold")?CharacterTacticalCardQuality.Gold:CharacterTacticalCardQuality.Silver,main.Quality);
            foreach(var card in p.Configuration.Accessories.Where(a=>a.Execution.Version==4)) {
                Assert.AreEqual(1,card.Execution.ConfirmationMomentumCost);Assert.AreEqual(CharacterTacticalCardQuality.Silver,card.Quality);Assert.IsNotEmpty(card.Reference.ContentHash);
            }
        }
        [Test] public void NineCardRegistryMigrationPreservesPriorInventoryAndAcquisitionLedger()
        {
            var legacy=TacticalDoubleEdgePublication.LegacyRegistry();var prior=TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(null).BuildItemDefinitions(legacy);
            var next=TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(null).BuildItemDefinitions(legacy);
            var store=new InventoryStore();var container=new InventoryContainer(new InventoryContainerId(1),new InventoryOwnerId(1),32,"character-test.player-roles");store.Register(container);
            Assert.IsTrue(new InventoryService(prior,store).AddStack(container.ContainerId,7,new ItemStack(new ItemDefinitionId(1),1)).Success);
            var ledger=new ItemAcquisitionLedger();Assert.IsTrue(ledger.TryCommit(new ItemAcquisitionRequestId(765)));
            var items=new CharacterTacticalItemState{Frame=123,Modules=new[]{new InventorySaveStateProvider(store,prior).CaptureSaveState().Value.ModuleStates[0],new ItemAcquisitionSaveStateProvider(ledger).CaptureSaveState().Value.ModuleStates[0]}};
            var player=new CharacterTacticalPlayerState{Schema=CharacterTacticalPlayerState.CharacterTacticalSchema,Items=items,NextCardInstanceId=79,LegacyImportCompleted=true,CharacterItems=new List<CharacterTacticalCharacterItemState>()};
            string original=JsonConvert.SerializeObject(player),migrated=TacticalDoubleEdgeInventoryMigration.Prepare(original,next);
            var after=JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(migrated);Assert.AreEqual(79,after.NextCardInstanceId);Assert.AreEqual(123,after.Items.Frame);
            Assert.AreEqual(items.Modules[1].CustomState.PayloadJson,after.Items.Modules[1].CustomState.PayloadJson);
            var a=JObject.Parse(items.Modules[0].CustomState.PayloadJson);var b=JObject.Parse(after.Items.Modules[0].CustomState.PayloadJson);
            Assert.AreNotEqual(a["definitionHash"],b["definitionHash"]);a.Remove("definitionHash");b.Remove("definitionHash");Assert.IsTrue(JToken.DeepEquals(a,b));
            Assert.IsTrue(new InventorySaveStateProvider(new InventoryStore(),next).RestoreSaveState(after.Items.ToRuntimeState()).Success);
            Assert.AreEqual(migrated,TacticalDoubleEdgeInventoryMigration.Prepare(migrated,next));Assert.AreEqual(original,JsonConvert.SerializeObject(player));
        }
    }
}
