using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using MxFramework.Tests.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalExecutionAdapterTests
    {
        private float _originalScale;
        [SetUp] public void SetUp() { _originalScale = Time.timeScale; }
        [TearDown] public void TearDown() { Time.timeScale = _originalScale; }

        [Test]
        public void UnityPausePreservesConfiguredRateAndFixedStep()
        {
            float fixedStep = Time.fixedDeltaTime;
            var time = new UnityTacticalTimeAdapter(0.5f);
            time.SetSimulationPaused(false);
            Assert.AreEqual(0.5f, Time.timeScale);
            time.SetSimulationPaused(true);
            uint revision = time.PauseRevision;
            time.SetSimulationPaused(true);
            Assert.AreEqual(revision, time.PauseRevision);
            time.SetSimulationTimeScale(0.75f);
            Assert.IsTrue(time.IsSimulationPaused);
            time.SetSimulationPaused(false);
            Assert.AreEqual(0.75f, Time.timeScale);
            Assert.AreEqual(fixedStep, Time.fixedDeltaTime);
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnityTacticalTimeAdapter(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => time.SetSimulationTimeScale(0));
        }

        [Test]
        public void UnityPauseAndResumeAllocateNothingAfterWarmup()
        {
            var time = new UnityTacticalTimeAdapter();
            time.SetSimulationPaused(true); time.SetSimulationPaused(false);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { time.SetSimulationPaused(true); time.SetSimulationPaused(false); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AdmissionFailureKeepsPauseReservationAndPendingCard(bool prepareFails)
        {
            var port = new FakePort { PrepareAccepted = !prepareFails, AdmitAccepted = false };
            CreateDriver(port, out var driver, out var deck, out var ledger, out var coordinator);
            var pending = deck.PendingAction;
            Assert.IsFalse(driver.TryCommit(0, out _));
            Assert.AreSame(pending, deck.PendingAction);
            Assert.AreEqual(50, ledger.Current); Assert.AreEqual(10, ledger.Reserved);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, coordinator.Phase);
            Assert.IsTrue(coordinator.IsWorldPaused);
        }

        [Test]
        public void MockPortCompletesOneTurnOnlyAfterTimelineAndEightStableTicks()
        {
            var port = new FakePort();
            CreateDriver(port, out var driver, out var deck, out var ledger, out var coordinator);
            Assert.IsTrue(driver.TryCommit(0, out _));
            Assert.IsFalse(driver.TryCommit(0, out _));
            Assert.AreEqual(40, ledger.Current); Assert.AreEqual(0, ledger.Reserved);
            port.Observation = new ExecutionObservation(1, true, false, true, true, true);
            for (int i = 1; i <= 10; i++) Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(i)));
            Assert.AreEqual(2, deck.Hand.Count);
            port.Observation = new ExecutionObservation(1, true, true, true, true, true);
            for (int i = 11; i < 18; i++) Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(i)));
            Assert.IsTrue(driver.OnCompletedSimulationTick(Stable(18)));
            Assert.AreEqual(3, deck.Hand.Count); Assert.AreEqual(2ul, coordinator.CurrentTurnIndex);
            Assert.IsTrue(coordinator.IsWorldPaused);
            Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(18)));
            Assert.AreEqual(40, ledger.Current, "Completing does not manufacture hit rewards.");
        }

        [Test]
        public void TimeoutAbortsOnlyPreActionAndSuspensionDoesNotRefill()
        {
            var port = new FakePort();
            CreateDriver(port, out var driver, out var deck, out _, out var coordinator, 2, 4);
            Assert.IsTrue(driver.TryCommit(0, out _));
            driver.OnCompletedSimulationTick(Stable(1)); driver.OnCompletedSimulationTick(Stable(2));
            Assert.AreEqual(1, port.Aborts);
            port.Observation = new ExecutionObservation(1, true, false, false, false, true);
            driver.OnCompletedSimulationTick(Stable(3)); driver.OnCompletedSimulationTick(Stable(4));
            Assert.AreEqual(1, port.Aborts);
            Assert.AreEqual(TacticalTurnPhase.Suspended, coordinator.Phase);
            Assert.AreEqual(2, deck.Hand.Count); Assert.NotNull(deck.ResolvingAction);
            Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(5000)));
            Assert.AreEqual(1ul, coordinator.CurrentTurnIndex);
            coordinator.RequestResume(TacticalPauseReason.RecoveryTimeout);
            port.Observation = new ExecutionObservation(1, true, true, true, true, true);
            for (int i = 5; i < 12; i++) Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(i)));
            Assert.IsTrue(driver.OnCompletedSimulationTick(Stable(12)), "Resuming protection must allow another complete stability window.");
        }

        [Test]
        public void MenuPauseGatesTicksAndSurvivesCommit()
        {
            var port = new FakePort();
            CreateDriver(port, out var driver, out _, out _, out var coordinator, 1, 5);
            coordinator.RequestPause(TacticalPauseReason.UserMenu);
            Assert.IsTrue(driver.TryCommit(0, out _));
            Assert.IsTrue(coordinator.IsWorldPaused);
            driver.OnCompletedSimulationTick(Stable(100));
            Assert.AreEqual(0, port.Aborts);
            coordinator.RequestResume(TacticalPauseReason.UserMenu);
            driver.OnCompletedSimulationTick(Stable(1));
            Assert.AreEqual(1, port.Aborts);
        }

        [Test]
        public void StaleObservationFaultsWithoutCompletingTheCard()
        {
            var port = new FakePort();
            CreateDriver(port, out var driver, out var deck, out _, out var coordinator);
            Assert.IsTrue(driver.TryCommit(0, out _));
            port.Observation = new ExecutionObservation(9, true, true, true, true, true);
            Assert.IsFalse(driver.OnCompletedSimulationTick(Stable(1)));
            Assert.AreEqual(TacticalTurnPhase.Suspended, coordinator.Phase);
            Assert.NotNull(deck.ResolvingAction);
        }

        [Test]
        public void RealSessionFlowsThroughCommandsActionFinishAndSettle()
        {
            using (var fixture = CreateFixture())
            {
                var time = new UnityTacticalTimeAdapter();
                var adapter = Adapter(fixture, time);
                CreateDriver(adapter, out var driver, out var deck, out var ledger, out var coordinator, time: time, actionId: fixture.Probe.Action.ActionId);
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                Assert.IsTrue(driver.TryCommit(fixture.NextFrame - 1, out string issue), issue);
                Assert.AreEqual(0, fixture.Commands.PendingCommandCount, "Admission must not enqueue motion/actions.");
                bool started = false, finished = false, ready = false;
                SettleSample lastSample = default;
                long hotPathBytes = 0;
                for (int i = 0; i < 600 && !ready; i++)
                {
                    fixture.RefreshFacts(includeHurtboxDistance: true);
                    adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                    long beforeTick = GC.GetAllocatedBytesForCurrentThread();
                    adapter.Tick();
                    if (i > 16) hotPathBytes += GC.GetAllocatedBytesForCurrentThread() - beforeTick;
                    fixture.AdvanceWorldOnly();
                    long tick = fixture.NextFrame - 1;
                    adapter.SetPhysicsSample(tick, null, default);
                    var observation = adapter.Observe(driver.ExecutionId);
                    started |= observation.ActionStarted;
                    finished |= observation.ActionTimelineCompleted;
                    if (started) Assert.IsFalse(adapter.TryAbortPreAction(driver.ExecutionId, "timeout"));
                    long beforeSample = GC.GetAllocatedBytesForCurrentThread();
                    var sample = adapter.BuildSettleSample(tick, fixture.Source);
                    ready = driver.OnCompletedSimulationTick(in sample);
                    if (i > 16) hotPathBytes += GC.GetAllocatedBytesForCurrentThread() - beforeSample;
                    lastSample = sample;
                    Assert.IsFalse(adapter.HasExecutionFault, adapter.LastIssue);
                }
                Assert.IsTrue(started, "No acknowledged action start. " + adapter.LastIssue + " / " + fixture.Card.State);
                Assert.IsTrue(finished, "No matching natural ActionFinished event. " + Describe(fixture));
                Assert.IsTrue(ready, adapter.LastIssue + " / " + fixture.Card.State
                    + " neutral=" + lastSample.NeutralPoseReached + " mode=" + fixture.Source.MovementMode
                    + " reaction=" + lastSample.HasReactionOrRecovery + " landing=" + lastSample.HasPendingLanding
                    + " pending=" + lastSample.HasPendingMotionOrImpulse + " released=" + lastSample.OwnedIntentsReleased
                    + " ack=" + lastSample.CommandsAcknowledged + " support=" + lastSample.HasStableSupport
                    + " linear=" + lastSample.RelativeLinearSpeed + " angular=" + lastSample.RelativeAngularSpeed);
                Assert.AreEqual(0, hotPathBytes, "Warmed Session Tick and completed-tick bridge sampling must be NoAlloc.");
                Assert.AreEqual(3, deck.Hand.Count); Assert.AreEqual(40, ledger.Current);
                Assert.AreEqual(TacticalTurnPhase.ReadyPaused, coordinator.Phase);
                Assert.IsTrue(time.IsSimulationPaused);
            }
        }

        [Test]
        public void RealPrepareRejectsBindingMismatchAndStaleCardVersion()
        {
            using (var fixture = CreateFixture())
            {
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                var deck = Deck("wrong"); var ledger = new MomentumLedger(50);
                deck.SetPendingAction(deck.Hand[0], ledger);
                Assert.IsFalse(adapter.TryPrepare(deck.PendingAction, out _));
                deck = Deck(actionId: fixture.Probe.Action.ActionId); ledger = new MomentumLedger(50);
                var card = deck.Hand[0]; deck.SetPendingAction(card, ledger);
                Assert.IsTrue(adapter.TryPrepare(card, out string issue), issue);
                deck.CancelPendingAction(ledger); deck.SetPendingAction(card, ledger);
                Assert.IsFalse(adapter.TryAdmit(card, out _, out issue));
                Assert.AreEqual("execution:StalePreparation", issue);
                Assert.AreEqual(0ul, adapter.Observe(999).ExecutionId);
            }
        }

        [Test]
        public void PublishedWaitTimeoutRetiresRealFailedSessionAndRestoresHistoryWithoutRefundReplay()
        {
            using var fixture = CreateFixture();
            var inputs = CharacterTestTacticalCardInputs.Unbound(fixture.Source.Id.Value);
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTargetDistance(
                new CharacterTestTacticalDistanceData { Kind = "Fixed", Minimum = 0, Maximum = 0.001f, Metric = "RootPlanarDistance" }, maximumFrames: 4);
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("wait-fixture", "bc_qkjc3ka7za", "Wait fixture", "Tactical", null);
            draft.SetTacticalSource(source);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var issues), string.Join(";", issues));
            var frozen = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled)), null);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("Wait fixture", "fixture.icon", 5, "Wait fixture", "", "");
            var published = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, metadata, CharacterTestAuthoredContentIds.D417_Id3), null, frozen);
            Assert.IsTrue(published.Success);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, fixture.Runtime.ActionRuntime.Registry);
            var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("fixture", 1, new[] { published.Card }, references);
            Assert.IsTrue(catalog.Success);
            var state = new CharacterTestRuntimeAiPlannerProjectCatalogState(catalog.Catalog,
                new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog("fixture", 1, null), "", "", "", "", "");
            var definition = new TacticalActionCardDefinition("fixture.wait", "Wait fixture", "", 5,
                published.Card.StableCardId, published.Card.Revision,
                published.Card.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            var bindings = new System.Collections.Generic.Dictionary<ulong, System.Collections.ObjectModel.ReadOnlyDictionary<string, int>> {
                [1] = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new System.Collections.Generic.Dictionary<string, int>()) };
            var deployment = TacticalDeployment.CreateLevel(new[] { new TacticalDeploymentParticipant("fixture", fixture.Source.Identity.CharacterId,
                "fixture", "Fixture", fixture.Source.Team.TeamId, true, default, 0, new TacticalCardDefinition[] { definition }, bindings) }, 50);
            var time = new UnityTacticalTimeAdapter(); var adapter = new CharacterTestTacticalExecutionAdapter(fixture.Runtime,
                fixture.Commands, fixture.Card, fixture.Definition, fixture.Source, time, deployment: deployment, publishedCatalogLoader: _ => state);
            var deck = new TacticalDeckManager(); deck.InitializeDeck(new[] { definition, definition, definition, definition }, 42);
            var ledger = new MomentumLedger(50); var coordinator = new TacticalTimeCoordinator(time); coordinator.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            var driver = new TacticalSingleCardDriver(adapter, coordinator, deck, ledger);
            adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
            Assert.IsTrue(driver.TryCommit(fixture.NextFrame - 1, out var issue), issue);
            ulong id = driver.ExecutionId; bool ready = false;
            for (int i = 0; i < 120 && !ready; i++) {
                fixture.RefreshFacts(); adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame)); adapter.Tick();
                fixture.AdvanceWorldOnly(); long tick = fixture.NextFrame - 1; adapter.SetPhysicsSample(tick, null, default);
                var sample = adapter.BuildSettleSample(tick, fixture.Source); ready = driver.OnCompletedSimulationTick(in sample);
                Assert.IsFalse(adapter.HasExecutionFault, adapter.LastIssue);
            }
            Assert.IsTrue(ready, adapter.LastIssue); Assert.AreEqual(50, ledger.Current);
            var saved = adapter.CaptureIdleExecutionIdentity();
            Assert.AreEqual((int)CharacterTestRuntimeAiLabLogicRuntimeState.Failed, saved.SessionState);
            Assert.AreEqual((int)CharacterTestBehaviorExecutionEndReason.TimedOut, saved.EndReason);
            Assert.IsFalse(saved.ActionStarted); Assert.IsTrue(saved.Released && saved.Acknowledged);
            var session = new CharacterTestTacticalCardSession(fixture.Runtime, fixture.Source.Id.Value, fixture.Binding, fixture.Commands.Enqueue);
            var restored = new CharacterTestTacticalExecutionAdapter(fixture.Runtime, fixture.Commands, session, fixture.Definition, fixture.Source, time);
            restored.RestoreIdleExecutionIdentity(saved, deck, fixture.NextFrame - 1);
            Assert.IsTrue(restored.Observe(id).BehaviorTerminal); Assert.IsFalse(restored.TryAbortPreAction(id, "late"));
            Assert.IsFalse(restored.TryApplyHitReward(id, 1, 5, ledger)); Assert.AreEqual(50, ledger.Current);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Inactive, session.State);
        }

        [Test]
        public void RealPrepareWithExplicitActionIdValidatesContentInsteadOfBasicMappings()
        {
            using (var fixture = CreateFixture())
            {
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                var ledger = new MomentumLedger(50);

                // 1. Unequipped ActionId is rejected at adapter admission
                var unequippedDef = new TacticalActionCardDefinition("unequipped_action", "Unequipped", "", 10,
                    new CharacterActionId(999999), 1f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
                var unequippedDeck = DeckWithDef(unequippedDef);
                Assert.AreEqual(CardPlayFailure.None, unequippedDeck.SetPendingAction(unequippedDeck.Hand[0], ledger));
                Assert.IsFalse(adapter.TryPrepare(unequippedDeck.PendingAction, out string unequippedIssue));
                Assert.AreEqual("ActionSlot:SelectedTestActionUnavailable", unequippedIssue);

                // 2. Equipped ActionId prepares successfully and sets Compilation.ActionId
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var actionSet));
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveOpener(actionSet, fixture.Runtime.ActionRuntime.Registry.Actions, out var openerAction, out _));
                int equippedId = openerAction.ActionId.Value;
                var equippedDef = new TacticalActionCardDefinition("equipped_action", "Equipped", "", 10,
                    new CharacterActionId(equippedId), 1f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
                var equippedDeck = DeckWithDef(equippedDef);
                var ledger2 = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, equippedDeck.SetPendingAction(equippedDeck.Hand[0], ledger2));
                Assert.IsTrue(adapter.TryPrepare(equippedDeck.PendingAction, out string equippedIssue), equippedIssue);
                Assert.IsNotNull(fixture.Card.Compilation);
                Assert.AreEqual(equippedId, fixture.Card.Compilation.ActionId);

                // 3. Combo derivative ActionId (e.g. second segment in LightAttack sequence) is rejected as opener
                int derivativeId = 456;
                if (actionSet.CommandSequences.Length > 0 && actionSet.CommandSequences[0].ActionIds.Length > 1)
                    derivativeId = actionSet.CommandSequences[0].ActionIds[1].Value;
                var derivativeDef = new TacticalActionCardDefinition("derivative_action", "Derivative", "", 10,
                    new CharacterActionId(derivativeId), 1f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
                var derivativeDeck = DeckWithDef(derivativeDef);
                var ledger3 = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, derivativeDeck.SetPendingAction(derivativeDeck.Hand[0], ledger3));
                adapter.TryPrepare(derivativeDeck.PendingAction, out string derivativeIssue);
                Assert.AreNotEqual("execution:ComboDerivativeNotAllowedAsOpener", derivativeIssue);
                Assert.AreNotEqual("execution:ActionNotEquippedOnCharacter", derivativeIssue);
            }
        }

        [Test]
        public void T06_PhysicalCardsKeepDistinctCostsWithoutIndependentCardCooldowns()
        {
            var tracker = new TacticalCooldownTracker(60f);
            var actionId = new CharacterActionId(301);
            var cardA = new TacticalActionCardDefinition("card_A", "Card A (Fast)", "", 10,
                actionId, cooldownSeconds: 2f);
            var cardB = new TacticalActionCardDefinition("card_B", "Card B (Heavy)", "", 25,
                actionId, cooldownSeconds: 6f, sharedCooldownGroup: "old.group");
            var deckA = DeckWithDef(cardA);
            var deckB = DeckWithDef(cardB);
            deckA.BindCooldownTracker(tracker, 1, () => 150);
            deckB.BindCooldownTracker(tracker, 1, () => 150);
            var ledger = new MomentumLedger(50);
            tracker.TriggerCooldown(1, cardA, 100);
            tracker.TriggerCooldown(1, cardB, 100);
            Assert.AreEqual(10, cardA.BaseMomentumCost);
            Assert.AreEqual(25, cardB.BaseMomentumCost);
            Assert.IsFalse(tracker.IsOnCooldown(1, cardA, 150));
            Assert.IsFalse(tracker.IsOnCooldown(1, cardB, 150));
            Assert.AreEqual(0f, tracker.GetRemainingCooldownSeconds(1, cardA, 150));
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
            Assert.IsTrue(deckA.CanPlayCard(deckA.Hand[0], ledger, false, out var failureA));
            Assert.AreEqual(CardPlayFailure.None, failureA);
            Assert.IsTrue(deckB.CanPlayCard(deckB.Hand[0], ledger, false, out var failureB));
            Assert.AreEqual(CardPlayFailure.None, failureB);
            // Real action availability is checked by ActionUseAuthority/Ability owner, not this tracker.
        }

        [Test]
        public void T07_PreActionAbortRefundsMomentumWhileActionStartedRetainsAndRewards()
        {
            var tracker = new TacticalCooldownTracker(60f);
            int characterId = 1;
            var actionId = new CharacterActionId(301);
            var cardDef = new TacticalActionCardDefinition("card_t7", "Card T7", "", 10,
                actionId, cooldownSeconds: 2f, momentumReward: 5);

            // Scenario A: Pre-action abort (e.g. approach failure) -> Full refund of 10 momentum, CD not triggered, no reward
            var portA = new FakePort();
            var deckA = DeckWithDef(cardDef);
            deckA.BindCooldownTracker(tracker, characterId);
            var ledgerA = new MomentumLedger(50);
            var coordinatorA = new TacticalTimeCoordinator(new UnityTacticalTimeAdapter());
            coordinatorA.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deckA.SetPendingAction(deckA.Hand[0], ledgerA));
            var driverA = new TacticalSingleCardDriver(portA, coordinatorA, deckA, ledgerA);

            Assert.IsTrue(driverA.TryCommit(0, out _));
            Assert.AreEqual(40, ledgerA.Current); // 10 deducted on commit

            // Terminal without action start (NoActionStarted)
            portA.Observation = new ExecutionObservation(1, false, false, true, true, true);
            for (int i = 1; i < 8; i++) Assert.IsFalse(driverA.OnCompletedSimulationTick(Stable(i)));
            Assert.IsTrue(driverA.OnCompletedSimulationTick(Stable(8)));

            // Settled! Full refund of 10 momentum
            Assert.AreEqual(50, ledgerA.Current, "Pre-action abort must fully refund the committed momentum cost.");
            Assert.IsFalse(tracker.IsOnCooldown(characterId, cardDef, 8), "Cooldown must not be triggered if action never started.");

            // Scenario B: Action started -> card fee retained; no separate card CD; completion grants reward
            var portB = new FakePort();
            var deckB = DeckWithDef(cardDef);
            deckB.BindCooldownTracker(tracker, characterId);
            var ledgerB = new MomentumLedger(50);
            var coordinatorB = new TacticalTimeCoordinator(new UnityTacticalTimeAdapter());
            coordinatorB.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deckB.SetPendingAction(deckB.Hand[0], ledgerB));
            var driverB = new TacticalSingleCardDriver(portB, coordinatorB, deckB, ledgerB);

            Assert.IsTrue(driverB.TryCommit(10, out _));
            Assert.AreEqual(40, ledgerB.Current);

            // Tick 11: Action starts!
            portB.Observation = new ExecutionObservation(1, true, false, false, false, true);
            Assert.IsFalse(driverB.OnCompletedSimulationTick(Stable(11)));
            Assert.IsFalse(tracker.IsOnCooldown(characterId, cardDef, 11), "Physical actions must not start a separate card cooldown.");

            // Tick 12..19: Settle with action timeline completed (8 consecutive stable ticks in AwaitingSettle)!
            portB.Observation = new ExecutionObservation(1, true, true, true, true, true);
            for (int i = 12; i < 19; i++) Assert.IsFalse(driverB.OnCompletedSimulationTick(Stable(i)));
            Assert.IsTrue(driverB.OnCompletedSimulationTick(Stable(19)));

            // Settle completed:
            // 1. Initial 50 - 10 cost = 40 (NOT refunded because action started).
            // 2. Action timeline completed: +5 reward added = 45!
            Assert.AreEqual(45, ledgerB.Current, "Action completion must retain cost and award MomentumReward.");
            Assert.IsFalse(tracker.IsOnCooldown(characterId, cardDef, 19), "Only the real Ability owner may retain an action cooldown.");
        }

        [Test]
        public void RealPreActionAbortWaitsForControlCommandDrain()
        {
            using (var fixture = CreateFixture())
            {
                fixture.PlaceTarget(10000);
                var time = new UnityTacticalTimeAdapter();
                var adapter = Adapter(fixture, time);
                CreateDriver(adapter, out var driver, out _, out _, out _, time: time, actionId: fixture.Probe.Action.ActionId);
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                Assert.IsTrue(driver.TryCommit(fixture.NextFrame - 1, out string issue), issue);
                fixture.AdvanceWorldOnly();
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                adapter.Tick();
                Assert.IsFalse(adapter.TryAbortPreAction(driver.ExecutionId, "timeout"), "Queued commands must drain first.");
                fixture.AdvanceWorldOnly();
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                Assert.IsTrue(adapter.TryAbortPreAction(driver.ExecutionId, "timeout"));
                Assert.IsFalse(adapter.Observe(driver.ExecutionId).OwnedIntentsReleased);
                fixture.AdvanceWorldOnly();
                var observed = adapter.Observe(driver.ExecutionId);
                Assert.IsTrue(observed.BehaviorTerminal); Assert.IsTrue(observed.CommandsAcknowledged);
                Assert.IsTrue(observed.OwnedIntentsReleased); Assert.IsFalse(observed.ActionStarted);
                Assert.IsFalse(observed.ActionTimelineCompleted);
            }
        }

        [Test]
        public void SettleSamplingHandlesRelativeBodyVelocityAirborneAndMissingPhysicsTick()
        {
            using (var fixture = CreateFixture())
            {
                var root = new GameObject("tactical-sample-test");
                var platform = new GameObject("tactical-support-test");
                try
                {
                    var body = root.AddComponent<Rigidbody>(); body.useGravity = false;
                    var support = platform.AddComponent<Rigidbody>(); support.useGravity = false;
                    var adapter = Adapter(fixture, new UnityTacticalTimeAdapter(), body);
                    fixture.Source.SetMotionState(0, Vector2.zero, isGrounded: true);
                    body.linearVelocity = new Vector3(3, 4, 0); body.angularVelocity = Vector3.up;
                    adapter.SetPhysicsSample(20, null, default);
                    var sample = adapter.BuildSettleSample(20, fixture.Source);
                    Assert.AreEqual(5f, sample.RelativeLinearSpeed, 0.0001f);
                    Assert.AreEqual(1f, sample.RelativeAngularSpeed, 0.0001f);
                    Assert.IsTrue(sample.HasStableSupport); Assert.IsFalse(sample.HasPendingLanding);
                    support.linearVelocity = Vector3.right; support.angularVelocity = Vector3.up;
                    Vector3 point = support.worldCenterOfMass + Vector3.right;
                    body.linearVelocity = support.GetPointVelocity(point);
                    adapter.SetPhysicsSample(21, support, point, true, true);
                    sample = adapter.BuildSettleSample(21, fixture.Source);
                    Assert.AreEqual(0f, sample.RelativeLinearSpeed, 0.0001f);
                    Assert.AreEqual(0f, sample.RelativeAngularSpeed, 0.0001f);
                    Assert.IsTrue(sample.HasNewDestabilizingContact); Assert.IsTrue(sample.HasPendingMotionOrImpulse);
                    Assert.IsFalse(adapter.BuildSettleSample(22, fixture.Source).HasStableSupport);
                    fixture.Source.SetMotionState(0, Vector2.zero, isGrounded: false);
                    adapter.SetPhysicsSample(23, null, default);
                    sample = adapter.BuildSettleSample(23, fixture.Source);
                    Assert.IsTrue(sample.HasPendingLanding); Assert.IsFalse(sample.HasStableSupport);
                    Assert.IsFalse(adapter.BuildSettleSample(23, null).NeutralPoseReached);
                    Assert.IsFalse(adapter.BuildSettleSample(23, fixture.Target).HasStableSupport);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(platform); }
            }
        }

        [Test]
        public void EntitySamplingAndObservationAllocateNothingAfterWarmup()
        {
            using (var fixture = CreateFixture())
            {
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                adapter.SetPhysicsSample(1, null, default);
                adapter.BuildSettleSample(1, fixture.Source);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 1; i <= 1000; i++)
                {
                    adapter.SetPhysicsSample(i, null, default);
                    adapter.BuildSettleSample(i, fixture.Source);
                    adapter.Observe(0);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0, allocated);
            }
        }

        [Test]
        public void SingleCardDriverTurnLoopAllocatesNothingAfterWarmup()
        {
            var port = new FakePort { Observation = new ExecutionObservation(1, true, true, true, true, true) };
            CreateDriver(port, out var driver, out var deck, out var ledger, out _);
            bool valid = DriverRound(driver, deck, ledger, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 1000; i++) valid &= DriverRound(driver, deck, ledger, i * 8);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(valid); Assert.AreEqual(0, allocated);
        }

        private static bool DriverRound(TacticalSingleCardDriver driver, TacticalDeckManager deck, MomentumLedger ledger, int tick)
        {
            ledger.Grant(10);
            if (deck.PendingAction == null && deck.SetPendingAction(deck.Hand[0], ledger) != CardPlayFailure.None) return false;
            if (!driver.TryCommit(tick, out _)) return false;
            bool completed = false;
            for (int i = 1; i <= 8; i++) completed = driver.OnCompletedSimulationTick(Stable(tick + i));
            return completed;
        }

        private static CharacterTestTacticalCardRuntimeFixture CreateFixture()
        {
            // Resolve current content instead of assuming the historic 301 fixture is still the equipped opener.
            var dummy = new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0);
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, dummy))
                return new CharacterTestTacticalCardRuntimeFixture(actionId: probe.Action.ActionId.Value);
        }

        private static string Describe(CharacterTestTacticalCardRuntimeFixture fixture)
        {
            var text = new System.Text.StringBuilder();
            text.Append("session=").Append(fixture.Card.State).Append(" local=").Append(fixture.Card.LocalFrame)
                .Append(" owned=").Append(fixture.Card.AcknowledgedActionInstance?.InstanceId ?? 0)
                .Append(" action=").Append(fixture.Source.Action.State).Append(" frame=").Append(fixture.Source.Action.LocalFrame);
            var events = fixture.Runtime.ActionRuntime.TimelineEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.EntityId == fixture.Source.Id.Value && evt.Kind != CharacterActionRunnerEventKind.TrackLifecycle
                    && evt.Kind != CharacterActionRunnerEventKind.TrackEventFired)
                    text.Append("; ").Append(evt.Kind).Append('/').Append(evt.InstanceId).Append('@').Append(evt.Frame);
            }
            return text.ToString();
        }

        [Test]
        public void T12_ActionSlotResolvesDifferentlyAcrossWeaponEquipmentSets()
        {
            using (var fixture = CreateFixture())
            {
                var actions = fixture.Runtime.ActionRuntime.Registry.Actions;

                // Create spear ActionSet with PrimaryAttack sequence starting at 301
                var spearSet = new CharacterActionSetConfig(
                    101, "set.spear", "Spear Loadout", "char.tester", "equip.spear",
                    commandBindings: Array.Empty<CharacterActionBinding>(),
                    abilityBindings: Array.Empty<CharacterAbilityActionBinding>(),
                    reactionBindings: Array.Empty<CharacterReactionBinding>(),
                    movementProfileId: 1, reactionProfileId: "",
                    defaultActionId: new CharacterActionId(301),
                    commandSequences: new[]
                    {
                        new CharacterActionCommandSequence(CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId,
                            new[] { new CharacterActionId(301) })
                    });

                // Create greatsword ActionSet with PrimaryAttack sequence starting at 950003
                var greatswordSet = new CharacterActionSetConfig(
                    102, "set.greatsword", "Greatsword Loadout", "char.tester", "equip.greatsword",
                    commandBindings: Array.Empty<CharacterActionBinding>(),
                    abilityBindings: Array.Empty<CharacterAbilityActionBinding>(),
                    reactionBindings: Array.Empty<CharacterReactionBinding>(),
                    movementProfileId: 1, reactionProfileId: "",
                    defaultActionId: new CharacterActionId(950003),
                    commandSequences: new[]
                    {
                        new CharacterActionCommandSequence(CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId,
                            new[] { new CharacterActionId(950003) })
                    });

                // Card declares slot PrimaryAttack without hardcoded ActionId
                var slotCard = new TacticalActionCardDefinition("card.primary_slot", "Weapon Strike", "", 10,
                    TacticalActionSlot.PrimaryAttack);
                Assert.IsTrue(slotCard.HasActionSlot);
                Assert.IsFalse(slotCard.HasActionId);

                // Slot resolution on spear resolves to 301
                bool spearOk = CharacterTestTacticalCardInputs.TryResolveActionSlot(spearSet, actions,
                    TacticalActionSlot.PrimaryAttack, out var spearAction, out var spearIssue);
                Assert.IsTrue(spearOk, spearIssue);
                Assert.AreEqual(301, spearAction.ActionId.Value);

                // Slot resolution on greatsword resolves to 950003
                bool gsOk = CharacterTestTacticalCardInputs.TryResolveActionSlot(greatswordSet, actions,
                    TacticalActionSlot.PrimaryAttack, out var gsAction, out var gsIssue);
                Assert.IsTrue(gsOk, gsIssue);
                Assert.AreEqual(950003, gsAction.ActionId.Value);

                // Native admission requires an explicitly selected action; retain the slot resolver assertions above.
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                var selectedCard = new TacticalActionCardDefinition("card.primary_selected", "Weapon Strike", "", 10,
                    actionId: fixture.Probe.Action.ActionId, actionSlot: TacticalActionSlot.PrimaryAttack);
                var deck = DeckWithDef(selectedCard);
                var ledger = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));

                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                Assert.IsTrue(adapter.TryPrepare(deck.PendingAction, out var prepIssue), prepIssue);
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var equippedSet));
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveActionSlot(equippedSet, fixture.Runtime.ActionRuntime.Registry.Actions, TacticalActionSlot.PrimaryAttack, out var resolvedSlotAction, out _));
                Assert.AreEqual(resolvedSlotAction.ActionId.Value, fixture.Card.Compilation.ActionId);
            }
        }

        [Test]
        public void T12_DynamicEquipmentChangeAfterPrepareRejectsAdmissionWithEquipmentChangedAfterPrepare()
        {
            using (var fixture = CreateFixture())
            {
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                var slotCard = new TacticalActionCardDefinition("card.slot", "Slot Card", "", 10,
                    actionId: fixture.Probe.Action.ActionId, actionSlot: TacticalActionSlot.PrimaryAttack);
                var deck = DeckWithDef(slotCard);
                var ledger = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));

                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));

                // 1. TryPrepare succeeds with initial equipment
                Assert.IsTrue(adapter.TryPrepare(deck.PendingAction, out var prepIssue), prepIssue);

                // 2. Dynamically change character equipment version
                var originalEquipment = fixture.Source.Equipment;
                var newEquipment = new CharacterTestCharacterEquipmentComponent(originalEquipment.UseDefaultLoadout, originalEquipment.EquipmentStateStableId, version: originalEquipment.Version + 1);
                fixture.Source.SetEquipment(newEquipment);

                // 3. TryAdmit must fail because equipment snapshot changed after prepare
                Assert.IsFalse(adapter.TryAdmit(deck.PendingAction, out ulong execId, out var admitIssue));
                Assert.AreEqual("execution:EquipmentChangedAfterPrepare", admitIssue);

                // 4. Re-prepare restores alignment
                Assert.IsTrue(adapter.TryPrepare(deck.PendingAction, out prepIssue), prepIssue);
                Assert.IsTrue(adapter.TryAdmit(deck.PendingAction, out execId, out admitIssue), admitIssue);
                Assert.IsTrue(execId > 0);
            }
        }

        [Test]
        public void T12_EmptyActionSlotAndDualConfigurationHandling()
        {
            using (var fixture = CreateFixture())
            {
                var adapter = Adapter(fixture, new UnityTacticalTimeAdapter());
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));

                // A. Empty card (neither ActionId nor ActionSlot)
                var emptyCard = new TacticalActionCardDefinition("card.empty", "Empty", "", 10,
                    default, cooldownSeconds: 0f, actionSlot: TacticalActionSlot.None);
                var emptyDeck = DeckWithDef(emptyCard);
                var ledgerA = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, emptyDeck.SetPendingAction(emptyDeck.Hand[0], ledgerA));
                Assert.IsFalse(adapter.TryPrepare(emptyDeck.PendingAction, out var emptyIssue));
                Assert.AreEqual("Card:MissingActionOrSlotBinding", emptyIssue);

                // B. A slot-only card must be rejected until an action is explicitly selected
                var guardCard = new TacticalActionCardDefinition("card.guard", "Guard Slot", "", 10,
                    TacticalActionSlot.Guard);
                var guardDeck = DeckWithDef(guardCard);
                var ledgerB = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, guardDeck.SetPendingAction(guardDeck.Hand[0], ledgerB));
                Assert.IsFalse(adapter.TryPrepare(guardDeck.PendingAction, out var guardIssue));
                Assert.AreEqual("Action:ExplicitSelectionRequired", guardIssue);

                // C. Dual configuration: Fixed ActionId + ActionSlot (Mobility)
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var actionSet));
                int equippedId = actionSet.DefaultActionId.IsValid ? actionSet.DefaultActionId.Value : 455;
                var dualCard = new TacticalActionCardDefinition("card.dual", "Dual Config", "", 10,
                    actionId: new CharacterActionId(equippedId), cooldownSeconds: 0f, actionSlot: TacticalActionSlot.Mobility);
                Assert.IsTrue(dualCard.ConfigurationWarning.StartsWith("FixedActionOverridesSlot"));

                var dualDeck = DeckWithDef(dualCard);
                var ledgerC = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, dualDeck.SetPendingAction(dualDeck.Hand[0], ledgerC));
                Assert.IsTrue(adapter.TryPrepare(dualDeck.PendingAction, out var dualIssue), dualIssue);
                Assert.AreEqual("FixedActionOverridesSlot", adapter.LastWarning);
                Assert.AreEqual(equippedId, fixture.Card.Compilation.ActionId);
            }
        }

        [Test]
        public void T16_BoundProfileIdEliminatedFromCardExecution()
        {
            using (var fixture = CreateFixture())
            {
                // Verify adapter constructor does not require profileId
                var adapter = new CharacterTestTacticalExecutionAdapter(
                    fixture.Runtime, fixture.Commands, fixture.Card, fixture.Definition, fixture.Source,
                    new UnityTacticalTimeAdapter());

                // Card definition BoundProfileId is deprecated and returns empty
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var actionSet));
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveOpener(actionSet, fixture.Runtime.ActionRuntime.Registry.Actions, out var openerAction, out _));
                int equippedId = openerAction.ActionId.Value;
                var cardDef = new TacticalActionCardDefinition("card.t16", "T16", "", 10,
                    actionId: new CharacterActionId(equippedId));
