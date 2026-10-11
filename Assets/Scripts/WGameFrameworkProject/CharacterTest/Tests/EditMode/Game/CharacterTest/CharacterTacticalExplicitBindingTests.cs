using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTacticalExplicitBindingTests
    {
        private CharacterTestTacticalCardRuntimeFixture _first, _second;
        [OneTimeSetUp] public void Setup()
        {
            _first = new CharacterTestTacticalCardRuntimeFixture(actionId: 301,
                sourceCharacterId: CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000002"));
            _second = new CharacterTestTacticalCardRuntimeFixture(actionId: 455,
                sourceCharacterId: CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000001"));
        }
        [OneTimeTearDown] public void Cleanup() { _second?.Dispose(); _first?.Dispose(); }
        private static CharacterTestTacticalAuthoringSource Source()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            source.Slots[0].Resolver = "binding";
            return source;
        }
        private static CharacterTestTacticalCardInputs Bind(CharacterTestTacticalCardRuntimeFixture fixture, params string[] slots)
        {
            var values = new Dictionary<string, CharacterTestTacticalCardInputs>();
            foreach (string slot in slots) values.Add(slot, fixture.Inputs);
            return CharacterTestTacticalCardInputs.BindSlots(fixture.Source.Identity.CharacterId, values);
        }
        [Test] public void SameAuthoredCardUsesEachExplicitPhysicalActionAndItsOwnDuration()
        {
            var source = Source();
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, Bind(_first, "primary"), out var first, out var errors), string.Join(";", errors));
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, Bind(_second, "primary"), out var second, out errors), string.Join(";", errors));
            Assert.AreEqual(301, first.EventActionId("attack"));
            Assert.AreEqual(455, second.EventActionId("attack"));
            Assert.AreEqual(_first.Inputs.DurationFrames + 1, first.Program.Execution.Timelines[0].DurationFrames);
            Assert.AreEqual(_second.Inputs.DurationFrames + 1, second.Program.Execution.Timelines[0].DurationFrames);
            Assert.AreNotEqual(first.DependencyFingerprint, second.DependencyFingerprint);
        }
        [Test] public void MissingSlotNeverFallsBackToAnEquippedOpener()
        {
            Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(Source(), Bind(_first, "different-slot"), out _, out var issues));
            Assert.IsTrue(issues.Any(i => i.Code == "ActionSlotUnavailable" || i.Code == "CompileRejected"));
        }
        [Test] public void EventBlockUsesEachRolesActionAndKeepsItsLocalZeroOrigin()
        {
            var source = Source();
            var block = CharacterTestTacticalBlockEdits.Ensure(source, source.EntryTimelineId, "attack", "block.attack");
            foreach (var fixture in new[] { _first, _second })
            {
                var preview = CharacterTestTacticalAuthoringPreview.FromSource(source, Bind(fixture, "primary"), "");
                Assert.IsTrue(preview.Ready, preview.Diagnostic);
                preview.SetEventFocus(block.BlockId);
                Assert.AreEqual(1, preview.DisplayOrigin);
                var displayed = preview.DisplayPayload.Timelines.Single().Events.Single();
                Assert.AreEqual("attack", displayed.EventId);
                Assert.AreEqual(0, displayed.StartFrame);
                Assert.AreEqual(fixture.Inputs.Action.ActionId, displayed.Operation.CharacterActionId);
                Assert.AreEqual(fixture.Inputs.DurationFrames, displayed.DurationFrames);
            }
        }
        [Test] public void RepeatedSharedDefinitionCompilesPerInstanceAndRequiresBothBindings()
        {
            var shared = Source(); shared.ActivationConditionRoot = null;
            CharacterTestTacticalBlockEdits.Ensure(shared, shared.EntryTimelineId, "attack", "block.attack");
            var source = new CharacterTestTacticalDslSource { CardId = "binding-test",
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    new CharacterTestTacticalDslNode { NodeId = "first", Kind = "Reference", DefinitionId = "shared" },
                    new CharacterTestTacticalDslNode { NodeId = "second", Kind = "Reference", DefinitionId = "shared" } } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "shared", Source = shared } } };
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(source, Bind(_first, "first/primary", "second/primary"), out var compiled, out string issue), issue);
            Assert.IsTrue(compiled.SharedCompilations.ContainsKey("first"));
            Assert.IsTrue(compiled.SharedCompilations.ContainsKey("second"));
            Assert.AreEqual(2, compiled.SourceLocations.Values.Count(location => location.BlockId == "block.attack"));
            Assert.IsFalse(CharacterTestTacticalDslCompiler.TryCompile(source, Bind(_first, "first/primary"), out _, out issue));
        }
        [Test] public void RenamingSlotUpdatesAllEventReferencesWithoutChoosingAnAction()
        {
            var source = Source();
            CharacterTestTacticalSlotEditing.Rename(source, "primary", "impact");
            Assert.AreEqual("impact", source.Timelines[0].Events.Single(e => e.Operation.Kind == "CharacterAction").Operation.SlotId);
            Assert.AreEqual("binding", source.Slots[0].Resolver);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, Bind(_first, "impact"), out var compiled, out var issues), string.Join(";", issues));
            Assert.AreEqual(301, compiled.EventActionId("attack"));
        }

        [Test] public void PublishedTacticalCatalogRoundTripPreservesIntegerIdentityAndSource()
        {
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, _first.Runtime.ActionRuntime.Registry);
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
            Assert.IsTrue(state.Success, state.Diagnostic);
            Assert.IsTrue(state.CardCatalog.TryGetExact("tactical.bound-action", 1, out var original));
            Assert.IsNotNull(original.TacticalSource);
            Assert.IsTrue(original.TacticalSource.HasPlayerActionSlot,
                "An authored action slot remains player-configurable regardless of its default resolver.");
            var loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(state.CardCatalog), references);
            Assert.IsTrue(loaded.Success, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
            Assert.IsTrue(loaded.Catalog.TryGetExact(original.StableCardId, original.Revision, out var restored));
            Assert.Greater(restored.NumericId, 0);
            Assert.AreEqual(original.NumericId, restored.NumericId);
            Assert.AreEqual(original.ContentHash, restored.ContentHash);

            var tacticalContent = new CharacterTacticalContentCatalog();
            tacticalContent.SetPublishedCards(new[] { restored });
            Assert.IsTrue(tacticalContent.Cards.Single().HasActionSlot);
            Assert.AreEqual(1, tacticalContent.Cards.Single().Slots.Length,
                "Player cards expose only whether one action slot exists; the internal path has no player meaning.");
        }
    }
}
