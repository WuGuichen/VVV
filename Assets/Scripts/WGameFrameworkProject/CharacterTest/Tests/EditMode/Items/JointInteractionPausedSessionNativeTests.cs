using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Items
{
    // Prepared engine-native tests; offline compilation is NOT execution evidence.
    [Parallelizable(ParallelScope.None)]
    public sealed class JointInteractionPausedSessionNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = Time.timeScale;
        [TearDown] public void Teardown() => Time.timeScale = _scale;

        private sealed class FrameInjection : RuntimeModule
        {
            internal Action<RuntimeFrame> Once;
            internal FrameInjection() : base("fixture.joint-combat-commit", RuntimeTickStage.PostSimulation, int.MaxValue) { }
            public override void Tick(RuntimeTickContext context) { var action = Once; Once = null; action?.Invoke(new RuntimeFrame(context.FrameIndex)); }
        }
        internal sealed class Fixture : IDisposable
        {
            internal readonly TacticalDeployment Deployment;
            internal readonly CharacterTestTacticalStageAuthoringDocument Config = new CharacterTestTacticalStageAuthoringDocument();
            internal GameSlice Initial;
            internal CharacterTestPausedSessionOwner Owner;
            internal int FactoryCalls;
            private readonly FrameInjection _injection = new FrameInjection();
            internal GameSlice Slice => Owner?.Current ?? Initial;
            internal TacticalMixedPlayerCardRuntime Mixed => Slice.Services.Modules.MixedPlayerTacticalStage;
            internal Fixture(bool publishedPreparations = false, bool formalPublishedResources = false,
                Func<TacticalCardDefinition, TacticalCardDefinition> transformPlayerDefinition = null)
            {
                var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var selection = content.CreateDefault();
                selection.InitialHand.Clear(); foreach (string id in TacticalInteractionTrialCards.Ids) {
                    TacticalInteractionTrialCards.TryCreate(id, out var utility);
                    var kind = ((InteractionPreparationPayload)utility.Payload).Effect;
                    string selected = formalPublishedResources ? content.Catalog.Cards.Single(c => c.InteractionPreparation?.Effect == kind).CardId : id;
                    selection.InitialHand.Add(new CharacterTestRuntimeAiLabCardTrialSelection(selected));
                }
                selection.InitialHand.Add(content.DefaultActionCard(selection.PlayerCharacterId));
                Deployment = content.Freeze(selection, 91);
                if (publishedPreparations) {
                    var participants = Deployment.Participants.Select(p => !p.IsPlayerControlled ? p : new TacticalDeploymentParticipant(
                        p.EntityId, p.CharacterId, p.CharacterStableId, p.DisplayName, p.TeamId, p.IsPlayerControlled, p.Position, p.YawDegrees,
                        p.Cards.Select(card => card is TacticalUtilityCardDefinition utility && utility.Payload is InteractionPreparationPayload preparation ?
                            (TacticalCardDefinition)JointInteractionSessionSnapshotTests.PublishedPreparationForFixture((int)preparation.Effect) : card).ToArray(),
                        p.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value), p.CharacterItemInstanceId, p.InventoryOwnerId, p.CharacterItemRevision)).ToArray();
                    Deployment = TacticalDeployment.CreateLabTrial(participants, Deployment.InitialMotion, Deployment.CardTrial);
                }
                if (transformPlayerDefinition != null) {
                    var participants = Deployment.Participants.Select(p => !p.IsPlayerControlled ? p : new TacticalDeploymentParticipant(
                        p.EntityId, p.CharacterId, p.CharacterStableId, p.DisplayName, p.TeamId, p.IsPlayerControlled, p.Position, p.YawDegrees,
                        p.Cards.Select(transformPlayerDefinition).ToArray(), p.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value),
                        p.CharacterItemInstanceId, p.InventoryOwnerId, p.CharacterItemRevision)).ToArray();
                    Deployment = TacticalDeployment.CreateLabTrial(participants, Deployment.InitialMotion, Deployment.CardTrial);
                }
                var options = Options(); options.StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>();
                options.DetachedTacticalRestore = true; Initial = new GameSlice(options);
                try {
                    Initial.Host.RegisterModule(_injection); Initial.StartDetachedPausedHost();
                    foreach (var participant in Deployment.Participants) {
                        var definition = content.Character(participant.CharacterId);
                        var p = participant.Position;
                        Initial.CharacterRuntime.World.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId, participant.TeamId,
                            p.x, p.y, p.z, participant.IsPlayerControlled ? CharacterTestCharacterControlOwner.LocalPlayer : CharacterTestCharacterControlOwner.RemotePlayer), definition);
                    }
                    for (int i = 0; i < 600 && (Mixed.Stage.Phase != TacticalStagePhase.Planning || !Mixed.PausedWorldTime.IsSimulationPaused); i++) Tick();
                    Assert.AreEqual(TacticalStagePhase.Planning, Mixed.Stage.Phase, Mixed.Stage.ExecutionIssue);
                    Assert.AreEqual(6, Mixed.Stage.Deck.Hand.Count);
                } catch { Initial.Dispose(); throw; }
            }
            internal CharacterTestSliceOptions Options() => new CharacterTestSliceOptions {EnableStoryBootstrap = false, CreateCharacterViews = false,
                EnableLocalInputCommands = false, EnableTraceFileWrite = false, SuppressMissingStartupSpawnWarning = true,
                MixedPlayerTacticalStage = Config, TacticalDeployment = Deployment, PhysicsRuntime = Floor()};
            internal void Freeze(bool targetLedger = false, Action<GameSlice> beforeHostStart = null)
            {
                if (targetLedger) Slice.CharacterRuntime.World.BindTacticalMotion(Mixed.CurrentTarget.Id.Value, new MomentumLedger(37));
                var context = new CharacterTestPausedRestoreContext(Slice, "lab", "joint-native", "five-preparations", "frozen-content",
                    Deployment, Config, () => {FactoryCalls++; return Options();}, () => true);
                Owner = new CharacterTestPausedSessionOwner(Slice, context, beforeHostStart);
            }
            internal void Tick() { Slice.Tick(1d / 60d); Mixed.TickWorldTimeTransition(1f / 60f); }
            internal void Play(string id)
            {
                var card = Mixed.Stage.Deck.Hand.Single(c => c.Definition.Id == id);
                Apply(TacticalStageCommandKind.SelectAction, card); Apply(TacticalStageCommandKind.ConfirmExecution, card);
            }
            internal void Apply(TacticalStageCommandKind kind, TacticalCardInstance card)
            {
                Mixed.PausedAdapter.SetContext(null, Slice.CurrentFrame);
                var s = Mixed.Stage; var command = new TacticalStageCommand(s.StageRunId, s.TurnIndex, card.InstanceId, card.Version, s.SnapshotRevision, kind);
                Assert.AreEqual(TacticalStageRejection.None, Owner == null ? s.Apply(in command) : Owner.Apply(Owner.Lease, in command), s.ExecutionIssue);
            }
            internal void InjectOneRootHit()
            {
                // A fixture module injects the real Combat damage applier after this frame's motion;
                // normal Host/clock/Stage completion still establishes the F barrier. It is never restored as a product module.
                _injection.Once = frame => {
                    var applier = new CharacterTestCombatDamageApplier(); var a = Mixed.Player; var b = Mixed.CurrentTarget;
                    applier.Apply(Slice.CharacterRuntime.World, null, frame, new[] {Result(a, b, frame.Value)}, Payload);
                    // This fixture module runs after CharacterRuntime's usual health projection pass.
                    // Complete the same projection before the Host publishes the full-frame snapshot boundary.
                    foreach (var actor in Slice.CharacterEntities) Slice.CharacterRuntime.World.SyncCombatStateHealth(actor);
                    var events = new List<GameplayRuntimeEvent>(); Slice.CharacterRuntime.World.GameplayComponentWorld.DrainEvents(frame, events);
                };
                Mixed.PausedWorldTime.SetSimulationPaused(false); long before = Slice.CurrentFrame.Value; Tick();
                Assert.AreEqual(before + 1, Slice.CurrentFrame.Value); Mixed.PausedWorldTime.SetSimulationPaused(true);
                Assert.IsNull(_injection.Once); Assert.AreEqual(before, Mixed.Stage.LastCompletedFrame);
            }
            internal void CompleteActualTurn()
            {
                var s = Mixed.Stage; ulong turn = s.TurnIndex; var action = s.Deck.Hand.First(c => c.Definition.Kind == TacticalCardKind.Action);
                Assert.IsTrue(Mixed.PausedAdapter.EvaluateCardAvailability(action, s.Ledger.Reserved).IsLegal);
                Apply(TacticalStageCommandKind.SelectAction, action); Apply(TacticalStageCommandKind.ConfirmExecution, action);
                Assert.AreEqual(TacticalStagePhase.Executing, s.Phase);
                for (int i = 0; i < 1200 && (s.TurnIndex == turn || s.Phase != TacticalStagePhase.Planning); i++) Tick();
                Assert.AreEqual(TacticalStageHealth.Running, s.Health, s.ExecutionIssue); Assert.Greater(s.TurnIndex, turn);
                Assert.AreEqual(TacticalStagePhase.Planning, s.Phase); Assert.AreEqual(0, s.Ledger.Reserved);
            }
            public void Dispose() { if (Owner != null) Owner.Dispose(); else Initial.Dispose(); }
            private static CharacterTestCharacterPhysicsRuntime Floor()
            {
                var floor = new CombatLevelAabbColliderDefinition("joint-floor", CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart,
                    CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart, 1, 1, CombatLevelFixedVector3.Zero,
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(-100), Fix64.FromInt(-1), Fix64.FromInt(-100))),
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(100), Fix64.Zero, Fix64.FromInt(100))), CombatLevelSurfaceMaterialDefinition.Default);
                var level = new CombatLevelDefinition(CombatLevelDefinition.CurrentSchemaVersion, "joint-floor", "Joint fixture", new[] {floor},
                    new[] {new CombatLevelPointDefinition("joint-fixture-spawn", CombatLevelFixedVector3.Zero)}, Array.Empty<CombatLevelPointDefinition>());
                var world = CombatLevelWorldBuilder.BuildWorld(level);
                return new CharacterTestCharacterPhysicsRuntime(world, CombatPhysicsLayerMask.FromLayer(1), new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                    Fix64.Zero, Fix64.Zero, staticColliderCount: world.TotalColliderCount, sourceDescription: "level:joint-floor", groundSnapDistance: Fix64.FromRatio(2,1000));
            }
        }

        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void ActualPublishedR1ResourceRestoresTriggersOnceAndCompletesRealTurn(int kind)
        {
            using var f = new Fixture(formalPublishedResources: true);
            var card = f.Mixed.Stage.Deck.Hand.Single(c => c.Definition is TacticalUtilityCardDefinition utility
                && utility.Payload is InteractionPreparationPayload preparation && (int)preparation.Effect == kind);
            Assert.IsTrue(card.Definition.HasPublishedBehavior); Assert.AreEqual(1, card.Definition.PublishedRevision);
            string id = card.Definition.PublishedCardId; string contentHash = card.Definition.PublishedContentHash;
            f.Play(id); f.Freeze(); string json = f.Owner.Capture(); var saved = CharacterTestPausedSessionCodec.Read(json);
            var previous = f.Slice; var previousLedger = f.Mixed.Stage.Ledger; var previousEffects = f.Mixed.Player.InteractionEffects;
            Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
            Assert.AreNotSame(previous, f.Slice); Assert.AreNotSame(previousLedger, f.Mixed.Stage.Ledger);
            Assert.AreNotSame(previousEffects, f.Mixed.Player.InteractionEffects);
            Assert.AreEqual(saved.StateHash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
            var restoredSource = f.Mixed.Stage.Deck.ExhaustPile.Single(c => c.Definition.Id == id);
            Assert.AreEqual(id, restoredSource.Definition.PublishedCardId); Assert.AreEqual(1, restoredSource.Definition.PublishedRevision);
            Assert.AreEqual(contentHash, restoredSource.Definition.PublishedContentHash);
            Assert.AreEqual(45, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(5, f.Mixed.Stage.Deck.PaidCardMomentum);
            long frame = f.Slice.CurrentFrame.Value;
            var root = kind == 5 ? Result(f.Mixed.CurrentTarget, f.Mixed.Player, frame) : Result(f.Mixed.Player, f.Mixed.CurrentTarget, frame);
            if (kind == 3) root = new HitResolveResult(root.AttackerId, root.TargetId, root.ActionId, root.ActionInstanceId, root.TraceId,
                root.Frame, HitResolveKind.Blocked, root.Damage, 0, FixVector3.Zero);
            f.Slice.CharacterRuntime.World.TryGetScatterPressure(f.Mixed.CurrentTarget, out var pressureBefore);
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(f.Slice.CharacterRuntime.World, null, new RuntimeFrame(frame), new[] {root}, Payload);
            int expectedMomentum = kind == 2 || kind == 5 ? 48 : 45;
            Assert.AreEqual(expectedMomentum, f.Mixed.Stage.Ledger.Current);
            Assert.AreEqual(kind == 4, f.Mixed.Player.InteractionEffects.ReturnPending);
            Assert.AreEqual(0, f.Mixed.Player.InteractionEffects.ActiveMask);
            Assert.AreEqual(kind == 1 ? 2 : 1, applier.DamageResolvedEvents.Count);
            if (kind == 3) {
                f.Slice.CharacterRuntime.World.TryGetScatterPressure(f.Mixed.CurrentTarget, out var pressureAfter);
                Assert.AreEqual(Math.Min(pressureBefore.MaxScatterDebt, pressureBefore.CurrentScatterPressure + 12), pressureAfter.CurrentScatterPressure);
            }
            applier.ClearFrameOutputs();
            applier.Apply(f.Slice.CharacterRuntime.World, null, new RuntimeFrame(frame), new[] {root}, Payload);
            Assert.AreEqual(expectedMomentum, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(1, applier.DamageResolvedEvents.Count);
            f.CompleteActualTurn();
        }

        [Test] public void BeforeTriggerFullSessionRestoreKeepsFivePaidSlotsAndIndependentActorLedger()
        {
            using var f = new Fixture(); foreach (string id in TacticalInteractionTrialCards.Ids) f.Play(id); f.Freeze(true);
            string json = f.Owner.Capture(); var old = f.Slice; string hash = CharacterTestPausedSessionCodec.Read(json).StateHash;
            Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
            Assert.AreEqual(hash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash); Assert.AreNotSame(old, f.Slice);
            Assert.AreNotSame(old.CharacterEntities[0].InteractionEffects, f.Mixed.Player.InteractionEffects);
            Assert.AreEqual(31, f.Mixed.Player.InteractionEffects.ActiveMask); Assert.AreEqual(25, f.Mixed.Stage.Ledger.Current);
            Assert.IsTrue(f.Slice.CharacterRuntime.World.TryGetTacticalMotion(f.Mixed.CurrentTarget.Id.Value, out var targetLedger));
            Assert.AreEqual(37, targetLedger.Current); Assert.AreNotSame(targetLedger, f.Mixed.Stage.Ledger);
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(f.Slice.CharacterRuntime.World, null, f.Slice.CurrentFrame,
                new[] {Result(f.Mixed.Player, f.Mixed.CurrentTarget, f.Slice.CurrentFrame.Value)}, Payload);
            Assert.AreEqual(28, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(37, targetLedger.Current);
            Assert.AreEqual(2, applier.DamageResolvedEvents.Count); Assert.IsTrue(f.Mixed.Player.InteractionEffects.ReturnPending);
            Assert.AreEqual(20, f.Mixed.Player.InteractionEffects.ActiveMask);
        }
        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void PublishedPreparationFullRestoreRetainsCommonIdentityAndCompletesRealTurn(int kind)
        {
            // Fixture publication references exercise formal definition identity inside the bounded Lab world.
            // They do not allocate production IDs or claim Items/profile/disk recovery coverage.
            using var f = new Fixture(true); string id = "joint-fixture-preparation-" + kind; f.Play(id); f.Freeze();
            var old = f.Slice; var oldEffects = f.Mixed.Player.InteractionEffects; var oldLedger = f.Mixed.Stage.Ledger;
            string json = f.Owner.Capture(); var document = CharacterTestPausedSessionCodec.Read(json);
            StringAssert.Contains(id, document.Config);
            Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
            Assert.AreNotSame(old, f.Slice); Assert.AreNotSame(oldEffects, f.Mixed.Player.InteractionEffects); Assert.AreNotSame(oldLedger, f.Mixed.Stage.Ledger);
            Assert.AreEqual(document.StateHash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
            var source = f.Mixed.Stage.Deck.ExhaustPile.Single(card => card.Definition.Id == id);
            Assert.AreEqual(id, source.Definition.PublishedCardId); Assert.AreEqual(2, source.Definition.PublishedRevision);
            Assert.AreEqual("123456789", source.Definition.PublishedContentHash); Assert.AreEqual((ulong)kind, source.Definition.DeckEntryInstanceId);
            Assert.AreEqual(1 << (kind - 1), f.Mixed.Player.InteractionEffects.ActiveMask);
            Assert.AreEqual(45, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(5, f.Mixed.Stage.Deck.PaidCardMomentum);
            f.CompleteActualTurn();
        }
        [Test] public void AfterTriggerRestoreDeduplicatesRealCombatRewardAndDerivedDamage()
        {
            using var f = new Fixture(); f.Play("lab.pursuit-mark"); f.Play("lab.momentum-recovery"); f.InjectOneRootHit(); f.Freeze();
            var source = f.Mixed.Player.InteractionEffects.Capture(); Assert.AreEqual(1, source.Receipts.Length); Assert.AreEqual(43, f.Mixed.Stage.Ledger.Current);
            string json = f.Owner.Capture(); Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(f.Slice.CharacterRuntime.World, null, new RuntimeFrame(f.Mixed.Stage.LastCompletedFrame),
                new[] {Result(f.Mixed.Player, f.Mixed.CurrentTarget, source.LastReceiptFrame)}, Payload);
            Assert.AreEqual(43, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(1, applier.DamageResolvedEvents.Count);
            Assert.AreEqual(1, f.Mixed.Player.InteractionEffects.Capture().Receipts.Length);
        }
        [TestCase(false)][TestCase(true)] public void RestoredReturnPendingWaitsForRealMotionBoundaryAndAttemptsOnce(bool blocked)
        {
            using var f = new Fixture(); f.Play("lab.return-mark"); var actor = f.Mixed.Player;
            var armed = actor.InteractionEffects.Capture().Slots[3]; var point = new TacticalWorldPoint(armed.X, armed.Y, armed.Z);
            var motion = (CharacterTestCharacterMotionSystem)typeof(CharacterTestCharacterRuntimeModule)
                .GetField("_motionSystem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Slice.CharacterRuntime);
            Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.Slice.CharacterRuntime.World, actor, new TacticalWorldPoint(point.X - 3000, point.Y, point.Z)));
            if (blocked) Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.Slice.CharacterRuntime.World, f.Mixed.CurrentTarget, point));
            f.InjectOneRootHit(); Assert.IsTrue(actor.InteractionEffects.ReturnPending); f.Freeze();
            string json = f.Owner.Capture(); Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue);
            Assert.IsTrue(f.Mixed.Player.InteractionEffects.ReturnPending); Assert.AreEqual(point.X - 3000, f.Mixed.Player.Transform.XMillimeters);
            long first = f.Slice.CurrentFrame.Value; f.Mixed.PausedWorldTime.SetSimulationPaused(false); f.Tick(); f.Mixed.PausedWorldTime.SetSimulationPaused(true);
            Assert.AreEqual(first + 1, f.Slice.CurrentFrame.Value); Assert.IsFalse(f.Mixed.Player.InteractionEffects.ReturnPending);
            Assert.AreEqual(blocked ? TacticalReturnResult.DestinationBlocked : TacticalReturnResult.Returned, f.Mixed.Player.InteractionEffects.ReturnResult);
            Assert.AreEqual(blocked ? point.X - 3000 : point.X, f.Mixed.Player.Transform.XMillimeters);
            f.Mixed.Player.InteractionEffects.CompleteReturnAttempt(!blocked);
            Assert.AreEqual(blocked ? TacticalReturnResult.DestinationBlocked : TacticalReturnResult.Returned, f.Mixed.Player.InteractionEffects.ReturnResult);
        }
        [TestCase("card")][TestCase("receipt")][TestCase("generation")][TestCase("unpaid")][TestCase("terminal-active")][TestCase("hash")]
        [TestCase("return-coordinate")][TestCase("ledger-owner")]
        public void MalformedFullSessionRejectsAndOldPaidWorldCompletesActualTurn(string mode)
        {
            using var f = new Fixture(); f.Play(mode == "terminal-active" || mode == "return-coordinate" ? "lab.return-mark" : "lab.momentum-recovery"); f.Freeze();
            string json = f.Owner.Capture(); var d = CharacterTestPausedSessionCodec.Read(json); var state = d.Interactions[0].State;
            if (mode == "card") state.Slots[1].CardInstance = 999;
            if (mode == "generation") d.Interactions[0].Generation++;
            if (mode == "unpaid") d.Deck.Paid = 0;
            if (mode == "ledger-owner") d.Motion[0].Generation++;
            if (mode == "return-coordinate") state.Slots[3].X = int.MaxValue;
            if (mode == "receipt") {state.LastReceiptFrame = d.CompletedFrame; var receipt = new TacticalInteractionReceiptData(new TacticalInteractionReceipt(
                state.Stage, state.Turn, d.CompletedFrame, state.Actor, new GameplayEntityId(d.Stage.Opponent, d.Stage.OpponentGeneration), 99, 1));
                state.Receipts = new[] {receipt, receipt};}
            if (mode == "terminal-active") {state.ReturnResult = TacticalReturnResult.Returned; state.Slots[3].X = state.Slots[3].Y = state.Slots[3].Z = 0;}
            if (mode != "hash") d.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(d); else d.StateHash = new string('0', 64);
            var old = f.Slice; ulong lease = f.Owner.Lease;
            Assert.IsFalse(f.Owner.TryRestore(JsonConvert.SerializeObject(d), out string issue)); Assert.IsNotEmpty(issue);
            Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease); Assert.AreEqual(json, f.Owner.Capture());
            f.CompleteActualTurn(); Assert.GreaterOrEqual(f.Mixed.Stage.Deck.PaidCardMomentum, 5);
        }
        [Test] public void RestoredPreparationStillUsesActualActionAdapterAndOldCompletedFrameBarrier()
        {
            using var f = new Fixture(); f.Play("lab.momentum-recovery"); f.Freeze(); string json = f.Owner.Capture();
            Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue); f.CompleteActualTurn(); int factories = f.FactoryCalls;
            Assert.IsFalse(f.Owner.TryRestore(json, out issue)); StringAssert.Contains("current-whole-frame-barrier", issue);
            Assert.AreEqual(factories, f.FactoryCalls);
        }
        [Test] public void ColdWiringCannotChangeRestoredLedgerBeforeAtomicPublication()
        {
            using var f = new Fixture(); f.Play("lab.momentum-recovery");
            f.Freeze(beforeHostStart: candidate => candidate.Services.Modules.MixedPlayerTacticalStage.Stage.Ledger.Grant(1));
            string json = f.Owner.Capture(); var old = f.Slice; ulong lease = f.Owner.Lease;
            Assert.IsFalse(f.Owner.TryRestore(json, out string issue)); StringAssert.Contains("actual-started-complete-state-hash", issue);
            Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease); Assert.AreEqual(json, f.Owner.Capture());
            f.CompleteActualTurn();
        }
        [TestCase(false)][TestCase(true)] public void CompletedRealReturnFullSnapshotRejectsOnlyActiveBitRearmAndKeepsOldTurn(bool blocked)
        {
            using var f = new Fixture(); f.Play("lab.return-mark"); var actor = f.Mixed.Player;
            var slot = actor.InteractionEffects.Capture().Slots[3]; var point = new TacticalWorldPoint(slot.X, slot.Y, slot.Z);
            if (blocked) {
                var motion = (CharacterTestCharacterMotionSystem)typeof(CharacterTestCharacterRuntimeModule)
                    .GetField("_motionSystem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Slice.CharacterRuntime);
                Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.Slice.CharacterRuntime.World, actor, new TacticalWorldPoint(point.X - 3000, point.Y, point.Z)));
                Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.Slice.CharacterRuntime.World, f.Mixed.CurrentTarget, point));
            }
            f.InjectOneRootHit(); Assert.IsTrue(actor.InteractionEffects.ReturnPending);
            f.Mixed.PausedWorldTime.SetSimulationPaused(false); f.Tick(); f.Mixed.PausedWorldTime.SetSimulationPaused(true);
            Assert.AreEqual(blocked ? TacticalReturnResult.DestinationBlocked : TacticalReturnResult.Returned, actor.InteractionEffects.ReturnResult);
            f.Freeze(); string json = f.Owner.Capture(); var d = CharacterTestPausedSessionCodec.Read(json);
            Assert.IsTrue(f.Owner.TryRestore(json, out string issue), issue); // First prove the legitimate COMPLETE terminal baseline restores.
            var old = f.Slice; ulong lease = f.Owner.Lease; string accepted = f.Owner.Capture();
            d.Interactions[0].State.Slots[3].Active = true; d.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(d);
            Assert.IsFalse(f.Owner.TryRestore(JsonConvert.SerializeObject(d), out issue));
            Assert.AreSame(old, f.Slice); Assert.AreEqual(lease, f.Owner.Lease); Assert.AreEqual(accepted, f.Owner.Capture());
            f.CompleteActualTurn();
        }
        private static HitResolveResult Result(CharacterTestCharacterEntity a, CharacterTestCharacterEntity b, long frame)
            => new HitResolveResult(new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart+a.Id.Value), new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart+b.Id.Value), 0, 99, 1,
                new CombatFrame(checked((int)frame)), HitResolveKind.Damage, 1, 0, FixVector3.Zero);
        private static DamageProfileResolveResult Payload(HitResolveResult r)
            => new DamageProfileResolveResult(true, "", 0, 0, DamageFormulaIds.Flat, r.Damage, 0, 0, FixVector3.Zero, 0, 0, 0,
                new DamageProfileUsage(DamageProfileSourceKind.WeaponTrace, actionInstanceId: r.ActionInstanceId, sourceEntityId: r.AttackerId.Value, targetEntityId: r.TargetId.Value));
    }
}
