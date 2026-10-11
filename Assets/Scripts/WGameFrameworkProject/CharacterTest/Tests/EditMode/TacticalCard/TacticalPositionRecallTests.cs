using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalPositionRecallTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 2);
        private static CharacterTestPositionRecallState Recorded()
        { var s = CharacterTestPositionRecallState.Bind(5, Actor); Assert.IsTrue(s.Submit(5, Actor, 1, CharacterTestPositionRecallOperation.Record, 600, 10)); s.CompleteRecord(1200, 0, -500, 10); return s; }
        [Test] public void FrozenRecordAndSingleSuccessHaveRealReceipts()
        {
            var s = Recorded(); Assert.IsTrue(s.HasRecord); Assert.AreEqual(10, s.StartedTick); Assert.AreEqual(610, s.ExpiresTick);
            Assert.IsTrue(s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 20));
            Assert.AreEqual(1200, s.X); Assert.AreEqual(-1, s.StartedTick); Assert.IsTrue(s.Pending);
            s.CompleteReturn(20); Assert.IsFalse(s.HasRecord); Assert.AreEqual(0, s.RemainingUses); Assert.AreEqual(20, s.StartedTick);
            Assert.IsFalse(s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 21));
            Assert.IsTrue(s.Submit(5, Actor, 3, CharacterTestPositionRecallOperation.Return, 0, 21));
            s.Complete(CharacterTestPositionRecallResult.NoRecord, 21); Assert.AreEqual(-1, s.StartedTick);
        }
        [TestCase(7)]
        [TestCase(6)]
        [TestCase(8)]
        public void FailedReturnKeepsExactRecordAndHasNoStartedReceipt(int result)
        {
            var s = Recorded(); s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 20); s.Complete((CharacterTestPositionRecallResult)result, 20);
            Assert.IsTrue(s.RecordValid(20)); Assert.AreEqual(1200, s.X); Assert.AreEqual(610, s.ExpiresTick); Assert.AreEqual(-1, s.StartedTick);
            s.Submit(5, Actor, 3, CharacterTestPositionRecallOperation.Return, 0, 21); s.CompleteReturn(21); Assert.IsFalse(s.HasRecord);
        }
        [Test] public void DuplicateStaleGenerationAndBattleRequestsCannotMutateRecord()
        {
            var s = Recorded(); Assert.IsFalse(s.Submit(5, Actor, 1, CharacterTestPositionRecallOperation.Record, 600, 11));
            Assert.IsFalse(s.Submit(6, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 11));
            Assert.IsFalse(s.Submit(5, new GameplayEntityId(1, 3), 2, CharacterTestPositionRecallOperation.Return, 0, 11));
            Assert.IsFalse(s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 9)); Assert.AreEqual(1200, s.X);
            Assert.IsTrue(s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 11));
            Assert.IsFalse(s.Submit(5, Actor, 3, CharacterTestPositionRecallOperation.Record, 600, 12));
        }
        [Test] public void SimulatedFrameExpiryPauseAndOverwriteAreExplicit()
        {
            var s = Recorded(); for (int i = 0; i < 100; i++) s.Expire(609); Assert.IsTrue(s.HasRecord);
            s.Expire(610); Assert.IsFalse(s.HasRecord);
            s = Recorded(); s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Record, 120, 20); s.CompleteRecord(2000, 0, 0, 20);
            Assert.AreEqual(2000, s.X); Assert.AreEqual(140, s.ExpiresTick); Assert.AreEqual(1, s.RemainingUses);
            s.ClearRecord(); Assert.IsFalse(s.HasRecord);
        }
        [Test] public void StateRoundtripHashAndSnapshotFrameValidation()
        {
            var s = Recorded(); Assert.IsTrue(s.IsValidSnapshot(10)); Assert.IsTrue(s.IsValidSnapshot());
            var restored = JsonConvert.DeserializeObject<CharacterTestPositionRecallState>(JsonConvert.SerializeObject(s));
            Assert.AreEqual(s.Actor, restored.Actor); Assert.AreEqual(s.ExpiresTick, restored.ExpiresTick); Assert.AreEqual(s.X, restored.X);
            var a = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1); s.AddHash(ref a);
            var b = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1); restored.AddHash(ref b); Assert.AreEqual(a.ToHash(), b.ToHash());
            restored.X++; b = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1); restored.AddHash(ref b); Assert.AreNotEqual(a.ToHash(), b.ToHash());
            Assert.IsFalse(s.IsValidSnapshot(9)); Assert.IsFalse(s.IsValidSnapshot(610));
            var bad = s; bad.StartedTick = 20; Assert.IsFalse(bad.IsValidSnapshot(10));
            var snap = new CharacterTestNetworkCharacterSnapshot { PositionRecall = s }; snap.NormalizeForSchema(39); Assert.AreEqual(0ul, snap.PositionRecall.StageRun);
            var document = new CharacterTestNetworkSnapshotDocument { Frame = 9, Characters = new[] { new CharacterTestNetworkCharacterSnapshot { PositionRecall = s } } };
            Assert.Throws<JsonSerializationException>(() => document.NormalizeForSchema(40));
        }
        [Test] public void CorruptReceiptOperationAndUnownedRecordAreRejected()
        {
            var state = CharacterTestPositionRecallState.Bind(5, Actor);
            state.HasRecord = true; state.X = 25; state.RemainingUses = 1;
            state.RecordedTick = 1; state.ExpiresTick = 601;
            Assert.IsFalse(state.IsValidSnapshot(2));
            state.ClearRecord();
            Assert.IsTrue(state.Submit(5, Actor, 1, CharacterTestPositionRecallOperation.Record, 600, 2));
            state.Complete(CharacterTestPositionRecallResult.Returned, 2);
            Assert.IsTrue(state.Pending);
            state.Result = CharacterTestPositionRecallResult.Returned; state.CompletedTick = state.StartedTick = 2;
            Assert.IsFalse(state.IsValidSnapshot(2));
        }

        [Test] public void CommandHasExactOwnerOperationAndStrictEnvelope()
        {
            var record = CharacterTestRuntimeCommandFactory.PositionRecall(new RuntimeFrame(10), 1, Actor, 5, 1, CharacterTestPositionRecallOperation.Record, 600);
            Assert.IsTrue(CharacterTestRuntimeCommandFactory.IsPositionRecallValid(record));
            Assert.IsFalse(CharacterTestRuntimeCommandFactory.IsPositionRecallValid(CharacterTestRuntimeCommandFactory.PositionRecall(new RuntimeFrame(10), 1, Actor, 0, 1, CharacterTestPositionRecallOperation.Record, 600)));
            Assert.IsFalse(CharacterTestRuntimeCommandFactory.IsPositionRecallValid(CharacterTestRuntimeCommandFactory.PositionRecall(new RuntimeFrame(10), 1, Actor, 5, 1, CharacterTestPositionRecallOperation.Return, 600)));
        }
        [TestCase("Record", 600)] [TestCase("Return", 0)]
        public void ExclusiveNoActionContractCompilesPublishesAndDeploys(string operation, int lifetime)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreatePositionRecall(operation, new TacticalPositionRecallAuthoring { Operation = operation, LifetimeFrames = lifetime });
            Assert.AreEqual(0, source.Slots.Count); Assert.AreEqual(0, source.Timelines[0].Events.Count); Assert.IsNull(source.ExecutionInput);
            string json = CharacterTestTacticalAuthoringJson.Write(source); Assert.IsFalse(json.Contains("executionInput"));
            var inputs = CharacterTestTacticalCardInputs.Unbound(1);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var issues), string.Join(";", issues));
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("N"), "bc_recalltest", operation, "Tactical", null) { Cost = 3 };
            draft.SetTacticalSource(source);
            var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled);
            Assert.IsTrue(frozen.Description.Contains(operation == "Record" ? "记录" : "返回"));
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(operation, "ui.behavior-card.custom", 3, "", "", "");
            var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, metadata, CharacterTestAuthoredContentIds.D417_Id3),
                null, new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen), null));
            Assert.IsTrue(result.Success, "No-action recall must publish through the actual publisher.");
            var published = CharacterTestTacticalPublishedSource.Read(result.Card.TacticalSource.ToJson());
            Assert.AreEqual(operation, published.CloneLegacy().PositionRecall.Operation); Assert.IsNull(published.PointMove);
            var reference = new CharacterTacticalCardReference { CardId = result.Card.StableCardId, Revision = result.Card.Revision, ContentHash = result.Card.ContentHash.ToString(), InstanceId = 5 };
            var catalog = new CharacterTacticalCardDefinition { CardId = reference.CardId, Revision = reference.Revision, ContentHash = reference.ContentHash,
                DisplayName = operation, Description = result.Card.Metadata.ShortDescription, MotionCost = 3, PresentationSource = published };
            var card = TacticalDeployment.CreateCard(reference, catalog); Assert.IsNotNull(card.PositionRecall); Assert.IsTrue(card.DefersPaymentUntilStarted);
            Assert.IsFalse(card.ActionId.IsValid); Assert.AreEqual(TacticalActionSlot.None, card.ActionSlot);
            Assert.IsFalse(published.TryCompile("recall", "recall", 1, inputs, out _, out var issue)); Assert.AreEqual("execution:PositionRecallAuthorityRequired", issue);
            var bad = JObject.Parse(json); bad["positionRecall"]["lifetimeFrames"] = lifetime.ToString();
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(bad.ToString()));
        }
        [Test] public void MalformedMixedAndOutOfRangeAuthoringIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new TacticalPositionRecallAuthoring { Operation = "Return", LifetimeFrames = 600 }.Freeze());
            Assert.Throws<ArgumentException>(() => new TacticalPositionRecallAuthoring { Operation = "Record", LifetimeFrames = 3601 }.Freeze());
            var source = CharacterTestTacticalAuthoringTemplates.CreatePositionRecall("record", new TacticalPositionRecallAuthoring { Operation = "Record", LifetimeFrames = 600 });
            source.ExecutionInput = TacticalGroundTractionTests.Contract(); Assert.Throws<JsonSerializationException>(() => source.Clone());
        }
        private static CombatPhysicsWorld World()
        {
            var world = new CombatPhysicsWorld(); AddBox(world, 10, 1, new FixVector3(-Fix64.FromInt(10), -Fix64.One, -Fix64.FromInt(10)), new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.FromInt(10))); return world;
        }
        private static void AddBox(CombatPhysicsWorld world, int id, int layer, FixVector3 min, FixVector3 max)
        { var body = new CombatBodyId(id); world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(id), body, FixVector3.Zero)); world.UpsertAabbCollider(new CombatPhysicsAabbCollider(body, new CombatColliderId(id), layer, min, max, CombatSurfaceMaterial.Rough)); }
        private static bool Safe(CombatPhysicsWorld world, FixVector3 point)
        {
            var ground = new CombatQueryHeader(910301, CombatQueryKind.Ray, new CombatEntityId(1), 910301, 0, 0, CombatPhysicsLayerMask.FromLayer(1));
            var clear = new CombatQueryHeader(910301, CombatQueryKind.Aabb, new CombatEntityId(1), 910301, 0, 0, CombatPhysicsLayerMask.FromLayer(1).AddLayer(2));
            return CharacterTestPositionRecallSafety.TryValidate(world, ground, clear, new CombatBodyId(1), point,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), Fix64.One, Fix64.FromRatio(1,100), Fix64.Half, Fix64.Half, new List<CombatQueryResult>(32), out _);
        }
        [Test] public void ExactEndpointRejectsOccupiedOrUnsupportedGroundWithoutSnapping()
        {
            var world = World(); Assert.IsTrue(Safe(world, FixVector3.Zero));
            AddBox(world, 2, 2, new FixVector3(-Fix64.Half, Fix64.Zero, -Fix64.Half), new FixVector3(Fix64.Half, Fix64.FromInt(2), Fix64.Half));
            Assert.IsFalse(Safe(world, FixVector3.Zero)); Assert.IsFalse(Safe(world, new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.Zero)));
            Assert.IsTrue(Safe(world, new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero)));
            var s = Recorded(); s.Submit(5, Actor, 2, CharacterTestPositionRecallOperation.Return, 0, 20); s.Complete(CharacterTestPositionRecallResult.DestinationBlocked, 20); Assert.IsTrue(s.RecordValid(20));
        }
        [Test] public void FullHeightClearanceDetectsCeilingAndReturnDoesNotRequireTravelPath()
        {
            var world = World(); AddBox(world, 20, 1, new FixVector3(-Fix64.One, Fix64.FromRatio(16,10), -Fix64.One), new FixVector3(Fix64.One, Fix64.FromInt(3), Fix64.One));
            Assert.IsFalse(Safe(world, FixVector3.Zero));
            world = World(); AddBox(world, 20, 1, new FixVector3(Fix64.One, Fix64.Zero, -Fix64.FromInt(3)), new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(3)));
            Assert.IsTrue(Safe(world, FixVector3.Zero)); Assert.IsTrue(Safe(world, new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero)));
        }
        [Test] public void CompleteMotorFootprintRejectsAnObstacleAtTheCapsuleCorner()
        {
            var world = World();
            AddBox(world, 21, 2, new FixVector3(Fix64.FromRatio(45,100), Fix64.FromRatio(7,10), Fix64.FromRatio(45,100)),
                new FixVector3(Fix64.Half, Fix64.FromRatio(8,10), Fix64.Half));
            Assert.IsFalse(Safe(world, FixVector3.Zero));
        }

        private static bool ClearForSize(CombatPhysicsWorld world, FixVector3 half, CombatBodyId ownBody = default)
        {
            var header = new CombatQueryHeader(910302, CombatQueryKind.Aabb, new CombatEntityId(1), 910302, 0, 0,
                CombatPhysicsLayerMask.FromLayer(1).AddLayer(2));
            return CharacterTestCharacterPlacementSafety.HasFullBodyClearance(world, header,
                ownBody.IsNone ? new CombatBodyId(1) : ownBody, FixVector3.Zero, half, Fix64.One,
                Fix64.FromRatio(1, 100), new List<CombatQueryResult>(32));
        }

        [Test] public void TargetWidthNeedsClearanceEvenWhenHeightDoesNotChange()
        {
            var world = World();
            AddBox(world, 22, 2, new FixVector3(Fix64.FromRatio(6, 10), Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.One, Fix64.FromRatio(15, 10), Fix64.Half));
            Assert.IsTrue(ClearForSize(world, new FixVector3(Fix64.Half, Fix64.One, Fix64.Half)));
            Assert.IsFalse(ClearForSize(world, new FixVector3(Fix64.FromRatio(8, 10), Fix64.One, Fix64.FromRatio(8, 10))));
        }

        [Test] public void PlacementQueryDoesNotMoveBodiesOrMutateWorldRevisions()
        {
            var world = World();
            AddBox(world, 23, 2, new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(4), Fix64.One, Fix64.One));
            Assert.IsTrue(world.TryGetBody(new CombatBodyId(23), out var before));
            int revision = world.Revision, topology = world.TopologyRevision;
            Assert.IsTrue(ClearForSize(world, new FixVector3(Fix64.Half, Fix64.One, Fix64.Half)));
            Assert.IsTrue(ClearForSize(world, new FixVector3(Fix64.FromRatio(8, 10), Fix64.One, Fix64.FromRatio(8, 10))));
            Assert.AreEqual(revision, world.Revision); Assert.AreEqual(topology, world.TopologyRevision);
            Assert.IsTrue(world.TryGetBody(new CombatBodyId(23), out var after)); Assert.AreEqual(before, after);
        }

        [Test] public void OwnMotorBodyIsIgnoredButAnotherBodyBlocksPlacement()
        {
            var world = World();
            AddBox(world, 24, 2, new FixVector3(-Fix64.Half, Fix64.Zero, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.FromInt(2), Fix64.Half));
            var half = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);
            Assert.IsTrue(ClearForSize(world, half, new CombatBodyId(24)));
            Assert.IsFalse(ClearForSize(world, half));
        }

        [Test] public void InvalidTargetDimensionsFailClosed()
        {
            Assert.IsFalse(ClearForSize(World(), new FixVector3(Fix64.Zero, Fix64.One, Fix64.Half)));
            Assert.IsFalse(ClearForSize(World(), new FixVector3(Fix64.Half, -Fix64.One, Fix64.Half)));
            Assert.IsFalse(ClearForSize(World(), new FixVector3(Fix64.Half, Fix64.One, Fix64.Zero)));
            Assert.IsFalse(ClearForSize(null, new FixVector3(Fix64.Half, Fix64.One, Fix64.Half)));
        }

        [TestCase(false)] [TestCase(true)]
        public void InstantNoActionReceiptPaysOrRefundsAndSettlesOnce(bool success)
        {
            var card = TacticalActionCardDefinition.CreatePositionRecall("recall", "record", "", 3, new TacticalPositionRecallAuthoring { Operation = "Record", LifetimeFrames = 600 }.Freeze());
            var deck = new TacticalDeckManager(); deck.InitializeDeck(new[] { card, card, card, card }, 42);
            var ledger = new MomentumLedger(50); var time = new TacticalTimeCoordinator(new TimePort()); time.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            var port = new ReceiptPort(); var driver = new TacticalSingleCardDriver(port, time, deck, ledger); Assert.IsTrue(driver.TryCommit(0, out _));
            Assert.AreEqual(50, ledger.Current); Assert.AreEqual(3, ledger.Reserved);
            port.Receipt = new ExecutionObservation(1, false, false, true, true, true, success, "", success ? 1 : -1);
            for (int tick=1;tick<=9;tick++) driver.OnCompletedSimulationTick(new SettleSample(tick,false,true,true,false,false,false,true,true,true,false,0,0));
            Assert.AreEqual(success ? 47 : 50, ledger.Current); Assert.AreEqual(0,ledger.Reserved); Assert.AreEqual(3,deck.Hand.Count); Assert.AreEqual(2ul,time.CurrentTurnIndex);
            Assert.IsFalse(driver.OnCompletedSimulationTick(new SettleSample(10,false,true,true,false,false,false,true,true,true,false,0,0)));
        }
        private sealed class TimePort : ITacticalTimeAdapter { public bool IsSimulationPaused { get; private set; } public void SetSimulationPaused(bool value)=>IsSimulationPaused=value; }
        private sealed class ReceiptPort : ITacticalExecutionPort
        {
            internal ExecutionObservation Receipt;
            public bool TryPrepare(TacticalCardInstance card,out string issue){issue="";return true;}
            public bool TryAdmit(TacticalCardInstance card,out ulong id,out string issue){id=1;issue="";return true;}
            public ExecutionObservation Observe(ulong id)=>Receipt;
            public bool TryAbortPreAction(ulong id,string reason)=>true;
        }
    }
}
