using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardAlternativeTests
    {
        [Test]
        public void SameCardCompilesTwoRealOpenersAgainstExplicitOffsetReferenceDummy()
        {
            // Test geometry, not the default human reference: height includes both 2200 mm hemispheres.
            var dummy = new CharacterActionReferenceDummy(2, 5000, 2200, 0, 2500, -4000);
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 100, 0, 10000, dummy);
            var definition = new CharacterTestTacticalCardDefinition("接近并普攻一次", CharacterTestTacticalCardApproach.OptimalRange);
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/Task03ExecutionEvidence"));
            Directory.CreateDirectory(directory);
            string measurementDirectory = Path.Combine(directory, "alternative-metrics", Guid.NewGuid().ToString("N"));
            var rows = new JArray();
            var report = new JObject
            {
                ["evidenceKind"] = "formal-measurement-and-compilation-only",
                ["runtimeCardExecutionVerified"] = false,
                ["source"] = JObject.Parse(definition.Write()),
                ["referenceDummy"] = new JObject
                {
                    ["version"] = dummy.Version, ["heightMillimeters"] = dummy.HeightMillimeters,
                    ["radiusMillimeters"] = dummy.RadiusMillimeters, ["centerXMillimeters"] = dummy.CenterXMillimeters,
                    ["centerYMillimeters"] = dummy.CenterYMillimeters, ["centerZMillimeters"] = dummy.CenterZMillimeters
                },
                ["stepMillimeters"] = 100, ["searchMinMillimeters"] = 0, ["searchMaxMillimeters"] = 10000,
                ["measurementDirectory"] = measurementDirectory,
                ["loadouts"] = rows, ["passed"] = false
            };
            try
            {
                var first = MeasureAndCompile(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    definition, measurement, measurementDirectory, rows);
                var second = MeasureAndCompile(CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                    definition, measurement, measurementDirectory, rows);
                Assert.AreEqual(first.SourceFingerprint, second.SourceFingerprint, "Only the loadout changes.");
                Assert.AreNotEqual(first.ActionId, second.ActionId);
                Assert.AreNotEqual(first.StartupEndFrame, second.StartupEndFrame);
                Assert.AreNotEqual(first.Program.Execution.Timelines[0].DurationFrames,
                    second.Program.Execution.Timelines[0].DurationFrames);
                Assert.AreNotEqual(first.DependencyFingerprint, second.DependencyFingerprint);
                Assert.AreNotEqual(first.Program.Execution.Identity, second.Program.Execution.Identity);
                report["passed"] = true;
            }
            catch (Exception error)
            {
                report["failure"] = error.ToString();
                throw;
            }
            finally
            {
                string json = report.ToString(Formatting.Indented);
                File.WriteAllText(Path.Combine(directory, "alternative-loadouts.json"), json);
                TestContext.WriteLine(json);
            }
        }

        private static CharacterTestTacticalCardCompilation MeasureAndCompile(int characterId,
            CharacterTestTacticalCardDefinition definition, CharacterActionSpatialMeasurement measurement,
            string evidenceDirectory, JArray rows)
        {
            var row = new JObject { ["sourceCharacterId"] = characterId };
            rows.Add(row);
            using (var session = CharacterActionReferenceProbeSession.ForCurrentOpener(characterId, measurement.ReferenceDummy))
            {
                int actionId = session.Action.ActionId.Value;
                row["requestedActionId"] = actionId;
                // Each run measures into its own derived directory; previous evidence may be open
                // in a report reader and must not become a shared mutable test fixture.
                var store = new CharacterActionTacticalMetricsStore(evidenceDirectory);
                var measured = store.Refresh(session, measurement);
                row["loadStatus"] = measured.Status.ToString();
                row["loadIssue"] = measured.Issue;
                if (measured.Metrics?.SpatialSummary != null)
                    row["summary"] = JObject.Parse(CharacterActionTacticalSummaryJson.Write(measured.Metrics.SpatialSummary));
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
                Assert.IsNotNull(measured.Metrics);
                var summary = measured.Metrics.SpatialSummary;
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateSummary(summary, out string issue), issue);
                Assert.GreaterOrEqual(summary.MinDistance.Value, 0d,
                    "A measured opener may hit at contact; positive-minimum retreat has its own dedicated geometry test.");
                Assert.IsTrue(measured.TryGetDefaultSurfaceDistance(out double optimal), measured.Issue);
                Assert.IsTrue(session.TryEvaluate((int)Math.Round(optimal * 1000d), out var opportunity, out issue), issue);
                Assert.IsTrue(opportunity.CanHitFromCurrentPose, "Measured optimum must pass the formal evaluator.");
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryPrepare(session.Runtime.ActionRuntime, session.Source,
                    session.Runtime.PhysicsRuntime, session.Runtime.CombatTraceSystem.BakedTraceSource,
                    session.Runtime.CombatTraceSystem.WeaponTraceBindingResolver, store, measurement,
                    out var inputs, out issue), issue);
                Assert.AreEqual(actionId, inputs.Action.ActionId.Value, "The requested candidate must be its formal LightAttack opener.");
                Assert.IsTrue(CharacterTestTacticalCardCompiler.TryCompile(definition, inputs, out var compiled, out issue), issue);
                var program = compiled.Program.Execution;
                Assert.AreEqual(1, program.Actions.Length);
                Assert.AreEqual(actionId, program.Actions[0].ActionId);
                Assert.AreEqual(1, program.Movements.Length);
                var movement = program.Movements[0];
                Assert.IsTrue(movement.IsOptimalRange);
                Assert.AreEqual((float)summary.MinDistance.Value, movement.Minimum);
                Assert.AreEqual((float)summary.MaxDistance.Value, movement.Maximum);
                Assert.AreEqual((float)summary.OptimalDistance.Value, movement.Optimal);
                Assert.AreEqual(1 + inputs.DurationFrames, program.Timelines[0].DurationFrames);
                int movementEnd = -1;
                int actionStart = -1;
                int actionEnd = -1;
                for (int i = 0; i < program.Events.Length; i++)
                {
                    string eventId = compiled.Program.ExecutionDebugMap.Event(i);
                    if (eventId == "approach") movementEnd = program.Events[i].EndFrame;
                    if (eventId == "attack")
                    {
                        actionStart = program.Events[i].StartFrame;
                        actionEnd = program.Events[i].EndFrame;
                    }
                }
                Assert.AreEqual(2 + inputs.StartupEndFrame, movementEnd);
                Assert.AreEqual(1, actionStart);
                Assert.AreEqual(1 + inputs.DurationFrames, actionEnd);
                row["sourceFingerprint"] = compiled.SourceFingerprint;
                row["dependencyFingerprint"] = compiled.DependencyFingerprint;
                row["executionIdentity"] = program.Identity;
                row["startupEndFrame"] = inputs.StartupEndFrame;
                row["movementEndFrame"] = movementEnd;
                row["actionStartFrame"] = actionStart;
                row["actionEndFrame"] = actionEnd;
                row["compiledMinimum"] = movement.Minimum;
                row["compiledMaximum"] = movement.Maximum;
                row["compiledOptimal"] = movement.Optimal;
                row["description"] = compiled.Describe();
                return compiled;
            }
        }
    }
}
