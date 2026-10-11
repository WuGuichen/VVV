using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalDefinitionOrderingTests
    {
        [Test]
        public void ActualTacticalContentDerivesAttackAuxiliaryAndFallbackWithoutUnusedActions()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                CharacterTestRuntimeAiLabCompiledLogicProgram Compile(CharacterTestTacticalDslSource document)
                {
                    if (document.Root.Kind != "Sequence")
                    {
                        var child = document.Root;
                        document.Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence");
                        document.Root.Children.Add(child);
                    }
                    var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                    Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                    document = workspace.ResolveComposition(workspace.Cards[id]);
                    Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
                    var program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.Capture(workspace, workspace.Cards[id], document, fixture.Inputs, compiled));
                    return program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(workspace.Cards[id], program));
                }
                var attackSource = CharacterTestTacticalDslTrialTemplates.Create();
                attackSource.Root = CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.attack");
                var attack = Compile(attackSource);
                Assert.AreEqual(300, attack.SelectionDefinition.Priority);
                CollectionAssert.Contains(attack.SelectionDefinition.Capabilities, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage);
                CollectionAssert.Contains(attack.SelectionDefinition.Capabilities, CharacterTestRuntimeAiPlannerBehaviorCapability.Engage);
                CollectionAssert.AreEqual(new[] { fixture.Inputs.Action.ActionId.Value }, CharacterTestBehaviorExecutionUsage.Analyze(attack).ActionIds);

                var auxiliary = CharacterTestTacticalDslTrialTemplates.Create();
                auxiliary.Root = CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.face");
                var turn = Compile(auxiliary);
                Assert.AreEqual(10, turn.SelectionDefinition.Priority);
                Assert.IsEmpty(CharacterTestBehaviorExecutionUsage.Analyze(turn).ActionIds, "Unused attack definitions and the prepared opener are not usage.");
                CollectionAssert.DoesNotContain(turn.SelectionDefinition.Capabilities, CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);

                var empty = new CharacterTestTacticalDslSource { CardId = "empty", Name = "empty", Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence") };
                Assert.AreEqual(10, Compile(empty).SelectionDefinition.Priority, "Empty capabilities alone do not mean configured fallback.");
                empty.Root = CharacterTestTacticalCompositionEdits.NewNode("Exit");
                Assert.AreEqual(0, Compile(empty).SelectionDefinition.Priority);

                var unreachable = CharacterTestTacticalDslTrialTemplates.Create();
                unreachable.Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence");
                unreachable.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Exit"));
                unreachable.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.attack"));
                var stopped = Compile(unreachable);
                Assert.IsEmpty(CharacterTestBehaviorExecutionUsage.Analyze(stopped).ActionIds);
                Assert.AreEqual(0, stopped.SelectionDefinition.Priority);

                var conditional = CharacterTestTacticalDslTrialTemplates.Create();
                conditional.Root = CharacterTestTacticalCompositionEdits.NewNode("If");
                conditional.Root.Condition = CharacterTestTacticalDslTrialTemplates.LowHealth(0.1f);
                conditional.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.attack"));
                Assert.Greater(fixture.Source.CombatState.Health, 10);
                var possible = Compile(conditional);
                Assert.IsNotEmpty(CharacterTestBehaviorExecutionUsage.Analyze(possible).ActionIds, "Static usage must not prune a condition because current HP makes it false.");
                Assert.AreEqual(300, possible.SelectionDefinition.Priority);
            }
        }

        [Test]
        public void ExplicitFallbackDoesNotOverrideHigherPriorityCapabilityAndTurnIsNotFallback()
        {
            Assert.AreEqual(0, CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScoreFromCapabilities(
                new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback }));
            Assert.AreEqual(200, CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScoreFromCapabilities(
                new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback, CharacterTestRuntimeAiPlannerBehaviorCapability.Damage }));
            Assert.AreEqual(10, CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScoreFromCapabilities(Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>()));
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload NativeHold()
        {
            var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { BehaviorRecipeVersion = 11, EntryTimelineId = "hold" };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "hold" };
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "exit", TimelineId = "hold", StartFrame = 30,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, DurationFrames = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner } });
            source.Timelines.Add(timeline); return source;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefinitionPriorityAndStableOrderIgnoreInstallationOrderAndOverrides(bool samePriority)
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var a = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, "bc_aaaaaaaaaa", "A", "test", null);
                var b = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, "bc_bbbbbbbbbb", "B", "test", null);
                if (!samePriority) b.RequiredCapabilities = new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Defense };
                var compiled = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(NativeHold()); Assert.IsTrue(compiled.Success);
                string expected = samePriority ? a.PublishedStableId : b.PublishedStableId;
                foreach (bool reverse in new[] { false, true, false })
                {
                    var cards = reverse ? new[] { b, a } : new[] { a, b };
                    var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("definition-order");
                    var programs = cards.Select(c => compiled.Program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromCard(c))).ToArray();
                    for (int i = 0; i < cards.Length; i++)
                    {
                        var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                            enabled: true, order: i == 0 ? 999 : -999) { EntryId = Guid.NewGuid().ToString("N") };
                        entry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey, cards[i].PublishedStableId == expected ? 0 : 9999);
                        profile.Entries.Add(entry);
                    }
                    var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile, programs, fixture.Runtime.ActionRuntime, null,
                        1000, entities: fixture.Runtime.Entities, characterRuntime: fixture.Runtime);
                    Assert.IsTrue(overlay.TryTick(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame), out _));
                    int selected = overlay.CreateStateSnapshot(0).Agents.Single().ActiveBindingIndex;
                    Assert.AreEqual(expected, programs[selected].SelectionDefinition.DefinitionId);
                    Assert.AreEqual(1, overlay.SelectionPassCount);
                }
            }
        }

        [Test]
        public void ReservedIdentityStaysUnpublishedAcrossCloneSaveReloadAndCopyGetsNewIdentity()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M2c/identity-" + Guid.NewGuid().ToString("N")));
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(directory);
            var workspace = persistence.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
            var card = workspace.Cards[id]; string stable = card.PublishedStableId;
            Assert.IsTrue(CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(stable));
            Assert.AreEqual(0, card.SourcePublishedRevision); Assert.IsFalse(card.HasPublishedSource);
            Assert.AreEqual(stable, card.Clone(false).PublishedStableId);
            Assert.AreEqual(stable, card.Clone(true).PublishedStableId, "Cloning a draft instance is not independently publishing a new definition.");
            Assert.IsTrue(workspace.TryDuplicateCompositionCard(new CharacterTestTacticalEditToken(workspace, card), 1, 0, out string copyId, out issue), issue);
            string copyStable = workspace.Cards[copyId].PublishedStableId;
            Assert.AreNotEqual(stable, copyStable); Assert.IsFalse(workspace.Cards[copyId].HasPublishedSource);
            Assert.IsTrue(workspace.TryUndo(out issue), issue); Assert.IsTrue(workspace.TryRedo(out issue), issue);
            Assert.AreEqual(copyStable, workspace.Cards[copyId].PublishedStableId);
            persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            var reader = new CharacterTestRuntimeAiLabDraftPersistence(directory);
            Assert.IsTrue(reader.TryLoad(workspace.WorkspaceId, out var restored, out issue), issue);
            Assert.AreEqual(stable, restored.Cards[id].PublishedStableId); Assert.AreEqual(copyStable, restored.Cards[copyId].PublishedStableId);
            Assert.IsFalse(restored.Cards[id].HasPublishedSource); Assert.IsFalse(restored.Cards[copyId].HasPublishedSource);
            Assert.AreEqual(stable, CharacterTestBehaviorCardSelectionDefinition.FromCard(restored.Cards[id]).DefinitionId);
        }
    }
}
