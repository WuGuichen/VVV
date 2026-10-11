using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardRebindRuntimeTests
    {
        [Test]
        public void FailedDifferentBindingCannotStartAndRepairRunsTheNewFixedAction()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                Assert.IsTrue(fixture.Runtime.ActionRuntime.TryGetEntityActionSet(fixture.Source.Id, out var equipmentB));
                var sequenceB = Array.Find(equipmentB.CommandSequences, s => s.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId);
                Assert.IsNotNull(sequenceB);
                var originalOpener = sequenceB.ActionIds[0];
                var originalEquipment = fixture.Source.Equipment;
                try
                {
                    Assert.IsTrue(fixture.Card.TryCompile(fixture.Definition), fixture.Card.LastIssue);
                    var retainedA = fixture.Card.Compilation;
                    Assert.AreEqual(originalOpener.Value, retainedA.ActionId);
                    Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                    for (int i = 0; i < 4; i++) fixture.Step();
                    fixture.StopAndDrain();

                    // Explicit in-memory loadout-revision fixture, not an authored asset edit.
                    // A/B retain the same physical default weapon and stable equipment key;
                    // B changes its functional opener, so comparing labels alone would be wrong.
                    sequenceB.ActionIds[0] = new CharacterActionId(324);
                    fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(originalEquipment.UseDefaultLoadout, equipmentB.EquipmentStateStableId));
                    Assert.IsFalse(fixture.Card.TryCompile(fixture.Definition), "The fixture cache has no summary for B's real opener yet.");
                    StringAssert.Contains("Missing", fixture.Card.LastIssue);
                    Assert.AreSame(retainedA, fixture.Card.Compilation);
                    int pending = fixture.Commands.CommandBuffer.PendingCount;
                    Assert.IsFalse(fixture.Start());
                    Assert.AreEqual(pending, fixture.Commands.CommandBuffer.PendingCount);
                    Assert.AreEqual(equipmentB.EquipmentStateStableId, fixture.Source.Equipment.EquipmentStateStableId,
                        "Compilation failure must not roll the applied equipment back to A.");
                    Assert.AreEqual(new CharacterActionId(324), sequenceB.ActionIds[0], "Failed compilation cannot roll the applied functional binding back to A.");
                    using (var measurement = new CharacterActionReferenceProbeSession(new CharacterActionId(324), fixture.Measurement.ReferenceDummy))
                    {
                        measurement.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(originalEquipment.UseDefaultLoadout, equipmentB.EquipmentStateStableId));
                        var repaired = fixture.Store.Refresh(measurement, fixture.Measurement);
                        Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, repaired.Status, repaired.Issue);
                    }
                    Assert.IsTrue(fixture.Card.TryCompile(fixture.Definition), fixture.Card.LastIssue);
                    Assert.AreEqual(324, fixture.Card.Compilation.ActionId);
                    Assert.AreNotEqual(retainedA.DependencyFingerprint, fixture.Card.Compilation.DependencyFingerprint);
                    Assert.AreEqual(originalOpener.Value, retainedA.Program.Execution.Actions[0].ActionId, "Replacing a binding cannot mutate the retained shared program.");
                    fixture.PlaceTarget(fixture.Card.Compilation.Summary.VerifiedOptimalMillimeters.Value);
                    fixture.AdvanceWorldOnly();
                    fixture.Samples.Clear();
                    Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                    for (int i = 0; i < 100 && fixture.Card.IsExecuting; i++) fixture.Step();
                    Assert.IsFalse(fixture.Card.IsExecuting, fixture.Card.LastIssue);
                    bool playedB = false;
                    foreach (var sample in fixture.Samples)
                    {
                        if (sample.ActionInstance <= 0) continue;
                        Assert.AreEqual(324, sample.ActionId, "No A action may leak into the repaired B execution.");
                        playedB = true;
                    }
                    Assert.IsTrue(playedB);
                    string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/Task03ExecutionEvidence"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "failed-binding-repair-runtime.json"), JsonConvert.SerializeObject(new
                    {
                        passed = true, loadoutFixture = "in-memory sword LightAttack opener changed to real action 324; restored after test",
                        oldAction = originalOpener.Value, newAction = 324, oldProgram = retainedA.Program.Execution.Identity,
                        newProgram = fixture.Card.Compilation.Program.Execution.Identity, samples = fixture.Samples
                    }, Formatting.Indented));
                }
                finally
                {
                    sequenceB.ActionIds[0] = originalOpener;
                    fixture.Source.SetEquipment(originalEquipment);
                }
            }
        }
    }
}