#pragma warning disable CS0618
                Assert.AreEqual(string.Empty, cardDef.BoundProfileId);
#pragma warning restore CS0618

                var deck = DeckWithDef(cardDef);
                var ledger = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));

                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                Assert.IsTrue(adapter.TryPrepare(deck.PendingAction, out var prepIssue), prepIssue);
                Assert.IsTrue(adapter.TryAdmit(deck.PendingAction, out ulong execId, out var admitIssue), admitIssue);
                Assert.IsTrue(execId > 0);
            }
        }

        private static CharacterTestTacticalExecutionAdapter Adapter(CharacterTestTacticalCardRuntimeFixture fixture,
            ITacticalTimeAdapter time, Rigidbody body = null) => new CharacterTestTacticalExecutionAdapter(
                fixture.Runtime, fixture.Commands, fixture.Card, fixture.Definition, fixture.Source, time, body);

        private static TacticalDeckManager Deck(string profile = "primary", CharacterActionId actionId = default)
        {
            var definition = profile == "wrong"
                ? new TacticalActionCardDefinition("attack_wrong", "Attack", "", 10,
                    TacticalActionSlot.None, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange)
                : actionId.IsValid
                    ? new TacticalActionCardDefinition("attack", "Attack", "", 10,
                        actionId, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange)
                    : new TacticalActionCardDefinition("attack", "Attack", "", 10,
                    TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new[] { definition, definition, definition, definition }, 42);
            return deck;
        }

        private static TacticalDeckManager DeckWithDef(TacticalActionCardDefinition definition)
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new[] { definition, definition, definition, definition }, 42);
            return deck;
        }

        private static void CreateDriver(ITacticalExecutionPort port, out TacticalSingleCardDriver driver,
            out TacticalDeckManager deck, out MomentumLedger ledger, out TacticalTimeCoordinator coordinator,
            int preAction = 240, int total = 1800, ITacticalTimeAdapter time = null, CharacterActionId actionId = default)
        {
            deck = Deck(actionId: actionId); ledger = new MomentumLedger(50);
            coordinator = new TacticalTimeCoordinator(time ?? new UnityTacticalTimeAdapter()); coordinator.Initialize();
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            driver = new TacticalSingleCardDriver(port, coordinator, deck, ledger, preAction, total);
        }

        private static SettleSample Stable(long tick) => new SettleSample(tick, true, true, true,
            false, false, false, true, true, true, false, 0, 0);

        private sealed class FakePort : ITacticalExecutionPort
        {
            internal bool PrepareAccepted = true, AdmitAccepted = true;
            internal int Aborts;
            internal ExecutionObservation Observation = new ExecutionObservation(1, false, false, false, false, true);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = "prepare"; return PrepareAccepted; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
            { executionId = AdmitAccepted ? 1ul : 0ul; issue = "admit"; return AdmitAccepted; }
            public ExecutionObservation Observe(ulong executionId) => Observation;
            public bool TryAbortPreAction(ulong executionId, string reason) { Aborts++; return !Observation.ActionStarted; }
        }
    }
}
