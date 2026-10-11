using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests
{
    public sealed class CharacterTacticalPlayerServiceTests
    {
        private sealed class Store : ICharacterTacticalStateStore
        {
            internal string Json;
            internal bool Fail;
            internal int Resets;
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public Task SaveAsync(string value)
            { if (Fail) throw new InvalidOperationException("disk full"); Json = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) { Resets++; return SaveAsync(value); }
        }
        private static CharacterTacticalCardReference Card() => new CharacterTacticalCardReference { CardId = "card", Revision = 1, ContentHash = "hash" };
        private static CharacterTacticalCardReference TestCard() => new CharacterTacticalCardReference { CardId = "test.card", Revision = 1, ContentHash = "test-hash" };
        private static CharacterTacticalContentCatalog Catalog() => new CharacterTacticalContentCatalog {
            Actions = new[] {
                new CharacterTacticalActionDefinition { Id = "action.a", ActionId = 301, ActionLibraryId = "Common", CharacterStableIds = new[] { "char.a", "char.b" } },
                new CharacterTacticalActionDefinition { Id = "action.b", ActionId = 455, ActionLibraryId = "Common", CharacterStableIds = new[] { "char.a", "char.b" } } },
            Cards = new[] {
                new CharacterTacticalCardDefinition { CardId = "card", Revision = 1, ContentHash = "hash", Slots = new[] { "attack", "followup/attack" } },
                new CharacterTacticalCardDefinition { CardId = "test.card", Revision = 1, ContentHash = "test-hash", Slots = Array.Empty<string>() } },
            Acquisition = new[] {
                new CharacterTacticalAcquisitionPool { CharacterStableId = "char.a", ActionIds = new[] { "action.a", "action.b" }, Cards = new[] { Card() } },
                new CharacterTacticalAcquisitionPool { CharacterStableId = "char.b", ActionIds = new[] { "action.a" }, Cards = new[] { Card() } } }
        };
        private static CharacterTacticalDefaults Defaults(string character) => new CharacterTacticalDefaults {
            CharacterStableId = character, ActionIds = new List<string> { "action.a" }, Cards = new List<CharacterTacticalCardReference> { Card() },
            Bindings = new List<CharacterTacticalSlotBinding> {
                new CharacterTacticalSlotBinding { CardId = "card", SlotPath = "attack", TacticalActionId = "action.a" },
                new CharacterTacticalSlotBinding { CardId = "card", SlotPath = "followup/attack", TacticalActionId = "action.a" } }
        };
        private static Task<CharacterTacticalPlayerService> Open(Store store) => CharacterTacticalPlayerService.OpenAsync(Catalog(),
            new[] { Defaults("char.a"), Defaults("char.b") }, store);

        [Test] public async Task ClassificationControlsAcquisitionWithoutGrantingOrRestoringOwnership()
        {
            var catalog = Catalog();
            var rules = new CharacterTacticalActionEligibility { Characters = new[] {
                new CharacterTacticalActionClassRule { CharacterStableId = "char.a", WeaponTypes = new[] { "sword" }, ActionTypes = new[] { "BasicAttack" } }
            } };
            var browser = new CharacterTestActionBrowserData {
                WeaponTypes = new[] { new CharacterTestActionWeaponType { Id = "sword", Label = "剑" }, new CharacterTestActionWeaponType { Id = "blade", Label = "刀" } },
                Actions = new[] {
                    new CharacterTestActionBrowserEntry { ActionId = 301, LibraryId = "Common", Name = "A", Category = MxFramework.CharacterAction.CharacterActionCategory.BasicAttack, WeaponTypes = new[] { "sword" } },
                    new CharacterTestActionBrowserEntry { ActionId = 455, LibraryId = "Common", Name = "B", Category = MxFramework.CharacterAction.CharacterActionCategory.BasicAttack, WeaponTypes = new[] { "blade" } }
                }
            };
            rules.Project(catalog, browser);
            var defaults = Defaults("char.a"); defaults.ActionIds.Clear(); defaults.Bindings.Clear();
            var store = new Store();
            var service = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, store);
            Assert.IsEmpty(service.Read("char.a").ActionIds, "Allowed categories do not grant ownership.");
            Assert.IsFalse(service.TryGrantAction("char.a", "action:455", out _));
            Assert.IsTrue(service.TryGrantAction("char.a", "action:301", out string issue), issue);
            Assert.IsTrue(service.TryGrantAction("char.a", "action.a", out issue), issue);
            Assert.AreEqual(1, service.Read("char.a").ActionIds.Count, "Legacy aliases must not duplicate a physical action.");
            Assert.IsTrue(service.TryRemoveAction("char.a", "action.a", service.Revision, true, out issue), issue);
            await service.FlushAsync();
            defaults.ActionIds.Add("action.a");
            service = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, store);
            Assert.IsEmpty(service.Read("char.a").ActionIds, "Changed defaults cannot replenish an existing player state.");
        }
        private static async Task<TacticalBattlePreparationService> OpenPreparation(Store store, CharacterTacticalPlayerService player = null)
        {
            player ??= await Open(new Store());
            var definitions = new[]
            {
                new CharacterTestCharacterDefinition(101, "游侠", default, 1, default,
                    physicsProfile: default, generatedPackageRoot: string.Empty, tacticalDefaults: Defaults("char.a")),
                new CharacterTestCharacterDefinition(202, "守卫", default, 2, default,
                    physicsProfile: default, generatedPackageRoot: string.Empty, tacticalDefaults: Defaults("char.b"))
            };
            return await TacticalBattlePreparationService.OpenAsync(player, store, definitions);
        }

        [Test] public async Task PublishedCatalogRefreshAddsCardsWithoutGrantingSavingOrChangingPreparation()
        {
            var playerStore = new Store(); var preparationStore = new Store();
            var player = await Open(playerStore);
            var preparation = await OpenPreparation(preparationStore, player);
            Assert.IsTrue(preparation.SaveAsPreset("保留配置", out _, out var issue), issue);
            await preparation.FlushAsync();
            string owned = JsonConvert.SerializeObject(player.Read("char.a"));
            string working = JsonConvert.SerializeObject(preparation.Working);
            string presets = JsonConvert.SerializeObject(preparation.Presets);
            string playerJson = playerStore.Json, preparationJson = preparationStore.Json;
            long playerRevision = player.Revision, preparationRevision = preparation.Revision;
            var services = new CharacterTacticalServiceSet(player, preparation);
            var cards = Catalog().Cards.Concat(new[] { new CharacterTacticalCardDefinition {
                CardId = "fresh.card", Revision = 1, ContentHash = "fresh-hash", Slots = new[] { "attack" } } }).ToArray();
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation, () => cards, out issue), issue);
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation, () => cards, out issue), issue);
            Assert.AreEqual(2, player.CatalogRevision);
            Assert.IsNotNull(player.Catalog.ExactCard(new CharacterTacticalCardReference {
                CardId = "fresh.card", Revision = 1, ContentHash = "fresh-hash" }));
            Assert.IsFalse(player.OwnsCard("char.a", "fresh.card"));
            Assert.AreEqual(owned, JsonConvert.SerializeObject(player.Read("char.a")));
            Assert.AreEqual(working, JsonConvert.SerializeObject(preparation.Working));
            Assert.AreEqual(presets, JsonConvert.SerializeObject(preparation.Presets));
            Assert.AreEqual(playerRevision, player.Revision); Assert.AreEqual(preparationRevision, preparation.Revision);
            Assert.AreEqual(playerJson, playerStore.Json); Assert.AreEqual(preparationJson, preparationStore.Json);
            Assert.IsFalse(player.HasUnsavedChanges); Assert.IsFalse(preparation.HasUnsavedChanges);
            Assert.AreEqual(0, playerStore.Resets + preparationStore.Resets);
            cards.Last().ContentHash = "caller-change";
            Assert.AreEqual("fresh-hash", player.Catalog.Cards.Last().ContentHash);
        }

        [Test] public async Task PublishedCatalogRefreshFailureRetainsPreviousCatalogAndCanRetry()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store(), player);
            var services = new CharacterTacticalServiceSet(player, preparation);
            var previous = player.Catalog.Cards;
            Assert.IsFalse(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation,
                () => throw new InvalidOperationException("目录校验失败"), out var issue));
            StringAssert.Contains("目录校验失败", issue); Assert.AreSame(previous, player.Catalog.Cards);
            Assert.IsFalse(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation,
                () => new[] { Catalog().Cards[0], Catalog().Cards[0] }, out issue));
            StringAssert.Contains("保留原卡库", issue); Assert.AreSame(previous, player.Catalog.Cards);
            Assert.AreEqual(0, player.CatalogRevision); Assert.IsTrue(player.IsCardUsable("char.a", "card"));
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation, () => Catalog().Cards, out issue), issue);
        }

        [TestCase(TacticalPreparationState.Deploying)]
        [TestCase(TacticalPreparationState.Battle)]
        public async Task PublishedCatalogRefreshRefusesBattleBeforeReadingDirectory(TacticalPreparationState phase)
        {
            var player = await Open(new Store());
            var services = new CharacterTacticalServiceSet(player, await OpenPreparation(new Store(), player));
            int reads = 0;
            Assert.IsFalse(services.RefreshPublishedCatalog(phase, () => { reads++; return Catalog().Cards; }, out var issue));
            Assert.AreEqual(0, reads); Assert.AreEqual(0, player.CatalogRevision);
            StringAssert.Contains("返回整备", issue);
        }

        [Test] public async Task PublishedCatalogRefreshReportsStaleReferencesWithoutUpgradingInstancesOrBindings()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store(), player);
            Assert.IsTrue(preparation.SaveAsPreset("旧卡预设", out _, out var issue), issue);
            var services = new CharacterTacticalServiceSet(player, preparation);
            string owned = JsonConvert.SerializeObject(player.Read("char.a"));
            string working = JsonConvert.SerializeObject(preparation.Working);
            string presets = JsonConvert.SerializeObject(preparation.Presets);
            var cards = Catalog().Cards; cards[0].Revision = 2; cards[0].ContentHash = "next-hash";
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation, () => cards, out issue), issue);
            StringAssert.Contains("失效引用", issue);
            Assert.IsNull(player.Catalog.ExactCard(player.FindCard("char.a", "card")));
            Assert.IsFalse(player.IsCardUsable("char.a", "card"));
            Assert.AreEqual(owned, JsonConvert.SerializeObject(player.Read("char.a")));
            Assert.AreEqual(working, JsonConvert.SerializeObject(preparation.Working));
            Assert.AreEqual(presets, JsonConvert.SerializeObject(preparation.Presets));
        }

        [Test] public async Task PublishedCatalogRefreshReportsRemovedCardsWithoutRewritingAcquisitionOrOwnedReferences()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store(), player);
            var services = new CharacterTacticalServiceSet(player, preparation);
            string pools = JsonConvert.SerializeObject(player.Catalog.Acquisition);
            string owned = JsonConvert.SerializeObject(player.Read("char.a"));
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation,
                () => Array.Empty<CharacterTacticalCardDefinition>(), out var issue), issue);
            StringAssert.Contains("失效引用", issue);
            Assert.AreEqual(pools, JsonConvert.SerializeObject(player.Catalog.Acquisition));
            Assert.AreEqual(owned, JsonConvert.SerializeObject(player.Read("char.a")));
            Assert.IsFalse(player.IsCardUsable("char.a", "card"));
        }

        [Test] public async Task PublishedCatalogRefreshPreservesOlderExactVersionWhenNewRevisionIsAdded()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store(), player);
            var services = new CharacterTacticalServiceSet(player, preparation);
            var reference = player.FindCard("char.a", "card");
            var cards = Catalog().Cards.Concat(new[] { new CharacterTacticalCardDefinition {
                CardId = "card", Revision = 2, ContentHash = "r2-hash", Slots = new[] { "attack" } } }).ToArray();
            Assert.IsTrue(services.RefreshPublishedCatalog(TacticalPreparationState.Preparation, () => cards, out var issue), issue);
            Assert.IsNotNull(player.Catalog.ExactCard(reference));
            Assert.IsTrue(player.IsCardUsable("char.a", "card"));
            Assert.AreEqual(reference.InstanceId, player.FindCard("char.a", "card").InstanceId);
            Assert.AreEqual("hash", player.FindCard("char.a", "card").ContentHash);
            StringAssert.DoesNotContain("失效引用", issue);
        }

        [Test] public async Task AcquisitionDoesNotChangeCardBindingsAndIsIdempotent()
        {
            var service = await Open(new Store());
            Assert.IsTrue(service.TryGrantAction("char.a", "action.b", out _));
            long revision = service.Revision;
            Assert.IsTrue(service.TryGrantAction("char.a", "action.b", out _));
            Assert.AreEqual(revision, service.Revision);
            Assert.IsTrue(service.TryGetBindings("char.a", "card", out var bindings, out _));
            Assert.AreEqual(301, bindings["attack"].ActionId);
        }
        [Test] public async Task ExplicitTestButtonCanAddPublishedCardOutsideAcquisitionAndPersistIt()
        {
            var store = new Store();
            var service = await Open(store);
            Assert.IsFalse(service.TryGrantCard("char.a", TestCard(), out _));
            Assert.IsTrue(service.TryGrantCardForTesting("char.a", TestCard(), out var issue), issue);
            Assert.IsTrue(service.OwnsCard("char.a", "test.card"));
            Assert.IsTrue(service.IsCardUsable("char.a", "test.card"));
            Assert.IsTrue(await service.FlushAsync());
            Assert.IsTrue((await Open(store)).OwnsCard("char.a", "test.card"));
        }
        [Test] public async Task BoundRemovalRequiresConfirmationAndDetachesAllReferencesAtomically()
        {
            var service = await Open(new Store()); long revision = service.Revision;
            Assert.IsFalse(service.TryRemoveAction("char.a", "action.a", revision, false, out _));
            Assert.AreEqual(revision, service.Revision);
            Assert.AreEqual(2, service.References("char.a", "action.a").Length);
            Assert.IsTrue(service.TryRemoveAction("char.a", "action.a", revision, true, out _));
            Assert.IsEmpty(service.Read("char.a").Bindings);
            Assert.IsTrue(service.OwnsCard("char.a", "card"));
            Assert.IsFalse(service.TryGetBindings("char.a", "card", out _, out _));
            Assert.IsTrue(service.TryGetBindings("char.b", "card", out _, out _));
        }
        [Test] public async Task ConfirmationCannotRemoveAfterInventoryChanged()
        {
            var service = await Open(new Store()); long old = service.Revision;
            service.TryGrantAction("char.a", "action.b", out _);
            Assert.IsFalse(service.TryRemoveAction("char.a", "action.a", old, true, out _));
            Assert.AreEqual(2, service.References("char.a", "action.a").Length);
        }
        [Test] public async Task DifferentInstanceSlotsBindIndependentlyAndReadCopiesCannotMutateAuthority()
        {
            var service = await Open(new Store());
            service.TryGrantAction("char.a", "action.b", out _);
            Assert.IsTrue(service.TryBind("char.a", "card", "followup/attack", "action.b", out _));
            service.Read("char.a").Bindings.Clear();
            Assert.IsTrue(service.TryGetBindings("char.a", "card", out var bindings, out _));
            Assert.AreEqual(301, bindings["attack"].ActionId);
            Assert.AreEqual(455, bindings["followup/attack"].ActionId);
        }
        [Test] public async Task EmptyInventorySurvivesRestartWithoutDefaultRefill()
        {
            var store = new Store(); var service = await Open(store);
            service.TryRemoveCard("char.a", "card", out _);
            service.TryRemoveAction("char.a", "action.a", service.Revision, true, out _);
            await service.FlushAsync();
            var restored = await Open(store);
            Assert.IsEmpty(restored.Read("char.a").ActionIds);
            Assert.IsEmpty(restored.Read("char.a").Cards);
            Assert.IsNotEmpty(restored.Read("char.b").Cards);
        }
        [Test] public async Task SameRevisionDefaultRepublishRepairsStaleSaveWithoutRefillingRemovedCards()
        {
            var stale = Defaults("char.a");
            stale.Cards[0].ContentHash = "old-hash";
            var removed = Defaults("char.b");
            removed.Cards.Clear();
            removed.Bindings.Clear();
            var store = new Store { Json = JsonConvert.SerializeObject(new CharacterTacticalPlayerState {
                Characters = new List<CharacterTacticalDefaults> { stale, removed }, LegacyImportCompleted = true }) };

            var service = await Open(store);

            Assert.AreEqual("hash", service.Read("char.a").Cards[0].ContentHash);
            Assert.IsTrue(service.IsCardUsable("char.a", "card"));
            Assert.IsEmpty(service.Read("char.b").Cards, "A migration must not refill content the player removed.");
            StringAssert.Contains("\"ContentHash\":\"hash\"", store.Json);
        }
        [Test] public async Task FunctionalResolverCardRemovesLegacyBindingAndRejectsNewPlayerBinding()
        {
            var state = Defaults("char.a");
            state.Cards.Add(TestCard());
            state.Bindings.Add(new CharacterTacticalSlotBinding {
                CardId = "test.card", SlotPath = "action", TacticalActionId = "action.a" });
            var store = new Store { Json = JsonConvert.SerializeObject(new CharacterTacticalPlayerState {
                Characters = new List<CharacterTacticalDefaults> { state }, LegacyImportCompleted = true }) };

            var service = await Open(store);

            Assert.IsFalse(service.Read("char.a").Bindings.Exists(binding => binding.CardId == "test.card"));
            Assert.IsTrue(service.IsCardUsable("char.a", "test.card"),
                "A functional resolver card needs no player binding to remain usable.");
            Assert.IsTrue(service.TryGetBindings("char.a", "test.card", out var bindings, out var issue), issue);
            Assert.IsEmpty(bindings);
            Assert.IsFalse(service.TryBind("char.a", "test.card", "action", "action.a", out issue));
            StringAssert.Contains("槽位引用不可用", issue);
        }
        [Test] public async Task DuplicateDeckEntriesKeepIndependentBindingsOrderAndMonotonicIdentity()
        {
            var store = new Store(); var service = await Open(store);
            Assert.IsTrue(service.TryGrantAction("char.a", "action.b", out var issue), issue);
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong first, out issue), issue);
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong second, out issue), issue);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(service.TryBind("char.a", first, "attack", "action.a", out issue), issue);
            Assert.IsTrue(service.TryBind("char.a", first, "followup/attack", "action.a", out issue), issue);
            Assert.IsTrue(service.TryBind("char.a", second, "attack", "action.b", out issue), issue);
            Assert.IsTrue(service.TryBind("char.a", second, "followup/attack", "action.b", out issue), issue);
            Assert.IsTrue(service.MoveDeckEntry("char.a", second, 0, out issue), issue);
            Assert.IsTrue(await service.FlushAsync());

            service = await Open(store);
            var restored = service.Read("char.a");
            Assert.AreEqual(second, restored.Cards[0].InstanceId);
            Assert.IsTrue(service.TryGetBindings("char.a", first, out var firstBindings, out issue), issue);
            Assert.IsTrue(service.TryGetBindings("char.a", second, out var secondBindings, out issue), issue);
            Assert.AreEqual(301, firstBindings["attack"].ActionId);
            Assert.AreEqual(455, secondBindings["attack"].ActionId);
            Assert.IsTrue(service.RemoveDeckEntry("char.a", first, out issue), issue);
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong third, out issue), issue);
            Assert.Greater(third, second);
        }

        [Test] public async Task V1SaveMigratesCardsAndBindingsToInstanceIdentity()
        {
            var old = Defaults("char.a");
            var store = new Store { Json = JsonConvert.SerializeObject(new CharacterTacticalPlayerState {
                Schema = "mx.character-test.tactical-player.v1",
                Characters = new List<CharacterTacticalDefaults> { old }, LegacyImportCompleted = true }) };
            var service = await Open(store);
            var migrated = service.Read("char.a");
            Assert.Greater(migrated.Cards[0].InstanceId, 0ul);
            Assert.AreEqual(migrated.Cards[0].InstanceId, migrated.Bindings[0].CardInstanceId);
            StringAssert.Contains("mx.character-test.tactical-player.v2", store.Json);
        }
        [Test] public async Task FailedSaveRemainsDirtyAndRetriesWithoutLosingChanges()
        {
            var store = new Store(); var service = await Open(store); store.Fail = true;
            service.TryGrantAction("char.a", "action.b", out _);
            Assert.IsFalse(await service.FlushAsync());
            Assert.IsTrue(service.HasUnsavedChanges);
            Assert.That(service.SaveIssue, Does.Contain("disk full"));
            store.Fail = false;
            Assert.IsTrue(await service.FlushAsync());
            Assert.IsFalse(service.HasUnsavedChanges);
            Assert.Contains("action.b", (await Open(store)).Read("char.a").ActionIds);
        }
        [Test] public async Task ResetThenAddBindRemoveAndReloadPreservesUniqueInstances()
        {
            var store = new Store();
            var service = await Open(store);
            Assert.IsTrue(await service.ResetAsync());
            ulong defaultInstance = service.Read("char.a").Cards[0].InstanceId;
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong first, out string issue), issue);
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong second, out issue), issue);
            Assert.IsTrue(service.TryBind("char.a", first, "attack", "action.a", out issue), issue);
            ulong[] ids = new[] { service.Read("char.a"), service.Read("char.b") }
                .SelectMany(value => value.Cards).Select(value => value.InstanceId).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.IsTrue(service.RemoveDeckEntry("char.a", first, out issue), issue);
            Assert.IsTrue(service.Read("char.a").Cards.Any(card => card.InstanceId == defaultInstance));
            Assert.IsTrue(service.Read("char.a").Cards.Any(card => card.InstanceId == second));
            Assert.IsTrue(service.Read("char.a").Bindings.All(binding => binding.CardInstanceId == defaultInstance));
            Assert.IsTrue(await service.FlushAsync());
            service = await Open(store);
            Assert.AreEqual(2, service.Read("char.a").Cards.Count);
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong third, out issue), issue);
            Assert.Greater(third, second);
        }

        [Test] public async Task FailedResetDoesNotPublishCandidateOrConsumeLiveInstanceIds()
        {
            var store = new Store();
            var service = await Open(store);
            string saved = store.Json;
            string live = JsonConvert.SerializeObject(service.Read("char.a"));
            ulong expected = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(saved).NextCardInstanceId;
            long revision = service.Revision;
            store.Fail = true;
            Assert.IsFalse(await service.ResetAsync());
            Assert.AreEqual(saved, store.Json);
            Assert.AreEqual(live, JsonConvert.SerializeObject(service.Read("char.a")));
            Assert.AreEqual(revision, service.Revision);
            store.Fail = false;
            Assert.IsTrue(service.AddDeckEntry("char.a", Card(), out ulong next, out string issue), issue);
            Assert.AreEqual(expected, next);
        }

        [Test] public async Task DuplicateSavedInstancesAreRejectedWithoutRewriting()
        {
            var store = new Store();
            await Open(store);
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Json);
            state.Characters[0].Cards.Add(JsonConvert.DeserializeObject<CharacterTacticalCardReference>(
                JsonConvert.SerializeObject(state.Characters[0].Cards[0])));
            store.Json = JsonConvert.SerializeObject(state);
            string corrupt = store.Json;
            Assert.ThrowsAsync<JsonSerializationException>(async () => await Open(store));
            Assert.AreEqual(corrupt, store.Json);
        }

        [Test] public async Task MixedLegacyAndAssignedInstancesAreNotSilentlyRenumbered()
        {
            var store = new Store(); await Open(store);
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Json);
            state.Characters[0].Cards.Add(TestCard());
            store.Json = JsonConvert.SerializeObject(state);
            string ambiguous = store.Json;
            Assert.ThrowsAsync<JsonSerializationException>(async () => await Open(store));
            Assert.AreEqual(ambiguous, store.Json);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task InvalidSavedCounterIsRejectedWithoutRewriting(bool zero)
        {
            var store = new Store();
            await Open(store);
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(store.Json);
            state.NextCardInstanceId = zero ? 0UL : state.Characters.SelectMany(value => value.Cards).Max(value => value.InstanceId);
            store.Json = JsonConvert.SerializeObject(state);
            string corrupt = store.Json;
            Assert.ThrowsAsync<JsonSerializationException>(async () => await Open(store));
            Assert.AreEqual(corrupt, store.Json);
        }

        [Test] public async Task ServiceOpeningSharesInflightAndPublishesOnlyCompleteSet()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store());
            var pending = new TaskCompletionSource<CharacterTacticalServiceSet>();
            int calls = 0;
            var opening = new CharacterTacticalServiceOpening(() => { calls++; return pending.Task; });
            Task<CharacterTacticalPlayerService> first = opening.OpenAsync();
            Assert.AreSame(first, opening.OpenAsync());
            Assert.AreEqual(1, calls);
            Assert.IsNull(opening.Published);
            Assert.IsFalse(first.IsCompleted);
            pending.SetResult(new CharacterTacticalServiceSet(player, preparation));
            Assert.AreSame(player, await first);
            Assert.AreSame(player, opening.Published.Player);
            Assert.AreSame(preparation, opening.Published.Preparation);
            Assert.AreSame(player, await opening.OpenAsync());
            Assert.AreEqual(1, calls);
        }

        [Test] public async Task ServiceOpeningFailurePublishesNothingAndCanRetry()
        {
            var player = await Open(new Store());
            var preparation = await OpenPreparation(new Store());
            int calls = 0;
            var opening = new CharacterTacticalServiceOpening(() =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("preparation unavailable");
                return Task.FromResult(new CharacterTacticalServiceSet(player, preparation));
            });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await opening.OpenAsync());
            Assert.IsNull(opening.Published);
            StringAssert.Contains("preparation unavailable", opening.Issue);
            Assert.AreSame(player, await opening.OpenAsync());
            Assert.AreEqual(2, calls);
            Assert.IsEmpty(opening.Issue);
        }

        [Test] public async Task ResetUsesDefaultsAndLeavesSourceDefaultsUnmodified()
        {
            var store = new Store(); var initial = Defaults("char.a");
            var service = await CharacterTacticalPlayerService.OpenAsync(Catalog(), new[] { initial }, store);
            service.TryRemoveCard("char.a", "card", out _);
            Assert.AreEqual(1, initial.Cards.Count);
            Assert.IsTrue(await service.ResetAsync());
            Assert.AreEqual(1, store.Resets);
            Assert.IsTrue(service.OwnsCard("char.a", "card"));
        }
        [Test] public async Task AcquisitionPoolAndUnknownSlotsAreEnforced()
        {
            var service = await Open(new Store());
            Assert.IsFalse(service.TryGrantAction("char.b", "action.b", out _));
            Assert.IsFalse(service.TryBind("char.a", "card", "PrimaryAttack", "action.a", out _));
            Assert.IsFalse(service.TryBind("char.a", "card", "attack", "action.b", out _));
        }
        [Test] public void CorruptSaveIsNotReplacedWithDefaults()
        {
            var store = new Store { Json = "{bad save" };
            Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(async () => await Open(store));
            Assert.AreEqual("{bad save", store.Json);
        }

        [Test] public async Task BattlePreparationKeepsDuplicateTemplatesAndEntityLoadoutsIndependent()
        {
            var store = new Store(); TacticalBattlePreparationService service = await OpenPreparation(store);
            Assert.IsTrue(service.AddEntity(101, new UnityEngine.Vector3(4f, 1f, 5f), 90f, 2,
                out string duplicateId, out string issue), issue);
            TacticalBattleSetup setup = service.Working;
            Assert.AreEqual(3, setup.Entities.Count);
            Assert.AreEqual(2, setup.Entities.Count(entity => entity.CharacterId == 101));
            TacticalBattleEntitySetup duplicate = setup.Entities.Single(entity => entity.EntityId == duplicateId);
            CharacterTacticalCardReference card = duplicate.Loadout.Cards[0];
            Assert.IsTrue(service.BindAction(duplicateId, card.InstanceId, "action.a", out issue), issue);
            Assert.IsTrue(service.AddCard(duplicateId, Card(), out ulong secondCard, out issue), issue);
            Assert.AreNotEqual(card.InstanceId, secondCard);
            Assert.IsTrue(service.SetPose(duplicateId, new UnityEngine.Vector3(7f, 1f, 8f), 135f, out issue), issue);
            Assert.IsTrue(await service.FlushAsync());

            service = await OpenPreparation(store);
            duplicate = service.Working.Entities.Single(entity => entity.EntityId == duplicateId);
            Assert.AreEqual(7f, duplicate.X);
            Assert.AreEqual(135f, duplicate.YawDegrees);
            Assert.AreEqual(2, duplicate.Loadout.Cards.Count);
            Assert.AreEqual(2, duplicate.Loadout.Bindings.Count,
                "Every slotted card should receive a usable default action when it is assembled.");
            Assert.AreEqual("action.a", duplicate.Loadout.Bindings.Single(binding =>
                binding.CardInstanceId == secondCard).TacticalActionId);
            Assert.AreEqual(1, service.Working.Entities[0].Loadout.Cards.Count,
                "Editing one entity must not mutate another entity created from the same template.");
        }

        [Test] public async Task BattlePreparationRejectsPositionsOutsideTheBaseYard()
        {
            var store = new Store();
            TacticalBattlePreparationService service = await OpenPreparation(store);
            Assert.IsTrue(service.AddEntity(101, new UnityEngine.Vector3(38f, 0f, 28f), 0f, 1,
                out string entityId, out string issue), issue);
            Assert.IsFalse(service.SetPose(entityId, new UnityEngine.Vector3(40f, 0f, 0f), 0f, out issue));
            Assert.That(issue, Does.Contain("战区"));
            Assert.IsFalse(service.AddEntity(101, new UnityEngine.Vector3(0f, 0f, 30f), 0f, 1,
                out _, out issue));
            Assert.IsTrue(await service.FlushAsync());
            TacticalBattlePreparationState saved = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json);
            saved.Working.Entities[0].X = 45f;
            store.Json = JsonConvert.SerializeObject(saved);
            service = await OpenPreparation(store);
            Assert.IsFalse(service.ValidateForBattle(out issue));
            Assert.That(issue, Does.Contain("战区外"));
        }

        [Test] public async Task BattlePreparationRepairsHistoricalMissingActionBindingsOnLoad()
        {
            var store = new Store();
            TacticalBattlePreparationService service = await OpenPreparation(store);
            Assert.IsTrue(await service.FlushAsync());
            TacticalBattlePreparationState state = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json);
            Assert.NotNull(state);
            state.Working.Entities[0].Loadout.Bindings.Clear();
            store.Json = JsonConvert.SerializeObject(state);

            service = await OpenPreparation(store);
            TacticalBattleEntitySetup repaired = service.Working.Entities[0];
            Assert.AreEqual(repaired.Loadout.Cards.Count, repaired.Loadout.Bindings.Count);
            Assert.IsTrue(repaired.Loadout.Bindings.All(binding => binding.TacticalActionId == "action.a"));
            Assert.IsTrue(await service.FlushAsync());
            StringAssert.Contains("action.a", store.Json);
        }

        [Test] public async Task NamedBattlePresetsAreIndependentSnapshots()
        {
            TacticalBattlePreparationService service = await OpenPreparation(new Store());
            Assert.IsTrue(service.SaveAsPreset("初始", out string first, out string issue), issue);
            string entityId = service.Working.Entities[0].EntityId;
            Assert.IsTrue(service.SetPose(entityId, new UnityEngine.Vector3(9f, 1f, 2f), 180f, out issue), issue);
            Assert.IsTrue(service.SaveAsPreset("变体", out string second, out issue), issue);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(service.LoadPreset(first, out issue), issue);
            Assert.AreNotEqual(9f, service.Working.Entities[0].X);
            Assert.IsTrue(service.LoadPreset(second, out issue), issue);
            Assert.AreEqual(9f, service.Working.Entities[0].X);
        }

        [Test] public async Task BattlePreparationPersistsExactlyOneOptionalPlayerEntity()
        {
            var store = new Store();
            TacticalBattlePreparationService service = await OpenPreparation(store);
            string first = service.Working.Entities[0].EntityId;
            string second = service.Working.Entities[1].EntityId;
            Assert.IsTrue(service.SetPlayerControlledEntity(first, out string issue), issue);
            Assert.AreEqual(first, service.Working.PlayerControlledEntityId);
            Assert.IsTrue(service.SetPlayerControlledEntity(second, out issue), issue);
            Assert.AreEqual(second, service.Working.PlayerControlledEntityId);
            Assert.IsTrue(service.SaveAsPreset("玩家控制", out string preset, out issue), issue);
            Assert.IsTrue(await service.FlushAsync());

            service = await OpenPreparation(store);
            Assert.AreEqual(second, service.Working.PlayerControlledEntityId);
            Assert.AreEqual(second, service.Presets.Single(value => value.PresetId == preset)
                .Snapshot.PlayerControlledEntityId);
            Assert.IsTrue(service.RemoveEntity(second, out issue), issue);
            Assert.IsEmpty(service.Working.PlayerControlledEntityId);
        }

        [Test] public async Task BattlePreparationV1MigratesToOptionalAutomaticControl()
        {
            var store = new Store();
            TacticalBattlePreparationService service = await OpenPreparation(store);
            Assert.IsTrue(await service.FlushAsync());
            TacticalBattlePreparationState state = JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json);
            state.Schema = "mx.character-test.tactical-battle-preparation.v1";
            state.Working.PlayerControlledEntityId = null;
            store.Json = JsonConvert.SerializeObject(state);

            service = await OpenPreparation(store);
            Assert.AreEqual(TacticalBattlePreparationState.CurrentSchema,
                JsonConvert.DeserializeObject<TacticalBattlePreparationState>(store.Json).Schema);
            Assert.IsEmpty(service.Working.PlayerControlledEntityId);
        }
    }
}
