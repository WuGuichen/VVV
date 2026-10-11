using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalSchedulingTests
    {
        private static CharacterTestRuntimeAiLabCompiledLogicProgram CreateCard(CharacterTestTacticalCardRuntimeFixture fixture,
            string id, int duration, bool lowHealth)
        {
            var content = lowHealth ? CharacterTestTacticalAuthoringTemplates.CreateSingleAttack(id) : CharacterTestTacticalAuthoringTemplates.CreateBlank(id);
            content.ActivationConditionRoot = null;
            if (!lowHealth) content.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "turn", Operation =
                new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = "currentCombatTarget" },
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), End = CharacterTestTacticalAuthoringTemplates.Absolute(duration) });
            var document = new CharacterTestTacticalDslSource { CardId = id, Name = id,
                ActivationConditionRoot = lowHealth ? CharacterTestTacticalDslTrialTemplates.LowHealth(0.5f) : null,
                Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence"),
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = id, Source = content } } };
            document.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", id));
            document.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Exit"));
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string cardId, out string issue), issue);
            document = workspace.ResolveComposition(workspace.Cards[cardId]);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
            var program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.Capture(workspace, workspace.Cards[cardId], document, fixture.Inputs, compiled));
            return program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(workspace.Cards[cardId], program));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActiveTacticalCardRefreshesFactsWithoutJointPlannerOrCardCompetitionThenResumes(bool nativeFallback)
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                fixture.Runtime.TrySetControlOwner(fixture.Source.Id.Value, CharacterTestCharacterControlOwner.AiControlled);
                fixture.Source.SyncCombatHealth(100, 100);
                fixture.PlaceTarget(4000);
                var programs = new[] { CreateCard(fixture, "hold", 30, false), CreateCard(fixture, "high-opportunity", 2, true) };
                if (nativeFallback)
                {
                    var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11, EntryTimelineId = "fallback" };
                    var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "fallback", Title = "普通兜底" };
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { TimelineId = "fallback", EventId = "exit",
                        StartFrame = 30, TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, DurationFrames = 1,
                        Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner } });
                    source.Timelines.Add(timeline);
                    var fallback = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source);
                    Assert.IsTrue(fallback.Success, string.Join(";", fallback.Diagnostics));
                    programs[0] = fallback.Program.WithSelectionDefinition(new CharacterTestBehaviorCardSelectionDefinition("native-fallback", 0,
                        new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback }));
                    Assert.IsNull(programs[0].TacticalSource);
                }
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("tactical-scheduling");
                for (int i = 0; i < 2; i++)
                {
                    var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        enabled: true, order: i + 1) { EntryId = "entry-" + i };
                    entry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey, i == 0 ? 10 : 100);
                    profile.Entries.Add(entry);
                }
                var profileCompile = CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile);
                Assert.IsTrue(profileCompile.Success, profileCompile.Report.ToText());
                var module = new CharacterTestRuntimeAiPlannerCommandModule(fixture.Runtime, fixture.Commands.Enqueue,
                    NullRuntimeLogger.Instance, profileCompile.Profile, CharacterTestDirectAiNavigationService.Instance, null, null, null);
                {
                    Assert.IsTrue(module.TryApplyBehaviorCompositionOverlay(profile, 0, programs, 1234, out var install), install.ReportText);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var evaluator = ((CharacterTestRuntimeAiPlannerEvaluator[])typeof(CharacterTestRuntimeAiPlannerCommandModule).GetField("_evaluators", flags).GetValue(module))[0];
                    var overlay = ((CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[])typeof(CharacterTestRuntimeAiPlannerCommandModule).GetField("_behaviorLogicOverlays", flags).GetValue(module))[0];
                    var rows = new List<object>();
                    void Tick()
                    {
                        int frame = fixture.NextFrame;
                        module.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.PreSimulation));
                        fixture.AdvanceWorldOnly();
                        var agent = overlay.CreateStateSnapshot(0).Agents.Single(a => a.EntityId == fixture.Source.Id.Value);
                        evaluator.TryGetFactState(fixture.Source.Id.Value, out var facts);
                        rows.Add(new { frame, active = agent.ActiveBindingIndex, overlaySelections = overlay.SelectionPassCount,
                            plannerSelections = evaluator.DecisionEvaluationCount, factRefreshes = evaluator.FactRefreshCount,
                            health = facts.SelfHealthRatio, distance = facts.TargetRootDistance,
                            jointCandidates = module.CaptureJointSelectionDiagnosticsForDiagnostics().Length });
                    }
                    Tick();
                    Assert.IsTrue(overlay.OwnsActiveCardExecution(fixture.Source.Id.Value));
                    Assert.AreEqual(0, overlay.CreateStateSnapshot(0).Agents[0].ActiveBindingIndex);
                    long selections = overlay.SelectionPassCount, baseSelections = evaluator.DecisionEvaluationCount, factsBefore = evaluator.FactRefreshCount;
                    fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 20);
                    fixture.PlaceTarget(5000);
                    for (int i = 0; i < 15; i++) Tick();
                    Assert.AreEqual(selections, overlay.SelectionPassCount, "Higher-priority opportunity must not trigger another card comparison during the active card.");
                    Assert.AreEqual(baseSelections, evaluator.DecisionEvaluationCount, "The base FSM/utility path must not run behind the active card.");
                    Assert.AreEqual(factsBefore + 15, evaluator.FactRefreshCount);
                    Assert.IsEmpty(module.CaptureJointSelectionDiagnosticsForDiagnostics(), "Joint action/zone candidates must not be generated for the active tactical actor.");
                    Assert.IsTrue(evaluator.TryGetFactState(fixture.Source.Id.Value, out var currentFacts));
                    Assert.LessOrEqual(currentFacts.SelfHealthRatio, 0.5f);
                    Assert.Greater(currentFacts.TargetRootDistance, 4f);
                    Assert.AreEqual(0, overlay.CreateStateSnapshot(0).Agents[0].ActiveBindingIndex);
                    bool selectedHigh = false;
                    for (int i = 0; i < 45 && !selectedHigh; i++)
                    {
                        Tick(); selectedHigh = overlay.CreateStateSnapshot(0).Agents[0].ActiveBindingIndex == 1;
                    }
                    Assert.IsTrue(selectedHigh, "The configured higher-priority card must become selectable after explicit completion/cleanup.");
                    Assert.Greater(overlay.SelectionPassCount, selections);
                    string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M2c"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, nativeFallback ? "native-fallback-fact-refresh.json" : "active-card-fact-refresh.json"), JsonConvert.SerializeObject(new {
                        source = "production CommandModule / evaluator / overlay / command queue / CharacterRuntime", nativeFallback, rows,
                        selectedHighAfterCompletion = selectedHigh }, Formatting.Indented));
                }
            }
        }
    }
}
