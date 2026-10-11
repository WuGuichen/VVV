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
    public sealed class CharacterTestTacticalAuthoringAcceptanceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ActualActionRevisionBecomesStaleAndExplicitRebuildRestoresCurrentInputs(bool second)
        {
            int character = second ? CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003")
                : CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            var measurement = CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence",
                "stale-" + Guid.NewGuid().ToString("N")));
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(character, measurement.ReferenceDummy))
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                var store = new CharacterActionTacticalMetricsStore(directory);
                int startupIndex = Array.FindIndex(probe.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Startup);
                int activeIndex = Array.FindIndex(probe.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Active);
                Assert.GreaterOrEqual(startupIndex, 0);
                Assert.GreaterOrEqual(activeIndex, 0);
                var startup = probe.Action.Phases[startupIndex];
                var active = probe.Action.Phases[activeIndex];
                CharacterTestTacticalCardInputs previousInputs = null;
                string previousHash = null;
                // Controlled prior loaded revision. Real action phase content changes; no fake cache hash.
                try
                {
                    probe.Action.Phases[startupIndex] = new CharacterActionPhase(startup.Kind, startup.StartFrame,
                        startup.EndFrame - 1, startup.CombatPhaseAnchor, startup.RequiresCombatPhaseMatch);
                    probe.Action.Phases[activeIndex] = new CharacterActionPhase(active.Kind, active.StartFrame - 1,
                        active.EndFrame, active.CombatPhaseAnchor, active.RequiresCombatPhaseMatch);
                    var measured = store.Refresh(probe, measurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
                    Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(probe, store, measurement, out previousInputs, out string issue), issue);
                    previousHash = previousInputs.DependencyFingerprint;
                    Assert.AreEqual(startup.EndFrame - 1, previousInputs.StartupEndFrame);
                }
                finally
                {
                    probe.Action.Phases[startupIndex] = startup;
                    probe.Action.Phases[activeIndex] = active;
                }

                var stale = store.Query(CharacterActionTacticalInput.FromSession(probe), measurement);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Stale, stale.Status);
                Assert.IsNull(stale.Metrics);
                Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(source, previousInputs, out var invalid, out var diagnostics));
                Assert.IsNull(invalid);
                Assert.That(diagnostics.Any(d => d.Code == "PreparedInputsStale"), Is.True);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryDescribe(probe.Runtime, probe.Source, measurement, out var request, out string currentIssue), currentIssue);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryRebuild(request, store, out _, out currentIssue), currentIssue);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,
                    store.Query(CharacterActionTacticalInput.FromSession(probe), measurement).Status);
                Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(probe, store, measurement, out var current, out currentIssue), currentIssue);
                Assert.AreNotEqual(previousHash, current.DependencyFingerprint);
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, current, out var compiled, out diagnostics), string.Join("\n", diagnostics));
                Assert.AreEqual(second ? 37 : 19, compiled.Events.ToArray().Single(e => e.EventId == "approach").EndFrame);
                File.WriteAllText(Path.Combine(directory, "stale-rebuild.json"), JsonConvert.SerializeObject(new
                {
                    evidenceKind = "actual-action-phase-revision-stale-rebuild",
                    character, action = request.ActionId, oldStartupEnd = startup.EndFrame - 1,
                    currentStartupEnd = current.StartupEndFrame, previousHash,
                    currentHash = current.DependencyFingerprint, program = compiled.Program.Execution.Identity,
                    staleStatus = stale.Status.ToString(), fixtureRestored = true, authoredAssetsModified = false,
                    runtimeOrUiAcceptance = false
                }, Formatting.Indented));
            }
        }

        [Test]
        public void InvalidSourcesNeverReturnPartialProgramsAndFailuresDoNotInventCycles()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                foreach (string invalid in new[] { "slot", "negative", "range", "overflow", "cross-timeline", "duplicate-gate" })
                {
                    var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                    var events = source.Timelines[0].Events;
                    var approach = events.Single(e => e.EventId == "approach");
                    var attack = events.Single(e => e.EventId == "attack");
                    string expected;
                    switch (invalid)
                    {
                        case "slot": source.Slots.Clear(); expected = "SlotMissing"; break;
                        case "negative": approach.End.OffsetFrames = -1000; expected = "NegativeResolvedFrame"; break;
                        case "range": approach.End = CharacterTestTacticalAuthoringTemplates.Absolute(0); expected = "InvalidRange"; break;
                        case "overflow": attack.Start = CharacterTestTacticalAuthoringTemplates.Absolute(int.MaxValue); expected = "FrameOverflow"; break;
                        case "cross-timeline":
                            events.Remove(attack);
                            source.Timelines.Add(new CharacterTestTacticalAuthoringTimeline { TimelineId = "other", Events = { attack } });
                            expected = "CrossTimelineReference"; break;
                        default:
                            events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "other-gate",
                                TimingKind = "Instant", Start = CharacterTestTacticalAuthoringTemplates.Absolute(0),
                                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" } });
                            expected = "DuplicateGate"; break;
                    }
                    string before = CharacterTestTacticalAuthoringJson.Write(source);
                    Assert.IsFalse(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out var compiled, out var diagnostics), invalid);
                    Assert.IsNull(compiled, invalid);
                    Assert.That(diagnostics.Any(d => d.Code == expected), Is.True, invalid + ": " + string.Join("\n", diagnostics));
                    Assert.That(diagnostics.Any(d => d.Code == "AnchorCycle"), Is.False, "Arithmetic failure must not leave visiting state latched.");
                    Assert.AreEqual(before, CharacterTestTacticalAuthoringJson.Write(source));
                }
            }
        }

        [Test]
        public void EventSignalsResolveStableIdentityAfterSamplingOrderChanges()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "signal-exit",
                    Start = CharacterTestTacticalAuthoringTemplates.Absolute(0),
                    End = CharacterTestTacticalAuthoringTemplates.Anchor("attack", "ActionEnd"),
                    Priority = 100, Operation = new CharacterTestTacticalAuthoringOperation { Kind = "ReturnToPlanner" },
                    ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "exit.signal",
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                        SourceEventId = "attack", Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed } });
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out var compiled, out var diagnostics), string.Join("\n", diagnostics));
                var signal = compiled.Program.Execution.Conditions.ToArray().Single(c =>
                    c.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal);
                Assert.AreEqual("attack", compiled.Program.ExecutionDebugMap.Event(signal.SourceEvent));
                Assert.AreNotEqual(3, signal.SourceEvent, "The authored array index is not an execution identity.");
            }
        }
    }
}
