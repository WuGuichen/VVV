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
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class TacticalBattleRewardTests
    {
        private static readonly InventoryOwnerId Owner = new(1);
        private static RuntimeTickContext Tick(long frame = 30) => new(frame, 0, frame / 60d, RuntimeTickStage.Simulation);
        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            internal string Json;
            internal int Writes;
            internal bool Reject, SaveThenThrow;
            internal TaskCompletionSource<bool> Gate;
            public Task<string> LoadAsync() => Task.FromResult(Json);
            public async Task SaveAsync(string json)
            {
                Writes++;
                if (Gate != null) await Gate.Task;
                if (Reject) throw new IOException("injected precommit failure");
                Json = json;
                if (SaveThenThrow) { SaveThenThrow = false; throw new IOException("injected lost completion receipt"); }
            }
            public Task ResetAsync(string json) => SaveAsync(json);
        }
        private sealed class Fixture
        {
            internal ICharacterTacticalStateStore Store;
            internal CharacterTacticalProgressStore Owner;
            internal CharacterTacticalProgressCandidate Progress;
            internal CharacterTacticalPlayerService Player;
            internal ItemInstanceId Role;
            internal CharacterTacticalBattleSettlement Settlement;
        }
        private sealed class FaultingDiskStore : ICharacterTacticalStateStore
        {
            private readonly ICharacterTacticalStateStore _disk;
            internal bool Reject, SaveThenThrow;
            internal int Writes;
            internal FaultingDiskStore(ICharacterTacticalStateStore disk) => _disk = disk;
            public Task<string> LoadAsync() => _disk.LoadAsync();
            public async Task SaveAsync(string json)
            {
                Writes++;
                if (Reject) throw new IOException("injected physical precommit failure");
                await _disk.SaveAsync(json);
                if (SaveThenThrow) { SaveThenThrow = false; throw new IOException("injected physical lost completion receipt"); }
            }
            public Task ResetAsync(string json) => throw new InvalidOperationException("Reward fault fixture cannot reset.");
        }
        private sealed class ProcessCrashStore : ICharacterTacticalStateStore
        {
            private readonly ICharacterTacticalStateStore _disk;
            private readonly bool _afterCommit;
            internal ProcessCrashStore(ICharacterTacticalStateStore disk, bool afterCommit) { _disk = disk; _afterCommit = afterCommit; }
            public Task<string> LoadAsync() => _disk.LoadAsync();
            public async Task SaveAsync(string json)
            {
                if (_afterCommit) await _disk.SaveAsync(json);
                Console.WriteLine(_afterCommit ? "READY_AFTER_COMMIT" : "READY_BEFORE_COMMIT");
                await Task.Delay(System.Threading.Timeout.Infinite);
            }
            public Task ResetAsync(string json) => throw new InvalidOperationException("Crash probe cannot reset.");
        }

        // Invoked only by the independent offline runner, in separate child processes with an explicit isolated root.
        internal static async Task<int> ProcessProbe(string phase, string root)
        {
            ICharacterTacticalStateStore disk = await CharacterTacticalSaveStore.CreateAsync(root, "reward-process-test", "reward-json");
            Fixture f;
            CharacterTacticalRewardClaimResult result = null;
            if (phase == "pending") { f = await Create(disk); await Pending(f); }
            else
            {
                if (phase.StartsWith("crash-", StringComparison.Ordinal)) disk = new ProcessCrashStore(disk, phase == "crash-after");
                f = await Reopen(new Fixture { Store = disk, Settlement = new CharacterTacticalBattleSettlement { BattleId = "fixture-battle" } });
                f.Role = new(f.Settlement.Rewards.CharacterItemInstanceId);
                if (phase != "inspect") result = await Claim(f);
                if (result != null) Assert.IsTrue(result.Success, result.Issue);
            }
            var settlement = f.Progress.ReadSettlement("fixture-battle");
            var state = State(f);
            Console.WriteLine(JsonConvert.SerializeObject(new { phase, status = result?.Status.ToString(),
                rewardState = settlement.Rewards.State.ToString(), settlement.Rewards.Fingerprint,
                cards = state.CharacterItems.Single().Cards.Select(value => value.InstanceId).ToArray(),
                receipts = settlement.Rewards.Receipts, profile = f.Progress.ProfileId }));
            return 0;
        }
        private static CharacterTacticalCardReference Card() => new() { CardId = "reward.main", Revision = 2, ContentHash = "published-v2" };
        private static CharacterTacticalContentCatalog Catalog() => new() {
            Cards = new[] { new CharacterTacticalCardDefinition { CardId = Card().CardId, Revision = 2, ContentHash = Card().ContentHash,
                DisplayName = "Fixed main", Slots = new[] { "behavior.action" } } },
            Actions = new[] { new CharacterTacticalActionDefinition { Id = "action:reward", DisplayName = "Fixed action", ActionId = 301,
                ActionLibraryId = "fixture.library", CharacterStableIds = new[] { "fixture.hero" } } },
            Acquisition = new[] { new CharacterTacticalAcquisitionPool { CharacterStableId = "fixture.hero",
                ActionIds = new[] { "action:reward" }, Cards = new[] { Card() } } }
        };
        private static CharacterTacticalDefaults Initial() => new() { CharacterStableId = "fixture.hero",
            ActionIds = new List<string> { "action:reward" }, Cards = new List<CharacterTacticalCardReference> {
                new() { InstanceId = 1, CardId = Card().CardId, Revision = 2, ContentHash = Card().ContentHash } },
            Bindings = new List<CharacterTacticalSlotBinding> { new() { CardInstanceId = 1, CardId = Card().CardId,
                SlotPath = "behavior.action", TacticalActionId = "action:reward" } } };
        private static CharacterTacticalAssemblyConfiguration Assembly()
        {
            var result = new CharacterTacticalAssemblyConfiguration { Schema = CharacterTacticalAssemblyConfiguration.CurrentSchema,
                Revision = 1, MainCards = new[] { new CharacterTacticalMainCardDefinition { Card = Card(), Quality = CharacterTacticalCardQuality.Silver } },
                Accessories = new[] { new CharacterTacticalAccessoryDefinition { ItemDefinitionId = 42001,
                    Reference = new() { AccessoryId = "reward.accessory", Revision = 1, ContentHash = "published-accessory-v1" },
                    Quality = CharacterTacticalCardQuality.Silver, TriggerContractId = "fixture.trigger", FailureContractId = "fixture.failure",
                    LifetimeContractId = "fixture.lifetime" } } };
            result.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(result); return result;
        }
        private static async Task<CharacterTacticalPlayerService> Player(CharacterTacticalProgressCandidate progress, int capacity = 8,
            CharacterTacticalContentCatalog catalog = null, bool configureAssembly = true)
        {
            var player = await CharacterTacticalPlayerService.OpenAsync(catalog ?? Catalog(), new[] { Initial() }, progress.PlayerStore,
                addMissingDefaults: false, instantiateTemplateDefaults: false);
            var defs = new ItemDefinitionRegistry();
            defs.Register(ItemDefinitionTests.CreateDefinition(41001, "fixture.role", ItemKind.Custom, 1));
            defs.Register(ItemDefinitionTests.CreateDefinition(42001, "reward.accessory", ItemKind.Custom, 1));
            Assert.IsTrue(player.ConfigureCharacterItemInventory(defs, new[] {
                new InventoryContainer(new(1), Owner, capacity, "fixture.player"),
                new InventoryContainer(new(2), new(2), capacity, "fixture.opponent") }, new[] {
                new CharacterTacticalCharacterItemDefinition { ItemDefinitionId = 41001, CharacterStableId = "fixture.hero" } }, out var issue), issue);
            if (configureAssembly) Assert.IsTrue(player.ConfigureCardAssembly(Assembly(), out issue), issue);
            return player;
        }
        private static CharacterTacticalRewardRecipe Recipe(params CharacterTacticalContentGrantRequest[] entries) => new() {
            RecipeId = "fixture.fixed-reward", Revision = 1, Entries = entries.Length == 0 ? new[] {
                new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.MainCard, MainCard = Card() },
                new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.MainCard, MainCard = Card() } } : entries };
        private static async Task<Fixture> Create(ICharacterTacticalStateStore store = null, int capacity = 8)
        {
            store ??= new MemoryStore();
            var owner = new CharacterTacticalProgressStore(store, await store.LoadAsync());
            var progress = owner.Create(); var player = await Player(progress, capacity);
            var acquired = player.AcquireCharacterItemAtTick(new(new(1), ItemAcquisitionSourceKind.DirectGrant, null, null),
                new(1), Owner, new(41001), "Reward Hero", player.Revision, 10, Tick(10), out var role, out var issue);
            Assert.IsTrue(acquired.Success, issue);
            progress.Bind(player.CaptureProgressJson, () => "fixture-preparation");
            await progress.SaveAsync(activate: true); progress.EnableAutosave();
            return new Fixture { Store = store, Owner = owner, Progress = progress, Player = player, Role = role };
        }
        private static async Task Pending(Fixture fixture, CharacterTacticalRewardRecipe recipe = null, string battle = "fixture-battle")
        {
            fixture.Settlement = new CharacterTacticalBattleSettlement { BattleId = battle, WinningTeamId = 1, PlayerTeamId = 1,
                CompletedAtUtc = DateTime.UtcNow.ToString("O"), Rewards = fixture.Player.FreezeBattleRewards(recipe ?? Recipe(), battle, fixture.Role, Owner) };
            await fixture.Progress.SaveAsync(settlement: fixture.Settlement);
        }
        private static async Task<Fixture> Reopen(Fixture fixture, int capacity = 8)
        {
            var owner = new CharacterTacticalProgressStore(fixture.Store, await fixture.Store.LoadAsync());
            var progress = owner.OpenActive(); var player = await Player(progress, capacity);
            progress.Bind(player.CaptureProgressJson, () => "fixture-preparation");
            foreach (var result in progress.ReadSettlements()) player.ValidateRewardReceipts(result.Rewards);
            return new Fixture { Store = fixture.Store, Owner = owner, Progress = progress, Player = player, Role = fixture.Role,
                Settlement = progress.ReadSettlement(fixture.Settlement?.BattleId ?? "fixture-battle") };
        }
        private static CharacterTacticalPlayerState State(Fixture fixture) => JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(fixture.Player.CaptureProgressJson());
        private static Task<CharacterTacticalRewardClaimResult> Claim(Fixture fixture) => fixture.Player.ClaimBattleRewardsAsync(
            fixture.Progress, fixture.Settlement.BattleId, Tick());

        [Test] public async Task PendingReopenAndClaimedReopenNeverRegenerateOrRepeatGrants()
        {
            var f = await Create(); await Pending(f); string original = f.Player.CaptureProgressJson();
            var pending = await Reopen(f); Assert.AreEqual(original, pending.Player.CaptureProgressJson());
            Assert.AreEqual(f.Settlement.Rewards.Fingerprint, pending.Settlement.Rewards.Fingerprint);
            var result = await Claim(pending); Assert.IsTrue(result.Success, result.Issue);
            Assert.AreEqual(3, State(pending).CharacterItems.Single().Cards.Count);
            var claimed = await Reopen(pending); int writes = ((MemoryStore)f.Store).Writes;
            var prior = claimed.Player.CaptureProgressJson(); result = await Claim(claimed);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, result.Status, result.Issue);
            Assert.AreEqual(prior, claimed.Player.CaptureProgressJson()); Assert.AreEqual(writes, ((MemoryStore)f.Store).Writes);
        }
        [Test] public async Task AllFourKindsJoinOneDurableBatchAndTrueOwners()
        {
            var f = await Create();
            await Pending(f, Recipe(new() { Kind = CharacterTacticalContentKind.MainCard, MainCard = Card() },
                new() { Kind = CharacterTacticalContentKind.Accessory, ItemDefinitionId = 42001, TargetContainerId = 1 },
                new() { Kind = CharacterTacticalContentKind.Action, ActionId = "action:reward" },
                new() { Kind = CharacterTacticalContentKind.Character, ItemDefinitionId = 41001, TargetContainerId = 1, DisplayName = "Awarded Role" }));
            int writes = ((MemoryStore)f.Store).Writes; var result = await Claim(f);
            Assert.IsTrue(result.Success, result.Issue); Assert.AreEqual(writes + 1, ((MemoryStore)f.Store).Writes);
            var state = State(f); Assert.AreEqual(2, state.CharacterItems.Count); Assert.AreEqual(1, state.AccessoryItems.Count);
            var target = state.CharacterItems.Single(value => value.ItemInstanceId == f.Role.Value);
            Assert.AreEqual(2, target.Cards.Count); Assert.AreEqual(1, target.ActionGrants.Count(value => value.Source == "direct"));
            Assert.AreEqual(4, result.Settlement.Rewards.Receipts.Length); Assert.AreEqual(0, target.Bindings.Count(value => value.CardInstanceId == target.Cards.Last().InstanceId));
            Assert.IsFalse(f.Player.TryReadCharacterItem(f.Role, new(2), out _, out _));
            var reopened = await Reopen(f); Assert.AreEqual(f.Player.CaptureProgressJson(), reopened.Player.CaptureProgressJson());
        }
        [Test] public async Task SaveFailureKeepsEntirePlayerAllocatorLedgerAndPendingThenRetriesExactlyOnce()
        {
            var f = await Create(); await Pending(f); var store = (MemoryStore)f.Store;
            string before = f.Player.CaptureProgressJson(), disk = store.Json; long revision = f.Player.Revision;
            store.Reject = true; var result = await Claim(f);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.SaveFailed, result.Status, result.Issue);
            Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.AreEqual(revision, f.Player.Revision); Assert.AreEqual(disk, store.Json);
            Assert.AreEqual(CharacterTacticalRewardState.Pending, f.Progress.ReadPendingReward().Rewards.State);
            store.Reject = false; result = await Claim(f);
            Assert.IsTrue(result.Success, result.Issue); var reopened = await Reopen(f);
            Assert.AreEqual(3, State(reopened).CharacterItems.Single().Cards.Count);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(reopened)).Status);
        }
        [Test] public async Task DuplicateClickWhileSavingIsBusyAndPublishesNothingUntilSaveFinishes()
        {
            var f = await Create(); await Pending(f); var store = (MemoryStore)f.Store;
            string before = f.Player.CaptureProgressJson(); store.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Claim(f); Assert.IsFalse(first.IsCompleted);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.Busy, (await Claim(f)).Status);
            Assert.AreEqual(before, f.Player.CaptureProgressJson());
            Assert.IsFalse(f.Player.TryRenameCharacterItemAtTick(f.Role, Owner, "blocked", 1, f.Player.Revision, 30, Tick(), out _));
            store.Gate.SetResult(true); Assert.IsTrue((await first).Success);
            Assert.AreEqual(3, State(f).CharacterItems.Single().Cards.Count);
        }
        [Test] public async Task QueuedOrdinarySaveCapturesAfterClaimAndCannotEraseIt()
        {
            var f = await Create(); await Pending(f); var store = (MemoryStore)f.Store;
            store.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var claim = Claim(f); var save = f.Progress.SaveAsync(); Assert.IsFalse(save.IsCompleted);
            store.Gate.SetResult(true); Assert.IsTrue((await claim).Success); await save;
            var reopened = await Reopen(f); Assert.AreEqual(CharacterTacticalRewardState.Claimed, reopened.Settlement.Rewards.State);
            Assert.AreEqual(3, State(reopened).CharacterItems.Single().Cards.Count);
        }
        [Test] public async Task LostCompletionReceiptIsReadBackAndCannotReaward()
        {
            var f = await Create(); await Pending(f); ((MemoryStore)f.Store).SaveThenThrow = true;
            var result = await Claim(f); Assert.IsTrue(result.Success, result.Issue);
            Assert.AreEqual(3, State(f).CharacterItems.Single().Cards.Count);
            var reopened = await Reopen(f); Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(reopened)).Status);
        }
        [Test] public async Task ConflictingFrozenPayloadRejectsWithoutAnyMutation()
        {
            var f = await Create(); await Pending(f); string before = f.Player.CaptureProgressJson(); int writes = ((MemoryStore)f.Store).Writes;
            Assert.IsTrue(f.Player.TryRefreshPublishedCards(new[] { new CharacterTacticalCardDefinition { CardId = Card().CardId,
                Revision = 3, ContentHash = "published-v3", Slots = new[] { "behavior.action" } } }, out _, out _) == false);
            // Configuration validation correctly blocks that refresh. Inject an invalid second frozen line through a distinct new result.
            var corrupted = CharacterTacticalRewardRules.Copy(f.Settlement);
            corrupted.Rewards.Lines[1].Grant.MainCard.ContentHash = "missing";
            corrupted.Rewards.Fingerprint = CharacterTacticalRewardRules.Fingerprint(corrupted.Rewards);
            try { await f.Progress.SaveAsync(settlement: corrupted); Assert.Fail("Conflicting frozen intent accepted."); } catch (InvalidOperationException) { }
            Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.AreEqual(writes, ((MemoryStore)f.Store).Writes);
        }
        [Test] public async Task LaterEntryWithChangedActionDefinitionRejectsEntireDetachedBatch()
        {
            var f = await Create(); await Pending(f, Recipe(new() { Kind = CharacterTacticalContentKind.MainCard, MainCard = Card() },
                new() { Kind = CharacterTacticalContentKind.Action, ActionId = "action:reward" }));
            var updated = Catalog(); updated.Actions[0].ActionLibraryId = "changed-library";
            f.Owner = new CharacterTacticalProgressStore(f.Store, await f.Store.LoadAsync()); f.Progress = f.Owner.OpenActive();
            f.Player = await Player(f.Progress, catalog: updated); f.Progress.Bind(f.Player.CaptureProgressJson, () => "fixture-preparation");
            string before = f.Player.CaptureProgressJson(); int writes = ((MemoryStore)f.Store).Writes;
            var result = await Claim(f); Assert.AreEqual(CharacterTacticalRewardClaimStatus.Rejected, result.Status);
            Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.AreEqual(writes, ((MemoryStore)f.Store).Writes);
            Assert.IsNotNull(f.Progress.ReadPendingReward());
        }
        [Test] public async Task MainCardOnlyRewardNeedsNoUnpublishedAssemblyConfiguration()
        {
            var f = await Create();
            f.Owner = new CharacterTacticalProgressStore(f.Store, await f.Store.LoadAsync()); f.Progress = f.Owner.OpenActive();
            f.Player = await Player(f.Progress, configureAssembly: false); f.Progress.Bind(f.Player.CaptureProgressJson, () => "fixture-preparation");
            await Pending(f); var result = await Claim(f); Assert.IsTrue(result.Success, result.Issue);
            Assert.AreEqual(3, State(f).CharacterItems.Single().Cards.Count);
            var reopened = f.Owner.OpenActive(); var player = await Player(reopened, configureAssembly: false);
            player.ValidateRewardReceipts(reopened.ReadSettlement(f.Settlement.BattleId).Rewards);
        }
        [Test] public async Task WrongRuntimeStageRejectsWithoutTouchingPlayerOrDisk()
        {
            var f = await Create(); await Pending(f); string before = f.Player.CaptureProgressJson(); int writes = ((MemoryStore)f.Store).Writes;
            var result = await f.Player.ClaimBattleRewardsAsync(f.Progress, f.Settlement.BattleId,
                new RuntimeTickContext(30, 0, 0, RuntimeTickStage.PreSimulation));
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.Rejected, result.Status); Assert.AreEqual(before, f.Player.CaptureProgressJson());
            Assert.AreEqual(writes, ((MemoryStore)f.Store).Writes);
        }
        [Test] public async Task CapacityFailureOnLaterEntryDiscardsEarlierStagedGrants()
        {
            var f = await Create(capacity: 1); await Pending(f, Recipe(new() { Kind = CharacterTacticalContentKind.MainCard, MainCard = Card() },
                new() { Kind = CharacterTacticalContentKind.Accessory, ItemDefinitionId = 42001, TargetContainerId = 1 }));
            string before = f.Player.CaptureProgressJson(); var result = await Claim(f);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.Rejected, result.Status);
            Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.IsNotNull(f.Progress.ReadPendingReward());
        }
        [Test] public async Task DefeatDrawAndLegacySettlementHaveNoClaimOrRetroactiveGeneration()
        {
            foreach (var result in new[] { new CharacterTacticalBattleSettlement { BattleId = "defeat", PlayerTeamId = 1, WinningTeamId = 2 },
                new CharacterTacticalBattleSettlement { BattleId = "draw", PlayerTeamId = 1, Draw = true },
                new CharacterTacticalBattleSettlement { BattleId = "legacy", WinningTeamId = 1 } })
            {
                var f = await Create(); await f.Progress.SaveAsync(settlement: result); f.Settlement = result;
                string before = f.Player.CaptureProgressJson(); Assert.AreEqual(CharacterTacticalRewardClaimStatus.Rejected, (await Claim(f)).Status);
                Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.IsNull(f.Progress.ReadPendingReward());
            }
        }
        [Test] public async Task DuplicateTerminalCallbackPreservesClaimedStateAndOriginalReceipt()
        {
            var f = await Create(); await Pending(f); Assert.IsTrue((await Claim(f)).Success);
            await f.Progress.SaveAsync(settlement: f.Settlement);
            Assert.AreEqual(CharacterTacticalRewardState.Claimed, f.Progress.ReadSettlement(f.Settlement.BattleId).Rewards.State);
            var reopened = await Reopen(f); Assert.AreEqual(3, State(reopened).CharacterItems.Single().Cards.Count);
        }
        [Test] public async Task ForeignOwnerAndUnpublishedRecipeRejectBeforeCreatingEntitlement()
        {
            var f = await Create(); string before = f.Player.CaptureProgressJson();
            Assert.Throws<InvalidOperationException>(() => f.Player.FreezeBattleRewards(Recipe(), "foreign", f.Role, new(2)));
            var recipe = Recipe(); recipe.Entries[1].MainCard.ContentHash = "wrong";
            Assert.Throws<InvalidOperationException>(() => f.Player.FreezeBattleRewards(recipe, "invalid", f.Role, Owner));
            Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.IsNull(f.Progress.ReadPendingReward());
        }
        [Test] public async Task SnapshotCopiesCannotRewriteManifestOrGrants()
        {
            var f = await Create(); await Pending(f); var copy = f.Progress.ReadPendingReward();
            copy.Rewards.Lines[0].Grant.OwnerId = 99; copy.Rewards.Lines[0].Label = "fake";
            Assert.AreEqual(1, f.Progress.ReadPendingReward().Rewards.OwnerId);
            Assert.AreEqual("Fixed main", f.Progress.ReadPendingReward().Rewards.Lines[0].Label);
        }
        [Test] public async Task StaleCandidateOrArchiveCannotOverwriteClaimedCheckpoint()
        {
            var f = await Create(); await Pending(f); var old = f.Owner.OpenActive();
            string stale = f.Player.CaptureProgressJson(); old.Bind(() => stale, () => "old-preparation");
            var archive = old.CaptureSnapshot(); Assert.IsTrue((await Claim(f)).Success);
            try { await old.SaveAsync(); Assert.Fail("Stale candidate accepted."); } catch (InvalidOperationException) { }
            var fresh = f.Owner.Create(stale, "new-preparation"); fresh.Bind(() => stale, () => "new-preparation");
            try { await fresh.SaveAsync(activate: true, archive: archive); Assert.Fail("Stale archive accepted."); } catch (InvalidOperationException) { }
            var reopened = await Reopen(f); Assert.AreEqual(CharacterTacticalRewardState.Claimed, reopened.Settlement.Rewards.State);
        }
        [Test] public async Task CorruptReceiptAndDowngradedAggregateSchemaAreRejectedWithoutGranting()
        {
            var f = await Create(); await Pending(f); Assert.IsTrue((await Claim(f)).Success);
            var json = JObject.Parse(((MemoryStore)f.Store).Json);
            json["Schema"] = "mx.character-test.tactical-slice-progress.v1";
            Assert.Throws<JsonSerializationException>(() => new CharacterTacticalProgressStore(f.Store, json.ToString()));
            json["Schema"] = CharacterTacticalRewardRules.ProgressSchema;
            json["Profiles"][0]["Settlements"][0]["Rewards"]["Receipts"][0]["CardInstanceId"] = 999UL;
            var candidate = new CharacterTacticalProgressStore(f.Store, json.ToString()).OpenActive();
            var player = await Player(candidate);
            Assert.Throws<JsonSerializationException>(() => player.ValidateRewardReceipts(candidate.ReadSettlements()[0].Rewards));
        }
        [Test] public async Task RealSaveGameEnvelopeReopensPendingAndClaimedExactlyOnce()
        {
            string fixtureRoot = Environment.GetEnvironmentVariable("MX_REWARD_FIXTURE_ROOT") ?? Path.Combine(Path.GetTempPath(), "MxBattleRewards");
            string root = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
            var disk = await CharacterTacticalSaveStore.CreateAsync(root, "reward-test", "reward-json");
            var f = await Create(disk); await Pending(f);
            var nextStore = await CharacterTacticalSaveStore.CreateAsync(root, "reward-test", "reward-json");
            f.Store = nextStore; f = await Reopen(f); Assert.IsNotNull(f.Progress.ReadPendingReward()); Assert.IsTrue((await Claim(f)).Success);
            f.Store = await CharacterTacticalSaveStore.CreateAsync(root, "reward-test", "reward-json");
            f = await Reopen(f); Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(f)).Status);
            Assert.AreEqual(3, State(f).CharacterItems.Single().Cards.Count);
            // Retain this isolated root for evidence; never open or modify the player's persistentDataPath.
        }
        [TestCase(false)] [TestCase(true)]
        public async Task PhysicalSaveFaultWithIsoDatesRetriesSameOwnerOrRecoversLostReceipt(bool afterCommit)
        {
            string fixtureRoot = Environment.GetEnvironmentVariable("MX_REWARD_FIXTURE_ROOT") ?? Path.Combine(Path.GetTempPath(), "MxBattleRewards");
            string root = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
            var store = new FaultingDiskStore(await CharacterTacticalSaveStore.CreateAsync(root, "reward-fault-test", "reward-json"));
            var f = await Create(store); await Pending(f);
            string before = f.Player.CaptureProgressJson(), disk = await store.LoadAsync();
            Assert.IsFalse(string.IsNullOrEmpty(f.Progress.CaptureSnapshot().CreatedAtUtc));
            Assert.IsFalse(string.IsNullOrEmpty(f.Settlement.CompletedAtUtc));
            store.Reject = !afterCommit; store.SaveThenThrow = afterCommit;
            var result = await Claim(f);
            if (!afterCommit)
            {
                Assert.AreEqual(CharacterTacticalRewardClaimStatus.SaveFailed, result.Status, result.Issue);
                Assert.AreEqual(before, f.Player.CaptureProgressJson()); Assert.AreEqual(disk, await store.LoadAsync());
                Assert.IsNotNull(f.Progress.ReadPendingReward());
                store.Reject = false; result = await Claim(f);
            }
            Assert.IsTrue(result.Success, result.Issue);
            int writes = store.Writes;
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(f)).Status);
            Assert.AreEqual(writes, store.Writes);
            f.Store = await CharacterTacticalSaveStore.CreateAsync(root, "reward-fault-test", "reward-json");
            var reopened = await Reopen(f);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(reopened)).Status);
            Assert.AreEqual(3, State(reopened).CharacterItems.Single().Cards.Count);
        }
        [Test] public async Task TwoRealSaveGameWritersCannotRepeatOrOverwriteAClaim()
        {
            string fixtureRoot = Environment.GetEnvironmentVariable("MX_REWARD_FIXTURE_ROOT") ?? Path.Combine(Path.GetTempPath(), "MxBattleRewards");
            string root = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
            var f = await Create(await CharacterTacticalSaveStore.CreateAsync(root, "reward-cas-test", "reward-json")); await Pending(f);
            var stale = new Fixture { Store = await CharacterTacticalSaveStore.CreateAsync(root, "reward-cas-test", "reward-json"),
                Role = f.Role, Settlement = f.Settlement };
            stale = await Reopen(stale); string before = stale.Player.CaptureProgressJson();
            Assert.IsTrue((await Claim(f)).Success);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.SaveFailed, (await Claim(stale)).Status);
            Assert.AreEqual(before, stale.Player.CaptureProgressJson());
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.SaveFailed, (await Claim(stale)).Status);
            stale.Store = await CharacterTacticalSaveStore.CreateAsync(root, "reward-cas-test", "reward-json"); stale = await Reopen(stale);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, (await Claim(stale)).Status);
            Assert.AreEqual(3, State(stale).CharacterItems.Single().Cards.Count);
        }
    }
}
