using System;
using System.Linq;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalGroundTractionTests
    {
        internal static TacticalPointMoveAuthoring Contract() => new TacticalPointMoveAuthoring {
            Mechanism = "GroundTraction", StartPolicy = "OwnedTractionAccepted", RangeMillimeters = 6000,
            ArrivalMillimeters = 250, MaximumFrames = 90, RequireReachable = true,
            TractionSpeedMillimetersPerSecond = 9000, TractionAccelerationMillimetersPerSecondSquared = 54000 };
        private static CharacterTestGroundPointTractionState State(int x = 3000, int z = 0) => new CharacterTestGroundPointTractionState {
            StageRun = 5, ExecutionId = 7, Actor = new GameplayEntityId(1, 2), AnchorX = x, AnchorZ = z,
            StopMillimeters = 250, MaximumFrames = 90, SpeedMillimetersPerSecond = 9000,
            AccelerationMillimetersPerSecondSquared = 54000, AcceptedTick = 0, LastTick = -1, StartedTick = -1,
            End = CharacterTestGroundPointTractionEnd.Active };
        [Test] public void PointContractRoundTripsWithoutWalkingOrActionSlots()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreatePointMove("定点牵引试用", Contract());
            string json = CharacterTestTacticalAuthoringJson.Write(source);
            Assert.IsFalse(json.Contains("speedMilli\""));
            var read = CharacterTestTacticalAuthoringJson.Read(json);
            Assert.IsTrue(read.ExecutionInput.Freeze().IsGroundTraction);
            Assert.AreEqual(0, read.Slots.Count); Assert.AreEqual(0, read.Timelines[0].Events.Count);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(read, CharacterTestTacticalCardInputs.Unbound(1), out _, out var issues), string.Join(";", issues));
            var wrong = JObject.Parse(json); wrong["executionInput"]["tractionSpeedMillimetersPerSecond"] = "9000";
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(wrong.ToString()));
        }
        [Test] public void LegacyWalkingCanonicalJsonDoesNotAcquireMechanismFields()
        {
            var c = new TacticalPointMoveAuthoring { RangeMillimeters = 5000, ArrivalMillimeters = 200,
                MaximumFrames = 180, SpeedMilli = 1000, RequireReachable = true };
            var s = CharacterTestTacticalAuthoringTemplates.CreatePointMove("walk", c);
            string before = CharacterTestTacticalAuthoringJson.Write(s);
            Assert.IsFalse(before.Contains("mechanism")); Assert.IsFalse(before.Contains("traction"));
            Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(CharacterTestTacticalAuthoringJson.Read(before)));
        }
        [Test] public void OwnMotionNeedsActualContributionAndStopsOnArrivalOrBlock()
        {
            var s = State(); Assert.IsTrue(s.TryStep(0, 0, 60, 0, out int x, out int z));
            Assert.AreEqual(-1, s.StartedTick);
            s.CompleteStep(100, 0, 100, 0, x, z, true, 0);
            Assert.AreEqual(-1, s.StartedTick, "Base-only motion must not pay for blocked traction.");
            Assert.AreEqual(CharacterTestGroundPointTractionEnd.Blocked, s.End); Assert.AreEqual(0, s.CurrentSpeed);
            s = State(300); s.TryStep(0, 0, 60, 0, out x, out z); s.CompleteStep(0, 0, x, z, x, z, true, 0);
            Assert.AreEqual(0, s.StartedTick); Assert.IsTrue(s.IsActive);
            s.Finish(CharacterTestGroundPointTractionEnd.Cancelled, 1); Assert.AreEqual(0, s.CurrentSpeed);
            var near = State(251); Assert.IsFalse(near.TryStep(0, 0, 60, 0, out _, out _));
            Assert.AreEqual(CharacterTestGroundPointTractionEnd.Arrived, near.End); Assert.AreEqual(-1, near.StartedTick);
        }
        [Test] public void DiagonalQuantizationAndBudgetReachTerminal()
        {
            var s = State(301, 301); s.StopMillimeters = 424;
            Assert.IsFalse(s.TryStep(0, 0, 60, 0, out _, out _)); Assert.AreEqual(CharacterTestGroundPointTractionEnd.Arrived, s.End);
            s = State(); Assert.IsFalse(s.TryStep(0, 0, 60, 90, out _, out _));
            Assert.AreEqual(CharacterTestGroundPointTractionEnd.TimedOut, s.End); Assert.AreEqual(0, s.CurrentSpeed);
        }
        [Test] public void RequestIdentityAndSnapshotAreExplicit()
        {
            var s = State(); var command = CharacterTestRuntimeCommandFactory.GroundPointTraction(RuntimeFrame.Zero, 1,
                s.Actor, s.StageRun, s.ExecutionId, new TacticalWorldPoint(3000, 0, 0), 6000, 250, 90, 9000, 54000);
            Assert.IsTrue(CharacterTestRuntimeCommandFactory.IsGroundPointTractionValid(command));
            Assert.IsFalse(s.Matches(6, 7)); Assert.IsFalse(s.Matches(5, 8));
            Assert.IsTrue(s.IsValidSnapshot(0));
            var future = s; future.LastTick = long.MaxValue; Assert.IsFalse(future.IsValidSnapshot(0));
            future = s; future.StartedTick = 2; Assert.IsFalse(future.IsValidSnapshot(0));
            var badDocument = new CharacterTestNetworkSnapshotDocument { Frame = 0, Characters = new[] {
                new CharacterTestNetworkCharacterSnapshot { GroundPointTraction = future } } };
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => badDocument.NormalizeForSchema(39));
            s.TryStep(0, 0, 60, 0, out int stepX, out int stepZ);
            s.CompleteStep(0, 0, stepX, stepZ, stepX, stepZ, true, 0);
            var saved = new CharacterTestNetworkCharacterSnapshot { GroundPointTraction = s };
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTestNetworkCharacterSnapshot>(
                Newtonsoft.Json.JsonConvert.SerializeObject(saved));
            Assert.IsTrue(restored.GroundPointTraction.IsValidSnapshot(0));
            Assert.AreEqual(s.Actor, restored.GroundPointTraction.Actor);
            Assert.AreEqual(s.StageRun, restored.GroundPointTraction.StageRun);
            Assert.AreEqual(s.ExecutionId, restored.GroundPointTraction.ExecutionId);
            Assert.AreEqual(s.AnchorX, restored.GroundPointTraction.AnchorX);
            Assert.AreEqual(s.CurrentSpeed, restored.GroundPointTraction.CurrentSpeed);
            Assert.AreEqual(s.StartedTick, restored.GroundPointTraction.StartedTick);
            var snap = new CharacterTestNetworkCharacterSnapshot { GroundPointTraction = s };
            snap.NormalizeForSchema(38); Assert.AreEqual(CharacterTestGroundPointTractionEnd.None, snap.GroundPointTraction.End);
            var a = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1); s.AddHash(ref a); var first = a.ToHash();
            s.AnchorX++; var b = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1); s.AddHash(ref b); Assert.AreNotEqual(first, b.ToHash());
        }
        [TestCase(false)] [TestCase(true)]
        public void DeferredCostAndNoActionTerminalSettleExactlyOnce(bool started)
        {
            var definition = TacticalActionCardDefinition.CreatePointMove("traction", "牵引", "", 3,
                Contract().Freeze(), 0, CardDestination.Discard);
            var deck = new TacticalDeckManager(); deck.InitializeDeck(new[] { definition, definition, definition, definition }, 42);
            var ledger = new MomentumLedger(50); var time = new TestTime();
            var coordinator = new TacticalTimeCoordinator(time); coordinator.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            var port = new ReceiptPort(); var driver = new TacticalSingleCardDriver(port, coordinator, deck, ledger);
            Assert.IsTrue(driver.TryCommit(0, out _));
            Assert.AreEqual(50, ledger.Current); Assert.AreEqual(3, ledger.Reserved); Assert.AreEqual(2, deck.Hand.Count);
            port.Observation = new ExecutionObservation(1, false, false, false, false, true, started, executionStartedTick: started ? 1 : -1);
            driver.OnCompletedSimulationTick(Stable(1));
            Assert.AreEqual(started ? 47 : 50, ledger.Current); Assert.AreEqual(started ? 0 : 3, ledger.Reserved);
            port.Observation = new ExecutionObservation(1, false, false, true, true, true, started, "traction:Blocked", started ? 1 : -1);
            for (int tick = 2; tick < 9; tick++) Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(tick)));
            Assert.IsTrue(driver.OnCompletedSimulationTick(Stable(9)));
            Assert.AreEqual(started ? 47 : 50, ledger.Current); Assert.AreEqual(0, ledger.Reserved);
            Assert.AreEqual(3, deck.Hand.Count); Assert.AreEqual(2ul, coordinator.CurrentTurnIndex);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, coordinator.Phase); Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(10)));
        }
        private static SettleSample Stable(long tick) => new SettleSample(tick, false, true, true,
            false, false, false, true, true, true, false, 0, 0);
        private sealed class TestTime : ITacticalTimeAdapter
        { public bool IsSimulationPaused { get; private set; } public void SetSimulationPaused(bool paused) => IsSimulationPaused = paused; }
        private sealed class ReceiptPort : ITacticalExecutionPort
        {
            internal ExecutionObservation Observation = new ExecutionObservation(1, false, false, false, false, true, false);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong id, out string issue) { id = 1; issue = ""; return true; }
            public ExecutionObservation Observe(ulong id) => Observation;
            public bool TryAbortPreAction(ulong id, string reason) => true;
        }
        [Test] public void NoActionPublishedTrialHasTractionDescriptionAndRestores()
        {
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("N"), "bc_qkjc3ka7za", "定点牵引试用", "Tactical", null);
            draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreatePointMove(draft.DisplayName, Contract())); draft.Cost = 3;
            var inputs = CharacterTestTacticalCardInputs.Unbound(1);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(draft.CloneTacticalSource(), inputs, out var compiled, out _));
            var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled);
            Assert.IsTrue(frozen.Description.Contains("牵引"));
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(draft.DisplayName, "ui.behavior-card.custom", 3, "", "", "");
            var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, metadata, CharacterTestAuthoredContentIds.D417_Id3),
                null, new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen), null));
            Assert.IsTrue(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
            Assert.IsTrue(result.Card.Metadata.ShortDescription.Contains("牵引")); Assert.AreEqual(3, result.Card.Metadata.Cost);
            Assert.IsTrue(CharacterTestTacticalPublishedSource.Read(result.Card.TacticalSource.ToJson()).PointMove.IsGroundTraction);
        }
    }
}
