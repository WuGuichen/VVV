using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalFrozenSourceTests
    {
        private static CharacterTestRuntimeAiLabDraftWorkspace Workspace(CharacterTestTacticalDslSource source, out string id)
        {
            var w = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(w.TryImportComposition(1, 0, w.CurrentRevision, source, out id, out string issue), issue); return w;
        }
        private static CharacterTestRuntimeAiLabCompiledLogicProgram Freeze(CharacterTestRuntimeAiLabDraftWorkspace w, string id,
            CharacterTestTacticalCardRuntimeFixture fixture)
        {
            var document = w.ResolveComposition(w.Cards[id]);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out string issue), issue);
            return compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.Capture(w, w.Cards[id], document, fixture.Inputs, compiled));
        }

        [Test]
        public void EveryGeneratedEventAndConditionHasExplicitFrozenLocationWithoutOwnerCollision()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var document = CharacterTestTacticalDslTrialTemplates.Create();
                document.Root.Children[0].NodeId = "a"; document.Root.Children[1].NodeId = "a/end";
                document.ActivationConditionRoot = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack().ActivationConditionRoot;
                var w = Workspace(document, out string id); var program = Freeze(w, id, fixture);
                var locations = program.TacticalSource.Locations;
                for (int i = 0; i < program.Execution.Events.Length; i++) Assert.IsTrue(locations.ContainsKey(program.ExecutionDebugMap.Event(i)));
                for (int i = 0; i < program.Execution.Conditions.Length; i++) Assert.IsTrue(locations.ContainsKey(program.ExecutionDebugMap.Condition(i)));
                Assert.IsTrue(locations.Values.Any(l => l.NodeId == "a" && l.Field == "endCondition" && l.DefinitionId == string.Empty));
                Assert.IsTrue(locations.Values.Any(l => l.NodeId == "a/end" && l.Field == "condition" && l.DefinitionId == string.Empty));
                var activation = locations.Values.Where(l => l.Field == "activationConditionRoot").ToArray();
                Assert.IsNotEmpty(activation); Assert.IsTrue(activation.All(l => l.CardId == id && l.NodeId == string.Empty));
                Assert.IsTrue(locations.Values.Any(l => l.DefinitionId == "shared.attack" && l.EventId == "attack" && l.Field == "conditionRoot"));
            }
        }

        [Test]
        public void SharedEditsInvalidateNewProgramsButFrozenSourceAndLocationsStayAtOldVersion()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var w = Workspace(CharacterTestTacticalDslTrialTemplates.Create(), out string id);
                var first = Freeze(w, id, fixture); string json = CharacterTestTacticalDslJson.Write(first.TacticalSource.CloneDocument());
                var shared = w.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack");
                shared.Source.Timelines[0].Events.Single(e => e.EventId == "approach").End.OffsetFrames = 4;
                Assert.IsTrue(w.TryReplaceTacticalDefinition(w.CaptureTacticalDefinitionEdit(shared.DefinitionId), shared.Source, true, out string issue), issue);
                var second = Freeze(w, id, fixture);
                Assert.AreNotEqual(first.SemanticHash, second.SemanticHash);
                Assert.AreEqual(json, CharacterTestTacticalDslJson.Write(first.TacticalSource.CloneDocument()));
                Assert.IsTrue(first.TacticalSource.Locations.Values.Where(l => l.DefinitionId == "shared.attack").All(l => l.DefinitionRevision == 1));
                Assert.IsTrue(second.TacticalSource.Locations.Values.Where(l => l.DefinitionId == "shared.attack").All(l => l.DefinitionRevision == 2));
                first.TacticalSource.CloneDocument().Definitions.Clear();
                Assert.AreEqual(3, first.TacticalSource.CloneDocument().Definitions.Count);
                Assert.IsTrue(first.TacticalSource.ValidateBinding(fixture.Runtime, fixture.Source, out issue), issue);
            }
        }

        [Test]
        public void ChangedActualOpenerCannotStartOldFrozenProgram()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var w = Workspace(CharacterTestTacticalDslTrialTemplates.Create(), out string id); var program = Freeze(w, id, fixture);
                Assert.IsTrue(program.TacticalSource.ValidateBinding(fixture.Runtime, fixture.Source, out string issue), issue);
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var set));
                var sequence = Array.Find(set.CommandSequences, s => s.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId);
                var original = sequence.ActionIds[0]; var equipment = fixture.Source.Equipment;
                var initialHost = new CharacterTestBehaviorExecutionHost(program, fixture.Runtime, fixture.Runtime.ActionRuntime,
                    null, fixture.Commands.Enqueue, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache);
                fixture.RefreshFacts(); Assert.IsTrue(initialHost.Start(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)), initialHost.LastFailureReason);
                var state = initialHost.CreateSnapshot(); initialHost.Stop(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)); fixture.AdvanceWorldOnly();
                var restoredHost = new CharacterTestBehaviorExecutionHost(program, fixture.Runtime, fixture.Runtime.ActionRuntime,
                    null, fixture.Commands.Enqueue, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache);
                Assert.IsTrue(restoredHost.TryRestoreSnapshot(state, fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)), restoredHost.LastFailureReason);
                try
                {
                    sequence.ActionIds[0] = new CharacterActionId(324);
                    fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(equipment.UseDefaultLoadout, set.EquipmentStateStableId));
                    Assert.IsFalse(program.TacticalSource.ValidateBinding(fixture.Runtime, fixture.Source, out issue));
                    var host = new CharacterTestBehaviorExecutionHost(program, fixture.Runtime, fixture.Runtime.ActionRuntime,
                        null, fixture.Commands.Enqueue, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache);
                    fixture.RefreshFacts();
                    Assert.IsFalse(host.Start(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                    StringAssert.Contains("Frozen tactical", host.LastFailureReason);
                    Assert.IsFalse(host.TryRestoreSnapshot(state, fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                    string beforeRestore = Newtonsoft.Json.JsonConvert.SerializeObject(restoredHost.CreateSnapshot());
                    Assert.IsFalse(restoredHost.TryRestoreSnapshot(state, fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                    Assert.AreEqual(beforeRestore, Newtonsoft.Json.JsonConvert.SerializeObject(restoredHost.CreateSnapshot()));
                }
                finally
                {
                    sequence.ActionIds[0] = original; fixture.Source.SetEquipment(equipment);
                    restoredHost.Stop(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame)); fixture.AdvanceWorldOnly();
                }
            }
        }

        [Test]
        public void SameCharacterCanCompileAnotherActualOpenerWithoutReusingOriginalBinding()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var w = Workspace(CharacterTestTacticalDslTrialTemplates.Create(), out string id); var originalProgram = Freeze(w, id, fixture);
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var set));
                var sequence = Array.Find(set.CommandSequences, s => s.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId);
                var original = sequence.ActionIds[0]; var equipment = fixture.Source.Equipment;
                try
                {
                    sequence.ActionIds[0] = new CharacterActionId(324);
                    fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(equipment.UseDefaultLoadout, set.EquipmentStateStableId));
                    using (var probe = new CharacterActionReferenceProbeSession(new CharacterActionId(324), fixture.Measurement.ReferenceDummy))
                    {
                        probe.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(equipment.UseDefaultLoadout, set.EquipmentStateStableId));
                        Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, fixture.Store.Refresh(probe, fixture.Measurement).Status);
                    }
                    Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(fixture.Probe, fixture.Store, fixture.Measurement, out var inputs, out string issue), issue);
                    Assert.AreEqual(originalProgram.TacticalSource.CharacterId, inputs.SourceCharacterId);
                    Assert.AreNotEqual(originalProgram.TacticalSource.ActionId, inputs.Action.ActionId.Value);
                    Assert.IsFalse(originalProgram.TacticalSource.ValidateBinding(fixture.Runtime, fixture.Source, out _));
                    Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(originalProgram.TacticalSource.CloneDocument(), inputs, out var compiled, out issue), issue);
                    var rebound = compiled.Program.WithTacticalSource(originalProgram.TacticalSource.Rebind(inputs, compiled.SourceLocations));
                    Assert.AreEqual(324, rebound.TacticalSource.ActionId);
                    Assert.AreEqual(301, originalProgram.TacticalSource.ActionId);
                    Assert.AreNotEqual(originalProgram.SemanticHash, rebound.SemanticHash);
                    Assert.IsTrue(rebound.TacticalSource.ValidateBinding(fixture.Runtime, fixture.Source, out issue), issue);
                }
                finally { sequence.ActionIds[0] = original; fixture.Source.SetEquipment(equipment); }
            }
        }

        [Test]
        public void FrozenHealthObservationDoesNotReadLaterEntityHealth()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                fixture.Source.SyncCombatHealth(100, 20);
                fixture.Facts.CaptureSelfHealth(fixture.Source, 40);
                var frozenValue = CharacterTestRuntimeAiLabFactCatalog.ReadValue(CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio, fixture.Facts);
                fixture.Source.SyncCombatHealth(100, 80);
                Assert.AreEqual(0.2f, frozenValue.Number, 0.0001f);
                Assert.AreEqual(0.2f, CharacterTestRuntimeAiLabFactCatalog.ReadValue(CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio, fixture.Facts).Number, 0.0001f);
                fixture.Facts.CaptureSelfHealth(fixture.Source, 41);
                Assert.AreEqual(0.8f, CharacterTestRuntimeAiLabFactCatalog.ReadValue(CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio, fixture.Facts).Number, 0.0001f);
                Assert.AreEqual(0.2f, frozenValue.Number, 0.0001f);
            }
        }
    }
}
