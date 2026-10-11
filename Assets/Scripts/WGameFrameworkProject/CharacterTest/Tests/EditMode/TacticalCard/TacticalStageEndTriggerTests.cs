using System;
using MxFramework.CharacterAction;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class TacticalStageEndTriggerTests
    {
        private float _oldScale;
        [SetUp] public void SetUp() { _oldScale = Time.timeScale; Time.timeScale = 1; }
        [TearDown] public void TearDown() { Time.timeScale = _oldScale; }

        [TestCase(100, 0, true)]
        [TestCase(0, 100, false)]
        [TestCase(0, 0, false)]
        [TestCase(-1, -5, false)]
        public void E01_AtomicVitalsProduceExactlyOneOutcomeAndDeferredDisablePreventsReversal(int player, int opponent, bool victory)
        {
            using var f = new Fixture();
            f.SetHp(player, opponent); f.Tick();
            Assert.AreEqual(1, f.Runtime.ActivationCount);
            Assert.AreEqual(1, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(2, f.Runtime.DeferredRecordCount);
            var record = f.Runtime.GetReliableOutboxRecord(0);
            Assert.AreEqual(victory ? CharacterTestStoryIds.Triggers.TacticalBattleVictory : CharacterTestStoryIds.Triggers.TacticalBattleDefeat, record.ActionPayload0);
            Assert.AreEqual(f.Player, record.Owner); Assert.AreEqual(f.Player, record.Subject);
            Assert.AreEqual(f.Opponent, record.Instigator);
            Assert.AreEqual(player, record.OccurrencePayload0); Assert.AreEqual(opponent, record.OccurrencePayload1);
            Assert.AreEqual(77, record.OccurrencePayload2); Assert.AreEqual(0, f.End.Vitals.LastSample.Sequence);
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
            Assert.IsFalse(f.Time.IsWorldPaused);
            f.SetHp(victory ? 0 : 100, victory ? 100 : 0); f.Tick();
            Assert.AreEqual(0, f.Runtime.ActivationCount); Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
            for (int i = 0; i < f.Catalog.Count; i++)
            { Assert.IsTrue(f.Runtime.TryGetSourceEnabled(f.Catalog.Get(i).SourceBindingId, out bool enabled)); Assert.IsFalse(enabled); }
            Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(victory, f.Flow.LastBattleVictory);
            Assert.AreEqual(1, f.OutcomeNotifications);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.BattleEnded));
        }

        [Test]
        public void E01_AlivePairDoesNotActivateAndMissingRosterDoesNotSubmit()
        {
            using var f = new Fixture(bindRoster: false);
            f.Tick(); Assert.AreEqual(-1, f.End.Vitals.LastSampleFrame); Assert.AreEqual(0, f.Runtime.ActivationCount);
            f.BindRoster(); f.Tick();
            Assert.AreEqual(2, f.End.Vitals.LastSampleFrame); Assert.AreEqual(0, f.Runtime.ActivationCount);
            Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void E01_AuthoringRejectsWeakenedConditionPolicyOrDisableTransaction(int mutation)
        {
            var doc = new CharacterTestLevelGameplayTriggerAuthoringDocument(); doc.AddTacticalBattleOutcomeTemplates();
            var entry = doc.Entries[0]; var definition = doc.Authoring.Definitions[0]; var route = definition.Routes[0];
            if (mutation == 0) doc.Authoring.Conditions[0] = new GameplayTriggerConditionAuthoring(entry.ConditionId,
                new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue));
            if (mutation == 1) route.Actions[2] = new GameplayTriggerActionAuthoring(
                CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalSetBindingEnabledActionId,
                GameplayTriggerTargetSelector.ExplicitBindingTarget, 2, 1);
            if (mutation == 2) definition.Routes[0] = new GameplayTriggerRouteAuthoring(route.RouteId, route.OccurrenceMask,
                route.ConditionId, route.Actions, stateScope: GameplayTriggerStateScope.PerInstance, maxActivations: 0, maxStateKeys: 1);
            if (mutation == 3) doc.Entries.RemoveAt(1);
            var report = CharacterTestLevelGameplayTriggerAuthoringAdapter.ValidateAndCompile(doc, new LevelWorldObjectAuthoringDocument(), out _);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void E03_ReservationRejectionPreventsReadyWithoutConsumingActivationOrPartiallyDisabling()
        {
            using var f = new Fixture(rejectReservation: true); f.SetHp(100, 0); f.Tick();
            Assert.AreEqual(0, f.Runtime.ActivationCount); Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(0, f.Runtime.DeferredRecordCount); Assert.AreEqual(1, f.Runtime.NextActivationId);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.CriticalTriggerRejected, f.Stage.Fault);
            Assert.AreEqual(0, f.Deck.Hand.Count);
            Assert.AreEqual(f.Frame, f.Stage.LastCompletedFrame, "Faults still remember the completed authority boundary.");
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.RequestRestart));
            f.Tick(); f.Tick();
            Assert.AreEqual(0, f.Story.CommandBuffer.PendingCount);
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health,
                "An old reservation fault must not stop the accepted cleanup/rebuild request.");
        }

        [TestCase(false)] [TestCase(true)]
        public void E03_MissingCriticalProducerOrEvaluationCannotCompleteThirdStableTick(bool skipEvaluation)
        {
            using var f = new Fixture(); f.Ready(); f.Commit(); f.Port.Terminal = true;
            f.Tick(); f.Tick();
            if (skipEvaluation) f.End.Evaluation.SetEnabled(false); else f.End.Vitals.SetEnabled(false);
            f.Tick();
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.CriticalTriggerRejected, f.Stage.Fault);
            Assert.AreEqual(1ul, f.Stage.TurnIndex); Assert.IsNotNull(f.Deck.ResolvingAction);
            Assert.AreEqual(2, f.Deck.Hand.Count);
        }

        [Test]
        public void F02_UnifiedTimeOwnerRestoresEntryScaleAndIgnoresLateDisposedCalls()
        {
            Time.timeScale = 0.5f;
            var adapter = new UnityTacticalTimeAdapter(0.2f);
            adapter.SetSimulationPaused(true); Assert.AreEqual(0, Time.timeScale);
            adapter.Dispose(); Assert.AreEqual(0.5f, Time.timeScale);
            adapter.SetSimulationPaused(true); adapter.SetSimulationTimeScale(1); adapter.Dispose();
            Assert.AreEqual(0.5f, Time.timeScale);
        }

        [Test]
        public void E02_FatalThirdStableTickKeepsResolvingPaymentAndTurnUntilStoryStep()
        {
            using var f = new Fixture(); f.Ready(); f.Commit();
            var card = f.Deck.ResolvingAction; int paid = f.Ledger.Current;
            f.Port.Terminal = true; f.Tick(); f.Tick();
            f.SetHp(100, 0); f.Tick();
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
            Assert.AreEqual(1ul, f.Stage.TurnIndex); Assert.AreEqual(paid, f.Ledger.Current);
            Assert.AreSame(card, f.Deck.ResolvingAction); Assert.AreEqual(2, f.Deck.Hand.Count);
            foreach (var kind in new[] { TacticalStageCommandKind.SelectAction, TacticalStageCommandKind.CancelPending,
                TacticalStageCommandKind.PlayUtility, TacticalStageCommandKind.ConfirmExecution })
                Assert.AreEqual(TacticalStageRejection.CriticalFlowPending, f.Apply(kind));
            f.Tick();
            Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(1ul, f.Stage.TurnIndex); Assert.AreSame(card, f.Deck.ResolvingAction);
            Assert.AreEqual(2, f.Deck.Hand.Count); Assert.AreEqual(paid, f.Ledger.Current);
        }

        [Test]
        public void E03_FullStoryBufferRetainsRecordAndRetriesOnceOnOriginalHost()
        {
            using var f = new Fixture(storyCapacity: 1); f.Ready(); f.Commit(); f.SetHp(100, 0); f.Tick();
            long sequence = f.Runtime.GetReliableOutboxRecord(0).Sequence;
            Assert.IsTrue(f.Story.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(new RuntimeFrame(f.Frame + 1),
                StoryRuntimeCommandSources.System, 999999)).Success);
            f.Tick();
            Assert.AreEqual(1, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(sequence, f.Runtime.GetReliableOutboxRecord(0).Sequence);
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
            Assert.IsFalse(f.End.HasStoryResult); Assert.IsFalse(f.Time.IsWorldPaused);
            f.Tick();
            Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(1, f.OutcomeNotifications);
        }

        [Test]
        public void E03_ClosedTransferTimesOutWithoutDroppingOutboxAndRecoveryAllowsConsumption()
        {
            using var f = new Fixture(transferBudget: 3); f.Transfer.SetEnabled(false);
            f.SetHp(0, 100); f.Tick(); f.Tick(); f.Tick(); f.Tick();
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.StoryTransferTimeout, f.Stage.Fault);
            Assert.AreEqual(1, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreNotEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(TacticalStageRejection.CriticalFlowPending, f.Apply(TacticalStageCommandKind.RequestRestart));
            f.Time.RequestPause(TacticalPauseReason.UserMenu);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.RequestRecovery));
            Assert.IsTrue(f.Time.IsWorldPaused); f.Time.RequestResume(TacticalPauseReason.UserMenu);
            f.Transfer.SetEnabled(true); f.Tick();
            Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
        }

        [Test]
        public void E03_SemanticRejectionIsTerminalAndNeverPretendsStageCompleted()
        {
            using var f = new Fixture(loadGraph: false); f.SetHp(100, 0); f.Tick(); f.Tick();
            Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
            Assert.IsTrue(f.End.HasStoryResult);
            Assert.AreEqual(StoryRuntimeCommandResultStatus.Rejected, f.End.LastStoryResult.Status);
            Assert.AreEqual(StoryDirectorResultCode.TriggerNotFound, f.End.LastStoryResult.DirectorResultCode);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.StoryTransferRejected, f.Stage.Fault);
            Assert.AreNotEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(CharacterTestBattleOutcome.None, f.Flow.LastBattleOutcome);
            Assert.IsTrue(f.End.CanLeaveSession);
        }

        [Test]
        public void E04_OverflowingObserverCannotChangeAuthoritativeOutcomeOrBarrier()
        {
            using var f = new Fixture(observerCapacity: 1); f.SetHp(0, 0); f.Tick();
            Assert.Greater(f.Observer.DroppedCount, 0);
            Assert.AreEqual(TacticalStageHealth.AwaitingStoryTransfer, f.Stage.Health);
            f.Tick();
            Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase);
            Assert.AreEqual(false, f.Flow.LastBattleVictory);
            Assert.AreEqual(StoryRuntimeCommandResultStatus.Succeeded, f.End.LastStoryResult.Status);
        }

        [TestCase(true)] [TestCase(false)]
        public void E05_DestroyedOrReusedGenerationCannotBecomeZeroHp(bool destroyPlayer)
        {
            using var f = new Fixture(); f.Tick(); Assert.IsTrue(f.End.Vitals.Armed);
            var destroyed = destroyPlayer ? f.Player : f.Opponent;
            Assert.IsTrue(f.World.DestroyEntity(destroyed)); var replacement = f.World.CreateEntity();
            Assert.AreNotEqual(destroyed, replacement);
            f.Attributes.Set(replacement, new GameplayAttributeSetComponent(new GameplayAttributeValue(CharacterTestCharacterAttributeIds.Hp, 100, 0)));
            f.Tick();
            Assert.AreEqual(1, f.End.Vitals.LastSampleFrame); Assert.AreEqual(100, f.End.Vitals.LastSample.Payload0);
            Assert.AreEqual(0, f.Runtime.ActivationCount); Assert.AreEqual(0, f.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.AreEqual(TacticalStageRejection.InvalidBattleVitals, f.Stage.Fault);
        }

        [Test]
        public void E05_MissingHpAttributeFailsClosedWhileAlreadyAcceptedTerminalSurvivesDestruction()
        {
            using (var f = new Fixture())
            {
                f.Tick(); f.Attributes.Set(f.Opponent, default); f.Tick();
                Assert.AreEqual(TacticalStageRejection.InvalidBattleVitals, f.Stage.Fault);
                Assert.AreEqual(0, f.Runtime.ActivationCount);
            }
            using (var f = new Fixture())
            {
                f.SetHp(100, 0); f.Tick(); f.World.DestroyEntity(f.Opponent); f.Tick();
                Assert.AreEqual(TacticalStagePhase.StageCompleted, f.Stage.Phase); Assert.AreEqual(true, f.Flow.LastBattleVictory);
            }
        }

        [Test]
        public void F01_ThreeHundredUnsettledTicksSuspendExactlyOnceAndRecoveryRequiresFreshWindow()
        {
            using var f = new Fixture(); f.Ready(); f.Commit(); f.Port.Terminal = true;
            var card = f.Deck.ResolvingAction; int paid = f.Ledger.Current;
            for (int i = 0; i < 299; i++) f.Tick(stable: false);
            Assert.AreEqual(TacticalStageHealth.Running, f.Stage.Health);
            f.Tick(stable: false);
            Assert.AreEqual(TacticalStageHealth.SuspendedByFault, f.Stage.Health);
            Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));
            ulong revision = f.Stage.SnapshotRevision;
            var invalidPausedSample = Sample(f.Frame + 1, false);
            f.Stage.OnCompletedSimulationTick(f.Frame + 1, in invalidPausedSample, in invalidPausedSample);
            Assert.AreEqual(revision, f.Stage.SnapshotRevision); Assert.AreSame(card, f.Deck.ResolvingAction);
            Assert.AreEqual(paid, f.Ledger.Current); Assert.AreEqual(1ul, f.Stage.TurnIndex); Assert.AreEqual(2, f.Deck.Hand.Count);
            Assert.AreEqual(TacticalStageRejection.None, f.Apply(TacticalStageCommandKind.RequestRecovery));
            f.Tick(); f.Tick(); Assert.AreSame(card, f.Deck.ResolvingAction);
            f.Tick(); Assert.AreEqual(TacticalStagePhase.Planning, f.Stage.Phase);
            Assert.AreEqual(2ul, f.Stage.TurnIndex); Assert.AreEqual(3, f.Deck.Hand.Count);
        }

        [TestCase(false)] [TestCase(true)]
        public void F02_StoryRetryOrNextWaveRequestReleasesPauseAndOldSessionCannotAffectNewOne(bool nextWave)
        {
            StoryRuntimeCommandResult oldResult;
            using (var f = new Fixture(executionBudget: 3))
            {
                f.Ready(); f.Commit();
                if (nextWave) { f.SetHp(100, 0); f.Tick(); f.Tick(); }
                else { f.Tick(false); f.Tick(false); f.Tick(false); }
                oldResult = f.End.LastStoryResult;
                Assert.IsTrue(f.Time.IsWorldPaused);
                Assert.IsTrue(nextWave ? f.Flow.TryRequestNextWave() : f.Flow.TryRequestRetry());
                Assert.IsFalse(f.Time.IsWorldPaused); Assert.AreEqual(1, f.Story.CommandBuffer.PendingCount);
                Assert.IsFalse(f.Flow.TryRequestRetry(), "A transition is accepted once.");
                f.Tick(); Assert.AreEqual(0, f.Story.CommandBuffer.PendingCount);
            }
            Assert.AreEqual(1, Time.timeScale);
            using var fresh = new Fixture(stageRunId: 2);
            fresh.End.Publish(in oldResult);
            var stale = new TacticalStoryStepToken(1, CharacterTestStoryIds.BootstrapGraph, 1,
                CharacterTestStoryIds.Steps.TacticalBattleVictory, CharacterTestStoryIds.Actions.TacticalBattleVictory);
            fresh.Stage.OnStoryStepStarted(in stale);
            Assert.AreEqual(TacticalStageRejection.WrongStage, fresh.Stage.LastRejection);
            Assert.IsFalse(fresh.End.HasStoryResult); fresh.Ready();
            Assert.AreEqual(TacticalStagePhase.Planning, fresh.Stage.Phase);
        }

        [Test]
        public void F02_DisposalKeepsMenuOwnershipAndUnsubscribesOldStoryBridge()
        {
            var f = new Fixture(); f.Ready(); f.Time.RequestPause(TacticalPauseReason.UserMenu);
            f.Dispose(); Assert.IsTrue(f.Time.HasPauseReason(TacticalPauseReason.UserMenu));
            Assert.IsTrue(f.Time.IsWorldPaused);
            f.Time.RequestResume(TacticalPauseReason.UserMenu); Assert.AreEqual(1, Time.timeScale);
            var evt = new StoryEvent(StoryEventKind.StepStarted, graphId: CharacterTestStoryIds.BootstrapGraph,
                beatInstanceId: 1, stepId: CharacterTestStoryIds.Steps.TacticalBattleVictory, auxId: CharacterTestStoryIds.Actions.TacticalBattleVictory);
            f.Director.Events.Publish(in evt); Assert.AreEqual(0, f.OutcomeNotifications);
        }

        private static SettleSample Sample(long frame, bool stable = true) => new SettleSample(frame, false, true, true,
            false, !stable, false, true, true, stable, false, stable ? 0 : 10, 0, noActionStarted: true);

        private sealed class Fixture : IDisposable
        {
            internal readonly GameplayComponentWorld World = new GameplayComponentWorld();
            internal readonly GameplayComponentStore<GameplayAttributeSetComponent> Attributes;
            internal readonly GameplayEntityId Player, Opponent;
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager();
            internal readonly MomentumLedger Ledger = new MomentumLedger();
            internal readonly TacticalTimeCoordinator Time = new TacticalTimeCoordinator(new UnityTacticalTimeAdapter(),
                new SettleDetector(new SettleDetectorConfig(requiredStableTicks: 3)));
            internal readonly Port Port = new Port();
            internal readonly TacticalStageFlowCoordinator Stage;
            internal readonly GameplayTriggerRuntime Runtime;
            internal readonly CharacterTestGameplayTriggerBattleVitalsCatalog Catalog;
            internal readonly TacticalStageEndTriggerBridge End;
            internal readonly StoryDirector Director = new StoryDirector();
            internal readonly RuntimeHost Host = new RuntimeHost();
            internal readonly StoryRuntimeModule Story;
            internal readonly GameplayTriggerStoryReliableCommandTransfer Transfer;
            internal readonly GameplayTriggerRuntimeEventObserverBuffer Observer;
            internal readonly CharacterTestStoryFlowBridge Flow;
            private readonly TacticalStoryBridge _storyBridge;
            private readonly GlobalResourceRuntimeServices _resources;
            internal long Frame;
            internal int OutcomeNotifications;

            internal Fixture(int storyCapacity = 8, int observerCapacity = 64, int transferBudget = 60,
                bool loadGraph = true, bool bindRoster = true, int executionBudget = 300, ulong stageRunId = 1, bool rejectReservation = false)
            {
                Player = World.CreateEntity(); Opponent = World.CreateEntity(); Attributes = World.CreateStore<GameplayAttributeSetComponent>(); SetHp(100, 100);
                var action = new TacticalActionCardDefinition("a", "A", "", 10,
                    TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy,
                    TacticalApproachMode.OptimalRange);
                var driver = new TacticalSingleCardDriver(Port, Time, Deck, Ledger, executionBudgetTicks: executionBudget);
                Stage = new TacticalStageFlowCoordinator(stageRunId, Time, Deck, Ledger, driver,
                    new TacticalCardDefinition[] { action, action, action, action }, 17, World.IsAlive,
                    new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi),
                    completionSteps: new[]
                    {
                        new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.TacticalBattleVictory, CharacterTestStoryIds.Actions.TacticalBattleVictory),
                        new TacticalStoryStepBinding(CharacterTestStoryIds.BootstrapGraph, CharacterTestStoryIds.Steps.TacticalBattleDefeat, CharacterTestStoryIds.Actions.TacticalBattleDefeat)
                    }, requestStoryRestart: (run, frame) => RequestRestart(run, frame), storyTransferBudgetTicks: transferBudget);
                var published = new CharacterTestLevelGameplayTriggerAuthoringDocument(); published.AddTacticalBattleOutcomeTemplates();
                var document = CharacterTestLevelGameplayTriggerAuthoringAdapter.BindBattleRosterOwner(published, Player);
                Assert.AreEqual(0, published.Authoring.Bindings[0].OwnerIndex, "Runtime roster identity must not mutate published authoring.");
                var report = CharacterTestLevelGameplayTriggerAuthoringAdapter.ValidateAndCompile(document,
                    new LevelWorldObjectAuthoringDocument(), out var compiled);
                Assert.IsFalse(report.HasErrors, Newtonsoft.Json.JsonConvert.SerializeObject(report)); Assert.IsTrue(compiled?.Succeeded == true);
                var plan = compiled.CompiledCatalog;
                Assert.IsTrue(CharacterTestGameplayTriggerBattleVitalsCatalog.TryCreate(plan, out Catalog, out string error), error);
                var actual = new[]
                {
                    GameplayTriggerActionBootstrapBinding.FromSink(rejectReservation ? (IGameplayTriggerActionSink)new RejectingSink()
                        : new GameplayTriggerStoryReliableCommandActionSink(GameplayTriggerStoryCommandActionKinds.RaiseTrigger)),
                    GameplayTriggerActionBootstrapBinding.FromInternal(new GameplayTriggerInternalDeferredActionDescriptor(
                        new GameplayTriggerActionKindId(CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalSetBindingEnabledActionId), GameplayTriggerInternalDeferredOperationKind.SetEnabled))
                };
                var bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(plan, actual,
                    new GameplayTriggerRuntimeCapacityProfile(2, 4, 4, 4, 4, 4, 4, 4, 12, 12, 8, 8, 4));
                Assert.IsTrue(bootstrap.Succeeded, bootstrap.DiagnosticCount > 0 ? bootstrap.GetDiagnostic(0).ToString() : "bootstrap");
                var events = new GameplayTriggerRuntimeEventSource(64);
                Runtime = new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value, events);
                End = new TacticalStageEndTriggerBridge(Runtime, Catalog, World, Stage, 77, new GameplayTriggerStoryCommandResultRelay(events));
                if (loadGraph) Director.LoadGraph(CharacterTestStoryFixture.CreateTacticalBattleGraph());
                _storyBridge = new TacticalStoryBridge(Director, Stage);
                var validator = new StoryRuntimeCommandValidator(Director);
                var commands = RuntimeCommandBuffer.CreateBounded(validator, RuntimeFrame.Zero, storyCapacity);
                Story = new StoryRuntimeModule(Director, commands, null, validator, End);
                var sources = new RuntimeCommandSourceRegistry(1);
                Assert.IsTrue(sources.TryRegister(StoryRuntimeCommandSources.GameplayBridge, RuntimeCommandSourceKind.Derived,
                    new[] { StoryRuntimeCommandIds.RaiseTrigger }, out _));
                Transfer = new GameplayTriggerStoryReliableCommandTransfer(GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge, Host, Story,
                    new RuntimeCommandSubmissionGate(sources, commands, RuntimeCommandIngressKind.TrustedDerived));
                var pipeline = new GameplaySystemPipeline();
                pipeline.Add(new GameplayTriggerDeferredSystem(Runtime));
                pipeline.Add(End.Vitals); pipeline.Add(End.Evaluation);
                Observer = new GameplayTriggerRuntimeEventObserverBuffer(observerCapacity);
                pipeline.Add(new GameplayTriggerEventRouterSystem(events, new[] { new GameplayTriggerRuntimeEventObserverRegistration("tactical.test", Observer) }));
                Host.RegisterModule(new GameplayTriggerReliableOutboxModule(Runtime, new IGameplayTriggerReliableTargetTransfer[] { Transfer }));
                Host.RegisterModule(Story);
                Host.RegisterModule(new GameplayRuntimeModule(new GameplayWorld(), new GameplayAbilityRegistry(),
                    new RuntimeCommandBuffer(), tickWorldAutomatically: false, systemPipeline: pipeline, componentWorld: World));
                Host.Initialize(); Host.Start();
                var manager = new ResourceManager();
                _resources = new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Editor, manager, new ResourcePreloadService(manager));
                Flow = new CharacterTestStoryFlowBridge(Story, _resources, NullRuntimeLogger.Instance, () => new RuntimeFrame(Frame), "", null);
                Flow.BindTacticalBattle(stageRunId, CharacterTestStoryIds.BootstrapGraph, () => End.CanLeaveSession,
                    RequestRestart, RequestRestart, Stage.BeginStoryRestart);
                Flow.BattleOutcomeStarted += evt => OutcomeNotifications++;
                var opening = new TacticalStoryStepToken(stageRunId, CharacterTestStoryIds.BootstrapGraph, 1,
                    CharacterTestStoryIds.Steps.OpenStartupUi, CharacterTestStoryIds.Actions.OpenStartupUi);
                Stage.OnStoryStepStarted(in opening); if (bindRoster) BindRoster();
            }

            internal void BindRoster() { Stage.OnSpawnBound(Stage.StageRunId, 0, Player, Frame); Stage.OnSpawnBound(Stage.StageRunId, 1, Opponent, Frame); }
            internal void SetHp(int player, int opponent)
            {
                Attributes.Set(Player, new GameplayAttributeSetComponent(new GameplayAttributeValue(CharacterTestCharacterAttributeIds.Hp, 100, player)));
                Attributes.Set(Opponent, new GameplayAttributeSetComponent(new GameplayAttributeValue(CharacterTestCharacterAttributeIds.Hp, 100, opponent)));
            }
            internal void Tick(bool stable = true)
            {
                Assert.IsTrue(Stage.CanStep, "The sole Host must not tick a paused simulation.");
                Host.Tick(++Frame, 1d / 60d); _storyBridge.Drain();
                var sample = Sample(Frame, stable); Stage.OnCompletedSimulationTick(Frame, in sample, in sample);
            }
            internal void Ready() { Tick(); Tick(); Tick(); Assert.AreEqual(TacticalStagePhase.Planning, Stage.Phase); }
            internal void Commit()
            {
                Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.SelectAction, Deck.Hand[0]));
                Assert.AreEqual(TacticalStageRejection.None, Apply(TacticalStageCommandKind.ConfirmExecution, Deck.PendingAction));
            }
            internal TacticalStageRejection Apply(TacticalStageCommandKind kind, TacticalCardInstance card = null)
            {
                var command = new TacticalStageCommand(Stage.StageRunId, Stage.TurnIndex, card?.InstanceId ?? 0,
                    card?.Version ?? 0, Stage.SnapshotRevision, kind);
                var pump = new TacticalCommandPump(Stage);
                Assert.AreEqual(TacticalStageRejection.None, pump.TrySubmit(in command, 0, 1));
                Assert.AreEqual(1, pump.Drain()); Assert.IsTrue(pump.TryReadReceipt(out var receipt)); return receipt.Rejection;
            }
            private bool RequestRestart(ulong run, long frame) => run == Stage.StageRunId && Story != null &&
                Story.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(new RuntimeFrame(frame),
                    StoryRuntimeCommandSources.System, CharacterTestStoryIds.Triggers.LevelStartRegion)).Success;
            public void Dispose() { Flow.Dispose(); _storyBridge.Dispose(); End.Dispose(); Host.Dispose(); _resources.Dispose(); }
        }

        private sealed class Port : ITacticalExecutionPort
        {
            internal bool Terminal;
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { executionId = 1; issue = ""; return true; }
            public ExecutionObservation Observe(ulong executionId) => new ExecutionObservation(executionId, true, Terminal, Terminal, Terminal, Terminal);
            public bool TryAbortPreAction(ulong executionId, string reason) => false;
        }

        private sealed class RejectingSink : IGameplayTriggerActionSink, IGameplayTriggerReliableActionPreparationValidator
        {
            public GameplayTriggerActionKindId ActionKindId => GameplayTriggerStoryCommandActionKinds.RaiseTrigger;
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.ReliableDeferred;
            public bool TryValidateReliableAction(in GameplayTriggerActionPreparation preparation) => false;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) { token = 0; throw new InvalidOperationException(); }
            public void Abort(int token) => throw new InvalidOperationException();
            public void Commit(int token, in GameplayTriggerActionIntent intent) => throw new InvalidOperationException();
        }
    }
}
