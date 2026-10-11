using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardSessionRestoreTests
    {
        [Test]
        public void RestoreRequiresExplicitActionBindingWithoutEmittingCommands()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var saved = fixture.Card.CaptureState();
                var binding = new CharacterTestTacticalCardBinding(new CharacterTestTacticalCardRuntimeInputs(
                    fixture.Runtime, fixture.Source.Id.Value, fixture.Store, fixture.Measurement));
                var restored = new CharacterTestTacticalCardSession(fixture.Runtime, fixture.Source.Id.Value,
                    binding, fixture.Commands.Enqueue);
                int before = fixture.Commands.CommandBuffer.PendingCount;
                Assert.IsFalse(binding.TryCompile(fixture.Definition));
                Assert.AreEqual("Action:ExplicitSelectionRequired", binding.LastIssue);
                Assert.IsFalse(restored.TryRestore(fixture.Definition, saved, fixture.Facts,
                    new RuntimeFrame(fixture.NextFrame)));
                Assert.AreEqual(before, fixture.Commands.CommandBuffer.PendingCount);
                Assert.IsFalse(restored.IsExecuting);
                Assert.IsTrue(binding.TryCompile(fixture.Definition, fixture.Probe.Action.ActionId.Value), binding.LastIssue);
                Assert.IsTrue(restored.TryRestore(fixture.Definition, saved, fixture.Facts,
                    new RuntimeFrame(fixture.NextFrame)), restored.LastIssue);
                Assert.AreEqual(before, fixture.Commands.CommandBuffer.PendingCount);
                Assert.AreEqual(Hash(fixture.Card), Hash(restored));
            }
        }

        [Test]
        public void CompiledUnusedBindingRoundTripsWithoutCreatingExecutionOrCommands()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var saved = JsonConvert.DeserializeObject<CharacterTestTacticalCardSessionState>(
                    JsonConvert.SerializeObject(fixture.Card.CaptureState()));
                Assert.IsNull(saved.Execution);
                var binding = new CharacterTestTacticalCardBinding(new CharacterTestTacticalCardRuntimeInputs(
                    fixture.Runtime, fixture.Source.Id.Value, fixture.Store, fixture.Measurement));
                Assert.IsTrue(binding.TryCompile(fixture.Definition, fixture.Probe.Action.ActionId.Value), binding.LastIssue);
                var restored = new CharacterTestTacticalCardSession(fixture.Runtime, fixture.Source.Id.Value, binding, fixture.Commands.Enqueue);
                int before = fixture.Commands.CommandBuffer.PendingCount;
                Assert.IsTrue(restored.TryRestore(fixture.Definition, saved, fixture.Facts, new RuntimeFrame(fixture.NextFrame)), restored.LastIssue);
                Assert.AreEqual(Hash(fixture.Card), Hash(restored));
                Assert.AreEqual(before, fixture.Commands.CommandBuffer.PendingCount);
                Assert.IsFalse(restored.IsExecuting);
                Assert.IsTrue(restored.TryStart(fixture.Definition, fixture.Facts, new RuntimeFrame(fixture.NextFrame)), restored.LastIssue);
                restored.Stop(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                fixture.AdvanceWorldOnly();
                Assert.IsFalse(restored.IsExecuting);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestoredCardProducesIdenticalCommandsAndHashAgainstTheSameFormalWorld(bool duringAction)
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(duringAction
                ? CharacterTestTacticalCardApproach.None : CharacterTestTacticalCardApproach.OptimalRange))
            {
                int optimum = fixture.Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value;
                fixture.PlaceTarget(optimum + (duringAction ? 0 : 40), duringAction ? 0 : 20000);
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                if (duringAction)
                {
                    for (int i = 0; i < 12 && fixture.Card.OwnedActionInstanceId == 0; i++) fixture.Step();
                    Assert.Greater(fixture.Card.OwnedActionInstanceId, 0);
                    for (int i = 0; i < 4; i++) fixture.Step();
                }
                else
                {
                    for (int i = 0; i < 12; i++) fixture.Step();
                    Assert.AreEqual(0, fixture.Card.LocalFrame, "The elevated live target must keep the real opportunity gate closed.");
                }
                var snapshot = JsonConvert.DeserializeObject<CharacterTestTacticalCardSessionState>(
                    JsonConvert.SerializeObject(fixture.Card.CaptureState()));
                var shadowCommands = new List<RuntimeCommand>();
                var current = new CharacterTestTacticalCardRuntimeInputs(fixture.Runtime, fixture.Source.Id.Value,
                    fixture.Store, fixture.Measurement);
                var binding = new CharacterTestTacticalCardBinding(current);
                Assert.IsTrue(binding.TryCompile(fixture.Definition, fixture.Probe.Action.ActionId.Value), binding.LastIssue);
                var restored = new CharacterTestTacticalCardSession(fixture.Runtime, fixture.Source.Id.Value, binding,
                    command => { shadowCommands.Add(command); return RuntimeCommandValidationResult.Accepted(command); });
                fixture.RefreshFacts();
                Assert.IsTrue(restored.TryRestore(fixture.Definition, snapshot, fixture.Facts,
                    new RuntimeFrame(fixture.NextFrame - 1)), restored.LastIssue);
                Assert.AreEqual(Hash(fixture.Card), Hash(restored), "JSON restore must preserve stopped/control and pending state.");
                int continuation = duringAction ? fixture.Inputs.DurationFrames + 12 : 10;
                for (int i = 0; i < continuation; i++)
                {
                    shadowCommands.Clear();
                    fixture.RefreshFacts();
                    restored.Tick(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                    // Only the original stream is consumed by the real command and character modules.
                    // The restored shadow observes the same authoritative world at each sampling boundary.
                    fixture.Step();
                    Assert.AreEqual(fixture.Commands.LastDrainedCommands.Count, shadowCommands.Count, "command count at " + i);
                    for (int c = 0; c < shadowCommands.Count; c++)
                        Assert.AreEqual(fixture.Commands.LastDrainedCommands[c].WithSequence(0), shadowCommands[c].WithSequence(0));
                    Assert.AreEqual(Hash(fixture.Card), Hash(restored), "continued authority state at " + i);
                }
                if (duringAction)
                {
                    Assert.IsFalse(fixture.Card.IsExecuting);
                    Assert.IsFalse(restored.IsExecuting);
                }
                string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,
                    "../Library/Task03ExecutionEvidence"));
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, duringAction ? "restore-active-action.json" : "restore-blocked-gate.json"),
                    JsonConvert.SerializeObject(new { passed = true, duringAction, continuation,
                        evidence = "same-authority-world restored-card command/hash comparison; original stream formally consumed",
                        finalHash = Hash(restored), snapshot }, Formatting.Indented));
                fixture.StopAndDrain();
            }
        }

        [Test]
        public void StartRejectsAnInertActorWithoutStealingControlOwnership()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                Assert.IsTrue(fixture.Runtime.TrySetControlOwner(fixture.Source.Id.Value, CharacterTestCharacterControlOwner.None));
                int pending = fixture.Commands.CommandBuffer.PendingCount;
                Assert.IsFalse(fixture.Start());
                Assert.AreEqual("session:SourceNotCommandDriven", fixture.Card.LastIssue);
                Assert.AreEqual(CharacterTestCharacterControlOwner.None, fixture.Source.Control.Owner);
                Assert.AreEqual(pending, fixture.Commands.CommandBuffer.PendingCount);
                Assert.IsFalse(fixture.Card.IsExecuting);
            }
        }

        [Test]
        public void ActiveSnapshotCannotBypassChangedEquipmentOrForgeItsLifecycle()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                fixture.PlaceTarget(2000, 20000);
                Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                fixture.Step();
                var snapshot = fixture.Card.CaptureState();
                fixture.StopAndDrain();
                var malformed = JsonConvert.DeserializeObject<CharacterTestTacticalCardSessionState>(JsonConvert.SerializeObject(snapshot));
                malformed.Binding = new CharacterTestTacticalCardBindingState(malformed.Binding.Version,
                    malformed.Binding.ProgramIdentity, malformed.Binding.SourceFingerprint,
                    malformed.Binding.DependencyFingerprint, false);
                Assert.IsFalse(fixture.Card.TryRestore(fixture.Definition, malformed, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                Assert.AreEqual("session:SnapshotLifecycleMismatch", fixture.Card.LastIssue);
                fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(false, "task03.invalid-loadout"));
                int pending = fixture.Commands.CommandBuffer.PendingCount;
                Assert.IsFalse(fixture.Card.TryCompile(fixture.Definition));
                Assert.IsNotNull(fixture.Card.Compilation, "The old immutable program may still be retained.");
                Assert.IsFalse(fixture.Card.TryStart(fixture.Definition, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                Assert.IsFalse(fixture.Card.TryRestore(fixture.Definition, snapshot, fixture.Facts, new RuntimeFrame(fixture.NextFrame)));
                Assert.AreEqual(pending, fixture.Commands.CommandBuffer.PendingCount, "Rejected old bindings must emit no commands.");
                Assert.IsFalse(fixture.Card.IsExecuting);
            }
        }

        private static long Hash(CharacterTestTacticalCardSession session)
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority, 2);
            session.AddHash(hash);
            return hash.ToHash();
        }
    }
}
