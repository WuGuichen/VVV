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
    public sealed class TacticalCardAssemblyFoundationTests
    {
        private static readonly InventoryOwnerId Owner = new(1), Other = new(2);
        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            internal string Json;
            internal bool RejectSave;
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public Task SaveAsync(string json)
            { if (RejectSave) throw new IOException("fixture save rejection"); Json = json; return Task.CompletedTask; }
            public Task ResetAsync(string json) => SaveAsync(json);
        }
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        private static CharacterTacticalCardReference MainReference() => new() { CardId = "fixture.main", Revision = 1, ContentHash = "fixture-main-v1" };
        private static CharacterTacticalAccessoryReference Accessory(int index) => new() {
            AccessoryId = "fixture.accessory." + index, Revision = 1, ContentHash = "fixture-accessory-v1-" + index };
        private static CharacterTacticalAssemblyConfiguration Configuration(CharacterTacticalCardQuality quality = CharacterTacticalCardQuality.Silver)
        {
            var config = new CharacterTacticalAssemblyConfiguration {
            Schema = CharacterTacticalAssemblyConfiguration.CurrentSchema, Revision = 1, ContentHash = "fixture-assembly-v1",
            MainCards = new[] { new CharacterTacticalMainCardDefinition { Card = MainReference(), Quality = quality } },
            Accessories = Enumerable.Range(1, 3).Select(index => new CharacterTacticalAccessoryDefinition {
                ItemDefinitionId = 42000 + index, Reference = Accessory(index), Quality = CharacterTacticalCardQuality.Gold,
                TriggerContractId = "fixture.trigger." + index, FailureContractId = "fixture.failure." + index,
                LifetimeContractId = "fixture.lifetime." + index }).ToArray()
            };
            config.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(config); return config;
        }
        private static CharacterTacticalContentCatalog Catalog() => new()
        {
            Actions = new[] { new CharacterTacticalActionDefinition { Id = "action:fixture", ActionId = 301,
                ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } } },
            Cards = new[] { new CharacterTacticalCardDefinition { CardId = MainReference().CardId, Revision = 1,
                ContentHash = MainReference().ContentHash, Slots = new[] { "behavior.action" } } },
            Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = "fixture.hero",
                ActionIds = new[] { "action:fixture" }, Cards = new[] { MainReference() } } }
        };
        private static CharacterTacticalDefaults Defaults() => new()
        {
            CharacterStableId = "fixture.hero", ActionIds = new List<string> { "action:fixture" },
            Cards = new List<CharacterTacticalCardReference> { new() { InstanceId = 1, CardId = MainReference().CardId, Revision = 1, ContentHash = MainReference().ContentHash },
                new() { InstanceId = 2, CardId = MainReference().CardId, Revision = 1, ContentHash = MainReference().ContentHash } },
            Bindings = new List<CharacterTacticalSlotBinding> {
                new() { CardInstanceId = 1, CardId = MainReference().CardId, SlotPath = "behavior.action", TacticalActionId = "action:fixture" },
                new() { CardInstanceId = 2, CardId = MainReference().CardId, SlotPath = "behavior.action", TacticalActionId = "action:fixture" } }
        };
        private static ItemDefinitionRegistry Definitions()
        {
            var value = new ItemDefinitionRegistry();
            value.Register(ItemDefinitionTests.CreateDefinition(41001, "fixture.role", ItemKind.Custom, 1));
            // Stack policy remains authored; this API grants one separately addressable copy.
            for (int i = 1; i <= 3; i++) value.Register(ItemDefinitionTests.CreateDefinition(42000 + i, "fixture.accessory." + i, ItemKind.Custom, 20));
            return value;
        }
        private static async Task<CharacterTacticalPlayerService> Open(ICharacterTacticalStateStore store,
            CharacterTacticalCardQuality quality = CharacterTacticalCardQuality.Silver, int capacity = 12, bool configure = true)
        {
            var player = await CharacterTacticalPlayerService.OpenAsync(Catalog(), new[] { Defaults() }, store,
                addMissingDefaults: false, instantiateTemplateDefaults: false);
            Assert.IsTrue(player.ConfigureCharacterItemInventory(Definitions(), new[] {
                new InventoryContainer(new InventoryContainerId(1), Owner, capacity, "fixture.bag"),
                new InventoryContainer(new InventoryContainerId(2), Other, capacity, "fixture.other") },
                new[] { new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = "fixture.hero" } }, out string issue), issue);
            if (configure) Assert.IsTrue(player.ConfigureCardAssembly(Configuration(quality), out issue), issue);
            return player;
        }
        private static RuntimeTickContext Tick(long frame = 10) => new(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation);
        private static ItemInstanceId Role(CharacterTacticalPlayerService player, long request = 1)
        {
            var result = player.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(request),
                ItemAcquisitionSourceKind.DirectGrant, null, null), new InventoryContainerId(1), Owner, new ItemDefinitionId(41001),
                "Fixture " + request, player.Revision, 10, Tick(), out var id, out string issue);
            Assert.IsTrue(result.Success, issue); return id;
        }
        private static CharacterTacticalCharacterItemState Read(CharacterTacticalPlayerService player, ItemInstanceId id)
        { Assert.IsTrue(player.TryReadCharacterItem(id, Owner, out var value, out string issue), issue); return value; }
        private static ItemInstanceId GrantAccessory(CharacterTacticalPlayerService player, int index = 1, long request = 100, bool other = false)
        {
            var result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(request), new InventoryContainerId(other ? 2 : 1),
                other ? Other : Owner, new ItemDefinitionId(42000 + index), player.Revision, 10, Tick(), out var id, out string issue);
            Assert.IsTrue(result.Success, issue); return id;
        }
        private static CharacterTacticalCardAssembly Assembly(CharacterTacticalPlayerService player, ItemInstanceId role, int cardIndex, params ItemInstanceId[] accessories)
        {
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson());
            player.TryReadCardAssemblyConfiguration(out var config, out _);
            return new CharacterTacticalCardAssembly { Schema = CharacterTacticalCardAssembly.CurrentSchema,
                ConfigurationRevision = config?.Revision ?? 1, ConfigurationHash = config?.ContentHash ?? "unconfigured", MainCard = Read(player, role).Cards[cardIndex],
                Accessories = accessories.Select((id, index) => new CharacterTacticalAccessorySlot { SlotIndex = index,
                    AccessoryItemInstanceId = id.Value, Reference = Copy(state.AccessoryItems.Single(value => value.ItemInstanceId == id.Value).Reference) }).ToList() };
        }
        private static bool Set(CharacterTacticalPlayerService player, ItemInstanceId role, CharacterTacticalCardAssembly assembly)
            => player.SetCharacterCardAssemblyAtTick(role, Owner, assembly, Read(player, role).Revision, player.Revision, 10, Tick(), out _);

        [Test] public async Task DataOnlyStoredEmptySelectionCannotBecomeExecutableButBareCardIsUnchanged()
        {
            var player = await Open(new MemoryStore()); var role = Role(player);
            var held = Read(player, role).Cards[0];
            var definition = new TacticalActionCardDefinition("fixture.main", "Fixture", "Data-only boundary", 1,
                held.CardId, held.Revision, held.ContentHash, held.InstanceId);
            var bindings = new Dictionary<string, int> { { "behavior.action", 301 } };
            Assert.IsTrue(player.TryFreezeStoredCardAssembly(role, Owner, definition, bindings, out var bare, out _));
            Assert.IsNull(bare);
            Assert.IsTrue(Set(player, role, Assembly(player, role, 0)));
            string before = player.CaptureProgressJson(); long revision = player.Revision;
            Assert.IsFalse(player.TryFreezeStoredCardAssembly(role, Owner, definition, bindings, out var frozen, out _));
            Assert.IsNull(frozen); Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public async Task QualityCapacityIsExactAndCommonToBothModes(int quality, int capacity)
        {
            var player = await Open(new MemoryStore(), (CharacterTacticalCardQuality)quality); var role = Role(player);
            var copies = Enumerable.Range(1, capacity).Select(index => GrantAccessory(player, index, 100 + index)).ToArray();
            var assembly = Assembly(player, role, 0, copies);
            foreach (var mode in new[] { CharacterTacticalAssemblyMode.FixedPreassembled, CharacterTacticalAssemblyMode.RandomDrawAssembly })
            {
                Assert.IsTrue(player.TryFreezeCardAssembly(role, Owner, assembly, mode, out var frozen, out string issue), issue);
                Assert.AreEqual(capacity, frozen.AccessorySlotCount); Assert.AreEqual(mode, frozen.Mode);
            }
            assembly.Accessories.Add(new CharacterTacticalAccessorySlot { SlotIndex = capacity, AccessoryItemInstanceId = copies[0].Value, Reference = Accessory(1) });
            string before = player.CaptureProgressJson(); long revision = player.Revision;
            Assert.IsFalse(Set(player, role, assembly)); Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task SameTemplateInstancesAreIndependentAndSnapshotsAreDetached()
        {
            var player = await Open(new MemoryStore()); var a = Role(player); var b = Role(player, 2);
            var copy = GrantAccessory(player); var assembly = Assembly(player, a, 0, copy);
            Assert.IsTrue(Set(player, a, assembly)); assembly.Accessories.Clear();
            var saved = Read(player, a); Assert.AreEqual(1, saved.CardAssemblies.Count); Assert.AreEqual(1, saved.CardAssemblies[0].Accessories.Count);
            Assert.IsNull(Read(player, b).CardAssemblies); Assert.AreNotEqual(saved.Cards[0].InstanceId, Read(player, b).Cards[0].InstanceId);
            Assert.IsTrue(player.TryFreezeCardAssembly(a, Owner, saved.CardAssemblies[0], CharacterTacticalAssemblyMode.FixedPreassembled, out var frozen, out _));
            frozen.Assembly.Accessories.Clear(); frozen.Accessories[0].TriggerContractId = "tampered";
            Assert.AreEqual(1, Read(player, a).CardAssemblies[0].Accessories.Count);
            Assert.IsTrue(player.TryFreezeCardAssembly(a, Owner, saved.CardAssemblies[0], CharacterTacticalAssemblyMode.RandomDrawAssembly, out frozen, out _));
            Assert.AreEqual("fixture.trigger.1", frozen.Accessories[0].TriggerContractId);
        }

        [Test] public async Task MissingStaleForeignReferencesAndRevisionsRejectWithoutMutation()
        {
            var player = await Open(new MemoryStore()); var role = Role(player); var otherRole = Role(player, 2);
            var copy = GrantAccessory(player); var foreign = GrantAccessory(player, 2, 101, true);
            var source = Assembly(player, role, 0, copy); string before = player.CaptureProgressJson(); long revision = player.Revision;
            var invalid = new Action<CharacterTacticalCardAssembly>[] {
                value => value.MainCard.Revision++, value => value.MainCard.ContentHash = "stale",
                value => value.MainCard = Read(player, otherRole).Cards[0], value => value.ConfigurationRevision++,
                value => value.ConfigurationHash = "stale", value => value.Schema = "future",
                value => value.Accessories[0].Reference.Revision++, value => value.Accessories[0].Reference.ContentHash = "stale",
                value => value.Accessories[0].AccessoryItemInstanceId = 999,
                value => { value.Accessories[0].AccessoryItemInstanceId = foreign.Value; value.Accessories[0].Reference = Accessory(2); } };
            foreach (var mutate in invalid) { var value = Copy(source); mutate(value); Assert.IsFalse(Set(player, role, value)); }
            Assert.IsFalse(player.SetCharacterCardAssemblyAtTick(role, Owner, source, Read(player, role).Revision - 1, revision, 10, Tick(), out _));
            Assert.IsFalse(player.SetCharacterCardAssemblyAtTick(role, Other, source, Read(player, role).Revision, revision, 10, Tick(), out _));
            Assert.IsFalse(player.SetCharacterCardAssemblyAtTick(role, Owner, source, Read(player, role).Revision, revision, 11, Tick(), out _));
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task AccessoryGrantUsesRealInventoryAndIsIdempotentWithPayloadChecks()
        {
            var player = await Open(new MemoryStore()); var role = Role(player); var id = GrantAccessory(player);
            long revision = player.Revision; string before = player.CaptureProgressJson();
            var result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42001), -1, 10, Tick(), out var repeated, out _);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, result.Status); Assert.AreEqual(id, repeated);
            result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42002), revision, 10, Tick(), out _, out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
            Assert.IsTrue(player.TryReadItemInventory(out var inventory, out _)); Assert.IsTrue(inventory.TryGet(new InventoryContainerId(1), out var bag));
            Assert.AreEqual(id, bag.GetStack(1).InstanceId); Assert.AreNotEqual(role, id);
            Assert.IsTrue(player.TryReadOwnedAccessories(Other, out var empty, out _)); Assert.IsEmpty(empty);
            Assert.IsTrue(player.TryReadOwnedAccessories(Owner, out var owned, out _)); owned[0].Reference.Revision++;
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task CapacityAndInvalidGrantFailuresDoNotConsumeAllocatorsOrLedger()
        {
            var player = await Open(new MemoryStore(), capacity: 1); Role(player); string before = player.CaptureProgressJson(); long revision = player.Revision;
            var result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42001), revision, 10, Tick(), out _, out _);
            Assert.AreEqual(ItemAcquisitionStatus.InsufficientCapacity, result.Status);
            result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(2), Owner,
                new ItemDefinitionId(42001), revision, 10, Tick(), out _, out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidContainer, result.Status);
            result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42001), revision - 1, 10, Tick(), out _, out _);
            Assert.AreEqual(ItemAcquisitionStatus.StaleRequest, result.Status);
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task MainCardGrantTargetsCharacterAndPreservesLearningSemantics()
        {
            var player = await Open(new MemoryStore()); var role = Role(player); var other = Role(player, 2); var read = Read(player, role);
            var result = player.GrantCharacterCardAtTick(new ItemAcquisitionRequestId(200), role, Owner, MainReference(), read.Revision,
                player.Revision, 10, Tick(), out var id, out string issue);
            Assert.IsTrue(result.Success, issue); Assert.AreEqual(3, Read(player, role).Cards.Count); Assert.AreEqual(2, Read(player, other).Cards.Count);
            Assert.IsFalse(Read(player, role).Bindings.Any(value => value.CardInstanceId == id)); Assert.AreEqual(1, Read(player, role).ActionGrants.Count);
            string before = player.CaptureProgressJson(); long revision = player.Revision;
            result = player.GrantCharacterCardAtTick(new ItemAcquisitionRequestId(200), role, Owner, MainReference(), -1, -1, 10, Tick(), out var repeated, out _);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, result.Status); Assert.AreEqual(id, repeated);
            result = player.GrantCharacterCardAtTick(new ItemAcquisitionRequestId(200), other, Owner, MainReference(), Read(player, other).Revision,
                revision, 10, Tick(), out _, out _); Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
            result = player.LearnCharacterActionAtTick(new ItemAcquisitionRequestId(200), role, Owner, "action:fixture",
                Read(player, role).Revision, revision, 10, Tick(), out _); Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status);
            Assert.AreEqual(before, player.CaptureProgressJson()); Assert.AreEqual(revision, player.Revision);
        }

        [Test] public async Task UndefinedDuplicateAndSharedCopyPoliciesAreExplicitlyUnsupported()
        {
            var player = await Open(new MemoryStore(), CharacterTacticalCardQuality.Gold); var role = Role(player);
            var first = GrantAccessory(player); var second = GrantAccessory(player, 1, 101);
            string before = player.CaptureProgressJson();
            Assert.IsFalse(player.TryFreezeCardAssembly(role, Owner, Assembly(player, role, 0, first, second),
                CharacterTacticalAssemblyMode.FixedPreassembled, out _, out string issue)); StringAssert.Contains("待定", issue);
            Assert.IsTrue(Set(player, role, Assembly(player, role, 0, first)));
            Assert.IsFalse(player.TryFreezeCardAssembly(role, Owner, Assembly(player, role, 1, first),
                CharacterTacticalAssemblyMode.FixedPreassembled, out _, out issue)); StringAssert.Contains("待定", issue);
            Assert.IsTrue(Set(player, role, Assembly(player, role, 1, second)));
            Assert.AreEqual(2, Read(player, role).CardAssemblies.Count); Assert.AreNotEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task ConfigurationCannotGrantOwnershipOrRewriteFrozenVersions()
        {
            var player = await Open(new MemoryStore()); Role(player); var id = GrantAccessory(player);
            string before = player.CaptureProgressJson(); var config = Configuration(); config.Accessories[0].Reference.Revision++;
            Assert.IsFalse(player.ConfigureCardAssembly(config, out _));
            config.Revision++; config.ContentHash = "next"; Assert.IsFalse(player.ConfigureCardAssembly(config, out _));
            Assert.IsTrue(player.TryReadOwnedAccessories(Owner, out var owned, out _)); Assert.AreEqual(id.Value, owned[0].ItemInstanceId);
            config = Configuration(); Assert.IsTrue(player.ConfigureCardAssembly(config, out _)); config.Accessories[0].TriggerContractId = "tamper";
            Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task LegacySaveRetainsOriginalChainAndMissingConfigurationRejectsOptIn()
        {
            var store = new MemoryStore(); var player = await Open(store, configure: false); var role = Role(player); await player.FlushAsync();
            string before = player.CaptureProgressJson();
            Assert.AreEqual(CharacterTacticalPlayerState.CharacterTacticalSchema, JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(before).Schema);
            Assert.IsFalse(player.TryFreezeCardAssembly(role, Owner, Assembly(player, role, 0),
                CharacterTacticalAssemblyMode.FixedPreassembled, out _, out _));
            var reopened = await Open(store, configure: false); Assert.AreEqual(before, reopened.CaptureProgressJson());
            Assert.IsTrue(reopened.TryGetCharacterItemBindings(role, Owner, Read(reopened, role).Cards[0].InstanceId, out _, out _));
            Assert.IsNull(Read(reopened, role).CardAssemblies); Assert.IsFalse(reopened.TryReadOwnedAccessories(Owner, out _, out _));
        }

        [Test] public async Task SaveFailureRetainsLogicalStateAndRetryDoesNotRegrant()
        {
            var store = new MemoryStore(); var player = await Open(store); var role = Role(player); await player.FlushAsync(); string old = store.Json;
            store.RejectSave = true; var copy = GrantAccessory(player); Assert.IsTrue(Set(player, role, Assembly(player, role, 0, copy)));
            Assert.IsFalse(await player.FlushAsync()); Assert.IsTrue(player.HasUnsavedChanges); Assert.AreEqual(old, store.Json);
            store.RejectSave = false; Assert.IsTrue(await player.FlushAsync()); Assert.IsFalse(player.HasUnsavedChanges);
            var reopened = await Open(store); Assert.AreEqual(player.CaptureProgressJson(), reopened.CaptureProgressJson());
            var result = reopened.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42001), -1, 10, Tick(), out var repeated, out _);
            Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, result.Status); Assert.AreEqual(copy, repeated);
        }

        [Test] public async Task MalformedSchemaProvenanceAndAssemblySaveRejectWithoutRewritingStore()
        {
            var store = new MemoryStore(); var player = await Open(store); var role = Role(player); var copy = GrantAccessory(player);
            Assert.IsTrue(Set(player, role, Assembly(player, role, 0, copy))); await player.FlushAsync(); string valid = store.Json;
            var changes = new Action<CharacterTacticalPlayerState>[] { value => value.Schema = CharacterTacticalPlayerState.CharacterTacticalSchema,
                value => value.AccessoryItems[0].ItemInstanceId = role.Value,
                value => value.AccessoryItems[0].AcquisitionRequestId = 999,
                value => value.AccessoryItems[0].AcquisitionRequestId = 1,
                value => value.CharacterItems[0].CardAssemblies[0].Schema = "future",
                value => value.CharacterItems[0].CardAssemblies[0].MainCard.InstanceId = 999,
                value => value.CharacterItems[0].CardAssemblies[0].Accessories[0].Reference.Revision++ };
            foreach (var change in changes)
            {
                var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(valid); change(state);
                store.Json = JsonConvert.SerializeObject(state); string malformed = store.Json;
                Assert.ThrowsAsync<JsonSerializationException>(async () => await Open(store)); Assert.AreEqual(malformed, store.Json);
            }
        }

        [Test] public async Task PlayerReferencesCannotOverrideFixedContractsOrUseOrdinaryItemBypass()
        {
            var player = await Open(new MemoryStore()); Role(player); string before = player.CaptureProgressJson();
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<CharacterTacticalAccessorySlot>(
                "{\"SlotIndex\":0,\"TriggerContractId\":\"player-edit\"}"));
            var result = player.ExecuteItemTransactionAtTick(new ItemAcquisitionRequest(new ItemAcquisitionRequestId(100),
                ItemAcquisitionSourceKind.DirectGrant, null, new[] { new ItemGrantLine(new InventoryContainerId(1), new ItemDefinitionId(42001), 1) }),
                null, 0, 10, Tick(), out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status); Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public void PublicationStagesRegistryAndCurrentSchemaGateIncludesV6()
        {
            var existing = new ItemDefinitionRegistry(); existing.Register(ItemDefinitionTests.CreateDefinition(41001, "fixture.role", ItemKind.Custom, 1));
            var publication = new CharacterTacticalAssemblyPublication { Schema = CharacterTacticalAssemblyPublication.CurrentSchema,
                Configuration = Configuration(), Items = Enumerable.Range(1, 3).Select(index => new CharacterTacticalAccessoryItemDefinitionConfig {
                    ItemDefinitionId = 42000 + index, StableId = "fixture.accessory." + index, NameKey = "fixture.name", DescriptionKey = "fixture.description", MaxStackCount = 20 }).ToArray() };
            var built = publication.BuildItemDefinitions(existing); Assert.AreEqual(4, built.Definitions.Count); Assert.AreEqual(1, existing.Definitions.Count);
            publication.Items[1].ItemDefinitionId = 41001; Assert.Throws<JsonSerializationException>(() => publication.BuildItemDefinitions(existing));
            Assert.AreEqual(1, existing.Definitions.Count);
            foreach (var schema in new[] { CharacterTacticalPlayerState.CharacterTacticalSchema, CharacterTacticalPlayerState.CardAssemblySchema })
                Assert.IsTrue(CharacterTacticalAssemblyRules.HasCurrentCharacterItems(new CharacterTacticalPlayerState {
                    Schema = schema, CharacterItems = new List<CharacterTacticalCharacterItemState>() }));
            Assert.IsFalse(CharacterTacticalAssemblyRules.HasCurrentCharacterItems(new CharacterTacticalPlayerState {
                Schema = "future", CharacterItems = new List<CharacterTacticalCharacterItemState>() }));
        }

        [Test] public async Task ConfigurationDigestRejectsTamperedContractsAndIgnoresCollectionOrder()
        {
            var player = await Open(new MemoryStore()); var role = Role(player); var copy = GrantAccessory(player);
            Assert.IsTrue(Set(player, role, Assembly(player, role, 0, copy))); string before = player.CaptureProgressJson();
            var config = Configuration(); config.Accessories[0].TriggerContractId = "tampered";
            Assert.IsFalse(player.ConfigureCardAssembly(config, out string issue)); StringAssert.Contains("HASH", issue);
            var reversed = Configuration(); Array.Reverse(reversed.Accessories);
            Assert.AreEqual(reversed.ContentHash, CharacterTacticalAssemblyRules.ConfigurationHash(reversed));
            Assert.IsTrue(player.ConfigureCardAssembly(reversed, out issue), issue);
            Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task UnifiedGrantAndQueryPreserveAllFourSemanticOwners()
        {
            var player = await Open(new MemoryStore());
            CharacterTacticalContentGrantRequest Request(CharacterTacticalContentKind kind, long id) => new() {
                Kind = kind, RequestId = id, OwnerId = 1, Frame = 10, ExpectedPlayerRevision = player.Revision };
            var roleRequest = Request(CharacterTacticalContentKind.Character, 1); roleRequest.TargetContainerId = 1;
            roleRequest.ItemDefinitionId = 41001; roleRequest.DisplayName = "Fixture";
            var receipt = player.GrantContentAtTick(roleRequest, Tick(), out string issue); Assert.IsTrue(receipt.Result.Success, issue);
            var role = receipt.ItemInstanceId;
            var accessory = Request(CharacterTacticalContentKind.Accessory, 100); accessory.TargetContainerId = 1; accessory.ItemDefinitionId = 42001;
            receipt = player.GrantContentAtTick(accessory, Tick(), out issue); Assert.IsTrue(receipt.Result.Success, issue);
            var card = Request(CharacterTacticalContentKind.MainCard, 200); card.CharacterItemInstanceId = role.Value;
            card.MainCard = MainReference(); card.ExpectedCharacterRevision = Read(player, role).Revision;
            receipt = player.GrantContentAtTick(card, Tick(), out issue); Assert.IsTrue(receipt.Result.Success, issue); Assert.Greater(receipt.CardInstanceId, 0UL);
            var action = Request(CharacterTacticalContentKind.Action, 300); action.CharacterItemInstanceId = role.Value;
            action.ActionId = "action:fixture"; action.ExpectedCharacterRevision = Read(player, role).Revision;
            receipt = player.GrantContentAtTick(action, Tick(), out issue); Assert.IsTrue(receipt.Result.Success, issue);
            Assert.IsTrue(player.TryReadOwnedContent(Owner, out var content, out issue), issue);
            Assert.AreEqual(1, content.Characters.Length); Assert.AreEqual(1, content.Accessories.Length);
            Assert.AreEqual(3, content.Characters[0].Cards.Count); Assert.AreEqual(2, content.Characters[0].ActionGrants.Count);
            string before = player.CaptureProgressJson(); content.Characters[0].Cards.Clear(); content.Accessories[0].Reference.Revision++;
            action.MainCard = MainReference(); receipt = player.GrantContentAtTick(action, Tick(), out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, receipt.Result.Status); Assert.AreEqual(before, player.CaptureProgressJson());
        }

        [Test] public async Task LegacyTemplateProgressCannotBeImplicitlyUpgradedByAccessoryGrant()
        {
            var player = await CharacterTacticalPlayerService.OpenAsync(Catalog(), new[] { Defaults() }, new MemoryStore());
            Assert.IsTrue(player.ConfigureCharacterItemInventory(Definitions(), new[] {
                new InventoryContainer(new InventoryContainerId(1), Owner, 12, "fixture.bag") },
                new[] { new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = "fixture.hero" } }, out _));
            Assert.IsTrue(player.ConfigureCardAssembly(Configuration(), out _)); string before = player.CaptureProgressJson();
            var result = player.AcquireAccessoryAtTick(new ItemAcquisitionRequestId(100), new InventoryContainerId(1), Owner,
                new ItemDefinitionId(42001), player.Revision, 10, Tick(), out _, out _);
            Assert.AreEqual(ItemAcquisitionStatus.InvalidRequest, result.Status); Assert.AreEqual(before, player.CaptureProgressJson());
            Assert.IsFalse(player.UsesCharacterItemTacticalProgress);
        }

        [Test] public async Task ExistingSaveGameAggregateRoundTripsAllNewRecordsAndRejectsStaleWriter()
        {
            string fixtureParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxFramework.CardAssemblyFixtures"));
            string root = Path.GetFullPath(Path.Combine(fixtureParent, Guid.NewGuid().ToString("N")));
            try
            {
                var physical = await CharacterTacticalSaveStore.CreateAsync(root, "assembly-fixture", "fixture-json-v1");
                var progress = new CharacterTacticalProgressStore(physical, await physical.LoadAsync()); var candidate = progress.Create();
                var player = await Open(candidate.PlayerStore); var role = Role(player); var accessory = GrantAccessory(player);
                Assert.IsTrue(Set(player, role, Assembly(player, role, 0, accessory)));
                var result = player.GrantCharacterCardAtTick(new ItemAcquisitionRequestId(200), role, Owner, MainReference(), Read(player, role).Revision,
                    player.Revision, 10, Tick(), out _, out _); Assert.IsTrue(result.Success);
                candidate.Bind(player.CaptureProgressJson, () => "{\"fixturePreparation\":true}"); await candidate.SaveAsync(activate: true);
                var secondPhysical = await CharacterTacticalSaveStore.CreateAsync(root, "assembly-fixture", "fixture-json-v1");
                var secondProgress = new CharacterTacticalProgressStore(secondPhysical, await secondPhysical.LoadAsync());
                var restoredCandidate = secondProgress.OpenActive(); var restored = await Open(restoredCandidate.PlayerStore);
                Assert.AreEqual(player.CaptureProgressJson(), restored.CaptureProgressJson()); Assert.IsTrue(restored.UsesCharacterItemTacticalProgress);
                Assert.AreEqual("{\"fixturePreparation\":true}", await restoredCandidate.PreparationStore.LoadAsync());
                restoredCandidate.Bind(restored.CaptureProgressJson, () => "{\"fixturePreparation\":true}");
                GrantAccessory(restored, 2, 101); await restoredCandidate.SaveAsync();
                GrantAccessory(player, 3, 102);
                // Await real I/O instead of NUnit's synchronous ThrowsAsync wait on Unity's main-thread context.
                try { await candidate.SaveAsync(); Assert.Fail("A stale candidate must be rejected."); }
                catch (InvalidOperationException) { }
            }
            finally
            {
                // Literal, freshly-created fixture root only; never touch player progress.
                if (!root.StartsWith(fixtureParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Fixture cleanup escaped its verified parent.");
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
