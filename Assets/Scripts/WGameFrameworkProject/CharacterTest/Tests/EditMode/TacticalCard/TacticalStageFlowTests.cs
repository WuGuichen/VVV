using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Tests.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalStageFlowTests
    {
        private float _originalScale;
        [SetUp] public void SetUp() => _originalScale = Time.timeScale;
        [TearDown] public void TearDown() => Time.timeScale = _originalScale;

        [Test]
        public void D01_CurrentAttackPrimaryCompletesThreeRealTurns()
        {
            using (var f = new RealFlow())
            {
                var states = new List<CharacterActionInstanceState>();
                var ids = new List<ulong>();
                f.Adapter.ActionStateChanged += (id, state) => { ids.Add(id); states.Add(state); };
                f.Ready();
                Assert.AreEqual(50, f.Ledger.Current);
                for (int turn = 1; turn <= 3; turn++)
                {
                    var card = f.Deck.Hand[0];
                    f.Commit(card);
                    ulong execution = f.Driver.ExecutionId;
                    Assert.AreEqual((ulong)turn, execution);
                    Assert.AreEqual(50 - 10 * turn, f.Ledger.Current);
                    bool started = false, finished = false, released = false, settling = false, hit = false;
                    int stableTicks = 0;
                    for (int tick = 0; tick < 600 && f.Stage.Phase != TacticalStagePhase.Planning; tick++)
                    {
                        var sample = f.Step(beforeStage: observation =>
                        {
                            started |= observation.ActionStarted;
                            finished |= observation.ActionTimelineCompleted;
                            released |= observation.OwnedIntentsReleased && observation.CommandsAcknowledged;
                            hit |= HasCurrentDamage(f);
                        });
                        settling |= f.Stage.Phase == TacticalStagePhase.TurnSettling;
                        stableTicks = IsStable(sample) ? stableTicks + 1 : 0;
                        Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health, f.Adapter.LastIssue);
                        if (stableTicks < 3) Assert.AreNotEqual(TacticalStagePhase.Planning, f.Stage.Phase);
                    }
                    Assert.IsTrue(started && finished && released && settling && hit,
                        $"turn={turn}, start={started}, finish={finished}, release={released}, settle={settling}, hit={hit}");
                    Assert.AreEqual(3, stableTicks);
                    Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase, f.Adapter.LastIssue);
                    Assert.AreEqual(TacticalTurnPhase.ReadyPaused, f.Time.Phase);
                    Assert.AreEqual((ulong)turn + 1, f.Stage.TurnIndex);
                    Assert.AreEqual(3, f.Deck.Hand.Count);
                    Assert.AreEqual(0ul, f.Driver.ExecutionId);
                    Assert.AreEqual(0f, UnityEngine.Time.timeScale);
                    Assert.AreEqual(50 - 10 * turn, f.Ledger.Current, "Finishing must not manufacture hit rewards.");
                    Assert.IsFalse(f.Stage.TryApplyHitReward(execution, turn, 5));
                    Assert.IsFalse(f.Adapter.TryApplyHitReward(execution, turn, 5, f.Ledger));
                }
                CollectionAssert.AreEqual(new[] { CharacterActionInstanceState.Running, CharacterActionInstanceState.Finished,
                    CharacterActionInstanceState.Running, CharacterActionInstanceState.Finished,
                    CharacterActionInstanceState.Running, CharacterActionInstanceState.Finished }, states);
                CollectionAssert.AreEqual(new ulong[] { 1, 1, 2, 2, 3, 3 }, ids);
            }
        }

        [Test]
        public void D02_RealCancelWindowDoesNotCompleteTheTimeline()
        {
            using (var f = new RealFlow())
            {
                f.Ready(); f.Commit();
                bool sawWindow = false;
                for (int i = 0; i < 600 && !sawWindow; i++)
                {
                    f.Step(beforeStage: observation =>
                    {
                        var action = f.Runtime.Card.AcknowledgedActionInstance;
                        if (action == null || !action.IsRunning ||
                            !CharacterTestCharacterActionRuntime.IsAnyCancelWindowActive(action.Definition.CancelRules, action.LocalFrame)) return;
                        sawWindow = true;
                        Assert.IsTrue(observation.ActionStarted);
                        Assert.IsFalse(observation.ActionTimelineCompleted);
                    });
                    Assert.AreNotEqual(TacticalStagePhase.Planning, f.Stage.Phase);
                }
                Assert.IsTrue(sawWindow, "Current formal opener must exercise an authored cancel window.");
            }
        }

        [TestCase(CharacterActionInstanceState.Cancelled)]
        [TestCase(CharacterActionInstanceState.Interrupted)]
        public void D02_AuthorityTerminalInterruptionFaultsAndNotifiesOnce(CharacterActionInstanceState terminal)
        {
            using (var f = new RealFlow())
            {
                f.Ready(); f.Commit(); f.UntilStarted();
                int notifications = 0;
                ulong execution = f.Driver.ExecutionId;
                f.Adapter.ActionStateChanged += (id, state) =>
                {
                    Assert.AreEqual(execution, id);
                    Assert.AreEqual(terminal, state);
                    notifications++;
                    Assert.AreEqual(0ul, f.Adapter.Observe(id).ExecutionId, "Re-observation cannot republish the state.");
                };
                // Controlled authority receipt on the real admitted instance, using the runner's
                // internal terminal API. Natural Finished is exercised only through world ticks in D01.
                f.Runtime.Card.AcknowledgedActionInstance.Complete(terminal, "T4-C controlled interruption");
                f.Adapter.Observe(execution); f.Adapter.Observe(execution);
                var sample = Stable(f.Stage.LastCompletedFrame + 1);
                f.Stage.OnCompletedSimulationTick(sample.SimulationTick, in sample, in sample);
                Assert.AreEqual(1, notifications);
                Assert.IsTrue(f.Adapter.HasExecutionFault);
                Assert.AreEqual("execution:ActionInterrupted", f.Stage.ExecutionIssue);
                Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
                Assert.AreEqual(TacticalTurnPhase.Suspended, f.Time.Phase);
                Assert.AreEqual(TacticalStagePhase.Executing, f.Stage.Phase);
                Assert.AreEqual(1ul, f.Stage.TurnIndex);
                Assert.AreEqual(2, f.Deck.Hand.Count);
                Assert.NotNull(f.Deck.ResolvingAction);
                Assert.AreEqual(40, f.Ledger.Current);
                Assert.IsFalse(f.Stage.TryApplyHitReward(1, 5));
            }
        }

        [Test]
        public void D02_BehaviorTerminalWithoutFinishedCannotSettleAtZeroSpeed()
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish(timelineCompleted: false);
            for (int i = 4; i <= 14; i++) f.Tick(i);
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
            Assert.AreEqual(2, f.Deck.Hand.Count);
            Assert.IsTrue(f.Stage.CanStep);
        }

        [Test]
        public void D02_ReactionAtZeroVelocityResetsTheThreeTickWindow()
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            f.Tick(4); f.Tick(5);
            for (int i = 6; i < 12; i++) f.Tick(i, Stable(i, reaction: true));
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            f.Tick(12); f.Tick(13);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
            f.Tick(14);
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
        }

        [Test]
        public void D02_EntityReactionIsPresentInTheAuthoritySample()
        {
            using (var f = new RealFlow())
            {
                var source = f.Runtime.Source;
                source.SetMotionState(0, Vector2.zero, true);
                source.SetCombatState(source.CombatState.ApplyHit(0, 10, f.Runtime.NextFrame, f.Runtime.Target.Id.Value, 1));
                f.Adapter.SetPhysicsSample(f.Runtime.NextFrame, null, default);
                var sample = f.Adapter.BuildSettleSample(f.Runtime.NextFrame, source);
                Assert.IsTrue(source.CombatState.IsReacting);
                Assert.IsTrue(sample.HasReactionOrRecovery);
                Assert.IsFalse(sample.NeutralPoseReached);
            }
        }

        [TestCase("landing")]
        [TestCase("support")]
        [TestCase("impulse")]
        [TestCase("contact")]
        public void D03_ZeroSpeedWithoutStablePhysicsRestartsTheWindow(string invalid)
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            f.Tick(4); f.Tick(5);
            f.Tick(6, Stable(6, landing: invalid == "landing", support: invalid != "support",
                impulse: invalid == "impulse", contact: invalid == "contact"));
            Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            f.Tick(7); f.Tick(8);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
            f.Tick(9);
            Assert.AreEqual(2ul, f.Stage.TurnIndex);
        }

        [TestCase(5)]
        [TestCase(4)]
        public void D03_DuplicateOrOlderStageFrameDiscardsExistingStability(int staleFrame)
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            f.Tick(4); f.Tick(5); f.Tick(staleFrame);
            Assert.AreEqual(TacticalStageRejection.StaleFrame, f.Stage.LastRejection);
            Assert.AreEqual(5, f.Stage.LastCompletedFrame);
            f.Tick(6); f.Tick(7);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
            f.Tick(8); Assert.AreEqual(2ul, f.Stage.TurnIndex);
        }

        [Test]
        public void D03_MissingAndMismatchedFramesCannotCompleteTheTurn()
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            f.Tick(4); f.Tick(5); f.Tick(7); f.Tick(8);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
            f.Tick(9); Assert.AreEqual(2ul, f.Stage.TurnIndex);
            f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            f.Tick(4); f.Tick(5); f.Tick(6, Stable(5));
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.StaleFrame, f.Stage.Fault);
            Assert.AreEqual(1ul, f.Stage.TurnIndex);
        }

        [Test]
        public void D03_ApexContactValidityAndRotatingSupportUseAuthorityFacts()
        {
            using (var f = new RealFlow())
            {
                var actorObject = new GameObject("T4C actor body");
                var platformObject = new GameObject("T4C support");
                try
                {
                    var body = actorObject.AddComponent<Rigidbody>(); body.useGravity = false;
                    var support = platformObject.AddComponent<Rigidbody>(); support.useGravity = false;
                    var adapter = f.CreateAdapter(body);
                    f.Runtime.Source.SetMotionState(0, Vector2.zero, false);
                    adapter.SetPhysicsSample(20, null, default);
                    var sample = adapter.BuildSettleSample(20, f.Runtime.Source);
                    Assert.IsTrue(sample.HasPendingLanding);
                    Assert.IsFalse(sample.HasStableSupport);
                    f.Runtime.Source.SetMotionState(0, Vector2.zero, true);
                    adapter.SetPhysicsSample(21, null, default, hasStableSupport: false);
                    Assert.IsFalse(adapter.BuildSettleSample(21, f.Runtime.Source).HasStableSupport);
                    support.linearVelocity = Vector3.right * 2;
                    support.angularVelocity = Vector3.up * 3;
                    Vector3 contact = support.worldCenterOfMass + Vector3.right;
                    body.linearVelocity = support.GetPointVelocity(contact);
                    body.angularVelocity = support.angularVelocity;
                    adapter.SetPhysicsSample(22, support, contact);
                    sample = adapter.BuildSettleSample(22, f.Runtime.Source);
                    Assert.IsTrue(sample.HasStableSupport);
                    Assert.AreEqual(0, sample.RelativeLinearSpeed, 0.0001f);
                    Assert.AreEqual(0, sample.RelativeAngularSpeed, 0.0001f);
                    Assert.IsFalse(adapter.BuildSettleSample(23, f.Runtime.Source).HasStableSupport);
                    adapter.SetPhysicsSample(24, support, new Vector3(float.NaN, 0, 0));
                    Assert.IsFalse(adapter.BuildSettleSample(24, f.Runtime.Source).HasStableSupport);
                    adapter.SetPhysicsSample(25, support, contact);
                    UnityEngine.Object.DestroyImmediate(platformObject);
                    Assert.IsFalse(adapter.BuildSettleSample(25, f.Runtime.Source).HasStableSupport);
                }
                finally { UnityEngine.Object.DestroyImmediate(actorObject); if (platformObject != null) UnityEngine.Object.DestroyImmediate(platformObject); }
            }
        }

        [Test]
        public void D04_OpponentWindupAndMotionDoNotDelayPlayerPlanning()
        {
            var f = new ControlledFlow(); f.ReadyAndCommit(); f.Port.Finish();
            for (long frame = 4; frame <= 6; frame++)
            {
                var player = Stable(frame);
                var opponent = new SettleSample(frame, false, false, false, false, false, true,
                    false, false, true, false, 4f, 3f);
                f.Stage.OnCompletedSimulationTick(frame, in player, in opponent);
                if (frame < 6) Assert.AreEqual(TacticalStagePhase.TurnSettling, f.Stage.Phase);
            }
            Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.IsFalse(f.Stage.CanStep);
            Assert.AreEqual(3, f.Deck.Hand.Count);
            Assert.AreEqual(2ul, f.Stage.TurnIndex);
        }

        [Test]
        public void D05_HitRewardsAndRecycledInstanceRejectDuplicatesAndOldReceipts()
        {
            using (var f = new RealFlow())
            {
                f.Ready(); var card = f.Deck.Hand[0]; f.Commit(card);
                ulong execution = f.Driver.ExecutionId;
                uint version = card.Version;
                Assert.IsFalse(f.Stage.TryApplyHitReward(7001, 5), "No reward before the acknowledged action start.");
                bool paid = false;
                for (int i = 0; i < 600 && !paid; i++)
                    f.Step(beforeStage: observation =>
                    {
                        if (!observation.ActionStarted || !HasCurrentDamage(f)) return;
                        Assert.IsFalse(f.Stage.TryApplyHitReward(execution + 1, 7001, 5));
                        Assert.IsFalse(f.Stage.TryApplyHitReward(0, 5));
                        Assert.IsFalse(f.Stage.TryApplyHitReward(7001, -1));
                        Assert.IsTrue(f.Stage.TryApplyHitReward(execution, 7001, 5));
                        Assert.IsFalse(f.Stage.TryApplyHitReward(execution, 7001, 90));
                        Assert.AreEqual(45, f.Ledger.Current);
                        paid = true;
                    });
                Assert.IsTrue(paid, "A reward must be driven by the real current action's combat hit.");
                f.UntilPlanning();
                Assert.AreEqual(45, f.Ledger.Current);
                Assert.Contains(card, (System.Collections.ICollection)f.Deck.Hand);
                Assert.Greater(card.Version, version, "Three-card deck must discard/recycle the same instance.");
                f.Commit(card);
                Assert.AreEqual(35, f.Ledger.Current);
                Assert.IsFalse(f.Deck.CompleteTurn(card.InstanceId, version));
                Assert.IsFalse(f.Stage.TryApplyHitReward(execution, 7002, 5));
                Assert.IsFalse(f.Adapter.TryApplyHitReward(execution, 7002, 5, f.Ledger));
                Assert.AreEqual(0ul, f.Adapter.Observe(execution).ExecutionId);
                f.UntilStarted();
                Assert.IsTrue(f.Stage.TryApplyHitReward(f.Driver.ExecutionId, 7001, 5), "The hit key is scoped to the new execution.");
                Assert.IsFalse(f.Stage.TryApplyHitReward(7001, 5));
                f.UntilPlanning();
                Assert.AreEqual(40, f.Ledger.Current);
                Assert.AreEqual(3ul, f.Stage.TurnIndex);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TargetFallbackAcceptsNullOrNoTargetFactsAndLocksAdmission(bool useResolver)
        {
            using (var f = new RealFlow(useResolver))
            {
                f.Ready();
                var empty = new CharacterTestRuntimeAiPlannerFactState();
                f.Adapter.SetContext(useResolver ? empty : null, new RuntimeFrame(f.Runtime.NextFrame));
                f.Commit(setContext: false);
                f.UntilPlanning();
                Assert.AreEqual(2ul, f.Stage.TurnIndex);
                Assert.AreEqual(40, f.Ledger.Current);
            }
        }

        [Test]
        public void D05_BoundedRewardReceiptsNeverEvictOrAllocateOnDuplicateHits()
        {
            using (var f = new RealFlow())
            {
                f.Ready(); f.Commit(); f.UntilStarted();
                for (int i = 1; i <= 256; i++) Assert.IsTrue(f.Stage.TryApplyHitReward(i, 1));
                Assert.AreEqual(100, f.Ledger.Current);
                Assert.IsFalse(f.Stage.TryApplyHitReward(257, 1));
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool accepted = false;
                for (int i = 0; i < 1000; i++) accepted |= f.Stage.TryApplyHitReward(1, 1);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsFalse(accepted); Assert.AreEqual(0, allocated);
                Assert.AreEqual(100, f.Ledger.Current);
            }
        }

        [TestCase("dead")]
        [TestCase("missing")]
        [TestCase("self")]
        public void InvalidEnemyTargetRejectsBeforeSpending(string invalid)
        {
            using (var f = new RealFlow(useResolver: true))
            {
                f.Ready();
                if (invalid == "dead") f.Runtime.Target.SyncCombatHealth(100, 0);
                if (invalid == "missing") f.ResolvedTarget = null;
                if (invalid == "self") f.ResolvedTarget = f.Runtime.Source;
                f.Adapter.SetContext(null, new RuntimeFrame(f.Runtime.NextFrame));
                f.Select(f.Deck.Hand[0]);
                Assert.AreEqual(TacticalStageRejection.ExecutionRejected, f.Apply(TacticalStageCommandKind.ConfirmExecution, f.Deck.PendingAction));
                Assert.AreEqual(invalid == "dead" ? "execution:TargetDead" : invalid == "missing" ? "execution:TargetMissing" : "execution:InvalidTarget", f.Stage.ExecutionIssue);
                Assert.AreEqual(50, f.Ledger.Current); Assert.AreEqual(10, f.Ledger.Reserved);
                Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
                Assert.AreEqual(0, f.Runtime.Commands.PendingCommandCount);
            }
        }

        [Test]
        public void TargetDyingBetweenPrepareAndAdmitRejectsThePreparedRequest()
        {
            using (var f = new RealFlow())
            {
                f.Ready(); f.Select(f.Deck.Hand[0]);
                f.Adapter.SetContext(f.Runtime.Facts, new RuntimeFrame(f.Runtime.NextFrame));
                Assert.IsTrue(f.Adapter.TryPrepare(f.Deck.PendingAction, out string issue), issue);
                f.Runtime.Target.SyncCombatHealth(100, 0);
                Assert.IsFalse(f.Adapter.TryAdmit(f.Deck.PendingAction, out ulong execution, out issue));
                Assert.AreEqual("execution:TargetDead", issue);
                Assert.AreEqual(0ul, execution); Assert.AreEqual(50, f.Ledger.Current);
            }
        }

        private static bool HasCurrentDamage(RealFlow f)
        {
            var hits = f.Runtime.Source.LastCombatHitResults;
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.IsAcceptedDamage && hit.ActionId == f.Runtime.Card.Compilation.ActionId &&
                    hit.AttackerId.Value == CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + f.Runtime.Source.Id.Value &&
                    hit.TargetId.Value == CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + f.Runtime.Target.Id.Value) return true;
            }
            return false;
        }

        private static bool IsStable(SettleSample s) => s.ActionTimelineCompleted && s.BehaviorTerminal &&
            s.NeutralPoseReached && !s.HasReactionOrRecovery && !s.HasPendingLanding && !s.HasPendingMotionOrImpulse &&
            s.OwnedIntentsReleased && s.CommandsAcknowledged && s.HasStableSupport && !s.HasNewDestabilizingContact &&
            s.RelativeLinearSpeed <= 0.08f && s.RelativeAngularSpeed <= 0.15f;

        private static SettleSample Stable(long frame, bool reaction = false, bool landing = false,
            bool support = true, bool impulse = false, bool contact = false, bool ingress = false)
            => new SettleSample(frame, !ingress, true, true, reaction, landing, impulse, true, true,
                support, contact, 0, 0, noActionStarted: ingress);

        private static TacticalCardDefinition[] Definitions()
        {
            var attack = new TacticalActionCardDefinition("primary", "AttackPrimary", "", 10,
                TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
            return new TacticalCardDefinition[] { attack, attack, attack };
        }

        private sealed class RealFlow : IDisposable
        {
            internal readonly CharacterTestTacticalCardRuntimeFixture Runtime;
            internal readonly UnityTacticalTimeAdapter WorldTime = new UnityTacticalTimeAdapter();
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager();
            internal readonly MomentumLedger Ledger = new MomentumLedger();
            internal readonly TacticalTimeCoordinator Time;
            internal readonly TacticalSingleCardDriver Driver;
            internal readonly TacticalStageFlowCoordinator Stage;
            internal readonly CharacterTestTacticalExecutionAdapter Adapter;
            internal CharacterTestCharacterEntity ResolvedTarget;
            private readonly bool _useResolver;

            internal RealFlow(bool useResolver = false)
            {
                WorldTime.SetSimulationPaused(false);
                var dummy = new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0);
                using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, dummy))
                    Runtime = new CharacterTestTacticalCardRuntimeFixture(actionId: probe.Action.ActionId.Value);
                // Set initial durability once through the existing attribute authority; never heal
                // between turns or replace the real combat/action path to keep the target alive.
                Runtime.Runtime.World.SetCurrentAttribute(Runtime.Target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 1000);
                Runtime.Runtime.World.SyncCombatStateHealth(Runtime.Target);
                _useResolver = useResolver; ResolvedTarget = Runtime.Target;
                Adapter = CreateAdapter();
                Time = new TacticalTimeCoordinator(WorldTime, new SettleDetector(new SettleDetectorConfig(3)));
                Driver = new TacticalSingleCardDriver(Adapter, Time, Deck, Ledger, executionBudgetTicks: 600);
                Stage = new TacticalStageFlowCoordinator(1, Time, Deck, Ledger, Driver, Definitions(), 42,
                    id => id.Generation == 1 && Runtime.Runtime.FindEntityById(id.Index) != null, new TacticalStoryStepBinding(11, 21, 31));
            }

            internal CharacterTestTacticalExecutionAdapter CreateAdapter(Rigidbody body = null)
                => new CharacterTestTacticalExecutionAdapter(Runtime.Runtime, Runtime.Commands, Runtime.Card,
                    Runtime.Definition, Runtime.Source, WorldTime, body,
                    _useResolver ? () => ResolvedTarget : (Func<CharacterTestCharacterEntity>)null);

            internal void Ready()
            {
                var token = new TacticalStoryStepToken(1, 11, 1, 21, 31); Stage.OnStoryStepStarted(in token);
                Stage.OnSpawnBound(1, 0, new GameplayEntityId(Runtime.Source.Id.Value, 1), Runtime.NextFrame - 1);
                Stage.OnSpawnBound(1, 1, new GameplayEntityId(Runtime.Target.Id.Value, 1), Runtime.NextFrame - 1);
                for (int i = 0; i < 3; i++)
                {
                    Runtime.AdvanceWorldOnly();
                    var sample = Stable(Runtime.NextFrame - 1, ingress: true);
                    Stage.OnCompletedSimulationTick(sample.SimulationTick, in sample, in sample);
                }
                Assert.AreEqual(TacticalStagePhase.Planning, Stage.Phase);
                Assert.AreEqual(3, Deck.Hand.Count);
            }

            internal TacticalStageRejection Apply(TacticalStageCommandKind kind, TacticalCardInstance card)
            {
                var command = new TacticalStageCommand(1, Stage.TurnIndex, card?.InstanceId ?? 0,
                    card?.Version ?? 0, Stage.SnapshotRevision, kind);
                return Stage.Apply(in command);
            }

            internal void Select(TacticalCardInstance card)
                => Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.SelectAction, card));

            internal void Commit(TacticalCardInstance card = null, bool setContext = true)
            {
                card = card ?? Deck.Hand[0]; Select(card);
                Assert.AreEqual(10, Ledger.Reserved);
                if (setContext) Adapter.SetContext(null, new RuntimeFrame(Runtime.NextFrame));
                Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.ConfirmExecution, card), Stage.ExecutionIssue);
                Assert.AreEqual(TacticalStagePhase.Executing, Stage.Phase);
                Assert.AreEqual(0, Ledger.Reserved); Assert.AreEqual(2, Deck.Hand.Count);
                Assert.AreEqual(1f, UnityEngine.Time.timeScale); Assert.IsTrue(Stage.CanStep);
                Assert.AreEqual(0, Runtime.Commands.PendingCommandCount, "Admission does not advance the world.");
            }

            internal SettleSample Step(Action<ExecutionObservation> beforeStage = null)
            {
                Assert.IsTrue(Stage.CanStep, Stage.ExecutionIssue);
                Adapter.SetContext(null, new RuntimeFrame(Runtime.NextFrame)); Adapter.Tick();
                Runtime.AdvanceWorldOnly();
                long frame = Runtime.NextFrame - 1;
                Adapter.SetPhysicsSample(frame, null, default);
                var observation = Adapter.Observe(Driver.ExecutionId);
                beforeStage?.Invoke(observation);
                var sample = Adapter.BuildSettleSample(frame, Runtime.Source);
                var opponent = new SettleSample(frame, false, false, false, false, false, true,
                    false, false, true, false, 4, 3);
                Stage.OnCompletedSimulationTick(frame, in sample, in opponent);
                return sample;
            }

            internal void UntilStarted()
            {
                for (int i = 0; i < 600; i++)
                {
                    Step();
                    if (Adapter.Observe(Driver.ExecutionId).ActionStarted) return;
                }
                Assert.Fail("No real action start: " + Adapter.LastIssue);
            }

            internal void UntilPlanning()
            {
                for (int i = 0; i < 600 && Stage.Phase != TacticalStagePhase.Planning; i++) Step();
                Assert.AreEqual(TacticalStagePhase.Planning, Stage.Phase, Adapter.LastIssue);
            }

            public void Dispose() => Runtime.Dispose();
        }

        private sealed class ControlledFlow
        {
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager();
            internal readonly ControlledPort Port = new ControlledPort();
            internal readonly TacticalStageFlowCoordinator Stage;
            internal ControlledFlow()
            {
                var ledger = new MomentumLedger();
                var worldTime = new UnityTacticalTimeAdapter(); worldTime.SetSimulationPaused(false);
                var time = new TacticalTimeCoordinator(worldTime, new SettleDetector(new SettleDetectorConfig(3)));
                var driver = new TacticalSingleCardDriver(Port, time, Deck, ledger);
                Stage = new TacticalStageFlowCoordinator(1, time, Deck, ledger, driver, Definitions(), 42,
                    id => id.IsValid, new TacticalStoryStepBinding(11, 21, 31));
            }
            internal void ReadyAndCommit()
            {
                var token = new TacticalStoryStepToken(1, 11, 1, 21, 31); Stage.OnStoryStepStarted(in token);
                Stage.OnSpawnBound(1, 0, new GameplayEntityId(1, 1), 0);
                Stage.OnSpawnBound(1, 1, new GameplayEntityId(2, 1), 0);
                for (int i = 1; i <= 3; i++) Tick(i, Stable(i, ingress: true));
                var card = Deck.Hand[0];
                var select = new TacticalStageCommand(1, 1, card.InstanceId, card.Version, Stage.SnapshotRevision, TacticalStageCommandKind.SelectAction);
                Assert.AreEqual(TacticalStageRejection.None, Stage.Apply(in select));
                var commit = new TacticalStageCommand(1, 1, card.InstanceId, card.Version, Stage.SnapshotRevision, TacticalStageCommandKind.ConfirmExecution);
                Assert.AreEqual(TacticalStageRejection.None, Stage.Apply(in commit));
            }
            internal void Tick(long frame, SettleSample? sample = null)
            { var value = sample ?? Stable(frame); Stage.OnCompletedSimulationTick(frame, in value, in value); }
        }

        private sealed class ControlledPort : ITacticalExecutionPort
        {
            private ExecutionObservation _observation = new ExecutionObservation(1, true, false, false, false, true);
            internal void Finish(bool timelineCompleted = true) => _observation = new ExecutionObservation(1, true, timelineCompleted, true, true, true);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { issue = ""; executionId = 1; return true; }
            public ExecutionObservation Observe(ulong executionId) => _observation;
            public bool TryAbortPreAction(ulong executionId, string reason) => false;
        }
    }
}
