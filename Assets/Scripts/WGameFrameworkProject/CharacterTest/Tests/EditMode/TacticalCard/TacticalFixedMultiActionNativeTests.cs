using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Tests.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Prepared native regression: actual published two-action Host and Action gateway.
    /// The interrupt policy is explicit fixture content; this is not formal-content or GameView evidence.</summary>
    public sealed class TacticalFixedMultiActionNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = UnityEngine.Time.timeScale;
        [TearDown] public void Teardown() => UnityEngine.Time.timeScale = _scale;

        private const int ExitAbilityId = 93131;
        private static CharacterTestCharacterActionRuntime ControlledRuntime(int actionId, bool priceActions = false)
        {
            var original = CharacterTestCharacterActionConfigSource.Load(CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            Assert.IsTrue(original.Registry.TryGetAction(new CharacterActionId(actionId), out var action));
            var interrupts = new[] { new CharacterInterruptRule(CharacterActionSourceKind.Hit, int.MinValue),
                new CharacterInterruptRule(CharacterActionSourceKind.Reaction, int.MinValue),
                new CharacterInterruptRule(CharacterActionSourceKind.CohesionBreak, int.MinValue) };
            var controlled = new CharacterActionConfig(action.ActionId, action.DisplayName, action.Category, action.TimelineAuthority,
                action.Tags, action.Priority, action.DurationFrames, priceActions ? action.Requirements.Concat(new[] {
                    new CharacterActionRequirement(CharacterActionRequirementKind.CooldownReady, referenceId: ExitAbilityId) }).ToArray()
                    : action.Requirements, action.Phases, action.CancelRules,
                interrupts, action.MotionTrack, action.CombatTrack, action.GameplayTrack, action.AnimationTrack,
                action.PresentationTrack, action.DebugTrack, action.CohesionSupport);
            var sets = priceActions ? original.Registry.ActionSets.Select(set => new CharacterActionSetConfig(
                set.Id, set.StableId, set.DisplayName, set.CharacterStableId, set.EquipmentStateStableId, set.CommandBindings,
                set.AbilityBindings.Concat(new[] { new CharacterAbilityActionBinding(ExitAbilityId, new CharacterActionId(actionId)) }).ToArray(),
                set.ReactionBindings, set.MovementProfileId, set.ReactionProfileId, set.DefaultActionId, set.DefaultActionSlotMask,
                set.LandingProfileId, set.CommandSequences)).ToArray() : original.Registry.ActionSets;
            var registry = new CharacterActionConfigRegistry(sets,
                original.Registry.Actions.Select(a => a.ActionId.Value == actionId ? controlled : a).ToArray(),
                original.Registry.ActionSetsSourcePath, original.Registry.ActionsSourcePath,
                original.Registry.ReactionProfiles, original.Registry.ReactionProfilesSourcePath,
                original.Registry.LandingProfiles, original.Registry.LandingProfilesSourcePath);
            return new CharacterTestCharacterActionRuntime(original.ImportResult, registry, original.ValidationReport,
                original.ActionSet, original.ConfigSourceKind, "Two-action regression; fixture interrupt policy",
                original.ActionSetsSourcePath, original.ActionsSourcePath, original.ConfigSourceDiagnostics.ToArray());
        }

        private static void Publication(CharacterTestTacticalCardRuntimeFixture f, out TacticalActionCardDefinition definition,
            out CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank("Two actual actions");
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "first",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(1),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "primary" } });
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "second",
                Start = CharacterTestTacticalAuthoringTemplates.Anchor("first", "ActionEnd", 30),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "primary" } });
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "face",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Anchor("second", "ActionEnd"),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" } });
            var inputs = CharacterTestTacticalCardInputs.BindSlots(f.Source.Identity.CharacterId,
                new Dictionary<string, CharacterTestTacticalCardInputs> { { "primary", f.Inputs } });
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("two-action-fixture", "bc_qkjc3ka7za", "Two actual actions", "Tactical", null);
            draft.SetTacticalSource(source);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var errors), string.Join(";", errors));
            var frozen = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(
                CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled)), null);
            var publication = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("Two actual actions", "fixture.icon", 3, "Two actual actions", "", ""),
                    CharacterTestAuthoredContentIds.D417_Id3), null, frozen);
            Assert.IsTrue(publication.Success);
            var published = publication.Card;
            var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("two-action-fixture", 1,
                new[] { published }, new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, f.Runtime.ActionRuntime.Registry));
            Assert.IsTrue(catalog.Success);
            state = new CharacterTestRuntimeAiPlannerProjectCatalogState(catalog.Catalog,
                new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog("two-action-fixture", 1, null), "", "", "", "", "");
            definition = new TacticalActionCardDefinition("two-action-fixture", "Two actual actions", "", 3,
                published.StableCardId, published.Revision, published.ContentHash.ToString(CultureInfo.InvariantCulture), 90);
        }
        private static AbilityRuntimeModule ExitAbility(int actionId)
        {
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(ExitAbilityId, "fixture.fixed-exit.ability", "Exit fixture cost", null,
                new AbilityCooldownConfig(0, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 1) }, 0,
                new AbilityActionBindingConfig(new CharacterActionId(actionId), CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId),
                default, 0, 0), out _));
            return new AbilityRuntimeModule(registry);
        }

        /// <summary>Actual GameSlice exit lane with a positive unspent second-action reservation.
        /// In-memory publication and Ability fees are fixture content, never persisted player configuration.</summary>
        internal sealed class PaidExitFixture : IDisposable
        {
            internal readonly GameSlice Slice;
            internal TacticalMixedPlayerCardRuntime Mixed => Slice.Services.Modules.MixedPlayerTacticalStage;
            internal TacticalCardInstance Card => Mixed.Stage.Deck.Hand.Single(c => c.Definition.FixedAssembly != null);
            internal PaidExitFixture()
            {
                int actionId;
                using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0))) actionId = probe.Action.ActionId.Value;
                TacticalActionCardDefinition definition; CharacterTestRuntimeAiPlannerProjectCatalogState state;
                using (var preparation = new CharacterTestTacticalCardRuntimeFixture(actionId: actionId,
                    actionRuntime: ControlledRuntime(actionId, true), abilityRuntime: ExitAbility(actionId))) Publication(preparation, out definition, out state);
                var bindings = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { { "primary", actionId } });
                var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
                snapshot.Assembly.MainCard.CardId = definition.PublishedCardId;
                snapshot.Assembly.MainCard.Revision = definition.PublishedRevision;
                snapshot.Assembly.MainCard.ContentHash = definition.PublishedContentHash;
                snapshot.Assembly.Accessories.Clear(); snapshot.Accessories = Array.Empty<CharacterTacticalAccessoryDefinition>();
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3, bindings, out var assembly, out var issue), issue);
                definition.AttachFixedAssembly(assembly);
                CharacterTestSliceOptions options; TacticalDeployment seedDeployment;
                using (var seed = new MxFramework.Tests.Items.JointInteractionPausedSessionNativeTests.Fixture()) {
                    options = seed.Options(); seedDeployment = seed.Deployment;
                }
                var participants = seedDeployment.Participants.Select(p => new TacticalDeploymentParticipant(p.EntityId, p.CharacterId,
                    p.CharacterStableId, p.DisplayName, p.TeamId, p.IsPlayerControlled, p.Position, p.YawDegrees,
                    p.IsPlayerControlled ? new TacticalCardDefinition[] { definition } : Array.Empty<TacticalCardDefinition>(),
                    p.IsPlayerControlled ? new Dictionary<ulong, ReadOnlyDictionary<string, int>> { { 90, bindings } }
                        : new Dictionary<ulong, ReadOnlyDictionary<string, int>>(),
                    p.CharacterItemInstanceId, p.InventoryOwnerId, p.CharacterItemRevision)).ToArray();
                options.TacticalDeployment = TacticalDeployment.CreateLabTrial(participants, 50, new TacticalCardTrialOptions(7, 1, 42, state));
                options.StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(); options.DetachedTacticalRestore = true;
                options.FreshActionRuntime = () => ControlledRuntime(actionId, true); options.FreshAbilityRuntime = () => ExitAbility(actionId);
                Slice = new GameSlice(options);
                try {
                    Slice.StartDetachedPausedHost(); var content = CharacterTestRuntimeAiLabCardTrialContent.Load();
                    foreach (var participant in participants) {
                        var character = content.Character(participant.CharacterId); var position = participant.Position;
                        Slice.CharacterRuntime.World.Spawn(new CharacterTestCharacterSpawnRequest(character.CharacterId, participant.TeamId,
                            position.x, position.y, position.z, participant.IsPlayerControlled ? CharacterTestCharacterControlOwner.LocalPlayer
                                : CharacterTestCharacterControlOwner.RemotePlayer), character);
                    }
                    for (int i = 0; i < 600 && (Mixed.Stage.Phase != TacticalStagePhase.Planning || !Mixed.PausedWorldTime.IsSimulationPaused); i++) Tick();
                    Assert.AreEqual(TacticalStagePhase.Planning, Mixed.Stage.Phase, Mixed.Stage.ExecutionIssue);
                    Assert.AreEqual(0, Mixed.Stage.Ledger.Reserved);
                } catch { Slice.Dispose(); throw; }
            }
            internal void Tick() { Slice.Tick(1d / 60d); Mixed.TickWorldTimeTransition(1f / 60f); }
            internal void Apply(TacticalStageCommandKind kind, TacticalCardInstance card)
            {
                Mixed.PausedAdapter.SetContext(null, Slice.CurrentFrame); var s = Mixed.Stage;
                var command = new TacticalStageCommand(s.StageRunId, s.TurnIndex, card.InstanceId, card.Version, s.SnapshotRevision, kind);
                Assert.AreEqual(TacticalStageRejection.None, s.Apply(in command), s.ExecutionIssue);
            }
            public void Dispose() => Slice.Dispose();
        }

        [TestCase(false)] [TestCase(true)]
        public void SecondOwnedActionCancellationOrInterruptionCannotKeepFirstCompletionOrReward(bool interrupt)
        {
            int actionId;
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0))) actionId = probe.Action.ActionId.Value;
            using var f = new CharacterTestTacticalCardRuntimeFixture(actionId: actionId, actionRuntime: ControlledRuntime(actionId));
            Publication(f, out var definition, out var state);
            var bindings = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { { "primary", actionId } });
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Assembly.MainCard.CardId = definition.PublishedCardId;
            snapshot.Assembly.MainCard.Revision = definition.PublishedRevision;
            snapshot.Assembly.MainCard.ContentHash = definition.PublishedContentHash;
            snapshot.Assembly.Accessories.Clear(); snapshot.Accessories = Array.Empty<CharacterTacticalAccessoryDefinition>();
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3, bindings, out var assembly, out var issue), issue);
            definition.AttachFixedAssembly(assembly);
            var deployment = TacticalDeployment.CreateLevel(new[] { new TacticalDeploymentParticipant("two-action-fixture",
                f.Source.Identity.CharacterId, "fixture", "Fixture", f.Source.Team.TeamId, true, default, 0,
                new TacticalCardDefinition[] { definition }, new Dictionary<ulong, ReadOnlyDictionary<string, int>> { { 90, bindings } }) }, 50);
            var timePort = new UnityTacticalTimeAdapter();
            var adapter = new CharacterTestTacticalExecutionAdapter(f.Runtime, f.Commands, f.Card, f.Definition, f.Source,
                timePort, deployment: deployment, publishedCatalogLoader: _ => state);
            var ledger = new MomentumLedger(50); f.Runtime.World.BindTacticalMotion(f.Source.Id.Value, ledger);
            var deck = new TacticalDeckManager(1); deck.InitializeDeck(new[] { definition }, 42, 1);
            var time = new TacticalTimeCoordinator(timePort); time.Initialize();
            f.Source.InteractionEffects.Bind(7, time.CurrentTurnIndex, f.Source.GameplayEntityId);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            var driver = new TacticalSingleCardDriver(adapter, time, deck, ledger);
            adapter.SetContext(f.Facts, new RuntimeFrame(f.NextFrame));
            Assert.IsTrue(driver.TryCommit(f.NextFrame - 1, out issue), issue);
            ulong id = driver.ExecutionId; CharacterActionInstance first = null, second = null;
            for (int i = 0; i < 1200 && second == null; i++) {
                f.RefreshFacts(); adapter.SetContext(f.Facts, new RuntimeFrame(f.NextFrame)); adapter.Tick(); f.AdvanceWorldOnly();
                adapter.Observe(id);
                var owned = f.Card.AcknowledgedActionInstance;
                if (owned == null) continue;
                if (first == null) first = owned;
                else if (!ReferenceEquals(first, owned) && owned.State == CharacterActionInstanceState.Running) second = owned;
            }
            Assert.NotNull(second, adapter.LastIssue); Assert.AreEqual(CharacterActionInstanceState.Finished, first.State);
            Assert.IsFalse(adapter.Observe(id).ActionTimelineCompleted, "The latest running action must replace the first action's completion.");
            Assert.AreSame(second, f.Runtime.ActionRuntime.GetActionInstanceForObservation(f.Source.Id),
                "The second ACK must refer to the actual current gateway instance.");
            int beforeTermination = ledger.Current;
            long secondId = second.InstanceId; var secondAction = second.Plan.ActionId;
            if (interrupt) {
                var hit = CharacterReactionContextBuilder.FromHitSourceNoAlloc(new CharacterReactionHitSource(
                    new RuntimeFrame(f.NextFrame), f.Source.GameplayEntityId,
                    currentScatterPressureBand: PressureBand.Broken, currentScatterPressure: 100, maxScatterPressure: 100, impactForce: 34));
                Assert.IsTrue(f.Runtime.ActionRuntime.TryReact(f.Source.Id, hit.Context, allowFallback: false), f.Source.Action.LastRejectReason);
                bool terminalEvent = false;
                var events = f.Runtime.ActionRuntime.TimelineEvents;
                for (int i = 0; i < events.Count; i++) {
                    var evt = events[i];
                    if (evt.EntityId == f.Source.Id.Value && evt.InstanceId == secondId && evt.ActionId == secondAction
                        && evt.Frame == f.NextFrame && evt.Kind == CharacterActionRunnerEventKind.ActionInterrupted) terminalEvent = true;
                }
                Assert.IsTrue(terminalEvent, "The original authority must publish the exact second action's interrupt event.");
                Assert.AreEqual(CharacterActionInstanceState.Interrupted, f.Card.AcknowledgedActionState);
                Assert.AreNotEqual(secondId, f.Runtime.ActionRuntime.GetActionInstanceForObservation(f.Source.Id).InstanceId);
            } else {
                Assert.IsTrue(f.Runtime.ActionRuntime.TerminateForOrchestration(f.Source.Id, new RuntimeFrame(f.NextFrame),
                    CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner));
                Assert.AreEqual(CharacterActionInstanceState.Cancelled, second.State);
            }
            var observation = adapter.Observe(id);
            Assert.IsFalse(observation.ActionTimelineCompleted); Assert.IsTrue(adapter.HasExecutionFault, adapter.LastIssue);
            for (int i = 0; i < 8; i++) {
                var sample = new SettleSample(f.NextFrame + i, true, true, true, false, false, false, true, true, true, false, 0, 0);
                Assert.IsFalse(driver.OnCompletedSimulationTick(in sample));
            }
            Assert.AreEqual(beforeTermination, ledger.Current); Assert.AreSame(definition, deck.ResolvingAction.Definition);
            Assert.AreEqual(1ul, time.CurrentTurnIndex);
            driver.ReleaseControl();
        }
    }
}
