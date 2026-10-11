using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class ItemAcquisitionTests
    {
        private static class SampleOwnerIds { public const int Hero = 1; public const int Other = 2; }

        [Test]
        public void DirectGrant_InsertsIntoEmptySlotAndMergesBeforeEmptySlot()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out ItemAcquisitionLedger ledger,
                out ItemDefinition herb,
                out _,
                out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 18)).Success);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(10001),
                ItemAcquisitionSourceKind.DirectGrant,
                null,
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 7) }));

            Assert.AreEqual(ItemAcquisitionStatus.Success, result.Status);
            Assert.AreEqual(20, bag.GetStack(0).Count);
            Assert.AreEqual(5, bag.GetStack(1).Count);
            Assert.IsTrue(ledger.Contains(new ItemAcquisitionRequestId(10001)));
        }

        [Test]
        public void DirectGrant_CapacityFailureDoesNotMutateInventoryOrLedger()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out ItemAcquisitionLedger ledger,
                out ItemDefinition herb,
                out ItemDefinition ore,
                out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 1, "bag");
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(ore.DefinitionId, 10)).Success);
            long hashBefore = ComputeInventoryAndLedgerHash(store, ledger);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(10002),
                ItemAcquisitionSourceKind.DirectGrant,
                null,
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 1) }));

            Assert.AreEqual(ItemAcquisitionStatus.InsufficientCapacity, result.Status);
            Assert.AreEqual(0, ledger.Count);
            Assert.AreEqual(hashBefore, ComputeInventoryAndLedgerHash(store, ledger));
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(0).DefinitionId);
            Assert.AreEqual(10, bag.GetStack(0).Count);
        }

        [Test]
        public void DirectGrant_RepeatedRequestIdDoesNotDuplicateItems()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out _,
                out ItemDefinition herb,
                out _,
                out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            var request = new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(10003),
                ItemAcquisitionSourceKind.DirectGrant,
                null,
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 3) });

            Assert.AreEqual(ItemAcquisitionStatus.Success, service.Execute(request).Status);
            ItemAcquisitionResult duplicate = service.Execute(request);

            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, duplicate.Status);
            Assert.AreEqual(3, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);
        }

        [Test]
        public void PurchaseTransaction_RemovesCostAndGrantsItemAtomically()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out _,
                out ItemDefinition herb,
                out _,
                out ItemDefinition coin);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 3, "bag");
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(coin.DefinitionId, 100)).Success);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(20001),
                ItemAcquisitionSourceKind.ShopPurchase,
                new[] { new ItemCostLine(bag.ContainerId, coin.DefinitionId, 35) },
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 4) }));

            Assert.AreEqual(ItemAcquisitionStatus.Success, result.Status);
            Assert.AreEqual(65, bag.GetStack(0).Count);
            Assert.AreEqual(herb.DefinitionId, bag.GetStack(1).DefinitionId);
            Assert.AreEqual(4, bag.GetStack(1).Count);
        }

        [Test]
        public void PurchaseTransaction_InsufficientCostDoesNotGrantItem()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out ItemAcquisitionLedger ledger,
                out ItemDefinition herb,
                out _,
                out ItemDefinition coin);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(coin.DefinitionId, 5)).Success);
            long hashBefore = ComputeInventoryAndLedgerHash(store, ledger);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(20002),
                ItemAcquisitionSourceKind.ShopPurchase,
                new[] { new ItemCostLine(bag.ContainerId, coin.DefinitionId, 10) },
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 1) }));

            Assert.AreEqual(ItemAcquisitionStatus.InsufficientCost, result.Status);
            Assert.AreEqual(0, result.CostLineIndex);
            Assert.AreEqual(hashBefore, ComputeInventoryAndLedgerHash(store, ledger));
            Assert.AreEqual(5, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);
        }

        [Test]
        public void PurchaseTransaction_GrantCapacityFailureDoesNotRemoveCost()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out ItemAcquisitionLedger ledger,
                out ItemDefinition herb,
                out ItemDefinition ore,
                out ItemDefinition coin);
            var wallet = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 1, "wallet");
            var bag = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(SampleOwnerIds.Hero), 1, "bag");
            store.Register(wallet);
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(wallet.ContainerId, 0, new ItemStack(coin.DefinitionId, 100)).Success);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(ore.DefinitionId, 10)).Success);
            long hashBefore = ComputeInventoryAndLedgerHash(store, ledger);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(20003),
                ItemAcquisitionSourceKind.ShopPurchase,
                new[] { new ItemCostLine(wallet.ContainerId, coin.DefinitionId, 20) },
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 1) }));

            Assert.AreEqual(ItemAcquisitionStatus.InsufficientCapacity, result.Status);
            Assert.AreEqual(100, wallet.GetStack(0).Count);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(0).DefinitionId);
            Assert.AreEqual(hashBefore, ComputeInventoryAndLedgerHash(store, ledger));
        }

        [Test]
        public void PurchaseTransaction_MultipleLinesAreCommittedTogether()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out _,
                out ItemDefinition herb,
                out ItemDefinition ore,
                out ItemDefinition coin);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 4, "bag");
            store.Register(bag);
            Assert.IsTrue(service.Inventory.AddStack(bag.ContainerId, 0, new ItemStack(coin.DefinitionId, 75)).Success);

            ItemAcquisitionResult result = service.Execute(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(20004),
                ItemAcquisitionSourceKind.ShopPurchase,
                new[] { new ItemCostLine(bag.ContainerId, coin.DefinitionId, 50) },
                new[]
                {
                    new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 20),
                    new ItemGrantLine(bag.ContainerId, ore.DefinitionId, 6)
                }));

            Assert.AreEqual(ItemAcquisitionStatus.Success, result.Status);
            Assert.AreEqual(25, bag.GetStack(0).Count);
            Assert.AreEqual(herb.DefinitionId, bag.GetStack(1).DefinitionId);
            Assert.AreEqual(20, bag.GetStack(1).Count);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(2).DefinitionId);
            Assert.AreEqual(6, bag.GetStack(2).Count);
        }

        [Test]
        public void AcquisitionSaveState_RoundTripsLedgerAndBlocksRepeatedRequest()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out ItemAcquisitionLedger ledger,
                out ItemDefinition herb,
                out _,
                out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            var request = new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(30001),
                ItemAcquisitionSourceKind.Reward,
                null,
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 2) });
            Assert.AreEqual(ItemAcquisitionStatus.Success, service.Execute(request).Status);
            long hashBefore = new RuntimeHashCombiner(new IRuntimeHashContributor[] { new ItemAcquisitionHashContributor(ledger) }).ComputeHash(RuntimeFrame.Zero);

            var provider = new ItemAcquisitionSaveStateProvider(ledger, () => 33);
            RuntimeSaveState captured = provider.CaptureSaveState().Value;
            Assert.AreEqual(ItemRuntimeIds.AcquisitionModule, captured.ModuleStates[0].ModuleId);
            Assert.AreEqual(ItemRuntimeIds.AcquisitionModule, captured.ModuleStates[0].CustomState.TypeId);
            string json = RuntimeSaveStateJson.SaveToJson(captured);
            RuntimeSaveState loaded = RuntimeSaveStateJson.LoadFromJson(json).Value;

            var restoredLedger = new ItemAcquisitionLedger();
            var restorer = new ItemAcquisitionSaveStateProvider(restoredLedger);
            Assert.IsTrue(restorer.RestoreSaveState(loaded).Success);
            long hashAfter = new RuntimeHashCombiner(new IRuntimeHashContributor[] { new ItemAcquisitionHashContributor(restoredLedger) }).ComputeHash(RuntimeFrame.Zero);
            Assert.AreEqual(hashBefore, hashAfter);

            var restoredService = new InventoryTransactionService(service.Inventory, restoredLedger);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, restoredService.Execute(request).Status);
            Assert.AreEqual(2, bag.GetStack(0).Count);
        }

        [Test]
        public void AcquisitionSaveState_InvalidLedgerPayloadDoesNotPartiallyRestore()
        {
            var ledger = new ItemAcquisitionLedger();
            Assert.IsTrue(ledger.TryCommit(new ItemAcquisitionRequestId(1)));
            var restorer = new ItemAcquisitionSaveStateProvider(ledger);
            var module = new RuntimeModuleSaveState(
                ItemRuntimeIds.AcquisitionModule,
                ItemAcquisitionSaveStateProvider.CustomStateSchemaVersion,
                new RuntimeCustomState(3304, ItemRuntimeIds.AcquisitionModule,
                    ItemAcquisitionSaveStateProvider.CustomStateSchemaVersion,
                    "{\"schemaVersion\":1,\"committedRequestIds\":[2,2]}"));
            var invalid = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                System.DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                null,
                null,
                new[] { module });

            RuntimeSaveStateResult<bool> result = restorer.RestoreSaveState(invalid);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            Assert.AreEqual(1, ledger.Count);
            Assert.IsTrue(ledger.Contains(new ItemAcquisitionRequestId(1)));
            Assert.IsFalse(ledger.Contains(new ItemAcquisitionRequestId(2)));
        }

        [Test]
        public void AcquisitionRuntimeModule_ExecutesRequestAndRejectsStaleHandle()
        {
            InventoryTransactionService service = CreateTransactionService(
                out InventoryStore store,
                out _,
                out ItemDefinition herb,
                out _,
                out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            var module = new ItemAcquisitionRuntimeModule(service);

            ItemAcquisitionRequestHandle handle = module.Requests.Add(new ItemAcquisitionRequest(
                new ItemAcquisitionRequestId(40001),
                ItemAcquisitionSourceKind.DirectGrant,
                null,
                new[] { new ItemGrantLine(bag.ContainerId, herb.DefinitionId, 3) }));
            module.Commands.Enqueue(ItemAcquisitionRuntimeCommandFactory.Create(RuntimeFrame.Zero, 7, handle, new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_ItemAcquisitionTests.Id_test_acquire, 0)));
            module.Tick(new RuntimeTickContext(0, 1d / 60d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(3, bag.GetStack(0).Count);
            var stale = new ItemAcquisitionRequestHandle(handle.Index, handle.Generation);
            module.Commands.Enqueue(ItemAcquisitionRuntimeCommandFactory.Create(new RuntimeFrame(1), 7, stale, new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_ItemAcquisitionTests.Id_test_stale, 0)));
            module.Tick(new RuntimeTickContext(1, 1d / 60d, 1d / 60d, RuntimeTickStage.Simulation));

            var events = new List<ItemAcquisitionRuntimeEvent>();
            module.DrainEvents(new RuntimeFrame(1), events);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ItemAcquisitionRuntimeEventType.Committed, events[0].EventType);
            Assert.AreEqual(ItemAcquisitionStatus.Success, events[0].Result.Status);
            Assert.AreEqual(ItemAcquisitionRuntimeEventType.Rejected, events[1].EventType);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, events[1].Result.Status);
        }

        private static InventoryTransactionService CreateTransactionService(
            out InventoryStore store,
            out ItemAcquisitionLedger ledger,
            out ItemDefinition herb,
            out ItemDefinition ore,
            out ItemDefinition coin)
        {
            var definitions = new ItemDefinitionRegistry();
            herb = ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20);
            ore = ItemDefinitionTests.CreateDefinition(1002, "item.ore", ItemKind.Material, 10);
            coin = ItemDefinitionTests.CreateDefinition(9001, "currency.coin", ItemKind.Currency, 999);
            Assert.IsTrue(definitions.Register(herb).Success);
            Assert.IsTrue(definitions.Register(ore).Success);
            Assert.IsTrue(definitions.Register(coin).Success);
            store = new InventoryStore();
            ledger = new ItemAcquisitionLedger();
            return new InventoryTransactionService(new InventoryService(definitions, store), ledger);
        }

        private static long ComputeInventoryAndLedgerHash(InventoryStore store, ItemAcquisitionLedger ledger)
        {
            return new RuntimeHashCombiner(new IRuntimeHashContributor[]
                {
                    new InventoryHashContributor(store),
                    new ItemAcquisitionHashContributor(ledger)
                }).ComputeHash(RuntimeFrame.Zero);
        }
    }

    /// <summary>Owner-level fixtures use explicit sample content; they never configure the live slice or player stores.</summary>
    public sealed class CharacterTacticalItemTransactionTests
    {
        private sealed class Store : ICharacterTacticalStateStore
        {
            internal string Json;
            internal bool Fail;
            internal TaskCompletionSource<bool> Gate;
            internal List<string> Writes = new();
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public async Task SaveAsync(string json)
            {
                if (Fail) throw new IOException("fixture write failure");
                if (Gate != null) await Gate.Task;
                Json = json; Writes.Add(json);
            }
            public Task ResetAsync(string json) => SaveAsync(json);
        }
        private static CharacterTacticalCardReference Card() => new() { CardId = "fixture.card", Revision = 3, ContentHash = "fixture-hash" };
        private static CharacterTacticalContentCatalog Catalog() => new() {
            Cards = new[] { new CharacterTacticalCardDefinition { CardId = "fixture.card", Revision = 3,
                ContentHash = "fixture-hash", Slots = Array.Empty<string>() } },
            Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = "fixture.hero", Cards = new[] { Card() } } }
        };
        private static CharacterTacticalDefaults[] Defaults() => new[] { new CharacterTacticalDefaults {
            CharacterStableId = "fixture.hero", Cards = new List<CharacterTacticalCardReference> { Card() } } };
        private static Task<CharacterTacticalPlayerService> Open(ICharacterTacticalStateStore store)
            => CharacterTacticalPlayerService.OpenAsync(Catalog(), Defaults(), store);
        private static ItemDefinitionRegistry Definitions()
        {
            var result = new ItemDefinitionRegistry();
            result.Register(ItemDefinitionTests.CreateDefinition(1001, "fixture.ordinary", ItemKind.Material, 20));
            return result;
        }
        private static void Configure(CharacterTacticalPlayerService owner)
        {
            Assert.IsTrue(owner.ConfigureItemInventory(Definitions(), new[] {
                new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 1, "fixture.bag") }, out string issue), issue);
        }
        private static ItemAcquisitionRequest Request(long id, int count = 1, int cost = 0) => new(
            new ItemAcquisitionRequestId(id), ItemAcquisitionSourceKind.Reward,
            cost == 0 ? null : new[] { new ItemCostLine(new InventoryContainerId(1), new ItemDefinitionId(1001), cost) },
            count == 0 ? null : new[] { new ItemGrantLine(new InventoryContainerId(1), new ItemDefinitionId(1001), count) });
        private static CharacterTacticalCardGrant[] Cards() => new[] { new CharacterTacticalCardGrant { CharacterStableId = "fixture.hero", Card = Card() } };
        private static ItemAcquisitionResult Execute(CharacterTacticalPlayerService owner, ItemAcquisitionRequest request,
            bool card = false, long settlement = 0, long frame = 10) => owner.ExecuteItemTransactionAtTick(request,
                card ? Cards() : null, settlement, frame, new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation), out _);
        private static int Count(CharacterTacticalPlayerService owner)
        {
            Assert.IsTrue(owner.TryReadItemInventory(out var inventory, out string issue), issue);
            Assert.IsTrue(inventory.TryGet(new InventoryContainerId(1), out var bag));
            return bag.GetStack(0).Count;
        }
        private static CharacterTacticalPlayerState Saved(Store store) => JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Json);

        [Test] public async Task V2ConfigurationDoesNotRewriteCardsOrCreateOwnership()
        {
            var store = new Store(); var owner = await Open(store); string before = store.Json; long revision = owner.Revision;
            Configure(owner);
            Assert.AreEqual(before, store.Json); Assert.AreEqual(revision, owner.Revision);
            Assert.AreEqual("mx.character-test.tactical-player.v2", Saved(store).Schema);
            Assert.IsNull(Saved(store).Items); Assert.AreEqual(0, Count(owner));
            Assert.AreEqual(1UL, owner.Read("fixture.hero").Cards.Single().InstanceId);
        }

        [Test] public async Task ExistingV2ExactCardBindingAndInstanceSurviveUpgrade()
        {
            var catalog = Catalog(); catalog.Cards[0].Slots = new[] { "attack" };
            catalog.Actions = new[] { new CharacterTacticalActionDefinition { Id = "fixture.attack", ActionId = 301,
                ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } } };
            var defaults = Defaults(); defaults[0].ActionIds.Add("fixture.attack");
            defaults[0].Bindings.Add(new CharacterTacticalSlotBinding { CardId = "fixture.card", SlotPath = "attack", TacticalActionId = "fixture.attack" });
            var store = new Store(); var owner = await CharacterTacticalPlayerService.OpenAsync(catalog, defaults, store);
            string card = JsonConvert.SerializeObject(owner.Read("fixture.hero").Cards[0]);
            string binding = JsonConvert.SerializeObject(owner.Read("fixture.hero").Bindings); Configure(owner);
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(120), true).Status); await owner.FlushAsync();
            owner = await CharacterTacticalPlayerService.OpenAsync(catalog, defaults, store);
            Assert.AreEqual(card, JsonConvert.SerializeObject(owner.Read("fixture.hero").Cards[0]));
            Assert.AreEqual(binding, JsonConvert.SerializeObject(owner.Read("fixture.hero").Bindings));
            Assert.AreEqual(2UL, owner.Read("fixture.hero").Cards[1].InstanceId);
            Assert.IsTrue(owner.TryGetBindings("fixture.hero", 1UL, out _, out _));
            Assert.IsFalse(owner.TryGetBindings("fixture.hero", 2UL, out _, out _));
        }

        [Test] public async Task ReadInventoryCannotAliasAuthority()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(121));
            Assert.IsTrue(owner.TryReadItemInventory(out var detached, out _)); detached.Clear();
            Assert.AreEqual(1, Count(owner));
        }

        [Test] public async Task SameTickCommandsProduceSameCheckpointWithoutWallClockInput()
        {
            var firstStore = new Store(); var secondStore = new Store();
            var first = await Open(firstStore); var second = await Open(secondStore); Configure(first); Configure(second);
            Execute(first, Request(122), true, 722);
            second.ExecuteItemTransactionAtTick(Request(122), Cards(), 722, 10,
                new RuntimeTickContext(10, 123, 456, RuntimeTickStage.Simulation), out _);
            await first.FlushAsync(); await second.FlushAsync();
            Assert.AreEqual(firstStore.Json, secondStore.Json, "Elapsed time and provider capture timestamps cannot enter logical checkpoint state.");
        }

        [Test] public async Task CombinedGrantPublishesOneRevisionWithExistingCardOwnerAndLedger()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); long revision = owner.Revision;
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(101, 4), true, 701).Status);
            await owner.FlushAsync(); var saved = Saved(store);
            Assert.AreEqual(revision + 1, owner.Revision); Assert.AreEqual(4, Count(owner));
            Assert.AreEqual(new ulong[] { 1, 2 }, owner.Read("fixture.hero").Cards.Select(value => value.InstanceId).ToArray());
            Assert.AreEqual(3UL, saved.NextCardInstanceId); Assert.AreEqual(CharacterTacticalPlayerState.ItemSchema, saved.Schema);
            Assert.AreEqual(101, saved.Items.Settlements.Single().RequestId);
            var ledger = new ItemAcquisitionLedger();
            Assert.IsTrue(new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(saved.Items.ToRuntimeState()).Success);
            Assert.IsTrue(ledger.Contains(new ItemAcquisitionRequestId(101)));
            Assert.IsEmpty(owner.Read("fixture.hero").Bindings, "Granting a card does not invent slot bindings.");
        }

        [TestCase(21, 0, ItemAcquisitionStatus.InsufficientCapacity)]
        [TestCase(0, 1, ItemAcquisitionStatus.InsufficientCost)]
        public async Task FailedCandidateRollsBackInventoryCardsLedgerAndCounters(int count, int cost, ItemAcquisitionStatus expected)
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); string before = store.Json; long revision = owner.Revision;
            Assert.AreEqual(expected, Execute(owner, Request(102, count, cost), true, 702).Status);
            Assert.AreEqual(before, store.Json); Assert.AreEqual(revision, owner.Revision);
            Assert.AreEqual(1, owner.Read("fixture.hero").Cards.Count); Assert.AreEqual(0, Count(owner));
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(102), true, 702).Status);
            Assert.AreEqual(2UL, owner.Read("fixture.hero").Cards.Last().InstanceId, "Failed candidates never consume live card identities.");
        }

        [Test] public async Task UnknownExactCardRevisionCannotChargeOrCommitLedger()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(100, 5));
            string before = store.Json; var grants = Cards(); grants[0].Card.ContentHash = "unpublished";
            var result = owner.ExecuteItemTransactionAtTick(Request(103, 0, 2), grants, 703, 11,
                new RuntimeTickContext(11, 0, 0, RuntimeTickStage.Simulation), out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
            Assert.AreEqual(before, store.Json); Assert.AreEqual(5, Count(owner));
        }

        [Test] public async Task CardOnlyPurchaseUsesOneLedgerAndKeepsOrdinaryInventorySeparate()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(100, 5));
            Assert.AreEqual(ItemAcquisitionStatus.EmptyTransaction, Execute(owner, Request(104, 0, 2)).Status);
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(104, 0, 2), true, 704).Status);
            Assert.AreEqual(3, Count(owner)); Assert.AreEqual(2, owner.Read("fixture.hero").Cards.Count);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(owner, Request(104, 0, 2), true, 704).Status);
            Assert.AreEqual(3, Count(owner));
        }

        [Test] public async Task SettlementAndRequestRetriesStayIdempotentAcrossReopen()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(105), true, 705);
            await owner.FlushAsync(); string committed = store.Json; owner = await Open(store); Configure(owner);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(owner, Request(105), true, 705, 11).Status);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(owner, Request(106), true, 705, 11).Status);
            Assert.AreEqual(committed, store.Json); Assert.AreEqual(1, Count(owner)); Assert.AreEqual(2, owner.Read("fixture.hero").Cards.Count);
        }

        [Test] public async Task WrongStageWrongFrameAndReorderedFrameDoNotPublish()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); string before = store.Json;
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, owner.ExecuteItemTransactionAtTick(Request(107), null, 0, 10,
                new RuntimeTickContext(9, 0, 0, RuntimeTickStage.Simulation), out _).Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, owner.ExecuteItemTransactionAtTick(Request(107), null, 0, 10,
                new RuntimeTickContext(10, 0, 0, RuntimeTickStage.PostSimulation), out _).Status);
            Assert.AreEqual(before, store.Json); Execute(owner, Request(107), frame: 10);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Execute(owner, Request(108), frame: 9).Status);
            Assert.AreEqual(1, Count(owner));
        }

        [Test] public async Task SaveFailureKeepsCompleteDirtyAuthorityAndRetryDoesNotDuplicate()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); string v2 = store.Json; store.Fail = true;
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(109), true, 709).Status);
            Assert.IsFalse(await owner.FlushAsync()); Assert.IsTrue(owner.HasUnsavedChanges); Assert.IsNotEmpty(owner.SaveIssue);
            Assert.AreEqual(v2, store.Json); Assert.AreEqual(1, Count(owner)); Assert.AreEqual(2, owner.Read("fixture.hero").Cards.Count);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(owner, Request(109), true, 709).Status);
            store.Fail = false; Assert.IsTrue(await owner.FlushAsync()); owner = await Open(store); Configure(owner);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(owner, Request(109), true, 709).Status);
            Assert.AreEqual(1, Count(owner)); Assert.AreEqual(2, owner.Read("fixture.hero").Cards.Count);
        }

        [Test] public async Task RestartBeforeFailedCheckpointRetriesWholeTransactionOnce()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); store.Fail = true;
            Execute(owner, Request(110), true, 710); Assert.IsFalse(await owner.FlushAsync());
            store.Fail = false; owner = await Open(store); Configure(owner);
            Assert.AreEqual(0, Count(owner)); Assert.AreEqual(1, owner.Read("fixture.hero").Cards.Count);
            Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(owner, Request(110), true, 710).Status);
            Assert.AreEqual(1, Count(owner)); Assert.AreEqual(2, owner.Read("fixture.hero").Cards.Count);
        }

        [Test] public async Task AsyncCompletionOnlyPersistsSnapshotsAndCannotReorderTickGrants()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); int baselineWrites = store.Writes.Count;
            store.Gate = new TaskCompletionSource<bool>();
            Execute(owner, Request(111), true, 711, 10); Execute(owner, Request(112), true, 712, 11);
            Assert.AreEqual(2, Count(owner)); Assert.AreEqual(3, owner.Read("fixture.hero").Cards.Count);
            Assert.AreEqual(baselineWrites, store.Writes.Count); Assert.IsTrue(owner.HasUnsavedChanges);
            store.Gate.SetResult(true); Assert.IsTrue(await owner.FlushAsync());
            Assert.AreEqual(2, store.Writes.Count - baselineWrites);
            var first = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Writes[baselineWrites]);
            var second = Saved(store);
            Assert.AreEqual(1, first.Items.Settlements.Count); Assert.AreEqual(2, first.Characters.Single().Cards.Count);
            Assert.AreEqual(2, second.Items.Settlements.Count); Assert.AreEqual(3, second.Characters.Single().Cards.Count);
            Assert.IsFalse(owner.HasUnsavedChanges);
        }

        [Test] public async Task MissingItemContentCannotDropSavedItemsOrRewriteV3()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(113));
            string before = store.Json; owner = await Open(store);
            Assert.IsFalse(owner.ConfigureItemInventory(new ItemDefinitionRegistry(), new[] {
                new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 1, "fixture.bag") }, out _));
            Assert.IsFalse(owner.TryReadItemInventory(out _, out _)); Assert.AreEqual(before, store.Json);
            Configure(owner); Assert.AreEqual(1, Count(owner));
        }

        [Test] public async Task InvalidSettlementCannotPublishOrRewriteLoadedCheckpoint()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Execute(owner, Request(114), settlement: 714);
            var state = Saved(store); state.Items.Settlements[0].RequestId = 999;
            store.Json = JsonConvert.SerializeObject(state); string invalid = store.Json;
            Assert.ThrowsAsync<JsonSerializationException>(() => Open(store)); Assert.AreEqual(invalid, store.Json);
        }

        [Test] public async Task RealFileSystemV2ToV3AndCASFailurePreserveWholeCheckpoint()
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxFramework.ItemOwnerFixtures"));
            string root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            try
            {
                var store = await CharacterTacticalSaveStore.CreateAsync(root); var first = await Open(store); Configure(first);
                string v2 = await (await CharacterTacticalSaveStore.CreateAsync(root)).LoadAsync();
                Assert.AreEqual("mx.character-test.tactical-player.v2", JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(v2).Schema);
                var staleStore = await CharacterTacticalSaveStore.CreateAsync(root); var stale = await Open(staleStore); Configure(stale);
                Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(first, Request(115), true, 715).Status);
                Assert.IsTrue(await first.FlushAsync());
                Assert.AreEqual(ItemAcquisitionStatus.Success, Execute(stale, Request(116), true, 716).Status);
                Assert.IsFalse(await stale.FlushAsync(), "Real CAS failure must leave only a complete older checkpoint durable.");
                Assert.IsTrue(stale.HasUnsavedChanges);
                var recoveredStore = await CharacterTacticalSaveStore.CreateAsync(root); var recovered = await Open(recoveredStore); Configure(recovered);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Execute(recovered, Request(115), true, 715).Status);
                Assert.AreEqual(1, Count(recovered)); Assert.AreEqual(2, recovered.Read("fixture.hero").Cards.Count);
                var saved = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(await recoveredStore.LoadAsync());
                Assert.AreEqual(715, saved.Items.Settlements.Single().SettlementId);
                Assert.AreEqual(115, saved.Items.Settlements.Single().RequestId);
            }
            finally
            {
                if (root.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }

    /// <summary>Opt-in role fixtures only. No live opening defaults, IDs, saves, scenes or AI Lab are changed.</summary>
    public sealed class CharacterTacticalCharacterItemTests
    {
        private sealed class Store : ICharacterTacticalStateStore
        {
            internal string Json;
            internal bool Fail;
            internal TaskCompletionSource<bool> Gate;
            internal List<string> Writes = new();
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public async Task SaveAsync(string json)
            {
                if (Fail) throw new IOException("fixture save failure");
                if (Gate != null) await Gate.Task;
                Json = json; Writes.Add(json);
            }
            public Task ResetAsync(string json) => SaveAsync(json);
        }
        private static Task<CharacterTacticalPlayerService> Open(ICharacterTacticalStateStore store)
            => CharacterTacticalPlayerService.OpenAsync(new CharacterTacticalContentCatalog(),
                new[] { new CharacterTacticalDefaults { CharacterStableId = "fixture.hero" } }, store);
        private static ItemDefinitionRegistry Definitions(int maxStack = 1)
        {
            var definitions = new ItemDefinitionRegistry();
            definitions.Register(ItemDefinitionTests.CreateDefinition(41001, "fixture.role-item", ItemKind.Custom, maxStack));
            definitions.Register(ItemDefinitionTests.CreateDefinition(41002, "fixture.material", ItemKind.Material, 20));
            return definitions;
        }
        private static InventoryContainer[] Layouts(int capacity = 4) => new[] {
            new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), capacity, "fixture.roles"),
            new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(2), capacity, "fixture.other") };
        private static CharacterTacticalCharacterItemDefinition[] Mapping(string template = "fixture.hero") => new[] {
            new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = template } };
        private static void Configure(CharacterTacticalPlayerService owner, int capacity = 4)
            => Assert.IsTrue(owner.ConfigureCharacterItemInventory(Definitions(), Layouts(capacity), Mapping(), out string issue), issue);
        private static RuntimeTickContext Tick(long frame) => new(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation);
        private static (ItemAcquisitionResult result, ItemInstanceId id, string issue) Acquire(CharacterTacticalPlayerService owner,
            long requestId, string name = "初始角色", long frame = 10, long? expectedRevision = null, int container = 1, int inventoryOwner = 1)
        {
            var result = owner.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(requestId),
                ItemAcquisitionSourceKind.DirectGrant, null, null), new InventoryContainerId(container), new InventoryOwnerId(inventoryOwner),
                new ItemDefinitionId(41001), name, expectedRevision ?? owner.Revision, frame, Tick(frame), out var id, out var issue);
            return (result, id, issue);
        }
        private static CharacterTacticalCharacterItemState Read(CharacterTacticalPlayerService owner, ItemInstanceId id, int inventoryOwner = 1)
        {
            Assert.IsTrue(owner.TryReadCharacterItem(id, new InventoryOwnerId(inventoryOwner), out var value, out string issue), issue);
            return value;
        }
        private static bool Rename(CharacterTacticalPlayerService owner, ItemInstanceId id, string name, long characterRevision = 1,
            long frame = 11, long? expectedRevision = null, int inventoryOwner = 1) => owner.TryRenameCharacterItemAtTick(id,
                new InventoryOwnerId(inventoryOwner), name, characterRevision, expectedRevision ?? owner.Revision, frame, Tick(frame), out _);
        private static CharacterTacticalPlayerState State(CharacterTacticalPlayerService owner)
            => JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(owner.CaptureProgressJson());
        private static InventorySaveState Inventory(CharacterTacticalPlayerState state)
            => JsonConvert.DeserializeObject<InventorySaveState>(state.Items.Modules.Single(
                value => value.ModuleId == InventorySaveStateProvider.ModuleId).CustomState.PayloadJson);

        [Test] public async Task ConfigurationAndTemplateDefaultsGrantNoRoleOwnershipOrRewriteV2()
        {
            var store = new Store(); var owner = await Open(store); string before = store.Json; long revision = owner.Revision;
            Configure(owner);
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(before, store.Json);
            Assert.AreEqual(revision, owner.Revision); Assert.IsNull(State(owner).CharacterItems);
            Assert.IsFalse(owner.TryReadCharacterItem(new ItemInstanceId(1), new InventoryOwnerId(1), out _, out _));
            Assert.IsTrue(owner.TryReadItemInventory(out var inventory, out _));
            Assert.IsTrue(inventory.CreateContainerSnapshot().All(container => Enumerable.Range(0, container.SlotCount).All(slot => container.GetStack(slot).IsEmpty)));
        }

        [Test] public async Task SameTemplateInstancesRenameIndependentlyAndReturnDetachedRecords()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner);
            var a = Acquire(owner, 1, "甲"); var b = Acquire(owner, 2, "乙");
            Assert.IsTrue(a.result.Success, a.issue); Assert.IsTrue(b.result.Success, b.issue);
            Assert.IsTrue(a.id.IsValid); Assert.AreNotEqual(a.id, b.id);
            Assert.IsTrue(Rename(owner, a.id, "甲改名")); Assert.AreEqual("乙", Read(owner, b.id).DisplayName);
            Assert.IsTrue(Rename(owner, b.id, "乙改名")); Assert.AreEqual("甲改名", Read(owner, a.id).DisplayName);
            var detached = Read(owner, a.id); detached.DisplayName = "外部改动"; detached.Revision = 100;
            Assert.AreEqual("甲改名", Read(owner, a.id).DisplayName); Assert.AreEqual(2, Read(owner, a.id).Revision);
            Assert.AreEqual("fixture.hero", Read(owner, b.id).CharacterStableId);
            var state = State(owner); Assert.AreEqual(CharacterTacticalPlayerState.CharacterTacticalSchema, state.Schema);
            var slots = Inventory(state).Containers.SelectMany(container => container.Slots).ToArray();
            Assert.AreEqual(2, slots.Length); Assert.IsTrue(slots.All(slot => slot.Count == 1 && slot.ItemInstanceId > 0));
            Assert.AreEqual(1, state.Characters.Count, "The old template-keyed owner is not converted or duplicated in this step.");
        }

        [Test] public async Task ReopenRetainsIdentityRevisionsAllocatorAndRetryAfterRename()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner);
            var a = Acquire(owner, 3, "原始甲"); var b = Acquire(owner, 4, "原始乙");
            Assert.IsTrue(Rename(owner, a.id, "持久甲")); Assert.IsTrue(Rename(owner, b.id, "持久乙"));
            Assert.IsTrue(await owner.FlushAsync()); string before = store.Json;
            owner = await Open(store); Configure(owner); Assert.AreEqual(before, store.Json);
            Assert.AreEqual("持久甲", Read(owner, a.id).DisplayName); Assert.AreEqual(2, Read(owner, b.id).Revision);
            var repeated = Acquire(owner, 3, "原始甲", frame: 0, expectedRevision: -1);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, repeated.result.Status); Assert.AreEqual(a.id, repeated.id);
            Assert.AreEqual(before, owner.CaptureProgressJson());
            var c = Acquire(owner, 5, frame: 12); Assert.IsTrue(c.result.Success); Assert.Greater(c.id.Value, b.id.Value);
        }

        [Test] public async Task DuplicateRequestPayloadConflictAndOtherLedgerUseDoNotPublish()
        {
            var owner = await Open(new Store()); Configure(owner); var first = Acquire(owner, 6, "甲");
            string before = owner.CaptureProgressJson(); long revision = owner.Revision;
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Acquire(owner, 6, "不同载荷").result.Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Acquire(owner, 6, "甲", container: 2, inventoryOwner: 2).result.Status);
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(revision, owner.Revision);
            var ordinary = owner.ExecuteItemTransactionAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(7),
                ItemAcquisitionSourceKind.DirectGrant, null, new[] { new ItemGrantLine(new InventoryContainerId(1), new ItemDefinitionId(41002), 1) }),
                null, 0, 10, Tick(10), out _);
            Assert.IsTrue(ordinary.Success); before = owner.CaptureProgressJson();
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Acquire(owner, 7).result.Status);
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(first.id.Value, State(owner).CharacterItems.Single().ItemInstanceId);
        }

        [Test] public async Task OrdinaryGrantCannotCreateRoleWithoutInstanceIdentity()
        {
            var owner = await Open(new Store()); Configure(owner); string before = owner.CaptureProgressJson();
            var result = owner.ExecuteItemTransactionAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(1),
                ItemAcquisitionSourceKind.DirectGrant, null, new[] { new ItemGrantLine(new InventoryContainerId(1), new ItemDefinitionId(41001), 1) }),
                null, 0, 10, Tick(10), out _);
            Assert.IsFalse(result.Success, "Role acquisition must allocate an Items instance and independent character record together.");
            Assert.AreEqual(before, owner.CaptureProgressJson());
            var a = Acquire(owner, 1); Assert.IsTrue(a.result.Success);
            before = owner.CaptureProgressJson();
            result = owner.ExecuteItemTransactionAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(2),
                ItemAcquisitionSourceKind.ShopPurchase, new[] { new ItemCostLine(new InventoryContainerId(1), new ItemDefinitionId(41001), 1) },
                new[] { new ItemGrantLine(new InventoryContainerId(1), new ItemDefinitionId(41002), 1) }), null, 0, 10, Tick(10), out _);
            Assert.IsFalse(result.Success); Assert.AreEqual(before, owner.CaptureProgressJson());
        }

        [Test] public async Task CapacityFailurePublishesNoRecordLedgerOrAllocatorAndSameRequestCanRetry()
        {
            var owner = await Open(new Store()); Configure(owner, 1); Assert.IsTrue(Acquire(owner, 8).result.Success);
            string before = owner.CaptureProgressJson(); long revision = owner.Revision;
            Assert.AreEqual(ItemAcquisitionStatus.InsufficientCapacity, Acquire(owner, 9).result.Status);
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(revision, owner.Revision);
            var next = Acquire(owner, 9, container: 2, inventoryOwner: 2);
            Assert.IsTrue(next.result.Success); Assert.AreEqual(2, next.id.Value);
            Assert.AreEqual(2, State(owner).CharacterItems.Count);
        }

        [Test] public async Task WrongOwnerStaleRevisionsAndFramesRejectBeforeAnyPublication()
        {
            var owner = await Open(new Store()); Configure(owner); var a = Acquire(owner, 10);
            string before = owner.CaptureProgressJson(); long revision = owner.Revision;
            Assert.IsFalse(owner.TryReadCharacterItem(a.id, new InventoryOwnerId(2), out _, out _));
            Assert.IsFalse(Rename(owner, a.id, "bad", inventoryOwner: 2));
            Assert.IsFalse(Rename(owner, a.id, "bad", characterRevision: 2));
            Assert.IsFalse(Rename(owner, a.id, "bad", expectedRevision: revision - 1));
            Assert.IsFalse(Rename(owner, a.id, "bad", frame: 9));
            Assert.AreEqual(ItemAcquisitionStatus.InvalidContainer, Acquire(owner, 11, inventoryOwner: 2).result.Status);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Acquire(owner, 11, expectedRevision: revision - 1).result.Status);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Acquire(owner, 11, frame: 9).result.Status);
            Assert.IsFalse(owner.TryRenameCharacterItemAtTick(a.id, new InventoryOwnerId(1), "bad", 1, revision, 11,
                new RuntimeTickContext(11, 1d / 60d, 11d / 60d, RuntimeTickStage.PostSimulation), out _));
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(revision, owner.Revision);
        }

        [Test] public async Task InvalidDefinitionOrMissingSavedMappingCannotRewriteCheckpoint()
        {
            var store = new Store(); var owner = await Open(store); string before = store.Json;
            Assert.IsFalse(owner.ConfigureCharacterItemInventory(Definitions(2), Layouts(), Mapping(), out _));
            Assert.IsFalse(owner.ConfigureCharacterItemInventory(Definitions(), Layouts(), Mapping("unknown.template"), out _));
            Assert.AreEqual(before, store.Json); Configure(owner); Acquire(owner, 12);
            Assert.IsTrue(await owner.FlushAsync()); before = store.Json; owner = await Open(store);
            Assert.IsFalse(owner.ConfigureItemInventory(Definitions(), Layouts(), out _));
            Assert.IsFalse(owner.ConfigureCharacterItemInventory(Definitions(), Layouts(), Mapping("unknown.template"), out _));
            Assert.AreEqual(before, store.Json); Configure(owner);
            Assert.AreEqual(1, State(owner).CharacterItems.Count);
        }

        [Test] public async Task BrokenRecordInventoryOrLedgerJoinsCannotLoadOrPartiallyRepair()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); Acquire(owner, 13); await owner.FlushAsync();
            string valid = store.Json;
            Action<CharacterTacticalPlayerState>[] corruptions = {
                state => state.CharacterItems[0].ItemInstanceId = 999,
                state => state.CharacterItems[0].AcquisitionRequestId = 999,
                state => state.CharacterItems.Add(JsonConvert.DeserializeObject<CharacterTacticalCharacterItemState>(JsonConvert.SerializeObject(state.CharacterItems[0]))),
                state => state.CharacterItems[0].Revision = 0,
                state => state.Schema = CharacterTacticalPlayerState.ItemSchema };
            foreach (var corrupt in corruptions)
            {
                var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(valid); corrupt(state);
                store.Json = JsonConvert.SerializeObject(state); string invalid = store.Json; int writes = store.Writes.Count;
                Assert.ThrowsAsync<JsonSerializationException>(() => Open(store));
                Assert.AreEqual(invalid, store.Json); Assert.AreEqual(writes, store.Writes.Count);
            }
        }

        [Test] public async Task SaveFailureRetryCapturesLatestBothInstancesWithoutRepeatingAcquisition()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner);
            var a = Acquire(owner, 14, "甲"); var b = Acquire(owner, 15, "乙"); await owner.FlushAsync(); string durable = store.Json;
            store.Fail = true; Assert.IsTrue(Rename(owner, a.id, "甲修改")); Assert.IsFalse(await owner.FlushAsync());
            Assert.IsTrue(Rename(owner, b.id, "乙修改")); Assert.AreEqual(durable, store.Json); Assert.IsTrue(owner.HasUnsavedChanges);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Acquire(owner, 14, "甲").result.Status);
            store.Fail = false; Assert.IsTrue(await owner.FlushAsync());
            owner = await Open(store); Configure(owner);
            Assert.AreEqual("甲修改", Read(owner, a.id).DisplayName); Assert.AreEqual("乙修改", Read(owner, b.id).DisplayName);
            Assert.AreEqual(2, State(owner).CharacterItems.Count); Assert.IsFalse(owner.HasUnsavedChanges);
        }

        [Test] public async Task RestartBeforeFailedSaveRetriesWholeAcquisitionOnce()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); string before = store.Json;
            store.Fail = true; var a = Acquire(owner, 16); Assert.IsTrue(a.result.Success); Assert.IsFalse(await owner.FlushAsync());
            Assert.AreEqual(before, store.Json); store.Fail = false;
            owner = await Open(store); Configure(owner); var retry = Acquire(owner, 16);
            Assert.IsTrue(retry.result.Success); Assert.AreEqual(a.id, retry.id); Assert.IsTrue(await owner.FlushAsync());
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Acquire(owner, 16).result.Status);
            Assert.AreEqual(1, State(owner).CharacterItems.Count);
        }

        [Test] public async Task AsyncSaveCompletionRetainsLatestIndependentEdits()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner);
            var a = Acquire(owner, 17, "甲"); var b = Acquire(owner, 18, "乙"); await owner.FlushAsync(); int writes = store.Writes.Count;
            store.Gate = new TaskCompletionSource<bool>();
            Assert.IsTrue(Rename(owner, a.id, "甲修改")); Assert.IsTrue(Rename(owner, b.id, "乙修改"));
            Assert.AreEqual(writes, store.Writes.Count); store.Gate.SetResult(true); Assert.IsTrue(await owner.FlushAsync());
            Assert.AreEqual(2, store.Writes.Count - writes);
            var saved = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Json);
            Assert.AreEqual("甲修改", saved.CharacterItems.Single(value => value.ItemInstanceId == a.id.Value).DisplayName);
            Assert.AreEqual("乙修改", saved.CharacterItems.Single(value => value.ItemInstanceId == b.id.Value).DisplayName);
        }

        [Test] public async Task WrongAcquisitionStageFrameAndNonDirectGrantLeaveAllAuthorityUntouched()
        {
            var owner = await Open(new Store()); Configure(owner); string before = owner.CaptureProgressJson();
            foreach (var source in new[] { ItemAcquisitionSourceKind.DirectGrant, ItemAcquisitionSourceKind.ShopPurchase })
            {
                var request = new ItemAcquisitionRequest(new ItemAcquisitionRequestId(21), source, null, null);
                var result = owner.AcquireCharacterItemAtTick(request, new InventoryContainerId(1), new InventoryOwnerId(1),
                    new ItemDefinitionId(41001), "角色", owner.Revision, 10,
                    new RuntimeTickContext(9, 1d / 60d, 9d / 60d, RuntimeTickStage.Simulation), out _, out _);
                Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
                result = owner.AcquireCharacterItemAtTick(request, new InventoryContainerId(1), new InventoryOwnerId(1),
                    new ItemDefinitionId(41001), "角色", owner.Revision, 10,
                    new RuntimeTickContext(10, 1d / 60d, 10d / 60d, RuntimeTickStage.PostSimulation), out _, out _);
                Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
            }
            var invalid = new ItemAcquisitionRequest(new ItemAcquisitionRequestId(21), ItemAcquisitionSourceKind.ShopPurchase, null, null);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, owner.AcquireCharacterItemAtTick(invalid,
                new InventoryContainerId(1), new InventoryOwnerId(1), new ItemDefinitionId(41001), "角色", owner.Revision,
                10, Tick(10), out _, out _).Status);
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.IsNull(State(owner).Items);
        }

        [Test] public async Task ExhaustedAllocatorAndCharacterRevisionOverflowCannotPartiallyPublish()
        {
            var store = new Store(); var owner = await Open(store); Configure(owner); var a = Acquire(owner, 22); await owner.FlushAsync();
            var state = State(owner); var inventory = Inventory(state); inventory.NextItemInstanceId = (long)int.MaxValue + 1;
            var module = state.Items.Modules.Single(value => value.ModuleId == InventorySaveStateProvider.ModuleId);
            state.Items.Modules = state.Items.Modules.Select(value => value == module
                ? new RuntimeModuleSaveState(module.ModuleId, module.SchemaVersion,
                    new RuntimeCustomState(module.CustomState.NumericTypeId, module.CustomState.TypeId, module.CustomState.SchemaVersion, JsonConvert.SerializeObject(inventory)))
                : value).ToArray();
            state.CharacterItems[0].Revision = long.MaxValue;
            store.Json = JsonConvert.SerializeObject(state); owner = await Open(store); Configure(owner);
            string before = owner.CaptureProgressJson(); long revision = owner.Revision;
            Assert.AreEqual(ItemAcquisitionStatus.CommitFailed, Acquire(owner, 23).result.Status);
            Assert.IsFalse(Rename(owner, a.id, "overflow", characterRevision: long.MaxValue));
            Assert.AreEqual(before, owner.CaptureProgressJson()); Assert.AreEqual(revision, owner.Revision); Assert.AreEqual(before, store.Json);
        }

        [Test] public async Task RealAggregateEnvelopeReopenAndCASConflictKeepWholeDurableCheckpoint()
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxFramework.CharacterItemFixtures"));
            string root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            const string preparation = "{\"fixture\":\"preparation-kept\"}";
            try
            {
                async Task<(CharacterTacticalPlayerService owner, CharacterTacticalProgressCandidate candidate, CharacterTacticalSaveStore physical)> Session(bool create)
                {
                    var physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
                    var progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
                    var candidate = create ? progress.Create() : progress.OpenActive();
                    var owner = await Open(candidate.PlayerStore); Configure(owner);
                    candidate.Bind(owner.CaptureProgressJson, () => preparation);
                    if (create) await candidate.SaveAsync(activate: true); else candidate.EnableAutosave();
                    return (owner, candidate, physical);
                }
                var first = await Session(true); var a = Acquire(first.owner, 19, "甲"); var b = Acquire(first.owner, 20, "乙");
                Assert.IsTrue(a.result.Success); Assert.IsTrue(b.result.Success);
                Assert.IsTrue(await first.owner.FlushAsync()); Assert.IsTrue(Rename(first.owner, a.id, "持久甲"));
                Assert.IsTrue(await first.owner.FlushAsync());
                var stale = await Session(false); var reopened = await Session(false);
                Assert.AreEqual("持久甲", Read(reopened.owner, a.id).DisplayName); Assert.AreEqual("乙", Read(reopened.owner, b.id).DisplayName);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Acquire(reopened.owner, 19, "甲").result.Status);
                Assert.IsTrue(Rename(reopened.owner, b.id, "胜出乙", frame: 12)); Assert.IsTrue(await reopened.owner.FlushAsync());
                Assert.IsTrue(Rename(stale.owner, b.id, "过时乙", frame: 12));
                Assert.IsFalse(await stale.owner.FlushAsync(), "Physical CAS must reject the stale whole-profile writer.");
                Assert.IsTrue(stale.owner.HasUnsavedChanges);
                var recovered = await Session(false); Assert.AreEqual("持久甲", Read(recovered.owner, a.id).DisplayName);
                Assert.AreEqual("胜出乙", Read(recovered.owner, b.id).DisplayName); Assert.AreEqual(2, State(recovered.owner).CharacterItems.Count);
                var document = JsonConvert.DeserializeObject<CharacterTacticalProgressDocument>(await recovered.physical.LoadAsync());
                Assert.AreEqual(preparation, document.Profiles.Single().PreparationJson);
                Assert.AreEqual(2, JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(document.Profiles.Single().PlayerJson).CharacterItems.Count);
            }
            finally
            {
                if (root.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    /// <summary>Explicit instance actions/preparation fixtures. No live defaults, item IDs, save roots or scenes.</summary>
    public sealed class CharacterTacticalCharacterActionInstanceTests
    {
        private sealed class Store : ICharacterTacticalStateStore
        {
            internal string Json;
            internal bool Fail;
            internal TaskCompletionSource<bool> Gate;
            internal readonly List<string> Writes = new();
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public async Task SaveAsync(string json)
            { if (Fail) throw new IOException("fixture save failure"); if (Gate != null) await Gate.Task; Json = json; Writes.Add(json); }
            public Task ResetAsync(string json) => SaveAsync(json);
        }
        private static readonly InventoryOwnerId Owner = new(1), Other = new(2);
        private static CharacterTacticalContentCatalog Catalog() => new()
        {
            Actions = new[] {
                new CharacterTacticalActionDefinition { Id = "action:fixture.first", ActionId = 301, ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } },
                new CharacterTacticalActionDefinition { Id = "action:fixture.second", ActionId = 302, ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } },
                new CharacterTacticalActionDefinition { Id = "ta.fixture.second", ActionId = 302, ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } },
                new CharacterTacticalActionDefinition { Id = "action:fixture.foreign", ActionId = 303, ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.other" } } },
            Cards = new[] { new CharacterTacticalCardDefinition { CardId = "fixture.card", Revision = 1, ContentHash = "fixture-card-v1", DisplayName = "Fixture Card", Slots = new[] { "behavior.action", "behavior.extra" } } },
            Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = "fixture.hero", ActionIds = new[] { "action:fixture.first", "action:fixture.second", "action:fixture.foreign" } } }
        };
        private static CharacterTacticalDefaults Defaults(bool alternate = false) => new()
        {
            CharacterStableId = "fixture.hero", ActionIds = new List<string> { alternate ? "action:fixture.second" : "action:fixture.first" },
            Cards = new List<CharacterTacticalCardReference> {
                new() { InstanceId = 1, CardId = "fixture.card", Revision = 1, ContentHash = "fixture-card-v1" },
                new() { InstanceId = 2, CardId = "fixture.card", Revision = 1, ContentHash = "fixture-card-v1" } },
            Bindings = new List<CharacterTacticalSlotBinding> {
                new() { CardInstanceId = 1, CardId = "fixture.card", SlotPath = "behavior.action", TacticalActionId = alternate ? "action:fixture.second" : "action:fixture.first" },
                new() { CardInstanceId = 1, CardId = "fixture.card", SlotPath = "behavior.extra", TacticalActionId = alternate ? "action:fixture.second" : "action:fixture.first" },
                new() { CardInstanceId = 2, CardId = "fixture.card", SlotPath = "behavior.action", TacticalActionId = alternate ? "action:fixture.second" : "action:fixture.first" },
                new() { CardInstanceId = 2, CardId = "fixture.card", SlotPath = "behavior.extra", TacticalActionId = alternate ? "action:fixture.second" : "action:fixture.first" } }
        };
        private static Task<CharacterTacticalPlayerService> Open(ICharacterTacticalStateStore store,
            CharacterTacticalContentCatalog catalog = null, CharacterTacticalDefaults defaults = null)
            => CharacterTacticalPlayerService.OpenAsync(catalog ?? Catalog(), new[] { defaults ?? Defaults() }, store);
        private static void Configure(CharacterTacticalPlayerService player)
        {
            var definitions = new ItemDefinitionRegistry();
            definitions.Register(ItemDefinitionTests.CreateDefinition(41001, "fixture.role-item", ItemKind.Custom, 1));
            Assert.IsTrue(player.ConfigureCharacterItemInventory(definitions, new[] {
                new InventoryContainer(new InventoryContainerId(1), Owner, 8, "fixture.roles"),
                new InventoryContainer(new InventoryContainerId(2), Other, 8, "fixture.other") }, new[] {
                new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = "fixture.hero" } }, out string issue), issue);
        }
        private static RuntimeTickContext Tick(long frame = 10) => new(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation);
        private static ItemInstanceId Acquire(CharacterTacticalPlayerService player, long request, string name)
        {
            var result = player.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(request),
                ItemAcquisitionSourceKind.DirectGrant, null, null), new InventoryContainerId(1), Owner, new ItemDefinitionId(41001),
                name, player.Revision, 10, Tick(), out var id, out string issue);
            Assert.IsTrue(result.Success, issue); return id;
        }
        private static CharacterTacticalCharacterItemState Read(CharacterTacticalPlayerService player, ItemInstanceId id)
        { Assert.IsTrue(player.TryReadCharacterItem(id, Owner, out var value, out string issue), issue); return value; }
        private static CharacterTacticalPlayerState State(CharacterTacticalPlayerService player)
            => JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson());
        private static ItemAcquisitionResult Learn(CharacterTacticalPlayerService player, ItemInstanceId id, long request = 100,
            string action = "action:fixture.second", InventoryOwnerId? owner = null, long? expectedCharacter = null, long? expectedProgress = null,
            long frame = 10, RuntimeTickContext? tick = null)
            => player.LearnCharacterActionAtTick(new ItemAcquisitionRequestId(request), id, owner ?? Owner, action,
                expectedCharacter ?? Read(player, id).Revision, expectedProgress ?? player.Revision, frame, tick ?? Tick(frame), out _);
        private static bool Bind(CharacterTacticalPlayerService player, ItemInstanceId id, ulong card, string action = "action:fixture.second",
            string slot = "behavior.action", InventoryOwnerId? owner = null, long? expectedCharacter = null, long? expectedProgress = null,
            long frame = 10, RuntimeTickContext? tick = null)
            => player.BindCharacterActionAtTick(id, owner ?? Owner, card, slot, action,
                expectedCharacter ?? Read(player, id).Revision, expectedProgress ?? player.Revision, frame, tick ?? Tick(frame), out _);
        private static bool Unbind(CharacterTacticalPlayerService player, ItemInstanceId id, ulong card, string slot = "behavior.action")
            => player.UnbindCharacterActionAtTick(id, Owner, card, slot, Read(player, id).Revision, player.Revision, 10, Tick(), out _);
        private static Task<TacticalBattlePreparationService> Prepare(CharacterTacticalPlayerService player, ICharacterTacticalStateStore store)
            => TacticalBattlePreparationService.OpenAsync(player, store, new[] {
                new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition(1, "Fixture", default, 1, default, default, tacticalDefaults: Defaults()) });
        private static bool Deploy(CharacterTacticalPlayerService player, TacticalBattleSetup setup,
            out TacticalDeployment deployment, out string issue)
            => TacticalDeployment.TryCreate(player, setup, out deployment, out issue, new[] {
                new MxFramework.Game.CharacterTest.CharacterTestCharacterDefinition(1, "Fixture", default, 1, default, default, tacticalDefaults: Defaults()) });
        private static string Add(TacticalBattlePreparationService prep, ItemInstanceId id, int team = 1)
        {
            Assert.IsTrue(prep.AddCharacterItemEntity(id, Owner, new UnityEngine.Vector3(team == 1 ? -2f : 2f, 1f, 0f),
                0f, team, prep.Revision, out string entityId, out string issue), issue); return entityId;
        }

        [Test] public async Task InventoryChoicesUseExplicitOwnerAndNeverTemplateDefaults()
        {
            var player = await Open(new Store()); Configure(player);
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var empty, out var issue), issue);
            Assert.AreEqual(0, empty.Length); Assert.IsFalse(player.HasCharacterItemProgress);
            var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(player.HasCharacterItemProgress);
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var owned, out issue), issue);
            CollectionAssert.AreEqual(new[] { a.Value, b.Value }, owned.Select(value => value.ItemInstanceId));
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Other, out empty, out issue), issue);
            Assert.AreEqual(0, empty.Length);
            Assert.IsFalse(player.TryReadOwnedCharacterItems(default, out empty, out issue));
            Assert.IsNotEmpty(issue); Assert.AreEqual(0, empty.Length);
        }

        [Test] public async Task InventoryChoicesAreDetachedFromAuthority()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A");
            string before = player.CaptureProgressJson();
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var choices, out _));
            choices[0].DisplayName = "wrong"; choices[0].ActionGrants.Clear(); choices[0].Cards.Clear();
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual("A", Read(player, a).DisplayName);
        }

        [Test] public async Task FindProjectsCurrentInstanceCardsActionsAndBindingsWithoutSavingThemInPreparation()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var prep = await Prepare(player, new Store()); string entityA = Add(prep, a), entityB = Add(prep, b, 2);
            string before = prep.CaptureProgressJson();
            Assert.IsTrue(Learn(player, a).Success); var record = Read(player, a);
            Assert.IsTrue(Bind(player, a, record.Cards[0].InstanceId));
            var displayA = prep.Find(entityA); var displayB = prep.Find(entityB);
            Assert.AreEqual("action:fixture.second", displayA.Loadout.Bindings[0].TacticalActionId);
            Assert.AreEqual("action:fixture.first", displayB.Loadout.Bindings[0].TacticalActionId);
            Assert.AreEqual(before, prep.CaptureProgressJson());
            displayA.Loadout.Cards.Clear(); displayA.Loadout.Bindings.Clear(); displayA.DisplayName = "wrong";
            Assert.AreEqual(record.Cards.Count, prep.Find(entityA).Loadout.Cards.Count);
            Assert.AreEqual("A", prep.Find(entityA).DisplayName); Assert.AreEqual(before, prep.CaptureProgressJson());
        }

        [Test] public async Task PoseAndTeamMutationsUseStoredReferenceAfterDisplayProjection()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A");
            var prep = await Prepare(player, new Store()); string entity = Add(prep, a);
            Assert.IsNotNull(prep.Find(entity)); long revision = prep.Revision;
            Assert.IsTrue(prep.SetPose(entity, new UnityEngine.Vector3(-3f, 1f, 1f), 90f, out var issue), issue);
            Assert.IsTrue(prep.SetTeam(entity, 2, out issue), issue);
            Assert.AreEqual(-3f, prep.Find(entity).X); Assert.AreEqual(2, prep.Find(entity).TeamId);
            Assert.AreEqual(a.Value, prep.Find(entity).CharacterItemInstanceId); Assert.AreEqual(revision + 2, prep.Revision);
        }

        [Test] public async Task LegacyRoleCanBeListedButNeverReceivesTacticalDefaultsDuringDisplay()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A");
            var state = State(player); state.Schema = CharacterTacticalPlayerState.CharacterItemSchema;
            state.CharacterItems[0].TacticalSchema = null; state.CharacterItems[0].ActionGrants = null;
            state.CharacterItems[0].Cards = null; state.CharacterItems[0].Bindings = null;
            store.Json = JsonConvert.SerializeObject(state); string before = store.Json;
            player = await Open(store); Configure(player);
            Assert.IsTrue(player.HasCharacterItemProgress);
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var choices, out var issue), issue);
            Assert.AreEqual(a.Value, choices.Single().ItemInstanceId);
            Assert.IsFalse(player.TryReadCharacterItemLoadout(a, Owner, out _, out _, out issue));
            Assert.IsNotEmpty(issue); Assert.AreEqual(before, store.Json);
        }

        [Test] public async Task MissingItemDirectoryCannotProduceInventoryChoices()
        {
            var store = new Store(); var player = await Open(store); Configure(player); Acquire(player, 1, "A");
            Assert.IsTrue(await player.FlushAsync()); string before = store.Json;
            player = await Open(store);
            Assert.IsFalse(player.TryReadOwnedCharacterItems(Owner, out var choices, out var issue));
            Assert.AreEqual(0, choices.Length); Assert.IsNotEmpty(issue); Assert.AreEqual(before, store.Json);
        }

        [Test] public async Task SameTemplateItemsOwnIndependentInitialCardsAndPermanentActions()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var beforeB = JsonConvert.SerializeObject(Read(player, b)); var beforeBindings = JsonConvert.SerializeObject(Read(player, a).Bindings);
            Assert.IsTrue(Learn(player, a).Success); var roleA = Read(player, a);
            CollectionAssert.AreEquivalent(new[] { 301, 302 }, roleA.ActionGrants.Select(value => value.ActionId));
            Assert.AreEqual(beforeBindings, JsonConvert.SerializeObject(roleA.Bindings));
            Assert.AreEqual(beforeB, JsonConvert.SerializeObject(Read(player, b)));
            Assert.IsFalse(roleA.Cards.Select(value => value.InstanceId).Intersect(Read(player, b).Cards.Select(value => value.InstanceId)).Any());
            Assert.IsNull(player.Read("fixture.hero")); Assert.IsFalse(player.TryGrantAction("fixture.hero", "action:fixture.second", out _));
        }

        [Test] public async Task LearnBindUnbindAndReopenKeepBothInstancesIndependent()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            ulong card = Read(player, a).Cards[0].InstanceId; Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, card));
            Assert.IsTrue(Unbind(player, a, card)); Assert.IsTrue(await player.FlushAsync());
            player = await Open(store, defaults: Defaults(alternate: true)); Configure(player);
            Assert.AreEqual(2, Read(player, a).ActionGrants.Count); Assert.AreEqual(1, Read(player, b).ActionGrants.Count);
            Assert.IsFalse(Read(player, a).Bindings.Any(value => value.CardInstanceId == card && value.SlotPath == "behavior.action"));
            Assert.AreEqual("action:fixture.first", Read(player, b).Bindings[0].TacticalActionId);
            Assert.IsFalse(player.TryGetCharacterItemBindings(a, Owner, card, out _, out string issue)); Assert.IsNotEmpty(issue);
            Assert.IsTrue(Bind(player, a, card)); Assert.IsTrue(player.TryGetCharacterItemBindings(a, Owner, card, out var slots, out issue), issue);
            Assert.AreEqual(302, slots["behavior.action"].ActionId); Assert.AreEqual(301, slots["behavior.extra"].ActionId);
        }

        [Test] public async Task LearningRetriesUseLedgerAndImmutablePayloadWithoutDuplicateOwnership()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); string before = player.CaptureProgressJson(); long revision = player.Revision;
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Learn(player, a, action: "ta.fixture.second", expectedCharacter: -1, expectedProgress: -1).Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, b).Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a, action: "action:fixture.first").Status);
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
            await player.FlushAsync(); player = await Open(store); Configure(player);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Learn(player, a).Status);
            Assert.IsTrue(Learn(player, a, 101, "ta.fixture.second").Success);
            Assert.IsTrue(player.TryReadCharacterItemLoadout(a, Owner, out var projection, out _, out _)); Assert.AreEqual(2, projection.ActionIds.Count);
        }

        [Test] public async Task BindingRejectsUnlearnedForeignCardsWrongSlotsOwnerAndStaleCommandsAtomically()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            ulong aCard = Read(player, a).Cards[0].InstanceId, bCard = Read(player, b).Cards[0].InstanceId; string before = player.CaptureProgressJson(); long revision = player.Revision;
            Assert.IsFalse(Bind(player, a, aCard)); Assert.IsFalse(Bind(player, a, bCard, "action:fixture.first"));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", "unknown.slot"));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", owner: Other));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", expectedCharacter: 0));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", expectedProgress: -1));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", frame: 9));
            Assert.IsFalse(Bind(player, a, aCard, "action:fixture.first", tick: new RuntimeTickContext(10, 0, 0, RuntimeTickStage.PostSimulation)));
            Assert.IsFalse(player.UnbindCharacterActionAtTick(a, Other, aCard, "behavior.action", 1, revision, 10, Tick(), out _));
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task LearningRejectsUnknownInapplicableOutOfPolicyAndStaleRequestsAtomically()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); string before = player.CaptureProgressJson();
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a, action: "missing").Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a, action: "action:fixture.foreign").Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a, owner: Other).Status);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Learn(player, a, expectedCharacter: 0).Status);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Learn(player, a, expectedProgress: -1).Status);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, Learn(player, a, frame: 9).Status);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a, tick: new RuntimeTickContext(11, 0, 0, RuntimeTickStage.Simulation)).Status);
            player.Catalog.Acquisition[0].ActionIds = new[] { "action:fixture.first" };
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a).Status);
            Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task ChangedActionEligibilityPreservesGrantButRejectsRebindAndDeployment()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, Read(player, a).Cards[0].InstanceId));
            var prep = await Prepare(player, new Store()); Add(prep, a); Add(prep, b, 2); var frozen = prep.Working; await player.FlushAsync();
            string durable = store.Json; var catalog = Catalog(); catalog.Actions[1].ActionLibraryId = "fixture.changed.library";
            player = await Open(store, catalog); Configure(player); Assert.AreEqual(durable, store.Json);
            Assert.IsFalse(Bind(player, a, Read(player, a).Cards[0].InstanceId));
            Assert.IsFalse(player.TryGetCharacterItemBindings(a, Owner, Read(player, a).Cards[0].InstanceId, out _, out _));
            Assert.IsFalse(Deploy(player, frozen, out _, out _)); Assert.AreEqual(durable, store.Json);
            Assert.AreEqual(2, Read(player, a).ActionGrants.Count);
        }

        [Test] public async Task InvalidInitialGrantCannotPublishItemLedgerOrConsumeCardAllocator()
        {
            var defaults = Defaults(); defaults.Bindings[0].SlotPath = "unknown.slot";
            var player = await Open(new Store(), defaults: defaults); Configure(player); string before = player.CaptureProgressJson();
            var result = player.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(1),
                ItemAcquisitionSourceKind.DirectGrant, null, null), new InventoryContainerId(1), Owner, new ItemDefinitionId(41001),
                "A", player.Revision, 10, Tick(), out var id, out _);
            Assert.AreEqual(ItemAcquisitionStatus.CommitFailed, result.Status); Assert.IsFalse(id.IsValid); Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task PreparationSavesReferencesAndDeploymentUsesSelectedAOrBConfiguration()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, Read(player, a).Cards[0].InstanceId));
            var store = new Store(); var prep = await Prepare(player, store); Assert.AreEqual(0, prep.Working.Entities.Count);
            string aEntity = Add(prep, a), bEntity = Add(prep, b, 2); Assert.IsTrue(prep.SetPlayerControlledEntity(aEntity, out _));
            Assert.IsTrue(prep.ValidateForBattle(out string issue), issue); string progressBefore = player.CaptureProgressJson();
            Assert.IsTrue(Deploy(player, prep.Working, out var deployment, out issue), issue);
            var partA = deployment.Participants.Single(value => value.EntityId == aEntity); var partB = deployment.Participants.Single(value => value.EntityId == bEntity);
            Assert.AreEqual(a.Value, partA.CharacterItemInstanceId); Assert.AreEqual(b.Value, partB.CharacterItemInstanceId);
            Assert.AreEqual(Owner.Value, partA.InventoryOwnerId); Assert.AreEqual(Read(player, a).Revision, partA.CharacterItemRevision);
            Assert.AreEqual(302, partA.Bindings[Read(player, a).Cards[0].InstanceId]["behavior.action"]);
            Assert.AreEqual(301, partB.Bindings[Read(player, b).Cards[0].InstanceId]["behavior.action"]);
            Assert.IsTrue(prep.SaveAsPreset("A/B", out var preset, out _)); Assert.IsTrue(await prep.FlushAsync());
            var saved = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json);
            Assert.IsTrue(saved.Working.Entities.All(value => value.Loadout.ActionIds.Count == 0 && value.Loadout.Cards.Count == 0 && value.Loadout.Bindings.Count == 0));
            prep = await Prepare(player, store); Assert.IsTrue(prep.LoadPreset(preset, out _));
            Assert.IsTrue(Deploy(player, prep.Working, out _, out issue), issue); Assert.AreEqual(progressBefore, player.CaptureProgressJson());
        }

        [Test] public async Task PreparationSelectionSwitchesOnePlaceToSpecificInstanceWithoutCopyingOwnership()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, Read(player, a).Cards[0].InstanceId));
            var prep = await Prepare(player, new Store()); string entity = Add(prep, a); string before = player.CaptureProgressJson();
            Assert.IsTrue(prep.SelectCharacterItem(entity, b, Owner, prep.Revision, out _));
            var selected = prep.Working.Entities.Single(); Assert.AreEqual(b.Value, selected.CharacterItemInstanceId); Assert.AreEqual("B", selected.DisplayName);
            Assert.AreEqual(301, player.Catalog.Action(selected.Loadout.Bindings[0].TacticalActionId).ActionId);
            Assert.IsTrue(prep.SelectCharacterItem(entity, a, Owner, prep.Revision, out _));
            Assert.AreEqual(302, player.Catalog.Action(prep.Working.Entities.Single().Loadout.Bindings[0].TacticalActionId).ActionId);
            Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task DuplicateWrongOwnerPoseAndStalePreparationRequestsLeaveStoredReferencesUntouched()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var prep = await Prepare(player, new Store()); string entity = Add(prep, a); string before = prep.CaptureProgressJson(); long revision = prep.Revision;
            Assert.IsFalse(prep.AddCharacterItemEntity(a, Owner, UnityEngine.Vector3.zero, 0, 2, revision, out _, out _));
            Assert.IsFalse(prep.AddCharacterItemEntity(b, Other, UnityEngine.Vector3.zero, 0, 2, revision, out _, out _));
            Assert.IsFalse(prep.AddCharacterItemEntity(b, Owner, new UnityEngine.Vector3(1000, 1, 0), 0, 2, revision, out _, out _));
            Assert.IsFalse(prep.AddCharacterItemEntity(b, Owner, UnityEngine.Vector3.zero, float.NaN, 2, revision, out _, out _));
            Assert.IsFalse(prep.SelectCharacterItem(entity, b, Owner, revision - 1, out _));
            Assert.IsFalse(prep.BindAction(entity, Read(player, a).Cards[0].InstanceId, "action:fixture.first", out _));
            Assert.IsFalse(prep.RemoveCard(entity, Read(player, a).Cards[0].InstanceId, out _));
            Assert.AreEqual(before, prep.CaptureProgressJson()); Assert.AreEqual(revision, prep.Revision);
        }

        [Test] public async Task ChangedInstanceRevisionAndTamperedSnapshotRequireFreshPreparation()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var prep = await Prepare(player, new Store()); Add(prep, a); Add(prep, b, 2); var frozen = prep.Working;
            Assert.IsTrue(Deploy(player, frozen, out _, out _)); Assert.IsTrue(Learn(player, a).Success);
            Assert.IsFalse(Deploy(player, frozen, out _, out string issue)); StringAssert.Contains("重新确认", issue);
            Assert.IsTrue(Deploy(player, prep.Working, out _, out issue), issue);
            Assert.AreEqual(1, Read(player, b).Revision);
            var tampered = prep.Working; tampered.Entities[0].Loadout.Cards[0].InstanceId = Read(player, b).Cards[0].InstanceId;
            Assert.IsFalse(Deploy(player, tampered, out _, out _));
            var duplicate = prep.Working; duplicate.Entities[1].CharacterItemInstanceId = a.Value;
            Assert.IsFalse(Deploy(player, duplicate, out _, out _));
            var legacy = prep.Working; legacy.Entities[0].CharacterItemInstanceId = null; legacy.Entities[0].InventoryOwnerId = null;
            Assert.IsFalse(Deploy(player, legacy, out _, out _));
        }

        [Test] public async Task UnboundSlotsStayUnboundThroughPreparationPresetAndReopen()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var prepStore = new Store(); var prep = await Prepare(player, prepStore); Add(prep, a); Add(prep, b, 2);
            Assert.IsTrue(prep.SaveAsPreset("AB", out var preset, out _)); Assert.IsTrue(await prep.FlushAsync());
            Assert.IsTrue(Unbind(player, a, Read(player, a).Cards[0].InstanceId)); Assert.IsTrue(await player.FlushAsync());
            player = await Open(store); Configure(player); prep = await Prepare(player, prepStore); Assert.IsTrue(prep.LoadPreset(preset, out _));
            Assert.IsFalse(prep.ValidateForBattle(out _)); Assert.IsFalse(Deploy(player, prep.Working, out _, out _));
            Assert.IsFalse(Read(player, a).Bindings.Any(value => value.CardInstanceId == Read(player, a).Cards[0].InstanceId && value.SlotPath == "behavior.action"));
        }

        [Test] public async Task V4IdentitiesRequireExplicitUpgradeAndChangedDefaultsDoNotRewriteThem()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A");
            var state = State(player); state.Schema = CharacterTacticalPlayerState.CharacterItemSchema;
            foreach (var role in state.CharacterItems) { role.TacticalSchema = null; role.ActionGrants = null; role.Cards = null; role.Bindings = null; }
            store.Json = JsonConvert.SerializeObject(state); string before = store.Json; int writes = store.Writes.Count;
            player = await Open(store, defaults: Defaults(alternate: true)); Configure(player);
            Assert.IsFalse(player.TryReadCharacterItemLoadout(a, Owner, out _, out _, out string issue)); StringAssert.Contains("升级", issue);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, Learn(player, a).Status);
            Assert.IsFalse(Bind(player, a, 1)); var prep = await Prepare(player, new Store());
            Assert.IsFalse(prep.AddCharacterItemEntity(a, Owner, UnityEngine.Vector3.zero, 0, 1, prep.Revision, out _, out _));
            Assert.AreEqual(before, store.Json); Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(writes, store.Writes.Count);
        }

        [Test] public async Task CorruptedGrantOrSharedCardOrBindingCannotLoadOrRepairPartially()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); await player.FlushAsync(); string valid = store.Json;
            Action<CharacterTacticalPlayerState>[] corruptions = {
                value => value.CharacterItems[0].ActionGrants[0].RequestId = 999,
                value => value.CharacterItems[0].ActionGrants[0].RequestFingerprint = new string('z', 64),
                value => value.CharacterItems[1].ActionGrants.Add(JsonConvert.DeserializeObject<CharacterTacticalActionGrant>(JsonConvert.SerializeObject(value.CharacterItems[0].ActionGrants[1]))),
                value => value.CharacterItems[1].Cards[0].InstanceId = value.CharacterItems[0].Cards[0].InstanceId,
                value => value.CharacterItems[0].Bindings[0].CardInstanceId = value.CharacterItems[1].Cards[0].InstanceId,
                value => value.CharacterItems[0].TacticalSchema = null };
            foreach (var corrupt in corruptions)
            {
                var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(valid); corrupt(state); store.Json = JsonConvert.SerializeObject(state);
                string invalid = store.Json; int writes = store.Writes.Count; Assert.ThrowsAsync<JsonSerializationException>(() => Open(store));
                Assert.AreEqual(invalid, store.Json); Assert.AreEqual(writes, store.Writes.Count);
            }
        }

        [Test] public async Task FailedSaveRetryCapturesLatestLearningAndBindingOnce()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            await player.FlushAsync(); string durable = store.Json; store.Fail = true;
            Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, Read(player, a).Cards[0].InstanceId)); Assert.IsFalse(await player.FlushAsync());
            Assert.AreEqual(durable, store.Json); Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Learn(player, a).Status);
            store.Fail = false; Assert.IsTrue(await player.FlushAsync()); player = await Open(store); Configure(player);
            Assert.AreEqual(2, Read(player, a).ActionGrants.Count); Assert.AreEqual(302, player.Catalog.Action(Read(player, a).Bindings[0].TacticalActionId).ActionId);
            Assert.AreEqual(1, Read(player, b).ActionGrants.Count); Assert.IsFalse(player.HasUnsavedChanges);
        }

        [Test] public async Task InFlightSavePersistsLatestIndependentLearningWithoutLostBinding()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            await player.FlushAsync(); int writes = store.Writes.Count; store.Gate = new TaskCompletionSource<bool>();
            Assert.IsTrue(Learn(player, a).Success); Assert.IsTrue(Bind(player, a, Read(player, a).Cards[0].InstanceId));
            Assert.IsTrue(Learn(player, b, 101).Success); Assert.IsTrue(Unbind(player, b, Read(player, b).Cards[0].InstanceId));
            Assert.AreEqual(writes, store.Writes.Count); store.Gate.SetResult(true); Assert.IsTrue(await player.FlushAsync()); Assert.AreEqual(2, store.Writes.Count - writes);
            player = await Open(store); Configure(player); Assert.AreEqual(2, Read(player, a).ActionGrants.Count); Assert.AreEqual(2, Read(player, b).ActionGrants.Count);
            Assert.AreEqual("action:fixture.second", Read(player, a).Bindings[0].TacticalActionId);
            Assert.IsFalse(Read(player, b).Bindings.Any(value => value.CardInstanceId == Read(player, b).Cards[0].InstanceId && value.SlotPath == "behavior.action"));
        }

        [Test] public async Task DeploymentSnapshotRemainsFrozenWhenItemBindingsAndInputSetupChange()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            Assert.IsTrue(Learn(player, a).Success); ulong card = Read(player, a).Cards[0].InstanceId; Assert.IsTrue(Bind(player, a, card));
            var prep = await Prepare(player, new Store()); string entity = Add(prep, a); Add(prep, b, 2); var setup = prep.Working;
            Assert.IsTrue(Deploy(player, setup, out var deployment, out string issue), issue);
            var participant = deployment.Participants.Single(value => value.EntityId == entity); long revision = participant.CharacterItemRevision;
            setup.Entities[0].Loadout.Bindings[0].TacticalActionId = "action:fixture.first"; Assert.IsTrue(Unbind(player, a, card));
            Assert.AreEqual(302, participant.Bindings[card]["behavior.action"]); Assert.AreEqual(revision, participant.CharacterItemRevision);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)participant.Bindings[card]).Add("external.slot", 303));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<ulong, System.Collections.ObjectModel.ReadOnlyDictionary<string, int>>)participant.Bindings).Clear());
        }

        [Test] public async Task MissingCardVersionAndRoleRevisionOverflowCannotPartiallyLearnOrBind()
        {
            var store = new Store(); var player = await Open(store); Configure(player); var a = Acquire(player, 1, "A");
            await player.FlushAsync(); var state = State(player); state.CharacterItems[0].Revision = long.MaxValue;
            store.Json = JsonConvert.SerializeObject(state); player = await Open(store); Configure(player); string before = player.CaptureProgressJson();
            Assert.AreEqual(ItemAcquisitionStatus.CommitFailed, Learn(player, a).Status);
            Assert.IsFalse(player.UnbindCharacterActionAtTick(a, Owner, Read(player, a).Cards[0].InstanceId, "behavior.action", long.MaxValue, player.Revision, 10, Tick(), out _));
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(before, store.Json);
            Assert.IsTrue(player.TryRefreshPublishedCards(new[] { new CharacterTacticalCardDefinition { CardId = "fixture.card", Revision = 2,
                ContentHash = "fixture-card-v2", DisplayName = "Changed", Slots = new[] { "behavior.action" } } }, out int invalidOwned, out _));
            Assert.AreEqual(2, invalidOwned); Assert.IsFalse(Bind(player, a, Read(player, a).Cards[0].InstanceId, "action:fixture.first"));
            Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task InvalidPresetReferencesFailWithoutChangingCurrentPreparation()
        {
            var player = await Open(new Store()); Configure(player); var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B");
            var store = new Store(); var prep = await Prepare(player, store); Add(prep, a); Add(prep, b, 2);
            Assert.IsTrue(prep.SaveAsPreset("AB", out string preset, out _)); await prep.FlushAsync();
            var saved = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json);
            saved.Presets.Single().Snapshot.Entities[0].InventoryOwnerId = Other.Value;
            store.Json = JsonConvert.SerializeObject(saved); prep = await Prepare(player, store); string before = prep.CaptureProgressJson();
            Assert.IsFalse(prep.LoadPreset(preset, out _)); Assert.AreEqual(before, prep.CaptureProgressJson());
            var tampered = prep.Working; tampered.Entities[0].CharacterId = 999;
            Assert.IsFalse(Deploy(player, tampered, out _, out _));
        }

        [Test] public async Task RealAggregateCheckpointReopensSelectedItemsAndRejectsStaleWriter()
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxFramework.CharacterActionInstanceFixtures"));
            string root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            try
            {
                async Task<(CharacterTacticalPlayerService player, TacticalBattlePreparationService prep, CharacterTacticalProgressCandidate candidate)> Session(bool create)
                {
                    var physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
                    var progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync()); var candidate = create ? progress.Create() : progress.OpenActive();
                    var player = await Open(candidate.PlayerStore); Configure(player); var prep = await Prepare(player, candidate.PreparationStore);
                    candidate.Bind(player.CaptureProgressJson, prep.CaptureProgressJson); if (create) await candidate.SaveAsync(activate: true); else candidate.EnableAutosave();
                    return (player, prep, candidate);
                }
                var first = await Session(true); var templateEntities = first.prep.Working.Entities.Select(value => value.EntityId).ToArray();
                var a = Acquire(first.player, 1, "A"); var b = Acquire(first.player, 2, "B");
                foreach (string templateEntity in templateEntities) Assert.IsTrue(first.prep.RemoveEntity(templateEntity, out _));
                Assert.IsTrue(Learn(first.player, a).Success); Assert.IsTrue(Bind(first.player, a, Read(first.player, a).Cards[0].InstanceId));
                Add(first.prep, a); Add(first.prep, b, 2); Assert.IsTrue(await first.player.FlushAsync()); Assert.IsTrue(await first.prep.FlushAsync());
                var stale = await Session(false); var reopened = await Session(false);
                Assert.IsTrue(Deploy(reopened.player, reopened.prep.Working, out _, out string issue), issue);
                Assert.AreEqual(a.Value, reopened.prep.Working.Entities[0].CharacterItemInstanceId); Assert.AreEqual(1, Read(reopened.player, b).ActionGrants.Count);
                Assert.IsTrue(Learn(reopened.player, b, 101).Success); Assert.IsTrue(await reopened.player.FlushAsync());
                Assert.IsTrue(Unbind(stale.player, a, Read(stale.player, a).Cards[0].InstanceId)); Assert.IsFalse(await stale.player.FlushAsync());
                var recovered = await Session(false); Assert.AreEqual(2, Read(recovered.player, b).ActionGrants.Count);
                Assert.IsTrue(Deploy(recovered.player, recovered.prep.Working, out _, out issue), issue);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, Learn(recovered.player, a).Status);
            }
            finally
            { if (root.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root, true); }
        }
        private static CharacterTacticalRoleInventoryConfig InventoryConfig(int capacity = 8) => new() {
            Schema = CharacterTacticalRoleInventoryConfig.CurrentSchema, OwnerScope = "profile-local", PlayerOwnerId = 1,
            Roles = new[] { new CharacterTacticalRoleDefinitionConfig { ItemDefinitionId = 41001, StableId = "fixture.role-item",
                CharacterStableId = "fixture.hero", NameKey = "fixture.role.name", DescriptionKey = "fixture.role.description", Kind = "Custom", MaxStackCount = 1 } },
            Containers = new[] {
                new CharacterTacticalRoleContainerConfig { ContainerId = 1, OwnerId = 1, Capacity = capacity, StableId = "fixture.roles" },
                new CharacterTacticalRoleContainerConfig { ContainerId = 2, OwnerId = 2, Capacity = capacity, StableId = "fixture.other" } } };
        private static CharacterTacticalRoleNewGameConfig NewGameConfig(bool foreignOwner = false)
        {
            var defaults = Defaults(); defaults.Cards.RemoveAt(1); defaults.Cards[0].InstanceId = 0;
            defaults.Bindings.RemoveAll(row => row.CardInstanceId == 2);
            foreach (var binding in defaults.Bindings) binding.CardInstanceId = 0;
            return new CharacterTacticalRoleNewGameConfig { Schema = CharacterTacticalRoleNewGameConfig.CurrentSchema,
                Characters = new[] { defaults }, PlayerControlledGrantRequestId = 1,
                CharacterItemGrants = new[] {
                    new CharacterTacticalRoleGrantConfig { RequestId = 1, RoleStableId = "fixture.role-item", OwnerId = 1, ContainerId = 1, DisplayName = "A",
                        Deployment = new CharacterTacticalRoleDeploymentConfig { X = -2, Y = 1, Z = -1, TeamId = 1, YawDegrees = 45 } },
                    new CharacterTacticalRoleGrantConfig { RequestId = 2, RoleStableId = "fixture.role-item", OwnerId = foreignOwner ? 2 : 1,
                        ContainerId = foreignOwner ? 2 : 1, DisplayName = "B",
                        Deployment = new CharacterTacticalRoleDeploymentConfig { X = 2, Y = 1, Z = 1, TeamId = 2, YawDegrees = 225 } } } };
        }
        private static void ConfigureContract(CharacterTacticalPlayerService player, CharacterTacticalRoleInventoryConfig config)
        {
            config.Build(new[] { "fixture.hero" }, out var definitions, out var containers, out var mapping);
            Assert.IsTrue(player.ConfigureCharacterItemInventory(definitions, containers, mapping, out string issue), issue);
        }
        private static Task<CharacterTacticalPlayerService> OpenRoleNewGame(ICharacterTacticalStateStore store, CharacterTacticalRoleNewGameConfig recipe)
            => CharacterTacticalPlayerService.OpenAsync(Catalog(), recipe.Characters, store,
                addMissingDefaults: false, instantiateTemplateDefaults: false);

        [Test] public async Task NewContractLoadsEmptyLayoutsWithoutInventingTemplateOrItemOwnership()
        {
            var recipe = NewGameConfig(); var config = InventoryConfig(); var store = new Store();
            config = JsonConvert.DeserializeObject<CharacterTacticalRoleInventoryConfig>(JsonConvert.SerializeObject(config));
            recipe = JsonConvert.DeserializeObject<CharacterTacticalRoleNewGameConfig>(JsonConvert.SerializeObject(recipe));
            recipe.Validate(config, Catalog(), new[] { "fixture.hero" });
            var player = await OpenRoleNewGame(store, recipe); string before = player.CaptureProgressJson();
            ConfigureContract(player, config);
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var items, out _)); Assert.IsEmpty(items);
            Assert.IsEmpty(State(player).Characters); Assert.IsNull(State(player).CharacterItems);
            Assert.AreEqual(1ul, State(player).NextCardInstanceId); Assert.AreEqual(before, player.CaptureProgressJson());
            Assert.AreEqual(before, store.Json); Assert.AreEqual(-1, player.LastItemTransactionFrame);
        }

        [Test] public void ContractRejectsUnregisteredIdsDuplicatesUnknownTemplatesAndImplicitOwnerScope()
        {
            Action<CharacterTacticalRoleInventoryConfig>[] mutations = {
                value => value.Roles[0].ItemDefinitionId = 0,
                value => value.Roles[0].CharacterStableId = "unknown.template",
                value => value.Roles[0].MaxStackCount = 2,
                value => value.Roles[0].Kind = "Equipment",
                value => value.Roles = new[] { value.Roles[0], value.Roles[0] },
                value => value.Containers[1].ContainerId = 1,
                value => value.Containers[1].StableId = value.Containers[0].StableId,
                value => value.PlayerOwnerId = 999,
                value => value.OwnerScope = "network-peer",
                value => value.Schema = null };
            foreach (var mutate in mutations)
            { var config = InventoryConfig(); mutate(config); Assert.Throws<JsonSerializationException>(() => config.Build(new[] { "fixture.hero" }, out _, out _, out _)); }
        }

        [Test] public void NewGameContractRejectsForeignOwnerDuplicateRequestsStaleCardsCapacityAndControl()
        {
            Action<CharacterTacticalRoleNewGameConfig>[] mutations = {
                value => value.Schema = "mx.character-test.tactical-new-game.v1",
                value => value.CharacterItemGrants[1].RequestId = 1,
                value => value.CharacterItemGrants[1].OwnerId = 2,
                value => value.CharacterItemGrants[1].RoleStableId = "unknown.role",
                value => value.CharacterItemGrants[1].ContainerId = 999,
                value => value.Characters[0].Cards[0].ContentHash = "wrong.version",
                value => value.Characters[0].Bindings[0].SlotPath = "unknown.slot",
                value => value.CharacterItemGrants[0].Deployment.Y = float.NaN,
                value => value.CharacterItemGrants[0].Deployment.X = 1000,
                value => value.CharacterItemGrants[0].Deployment = null,
                value => value.PlayerControlledGrantRequestId = 999 };
            foreach (var mutate in mutations)
            { var recipe = NewGameConfig(); mutate(recipe); Assert.Throws<JsonSerializationException>(() => recipe.Validate(InventoryConfig(), Catalog(), new[] { "fixture.hero" })); }
            Assert.Throws<JsonSerializationException>(() => NewGameConfig().Validate(InventoryConfig(1), Catalog(), new[] { "fixture.hero" }));
            var foreign = NewGameConfig(true); foreign.PlayerControlledGrantRequestId = 2;
            Assert.Throws<JsonSerializationException>(() => foreign.Validate(InventoryConfig(), Catalog(), new[] { "fixture.hero" }));
        }

        [Test] public async Task RealRuntimeHostBootstrapAllocatesIndependentInstancesAndCardsExactlyOnce()
        {
            var recipe = NewGameConfig(); var config = InventoryConfig(); recipe.Validate(config, Catalog(), new[] { "fixture.hero" });
            var player = await OpenRoleNewGame(new Store(), recipe); ConfigureContract(player, config);
            var instances = CharacterTacticalRoleNewGameBootstrap.Execute(player, config, recipe);
            Assert.AreEqual(2, instances.Count); Assert.AreNotEqual(instances[1], instances[2]);
            var a = Read(player, instances[1]); var b = Read(player, instances[2]);
            Assert.AreEqual("A", a.DisplayName); Assert.AreEqual("B", b.DisplayName);
            Assert.AreNotEqual(a.Cards[0].InstanceId, b.Cards[0].InstanceId); Assert.AreEqual(0, player.LastItemTransactionFrame);
            Assert.IsEmpty(State(player).Characters); Assert.AreEqual(3ul, State(player).NextCardInstanceId);
            string before = player.CaptureProgressJson();
            Assert.Throws<InvalidOperationException>(() => CharacterTacticalRoleNewGameBootstrap.Execute(player, config, recipe));
            Assert.AreEqual(before, player.CaptureProgressJson());
            Assert.IsTrue(Learn(player, instances[1]).Success); Assert.AreEqual(1, Read(player, instances[2]).ActionGrants.Count);
        }

        [Test] public async Task NewGamePreparationReferencesGrantResultsAndKeepsOwnerSeparateFromTeam()
        {
            var recipe = NewGameConfig(true); var config = InventoryConfig(); recipe.Validate(config, Catalog(), new[] { "fixture.hero" });
            var player = await OpenRoleNewGame(new Store(), recipe); ConfigureContract(player, config);
            var instances = CharacterTacticalRoleNewGameBootstrap.Execute(player, config, recipe);
            var prep = await Prepare(player, new Store()); Assert.IsEmpty(prep.Working.Entities);
            Assert.IsTrue(prep.ConfigureCharacterItemNewGame(recipe, instances, out string issue), issue);
            Assert.IsTrue(prep.ValidateForBattle(out issue), issue);
            Assert.AreEqual(instances[1].Value, prep.Find(prep.Working.PlayerControlledEntityId).CharacterItemInstanceId);
            Assert.AreEqual(2, prep.Working.Entities[1].InventoryOwnerId); Assert.AreEqual(2, prep.Working.Entities[1].TeamId);
            Assert.IsTrue(player.TryReadOwnedCharacterItems(Owner, out var items, out _)); Assert.AreEqual(1, items.Length);
            Assert.IsFalse(player.TryReadCharacterItem(instances[2], Owner, out _, out _));
            var stored = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(prep.CaptureProgressJson());
            Assert.AreEqual(TacticalBattlePreparationState.CharacterItemSchema, stored.Schema);
            Assert.IsTrue(stored.Working.Entities.All(entity => entity.Loadout.Cards.Count == 0));
            Assert.IsTrue(prep.SetTeam(stored.Working.Entities[0].EntityId, 2, out _));
            Assert.AreEqual(1, prep.Working.Entities[0].InventoryOwnerId);
        }

        [Test] public async Task InvalidInitialPreparationRestoresEmptyCandidateWithoutChangingRoleOwnership()
        {
            var recipe = NewGameConfig(); var config = InventoryConfig(); recipe.Validate(config, Catalog(), new[] { "fixture.hero" });
            var player = await OpenRoleNewGame(new Store(), recipe); ConfigureContract(player, config);
            var instances = CharacterTacticalRoleNewGameBootstrap.Execute(player, config, recipe);
            var prep = await Prepare(player, new Store()); string before = prep.CaptureProgressJson(); long revision = prep.Revision;
            string progress = player.CaptureProgressJson();
            var incomplete = new Dictionary<long, ItemInstanceId> { [1] = instances[1] };
            Assert.IsFalse(prep.ConfigureCharacterItemNewGame(recipe, incomplete, out _));
            Assert.AreEqual(before, prep.CaptureProgressJson()); Assert.AreEqual(revision, prep.Revision);
            Assert.AreEqual(progress, player.CaptureProgressJson());
            Assert.IsTrue(prep.ConfigureCharacterItemNewGame(recipe, instances, out _));
            before = prep.CaptureProgressJson(); Assert.IsFalse(prep.ConfigureCharacterItemNewGame(recipe, instances, out _));
            Assert.AreEqual(before, prep.CaptureProgressJson());
        }

        [Test] public async Task AggregateNewGameBootstrapReopensSavedInstancesWithoutRegrantingOrCopyingNewDefaults()
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxFramework.RoleNewGameFixtures"));
            string root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            try
            {
                var recipe = NewGameConfig(); var config = InventoryConfig(); recipe.Validate(config, Catalog(), new[] { "fixture.hero" });
                var physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
                var progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync()); var candidate = progress.Create();
                var player = await OpenRoleNewGame(candidate.PlayerStore, recipe); ConfigureContract(player, config);
                var instances = CharacterTacticalRoleNewGameBootstrap.Execute(player, config, recipe);
                var prep = await Prepare(player, candidate.PreparationStore);
                Assert.IsTrue(prep.ConfigureCharacterItemNewGame(recipe, instances, out _));
                candidate.Bind(player.CaptureProgressJson, prep.CaptureProgressJson);
                Assert.IsFalse(progress.HasCheckpoint, "Detached bootstrap cannot publish or persist an incomplete aggregate.");
                await candidate.SaveAsync(activate: true); string durablePlayer = player.CaptureProgressJson(); string durablePrep = prep.CaptureProgressJson();
                physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
                progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync()); candidate = progress.OpenActive();
                var changed = NewGameConfig(); changed.Characters[0].ActionIds[0] = "action:fixture.second";
                foreach (var binding in changed.Characters[0].Bindings) binding.TacticalActionId = "action:fixture.second";
                player = await OpenRoleNewGame(candidate.PlayerStore, changed); ConfigureContract(player, config);
                prep = await Prepare(player, candidate.PreparationStore);
                Assert.AreEqual(durablePlayer, player.CaptureProgressJson()); Assert.AreEqual(durablePrep, prep.CaptureProgressJson());
                Assert.AreEqual(1, Read(player, instances[1]).ActionGrants.Count); Assert.AreEqual(301, Read(player, instances[1]).ActionGrants[0].ActionId);
                Assert.AreEqual(2, prep.Working.Entities.Count); Assert.IsTrue(prep.ValidateForBattle(out string issue), issue);
                Assert.Throws<InvalidOperationException>(() => CharacterTacticalRoleNewGameBootstrap.Execute(player, config, changed));
            }
            finally { if (root.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static RuntimeHost BindingHost(CharacterTacticalRoleBindingQueue queue, Func<bool> active = null)
        {
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => queue, active ?? (() => true)));
            host.Initialize(); host.Start(); return host;
        }
        private static bool QueueBinding(CharacterTacticalRoleBindingQueue queue, CharacterTacticalPlayerService player,
            TacticalBattlePreparationService prep, string entity, ItemInstanceId item, string action,
            Func<bool> current, Action<bool, string> complete)
        {
            var role = Read(player, item);
            return queue.Enqueue(entity, item, Owner, role.Cards[0].InstanceId, "behavior.action", action,
                role.Revision, player.Revision, prep.Revision, player.CatalogRevision, current, complete, out _);
        }
        [Test] public async Task BindingQueueChangesOnlyTheSelectedInstanceAfterActualSimulationTickAndReopens()
        {
            var store = new Store(); var prepStore = new Store(); var player = await Open(store); Configure(player);
            var a = Acquire(player, 1, "A"); var b = Acquire(player, 2, "B"); Assert.IsTrue(Learn(player, a).Success);
            var prep = await Prepare(player, prepStore); string entity = Add(prep, a); Add(prep, b, 2);
            var queue = new CharacterTacticalRoleBindingQueue(player, prep, Owner); bool? result = null;
            string before = player.CaptureProgressJson(), other = JsonConvert.SerializeObject(Read(player, b));
            Assert.IsTrue(QueueBinding(queue, player, prep, entity, a, "action:fixture.second", () => true, (ok, _) => result = ok));
            Assert.AreEqual(1, queue.PendingCount); Assert.IsNull(result); Assert.AreEqual(before, player.CaptureProgressJson());
            using var host = BindingHost(queue); host.Tick(11, 1d / 60d);
            Assert.AreEqual(true, result); Assert.AreEqual(0, queue.PendingCount);
            Assert.AreEqual("action:fixture.second", Read(player, a).Bindings[0].TacticalActionId);
            Assert.AreEqual(other, JsonConvert.SerializeObject(Read(player, b)));
            Assert.AreEqual("action:fixture.first", Read(player, a).Bindings.First(row => row.CardInstanceId == Read(player, a).Cards[1].InstanceId).TacticalActionId);
            Assert.IsTrue(await player.FlushAsync()); Assert.IsTrue(await prep.FlushAsync());
            player = await Open(store); Configure(player); prep = await Prepare(player, prepStore);
            Assert.AreEqual("action:fixture.second", prep.Find(entity).Loadout.Bindings[0].TacticalActionId);
            Assert.IsTrue(Deploy(player, prep.Working, out _, out string issue), issue);
        }
        [Test] public async Task BindingQueueCancelsOnPhaseExitSourceSwitchAndStaleVersionsWithoutMutation()
        {
            foreach (int scenario in Enumerable.Range(0, 5))
            {
                var player = await Open(new Store()); Configure(player); var item = Acquire(player, 1, "A"); Assert.IsTrue(Learn(player, item).Success);
                var prep = await Prepare(player, new Store()); string entity = Add(prep, item); var queue = new CharacterTacticalRoleBindingQueue(player, prep, Owner);
                bool current = true; bool? result = null;
                Assert.IsTrue(QueueBinding(queue, player, prep, entity, item, "action:fixture.second", () => current, (ok, _) => result = ok));
                if (scenario == 1) current = false;
                if (scenario == 2) Assert.IsTrue(prep.SetTeam(entity, 2, out _));
                if (scenario == 3) Assert.IsTrue(player.TryRenameCharacterItemAtTick(item, Owner, "Renamed", Read(player, item).Revision, player.Revision, 10, Tick(), out _));
                string before = player.CaptureProgressJson();
                using var host = BindingHost(queue, () => scenario != 0);
                if (scenario == 4) queue.Cancel("profile switched"); else host.Tick(11, 1d / 60d);
                Assert.AreEqual(false, result); Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(0, queue.PendingCount);
            }
        }
        [Test] public async Task BindingQueueRejectsForeignOwnerAndDuplicateSubmissionAndDoesNotAcceptWrongStage()
        {
            var player = await Open(new Store()); Configure(player); var item = Acquire(player, 1, "A"); var prep = await Prepare(player, new Store()); string entity = Add(prep, item);
            var queue = new CharacterTacticalRoleBindingQueue(player, prep, Owner); var role = Read(player, item);
            Assert.IsFalse(queue.Enqueue(entity, item, Other, role.Cards[0].InstanceId, "behavior.action", null, role.Revision,
                player.Revision, prep.Revision, player.CatalogRevision, () => true, (_, _) => { }, out _));
            int completions = 0; Assert.IsTrue(QueueBinding(queue, player, prep, entity, item, null, () => true, (_, _) => completions++));
            Assert.IsFalse(QueueBinding(queue, player, prep, entity, item, null, () => true, (_, _) => completions++));
            string before = player.CaptureProgressJson(); Assert.Throws<InvalidOperationException>(() => queue.Drain(new RuntimeTickContext(11, 0, 0, RuntimeTickStage.PreSimulation), true));
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(1, queue.PendingCount);
            using var host = BindingHost(queue); host.Tick(11, 0);
            Assert.AreEqual(1, completions); Assert.IsFalse(Read(player, item).Bindings.Any(row => row.CardInstanceId == role.Cards[0].InstanceId && row.SlotPath == "behavior.action"));
        }
        [Test] public async Task BindingModuleDisposeCancelsPendingRequestAndMissingCardVersionCannotBind()
        {
            var player = await Open(new Store()); Configure(player); var item = Acquire(player, 1, "A"); var prep = await Prepare(player, new Store()); string entity = Add(prep, item);
            var queue = new CharacterTacticalRoleBindingQueue(player, prep, Owner); bool? result = null; string before = player.CaptureProgressJson();
            Assert.IsTrue(QueueBinding(queue, player, prep, entity, item, null, () => true, (ok, _) => result = ok));
            var host = BindingHost(queue); host.Dispose(); Assert.AreEqual(false, result); Assert.AreEqual(before, player.CaptureProgressJson());
            result = null; Assert.IsTrue(QueueBinding(queue, player, prep, entity, item, "action:fixture.first", () => true, (ok, _) => result = ok));
            Assert.IsTrue(player.TryRefreshPublishedCards(Array.Empty<CharacterTacticalCardDefinition>(), out _, out _));
            using var next = BindingHost(queue); next.Tick(11, 0); Assert.AreEqual(false, result); Assert.AreEqual(before, player.CaptureProgressJson());
        }

        private sealed class RoleEditFlowState : IAppFlowState
        {
            internal RoleEditFlowState(string id) { StateId = id; }
            public string StateId { get; }
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }
        [Test] public async Task RealAppFlowTransitionRunsBeforeBindingConsumerAndPreventsPostDeploymentEdit()
        {
            var player = await Open(new Store()); Configure(player); var item = Acquire(player, 1, "A"); Assert.IsTrue(Learn(player, item).Success);
            var prep = await Prepare(player, new Store()); string entity = Add(prep, item);
            var queue = new CharacterTacticalRoleBindingQueue(player, prep, Owner); bool? result = null;
            var app = new AppFlowController(); app.RegisterState(new RoleEditFlowState("Preparation")); app.RegisterState(new RoleEditFlowState("Deploying")); app.Start("Preparation");
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new AppFlowRuntimeModule(app, priority: -1));
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => queue, () => app.CurrentStateId == "Preparation"));
            host.Initialize(); host.Start();
            Assert.IsTrue(QueueBinding(queue, player, prep, entity, item, "action:fixture.second", () => true, (ok, _) => result = ok));
            string before = player.CaptureProgressJson(); app.RequestTransition("Deploying"); host.Tick(11, 0);
            Assert.AreEqual("Deploying", app.CurrentStateId); Assert.AreEqual(false, result); Assert.AreEqual(before, player.CaptureProgressJson());
        }

    }

    internal static class FixtureIds_ItemAcquisitionTests
    {
        public const int Id_test_acquire = 1;
        public const int Id_test_stale = 2;
    }
}
