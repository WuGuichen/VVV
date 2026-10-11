using System;
using System.IO;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    public sealed class RuntimeAiLabAuthoringUxFlowScenarioTests
    {
        [Test]
        public void Descriptor_UsesStableIdPlayModeAndExplicitExternalEvidence()
        {
            var scenario = new RuntimeAiLabAuthoringUxFlowScenario();

            Assert.That(
                scenario.Descriptor.ScenarioId.Value,
                Is.EqualTo(RuntimeAiLabAuthoringUxFlowScenario.ScenarioId));
            Assert.That(
                scenario.Descriptor.ExecutionProfile,
                Is.EqualTo(TriggerTestExecutionProfile.PlayMode));
            Assert.That(scenario.Descriptor.MaxFrames, Is.EqualTo(1));
            Assert.That(scenario.Descriptor.ExternalChecks.Count, Is.EqualTo(3));
            Assert.That(
                scenario.Descriptor.ExternalChecks[0].CheckId,
                Is.EqualTo("runtime-ai-lab-authoring-live-command-flow"));
            Assert.That(
                scenario.Descriptor.ExternalChecks[1].CheckId,
                Is.EqualTo("runtime-ai-lab-authoring-timed-autosave-and-f0"));
            Assert.That(
                scenario.Descriptor.ExternalChecks[2].CheckId,
                Is.EqualTo("runtime-ai-lab-authoring-popup-input-and-layout"));
        }

        [Test]
        public void Build_RegistersTypedSignalFunctionAndIndependentProbeAssertion()
        {
            var scenario = new RuntimeAiLabAuthoringUxFlowScenario();
            var builder = new TriggerTestScenarioBuilder(scenario.Descriptor);
            scenario.Build(builder);

            TriggerTestScenarioPlan plan = builder.BuildPlan();

            Assert.That(plan.StepCount, Is.EqualTo(1));
            Assert.That(plan.AssertionCount, Is.EqualTo(3));
            Assert.That(
                plan.GetStep(0).Step,
                Is.TypeOf<TriggerTestSubmitSignalsStep>());
            Assert.That(
                plan.GetAssertion(2).Assertion,
                Is.SameAs(RuntimeAiLabAuthoringUxFlowAssertion.Instance));
            Assert.That(plan.TryGetCall(1, out TriggerTestFunctionCallSpec call), Is.True);
            Assert.That(call.FunctionId, Is.EqualTo(RuntimeAiLabAuthoringUxFlowFunction.Id));
            Assert.That(
                call.Arguments,
                Is.SameAs(RuntimeAiLabAuthoringUxFlowArguments.Instance));
            Assert.That(
                plan.TryGetFunction(call.FunctionId, out ITriggerTestFunction function),
                Is.True);
            Assert.That(function.Capability, Is.EqualTo(TriggerTestFunctionCapability.UnityAdapter));
        }

        [Test]
        public void IsolatedCore_UsesOnlyTempAuthoritiesAndPreservesProductionHashes()
        {
            using (var probe = new RuntimeAiLabAuthoringUxFlowProbe())
            {
                string normalizedTemp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(
                    Path.GetFullPath(probe.TempRoot).StartsWith(
                        normalizedTemp,
                        StringComparison.OrdinalIgnoreCase),
                    Is.True);
                Assert.That(probe.AllWritableAuthoritiesUseTemp, Is.True);

                Assert.That(
                    probe.TryRunIsolatedCore(out string executionDiagnostic),
                    Is.True,
                    executionDiagnostic);
                Assert.That(
                    probe.TryValidateCore(out string probeDiagnostic),
                    Is.True,
                    probeDiagnostic);
                Assert.That(probe.ProductionFootprintValid, Is.True);
                Assert.That(probe.ProductionUnchangedAfterCore, Is.True);
                Assert.That(probe.TempPublishedWriteCount, Is.EqualTo(1));
                Assert.That(probe.PublishPreviewCancelledWithoutWrite, Is.True);
                Assert.That(probe.ExperimentPublishUnavailable, Is.True);
            }
        }

        [Test]
        public void DescriptorWithSuccessfulInternalCore_RemainsPendingExternalEvidence()
        {
            var scenario = new RuntimeAiLabAuthoringUxFlowScenario();

            TriggerTestRunStatus projected = scenario.Descriptor.ExternalChecks.Count > 0
                ? TriggerTestRunStatus.PendingExternalEvidence
                : TriggerTestRunStatus.Passed;

            Assert.That(projected, Is.EqualTo(TriggerTestRunStatus.PendingExternalEvidence));
        }
    }
}
