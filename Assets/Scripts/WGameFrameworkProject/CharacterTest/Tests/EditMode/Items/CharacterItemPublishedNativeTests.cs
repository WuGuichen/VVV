using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.Items;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Items
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterItemPublishedNativeTests
    {
        public static string ValidationRoot;
        [UnityEngine.TestTools.UnitySetUp]
        public System.Collections.IEnumerator EnterNativePlayMode()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
        }
        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator ExitNativePlayMode()
        {
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
        [Test]
        public async Task PublishedNewProfile_LearnBindUnbindReopenAndDeploy_UsesRealItemsAndEnvelope()
        {
            Assert.IsTrue(Application.isPlaying, "Run in the graphical Editor's isolated PlayMode session.");
            string root = ValidationRoot ?? Path.Combine(Path.GetTempPath(), "MxCharacterItemNative-" + Guid.NewGuid().ToString("N"));
            var physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
            Assert.IsNull(await physical.LoadAsync(), "This acceptance creates a separate new save root.");
            var progress = new CharacterTacticalProgressStore(physical, null);
            var candidate = progress.Create();
            var services = await Build(candidate);
            await candidate.SaveAsync(activate: true);
            var player = services.Player;
            Assert.IsTrue(player.TryReadOwnedCharacterItems(services.InventoryOwner, out var owned, out var issue), issue);
            Assert.AreEqual(1, owned.Length, "The formal recipe grants one owned player item, not every discovered template.");
            var item = new ItemInstanceId(owned[0].ItemInstanceId);
            var entity = services.Preparation.Working.Entities.Single(value => value.CharacterItemInstanceId == item.Value);
            Assert.AreEqual(services.InventoryOwner.Value, entity.InventoryOwnerId);
            Assert.IsTrue(services.Preparation.TryCaptureWorking(out _, out issue), issue);
            var initial = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson());
            Assert.AreEqual(CharacterTacticalPlayerState.CharacterTacticalSchema, initial.Schema);
            Assert.AreEqual(2, initial.CharacterItems.Count);
            Assert.AreEqual(2, initial.CharacterItems.Select(value => value.ItemInstanceId).Distinct().Count());
            Assert.AreEqual(0, initial.Characters.Count);
            string opponentBefore = JsonConvert.SerializeObject(initial.CharacterItems.Single(value => value.ItemInstanceId != item.Value));
            var card = owned[0].Cards.First(value => player.Catalog.ExactCard(value).Slots.Length > 0);
            string slot = player.Catalog.ExactCard(card).Slots[0];
            const string action = "action:302";
            Assert.NotNull(player.Catalog.Action(action));
            Run(1, tick => {
                Assert.IsTrue(player.TryReadCharacterItem(item, services.InventoryOwner, out var role, out var readIssue), readIssue);
                Assert.IsTrue(player.LearnCharacterActionAtTick(new ItemAcquisitionRequestId(3), item, services.InventoryOwner,
                    action, role.Revision, player.Revision, tick.FrameIndex, tick, out var learnIssue).Success, learnIssue);
            });
            Bind(services, entity.EntityId, item, card.InstanceId, slot, action, 2);
            Assert.IsTrue(player.TryReadCharacterItem(item, services.InventoryOwner, out var bound, out issue), issue);
            Assert.AreEqual(action, bound.Bindings.Single(value => value.CardInstanceId == card.InstanceId && value.SlotPath == slot).TacticalActionId);
            Bind(services, entity.EntityId, item, card.InstanceId, slot, string.Empty, 3);
            Assert.IsTrue(player.TryReadCharacterItem(item, services.InventoryOwner, out var unbound, out issue), issue);
            Assert.IsFalse(unbound.Bindings.Any(value => value.CardInstanceId == card.InstanceId && value.SlotPath == slot));
            Bind(services, entity.EntityId, item, card.InstanceId, slot, action, 4);
            Assert.IsTrue(await player.FlushAsync());
            Assert.IsTrue(await services.Preparation.FlushAsync());
            await candidate.SaveAsync(activate: true);
            string expectedPlayer = player.CaptureProgressJson(), expectedPreparation = services.Preparation.CaptureProgressJson();
            var saved = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(expectedPlayer);
            Assert.AreEqual(opponentBefore, JsonConvert.SerializeObject(saved.CharacterItems.Single(value => value.ItemInstanceId != item.Value)));
            var reopenedPhysical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
            var reopenedProgress = new CharacterTacticalProgressStore(reopenedPhysical, await reopenedPhysical.LoadAsync());
            var reopened = await Build(reopenedProgress.OpenActive());
            Assert.AreEqual(expectedPlayer, reopened.Player.CaptureProgressJson());
            Assert.AreEqual(expectedPreparation, reopened.Preparation.CaptureProgressJson());
            Assert.IsTrue(reopened.Player.TryReadCharacterItem(item, services.InventoryOwner, out var restored, out issue), issue);
            Assert.AreEqual(1, restored.ActionGrants.Count(value => value.RequestId == 3));
            Assert.IsTrue(reopened.Preparation.TryCaptureWorking(out var restoredSetup, out issue), issue);
            Assert.IsTrue(TacticalDeployment.TryCreate(reopened.Player, restoredSetup, out var deployment, out issue), issue);
            Assert.AreEqual(item.Value, deployment.PlayerParticipant.CharacterItemInstanceId);
            Assert.AreEqual(services.InventoryOwner.Value, deployment.PlayerParticipant.InventoryOwnerId);
            Assert.AreEqual(restored.Revision, deployment.PlayerParticipant.CharacterItemRevision);
            var opening = (CharacterTacticalServiceOpening)typeof(CharacterTacticalServices).GetField("Opening", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previous = opening.Published;
            try
            {
                opening.Publish(reopened);
                var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                Assert.NotNull(manager);
                manager.RuntimeContext.SessionBuilder.RebuildTacticalPreparationSession("Isolated published role-item acceptance");
                // Published character resources/spawns complete on later player-loop updates.
                for (int attempt = 0; attempt < 360 && manager.RuntimeContext.Slice.Services.CharacterEntities.Count != deployment.Participants.Count; attempt++)
                    await Task.Delay(16);
                Assert.AreEqual(deployment.Participants.Count, manager.RuntimeContext.Slice.Services.CharacterEntities.Count, "Both actual configured actors must be ready before admission.");
                var flow = manager.GetComponent<TacticalCardSliceCoordinator>();
                var args = new object[] { deployment, null };
                Assert.IsTrue((bool)typeof(TacticalCardSliceCoordinator).GetMethod("TryDeployPreparedCore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, args), args[1] as string);
                for (int attempt = 0; attempt < 360 && flow.State == TacticalPreparationState.Deploying; attempt++)
                    await Task.Delay(16);
                Assert.NotNull(manager.RuntimeContext.Slice.Services.Modules.MixedPlayerTacticalStage);
                Assert.AreEqual(TacticalPreparationState.Battle, flow.State);
                Assert.AreEqual(deployment.Participants.Count, manager.RuntimeContext.Slice.Services.CharacterEntities.Count);
            }
            finally { opening.Publish(previous); }
            File.WriteAllText(Path.Combine(root, "acceptance.json"), JsonConvert.SerializeObject(new {
                passed = true, realPublishedLoader = true, playerOwnedItems = owned.Length, itemInstance = item.Value,
                owner = services.InventoryOwner.Value, learningRequest = 3, bindUnbind = true, realRuntimeHost = true,
                realEnvelopeReopen = true, exactPlayerAndPreparationRetained = true, opponentUnchanged = true,
                realBattleSessionPublished = true, actualPlayerSaveTouched = false
            }, Formatting.Indented));
        }
        private static Task<CharacterTacticalServiceSet> Build(CharacterTacticalProgressCandidate candidate)
            => (Task<CharacterTacticalServiceSet>)typeof(CharacterTacticalServices).GetMethod("BuildCandidateAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { candidate });
        private static void Bind(CharacterTacticalServiceSet services, string entity, ItemInstanceId item, ulong card,
            string slot, string action, long frame)
        {
            Assert.IsTrue(services.Player.TryReadCharacterItem(item, services.InventoryOwner, out var role, out var issue), issue);
            bool? completed = null;
            Assert.IsTrue(services.RoleBindings.Enqueue(entity, item, services.InventoryOwner, card, slot, action,
                role.Revision, services.Player.Revision, services.Preparation.Revision, services.Player.CatalogRevision,
                () => true, (success, message) => { completed = success; issue = message; }, out issue), issue);
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(() => services.RoleBindings, () => true));
            host.Initialize(); host.Start(); host.Tick(frame, 1d / 60d, frame / 60d);
            Assert.AreEqual(true, completed, issue);
        }
        private static void Run(long frame, Action<RuntimeTickContext> action)
        {
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new TickAction(action)); host.Initialize(); host.Start(); host.Tick(frame, 1d / 60d, frame / 60d);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
        }
        private sealed class TickAction : RuntimeModule
        {
            private readonly Action<RuntimeTickContext> _action;
            internal TickAction(Action<RuntimeTickContext> action) : base("native-role-learning", RuntimeTickStage.Simulation, 0) { _action = action; }
            public override void Tick(RuntimeTickContext context) => _action(context);
        }
    }
}
