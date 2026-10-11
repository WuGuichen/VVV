using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalAuthoringBindingTests
    {
        [Test]
        public void DefaultEquipmentMarkerPreservesTheSpawnResolvedOpener()
        {
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, measurement.ReferenceDummy,
                new CharacterTestCharacterEquipmentComponent(true)))
            {
                Assert.AreEqual(301, probe.Action.ActionId.Value);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryDescribe(probe.Runtime, probe.Source,
                    measurement, out var request, out string issue), issue);
                Assert.IsNotEmpty(request.ActionSetId);
            }
        }

        [Test]
        public void NewSourceUsesTheExistingBindingAndRejectsAppliedInvalidOpener()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                var binding = fixture.Binding;
                Assert.IsTrue(binding.TryCompileAuthoring(source), binding.LastIssue);
                var retained = binding.RetainedCompilation;
                var state = binding.CaptureState();
                Assert.IsTrue(binding.TryAcquireAuthoring(source, out _), binding.LastIssue);
                try
                {
                    Assert.IsFalse(binding.TryCompileAuthoring(source));
                    Assert.IsFalse(binding.TryValidateAuthoringRestore(source, state));
                    Assert.AreSame(retained, binding.RetainedCompilation);
                }
                finally { binding.Release(); }
                Assert.IsTrue(binding.TryValidateAuthoringRestore(source, state), binding.LastIssue);
                var changed = source.Clone();
                changed.Timelines[0].Events[0].End.OffsetFrames = 4;
                Assert.IsFalse(binding.TryAcquireAuthoring(changed, out _));
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var set));
                var sequence = Array.Find(set.CommandSequences, item => item.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId);
                var original = sequence.ActionIds[0];
                var originalEquipment = fixture.Source.Equipment;
                try
                {
                    sequence.ActionIds[0] = new CharacterActionId(324);
                    fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(originalEquipment.UseDefaultLoadout, set.EquipmentStateStableId));
                    Assert.IsFalse(binding.TryCompileAuthoring(source));
                    StringAssert.Contains("Missing", binding.LastIssue);
                    Assert.AreSame(retained, binding.RetainedCompilation);
                    Assert.IsFalse(binding.TryAcquireAuthoring(source, out _));
                    Assert.IsFalse(binding.TryValidateAuthoringRestore(source, state));
                    Assert.AreEqual(new CharacterActionId(324), sequence.ActionIds[0]);
                    using (var probe = new CharacterActionReferenceProbeSession(new CharacterActionId(324), fixture.Measurement.ReferenceDummy))
                    {
                        probe.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(originalEquipment.UseDefaultLoadout, set.EquipmentStateStableId));
                        Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, fixture.Store.Refresh(probe, fixture.Measurement).Status);
                    }
                    Assert.IsTrue(binding.TryCompileAuthoring(source), binding.LastIssue);
                    Assert.AreEqual(324, binding.RetainedCompilation.Program.Execution.Actions[0].ActionId);
                    Assert.AreEqual(301, retained.Program.Execution.Actions[0].ActionId);
                    Assert.IsFalse(binding.TryValidateAuthoringRestore(source, state));
                    Assert.IsTrue(binding.TryAcquireAuthoring(source, out _), binding.LastIssue);
                    binding.Release();
                }
                finally { binding.Release(); sequence.ActionIds[0] = original; fixture.Source.SetEquipment(originalEquipment); }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentCharacterOpenerRebuildsMissingSummaryAndCompilesItsOwnTiming(bool second)
        {
            int character = second ? CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003")
                : CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            int expectedAction = second ? 940000 : 301;
            var measurement = CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence",
                "summary-" + Guid.NewGuid().ToString("N")));
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(character, measurement.ReferenceDummy))
            {
                Assert.AreEqual(expectedAction, probe.Action.ActionId.Value);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryDescribe(probe.Runtime, probe.Source, measurement, out var request, out string issue), issue);
                var store = new CharacterActionTacticalMetricsStore(directory);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Missing,
                    store.Query(CharacterActionTacticalInput.FromSession(probe), measurement).Status);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryRebuild(request, store, out var result, out issue), issue);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, result.Status);
                Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(probe, store, measurement, out var inputs, out issue), issue);
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                source.Timelines[0].Events[0].End.OffsetFrames = 4;
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var diagnostics), string.Join("\n", diagnostics));
                var movement = compiled.Events.ToArray().Single(e => e.EventId == "approach");
                Assert.AreEqual(second ? 41 : 23, movement.EndFrame);
                Assert.AreEqual(second ? 121 : 77, compiled.Events.ToArray().Single(e => e.EventId == "attack").EndFrame);
                Assert.AreEqual(expectedAction, compiled.Program.Execution.Actions[0].ActionId);
                Assert.AreEqual(character, compiled.SourceCharacterId);
                Assert.AreEqual(request.ActionSetId, compiled.ActionSetId);
                Assert.AreEqual(expectedAction, compiled.ActionId);

                // A changed preparation identity cannot overwrite the just-validated summary.
                var staleRequest = new CharacterTestTacticalSummaryRequest(request.CharacterId, request.ActionId,
                    request.ActionSetId, request.Equipment, "different-loaded-content", request.Measurement);
                string cachePath = store.GetPath(character, probe.Action.ActionId);
                string before = File.ReadAllText(cachePath);
                Assert.IsFalse(CharacterTestTacticalSummaryPreparation.TryRebuild(staleRequest, store, out _, out issue));
                StringAssert.Contains("LoadedProviderMismatch", issue);
                Assert.AreEqual(before, File.ReadAllText(cachePath));
                File.WriteAllText(Path.Combine(directory, "compilation-evidence.json"), JsonConvert.SerializeObject(new
                {
                    evidenceKind = "Task04-T4-2-measurement-and-compilation",
                    character, action = expectedAction, source = CharacterTestTacticalAuthoringJson.Write(source),
                    sourceFingerprint = compiled.SourceFingerprint, dependencies = compiled.DependencyFingerprint,
                    execution = compiled.Program.Execution.Identity, moveEnd = movement.EndFrame,
                    actionEnd = compiled.Events.ToArray().Single(e => e.EventId == "attack").EndFrame,
                    runtimeOrUiAcceptance = false
                }, Formatting.Indented));
            }
        }
    }
}
