using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestActionUseAuthorityTests
    {
        private static AbilityRuntimeModule CreateUsageOwner()
        {
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(ControlledAbilityId, OwnerId, "Controlled opener usage", null,
                new AbilityCooldownConfig(50, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp, 10) },
                0, new AbilityActionBindingConfig(new CharacterActionId(301), global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0030), default, 0, 0), out _));
            return new AbilityRuntimeModule(registry);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CostOwnerRefusalRestoresPriorGatewayWithoutPublishingActionOrCleanup(bool hadPriorAction)
        {
            var abilities = CreateUsageOwner();
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: abilities))
            {
                var runtime = fixture.Runtime.ActionRuntime; var frame = new RuntimeFrame(fixture.NextFrame);
                if (hadPriorAction) Assert.IsTrue(runtime.ReplaceForOrchestration(fixture.Source.Id, new CharacterActionId(301),
                    CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump, frame, new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0001)));
                var store = (CharacterTestCharacterActionExecutionStore)typeof(CharacterTestCharacterActionRuntime)
                    .GetField("_executions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                bool hadGateway = store.TryGet(fixture.Source.Id, out var gateway);
                string gatewayBefore = hadGateway ? JsonConvert.SerializeObject(gateway.CaptureState()) : null;
                string actionBefore = JsonConvert.SerializeObject(fixture.Source.Action);
                var animationBefore = fixture.Source.Animation;
                string abilityBefore = JsonConvert.SerializeObject(abilities.CreateStateSnapshot());
                int hpBefore = fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp);
                var refusal = new RefusingCostOwner(); abilities.SetCostAuthority(refusal);
                Assert.IsTrue(runtime.Registry.TryGetAction(new CharacterActionId(301), out var action));
                var qualification = fixture.Runtime.ActionUseAuthority.Evaluate(fixture.Source, 301, frame, fixture.Facts);
                Assert.IsTrue(qualification.Available, qualification.Status.ToString());
                var result = fixture.Runtime.ActionUseAuthority.TryCommit(fixture.Source, action, frame, fixture.Facts);
                Assert.IsFalse(result.Committed); Assert.AreEqual(1, refusal.CommitAttempts, result.Reason);
                Assert.AreEqual(hadGateway, store.TryGet(fixture.Source.Id, out var restoredGateway));
                if (hadGateway) Assert.AreEqual(gatewayBefore, JsonConvert.SerializeObject(restoredGateway.CaptureState()));
                Assert.AreEqual(actionBefore, JsonConvert.SerializeObject(fixture.Source.Action));
                Assert.AreEqual(animationBefore, fixture.Source.Animation);
                Assert.AreEqual(abilityBefore, JsonConvert.SerializeObject(abilities.CreateStateSnapshot()));
                Assert.AreEqual(hpBefore, fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(0, store.EventBuffer.Count, "Neither candidate start nor prior-owner cleanup was published.");
                WriteAtomicEvidence("cost-owner-refusal-" + hadPriorAction, new { hadPriorAction, result.Committed, result.Reason, refusal.CommitAttempts,
                    priorGatewayPreserved = true, animationUnchanged = true, costsAndCooldownUnchanged = true });
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfiguredCommitFailureHandsMovementAndTurnToRetreatInTheSameWorldFrame(bool withApproachMomentum)
        {
            var abilities = CreateUsageOwner();
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: abilities))
            {
                var attack = CharacterTestTacticalAuthoringTemplates.CreateBlank("有消耗的进攻段");
                attack.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "attack", Start = CharacterTestTacticalAuthoringTemplates.Absolute(5),
                    Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "primary" } });
                attack.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "approach", Start = CharacterTestTacticalAuthoringTemplates.Absolute(0),
                    End = CharacterTestTacticalAuthoringTemplates.Absolute(80), Operation = new CharacterTestTacticalAuthoringOperation {
                        Kind = "ApproachOptimalRange", SourceActionEventId = "attack" } });
                var retreat = CharacterTestTacticalAuthoringTemplates.CreateBlank("后撤并转向");
                retreat.Schema = CharacterTestTacticalAuthoringSource.EventSetSchema; retreat.Version = 1;
                foreach (string kind in new[] { "RetreatFromTarget", "TurnToTarget" })
                    retreat.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = kind,
                        Priority = kind == "TurnToTarget" ? 1 : 0,
                        Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(24),
                        Operation = new CharacterTestTacticalAuthoringOperation { Kind = kind, Target = "currentCombatTarget" } });
                var attackUse = CharacterTestTacticalDslTrialTemplates.Reference("attack-use", "attack-definition");
                attackUse.EndCondition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "failed-commit",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal, SourceEventId = "attack",
                    Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed };
                var document = new CharacterTestTacticalDslSource { CardId = "failed-attack-retreat", Name = "进攻提交失败则后撤",
                    Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                        attackUse, CharacterTestTacticalDslTrialTemplates.Reference("retreat-use", "retreat-definition"),
                        new CharacterTestTacticalDslNode { NodeId = "explicit-exit", Kind = "Exit" } } },
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "attack-definition", Source = attack },
                        new CharacterTestTacticalSharedDefinition { DefinitionId = "retreat-definition", Source = retreat } } };
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                document = workspace.ResolveComposition(workspace.Cards[id]);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
                var program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.Capture(workspace, workspace.Cards[id], document, fixture.Inputs, compiled));
                program = program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(workspace.Cards[id], program));
                var fallbackBuilder = new CharacterTestBehaviorExecutionBuilder(new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, fixture.Runtime.ActionRuntime.Registry), "fallback");
                fallbackBuilder.AddTimeline("fallback", 120); fallbackBuilder.AddReturn("fallback-exit", "fallback", 119, 1);
                var fallbackCard = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, "fixture.fallback", "明确兜底", "fixture", null)
                { RequiredCapabilities = new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback } };
                var fallback = fallbackBuilder.Build().WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromCard(fallbackCard));
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("same-frame-commit-failure");
                for (int i = 0; i < 2; i++) profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, i)
                    { EntryId = "entry-" + i, NumericId = FirstCardEntryId + i });
                var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile, new[] { program, fallback }, fixture.Runtime.ActionRuntime,
                    abilities, 7811, entities: fixture.Runtime.Entities, navigationService: CharacterTestDirectAiNavigationService.Instance, characterRuntime: fixture.Runtime);
                fixture.PlaceTarget(withApproachMomentum ? 9000 : (int)Math.Round((float)fixture.Inputs.Metrics.SpatialSummary.OptimalDistance.Value * 1000));
                fixture.Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    fixture.Source.Transform.XMillimeters, fixture.Source.Transform.YMillimeters, fixture.Source.Transform.ZMillimeters, 90000));
                bool approached = false; long failureFrame = -1; int movementDelta = 0, turnDelta = 0, previousMovementDelta = 0;
                void Tick()
                {
                    // Approach now consumes real hurtbox clearance, not the legacy root-distance-only fixture facts.
                    fixture.RefreshFacts(includeHurtboxDistance: true); var frame = new RuntimeFrame(fixture.NextFrame);
                    Assert.IsTrue(overlay.TryTick(fixture.Source, fixture.Facts, frame, out var intent));
                    if (intent.Kind != CharacterTestRuntimeAiLabRuntimeIntentKind.None)
                    { var submitted = CharacterTestBehaviorExecutionCommandWriter.Submit(frame, fixture.Source.Id.Value, intent, fixture.Commands.Enqueue); overlay.RecordCommandResult(fixture.Source.Id.Value, submitted); Assert.IsTrue(submitted.Success); }
                    int z = fixture.Source.Transform.ZMillimeters, yaw = fixture.Source.Transform.YawMilliDegrees;
                    long attempts = fixture.Runtime.ActionUseAuthority.CommitCount;
                    fixture.AdvanceWorldOnly();
                    if (fixture.Runtime.ActionUseAuthority.CommitCount == attempts)
                    { approached |= fixture.Source.Transform.ZMillimeters > z; previousMovementDelta = fixture.Source.Transform.ZMillimeters - z; }
                    else
                    {
                        failureFrame = frame.Value; movementDelta = fixture.Source.Transform.ZMillimeters - z; turnDelta = fixture.Source.Transform.YawMilliDegrees - yaw;
                        var state = overlay.CreateStateSnapshot(0).Agents.Single().ExecutionState;
                        WriteAtomicEvidence("same-frame-retreat-probe-" + withApproachMomentum, new { withApproachMomentum, failureFrame,
                            movementDelta, previousMovementDelta, turnDelta, worldMoveZ = fixture.Source.Input.WorldMove.y,
                            continuationCommands = fixture.Commands.LastBehaviorContinuations.Count, timeline = program.ExecutionDebugMap.Timeline(state.Timeline) });
                        Assert.AreEqual("retreat-use", program.ExecutionDebugMap.Timeline(state.Timeline));
                        Assert.Less(fixture.Source.Input.WorldMove.y, 0, "The formal command consumer owns the retreat input in the rejection frame.");
                        if (withApproachMomentum)
                            Assert.Less(movementDelta, previousMovementDelta, "The existing acceleration model must begin braking the previous forward velocity immediately.");
                        else Assert.Less(movementDelta, 0, "From rest, actual displacement must retreat in the rejection frame.");
                        Assert.Less(turnDelta, 0, "The explicitly higher-ranked turn control must rotate toward the target in that same frame.");
                        Assert.IsFalse(fixture.Source.Input.BehaviorControl.IsRelease);
                        Assert.IsTrue(fixture.Commands.LastBehaviorContinuations.Count > 0);
                        Assert.IsTrue(fixture.Commands.LastBehaviorContinuations.All(c => c.Frame.Value == frame.Value));
                        Assert.IsTrue(state.Results.Contains((int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed));
                        Assert.IsTrue(state.CommitFailureDetails.Any(r => r.Status == CharacterTestActionUseStatus.ResourceUnavailable));
                    }
                }
                Tick(); Assert.AreEqual(1, overlay.SelectionPassCount);
                fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 5);
                for (int i = 0; i < 20 && failureFrame < 0; i++) Tick();
                Assert.GreaterOrEqual(failureFrame, 0); if (withApproachMomentum) Assert.IsTrue(approached);
                Assert.AreEqual(1, overlay.SelectionPassCount);
                for (int i = 0; i < 6; i++) Tick();
                Assert.AreEqual(1, fixture.Runtime.ActionUseAuthority.CommitCount);
                Assert.AreEqual(1, overlay.SelectionPassCount, "No card competition inside the configured retreat continuation.");
                for (int i = 0; i < 40 && overlay.SelectionPassCount == 1; i++) Tick();
                Assert.Greater(overlay.SelectionPassCount, 1, "Competition resumes after configured exit.");
                StringAssert.Contains("提交失败", program.TacticalSource.Description);
                WriteAtomicEvidence("same-frame-retreat-" + withApproachMomentum, new { withApproachMomentum, failureFrame, movementDelta, turnDelta, approached,
                    attempts = fixture.Runtime.ActionUseAuthority.CommitCount, selectionPasses = overlay.SelectionPassCount, description = program.TacticalSource.Description });
            }
        }

        private sealed class RefusingCostOwner : IAbilityCostAuthority
        {
            internal int CommitAttempts;
            public bool CanAfford(int entity, IReadOnlyList<AbilityCostConfig> costs, out int missing) { missing = 0; return true; }
            public bool TryCommitCosts(int entity, IReadOnlyList<AbilityCostConfig> costs, out int missing) { CommitAttempts++; missing = 1; return false; }
        }
        private static void WriteAtomicEvidence(string name, object value)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/AtomicActionUse"));
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
        }
    }
}
