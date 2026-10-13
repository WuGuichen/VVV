using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Items.NativeRewards
{
    /// <summary>Explicit native-only probe of real publication/composition and independent SaveGame envelopes.
    /// No scene, Play state, static service publication or player persistentDataPath is changed.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalBattleRewardPublishedNativeTests
    {
        [Test] public async Task FormalRecipePendingAndClaimedReopenThroughRealCompositionRoot()
        {
            string parent = Environment.GetEnvironmentVariable("MX_REWARD_FIXTURE_ROOT") ?? Path.Combine(Path.GetTempPath(), "MxRewardNative");
            string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            var physical = await CharacterTacticalSaveStore.CreateAsync(root, "reward-native", "reward-native-json");
            var store = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            var progress = store.Create(); var services = await Build(progress);
            Assert.NotNull(services.RewardRecipe, "The formal fixed reward JSON must have been imported in the coordinated window.");
            var setup = services.Preparation.Working;
            var entity = setup.Entities.Single(value => value.EntityId == setup.PlayerControlledEntityId);
            Assert.IsTrue(entity.CharacterItemInstanceId.HasValue); Assert.IsTrue(entity.InventoryOwnerId.HasValue);
            var role = new ItemInstanceId(entity.CharacterItemInstanceId.Value);
            var owner = new InventoryOwnerId(entity.InventoryOwnerId.Value);
            Assert.IsTrue(services.Player.TryReadCharacterItem(role, owner, out var before, out var issue), issue);
            await progress.SaveAsync(activate: true);
            string originalPlayer = services.Player.CaptureProgressJson(), originalPreparation = services.Preparation.CaptureProgressJson();
            var settlement = new CharacterTacticalBattleSettlement { BattleId = Guid.NewGuid().ToString("N"), PlayerTeamId = entity.TeamId,
                WinningTeamId = entity.TeamId, CompletedAtUtc = DateTime.UtcNow.ToString("O") };
            settlement.Rewards = services.Player.FreezeBattleRewards(services.RewardRecipe, settlement.BattleId, role, owner);
            Assert.AreEqual(1, settlement.Rewards.Lines.Length, "This native probe freezes the explicitly scoped one-main-card trial recipe.");
            Assert.AreEqual(CharacterTacticalContentKind.MainCard, settlement.Rewards.Lines[0].Grant.Kind);
            await progress.SaveAsync(settlement: settlement);
            physical = await CharacterTacticalSaveStore.CreateAsync(root, "reward-native", "reward-native-json");
            store = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            services = await Build(store.OpenActive());
            Assert.AreEqual(originalPlayer, services.Player.CaptureProgressJson());
            Assert.AreEqual(originalPreparation, services.Preparation.CaptureProgressJson());
            Assert.AreEqual(settlement.Rewards.Fingerprint, services.Progress.ReadPendingReward().Rewards.Fingerprint);
            var claim = await ClaimAtTick(services, settlement.BattleId);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.Claimed, claim.Status, claim.Issue);
            Assert.IsTrue(services.Player.TryReadCharacterItem(role, owner, out var after, out issue), issue);
            Assert.AreEqual(before.Cards.Count + 1, after.Cards.Count); Assert.AreEqual(before.ActionGrants.Count, after.ActionGrants.Count);
            Assert.AreEqual(before.Bindings.Count, after.Bindings.Count);
            string expected = services.Player.CaptureProgressJson();
            physical = await CharacterTacticalSaveStore.CreateAsync(root, "reward-native", "reward-native-json");
            store = new CharacterTacticalProgressStore(physical, await physical.LoadAsync());
            services = await Build(store.OpenActive());
            Assert.AreEqual(expected, services.Player.CaptureProgressJson()); Assert.IsNull(services.Progress.ReadPendingReward());
            claim = await ClaimAtTick(services, settlement.BattleId);
            Assert.AreEqual(CharacterTacticalRewardClaimStatus.AlreadyClaimed, claim.Status, claim.Issue);
            Assert.AreEqual(expected, services.Player.CaptureProgressJson());
            File.WriteAllText(Path.Combine(root, "acceptance.json"), JsonConvert.SerializeObject(new {
                passed = true, formalComposition = true, realSaveGameEnvelope = true, pendingRestoredWithoutRegeneration = true,
                claimedReopenDoesNotReaward = true, role = role.Value, owner = owner.Value, card = settlement.Rewards.Lines[0].Grant.MainCard,
                actualPlayerSaveTouched = false, sceneChanged = false, playChanged = false
            }, Formatting.Indented));
        }
        private static Task<CharacterTacticalServiceSet> Build(CharacterTacticalProgressCandidate candidate)
            => (Task<CharacterTacticalServiceSet>)typeof(CharacterTacticalServices).GetMethod("BuildCandidateAsync",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { candidate });
        private static Task<CharacterTacticalRewardClaimResult> ClaimAtTick(CharacterTacticalServiceSet services, string battle)
        {
            Task<CharacterTacticalRewardClaimResult> claim = null;
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new ClaimTick(context => claim = services.Player.ClaimBattleRewardsAsync(services.Progress, battle, context)));
            host.Initialize(); host.Start(); long frame = checked(services.Player.LastItemTransactionFrame + 1);
            host.Tick(frame, 0, frame / 60d); Assert.IsTrue(host.LastTickAttemptSucceeded); Assert.NotNull(claim); return claim;
        }
        private sealed class ClaimTick : RuntimeModule
        {
            private readonly Action<RuntimeTickContext> _action;
            internal ClaimTick(Action<RuntimeTickContext> action) : base("reward-native-claim", RuntimeTickStage.Simulation, 0) => _action = action;
            public override void Tick(RuntimeTickContext context) => _action(context);
        }
    }
}
