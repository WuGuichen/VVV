using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Items
{
    // Engine-native acceptance, not included in a noEngine harness. Uses existing action/ability fixture identities.
    [Parallelizable(ParallelScope.None)]
    public sealed class ActualMixedPausedSessionRestoreTests
    {
        private float _entryScale;
        [SetUp] public void Setup() => _entryScale = Time.timeScale;
        [TearDown] public void Teardown() => Time.timeScale = _entryScale;

        private sealed class Fixture : IDisposable
        {
            internal readonly TacticalDeployment Deployment;
            internal readonly CharacterTestTacticalStageAuthoringDocument Config = new CharacterTestTacticalStageAuthoringDocument();
            internal readonly CharacterTestPausedSessionOwner Owner;
            internal int FactoryCalls;
            internal bool FailFactory, EpochCurrent = true, AliasAbility, AliasPhysics, ChangedFreshFloor;
            private readonly int _additionalFee;
            internal GameSlice Slice => Owner.Current;
            internal TacticalMixedPlayerCardRuntime Mixed => Slice.Services.Modules.MixedPlayerTacticalStage;
            internal Fixture(int additionalFee = 0, Action<GameSlice> beforeHostStart = null)
            {
                _additionalFee = additionalFee;
                var cards = new TacticalCardDefinition[] {
                    new TacticalActionCardDefinition("too-expensive", "A", "", 35, "bc_mme0my92jb", 1, "8924602348978526995", 1),
                    new TacticalActionCardDefinition("payable-a", "B", "", 30, "bc_mme0my92jb", 1, "8924602348978526995", 2),
                    new TacticalActionCardDefinition("payable-b", "C", "", 30, "bc_mme0my92jb", 1, "8924602348978526995", 3) };
                // Existing legacy lab constructor: explicit temporary actors, never claimed to be persistent owned Items.
                var constructor = typeof(TacticalDeployment).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(c => c.GetParameters().Length == 7);
                Deployment = (TacticalDeployment)constructor.Invoke(new object[] {
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    "temporary-paused-lab", 50, cards, new Dictionary<ulong, ReadOnlyDictionary<string,int>> {
                        [1] = new ReadOnlyDictionary<string,int>(new Dictionary<string,int> { ["c06d387c51e64863b0bbc7036fcc4927/primary"] = 301 }),
                        [2] = new ReadOnlyDictionary<string,int>(new Dictionary<string,int> { ["c06d387c51e64863b0bbc7036fcc4927/primary"] = 301 }),
                        [3] = new ReadOnlyDictionary<string,int>(new Dictionary<string,int> { ["c06d387c51e64863b0bbc7036fcc4927/primary"] = 301 }) }, false });
                var initialOptions = Options();
                // This fixture explicitly spawns its two actors; null would also enqueue legacy startup actors.
                initialOptions.StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>();
                var slice = new GameSlice(initialOptions);
                try {
                    var definitions = new CharacterTestCharacterDefinitionRegistry();
                    Assert.IsTrue(definitions.TryGet(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, out var definition));
                    var world = slice.CharacterRuntime.World;
                    var discarded = world.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 9, 0, 0, 0,
                        CharacterTestCharacterControlOwner.RemotePlayer), definition);
                    Assert.IsTrue(world.Remove(discarded.Id)); // runtime 2 maps to Gameplay index 1, generation 2.
                    var player = world.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 1, 0, 0, 0,
                        CharacterTestCharacterControlOwner.LocalPlayer), definition);
                    world.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 2, 0, 0, 4,
                        CharacterTestCharacterControlOwner.RemotePlayer), definition);
                    // Leave two freed Gameplay slots in a nonascending FIFO order (4, then 3); the player stays runtime 2 / Gameplay 1 generation 2.
                    var freeFirst = world.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 9, 0, 0, 0,
                        CharacterTestCharacterControlOwner.RemotePlayer), definition);
                    var freeSecond = world.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, 9, 0, 0, 0,
                        CharacterTestCharacterControlOwner.RemotePlayer), definition);
                    Assert.AreEqual(new GameplayEntityId(3, 1), freeFirst.GameplayEntityId);
                    Assert.AreEqual(new GameplayEntityId(4, 1), freeSecond.GameplayEntityId);
                    Assert.IsTrue(world.Remove(freeSecond.Id)); Assert.IsTrue(world.Remove(freeFirst.Id));
                    Assert.IsTrue(slice.RuntimeCommands.Enqueue(CharacterTestRuntimeCommandFactory.SetCameraMode(RuntimeFrame.Zero,
                        CharacterTestRuntimeCommandSources.System, CharacterTestCameraMode.MultiTargetAuto)).Success);
                    world.SetCurrentAttribute(player.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 30);
                    world.SyncCombatStateHealth(player);
                    for (int i = 0; i < 240; i++) {
                        slice.Tick(1d / 60d);
                        slice.Services.Modules.MixedPlayerTacticalStage.TickWorldTimeTransition(1f / 60f);
                        var stage = slice.Services.Modules.MixedPlayerTacticalStage.Stage;
                        if (stage.Phase == TacticalStagePhase.Planning && stage.LastCompletedFrame >= 0) break;
                    }
                    var context = new CharacterTestPausedRestoreContext(slice, "lab", "native-new-session", "paused-restore",
                        "native-fixed-published-epoch", Deployment, Config, FreshOptions, () => EpochCurrent);
                    Owner = new CharacterTestPausedSessionOwner(slice, context, beforeHostStart);
                } catch { slice.Dispose(); throw; }
            }
            private static T Reuse<T>(string method, params object[] args) => (T)typeof(global::MxFramework.Tests.Game.CharacterTest.CharacterTestActionUseAuthorityTests)
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            internal CharacterTestSliceOptions Options() => new CharacterTestSliceOptions {
                EnableStoryBootstrap = false, CreateCharacterViews = false, EnableLocalInputCommands = false,
                SuppressMissingStartupSpawnWarning = true, MixedPlayerTacticalStage = Config, TacticalDeployment = Deployment,
                PhysicsRuntime = FixedFloor(), FreshAbilityRuntime = CreateAbility,
                FreshActionRuntime = () => Reuse<CharacterTestCharacterActionRuntime>("RestrictedRuntime") };
            private static CharacterTestCharacterPhysicsRuntime FixedFloor(int extent = 100)
            {
                // Same formal level builder as the scene bridge; fixture data is in memory and has no formal ID allocation.
                var floor = new CombatLevelAabbColliderDefinition("reference-ground",
                    CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart, CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart,
                    1, 1, CombatLevelFixedVector3.Zero,
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(-extent), Fix64.FromInt(-1), Fix64.FromInt(-extent))),
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(extent), Fix64.Zero, Fix64.FromInt(extent))),
                    CombatLevelSurfaceMaterialDefinition.Default);
                var level = new CombatLevelDefinition(CombatLevelDefinition.CurrentSchemaVersion, "paused-lab-fixed-floor", "Paused Lab",
                    new[] { floor }, new[] { new CombatLevelPointDefinition("fixture-spawn", CombatLevelFixedVector3.Zero) }, Array.Empty<CombatLevelPointDefinition>());
                var world = CombatLevelWorldBuilder.BuildWorld(level);
                return new CharacterTestCharacterPhysicsRuntime(world, CombatPhysicsLayerMask.FromLayer(1),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), Fix64.Zero, Fix64.Zero,
                    staticColliderCount: world.TotalColliderCount, sourceDescription: "level:paused-lab-fixed-floor");
            }
            private AbilityRuntimeModule CreateAbility()
            {
                var original = Reuse<AbilityRuntimeModule>("TransactionOwner", 60L);
                if (_additionalFee == 0) return original;
                var registry = new AbilityDefinitionRegistry();
                foreach (var d in original.Definitions.CreateSnapshot()) {
                    var costs = d.Costs.Concat(new[] { new AbilityCostConfig(_additionalFee, 1) }).ToArray();
                    Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(d.AbilityId, d.AbilityStableId, d.DisplayName,
                        d.Input, d.Cooldown, costs, d.TargetPolicyId, d.ActionBinding, d.Damage, d.PresentationProfileId,
                        d.EffectTimelineId, d.TargetPolicyLabel, d.PresentationProfileLabel, d.EffectTimelineLabel), out _));
                }
                return new AbilityRuntimeModule(registry, original.ModuleId);
            }
            private CharacterTestSliceOptions FreshOptions()
            {
                FactoryCalls++;
                if (FailFactory) throw new InvalidOperationException("actual factory fault");
                var options = Options();
                if (AliasAbility) options.FreshAbilityRuntime = () => Owner.Current.AbilityRuntime;
                if (AliasPhysics) options.PhysicsRuntime = Owner.Current.PhysicsRuntime;
                if (ChangedFreshFloor) options.PhysicsRuntime = FixedFloor(90);
                return options;
            }
            internal TacticalCardInstance Card(string id) => Mixed.Stage.Deck.Hand.Single(c => c.Definition.Id == id);
            internal TacticalStageCommand Command(TacticalStageCommandKind kind, TacticalCardInstance card) =>
                new TacticalStageCommand(Mixed.Stage.StageRunId, Mixed.Stage.TurnIndex, card?.InstanceId ?? 0,
                    card?.Version ?? 0, Mixed.Stage.SnapshotRevision, kind);
            internal TacticalStageRejection Apply(TacticalStageCommandKind kind, TacticalCardInstance card)
            { var command = Command(kind, card); return Owner.Apply(Owner.Lease, in command); }
            internal void CompleteActualTurn()
            {
                if (Mixed.Stage.Phase == TacticalStagePhase.Planning) {
                    var card = Card("payable-a");
                    Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.SelectAction, card));
                    Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.ConfirmExecution, card), Mixed.Stage.ExecutionIssue);
                } else Assert.AreEqual(TacticalStagePhase.Executing, Mixed.Stage.Phase);
                ulong before = Mixed.Stage.TurnIndex;
                long firstFrame = Slice.CurrentFrame.Value;
                for (int i = 0; i < 1200; i++) {
                    Mixed.TickWorldTimeTransition(1f / 60f);
                    Slice.Tick(1d / 60d);
                    if (i == 0) {
                        Assert.AreEqual(firstFrame + 1, Slice.CurrentFrame.Value, "Exactly the first next authority frame is consumed.");
                        Assert.IsTrue(Slice.CharacterRuntime.TryGetLastCompletedRuntimeFrame(out var completed));
                        Assert.AreEqual(firstFrame, completed.Value);
                    }
                    if (Mixed.Stage.Phase == TacticalStagePhase.Planning && Mixed.Stage.TurnIndex > before) return;
                }
                Assert.Fail("Actual Mixed/RuntimeHost did not complete a real action turn: " + Mixed.Stage.ExecutionIssue);
            }
            public void Dispose() => Owner.Dispose();
        }

        [Test] public void ActualMixedFreshSessionRestoresExactProjectionAllocatorAndHostClock()
        {
            using (var f = new Fixture()) {
                var before = f.Slice; var beforeAdapter = f.Mixed.PausedAdapter;
                long frame = before.CurrentFrame.Value; int runtime = f.Mixed.Player.Id.Value;
                var gameplay = f.Mixed.Player.GameplayEntityId;
                Assert.AreNotEqual(runtime, gameplay.Index); Assert.AreEqual(2, gameplay.Generation);
                int allocator = before.CharacterRuntime.World.PausedNextRuntimeEntityId;
                string json = f.Owner.Capture();
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreNotSame(before, f.Slice); Assert.AreNotSame(beforeAdapter, f.Mixed.PausedAdapter);
                Assert.AreEqual(runtime, f.Mixed.Player.Id.Value); Assert.AreEqual(gameplay, f.Mixed.Player.GameplayEntityId);
                Assert.AreEqual(allocator, f.Slice.CharacterRuntime.World.PausedNextRuntimeEntityId);
                Assert.AreEqual(frame, f.Slice.CurrentFrame.Value);
                Assert.AreEqual(frame - 1, f.Mixed.Stage.LastCompletedFrame);
                Assert.AreEqual(RuntimeLifecycleState.Started, f.Slice.Host.State); Assert.AreEqual(0f, Time.timeScale);
                Assert.AreEqual(30, f.Slice.CharacterRuntime.World.GetCurrentAttribute(f.Mixed.Player, CharacterTestCharacterAttributeIds.Hp));
                Assert.Throws<InvalidOperationException>(() => before.Tick(1d / 60d));
                f.CompleteActualTurn(); // Uses fresh actual adapter, session, action gateway, commands and Host.
            }
        }
        [Test] public void ActualAdapterRetainsMergedFeeGateAndPendingOwnReservationCredit()
        {
            using (var f = new Fixture()) {
                Assert.IsFalse(f.Mixed.PausedAdapter.EvaluateCardAvailability(f.Card("too-expensive"), 0).IsLegal);
                var payable = f.Card("payable-a");
                Assert.IsTrue(f.Mixed.PausedAdapter.EvaluateCardAvailability(payable, 0).IsLegal);
                Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, payable));
                var pending = f.Mixed.Stage.Deck.PendingAction; ulong token = f.Mixed.Stage.Deck.CaptureSessionState(f.Mixed.Stage.SnapshotDefinitions()).PendingToken;
                string json = f.Owner.Capture(); var old = f.Slice;
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreNotSame(old, f.Slice); Assert.AreEqual(30, f.Mixed.Stage.Ledger.Reserved);
                Assert.AreEqual(token, f.Mixed.Stage.Deck.CaptureSessionState(f.Mixed.Stage.SnapshotDefinitions()).PendingToken);
                Assert.AreEqual(pending.InstanceId, f.Mixed.Stage.Deck.PendingAction.InstanceId);
                Assert.IsFalse(f.Mixed.PausedAdapter.EvaluateCardAvailability(f.Card("too-expensive"), 0).IsLegal);
                Assert.IsTrue(f.Mixed.PausedAdapter.EvaluateCardAvailability(f.Mixed.Stage.Deck.PendingAction, 30).IsLegal);
                Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.CancelPending, f.Mixed.Stage.Deck.PendingAction));
                Assert.AreEqual(50, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Reserved);
            }
        }
        [Test] public void W1CompletedRealTurnAndPausedAgainRejectsFrozenRestoreBeforeFactory()
        {
            using (var f = new Fixture()) {
                string json = f.Owner.Capture(); var old = f.Slice; ulong lease = f.Owner.Lease;
                long frozen = f.Mixed.Stage.LastCompletedFrame;
                f.CompleteActualTurn();
                Assert.Greater(f.Mixed.Stage.LastCompletedFrame, frozen);
                Assert.AreEqual(TacticalStagePhase.Planning, f.Mixed.Stage.Phase);
                int calls = f.FactoryCalls;
                Assert.IsFalse(f.Owner.TryRestore(json, out string issue));
                StringAssert.Contains("current-whole-frame-barrier", issue);
                Assert.AreEqual(calls, f.FactoryCalls); Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease);
                Assert.AreEqual(0, f.Mixed.Stage.Ledger.Current, "Old completed turn remains paid; no rewind to 50.");
                Assert.Throws<InvalidOperationException>(() => f.Owner.Capture());
            }
        }
        [TestCase("factory")][TestCase("alias")][TestCase("epoch")][TestCase("bad-world")]
        public void FailedActualRestorePreservesOldWorldQueriesControlsAndPayment(string mode)
        {
            using (var f = new Fixture()) {
                string json = f.Owner.Capture(); var old = f.Slice; var adapter = f.Mixed.PausedAdapter; ulong lease = f.Owner.Lease;
                if (mode == "factory") f.FailFactory = true;
                if (mode == "alias") f.AliasAbility = true;
                if (mode == "epoch") f.EpochCurrent = false;
                if (mode == "bad-world") { var root = JObject.Parse(json); root["World"] = "{}"; json = root.ToString(); }
                Assert.IsFalse(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreSame(old, f.Slice); Assert.AreSame(adapter, f.Mixed.PausedAdapter); Assert.AreEqual(lease, f.Owner.Lease);
                Assert.AreEqual(0f, Time.timeScale); Assert.AreEqual(50, f.Mixed.Stage.Ledger.Current);
                f.CompleteActualTurn(); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Current);
            }
        }
        [Test] public void MissingHpCannotBeRecreatedFromNetworkProjectionAndOldLeaseIsRejected()
        {
            using (var f = new Fixture()) {
                string json = f.Owner.Capture(); var root = JObject.Parse(json);
                var world = JObject.Parse((string)root["World"]);
                var payloadField = world.SelectTokens("$..payloadJson").Single();
                var payload = JObject.Parse((string)payloadField);
                var stores = (JArray)payload["componentStores"];
                var attributeStore = stores.Single(s => ((string)s["schemaId"]).Contains("attribute"));
                attributeStore["entries"] = new JArray();
                payloadField.Replace(new JValue(payload.ToString())); root["World"] = world.ToString();
                Assert.IsFalse(f.Owner.TryRestore(root.ToString(), out string issue));
                StringAssert.Contains("actual-hp-projection-join", issue);
                var card = f.Card("payable-a"); var command = f.Command(TacticalStageCommandKind.SelectAction, card); ulong lease = f.Owner.Lease;
                Assert.IsTrue(f.Owner.TryRestore(json, out issue), issue);
                Assert.AreEqual(TacticalStageRejection.StaleSnapshot, f.Owner.Apply(lease, in command));
                Assert.AreEqual(50, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Reserved);
            }
        }
        [Test] public void OldUnclassifiedTacticalDtoAndMalformedEnvelopeNeverCallActualFactory()
        {
            using (var f = new Fixture()) {
                Assert.IsFalse(f.Owner.TryRestore("{\"Version\":2}", out _));
                string json = f.Owner.Capture();
                Assert.IsFalse(f.Owner.TryRestore(json + "{}", out _));
                Assert.AreEqual(0, f.FactoryCalls);
            }
        }
        [Test] public void RestoredActualAdapterSharesActionCooldownAcrossCardsAndKeepsOtherRuntimeActorIndependent()
        {
            using (var f = new Fixture()) {
                var ability = f.Slice.AbilityRuntime;
                int actor = f.Mixed.Player.Id.Value;
                long completed = f.Mixed.Stage.LastCompletedFrame;
                typeof(AbilityRuntimeModule).GetMethod("RestoreRuntimeState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ability, new object[] { new AbilityRuntimeState(AbilityRuntimeState.CurrentSchemaVersion,
                        ability.ModuleId, ability.Definitions.ComputeStableHash(), new[] { new AbilityRuntimeCooldownEntry(actor, 93001, completed + 40) }) });
                string json = f.Owner.Capture();
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreEqual(39, f.Mixed.PausedAdapter.EvaluateCardAvailability(f.Card("payable-a"), 0).RemainingCooldownFrames);
                Assert.AreEqual(39, f.Mixed.PausedAdapter.EvaluateCardAvailability(f.Card("payable-b"), 0).RemainingCooldownFrames);
                var other = f.Mixed.CurrentTarget;
                f.Slice.CharacterRuntime.World.BindTacticalMotion(other.Id.Value, new MomentumLedger(50));
                Assert.IsTrue(f.Slice.AbilityRuntime.Definitions.TryGet(93001, out var definition));
                Assert.IsTrue(f.Slice.AbilityRuntime.TryEvaluate(other.Id.Value, definition, f.Slice.CurrentFrame, out var result));
                Assert.AreEqual(0, result.RemainingCooldownFrames);
                Assert.AreEqual(50, f.Mixed.Stage.Ledger.Current);
                Assert.AreEqual(30, f.Slice.CharacterRuntime.World.GetCurrentAttribute(f.Mixed.Player, CharacterTestCharacterAttributeIds.Hp));
            }
        }

        [TestCase("missing-stamina")][TestCase("stamina-mismatch")][TestCase("stamina-base-mismatch")]
        [TestCase("missing-cost-attribute")][TestCase("unexpected-attribute")]
        public void MissingOrUnknownFeeResourcesRejectBeforeNetworkProjection(string mode)
        {
            using (var f = new Fixture(CharacterTestCharacterAttributeIds.Attack)) {
                string json = f.Owner.Capture(); var root = JObject.Parse(json); var old = f.Slice; ulong lease = f.Owner.Lease;
                var world = JObject.Parse((string)root["World"]); var outer = world.SelectTokens("$..payloadJson").Single();
                var payload = JObject.Parse((string)outer);
                var store = ((JArray)payload["componentStores"]).Single(v => ((string)v["schemaId"]).Contains("attribute"));
                foreach (var entry in (JArray)store["entries"]) {
                    var dataField = entry.SelectTokens("$..payloadJson").Single(); var data = JObject.Parse((string)dataField);
                    var values = (JArray)data.GetValue("Attributes", StringComparison.OrdinalIgnoreCase);
                    int id = mode == "missing-cost-attribute" ? CharacterTestCharacterAttributeIds.Attack : CharacterTestCombatEvadeRuntime.StaminaAttributeId;
                    var value = values.Children<JObject>().Single(v => (int)v.GetValue("AttributeId", StringComparison.OrdinalIgnoreCase) == id);
                    if (mode == "stamina-mismatch") value.GetValue("CurrentValue", StringComparison.OrdinalIgnoreCase).Replace(new JValue(0));
                    else if (mode == "stamina-base-mismatch") value.GetValue("BaseValue", StringComparison.OrdinalIgnoreCase).Replace(new JValue(0));
                    else if (mode == "unexpected-attribute") values.Add(new JObject { ["attributeId"] = int.MaxValue, ["baseValue"] = 1, ["currentValue"] = 1 }); // malformed sentinel, never allocated
                    else value.Remove();
                    dataField.Replace(new JValue(data.ToString()));
                }
                outer.Replace(new JValue(payload.ToString())); root["World"] = world.ToString();
                Assert.IsFalse(f.Owner.TryRestore(root.ToString(), out string issue));
                StringAssert.Contains(mode == "missing-cost-attribute" ? "actual-fee-resource-required" : (mode == "unexpected-attribute" || mode == "stamina-base-mismatch") ?
                    "actual-unknown-or-missing-attribute-layout" : "actual-stamina-projection-join", issue);
                Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease);
                Assert.AreEqual(30, old.CharacterRuntime.World.GetCurrentAttribute(f.Mixed.Player, CharacterTestCharacterAttributeIds.Hp));
                VerifyIdleSelectionAndCancellation(f);
            }
        }
        private sealed class FinalStartFailure : RuntimeModule
        {
            internal int Starts, Stops, Disposes;
            internal FinalStartFailure() : base("fixture.paused-last-start", RuntimeTickStage.Diagnostics, int.MaxValue) { }
            public override void Start(RuntimeHostContext context) { Starts++; throw new InvalidOperationException("last actual Host start fault"); }
            public override void Stop(RuntimeHostContext context) { Stops++; }
            public override void Dispose() { Disposes++; }
        }
        [Test] public void LastActualHostStartFailureCompensatesCandidateAndPreservesOldRoot()
        {
            var fault = new FinalStartFailure(); GameSlice failed = null;
            using (var f = new Fixture(beforeHostStart: candidate => { failed = candidate; candidate.Host.RegisterModule(fault); })) {
                string json = f.Owner.Capture(); var old = f.Slice; ulong lease = f.Owner.Lease;
                Assert.IsFalse(f.Owner.TryRestore(json, out string issue)); StringAssert.Contains("start fault", issue);
                Assert.AreEqual(1, fault.Starts); Assert.AreEqual(1, fault.Stops); Assert.AreEqual(1, fault.Disposes);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, failed.Host.State);
                Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease); Assert.AreEqual(0f, Time.timeScale);
                VerifyIdleSelectionAndCancellation(f);
            }
        }
        [Test] public void ThrowingPublishedObserverReportsCommittedSuccessAndDoesNotReviveOldLease()
        {
            using (var f = new Fixture()) {
                string json = f.Owner.Capture(); var old = f.Slice; ulong lease = f.Owner.Lease; bool secondCalled = false;
                var command = f.Command(TacticalStageCommandKind.SelectAction, f.Card("payable-a"));
                f.Owner.Published += (_, __) => { throw new InvalidOperationException("published observer fault"); };
                f.Owner.Published += (_, __) => secondCalled = true;
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreNotSame(old, f.Slice); Assert.AreEqual(lease + 1, f.Owner.Lease); Assert.IsTrue(secondCalled);
                Assert.IsNotNull(f.Owner.LastPublicationError); StringAssert.Contains("committed", issue);
                Assert.AreEqual(TacticalStageRejection.StaleSnapshot, f.Owner.Apply(lease, in command));
                Assert.Throws<InvalidOperationException>(() => old.Tick(1d / 60d));
                VerifyIdleSelectionAndCancellation(f);
            }
        }
        private static CombatQueryResult[] QueryActualPhysics(GameSlice slice)
        {
            var header = new CombatQueryHeader(0, CombatQueryKind.Aabb, CombatEntityId.None, 0, 0, 0,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer)
                    .AddLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                    .AddLayer(CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            var hits = new List<CombatQueryResult>();
            slice.PhysicsRuntime.World.QueryAabb(new CombatAabbQuery(header,
                new FixVector3(Fix64.FromInt(-10), Fix64.FromInt(-10), Fix64.FromInt(-10)),
                new FixVector3(Fix64.FromInt(10), Fix64.FromInt(10), Fix64.FromInt(10))), hits);
            return hits.ToArray();
        }
        [Test] public void FreshRealPhysicsRetainsCharacterQueriesAndCompletesRestoredTurn()
        {
            using (var f = new Fixture()) {
                var old = f.Slice; var oldPhysics = old.PhysicsRuntime;
                string signature = CharacterTestPausedPhysics.CaptureWorld(old);
                var hits = QueryActualPhysics(old); Assert.IsNotEmpty(hits);
                CollectionAssert.AreEquivalent(old.CharacterEntities.Select(e => oldPhysics.GetEntityId(e)).ToArray(),
                    hits.Select(h => h.TargetEntityId).Distinct().ToArray());
                string json = f.Owner.Capture();
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                Assert.AreNotSame(oldPhysics, f.Slice.PhysicsRuntime); Assert.AreNotSame(oldPhysics.World, f.Slice.PhysicsRuntime.World);
                Assert.AreEqual(signature, CharacterTestPausedPhysics.CaptureWorld(f.Slice));
                CollectionAssert.AreEqual(hits, QueryActualPhysics(f.Slice));
                Assert.AreEqual(signature, CharacterTestPausedPhysics.CaptureWorld(old), "Preparing/publishing never mutates old physics.");
                f.CompleteActualTurn(); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Current);
            }
        }
        [TestCase("alias")][TestCase("fresh-floor")][TestCase("incoming-floor")]
        public void InvalidPhysicsRestorePreservesOldQueriesAndRealPayment(string mode)
        {
            using (var f = new Fixture()) {
                var old = f.Slice; ulong lease = f.Owner.Lease;
                var hits = QueryActualPhysics(old); string physics = CharacterTestPausedPhysics.CaptureWorld(old);
                string json = f.Owner.Capture();
                if (mode == "alias") f.AliasPhysics = true;
                if (mode == "fresh-floor") f.ChangedFreshFloor = true;
                if (mode == "incoming-floor") { var root = JObject.Parse(json); root["PhysicsStatic"] = "{}"; json = root.ToString(); }
                Assert.IsFalse(f.Owner.TryRestore(json, out string issue));
                StringAssert.Contains(mode == "alias" ? "actual-fresh-physics-alias" : mode == "fresh-floor" ?
                    "actual-fresh-physics-static-input" : "actual-physics-document-mismatch", issue);
                Assert.AreEqual(mode == "incoming-floor" ? 0 : 1, f.FactoryCalls);
                Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease); Assert.AreEqual(0f, Time.timeScale);
                Assert.AreEqual(physics, CharacterTestPausedPhysics.CaptureWorld(old));
                CollectionAssert.AreEqual(hits, QueryActualPhysics(old));
                f.CompleteActualTurn(); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Current);
            }
        }
        [Test] public void RestoredAllocatorsAllocateSuccessorsInOriginalFreeListOrder()
        {
            using (var f = new Fixture()) {
                int nextRuntime = f.Slice.CharacterRuntime.World.PausedNextRuntimeEntityId;
                string json = f.Owner.Capture();
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                var world = f.Slice.CharacterRuntime.World;
                var definitions = new CharacterTestCharacterDefinitionRegistry();
                Assert.IsTrue(definitions.TryGet(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, out var definition));
                var request = new CharacterTestCharacterSpawnRequest(definition.CharacterId, 9, 0, 0, 0, CharacterTestCharacterControlOwner.RemotePlayer);
                var first = world.Spawn(request, definition); var second = world.Spawn(request, definition);
                Assert.AreEqual(nextRuntime, first.Id.Value); Assert.AreEqual(nextRuntime + 1, second.Id.Value);
                Assert.AreEqual(new GameplayEntityId(4, 2), first.GameplayEntityId);
                Assert.AreEqual(new GameplayEntityId(3, 2), second.GameplayEntityId);
                var freed = first.GameplayEntityId; Assert.IsTrue(world.Remove(first.Id));
                var third = world.Spawn(request, definition);
                Assert.AreEqual(nextRuntime + 2, third.Id.Value); Assert.AreEqual(new GameplayEntityId(4, 3), third.GameplayEntityId);
                Assert.IsFalse(world.GameplayComponentWorld.IsAlive(freed)); Assert.IsTrue(world.GameplayComponentWorld.IsAlive(third.GameplayEntityId));
            }
        }
        [Test] public void RestoredFirstActualCommandKeepsSequenceAndNextFrameAndIsDrainedByRealTurn()
        {
            using (var f = new Fixture()) {
                var before = f.Slice.RuntimeCommands.CommandBuffer.CaptureSnapshot(); Assert.Greater(before.NextSequence, 0);
                string json = f.Owner.Capture();
                Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
                // Admission is cold and emits no command. Ready cannot be bypassed to drain a camera
                // command, so legally commit the action first, then queue the first restored runtime command.
                var payable = f.Card("payable-a");
                Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, payable));
                Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.ConfirmExecution, payable), f.Mixed.Stage.ExecutionIssue);
                Assert.AreEqual(TacticalStagePhase.Executing, f.Mixed.Stage.Phase);
                long firstFrame = f.Slice.CurrentFrame.Value;
                var result = f.Slice.RuntimeCommands.Enqueue(CharacterTestRuntimeCommandFactory.SetCameraMode(
                    f.Slice.CurrentFrame, CharacterTestRuntimeCommandSources.System, CharacterTestCameraMode.MultiTargetAuto));
                Assert.IsTrue(result.Success, result.Error.Message); Assert.AreEqual(before.NextSequence, result.Command.Sequence);
                Assert.AreEqual(firstFrame, result.Command.Frame.Value);
                var pending = f.Slice.RuntimeCommands.CommandBuffer.CaptureSnapshot();
                Assert.AreEqual(before.NextSequence + 1, pending.NextSequence);
                Assert.AreEqual(result.Command, pending.PendingCommands.Single());
                f.CompleteActualTurn();
                Assert.IsFalse(f.Slice.RuntimeCommands.CommandBuffer.CaptureSnapshot().PendingCommands.Contains(result.Command));
                Assert.AreEqual(0, f.Mixed.Stage.Ledger.Current);
            }
        }
        // Review-specific resource/publication proofs are independent of the separately retained full-turn cases.
        // Whole-turn proofs stay in the original cases; this fixture now supplies real fixed-floor Physics.
        private static void VerifyIdleSelectionAndCancellation(Fixture f)
        {
            Assert.AreEqual(RuntimeLifecycleState.Started, f.Slice.Host.State);
            var card = f.Card("payable-a");
            Assert.IsTrue(f.Mixed.PausedAdapter.EvaluateCardAvailability(card, 0).IsLegal);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.SelectAction, card));
            Assert.AreEqual(30, f.Mixed.Stage.Ledger.Reserved);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.CancelPending, f.Mixed.Stage.Deck.PendingAction));
            Assert.AreEqual(50, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Reserved);
        }
    }
}
